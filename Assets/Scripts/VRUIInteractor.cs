using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

[AddComponentMenu("VR/VR UI Interactor")]
public class VRUIInteractor : MonoBehaviour
{
    public enum HandSide { Left, Right }

    [Header("Input")]
    public HandSide handSide = HandSide.Right;
    [Tooltip("Threshold for considering the trigger pressed (0..1). Lower to make trigger more sensitive.")]
    public float pressThreshold = 0.1f;
    [Tooltip("Use this for editor quick testing (maps to mouse left button).")]
    public bool allowMouseClickForEditor = true;

    [Header("Ray")]
    public Transform pointerOrigin; // usually controller or wrist/hand transform
    public float maxDistance = 10f;
    public LayerMask uiLayerMask = ~0;
    public Color laserColor = new Color(0.0f, 0.7f, 1.0f, 1.0f);
    public float laserWidth = 0.01f;
    public GameObject hitDotPrefab;

    [Header("UI")]
    [Tooltip("Optional override camera used for UI raycasts. If null, will use Camera.main or Canvas.worldCamera.")]
    public Camera uiCamera;

    // runtime
    LineRenderer m_line;
    GameObject m_hitDot;
    PointerEventData m_pointerEventData;
    List<RaycastResult> m_raycastResults = new List<RaycastResult>();
    GameObject m_currentUIObject;
    bool m_isPressed;

    void Awake()
    {
        if (pointerOrigin == null)
            pointerOrigin = transform;

        // create pointer event data using a temporary EventSystem reference
        if (EventSystem.current == null)
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        m_pointerEventData = new PointerEventData(EventSystem.current);

        // create laser
        m_line = gameObject.GetComponent<LineRenderer>();
        if (m_line == null)
            m_line = gameObject.AddComponent<LineRenderer>();

        m_line.positionCount = 2;
        m_line.startWidth = laserWidth;
        m_line.endWidth = laserWidth;
        m_line.material = new Material(Shader.Find("Sprites/Default"));
        m_line.startColor = laserColor;
        m_line.endColor = laserColor;
        m_line.useWorldSpace = true;
        m_line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        m_line.receiveShadows = false;
        m_line.numCapVertices = 4;

        if (hitDotPrefab != null)
            m_hitDot = Instantiate(hitDotPrefab);
        else
        {
            m_hitDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_hitDot.transform.localScale = Vector3.one * 0.02f;
            var mr = m_hitDot.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = new Material(Shader.Find("Standard"));
                mr.material.color = laserColor;
            }
            DestroyImmediate(m_hitDot.GetComponent<Collider>());
        }
        m_hitDot.SetActive(false);
    }

    void Update()
    {
        if (pointerOrigin == null)
            return;

        // read input (XR)
        bool isPressedThisFrame = ReadTriggerPressed();

        // compute ray origin/direction. Prefer XR device pose when available so controller rays follow devices.
        Vector3 rayOrigin = pointerOrigin.position;
        Vector3 rayDir = pointerOrigin.forward;
        Vector3 xrPos;
        Quaternion xrRot;
        if (TryGetXRNodePose(out xrPos, out xrRot))
        {
            rayOrigin = xrPos;
            rayDir = xrRot * Vector3.forward;
        }

        // cast a physics ray to find geometry / canvas
        Ray ray = new Ray(rayOrigin, rayDir);
        RaycastHit hitInfo;
        bool hit = Physics.Raycast(ray, out hitInfo, maxDistance, uiLayerMask);

        Vector3 hitWorldPos = rayOrigin + rayDir * maxDistance;
        float hitDistance = maxDistance;

        if (hit)
        {
            hitWorldPos = hitInfo.point;
            hitDistance = hitInfo.distance;
        }

        // draw laser
        m_line.SetPosition(0, pointerOrigin.position);
        m_line.SetPosition(1, hitWorldPos);

        // update hit dot
        m_hitDot.SetActive(true);
        m_hitDot.transform.position = hitWorldPos;
        m_hitDot.transform.localScale = Vector3.one * Mathf.Clamp(0.02f * (hitDistance / 2f), 0.01f, 0.05f);

        // perform UI raycast using GraphicRaycasters
        // determine UI camera
        Camera cam = uiCamera != null ? uiCamera : Camera.main;

        // if we hit a Canvas with a worldCamera override, prefer that
        Canvas hitCanvas = null;
        if (hit)
        {
            var go = hitInfo.collider.gameObject;
            hitCanvas = go.GetComponentInParent<Canvas>();
            if (hitCanvas != null && hitCanvas.worldCamera != null)
                cam = hitCanvas.worldCamera;
        }

        // screen position for pointer event
        Vector3 screenPoint = cam != null ? cam.WorldToScreenPoint(hitWorldPos) : new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

        // populate PointerEventData
        m_pointerEventData.Reset();
        m_pointerEventData.position = new Vector2(screenPoint.x, screenPoint.y);
        m_pointerEventData.pointerCurrentRaycast = new RaycastResult();

        // gather raycasters
        m_raycastResults.Clear();
        var raycasters = GameObject.FindObjectsOfType<GraphicRaycaster>();
        foreach (var rc in raycasters)
        {
            // skip non-worldspace canvases if that's desired — here we allow both
            var canvas = rc.GetComponent<Canvas>();
            Camera useCam = cam;
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera != null)
                useCam = canvas.worldCamera;

            // if camera null, GraphicRaycaster will still work (it uses camera if provided)
            rc.Raycast(m_pointerEventData, m_raycastResults);
            if (m_raycastResults.Count > 0)
                break;
        }

        GameObject newUIObject = null;
        if (m_raycastResults.Count > 0)
        {
            newUIObject = m_raycastResults[0].gameObject;
        }

        // handle pointer enter/exit
        if (newUIObject != m_currentUIObject)
        {
            if (m_currentUIObject != null)
                ExecuteEvents.Execute(m_currentUIObject, m_pointerEventData, ExecuteEvents.pointerExitHandler);

            m_currentUIObject = newUIObject;

            if (m_currentUIObject != null)
                ExecuteEvents.Execute(m_currentUIObject, m_pointerEventData, ExecuteEvents.pointerEnterHandler);
        }

        // handle press / release
        if (isPressedThisFrame && !m_isPressed)
        {
            // press down
            m_isPressed = true;
            if (m_currentUIObject != null)
            {
                ExecuteEvents.Execute(m_currentUIObject, m_pointerEventData, ExecuteEvents.pointerDownHandler);
                // also set as selected
                EventSystem.current.SetSelectedGameObject(m_currentUIObject);
            }
        }
        else if (!isPressedThisFrame && m_isPressed)
        {
            // release
            if (m_currentUIObject != null)
            {
                ExecuteEvents.Execute(m_currentUIObject, m_pointerEventData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(m_currentUIObject, m_pointerEventData, ExecuteEvents.pointerClickHandler);
            }
            m_isPressed = false;
        }
    }

    bool ReadTriggerPressed()
    {
        // Editor mouse fallback
        #if UNITY_EDITOR
        // Respect which input package is active. Use legacy Input only if enabled; otherwise use the new Input System when available.
        #if ENABLE_LEGACY_INPUT_MANAGER
        if (allowMouseClickForEditor && UnityEngine.Input.GetMouseButton(0))
            return true;
        #elif ENABLE_INPUT_SYSTEM
        try
        {
            // Use InputSystem's Mouse when available
            var mouseType = System.Type.GetType("UnityEngine.InputSystem.Mouse, UnityEngine.InputSystem");
            if (mouseType != null)
            {
                var currentProp = mouseType.GetProperty("current");
                var mouse = currentProp.GetValue(null, null);
                if (mouse != null)
                {
                    var leftProp = mouseType.GetProperty("leftButton");
                    var leftBtn = leftProp.GetValue(mouse, null);
                    if (leftBtn != null)
                    {
                        var btnType = leftBtn.GetType();
                        var isPressedProp = btnType.GetProperty("isPressed");
                        var isPressed = (bool)isPressedProp.GetValue(leftBtn, null);
                        if (isPressed && allowMouseClickForEditor)
                            return true;
                    }
                }
            }
        }
        catch { }
        #endif
        #endif

        // XR input
        XRNode node = handSide == HandSide.Left ? XRNode.LeftHand : XRNode.RightHand;
        var device = InputDevices.GetDeviceAtXRNode(node);

        if (device.isValid)
        {
            // Primary trigger often maps to triggerButton or trigger
            bool pressedBool;
            float pressedFloat;

            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out pressedBool))
            {
                if (pressedBool)
                {
                    Debug.Log($"[VRUIInteractor] Trigger button detected on {handSide}");
                    return true;
                }
            }

            if (device.TryGetFeatureValue(CommonUsages.trigger, out pressedFloat))
            {
                if (pressedFloat > pressThreshold)
                {
                    Debug.Log($"[VRUIInteractor] Trigger float {pressedFloat:F2} on {handSide} > {pressThreshold}");
                    return true;
                }
                else
                {
                    // helpful debug when users report lack of response
                    Debug.Log($"[VRUIInteractor] Trigger float {pressedFloat:F2} on {handSide} below threshold {pressThreshold}");
                }
            }

            // fallback to primaryButton (A/X etc) or gripButton
            if (device.TryGetFeatureValue(CommonUsages.primaryButton, out pressedBool) && pressedBool)
                return true;

            if (device.TryGetFeatureValue(CommonUsages.gripButton, out pressedBool) && pressedBool)
                return true;
        }

        return false;
    }

    void OnDisable()
    {
        if (m_hitDot != null)
            m_hitDot.SetActive(false);
    }

    bool TryGetXRNodePose(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        XRNode node = handSide == HandSide.Left ? XRNode.LeftHand : XRNode.RightHand;
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid)
        {
            Vector3 pos;
            Quaternion rot;
            bool gotPos = device.TryGetFeatureValue(CommonUsages.devicePosition, out pos);
            bool gotRot = device.TryGetFeatureValue(CommonUsages.deviceRotation, out rot);
            if (gotPos && gotRot)
            {
                position = pos;
                rotation = rot;
                return true;
            }
        }

        return false;
    }
}

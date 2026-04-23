using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

[AddComponentMenu("VR/VR UI Interactor")]
public class VRUIInteractor : MonoBehaviour
{
    public enum HandSide { Left, Right }

    [Header("Input")]
    public HandSide handSide = HandSide.Right;
    [Tooltip("Threshold for controller trigger (0..1).")]
    public float triggerThreshold = 0.1f;
    [Tooltip("Distance between thumb and index tips to register a pinch (meters).")]
    public float pinchDistance = 0.03f;

    [Header("Ray")]
    public float maxDistance = 10f;
    public LayerMask uiLayerMask = ~0;
    public Color laserColor = new Color(0.0f, 0.7f, 1.0f, 1.0f);
    public float laserWidth = 0.01f;

    [Header("UI")]
    public Camera uiCamera;

    private LineRenderer m_line;
    private GameObject m_hitDot;
    private PointerEventData m_pointerEventData;
    private List<RaycastResult> m_raycastResults = new List<RaycastResult>();
    private GameObject m_currentUIObject;
    private GameObject m_pointerPress;
    private bool m_isPressed;
    private XRNode m_xrNode;
    private XRHandSubsystem m_handSubsystem;

    void Awake()
    {
        m_xrNode = (handSide == HandSide.Left) ? XRNode.LeftHand : XRNode.RightHand;

        // Ensure EventSystem exists
        if (EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        m_pointerEventData = new PointerEventData(EventSystem.current);

        // Create line renderer for laser
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

        // Create hit indicator dot
        m_hitDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        m_hitDot.transform.localScale = Vector3.one * 0.02f;
        var mr = m_hitDot.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.material = new Material(Shader.Find("Standard"));
            mr.material.color = laserColor;
        }
        DestroyImmediate(m_hitDot.GetComponent<Collider>());
        m_hitDot.SetActive(false);

        // Get hand subsystem for OpenXR hands
        var handSubsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(handSubsystems);
        if (handSubsystems.Count > 0)
            m_handSubsystem = handSubsystems[0];
    }

    void Update()
    {
        Vector3 rayOrigin = transform.position;
        Vector3 rayDir = transform.forward;
        bool isPressedThisFrame = false;

        // Try to get OpenXR hand joints for finger pointing
        if (m_handSubsystem != null && m_handSubsystem.running)
        {
            XRHand hand = (handSide == HandSide.Left) ? m_handSubsystem.leftHand : m_handSubsystem.rightHand;
            if (hand.isTracked)
            {
                // Get index tip and index proximal for pointing direction
                if (hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexTipPose) &&
                    hand.GetJoint(XRHandJointID.IndexIntermediate).TryGetPose(out Pose indexMidPose))
                {
                    rayOrigin = indexTipPose.position;
                    rayDir = (indexTipPose.position - indexMidPose.position).normalized;
                }

                // Check for pinch (index + thumb distance)
                if (hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose iTip) &&
                    hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose tTip))
                {
                    float pinchDist = Vector3.Distance(iTip.position, tTip.position);
                    isPressedThisFrame = pinchDist < pinchDistance;
                }
            }
        }
        else
        {
            // Fallback: controller trigger
            var device = InputDevices.GetDeviceAtXRNode(m_xrNode);
            if (device.isValid)
            {
                // Try trigger float
                if (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
                {
                    isPressedThisFrame = triggerValue > triggerThreshold;
                }
                // Fallback to trigger button
                else if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButton))
                {
                    isPressedThisFrame = triggerButton;
                }
            }
        }

        // Cast ray
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

        // Draw laser
        m_line.SetPosition(0, rayOrigin);
        m_line.SetPosition(1, hitWorldPos);

        // Update hit dot
        m_hitDot.SetActive(true);
        m_hitDot.transform.position = hitWorldPos;
        m_hitDot.transform.localScale = Vector3.one * Mathf.Max(0.01f, hitDistance * 0.01f);

        // UI raycast
        Camera cam = uiCamera != null ? uiCamera : Camera.main;
        if (cam == null) return;

        Vector3 screenPoint = cam.WorldToScreenPoint(hitWorldPos);
        m_pointerEventData.Reset();
        m_pointerEventData.position = new Vector2(screenPoint.x, screenPoint.y);
        m_pointerEventData.button = PointerEventData.InputButton.Left;

        // Find UI elements at hit point
        m_raycastResults.Clear();
        var raycasters = GameObject.FindObjectsOfType<GraphicRaycaster>();
        foreach (var rc in raycasters)
        {
            rc.Raycast(m_pointerEventData, m_raycastResults);
            if (m_raycastResults.Count > 0)
            {
                m_pointerEventData.pointerCurrentRaycast = m_raycastResults[0];
                break;
            }
        }

        GameObject newUIObject = m_raycastResults.Count > 0 ? m_raycastResults[0].gameObject : null;

        // Handle pointer enter/exit
        if (newUIObject != m_currentUIObject)
        {
            if (m_currentUIObject != null)
                ExecuteEvents.Execute(m_currentUIObject, m_pointerEventData, ExecuteEvents.pointerExitHandler);

            m_currentUIObject = newUIObject;

            if (m_currentUIObject != null)
                ExecuteEvents.Execute(m_currentUIObject, m_pointerEventData, ExecuteEvents.pointerEnterHandler);
        }

        // Handle press/release
        if (isPressedThisFrame && !m_isPressed)
        {
            m_isPressed = true;
            if (m_currentUIObject != null)
            {
                m_pointerEventData.pressPosition = m_pointerEventData.position;
                m_pointerEventData.pointerPressRaycast = m_pointerEventData.pointerCurrentRaycast;

                var handler = ExecuteEvents.ExecuteHierarchy(m_currentUIObject, m_pointerEventData, ExecuteEvents.pointerDownHandler);
                m_pointerPress = handler != null ? handler : m_currentUIObject;
                EventSystem.current.SetSelectedGameObject(m_pointerPress);
            }
        }
        else if (!isPressedThisFrame && m_isPressed)
        {
            m_isPressed = false;
            if (m_pointerPress != null)
            {
                ExecuteEvents.Execute(m_pointerPress, m_pointerEventData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(m_pointerPress, m_pointerEventData, ExecuteEvents.pointerClickHandler);
            }
            m_pointerPress = null;
        }
    }

    void OnDisable()
    {
        if (m_hitDot != null)
            m_hitDot.SetActive(false);
    }
}

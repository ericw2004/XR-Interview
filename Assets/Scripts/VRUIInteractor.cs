using System;
using System.Collections.Generic;
using System.Reflection;
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
    [Tooltip("Threshold for controller trigger (0..1).")]
    public float triggerThreshold = 0.1f;
    [Tooltip("Distance between thumb and index tips to register a pinch (meters). Default 0.04 = 4 cm.")]
    public float pinchDistance = 0.04f;

    [Header("Ray")]
    [Tooltip("Override the ray origin transform. If null, defaults to this GameObject's transform.")]
    public Transform pointerOrigin;
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
    private ReflectionHandsProvider m_handsProvider;
    private List<GraphicRaycaster> m_cachedRaycasters;

    void Awake()
    {
        m_xrNode = (handSide == HandSide.Left) ? XRNode.LeftHand : XRNode.RightHand;

        if (EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        m_pointerEventData = new PointerEventData(EventSystem.current);

        // Line renderer for laser
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

        // Hit indicator dot
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

        // Reflection-based hands provider — compiles with or without XR Hands package
        m_handsProvider = new ReflectionHandsProvider();
        m_handsProvider.Init();
    }

    void Update()
    {
        // Start from the assigned pointer origin or this transform.
        // Both hand tracking and controller branches may override these below.
        Transform originTransform = pointerOrigin != null ? pointerOrigin : transform;
        Vector3 rayOrigin = originTransform.position;
        Vector3 rayDir    = originTransform.forward;
        bool isPressedThisFrame = false;

        // --- Determine input mode each frame independently ---
        // This means switching between hands and controllers mid-session works
        // without any stale state: whichever is active this frame wins.

        bool handTrackedThisFrame = false;
        var rhSide = (handSide == HandSide.Left)
            ? ReflectionHandsProvider.HandSide.Left
            : ReflectionHandsProvider.HandSide.Right;

        if (m_handsProvider != null && m_handsProvider.IsAvailable &&
            m_handsProvider.IsHandTracked(rhSide))
        {
            handTrackedThisFrame = true;

            // MetaOS-style ray: origin at IndexProximal (knuckle), aimed toward IndexTip.
            // Starting at the knuckle instead of the fingertip means the ray endpoint
            // stays stable when the fingers come together for a pinch.
            Pose proximalPose, tipPose;
            bool hasProximal = m_handsProvider.TryGetJointPose(rhSide, "IndexProximal", out proximalPose);
            bool hasTip      = m_handsProvider.TryGetJointPose(rhSide, "IndexTip",      out tipPose);

            if (hasProximal && hasTip)
            {
                Vector3 d = (tipPose.position - proximalPose.position).normalized;
                if (d.sqrMagnitude > 0.001f) { rayOrigin = proximalPose.position; rayDir = d; }
            }
            else if (hasTip)
            {
                Pose midPose;
                if (m_handsProvider.TryGetJointPose(rhSide, "IndexIntermediate", out midPose))
                {
                    Vector3 d = (tipPose.position - midPose.position).normalized;
                    if (d.sqrMagnitude > 0.001f) { rayOrigin = midPose.position; rayDir = d; }
                }
            }

            // Pinch: index tip <-> thumb tip with 1 cm release hysteresis to avoid flicker.
            Pose iTip, tTip;
            if (m_handsProvider.TryGetJointPose(rhSide, "IndexTip",  out iTip) &&
                m_handsProvider.TryGetJointPose(rhSide, "ThumbTip", out tTip))
            {
                float dist = Vector3.Distance(iTip.position, tTip.position);
                float releaseThreshold = pinchDistance + 0.01f;
                isPressedThisFrame = m_isPressed ? (dist < releaseThreshold) : (dist < pinchDistance);
            }
        }

        // Controller input — evaluated every frame, not just as a fallback,
        // so the ray follows the physical controller the moment hands are lost.
        if (!handTrackedThisFrame)
        {
            var device = InputDevices.GetDeviceAtXRNode(m_xrNode);
            if (device.isValid)
            {
                // Drive ray from live device pose so it always follows the controller.
                if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 devicePos) &&
                    device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion deviceRot))
                {
                    // Guard against zero-pose returned before first tracking packet
                    if (devicePos != Vector3.zero || deviceRot != Quaternion.identity)
                    {
                        rayOrigin = devicePos;
                        rayDir    = deviceRot * Vector3.forward;
                    }
                }

                // Trigger input
                if (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
                    isPressedThisFrame = triggerValue > triggerThreshold;
                else if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButton))
                    isPressedThisFrame = triggerButton;
            }
        }

        // --- Canvas-plane hit detection ---
        // Physics.Raycast is skipped: canvas objects live on the Default layer, not the UI
        // physics layer, so a ray-plane intersection against each world-space canvas plane
        // is the only reliable way to find what the controller is pointing at.

        // Refresh raycaster cache every ~2 seconds (120 frames at 60 fps)
        if (m_cachedRaycasters == null || Time.frameCount % 120 == 0)
            m_cachedRaycasters = new List<GraphicRaycaster>(
                GameObject.FindObjectsOfType<GraphicRaycaster>());

        Camera cam = uiCamera != null ? uiCamera : Camera.main;

        float   closestT  = maxDistance;
        Vector3 canvasHit = rayOrigin + rayDir * maxDistance;

        if (cam != null)
        {
            foreach (var rc in m_cachedRaycasters)
            {
                if (rc == null) continue;
                var canvas = rc.GetComponent<Canvas>();
                if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) continue;

                Vector3 planeNormal = canvas.transform.forward;
                float   denom       = Vector3.Dot(planeNormal, rayDir);
                if (Mathf.Abs(denom) < 0.001f) continue;   // ray parallel to canvas

                float t = Vector3.Dot(canvas.transform.position - rayOrigin, planeNormal) / denom;
                if (t <= 0f || t >= closestT) continue;

                // Confirm intersection is in front of the camera before accepting it
                Vector3 sp3 = cam.WorldToScreenPoint(rayOrigin + rayDir * t);
                if (sp3.z <= 0f) continue;

                closestT  = t;
                canvasHit = rayOrigin + rayDir * t;
            }
        }

        bool  hitCanvas   = closestT < maxDistance;
        float hitDistance = closestT;

        // Draw laser
        m_line.SetPosition(0, rayOrigin);
        m_line.SetPosition(1, canvasHit);

        // Hit dot — only show when the ray intersects a canvas
        m_hitDot.SetActive(hitCanvas);
        m_hitDot.transform.position   = canvasHit;
        m_hitDot.transform.localScale = Vector3.one * Mathf.Max(0.01f, hitDistance * 0.01f);

        // --- UI interaction ---
        if (cam == null) return;

        if (!hitCanvas)
        {
            if (m_currentUIObject != null)
            {
                ExecuteEvents.Execute(m_currentUIObject, m_pointerEventData,
                    ExecuteEvents.pointerExitHandler);
                m_currentUIObject = null;
            }
            return;
        }

        m_pointerEventData.Reset();
        m_pointerEventData.button = PointerEventData.InputButton.Left;

        // GraphicRaycaster pass — test all world-space canvases and pick the closest hit.
        m_raycastResults.Clear();
        var tempResults = new List<RaycastResult>();
        foreach (var rc in m_cachedRaycasters)
        {
            if (rc == null) continue;
            var canvas = rc.GetComponent<Canvas>();
            Camera useCam = (canvas != null && canvas.worldCamera != null) ? canvas.worldCamera : cam;
            if (useCam == null) continue;

            // compute screen point for this canvas' camera
            Vector3 sp = useCam.WorldToScreenPoint(canvasHit);
            if (sp.z <= 0f) continue; // behind this camera

            m_pointerEventData.position = new Vector2(sp.x, sp.y);
            tempResults.Clear();
            rc.Raycast(m_pointerEventData, tempResults);
            if (tempResults.Count > 0)
                m_raycastResults.AddRange(tempResults);
        }

        // choose nearest result (smallest distance)
        if (m_raycastResults.Count > 0)
        {
            m_raycastResults.Sort((a, b) => a.distance.CompareTo(b.distance));
            m_pointerEventData.pointerCurrentRaycast = m_raycastResults[0];
        }

        GameObject newUIObject = m_raycastResults.Count > 0 ? m_raycastResults[0].gameObject : null;

        // Pointer enter / exit
        if (newUIObject != m_currentUIObject)
        {
            if (m_currentUIObject != null)
                ExecuteEvents.Execute(m_currentUIObject, m_pointerEventData, ExecuteEvents.pointerExitHandler);

            m_currentUIObject = newUIObject;

            if (m_currentUIObject != null)
                ExecuteEvents.Execute(m_currentUIObject, m_pointerEventData, ExecuteEvents.pointerEnterHandler);
        }

        // Press / release — fires pointerClick on release, matching Unity UI convention
        if (isPressedThisFrame && !m_isPressed)
        {
            m_isPressed = true;
            if (m_currentUIObject != null)
            {
                m_pointerEventData.pressPosition       = m_pointerEventData.position;
                m_pointerEventData.pointerPressRaycast = m_pointerEventData.pointerCurrentRaycast;

                var handler = ExecuteEvents.ExecuteHierarchy(
                    m_currentUIObject, m_pointerEventData, ExecuteEvents.pointerDownHandler);
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
                if (m_currentUIObject != null)
                    ExecuteEvents.Execute(m_pointerPress, m_pointerEventData, ExecuteEvents.pointerClickHandler);
            }
            m_pointerPress = null;
        }
    }

    void OnDisable()
    {
        // Clean up hover/press state so re-enabling doesn't leave ghost events
        if (m_currentUIObject != null)
        {
            ExecuteEvents.Execute(m_currentUIObject, m_pointerEventData, ExecuteEvents.pointerExitHandler);
            m_currentUIObject = null;
        }
        if (m_pointerPress != null)
        {
            ExecuteEvents.Execute(m_pointerPress, m_pointerEventData, ExecuteEvents.pointerUpHandler);
            m_pointerPress = null;
        }
        m_isPressed = false;

        if (m_hitDot != null)
            m_hitDot.SetActive(false);
    }
}

/// <summary>
/// Accesses UnityEngine.XR.Hands types via reflection so the script compiles and runs
/// regardless of whether the XR Hands package is installed. When the package is absent,
/// IsAvailable returns false and the caller falls back to controller input.
/// </summary>
public class ReflectionHandsProvider
{
    public enum HandSide { Left, Right }

    private Type       m_xrHandSubsystemType;
    private Type       m_xrHandJointIDType;
    private Type       m_xrHandType;
    private Type       m_xrHandJointType;
    private MethodInfo m_tryGetJointMethod;
    private MethodInfo m_jointTryGetPoseMethod;
    private object     m_subsystem;

    public bool IsAvailable { get; private set; }

    public void Init()
    {
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (m_xrHandSubsystemType == null) m_xrHandSubsystemType = asm.GetType("UnityEngine.XR.Hands.XRHandSubsystem");
                if (m_xrHandJointIDType   == null) m_xrHandJointIDType   = asm.GetType("UnityEngine.XR.Hands.XRHandJointID");
                if (m_xrHandType          == null) m_xrHandType          = asm.GetType("UnityEngine.XR.Hands.XRHand");
                if (m_xrHandJointType     == null) m_xrHandJointType     = asm.GetType("UnityEngine.XR.Hands.XRHandJoint");

                if (m_xrHandSubsystemType != null && m_xrHandJointIDType != null &&
                    m_xrHandType != null && m_xrHandJointType != null) break;
            }

            if (m_xrHandSubsystemType == null || m_xrHandJointIDType == null ||
                m_xrHandType == null  || m_xrHandJointType == null)
            { IsAvailable = false; return; }

            m_tryGetJointMethod = m_xrHandType.GetMethod("GetJoint",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { m_xrHandJointIDType }, null);

            m_jointTryGetPoseMethod = m_xrHandJointType.GetMethod("TryGetPose",
                BindingFlags.Public | BindingFlags.Instance);

            if (m_tryGetJointMethod == null || m_jointTryGetPoseMethod == null)
            { IsAvailable = false; return; }

            IsAvailable = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ReflectionHandsProvider] Init failed: " + e.Message);
            IsAvailable = false;
        }
    }

    private bool TryGetSubsystem()
    {
        if (!IsAvailable) return false;
        try
        {
            var listType      = typeof(List<>).MakeGenericType(m_xrHandSubsystemType);
            var list          = Activator.CreateInstance(listType);
            var getSubsystems = typeof(SubsystemManager).GetMethod("GetSubsystems", BindingFlags.Public | BindingFlags.Static);
            if (getSubsystems == null) return false;
            getSubsystems.MakeGenericMethod(m_xrHandSubsystemType).Invoke(null, new[] { list });

            int count = (int)listType.GetProperty("Count").GetValue(list);
            if (count == 0) { m_subsystem = null; return false; }

            m_subsystem = listType.GetProperty("Item").GetValue(list, new object[] { 0 });
            return m_subsystem != null;
        }
        catch { m_subsystem = null; return false; }
    }

    public bool IsHandTracked(HandSide side)
    {
        if (!TryGetSubsystem()) return false;
        try
        {
            string propName  = side == HandSide.Left ? "leftHand" : "rightHand";
            var    handProp  = m_xrHandSubsystemType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (handProp == null) return false;
            object hand      = handProp.GetValue(m_subsystem);
            var    trackedProp = m_xrHandType.GetProperty("isTracked", BindingFlags.Public | BindingFlags.Instance);
            if (trackedProp == null) return false;
            return (bool)trackedProp.GetValue(hand);
        }
        catch { return false; }
    }

    public bool TryGetJointPose(HandSide side, string jointName, out Pose pose)
    {
        pose = Pose.identity;
        if (!TryGetSubsystem()) return false;
        try
        {
            string propName = side == HandSide.Left ? "leftHand" : "rightHand";
            var    handProp = m_xrHandSubsystemType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (handProp == null) return false;
            object hand = handProp.GetValue(m_subsystem);

            object jointID;
            try { jointID = Enum.Parse(m_xrHandJointIDType, jointName); }
            catch { return false; }

            object joint = m_tryGetJointMethod.Invoke(hand, new[] { jointID });
            if (joint == null) return false;

            var    poseArgs = new object[] { Pose.identity };
            bool   success  = (bool)m_jointTryGetPoseMethod.Invoke(joint, poseArgs);
            if (success) pose = (Pose)poseArgs[0];
            return success;
        }
        catch { return false; }
    }
}

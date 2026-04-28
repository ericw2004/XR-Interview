using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Fixes the two requirements for hand-pinch and controller-ray UI interaction:
///
///   1. EventSystem — replaces StandaloneInputModule with XRUIInputModule so that
///      tracked-device (controller ray and hand ray) input is routed to UI elements.
///
///   2. World-space canvases — replaces GraphicRaycaster with
///      TrackedDeviceGraphicRaycaster so that XR ray interactors can hit buttons.
///      Without this component, controller and hand rays pass through the canvas.
///
/// Usage: Attach to any persistent GameObject (e.g. a Managers object). Runs
/// once in Awake, before any XR interactors attempt UI interaction.
/// </summary>
[DefaultExecutionOrder(-100)]
public class VRCanvasSetup : MonoBehaviour
{
    void Awake()
    {
        SetupEventSystem();
        SetupCanvases();
    }

    static void SetupEventSystem()
    {
        var eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogWarning("VRCanvasSetup: No EventSystem found in scene.");
            return;
        }

        // Disable the legacy standalone module; XR needs its own input module.
        var standalone = eventSystem.GetComponent<StandaloneInputModule>();
        if (standalone != null)
            standalone.enabled = false;

        if (eventSystem.GetComponent<XRUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<XRUIInputModule>();
            Debug.Log("VRCanvasSetup: Added XRUIInputModule to EventSystem.");
        }
    }

    static void SetupCanvases()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            // Only world-space canvases need TrackedDeviceGraphicRaycaster.
            if (canvas.renderMode != RenderMode.WorldSpace) continue;

            // Disable the standard raycaster — it does not work with XR ray interactors.
            var legacy = canvas.GetComponent<GraphicRaycaster>();
            if (legacy != null)
                legacy.enabled = false;

            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                Debug.Log($"VRCanvasSetup: Added TrackedDeviceGraphicRaycaster to '{canvas.name}'.");
            }
        }
    }
}

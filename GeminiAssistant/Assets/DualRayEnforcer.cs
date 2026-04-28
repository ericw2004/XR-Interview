using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Ensures both controller NearFarInteractor ray line visuals remain enabled,
/// reproducing the Meta OS behaviour where both controller rays are always visible.
///
/// Usage: Attach to the XR Origin (XR Rig) root. Assign Left and Right
/// NearFarInteractor references in the Inspector (drag from the Left Controller
/// and Right Controller children of Camera Offset).
///
/// The enforcer only re-enables a line visual when its parent NearFarInteractor
/// is itself active, so rays are still correctly hidden during teleport.
/// </summary>
[DefaultExecutionOrder(200)]
public class DualRayEnforcer : MonoBehaviour
{
    [Tooltip("NearFarInteractor on the Left Controller.")]
    [SerializeField] NearFarInteractor leftInteractor;

    [Tooltip("NearFarInteractor on the Right Controller.")]
    [SerializeField] NearFarInteractor rightInteractor;

    XRInteractorLineVisual m_LeftLine;
    XRInteractorLineVisual m_RightLine;

    void Start()
    {
        m_LeftLine = FindLineVisual(leftInteractor, "left");
        m_RightLine = FindLineVisual(rightInteractor, "right");
    }

    void LateUpdate()
    {
        EnsureEnabled(leftInteractor, m_LeftLine);
        EnsureEnabled(rightInteractor, m_RightLine);
    }

    static XRInteractorLineVisual FindLineVisual(NearFarInteractor interactor, string side)
    {
        if (interactor == null)
        {
            Debug.LogWarning($"DualRayEnforcer: {side} NearFarInteractor is not assigned.", interactor);
            return null;
        }

        // The line visual is typically on the same GameObject as the NearFarInteractor.
        var visual = interactor.GetComponent<XRInteractorLineVisual>();
        if (visual == null)
            visual = interactor.GetComponentInChildren<XRInteractorLineVisual>(true);

        if (visual == null)
            Debug.LogWarning($"DualRayEnforcer: Could not find XRInteractorLineVisual for {side} interactor. " +
                             "Make sure the NearFarInteractor has an XRInteractorLineVisual component.", interactor);

        return visual;
    }

    static void EnsureEnabled(NearFarInteractor interactor, XRInteractorLineVisual lineVisual)
    {
        if (interactor == null || lineVisual == null) return;
        if (!interactor.gameObject.activeInHierarchy) return;

        if (!lineVisual.enabled)
            lineVisual.enabled = true;
    }
}

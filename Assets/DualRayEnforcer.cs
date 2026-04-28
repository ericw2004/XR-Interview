using UnityEngine;

/// <summary>
/// Safety net: ensures the LineRenderer on every VRUIInteractor stays enabled
/// so both controller rays remain visible (Meta OS dual-ray style).
///
/// VRUIRuntimeAutoAttacher creates one VRUIInteractor per hand, but if a ray
/// is ever disabled by another script this component re-enables it each frame.
///
/// Usage: attach to any persistent GameObject in the scene. No Inspector setup.
/// </summary>
[DefaultExecutionOrder(200)]
public class DualRayEnforcer : MonoBehaviour
{
    VRUIInteractor[] m_Interactors;

    void Start()
    {
        m_Interactors = FindObjectsByType<VRUIInteractor>(FindObjectsSortMode.None);

        if (m_Interactors.Length < 2)
            Debug.LogWarning($"DualRayEnforcer: found {m_Interactors.Length} VRUIInteractor(s); expected 2 (one per hand). " +
                             "Check that VRUIRuntimeAutoAttacher is in the scene.", this);
    }

    void LateUpdate()
    {
        foreach (var interactor in m_Interactors)
        {
            if (interactor == null) continue;
            var line = interactor.GetComponent<LineRenderer>();
            if (line != null && !line.enabled)
                line.enabled = true;
        }
    }
}

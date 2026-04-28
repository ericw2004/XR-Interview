using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PinchVizController : MonoBehaviour
{
    [SerializeField]
    SkinnedMeshRenderer m_Pointer;

    XRRayInteractor m_RayInteractor;
    NearFarInteractor m_NearFarInteractor;

    void Start()
    {
        m_RayInteractor = GetComponent<XRRayInteractor>();
        m_NearFarInteractor = GetComponent<NearFarInteractor>();

        if (m_RayInteractor == null && m_NearFarInteractor == null)
            Debug.LogWarning("PinchVizController: No XRRayInteractor or NearFarInteractor found on this GameObject.", this);
    }

    void Update()
    {
        if (m_Pointer == null) return;

        float inputValue = 0f;
        if (m_RayInteractor != null)
            inputValue = Mathf.Max(m_RayInteractor.selectInput.ReadValue(), m_RayInteractor.uiPressInput.ReadValue());
        else if (m_NearFarInteractor != null)
            inputValue = Mathf.Max(m_NearFarInteractor.selectInput.ReadValue(), m_NearFarInteractor.uiPressInput.ReadValue());

        m_Pointer.SetBlendShapeWeight(0, inputValue * 100f);
    }
}

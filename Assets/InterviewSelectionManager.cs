using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages interview setup panel selection for Toggle-based UIs:
///   - Difficulty toggles: mutually exclusive (radio-button style).
///   - Subject toggles: fully independent (checkbox style, any combination).
///
/// Note: the panel built by InterviewXRBuilder uses Button components with
/// InterviewXRPanel handling state directly — this script is for scenes that
/// use Toggle components instead of Buttons.
/// </summary>
public class InterviewSelectionManager : MonoBehaviour
{
    [Header("Difficulty — single select")]
    [Tooltip("All difficulty Toggle components. Exactly one stays selected.")]
    [SerializeField] List<Toggle> difficultyToggles = new();

    [Header("Subjects — multi select")]
    [Tooltip("All subject Toggle components. Any combination may be selected.")]
    [SerializeField] List<Toggle> subjectToggles = new();

    ToggleGroup m_DifficultyGroup;

    void Awake()
    {
        m_DifficultyGroup = gameObject.AddComponent<ToggleGroup>();
        m_DifficultyGroup.allowSwitchOff = false;

        foreach (var t in difficultyToggles)
        {
            if (t == null) continue;
            t.group = m_DifficultyGroup;
        }

        foreach (var t in subjectToggles)
        {
            if (t == null) continue;
            t.group = null;
        }
    }

    public string GetSelectedDifficulty()
    {
        foreach (var t in difficultyToggles)
            if (t != null && t.isOn)
                return t.GetComponentInChildren<TMP_Text>()?.text ?? t.name;
        return string.Empty;
    }

    public List<string> GetSelectedSubjects()
    {
        var result = new List<string>();
        foreach (var t in subjectToggles)
            if (t != null && t.isOn)
                result.Add(t.GetComponentInChildren<TMP_Text>()?.text ?? t.name);
        return result;
    }
}

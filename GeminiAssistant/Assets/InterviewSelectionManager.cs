using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages interview setup panel selection:
///   - Difficulty toggles: mutually exclusive (one at a time, radio-button style).
///   - Subject toggles: fully independent (any combination, checkbox style).
///
/// Usage: Attach to any persistent GameObject in the scene. Assign your Toggle
/// components in the Inspector under each list. The script handles ToggleGroup
/// wiring at runtime so you do not need to configure ToggleGroups manually.
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
        // Create a dedicated ToggleGroup for difficulty so exactly one stays on.
        m_DifficultyGroup = gameObject.AddComponent<ToggleGroup>();
        m_DifficultyGroup.allowSwitchOff = false;

        foreach (var t in difficultyToggles)
        {
            if (t == null) continue;
            t.group = m_DifficultyGroup;
        }

        // Subjects must have NO ToggleGroup — each toggle is independent.
        // This is the most common cause of multi-select buttons that won't stay on.
        foreach (var t in subjectToggles)
        {
            if (t == null) continue;
            t.group = null;
        }
    }

    /// <summary>Returns the label text of the currently selected difficulty toggle.</summary>
    public string GetSelectedDifficulty()
    {
        foreach (var t in difficultyToggles)
        {
            if (t != null && t.isOn)
                return t.GetComponentInChildren<TMP_Text>()?.text ?? t.name;
        }
        return string.Empty;
    }

    /// <summary>Returns label texts of every selected subject toggle.</summary>
    public List<string> GetSelectedSubjects()
    {
        var result = new List<string>();
        foreach (var t in subjectToggles)
        {
            if (t != null && t.isOn)
                result.Add(t.GetComponentInChildren<TMP_Text>()?.text ?? t.name);
        }
        return result;
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(InterviewXRPanel))]
public class InterviewXRBuilder : MonoBehaviour
{
    void Awake()
    {
        var panel = GetComponent<InterviewXRPanel>();
        BuildCanvas(panel);
    }

    void BuildCanvas(InterviewXRPanel panel)
    {
        // ── Canvas ────────────────────────────────────────────────────────────
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // worldCamera must be set so GraphicRaycaster can convert world-space
        // hit positions to screen-space for click detection.
        canvas.worldCamera = Camera.main;

        gameObject.AddComponent<GraphicRaycaster>();

        // The canvas rect in pixels
        var canvasRT = GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(800f, 550f);

        // THIS is the only size control you need.
        // 0.001 means 1 pixel = 0.001 metres, so 800px = 0.8m wide (~31 inches).
        // Increase to make bigger, decrease to make smaller.
        transform.localScale = Vector3.one * 0.001f;

        // ── Background Panel ──────────────────────────────────────────────────
        var bg = MakeImage("Background", transform,
            new Color(0.06f, 0.07f, 0.12f, 1f),
            new Vector2(800f, 550f));
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = bgRT.anchorMax = new Vector2(0.5f, 0.5f);
        bgRT.anchoredPosition = Vector2.zero;

        // Vertical layout for all content
        var vlg = bg.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(30, 30, 25, 25);
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var root = bg.transform;

        // ── Header ────────────────────────────────────────────────────────────
        MakeLabel(root, "POWERED BY XR", 11f, new Color(0.22f, 0.85f, 0.73f), 16f, spacing: 3f);
        MakeLabel(root, "Welcome to InterviewXR!", 40f, new Color(0.93f, 0.95f, 1f), 55f, bold: true);
        MakeLabel(root, "Configure your session to begin your practice interview.",
                  13f, new Color(0.55f, 0.60f, 0.72f), 20f);

        MakeDivider(root, new Color(0.29f, 0.56f, 1f, 0.3f));

        // ── Difficulty ────────────────────────────────────────────────────────
        MakeSectionLabel(root, "SELECT DIFFICULTY");
        var diffRow = MakeRow(root);
        panel.btnEasy   = MakeButton(diffRow.transform, "Easy");
        panel.btnMedium = MakeButton(diffRow.transform, "Medium");
        panel.btnHard   = MakeButton(diffRow.transform, "Hard");

        MakeDivider(root, new Color(0.22f, 0.85f, 0.73f, 0.2f));

        // ── Topics ────────────────────────────────────────────────────────────
        MakeSectionLabel(root, "SELECT TOPICS  \u00b7  choose one or more");
        var row1 = MakeRow(root);
        panel.btnPersonal     = MakeButton(row1.transform, "Personal");
        panel.btnComputerArch = MakeButton(row1.transform, "Comp. Architecture");
        panel.btnProgramming  = MakeButton(row1.transform, "Programming");

        var row2 = MakeRow(root);
        panel.btnSystemArch    = MakeButton(row2.transform, "System Architecture");
        panel.btnNetworking    = MakeButton(row2.transform, "Networking");
        panel.btnCybersecurity = MakeButton(row2.transform, "Cybersecurity");

        // ── Confirm ───────────────────────────────────────────────────────────
        panel.btnConfirm = MakeConfirmButton(root, out panel.confirmLabel);

        // Make every button raycast-compatible with OVR
        Button[] allButtons = bg.GetComponentsInChildren<Button>();
        foreach (var btn in allButtons)
        {
            var img = btn.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    GameObject MakeImage(string name, Transform parent, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return go;
    }

    void MakeLabel(Transform parent, string text, float fontSize, Color color,
                   float height, bool bold = false, float spacing = 0f)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.fontSize         = fontSize;
        tmp.color            = color;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.fontStyle        = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.characterSpacing = spacing;
        go.AddComponent<LayoutElement>().preferredHeight = height;
    }

    void MakeSectionLabel(Transform parent, string text)
    {
        var go = new GameObject("SectionLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.fontSize         = 11f;
        tmp.color            = new Color(0.55f, 0.60f, 0.72f);
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.characterSpacing = 2f;
        go.AddComponent<LayoutElement>().preferredHeight = 16f;
    }

    void MakeDivider(Transform parent, Color color)
    {
        var go = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 1f;
        le.preferredWidth  = 740f;
    }

    GameObject MakeRow(Transform parent)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing              = 10f;
        hlg.childAlignment       = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = false;
        go.AddComponent<LayoutElement>().preferredHeight = 48f;
        return go;
    }

    Button MakeButton(Transform parent, string label)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.26f);
        go.AddComponent<LayoutElement>().preferredHeight = 44f;

        var btn = go.AddComponent<Button>();

        var lblGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(go.transform, false);
        var lrt = lblGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = lblGO.GetComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 14f;
        tmp.color     = new Color(0.93f, 0.95f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    Button MakeConfirmButton(Transform parent, out TextMeshProUGUI labelOut)
    {
        var go = new GameObject("ConfirmBtn", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.20f, 0.20f, 0.25f);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 52f;

        var btn = go.AddComponent<Button>();
        btn.interactable = false;

        var lblGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(go.transform, false);
        var lrt = lblGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = lblGO.GetComponent<TextMeshProUGUI>();
        tmp.text      = "Select a difficulty and at least one topic";
        tmp.fontSize  = 16f;
        tmp.color     = new Color(0.93f, 0.95f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        labelOut = tmp;
        return btn;
    }
}

using System.Linq;
using UnityEngine;

// Runtime helper: automatically attach VRUIInteractor to likely controller/hand objects at runtime.
[DefaultExecutionOrder(-1000)]
public class VRUIRuntimeAutoAttacher : MonoBehaviour
{
    void Awake()
    {
        // Count existing interactors per side. If both hands already have one, skip.
        // Bailing on the first found was preventing the second hand's ray from being created.
        var existingAll = FindObjectsOfType<VRUIInteractor>();
        bool hasLeft  = System.Array.Exists(existingAll, i => i.handSide == VRUIInteractor.HandSide.Left);
        bool hasRight = System.Array.Exists(existingAll, i => i.handSide == VRUIInteractor.HandSide.Right);
        if (hasLeft && hasRight)
            return;

        // candidate name keywords
        string[] leftKeywords = { "lefthand", "left hand", "left_controller", "leftcontroller", "left controller", "left controller", "left", "controller_l", "hand_l" };
        string[] rightKeywords = { "righthand", "right hand", "right_controller", "rightcontroller", "right controller", "right controller", "right", "controller_r", "hand_r" };

        Transform leftRoot = FindFirstContaining(leftKeywords);
        Transform rightRoot = FindFirstContaining(rightKeywords);

        if (leftRoot == null || rightRoot == null)
        {
            // fallback: attach under main camera
            var cam = Camera.main;
            if (cam != null)
            {
                if (leftRoot == null)
                {
                    var go = new GameObject("LeftHand");
                    go.transform.SetParent(cam.transform, false);
                    leftRoot = go.transform;
                }
                if (rightRoot == null)
                {
                    var go = new GameObject("RightHand");
                    go.transform.SetParent(cam.transform, false);
                    rightRoot = go.transform;
                }
            }
        }

        if (leftRoot != null)
            CreateInteractorForRoot(leftRoot, true);
        if (rightRoot != null)
            CreateInteractorForRoot(rightRoot, false);
    }

    static Transform FindFirstContaining(string[] keywords)
    {
        var all = Object.FindObjectsOfType<Transform>();
        foreach (var t in all)
        {
            string n = t.name.ToLowerInvariant();
            if (keywords.Any(k => n.Contains(k)))
                return t;

            // also inspect components for common controller types
            var comps = t.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                string typeName = c.GetType().Name.ToLowerInvariant();
                if (typeName.Contains("actionbasedcontroller") || typeName.Contains("xrcontroller") || typeName.Contains("trackedposedriver") || typeName.Contains("ovrcontroller") || typeName.Contains("hand"))
                    return t;
            }
        }
        return null;
    }

    static void CreateInteractorForRoot(Transform root, bool isLeft)
    {
        if (root == null) return;

        // look for an existing tip/pointer child
        Transform origin = null;
        foreach (Transform child in root)
        {
            var n = child.name.ToLowerInvariant();
            if (n.Contains("tip") || n.Contains("pointer") || n.Contains("aim") || n.Contains("attach") || n.Contains("model"))
            {
                origin = child;
                break;
            }
        }

        if (origin == null)
        {
            origin = root.Find("UI Pointer Origin");
        }

        if (origin == null)
        {
            var go = new GameObject("UI Pointer Origin");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            origin = go.transform;
        }

        var interactor = origin.GetComponent<VRUIInteractor>();
        if (interactor == null) interactor = origin.gameObject.AddComponent<VRUIInteractor>();

        interactor.pointerOrigin = origin;
        interactor.handSide = isLeft ? VRUIInteractor.HandSide.Left : VRUIInteractor.HandSide.Right;

        if (Camera.main != null) interactor.uiCamera = Camera.main;

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer != -1) interactor.uiLayerMask = LayerMask.GetMask("UI");
    }
}

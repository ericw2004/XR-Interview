using System.Linq;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class VRUIInteractorAutoSetup
{
    static VRUIInteractorAutoSetup()
    {
        // Run auto-setup when entering play mode so scenes are wired automatically
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            AutoSetup();
        }
    }
    [MenuItem("VR/Auto-Setup VR UI Interactors")]
    public static void AutoSetup()
    {
        // Ensure EventSystem
        if (EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            // Add Standalone by default; we'll replace it with a better module if available
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[VR AutoSetup] Created EventSystem with StandaloneInputModule.");
        }

        // Try to replace the StandaloneInputModule with a more appropriate module if available
        var esGOObj = EventSystem.current.gameObject;
        // remove existing non-editor input modules (except EventSystem)
        var existingModules = esGOObj.GetComponents<UnityEngine.EventSystems.BaseInputModule>();
        foreach (var m in existingModules)
        {
            // keep EventSystem's BaseInputModule if it's the only option; we'll remove and re-add below
            if (m == null) continue;
        }

        // Add Input System UI Input Module if present
        var inputSystemType = System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => {
                try { return a.GetTypes(); } catch { return new System.Type[0]; }
            })
            .FirstOrDefault(t => t.Name == "InputSystemUIInputModule");

        if (inputSystemType != null)
        {
            // remove StandaloneInputModule if present
            var stand = esGOObj.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (stand != null) Object.DestroyImmediate(stand, true);
            esGOObj.AddComponent(inputSystemType);
            Debug.Log("[VR AutoSetup] Added InputSystemUIInputModule to EventSystem.");
        }
        else
        {
            // Try XR UI Input Module (XR Interaction Toolkit)
            var xrType = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => {
                    try { return a.GetTypes(); } catch { return new System.Type[0]; }
                })
                .FirstOrDefault(t => t.Name == "XRUIInputModule" || t.Name == "XRUIInputModuleEditor");
            if (xrType != null)
            {
                var stand = esGOObj.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (stand != null) Object.DestroyImmediate(stand, true);
                esGOObj.AddComponent(xrType);
                Debug.Log("[VR AutoSetup] Added XR UI Input Module to EventSystem.");
            }
            else
            {
                Debug.Log("[VR AutoSetup] Using StandaloneInputModule on EventSystem (no InputSystem/XR UI module found).");
            }
        }

        // Candidate names to find left/right controller/hand objects
        string[] leftNameCandidates = { "LeftHand", "LeftHand Controller", "Left Controller", "left", "Left" };
        string[] rightNameCandidates = { "RightHand", "RightHand Controller", "Right Controller", "right", "Right" };

        Transform leftRoot = FindFirstNamed(leftNameCandidates);
        Transform rightRoot = FindFirstNamed(rightNameCandidates);

        if (leftRoot == null && rightRoot == null)
        {
            Debug.Log("[VR AutoSetup] Could not auto-find controller/hand objects by name. Creating default interactors under Camera.main.");
            var cam = Camera.main;
            if (cam != null)
            {
                // create left/right under camera
                var leftGo = new GameObject("LeftHand");
                leftGo.transform.SetParent(cam.transform, false);
                leftRoot = leftGo.transform;

                var rightGo = new GameObject("RightHand");
                rightGo.transform.SetParent(cam.transform, false);
                rightRoot = rightGo.transform;
            }
        }

        if (leftRoot != null) CreateInteractorForRoot(leftRoot, true);
        if (rightRoot != null) CreateInteractorForRoot(rightRoot, false);

        // Ensure world-space canvases have colliders so physics ray hits align with visuals
        var canvases = Object.FindObjectsOfType<Canvas>();
        foreach (var c in canvases)
        {
            if (c.renderMode != RenderMode.WorldSpace) continue;
            var go = c.gameObject;
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) continue;

            var bc = go.GetComponent<BoxCollider>();
            if (bc == null)
            {
                bc = Undo.AddComponent<BoxCollider>(go);
                // size in local canvas space based on rect
                var rect = rt.rect;
                bc.size = new Vector3(rect.width, rect.height, 0.01f);
                bc.center = new Vector3((0.5f - rt.pivot.x) * rect.width, (0.5f - rt.pivot.y) * rect.height, 0f);
                bc.isTrigger = true;
                Debug.Log($"[VR AutoSetup] Added BoxCollider to world-space Canvas '{go.name}' (size {bc.size}).");
            }

            // set layer to UI if available
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer != -1)
            {
                go.layer = uiLayer;
            }
        }

        Debug.Log("[VR AutoSetup] Done. Verify Canvas is World Space and has GraphicRaycaster. Adjust pointerOrigin and uiCamera on created components as needed.");
    }

    static Transform FindFirstNamed(string[] candidates)
    {
        foreach (var name in candidates)
        {
            var go = GameObject.Find(name);
            if (go != null) return go.transform;
        }

        var all = Object.FindObjectsOfType<Transform>();
        foreach (var t in all)
        {
            string n = t.name.ToLowerInvariant();
            if (candidates.Any(c => n.Contains(c.ToLowerInvariant()))) return t;
        }
        return null;
    }

    static void CreateInteractorForRoot(Transform root, bool isLeft)
    {
        // try to find an existing tip/pointer child to attach to
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
            // offset slightly forward so ray appears in front of controller model
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

        Debug.Log($"[VR AutoSetup] Added VRUIInteractor to '{origin.GetFullPath()}' (handSide={(isLeft?"Left":"Right")}).");
    }

    // utility extension to get full path for logging
    static string GetFullPath(this Transform t)
    {
        string s = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            s = t.name + "/" + s;
        }
        return s;
    }
}

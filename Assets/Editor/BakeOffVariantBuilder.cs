#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Menu: BakeOff > Build Variants And Menu
// Takes BakeOff_Base and produces BakeOff_A_Ray, BakeOff_B_Direct, BakeOff_C_GazePinch and MainMenu,
// then fills the Build Settings scene list. Run "BakeOff > Create Base Scene" first if the base is missing.
public static class BakeOffVariantBuilder
{
    const string Base = "Assets/Scenes/BakeOff_Base.unity";
    const string S = "Assets/Samples/XR Interaction Toolkit/3.3.2/";
    const string RigController = S + "Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    const string RigHands = S + "Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab";
    const string GazePrefab = S + "Starter Assets/Prefabs/Interactors/Gaze Interactor.prefab";
    const string Actions = S + "Starter Assets/XRI Default Input Actions.inputactions";

    [MenuItem("BakeOff/Build Variants And Menu")]
    public static void BuildAll()
    {
        Build("BakeOff_A_Ray", "A_Ray", RigController, v => ConfigNearFar(v, near: false, far: true, activate: true));
        Build("BakeOff_B_Direct", "B_Direct", RigController, v => ConfigNearFar(v, near: true, far: false, activate: false));
        Build("BakeOff_C_GazePinch", "C_GazePinch", RigHands, ConfigGaze);
        BuildMenu();

        EditorBuildSettings.scenes = new[] { "MainMenu", "BakeOff_A_Ray", "BakeOff_B_Direct", "BakeOff_C_GazePinch" }
            .Select(n => new EditorBuildSettingsScene("Assets/Scenes/" + n + ".unity", true)).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("BakeOff: A/B/C + MainMenu built, Build Settings updated.");
    }

    // ---------- shared pieces ----------

    static void Build(string sceneName, string variant, string rigPath, System.Action<GameObject> configure)
    {
        var scene = EditorSceneManager.OpenScene(Base, OpenSceneMode.Single);

        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(rigPath));
        rig.transform.position = Vector3.zero;
        DisableLocomotion(rig);
        RemoveDefaultCamera();
        configure(rig);

        var tm = Object.FindFirstObjectByType<TrialManager>();
        tm.variant = variant;
        tm.menuScene = "MainMenu";
        tm.endPanel = MakeEndPanel(tm);

        // identical click feedback in all variants
        var socket = tm.socket;
        var audio = socket.gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.clip = MakeClick();
        var player = socket.gameObject.AddComponent<ClickOnSelect>();
        player.source = audio;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/" + sceneName + ".unity");
    }


    // The default scene camera would give a second audio listener next to the rig camera.
    static void RemoveDefaultCamera()
    {
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "Main Camera") Object.DestroyImmediate(go);
    }

    static void DisableLocomotion(GameObject rig)
    {
        foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
            if (t != rig.transform && (t.name == "Locomotion" || t.name.Contains("Teleport")))
                t.gameObject.SetActive(false);
    }

    static void ConfigNearFar(GameObject rig, bool near, bool far, bool activate)
    {
        foreach (var nf in rig.GetComponentsInChildren<NearFarInteractor>(true))
        {
            nf.enableNearCasting = near;
            nf.enableFarCasting = far;
            if (activate) SetSelectAction(nf, IsUnder(nf.transform, "Left") ? "Left" : "Right");
            EditorUtility.SetDirty(nf);
        }
    }

    static bool IsUnder(Transform t, string key) { for (; t != null; t = t.parent) if (t.name.Contains(key)) return true; return false; }

    // Set the interactor's Select Input to that hand's Activate (trigger) action.
    static void SetSelectAction(NearFarInteractor nf, string hand)
    {
        string wanted = "XRI " + hand + " Interaction/Activate";
        var refs = AssetDatabase.LoadAllAssetsAtPath(Actions).OfType<InputActionReference>();
        var reference = refs.FirstOrDefault(r => r.name == wanted) ?? refs.FirstOrDefault(r => r.name.EndsWith(hand + " Interaction/Activate", System.StringComparison.OrdinalIgnoreCase));
        if (reference == null) { Debug.LogWarning("BakeOff: Activate action not found for " + hand); return; }

        var so = new SerializedObject(nf);
        var sel = so.FindProperty("m_SelectInput");
        if (sel == null) { Debug.LogWarning("BakeOff: m_SelectInput not found on " + nf.name); return; }
        var mode = sel.FindPropertyRelative("m_InputSourceMode");
        var perf = sel.FindPropertyRelative("m_InputActionReferencePerformed");
        var val = sel.FindPropertyRelative("m_InputActionReferenceValue");
        if (mode != null) mode.enumValueIndex = 2; // InputActionReference
        if (perf != null) perf.objectReferenceValue = reference;
        if (val != null) val.objectReferenceValue = reference;
        so.ApplyModifiedProperties();
        Debug.Log("BakeOff: " + nf.name + " select input -> " + reference.name);
    }

    static void ConfigGaze(GameObject rig)
    {
        // pinch only confirms the gaze target: turn off hand rays / near interactors
        foreach (var nf in rig.GetComponentsInChildren<NearFarInteractor>(true)) nf.gameObject.SetActive(false);

        var offset = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Camera Offset");
        var parent = offset != null ? offset : rig.transform;
        if (rig.GetComponentInChildren<XRGazeInteractor>(true) == null)
        {
            var gaze = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GazePrefab), parent);
            gaze.transform.localPosition = Vector3.zero;
        }
        foreach (var g in rig.GetComponentsInChildren<XRGazeInteractor>(true))
        {
            g.hoverToSelect = false;
            SetBool(g, "m_HoverToSelect", false);
            // fall back to head pose (Quest has no eye tracking)
            var mgr = g.GetComponentInParent<Transform>().root.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(m => m != null && m.GetType().Name == "GazeInputManager");
            if (mgr != null) SetBool(mgr, "m_FallbackIfEyeTrackingUnavailable", true);
            EditorUtility.SetDirty(g);
        }
        foreach (var cube in Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None))
        {
            cube.allowGazeInteraction = true;
            cube.allowGazeSelect = true;
            EditorUtility.SetDirty(cube);
        }
    }

    static void SetBool(Object o, string field, bool value)
    {
        var so = new SerializedObject(o);
        var p = so.FindProperty(field);
        if (p != null) { p.boolValue = value; so.ApplyModifiedProperties(); }
    }

    // ---------- UI ----------

    static GameObject MakeEndPanel(TrialManager tm)
    {
        var canvas = MakeWorldCanvas("EndPanel", new Vector3(0, 1.25f, 0.35f), new Vector2(400, 200));
        MakeText(canvas.transform, "Block complete", 36, new Vector2(0, 40), new Vector2(380, 60));
        var btn = MakeButton(canvas.transform, "Back to Main Menu", new Vector2(0, -40), new Vector2(300, 60));
        UnityEventTools.AddPersistentListener(btn.onClick, tm.BackToMenu);
        return canvas.gameObject;
    }

    static void BuildMenu()
    {
        var scene = EditorSceneManager.OpenScene(Base, OpenSceneMode.Single);
        // menu scene has only floor, marker, rig and menu
        foreach (var n in new[] { "Cubes", "Pedestal", "Socket", "TrialManager" })
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigHands));
        DisableLocomotion(rig);
        RemoveDefaultCamera();

        var canvas = MakeWorldCanvas("Menu", new Vector3(0, 1.2f, 0.45f), new Vector2(500, 500));
        var layout = canvas.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 20; layout.childControlHeight = false; layout.childControlWidth = false;

        var manager = new GameObject("MenuManager").AddComponent<MainMenu>();

        MakeText(canvas.transform, "Interaction Bake-Off", 40, Vector2.zero, new Vector2(460, 70));

        var row = new GameObject("ParticipantRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(canvas.transform, false);
        ((RectTransform)row.transform).sizeDelta = new Vector2(460, 70);
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter; h.spacing = 20; h.childControlWidth = false; h.childControlHeight = false;
        var minus = MakeButton(row.transform, "-", Vector2.zero, new Vector2(70, 70));
        var label = MakeText(row.transform, Session.ParticipantId, 40, Vector2.zero, new Vector2(200, 70));
        label.gameObject.name = "ParticipantLabel";
        var plus = MakeButton(row.transform, "+", Vector2.zero, new Vector2(70, 70));
        manager.participantLabel = label;
        UnityEventTools.AddPersistentListener(minus.onClick, manager.PreviousParticipant);
        UnityEventTools.AddPersistentListener(plus.onClick, manager.NextParticipant);

        AddSceneButton(canvas.transform, manager, "A: Ray + Trigger", "BakeOff_A_Ray");
        AddSceneButton(canvas.transform, manager, "B: Direct Grab", "BakeOff_B_Direct");
        AddSceneButton(canvas.transform, manager, "C: Gaze + Pinch", "BakeOff_C_GazePinch");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
    }

    static void AddSceneButton(Transform parent, MainMenu manager, string text, string sceneName)
    {
        var b = MakeButton(parent, text, Vector2.zero, new Vector2(400, 70));
        UnityEventTools.AddStringPersistentListener(b.onClick, manager.LoadScene, sceneName);
    }

    static Canvas MakeWorldCanvas(string name, Vector3 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.position = pos;
        rt.localScale = Vector3.one * 0.001f;
        go.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
        // XRI UI interaction needs the tracked device raycaster + an XR UI input module
        var tdr = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (tdr != null) go.AddComponent(tdr);
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            var xrInput = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
            if (xrInput != null) es.AddComponent(xrInput); else es.AddComponent<InputSystemUIInputModule>();
        }
        return canvas;
    }

    static TMP_Text MakeText(Transform parent, string text, float size, Vector2 pos, Vector2 box)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
        return t;
    }

    static Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(text + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        go.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.9f);
        var label = MakeText(go.transform, text, 30, Vector2.zero, box);
        ((RectTransform)label.transform).anchorMin = Vector2.zero;
        ((RectTransform)label.transform).anchorMax = Vector2.one;
        ((RectTransform)label.transform).sizeDelta = Vector2.zero;
        return go.GetComponent<Button>();
    }

    // short synthesized click saved as an asset (no external audio file needed)
    static AudioClip MakeClick()
    {
        const string path = "Assets/BakeOff/click.wav";
        if (!File.Exists(path))
        {
            int rate = 44100, n = rate / 20;
            var data = new short[n];
            for (int i = 0; i < n; i++)
                data[i] = (short)(Mathf.Sin(2 * Mathf.PI * 1200 * i / rate) * Mathf.Exp(-i / 400f) * 20000);
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                w.Write("RIFF".ToCharArray()); w.Write(36 + n * 2); w.Write("WAVEfmt ".ToCharArray());
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write("data".ToCharArray()); w.Write(n * 2);
                foreach (var s in data) w.Write(s);
            }
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
#endif

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Menu: BakeOff > Create Base Scene
// Builds BakeOff_Base: floor, marker, nine cubes in an arc, pedestal, socket and TrialManager.
public static class BakeOffSceneBuilder
{
    private static readonly float[] RotY = { -60, -45, -30, -15, 0, 15, 30, 45, 60 };
    private static readonly float[] PosX = { -0.520f, -0.424f, -0.300f, -0.155f, 0.000f, 0.155f, 0.300f, 0.424f, 0.520f };
    private static readonly float[] PosZ = { 0.300f, 0.424f, 0.520f, 0.580f, 0.600f, 0.580f, 0.520f, 0.424f, 0.300f };

    [MenuItem("BakeOff/Create Base Scene")]
    public static void CreateBaseScene()
    {
        Directory.CreateDirectory("Assets/BakeOff/Materials");
        Directory.CreateDirectory("Assets/Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = Vector3.zero;

        // Marker at origin
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Marker";
        marker.transform.position = new Vector3(0, 0.005f, 0);
        marker.transform.localScale = new Vector3(0.4f, 0.005f, 0.4f);
        Object.DestroyImmediate(marker.GetComponent<Collider>());

        // Materials
        var target = MakeMaterial("Target", new Color(1f, 0.55f, 0f));
        var distractor = MakeMaterial("Distractor", new Color(0.7f, 0.85f, 1f));

        // Cubes
        var cubes = new XRGrabInteractable[9];
        var cubeParent = new GameObject("Cubes");
        for (int i = 0; i < 9; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Cube_" + (i + 1);
            go.transform.SetParent(cubeParent.transform);
            go.transform.position = new Vector3(PosX[i], 1.0f, PosZ[i]);
            go.transform.rotation = Quaternion.Euler(0, RotY[i], 0);
            go.transform.localScale = Vector3.one * 0.06f;
            go.GetComponent<Renderer>().sharedMaterial = distractor;

            var grab = go.AddComponent<XRGrabInteractable>(); // adds Rigidbody
            grab.throwOnDetach = false;
            var rb = go.GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            cubes[i] = grab;
        }

        // Pedestal (top at 0.9 m)
        var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "Pedestal";
        pedestal.transform.position = new Vector3(0, 0.45f, 0.4f);
        pedestal.transform.localScale = new Vector3(0.2f, 0.45f, 0.2f);

        // Socket
        var socketGo = new GameObject("Socket");
        socketGo.transform.position = new Vector3(0, 0.93f, 0.4f);
        var sphere = socketGo.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 0.06f;
        var socket = socketGo.AddComponent<XRSocketInteractor>();
        var attach = new GameObject("Attach");
        attach.transform.SetParent(socketGo.transform, false);
        socket.attachTransform = attach.transform;
        socket.showInteractableHoverMeshes = true;

        // Trial manager
        var tm = new GameObject("TrialManager").AddComponent<TrialManager>();
        tm.socket = socket;
        tm.cubes = cubes;
        tm.targetMaterial = target;
        tm.distractorMaterial = distractor;
        tm.variant = "A_Ray";

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/BakeOff_Base.unity");
        Debug.Log("BakeOff_Base created. Add the rig, an EndPanel, then save copies A/B/C.");
    }

    private static Material MakeMaterial(string name, Color color)
    {
        string path = "Assets/BakeOff/Materials/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
        mat.color = color;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
#endif

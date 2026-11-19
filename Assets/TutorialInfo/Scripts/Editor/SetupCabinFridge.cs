using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SetupCabinFridge
{
    private const string SOURCE_FBX = "Assets/FurnishedCabin/Meshes/Fridge.fbx";
    private const string BODY_MESH_PATH = "Assets/FurnishedCabin/Meshes/Fridge_Body_Split.asset";
    private const string DOOR_MESH_PATH = "Assets/FurnishedCabin/Meshes/Fridge_Door_Top.asset";

    static SetupCabinFridge()
    {
        EditorApplication.delayCall += ExecuteOnce;
    }

    private static void ExecuteOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Execute();
    }

    [MenuItem("Tools/Setup Cabin Fridge")]
    public static void Execute()
    {
        // 1. Generate clean coordinate-split fridge meshes from Fridge.fbx
        GenerateSplitMeshes();

        // 2. Configure PFB_Fridge if HouseScene is active
        Scene activeScene = SceneManager.GetActiveScene();
        bool isHouseScene = string.Equals(activeScene.name, "HouseScene", System.StringComparison.OrdinalIgnoreCase);
        if (!isHouseScene) return;

        GameObject fridgeGo = GameObject.Find("PFB_Fridge");
        if (fridgeGo == null) return;

        Undo.RegisterFullObjectHierarchyUndo(fridgeGo, "Setup Cabin Fridge");

        // Assign source mesh first so RepairAndPopulateFridge has the full 196-triangle geometry
        Mesh bodyMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BODY_MESH_PATH);
        Mesh doorMesh = AssetDatabase.LoadAssetAtPath<Mesh>(DOOR_MESH_PATH);

        InteractableFoodItem.RepairAndPopulateFridge(fridgeGo);

        MeshFilter bodyMf = fridgeGo.GetComponent<MeshFilter>();
        if (bodyMf != null && bodyMesh != null)
        {
            bodyMf.sharedMesh = bodyMesh;
        }

        Transform doorModel = fridgeGo.transform.Find("Door_Pivot/Door_Model");
        if (doorModel != null && doorMesh != null)
        {
            MeshFilter doorMf = doorModel.GetComponent<MeshFilter>();
            if (doorMf != null) doorMf.sharedMesh = doorMesh;
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        Debug.Log("[SetupCabinFridge] Successfully repaired and configured PFB_Fridge with coordinate-split door, 3D Egg Tray (8 eggs), and 3D Pack of Bacon.");
    }

    public static void GenerateSplitMeshes()
    {
        Mesh sourceMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SOURCE_FBX);
        if (sourceMesh == null) return;

        Vector3[] v = sourceMesh.vertices;
        Vector3[] n = sourceMesh.normals;
        Vector2[] uv = sourceMesh.uv;
        Vector4[] tan = sourceMesh.tangents;
        int[] tris = sourceMesh.triangles;

        List<int> bodyTris = new List<int>();
        List<int> doorTris = new List<int>();

        int triCount = tris.Length / 3;
        for (int i = 0; i < triCount; i++)
        {
            int i0 = tris[i * 3];
            int i1 = tris[i * 3 + 1];
            int i2 = tris[i * 3 + 2];

            Vector3 p0 = v[i0];
            Vector3 p1 = v[i1];
            Vector3 p2 = v[i2];

            // Top door + top handle reside strictly in upper front bounds (Y > 0.758m and Z > 0.415m)
            bool isTopDoor = (p0.y > 0.758f && p1.y > 0.758f && p2.y > 0.758f) &&
                             (p0.z > 0.415f && p1.z > 0.415f && p2.z > 0.415f);

            if (isTopDoor)
            {
                doorTris.Add(i0);
                doorTris.Add(i1);
                doorTris.Add(i2);
            }
            else
            {
                bodyTris.Add(i0);
                bodyTris.Add(i1);
                bodyTris.Add(i2);
            }
        }

        Mesh bodyMesh = BuildSubMesh(v, n, uv, tan, bodyTris, "Fridge_Body_Split");
        Mesh existingBody = AssetDatabase.LoadAssetAtPath<Mesh>(BODY_MESH_PATH);
        if (existingBody == null)
        {
            AssetDatabase.CreateAsset(bodyMesh, BODY_MESH_PATH);
        }
        else
        {
            EditorUtility.CopySerialized(bodyMesh, existingBody);
        }

        Mesh doorMesh = BuildSubMesh(v, n, uv, tan, doorTris, "Fridge_Door_Top");
        Mesh existingDoor = AssetDatabase.LoadAssetAtPath<Mesh>(DOOR_MESH_PATH);
        if (existingDoor == null)
        {
            AssetDatabase.CreateAsset(doorMesh, DOOR_MESH_PATH);
        }
        else
        {
            EditorUtility.CopySerialized(doorMesh, existingDoor);
        }

        AssetDatabase.SaveAssets();
    }

    private static Mesh BuildSubMesh(Vector3[] v, Vector3[] n, Vector2[] uv, Vector4[] tan, List<int> triIndices, string meshName)
    {
        Dictionary<int, int> remap = new Dictionary<int, int>();
        List<Vector3> newV = new List<Vector3>();
        List<Vector3> newN = new List<Vector3>();
        List<Vector2> newUV = new List<Vector2>();
        List<Vector4> newTan = new List<Vector4>();
        List<int> newTris = new List<int>();

        for (int i = 0; i < triIndices.Count; i++)
        {
            int oldIdx = triIndices[i];
            if (!remap.TryGetValue(oldIdx, out int newIdx))
            {
                newIdx = newV.Count;
                remap[oldIdx] = newIdx;
                newV.Add(v[oldIdx]);
                if (n != null && oldIdx < n.Length) newN.Add(n[oldIdx]);
                if (uv != null && oldIdx < uv.Length) newUV.Add(uv[oldIdx]);
                if (tan != null && oldIdx < tan.Length) newTan.Add(tan[oldIdx]);
            }
            newTris.Add(newIdx);
        }

        Mesh m = new Mesh();
        m.name = meshName;
        m.vertices = newV.ToArray();
        if (newN.Count == newV.Count) m.normals = newN.ToArray();
        if (newUV.Count == newV.Count) m.uv = newUV.ToArray();
        if (newTan.Count == newV.Count) m.tangents = newTan.ToArray();
        m.triangles = newTris.ToArray();
        m.RecalculateBounds();
        return m;
    }
}

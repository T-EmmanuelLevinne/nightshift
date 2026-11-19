using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RemoveEntranceGlass
{
    private const string FbxPath = "Assets/small-red-gas-station/source/stacja.fbx";
    private const string AssetFolder = "Assets/small-red-gas-station";

    [MenuItem("Tools/Gas Station/Remove Doorway Only (2 Center Panels in front of mat)")]
    public static void RemoveDoorwayOnly()
    {
        ProcessMesh(removeDoorwayOnly: true);
    }

    [MenuItem("Tools/Gas Station/Remove All 3 Glass Panels in that Bay")]
    public static void RemoveAllThreePanels()
    {
        ProcessMesh(removeDoorwayOnly: false);
    }

    [MenuItem("Tools/Gas Station/Restore Original Glass")]
    public static void RestoreOriginalGlass()
    {
        GameObject stacja = GameObject.Find("stacja");
        if (stacja == null)
        {
            Debug.LogWarning("[RemoveEntranceGlass] 'stacja' not found in scene.");
            return;
        }

        Transform plane1 = FindPlane1(stacja);
        if (plane1 == null) return;

        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        Mesh originalMesh = null;
        foreach (var sub in subAssets)
        {
            if (sub is Mesh m && m.name == "Plane.001")
            {
                originalMesh = m;
                break;
            }
        }

        if (originalMesh != null)
        {
            var mf = plane1.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = originalMesh;

            var mc = plane1.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = originalMesh;

            EditorUtility.SetDirty(plane1.gameObject);
            MarkSceneDirty();
            Debug.Log("[RemoveEntranceGlass] Restored original Plane.001 glass mesh.");
        }
    }

    private static void ProcessMesh(bool removeDoorwayOnly)
    {
        GameObject stacja = GameObject.Find("stacja");
        if (stacja == null)
        {
            Debug.LogWarning("[RemoveEntranceGlass] 'stacja' not found in scene.");
            return;
        }

        Transform plane1 = FindPlane1(stacja);
        if (plane1 == null) return;

        MeshFilter mf = plane1.GetComponent<MeshFilter>();
        if (mf == null) return;

        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        Mesh sourceMesh = null;
        foreach (var sub in subAssets)
        {
            if (sub is Mesh m && m.name == "Plane.001")
            {
                sourceMesh = m;
                break;
            }
        }

        if (sourceMesh == null)
        {
            sourceMesh = mf.sharedMesh;
        }

        Mesh newMesh = UnityEngine.Object.Instantiate(sourceMesh);
        string modeName = removeDoorwayOnly ? "DoorwayRemoved" : "Bay3GlassRemoved";
        newMesh.name = $"Plane.001_{modeName}";

        // Submesh 2 is Material.032 (glass)
        if (newMesh.subMeshCount > 2)
        {
            int[] glassTris = newMesh.GetTriangles(2);
            Vector3[] verts = newMesh.vertices;
            List<int> keptTris = new List<int>();

            for (int i = 0; i < glassTris.Length; i += 3)
            {
                Vector3 v0 = verts[glassTris[i]];
                Vector3 v1 = verts[glassTris[i + 1]];
                Vector3 v2 = verts[glassTris[i + 2]];
                Vector3 center = (v0 + v1 + v2) / 3f;

                // In Unity local coordinates for Plane.001:
                // FBX Y maps to local Y, FBX Z maps to local Z
                // Face 327 (left window on curb): Y ~ 5.37-6.72
                // Face 326 (left door panel): Y ~ 6.75-8.10
                // Face 325 (right door panel): Y ~ 8.13-9.48
                // Faces 322-324 (cashier bay): Y > 10.0
                bool isDoorway = (center.y >= 6.7f && center.y <= 9.6f && center.z <= 0.8f);
                bool isLeftWindow = (center.y >= 5.2f && center.y < 6.7f && center.z <= 0.8f);

                bool shouldRemove = removeDoorwayOnly ? isDoorway : (isDoorway || isLeftWindow);

                if (!shouldRemove)
                {
                    keptTris.Add(glassTris[i]);
                    keptTris.Add(glassTris[i + 1]);
                    keptTris.Add(glassTris[i + 2]);
                }
            }

            newMesh.SetTriangles(keptTris.ToArray(), 2);
        }

        // Save as persistent asset so it won't be lost
        string savePath = $"{AssetFolder}/{newMesh.name}.asset";
        AssetDatabase.CreateAsset(newMesh, savePath);
        AssetDatabase.SaveAssets();

        Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(savePath);
        mf.sharedMesh = savedMesh;

        MeshCollider mc = plane1.GetComponent<MeshCollider>();
        if (mc != null)
        {
            mc.sharedMesh = savedMesh;
        }

        EditorUtility.SetDirty(plane1.gameObject);
        MarkSceneDirty();

        string actionDesc = removeDoorwayOnly 
            ? "Removed the 2 center doorway panels in front of the mat."
            : "Removed all 3 glass panels in that bay (doorway + side curb window).";
        Debug.Log($"[RemoveEntranceGlass] Successfully applied! {actionDesc}");
    }

    private static Transform FindPlane1(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Plane.001") return t;
        }
        Debug.LogWarning("[RemoveEntranceGlass] Could not find 'Plane.001' under " + root.name);
        return null;
    }

    private static void MarkSceneDirty()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!EditorApplication.isPlaying && !Application.isPlaying)
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
    }
}

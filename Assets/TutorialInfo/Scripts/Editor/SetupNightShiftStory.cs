using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SetupNightShiftStory
{
    private const string CanPrefabPath = "Assets/Resources/CustomerCocaColaCan.prefab";
    private const string SingleCanMeshPath = "Assets/soda-cans-classic-355ml-free-download/Meshes/SingleCan_CocaCola.asset";
    private const string CokeMatPath = "Assets/soda-cans-classic-355ml-free-download/Materials/Coca-Cola_BaseColor.mat";

    static SetupNightShiftStory()
    {
        EditorApplication.delayCall += () =>
        {
            EnsureCocaColaCanPrefabAsset();
            EnsureStoryControllerInActiveScene();
        };
    }

    private static void EnsureCocaColaCanPrefabAsset()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CanPrefabPath) != null) return;

        Mesh canMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SingleCanMeshPath);
        Material cokeMat = AssetDatabase.LoadAssetAtPath<Material>(CokeMatPath);
        if (canMesh == null || cokeMat == null) return;

        if (!Directory.Exists("Assets/Resources"))
        {
            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.Refresh();
        }

        GameObject temp = new GameObject("CustomerCocaColaCan");
        MeshFilter mf = temp.AddComponent<MeshFilter>();
        mf.sharedMesh = canMesh;
        MeshRenderer mr = temp.AddComponent<MeshRenderer>();
        mr.sharedMaterial = cokeMat;

        PrefabUtility.SaveAsPrefabAsset(temp, CanPrefabPath);
        Object.DestroyImmediate(temp);
    }

    [MenuItem("Tools/Night Shift/Setup Story Controller in Scene")]
    public static void EnsureStoryControllerInActiveScene()
    {
        // Strictly only create in GasScene2!
        UnityEngine.SceneManagement.Scene activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, "GasScene2", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        NightShiftStoryController existing = Object.FindAnyObjectByType<NightShiftStoryController>();
        if (existing == null)
        {
            GameObject go = new GameObject("NightShift_StoryController");
            go.AddComponent<NightShiftStoryController>();
            Undo.RegisterCreatedObjectUndo(go, "Create NightShift_StoryController");
            if (!EditorApplication.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
            Debug.Log("[SetupNightShiftStory] Created NightShift_StoryController in GasScene2.");
        }
    }
}


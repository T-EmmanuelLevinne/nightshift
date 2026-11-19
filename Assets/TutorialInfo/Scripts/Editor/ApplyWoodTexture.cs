using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// [InitializeOnLoad]
public static class ApplyWoodTexture
{
    private const string TexturePath = "Assets/fire-tower/source/raw_wooden_floor_01_diff_4k.jpg";
    private const string MaterialPath = "Assets/fire-tower/source/WatchTower_Wood.mat";
    private const string FbxPath = "Assets/fire-tower/source/FireTower.fbx";

    static ApplyWoodTexture()
    {
        // EditorApplication.delayCall += Execute;
    }

    [MenuItem("Tools/Apply Wood Texture to FireTower")]
    public static void Execute()
    {
        Debug.Log("[ApplyWoodTexture] Starting process...");

        // 1. Ensure texture exists
        Texture2D woodTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (woodTexture == null)
        {
            Debug.LogError($"[ApplyWoodTexture] Texture not found at {TexturePath}");
            return;
        }

        // 2. Create or load the Wood Material
        Material woodMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (woodMaterial == null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                litShader = Shader.Find("Standard");
            }
            woodMaterial = new Material(litShader);
            woodMaterial.name = "WatchTower_Wood";
            woodMaterial.SetTexture("_BaseMap", woodTexture);
            woodMaterial.SetTexture("_MainTex", woodTexture);
            woodMaterial.SetColor("_BaseColor", Color.white);
            woodMaterial.SetFloat("_Smoothness", 0.35f);

            AssetDatabase.CreateAsset(woodMaterial, MaterialPath);
            Debug.Log($"[ApplyWoodTexture] Created new wood material at {MaterialPath}");
        }
        else
        {
            woodMaterial.SetTexture("_BaseMap", woodTexture);
            woodMaterial.SetTexture("_MainTex", woodTexture);
            EditorUtility.SetDirty(woodMaterial);
            Debug.Log($"[ApplyWoodTexture] Updated existing wood material at {MaterialPath}");
        }

        // 3. Remap Material on FireTower.fbx
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer != null)
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            foreach (var subAsset in subAssets)
            {
                if (subAsset is Material mat && mat.name == "WatchTower")
                {
                    var id = new AssetImporter.SourceAssetIdentifier(subAsset);
                    importer.AddRemap(id, woodMaterial);
                    Debug.Log($"[ApplyWoodTexture] Remapped subAsset material 'WatchTower' to {MaterialPath}");
                }
            }

            // Also try explicit name identifier fallback
            var nameId = new AssetImporter.SourceAssetIdentifier(typeof(Material), "WatchTower");
            importer.AddRemap(nameId, woodMaterial);

            AssetDatabase.WriteImportSettingsIfDirty(FbxPath);
            importer.SaveAndReimport();
            Debug.Log($"[ApplyWoodTexture] Saved and reimported {FbxPath}");
        }
        else
        {
            Debug.LogWarning($"[ApplyWoodTexture] ModelImporter not found for {FbxPath}");
        }

        // 4. Update scene instances
        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        int updatedCount = 0;

        foreach (var root in rootObjects)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in renderers)
            {
                Material[] sharedMats = mr.sharedMaterials;
                bool modified = false;

                for (int i = 0; i < sharedMats.Length; i++)
                {
                    if (sharedMats[i] != null && sharedMats[i].name.StartsWith("WatchTower") && !sharedMats[i].name.Contains("001"))
                    {
                        sharedMats[i] = woodMaterial;
                        modified = true;
                        updatedCount++;
                    }
                }

                if (modified)
                {
                    mr.sharedMaterials = sharedMats;
                    EditorUtility.SetDirty(mr);
                }
            }
        }

        Debug.Log($"[ApplyWoodTexture] Updated {updatedCount} material slots in active scene '{activeScene.name}'.");

        // 5. Save Scene and Assets
        if (!EditorApplication.isPlaying && !Application.isPlaying)
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[ApplyWoodTexture] Completed successfully!");
    }
}

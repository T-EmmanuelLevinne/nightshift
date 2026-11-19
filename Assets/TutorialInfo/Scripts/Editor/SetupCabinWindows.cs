using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Converts all 6 blocked black-alpha window panes in HouseScene's PFB_Building_Base
/// into real see-through URP Transparent glass panes, while keeping the exact white frames,
/// grid mullions, curtains, and solid window collision bounds intact.
/// </summary>
[InitializeOnLoad]
public static class SetupCabinWindows
{
    private const string SourceFbxPath = "Assets/FurnishedCabin/Meshes/Building.fbx";
    private const string StructureMeshAssetPath = "Assets/FurnishedCabin/Meshes/Building_WindowsRemoved.asset";
    private const string GlassMeshAssetPath = "Assets/FurnishedCabin/Meshes/Building_WindowGlass.asset";
    private const string GlassMaterialAssetPath = "Assets/FurnishedCabin/Meshes/Materials/CabinWindowGlass_MAT.mat";
    private const string GlassSheenTextureAssetPath = "Assets/FurnishedCabin/Meshes/Materials/CabinWindowGlassSheen_TEX.png";
    private const string SessionKey = "SetupCabinWindows_Applied_V3";

    static SetupCabinWindows()
    {
        EditorApplication.delayCall += AutoApplyOnce;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void AutoApplyOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SessionState.GetBool(SessionKey, false)) return;

        SessionState.SetBool(SessionKey, true);
        Execute(verbose: false);
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (string.Equals(scene.name, "HouseScene", StringComparison.OrdinalIgnoreCase))
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Execute(verbose: false);
                }
            };
        }
    }

    [MenuItem("Tools/House Scene/Convert Cabin Windows to Real Glass")]
    public static void MenuExecute()
    {
        Execute(verbose: true);
    }

    public static void Execute(bool verbose = true)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Material glassMat = EnsureGlassMaterialAsset();
        if (!EnsureSplitMeshAssets(out Mesh structureAsset, out Mesh glassAsset, out int glassTriCount))
        {
            if (verbose)
            {
                Debug.LogWarning($"[SetupCabinWindows] Could not extract window glass triangles from '{SourceFbxPath}'.");
            }
            return;
        }

        Scene houseScene = SceneManager.GetSceneByName("HouseScene");
        if (!houseScene.IsValid() || !houseScene.isLoaded) return;

        GameObject buildingBase = CabinWindowUtility.FindBuildingBaseInScene(houseScene);
        if (buildingBase == null)
        {
            if (verbose)
            {
                Debug.LogWarning("[SetupCabinWindows] 'PFB_Building_Base' not found in HouseScene.");
            }
            return;
        }

        MeshFilter mf = buildingBase.GetComponent<MeshFilter>();
        if (mf == null) return;

        bool sceneModified = false;

        if (mf.sharedMesh != structureAsset)
        {
            Undo.RecordObject(mf, "Assign Cutout Window Mesh to Cabin");
            mf.sharedMesh = structureAsset;
            EditorUtility.SetDirty(mf);
            sceneModified = true;
        }

        Transform existingChild = buildingBase.transform.Find(CabinWindowUtility.GlassChildObjectName);
        if (existingChild == null)
        {
            Undo.RecordObject(buildingBase, "Create Cabin Window Glass Child");
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(existingChild.gameObject, "Update Cabin Window Glass");
        }

        if (CabinWindowUtility.ConfigureGlassChildRenderer(buildingBase.transform, glassAsset, glassMat))
        {
            Transform glassChild = buildingBase.transform.Find(CabinWindowUtility.GlassChildObjectName);
            if (glassChild != null) EditorUtility.SetDirty(glassChild.gameObject);
            sceneModified = true;
        }

        if (sceneModified)
        {
            EditorSceneManager.MarkSceneDirty(houseScene);
            if (verbose)
            {
                Debug.Log($"[SetupCabinWindows] Converted all 6 cabin windows ({glassTriCount} glass triangles) to clear URP Transparent glass on '{buildingBase.name}'.");
            }
        }
        else if (verbose)
        {
            Debug.Log($"[SetupCabinWindows] Cabin windows are already configured ({glassTriCount} glass triangles).");
        }
    }

    private static Material EnsureGlassMaterialAsset()
    {
        EnsureParentDirectory(GlassMaterialAssetPath);

        Texture2D sheenAsset = EnsureGlassSheenTextureAsset();
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialAssetPath);
        if (existing != null)
        {
            CabinWindowUtility.ApplyGlassMaterialProperties(existing, sheenAsset);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        Material created = CabinWindowUtility.CreateTransparentGlassMaterial();
        CabinWindowUtility.ApplyGlassMaterialProperties(created, sheenAsset);
        AssetDatabase.CreateAsset(created, GlassMaterialAssetPath);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialAssetPath);
    }

    private static Texture2D EnsureGlassSheenTextureAsset()
    {
        Texture2D tex = CabinWindowUtility.GetOrCreateWindowGlassSheenTexture();
        if (tex != null)
        {
            byte[] png = tex.EncodeToPNG();
            if (png != null && png.Length > 0)
            {
                bool isNew = !File.Exists(GlassSheenTextureAssetPath);
                byte[] existingBytes = isNew ? null : File.ReadAllBytes(GlassSheenTextureAssetPath);
                if (isNew || BytesDiffer(existingBytes, png))
                {
                    File.WriteAllBytes(GlassSheenTextureAssetPath, png);
                    AssetDatabase.ImportAsset(GlassSheenTextureAssetPath, ImportAssetOptions.ForceUpdate);
                }
                TextureImporter importer = AssetImporter.GetAtPath(GlassSheenTextureAssetPath) as TextureImporter;
                if (importer != null && (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Repeat))
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.SaveAndReimport();
                }
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(GlassSheenTextureAssetPath);
    }

    private static bool BytesDiffer(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return true;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return true;
        }
        return false;
    }

    private static bool EnsureSplitMeshAssets(out Mesh structureAsset, out Mesh glassAsset, out int glassTriCount)
    {
        structureAsset = AssetDatabase.LoadAssetAtPath<Mesh>(StructureMeshAssetPath);
        glassAsset = AssetDatabase.LoadAssetAtPath<Mesh>(GlassMeshAssetPath);
        glassTriCount = glassAsset != null ? glassAsset.triangles.Length / 3 : 0;

        if (structureAsset != null && glassAsset != null && glassTriCount > 0)
        {
            return true;
        }

        Mesh sourceMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SourceFbxPath);
        if (sourceMesh == null) return false;

        // Find BuildingB submesh from PFB_Building_Base prefab or default slot 1
        int bSubMesh = sourceMesh.subMeshCount > 1 ? 1 : 0;
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FurnishedCabin/Prefabs/PFB_Building_Base.prefab");
        if (prefabAsset != null)
        {
            MeshRenderer prefabMr = prefabAsset.GetComponent<MeshRenderer>();
            if (prefabMr != null)
            {
                bSubMesh = CabinWindowUtility.ResolveBuildingBSubMeshIndex(sourceMesh, prefabMr);
            }
        }

        glassTriCount = CabinWindowUtility.SplitBuildingWindows(sourceMesh, bSubMesh, out Mesh newStructure, out Mesh newGlass);
        if (glassTriCount <= 0 || newStructure == null || newGlass == null)
        {
            return false;
        }

        EnsureParentDirectory(StructureMeshAssetPath);
        SaveOrUpdateMeshAsset(newStructure, StructureMeshAssetPath);
        SaveOrUpdateMeshAsset(newGlass, GlassMeshAssetPath);
        AssetDatabase.SaveAssets();

        structureAsset = AssetDatabase.LoadAssetAtPath<Mesh>(StructureMeshAssetPath);
        glassAsset = AssetDatabase.LoadAssetAtPath<Mesh>(GlassMeshAssetPath);
        return structureAsset != null && glassAsset != null;
    }

    private static void SaveOrUpdateMeshAsset(Mesh source, string assetPath)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(source, assetPath);
        }
        else
        {
            EditorUtility.CopySerialized(source, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    private static void EnsureParentDirectory(string assetPath)
    {
        string dir = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }
}

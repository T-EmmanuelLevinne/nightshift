using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RecreateStoreGondolaShelf
{
    private const string FbxPath = "Assets/small-red-gas-station/source/Store_Gondola_Shelf.fbx";
    private const string Shelf1FbxPath = "Assets/small-red-gas-station/source/Shelf1_RestockReady.fbx";
    private const string MaterialsFolder = "Assets/small-red-gas-station/Materials";
    private const string PrefabsFolder = "Assets/small-red-gas-station/Prefabs";
    private const string StockedPrefabPath = "Assets/small-red-gas-station/Prefabs/Store_Gondola_Shelf_Stocked.prefab";
    private const string EmptyPrefabPath = "Assets/small-red-gas-station/Prefabs/Store_Gondola_Shelf_Empty.prefab";
    private const string SetupPrefKey = "GondolaShelf_RestockSlot_V4_Applied";

    private static readonly string[] ShelfMaterialNames = new string[]
    {
        "Material.008",
        "Material.003",
        "Material.004",
        "Material.005",
        "Material.007",
        "Material.019",
        "Material.002",
        "Material.006",
        "Material.009",
        "Material.010"
    };

    static RecreateStoreGondolaShelf()
    {
        EditorApplication.delayCall += AutoSetupOnce;
    }

    private static void AutoSetupOnce()
    {
        if (!SessionState.GetBool(SetupPrefKey, false) || !File.Exists(StockedPrefabPath) || !File.Exists(EmptyPrefabPath))
        {
            SessionState.SetBool(SetupPrefKey, true);
            EnsureShelfMaterialsAndPrefabs(forceRebuildPrefabs: true);
            ApplyEmptyCanSlotToSceneShelves();
        }
    }

    [MenuItem("Tools/Gas Station/Spawn Store Shelf (Stocked - With Empty Can Restock Slot)")]
    public static void SpawnStockedShelf()
    {
        EnsureShelfMaterialsAndPrefabs(forceRebuildPrefabs: true);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StockedPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[RecreateStoreGondolaShelf] Could not load Stocked Prefab at " + StockedPrefabPath);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "Store_Gondola_Shelf_Stocked";
        PositionNearStoreAisle(instance);

        Undo.RegisterCreatedObjectUndo(instance, "Spawn Stocked Store Gondola Shelf");
        Selection.activeGameObject = instance;
        SceneView.FrameLastActiveSceneView();
        Debug.Log("[RecreateStoreGondolaShelf] Spawned 'Store_Gondola_Shelf_Stocked' with empty can restock slot & fixed textures!");
    }

    [MenuItem("Tools/Gas Station/Spawn Store Shelf (Empty Shell/Frame Only)")]
    public static void SpawnEmptyShelf()
    {
        EnsureShelfMaterialsAndPrefabs(forceRebuildPrefabs: false);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EmptyPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[RecreateStoreGondolaShelf] Could not load Empty Prefab at " + EmptyPrefabPath);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "Store_Gondola_Shelf_Empty";
        PositionNearStoreAisle(instance);

        Undo.RegisterCreatedObjectUndo(instance, "Spawn Empty Store Gondola Shelf");
        Selection.activeGameObject = instance;
        SceneView.FrameLastActiveSceneView();
        Debug.Log("[RecreateStoreGondolaShelf] Spawned 'Store_Gondola_Shelf_Empty' (empty shelf frame with exact store textures & design)!");
    }

    public static void EnsureShelfMaterialsAndPrefabs(bool forceRebuildPrefabs = false)
    {
        FixMissingMaterial005Texture();
        RemapFbxMaterials(FbxPath);
        RemapFbxMaterials(Shelf1FbxPath);

        if (!Directory.Exists(PrefabsFolder))
        {
            Directory.CreateDirectory(PrefabsFolder);
            AssetDatabase.Refresh();
        }

        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxAsset == null) return;

        // 1. Build Stocked Prefab (with empty restock space on 2nd shelf and inactive Restock_Cans_Slot)
        if (forceRebuildPrefabs || !File.Exists(StockedPrefabPath))
        {
            GameObject tempStocked = Object.Instantiate(fbxAsset);
            tempStocked.name = "Store_Gondola_Shelf_Stocked";
            ApplyMaterialsExplicitly(tempStocked);

            Transform restockSlot = tempStocked.transform.Find("Restock_Cans_Slot");
            if (restockSlot != null)
            {
                // Leave inactive so the shelf spot is visibly empty until restocked
                restockSlot.gameObject.SetActive(false);

                ShelfRestockTarget rt = tempStocked.GetComponent<ShelfRestockTarget>();
                if (rt == null) rt = tempStocked.AddComponent<ShelfRestockTarget>();
                rt.restockCansSlot = restockSlot.gameObject;
            }

            AddCleanBoxCollider(tempStocked);
            PrefabUtility.SaveAsPrefabAsset(tempStocked, StockedPrefabPath);
            Object.DestroyImmediate(tempStocked);
        }

        // 2. Build Empty Shell/Frame Prefab
        if (forceRebuildPrefabs || !File.Exists(EmptyPrefabPath))
        {
            GameObject tempEmpty = Object.Instantiate(fbxAsset);
            tempEmpty.name = "Store_Gondola_Shelf_Empty";
            Transform prod = tempEmpty.transform.Find("Gondola_Shelf_Products");
            if (prod != null)
            {
                Object.DestroyImmediate(prod.gameObject);
            }
            Transform restockSlot = tempEmpty.transform.Find("Restock_Cans_Slot");
            if (restockSlot != null)
            {
                Object.DestroyImmediate(restockSlot.gameObject);
            }
            ApplyMaterialsExplicitly(tempEmpty);
            AddCleanBoxCollider(tempEmpty);
            PrefabUtility.SaveAsPrefabAsset(tempEmpty, EmptyPrefabPath);
            Object.DestroyImmediate(tempEmpty);
        }

        AssetDatabase.SaveAssets();
    }

    private static void FixMissingMaterial005Texture()
    {
        Material mat005 = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Material.005.mat");
        Material mat003 = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Material.003.mat");
        if (mat005 != null && mat003 != null)
        {
            Texture canTex = mat003.GetTexture("_BaseMap") ?? mat003.mainTexture;
            if (canTex != null && mat005.GetTexture("_BaseMap") == null)
            {
                mat005.SetTexture("_BaseMap", canTex);
                mat005.SetTexture("_MainTex", canTex);
                mat005.SetColor("_BaseColor", Color.white);
                mat005.SetColor("_Color", Color.white);
                EditorUtility.SetDirty(mat005);
            }
        }
    }

    private static void RemapFbxMaterials(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;

        bool changed = false;
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            changed = true;
        }

        foreach (string matName in ShelfMaterialNames)
        {
            string matPath = $"{MaterialsFolder}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);
                changed = true;
            }
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    public static void ApplyEmptyCanSlotToSceneShelves()
    {
        // 1. Update Shelf1 in the scene so the cans spot on the 2nd tier is empty right away
        GameObject shelf1 = GameObject.Find("Shelf1");
        if (shelf1 != null)
        {
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(Shelf1FbxPath);
            Mesh mainMeshWithEmptySlot = null;
            Mesh restockCansMesh = null;

            foreach (Object asset in allAssets)
            {
                if (asset is Mesh m)
                {
                    if (m.name == "Shelf1_Mesh_With_Empty_Slot")
                        mainMeshWithEmptySlot = m;
                    else if (m.name == "Shelf1_Restock_Cans")
                        restockCansMesh = m;
                }
            }

            if (mainMeshWithEmptySlot != null)
            {
                MeshFilter mf = shelf1.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != mainMeshWithEmptySlot)
                {
                    mf.sharedMesh = mainMeshWithEmptySlot;
                    EditorUtility.SetDirty(mf);
                }

                MeshCollider mc = shelf1.GetComponent<MeshCollider>();
                if (mc != null && mc.sharedMesh != mainMeshWithEmptySlot)
                {
                    mc.sharedMesh = mainMeshWithEmptySlot;
                    EditorUtility.SetDirty(mc);
                }
            }

            if (restockCansMesh != null)
            {
                Transform existingSlot = shelf1.transform.Find("Restock_Cans_Slot");
                GameObject slotObj;
                if (existingSlot == null)
                {
                    slotObj = new GameObject("Restock_Cans_Slot");
                    slotObj.transform.SetParent(shelf1.transform, false);
                    slotObj.transform.localPosition = Vector3.zero;
                    slotObj.transform.localRotation = Quaternion.identity;
                    slotObj.transform.localScale = Vector3.one;
                }
                else
                {
                    slotObj = existingSlot.gameObject;
                }

                MeshFilter slotMf = slotObj.GetComponent<MeshFilter>();
                if (slotMf == null) slotMf = slotObj.AddComponent<MeshFilter>();
                slotMf.sharedMesh = restockCansMesh;

                MeshRenderer slotMr = slotObj.GetComponent<MeshRenderer>();
                if (slotMr == null) slotMr = slotObj.AddComponent<MeshRenderer>();
                slotMr.sharedMaterials = new Material[]
                {
                    AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Material.003.mat"),
                    AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Material.004.mat"),
                    AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Material.005.mat")
                };

                // Keep inactive so the shelf space is empty for restocking
                slotObj.SetActive(false);

                ShelfRestockTarget restockTarget = shelf1.GetComponent<ShelfRestockTarget>();
                if (restockTarget == null) restockTarget = shelf1.AddComponent<ShelfRestockTarget>();
                restockTarget.restockCansSlot = slotObj;

                EditorUtility.SetDirty(shelf1);
                if (shelf1.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(shelf1.scene);
                }
            }
        }

        // 2. Also update any spawned Store_Gondola_Shelf_Stocked instances in the scene
        GameObject stockedInstance = GameObject.Find("Store_Gondola_Shelf_Stocked");
        if (stockedInstance != null)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(stockedInstance))
            {
                PrefabUtility.RevertPrefabInstance(stockedInstance, InteractionMode.AutomatedAction);
            }

            Transform slot = stockedInstance.transform.Find("Restock_Cans_Slot");
            if (slot != null)
            {
                ShelfRestockTarget rt = stockedInstance.GetComponent<ShelfRestockTarget>();
                if (rt == null) rt = stockedInstance.AddComponent<ShelfRestockTarget>();
                rt.restockCansSlot = slot.gameObject;
                EditorUtility.SetDirty(stockedInstance);
            }
        }

        SceneView.RepaintAll();
    }

    private static void ApplyMaterialsExplicitly(GameObject root)
    {
        Transform frame = root.transform.Find("Gondola_Shelf_Frame");
        if (frame != null)
        {
            MeshRenderer mr = frame.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material m9 = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Material.009.mat");
                Material m10 = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Material.010.mat");
                if (m9 != null && m10 != null)
                {
                    mr.sharedMaterials = new Material[] { m9, m10 };
                }
            }
        }

        Transform prod = root.transform.Find("Gondola_Shelf_Products");
        if (prod != null)
        {
            MeshRenderer mr = prod.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                string[] prodMatNames = new string[]
                {
                    "Material.008", "Material.003", "Material.004", "Material.005",
                    "Material.007", "Material.019", "Material.002", "Material.006"
                };
                Material[] mats = new Material[prodMatNames.Length];
                for (int i = 0; i < prodMatNames.Length; i++)
                {
                    mats[i] = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{prodMatNames[i]}.mat");
                }
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        Transform restockSlot = root.transform.Find("Restock_Cans_Slot");
        if (restockSlot != null)
        {
            MeshRenderer mr = restockSlot.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterials = new Material[]
                {
                    AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Material.003.mat"),
                    AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Material.004.mat"),
                    AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Material.005.mat")
                };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }

    private static void AddCleanBoxCollider(GameObject root)
    {
        Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
        {
            b.Encapsulate(rends[i].bounds);
        }

        BoxCollider bc = root.GetComponent<BoxCollider>();
        if (bc == null) bc = root.AddComponent<BoxCollider>();
        bc.center = root.transform.InverseTransformPoint(b.center);
        bc.size = b.size;
    }

    private static void PositionNearStoreAisle(GameObject instance)
    {
        GameObject refShelf = GameObject.Find("Shelf2") ?? GameObject.Find("Shelf3") ?? GameObject.Find("Shelf1");
        if (refShelf != null)
        {
            instance.transform.position = refShelf.transform.position + new Vector3(0f, 0f, -1.5f);
            instance.transform.rotation = Quaternion.identity;
        }
        else
        {
            instance.transform.position = new Vector3(-8.5f, 0.15f, -1.5f);
            instance.transform.rotation = Quaternion.identity;
        }
    }
}

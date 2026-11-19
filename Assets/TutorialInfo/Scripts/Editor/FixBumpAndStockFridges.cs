using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// [InitializeOnLoad]
public static class FixBumpAndStockFridges
{
    private struct BrandStockInfo
    {
        public string objectName;
        public string objFileName;
        public string matFileName;
    }

    private static readonly BrandStockInfo[] Brands = new BrandStockInfo[]
    {
        new BrandStockInfo { objectName = "Cans_DrPepper", objFileName = "Cans_DrPepper.obj", matFileName = "Dr_Pepper_BaseColor.mat" },
        new BrandStockInfo { objectName = "Cans_Sprite", objFileName = "Cans_Sprite.obj", matFileName = "Sprite_BaseColor.mat" },
        new BrandStockInfo { objectName = "Cans_CocaCola", objFileName = "Cans_CocaCola.obj", matFileName = "Coca-Cola_BaseColor.mat" },
        new BrandStockInfo { objectName = "Cans_CocaColaZero", objFileName = "Cans_CocaColaZero.obj", matFileName = "Coca-Cola_Zero_Sugar_BaseColor.mat" },
        new BrandStockInfo { objectName = "Cans_Pepsi", objFileName = "Cans_Pepsi.obj", matFileName = "Pepsi_BaseColor.mat" },
        new BrandStockInfo { objectName = "Cans_PepsiZero", objFileName = "Cans_PepsiZero.obj", matFileName = "Pepsi_Zero_Sugar_BaseColor.mat" },
    };

    static FixBumpAndStockFridges()
    {
        // EditorApplication.delayCall += ExecuteFix;
    }

    [MenuItem("Tools/Gas Station/Apply Fridge Stocking & Bump Removal")]
    public static void ExecuteFix()
    {
        Debug.Log("[FixBumpAndStockFridges] Starting automated fix for ground bumps, empty fridges, and ambience...");

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded || string.IsNullOrEmpty(activeScene.path))
        {
            activeScene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        }

        bool sceneModified = false;

        // 1. REMOVE BUMP COLLIDERS ON ASPHALT / WALKWAY CUBES
        GameObject stacja = GameObject.Find("stacja");
        if (stacja != null)
        {
            int removedCount = 0;
            foreach (Transform child in stacja.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("Cube"))
                {
                    MeshCollider mc = child.GetComponent<MeshCollider>();
                    if (mc != null)
                    {
                        Debug.Log($"[FixBumpAndStockFridges] Removing speedbump MeshCollider from: {child.name} at {child.position}");
                        UnityEngine.Object.DestroyImmediate(mc, true);
                        removedCount++;
                        sceneModified = true;
                    }
                }
            }
            Debug.Log($"[FixBumpAndStockFridges] Cleaned {removedCount} bump colliders from ground curbs/cubes.");
        }

        // 2. LOAD BRAND MESHES AND MATERIALS
        Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        Dictionary<string, Material> materials = new Dictionary<string, Material>();

        foreach (var b in Brands)
        {
            string objPath = $"Assets/soda-cans-classic-355ml-free-download/source/{b.objFileName}";
            string matPath = $"Assets/soda-cans-classic-355ml-free-download/Materials/{b.matFileName}";

            Mesh m = null;
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(objPath);
            foreach (var a in assets)
            {
                if (a is Mesh meshAsset)
                {
                    m = meshAsset;
                    break;
                }
            }

            if (m == null)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
                if (model != null)
                {
                    MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
                    if (mf != null) m = mf.sharedMesh;
                }
            }

            if (m != null) meshes[b.objectName] = m;
            else Debug.LogError($"[FixBumpAndStockFridges] Failed to locate mesh for {b.objectName}");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null) materials[b.objectName] = mat;
            else Debug.LogError($"[FixBumpAndStockFridges] Failed to locate material for {b.objectName}");
        }

        // 3. RESTOCK ALL DOUBLE FRIDGES
        List<Transform> fridges = new List<Transform>();
        Transform[] allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (var t in allTransforms)
        {
            if (t.name.StartsWith("Double_Beverage_Fridge"))
            {
                fridges.Add(t);
            }
        }

        Debug.Log($"[FixBumpAndStockFridges] Found {fridges.Count} commercial double beverage fridges.");

        foreach (var fridge in fridges)
        {
            Transform cansGroup = fridge.Find("Drink_Cans");
            if (cansGroup == null)
            {
                GameObject cg = new GameObject("Drink_Cans");
                cg.transform.SetParent(fridge, false);
                cg.transform.localPosition = Vector3.zero;
                cg.transform.localRotation = Quaternion.identity;
                cg.transform.localScale = Vector3.one;
                cansGroup = cg.transform;
                sceneModified = true;
            }

            foreach (var b in Brands)
            {
                Transform brandT = cansGroup.Find(b.objectName);
                GameObject brandGo;
                if (brandT == null)
                {
                    brandGo = new GameObject(b.objectName);
                    brandGo.transform.SetParent(cansGroup, false);
                    brandGo.transform.localPosition = Vector3.zero;
                    brandGo.transform.localRotation = Quaternion.identity;
                    brandGo.transform.localScale = Vector3.one;
                    sceneModified = true;
                }
                else
                {
                    brandGo = brandT.gameObject;
                }

                MeshFilter mf = brandGo.GetComponent<MeshFilter>();
                if (mf == null) { mf = brandGo.AddComponent<MeshFilter>(); sceneModified = true; }

                if (mf.sharedMesh == null && meshes.ContainsKey(b.objectName))
                {
                    mf.sharedMesh = meshes[b.objectName];
                    sceneModified = true;
                }

                MeshRenderer mr = brandGo.GetComponent<MeshRenderer>();
                if (mr == null) { mr = brandGo.AddComponent<MeshRenderer>(); sceneModified = true; }

                if (materials.ContainsKey(b.objectName))
                {
                    mr.sharedMaterial = materials[b.objectName];
                }

                mr.enabled = true;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;

                // Ensure proper scale and position
                brandGo.transform.localPosition = Vector3.zero;
                brandGo.transform.localRotation = Quaternion.identity;
                brandGo.transform.localScale = Vector3.one;
            }

            EditorUtility.SetDirty(fridge.gameObject);
        }

        // 4. ATTACH StockFridgesRuntime TO PLAYER AND STORE
        GameObject player = GameObject.Find("Test_FPS_Player");
        if (player != null)
        {
            StockFridgesRuntime sfr = player.GetComponent<StockFridgesRuntime>();
            if (sfr == null)
            {
                player.AddComponent<StockFridgesRuntime>();
                sceneModified = true;
            }

            HorrorAmbience ha = player.GetComponent<HorrorAmbience>();
            if (ha != null)
            {
                ha.volume = 0.70f;
                ha.layerWithAudioClip = true;
                ha.refrigeratorHumLevel = 0.55f;
                ha.subBassDreadLevel = 0.45f;
                ha.hollowWindLevel = 0.35f;
                EditorUtility.SetDirty(ha);
                sceneModified = true;
            }

            EditorUtility.SetDirty(player);
        }

        GameObject store = GameObject.Find("Convenience_Store_V2");
        if (store != null)
        {
            StockFridgesRuntime sfr = store.GetComponent<StockFridgesRuntime>();
            if (sfr == null)
            {
                store.AddComponent<StockFridgesRuntime>();
                sceneModified = true;
            }
            EditorUtility.SetDirty(store);
        }

        // 5. SAVE SCENE AND ASSETS
        if (sceneModified)
        {
            if (!EditorApplication.isPlaying && !Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log("[FixBumpAndStockFridges] Fix completed successfully! Scene saved with full cans, removed bumps, and rich ambience.");
        }
        else
        {
            Debug.Log("[FixBumpAndStockFridges] All components and fridges already in verified state.");
        }
    }
}

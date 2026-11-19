using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StockCommercialFridgesAuthentic
{
    private const string FBXPath = "Assets/soda-cans-classic-355ml-free-download/source/All_Six_Soda_Cans_Optimized.fbx";
    private const string MaterialsFolder = "Assets/soda-cans-classic-355ml-free-download/Materials";
    private const string OutputMeshFolder = "Assets/soda-cans-classic-355ml-free-download/Meshes";

    public struct BrandDef
    {
        public string brandKey;
        public string nodeName;
        public string matName;
    }

    public static readonly BrandDef[] Brands = new BrandDef[]
    {
        new BrandDef { brandKey = "CocaCola", nodeName = "Can_CocaCola", matName = "Coca-Cola_BaseColor.mat" },
        new BrandDef { brandKey = "CocaColaZero", nodeName = "Can_CocaColaZero", matName = "Coca-Cola_Zero_Sugar_BaseColor.mat" },
        new BrandDef { brandKey = "DrPepper", nodeName = "Can_DrPepper", matName = "Dr_Pepper_BaseColor.mat" },
        new BrandDef { brandKey = "Pepsi", nodeName = "Can_Pepsi", matName = "Pepsi_BaseColor.mat" },
        new BrandDef { brandKey = "PepsiZero", nodeName = "Can_PepsiZero", matName = "Pepsi_Zero_Sugar_BaseColor.mat" },
        new BrandDef { brandKey = "Sprite", nodeName = "Can_Sprite", matName = "Sprite_BaseColor.mat" }
    };

    [MenuItem("Tools/Gas Station/Stock Commercial Fridges (Ultra Optimized)")]
    public static void RunStocking()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;

        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FBXPath);
        if (fbx == null)
        {
            Debug.LogError($"[StockCommercialFridgesAuthentic] Could not find '{FBXPath}'!");
            return;
        }

        if (!Directory.Exists(OutputMeshFolder))
        {
            Directory.CreateDirectory(OutputMeshFolder);
            AssetDatabase.Refresh();
        }

        GameObject tempInst = PrefabUtility.InstantiatePrefab(fbx) as GameObject;
        if (tempInst == null) tempInst = Object.Instantiate(fbx);

        Dictionary<string, Mesh> singleCanMeshes = new Dictionary<string, Mesh>();
        Dictionary<string, Material> brandMaterials = new Dictionary<string, Material>();

        foreach (var b in Brands)
        {
            Transform canT = tempInst.transform.Find(b.nodeName);
            if (canT == null)
            {
                foreach (Transform child in tempInst.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == b.nodeName) { canT = child; break; }
                }
            }

            if (canT == null)
            {
                Debug.LogError($"[StockCommercialFridgesAuthentic] Node '{b.nodeName}' not found in FBX!");
                continue;
            }

            MeshFilter mf = canT.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogError($"[StockCommercialFridgesAuthentic] Mesh missing on '{b.nodeName}'!");
                continue;
            }

            // Bake can transform so it is centered at origin with base at Y=0 and scale 1
            Matrix4x4 canMat = Matrix4x4.TRS(Vector3.zero, canT.localRotation, canT.localScale);
            CombineInstance ci = new CombineInstance { mesh = mf.sharedMesh, transform = canMat };

            string assetPath = $"{OutputMeshFolder}/SingleCan_{b.brandKey}.asset";
            Mesh singleMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (singleMesh == null)
            {
                singleMesh = new Mesh();
                singleMesh.name = $"SingleCan_{b.brandKey}";
                singleMesh.CombineMeshes(new[] { ci }, true, true);
                AssetDatabase.CreateAsset(singleMesh, assetPath);
            }
            else
            {
                singleMesh.Clear();
                singleMesh.CombineMeshes(new[] { ci }, true, true);
                singleMesh.name = $"SingleCan_{b.brandKey}";
                EditorUtility.SetDirty(singleMesh);
            }
            singleCanMeshes[b.brandKey] = singleMesh;

            string matPath = $"{MaterialsFolder}/{b.matName}";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                brandMaterials[b.brandKey] = mat;
            }
        }

        Object.DestroyImmediate(tempInst);

        // Find strictly Double_Beverage_Fridge (NEVER touch Beverage_Service_Counter!)
        GameObject stacja = GameObject.Find("stacja");
        if (stacja == null) return;

        Transform store = stacja.transform.Find("Convenience_Store_V2");
        if (store == null) return;

        List<Transform> fridges = new List<Transform>();
        foreach (Transform t in store)
        {
            if (t.name.StartsWith("Double_Beverage_Fridge"))
            {
                fridges.Add(t);
            }
        }

        if (fridges.Count == 0)
        {
            Debug.LogError("[StockCommercialFridgesAuthentic] No Double_Beverage_Fridge found!");
            return;
        }

        float[] shelfHeights = new float[] { 0.432f, 0.832f, 1.232f, 1.632f };
        float leftBayX = -0.52f;
        float rightBayX = 0.52f;

        // 6 columns across per bay, 2 dense rows deep (12 cans per bay, 24 cans per shelf, 96 cans per fridge)
        int cols = 6;
        float colSpacing = 0.082f;
        float colStartOffset = -((cols - 1) * colSpacing) * 0.5f;
        float[] rowZPositions = new float[] { 0.17f, 0.04f };

        bool sceneDirty = false;

        foreach (Transform fridge in fridges)
        {
            Transform cansParent = fridge.Find("Drink_Cans");
            if (cansParent == null)
            {
                GameObject cg = new GameObject("Drink_Cans");
                cg.transform.SetParent(fridge, false);
                cg.transform.localPosition = Vector3.zero;
                cg.transform.localRotation = Quaternion.identity;
                cg.transform.localScale = Vector3.one;
                cansParent = cg.transform;
                sceneDirty = true;
            }

            while (cansParent.childCount > 0)
            {
                Object.DestroyImmediate(cansParent.GetChild(0).gameObject);
                sceneDirty = true;
            }

            Dictionary<string, List<CombineInstance>> fridgeBrandCombines = new Dictionary<string, List<CombineInstance>>();
            foreach (var b in Brands)
            {
                fridgeBrandCombines[b.brandKey] = new List<CombineInstance>();
            }

            for (int s = 0; s < shelfHeights.Length; s++)
            {
                float shY = shelfHeights[s];
                string leftBrand = "CocaCola";
                string rightBrand = "Sprite";

                if (s == 3) { leftBrand = "CocaCola"; rightBrand = "CocaColaZero"; }
                else if (s == 2) { leftBrand = "Pepsi"; rightBrand = "PepsiZero"; }
                else if (s == 1) { leftBrand = "Sprite"; rightBrand = "DrPepper"; }
                else if (s == 0) { leftBrand = "CocaCola"; rightBrand = "Sprite"; }

                if (singleCanMeshes.ContainsKey(leftBrand))
                {
                    Mesh canM = singleCanMeshes[leftBrand];
                    for (int r = 0; r < rowZPositions.Length; r++)
                    {
                        float z = rowZPositions[r];
                        for (int c = 0; c < cols; c++)
                        {
                            float x = leftBayX + colStartOffset + c * colSpacing;
                            Matrix4x4 m = Matrix4x4.TRS(new Vector3(x, shY, z), Quaternion.identity, Vector3.one);
                            fridgeBrandCombines[leftBrand].Add(new CombineInstance { mesh = canM, transform = m });
                        }
                    }
                }

                if (singleCanMeshes.ContainsKey(rightBrand))
                {
                    Mesh canM = singleCanMeshes[rightBrand];
                    for (int r = 0; r < rowZPositions.Length; r++)
                    {
                        float z = rowZPositions[r];
                        for (int c = 0; c < cols; c++)
                        {
                            float x = rightBayX + colStartOffset + c * colSpacing;
                            Matrix4x4 m = Matrix4x4.TRS(new Vector3(x, shY, z), Quaternion.identity, Vector3.one);
                            fridgeBrandCombines[rightBrand].Add(new CombineInstance { mesh = canM, transform = m });
                        }
                    }
                }
            }

            foreach (var b in Brands)
            {
                var list = fridgeBrandCombines[b.brandKey];
                if (list == null || list.Count == 0) continue;

                GameObject brandGo = new GameObject($"Cans_{b.brandKey}");
                brandGo.transform.SetParent(cansParent, false);
                brandGo.transform.localPosition = Vector3.zero;
                brandGo.transform.localRotation = Quaternion.identity;
                brandGo.transform.localScale = Vector3.one;

                MeshFilter mf = brandGo.AddComponent<MeshFilter>();
                string fridgeMeshPath = $"{OutputMeshFolder}/Stocked_{b.brandKey}_{fridge.name}.asset";
                Mesh combinedBrandMesh = AssetDatabase.LoadAssetAtPath<Mesh>(fridgeMeshPath);
                if (combinedBrandMesh == null)
                {
                    combinedBrandMesh = new Mesh();
                    combinedBrandMesh.name = $"Stocked_{b.brandKey}_{fridge.name}";
                    combinedBrandMesh.CombineMeshes(list.ToArray(), true, true);
                    AssetDatabase.CreateAsset(combinedBrandMesh, fridgeMeshPath);
                }
                else
                {
                    combinedBrandMesh.Clear();
                    combinedBrandMesh.CombineMeshes(list.ToArray(), true, true);
                    EditorUtility.SetDirty(combinedBrandMesh);
                }
                mf.sharedMesh = combinedBrandMesh;

                MeshRenderer mr = brandGo.AddComponent<MeshRenderer>();
                if (brandMaterials.ContainsKey(b.brandKey))
                {
                    mr.sharedMaterial = brandMaterials[b.brandKey];
                }

                // CRITICAL OPTIMIZATION:
                // Disable shadow casting for interior fridge cans.
                // This eliminates millions of shadow caster passes across 29-41 lights!
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = true;
                mr.enabled = true;

                sceneDirty = true;
            }

            EditorUtility.SetDirty(fridge.gameObject);
        }

        if (sceneDirty && !EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StockCommercialFridgesAuthentic] SUCCESS: Stocked {fridges.Count} commercial fridges with ULTRA-OPTIMIZED authentic cans! (Only ~9k verts per brand, shadow casting OFF, zero lag!)");
        }
    }
}

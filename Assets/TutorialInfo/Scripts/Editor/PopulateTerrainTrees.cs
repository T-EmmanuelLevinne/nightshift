using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Disabled [InitializeOnLoad] so user-placed / moved trees in Forest_Trees are NEVER overwritten automatically.
// [InitializeOnLoad]
public static class PopulateTerrainTrees
{
    private const string SetupKey = "PopulateTerrainTrees_Massive_MultiScale_V3_Done";

    // 1. Massive Colossal Tree Prefabs (Giant landmarks & multi-tree groves)
    private static readonly string[] MassiveGiantPrefabs = new string[]
    {
        "Assets/Realistic Tree/Prefabs/URP/Ash/The Great Ash Tree.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce Group 1.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce Group 2.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce Group 3.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch Group 1.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch Group 2.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch Group 3.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 1.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 7.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 10.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Chestnut/Chestnut 1.prefab"
    };

    // 2. Evergreen Pines (Spruces)
    private static readonly string[] SprucePrefabs = new string[]
    {
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 1.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 2.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 3.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 4.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 5.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 6.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 7.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 8.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 9.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce 10.prefab"
    };

    // 3. Birch Trees
    private static readonly string[] BirchPrefabs = new string[]
    {
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch 1.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch 2.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch 3.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch 4.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch 5.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch 6.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch 7.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch 8.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch 9.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Birch/Birch 10.prefab"
    };

    // 4. Hardwood & Broadleaf Trees
    private static readonly string[] HardwoodPrefabs = new string[]
    {
        "Assets/Realistic Tree/Prefabs/URP/Ash/Ash 1.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Ash/Ash 2.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Ash/Ash 3.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Ash/Ash 4.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Chestnut/Chestnut 1.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Chestnut/Chestnut 2.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Chestnut/Chestnut 3.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Weeping Willow/Weeping_Willow 1.prefab",
        "Assets/Realistic Tree/Prefabs/URP/Weeping Willow/Weeping_Willow 2.prefab"
    };

    // Disabled auto-populate so manually moved trees are preserved across Unity sessions
    // static PopulateTerrainTrees()
    // {
    //     EditorApplication.delayCall += AutoPopulateOnce;
    // }

    private static void AutoPopulateOnce()
    {
        if (!SessionState.GetBool(SetupKey, false))
        {
            SessionState.SetBool(SetupKey, true);
            Execute();
        }
    }

    [MenuItem("Tools/Gas Station/Populate Realistic Trees on All Terrains")]
    public static void Execute()
    {
        // CRITICAL SCENE OBJECT PROTECTION:
        // NEVER touch, move, modify, or destroy Beverage_Service_Counter or any props on it!

        // 1. Load tree prefabs
        List<GameObject> giants = LoadValidPrefabs(MassiveGiantPrefabs);
        List<GameObject> spruces = LoadValidPrefabs(SprucePrefabs);
        List<GameObject> birches = LoadValidPrefabs(BirchPrefabs);
        List<GameObject> hardwoods = LoadValidPrefabs(HardwoodPrefabs);

        if (giants.Count == 0 && spruces.Count == 0 && birches.Count == 0)
        {
            Debug.LogError("[PopulateTerrainTrees] No tree prefabs found in Realistic Tree folder!");
            return;
        }

        // 2. Find all active Terrains in the scene
        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0)
        {
            terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
        }

        if (terrains == null || terrains.Length == 0)
        {
            Debug.LogWarning("[PopulateTerrainTrees] No Terrains found in the active scene.");
            return;
        }

        // Clean any broken TerrainData tree prototypes
        foreach (Terrain t in terrains)
        {
            if (t != null && t.terrainData != null)
            {
                t.terrainData.SetTreeInstances(new TreeInstance[0], false);
                t.terrainData.treePrototypes = new TreePrototype[0];
                EditorUtility.SetDirty(t.terrainData);
            }
        }

        // 3. Prepare clean categorized hierarchy container in the scene
        const string RootName = "Forest_Trees";
        GameObject rootContainer = GameObject.Find(RootName);
        if (rootContainer != null)
        {
            Undo.DestroyObjectImmediate(rootContainer);
        }

        rootContainer = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootContainer, "Create Forest Trees");

        // 4 Size Tier Containers
        GameObject giantParent = new GameObject("1_Massive_Ancient_Giants");
        giantParent.transform.SetParent(rootContainer.transform);

        GameObject largeParent = new GameObject("2_Large_Canopy_Trees");
        largeParent.transform.SetParent(rootContainer.transform);

        GameObject standardParent = new GameObject("3_Standard_Trees");
        standardParent.transform.SetParent(rootContainer.transform);

        GameObject saplingParent = new GameObject("4_Understory_Saplings");
        saplingParent.transform.SetParent(rootContainer.transform);

        // 4. Distribute realistic trees across all terrain tiles with 4 distinct size tiers
        int totalSpawned = 0;
        int seed = 108;

        foreach (Terrain terrain in terrains)
        {
            if (terrain == null || terrain.terrainData == null) continue;

            TerrainData tData = terrain.terrainData;
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = tData.size;

            Random.InitState(seed++);

            // Density: ~35-50 trees per terrain tile
            int targetTileTrees = 42;
            int placedForThisTile = 0;

            for (int attempt = 0; attempt < targetTileTrees * 4 && placedForThisTile < targetTileTrees; attempt++)
            {
                float normX = Random.Range(0.04f, 0.96f);
                float normZ = Random.Range(0.04f, 0.96f);

                float worldX = tPos.x + normX * tSize.x;
                float worldZ = tPos.z + normZ * tSize.z;

                // --- EXCLUSION CHECK ---
                if (IsInExclusionZone(worldX, worldZ))
                {
                    continue;
                }

                // Slope check: avoid sheer vertical rock faces
                float steepness = tData.GetSteepness(normX, normZ);
                if (steepness > 48f)
                {
                    continue;
                }

                // Sample exact ground height
                float worldY = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + tPos.y;

                // Determine Size Tier and Model
                GameObject prefabToSpawn;
                Transform parentGroup;
                float baseScale;

                float roll = Random.value;

                if (roll < 0.16f && giants.Count > 0)
                {
                    // TIER 1: Massive Ancient Giants (2.5x to 3.8x scale)
                    prefabToSpawn = giants[Random.Range(0, giants.Count)];
                    parentGroup = giantParent.transform;
                    baseScale = Random.Range(2.50f, 3.80f);
                }
                else if (roll < 0.45f)
                {
                    // TIER 2: Large Canopy Trees (1.6x to 2.3x scale)
                    float subRoll = Random.value;
                    if (subRoll < 0.65f && spruces.Count > 0)
                        prefabToSpawn = spruces[Random.Range(0, spruces.Count)];
                    else if (subRoll < 0.85f && birches.Count > 0)
                        prefabToSpawn = birches[Random.Range(0, birches.Count)];
                    else if (hardwoods.Count > 0)
                        prefabToSpawn = hardwoods[Random.Range(0, hardwoods.Count)];
                    else
                        prefabToSpawn = spruces[0];

                    parentGroup = largeParent.transform;
                    baseScale = Random.Range(1.60f, 2.30f);
                }
                else if (roll < 0.80f)
                {
                    // TIER 3: Standard Forest Trees (0.95x to 1.45x scale)
                    float subRoll = Random.value;
                    if (subRoll < 0.60f && spruces.Count > 0)
                        prefabToSpawn = spruces[Random.Range(0, spruces.Count)];
                    else if (subRoll < 0.85f && birches.Count > 0)
                        prefabToSpawn = birches[Random.Range(0, birches.Count)];
                    else if (hardwoods.Count > 0)
                        prefabToSpawn = hardwoods[Random.Range(0, hardwoods.Count)];
                    else
                        prefabToSpawn = spruces[0];

                    parentGroup = standardParent.transform;
                    baseScale = Random.Range(0.95f, 1.45f);
                }
                else
                {
                    // TIER 4: Understory & Young Saplings (0.45x to 0.75x scale)
                    float subRoll = Random.value;
                    if (subRoll < 0.60f && spruces.Count > 0)
                        prefabToSpawn = spruces[Random.Range(0, spruces.Count)];
                    else if (birches.Count > 0)
                        prefabToSpawn = birches[Random.Range(0, birches.Count)];
                    else
                        prefabToSpawn = spruces[0];

                    parentGroup = saplingParent.transform;
                    baseScale = Random.Range(0.45f, 0.75f);
                }

                // Instantiate prefab
                GameObject treeInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabToSpawn, parentGroup);
                if (treeInstance != null)
                {
                    treeInstance.transform.position = new Vector3(worldX, worldY, worldZ);
                    treeInstance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    treeInstance.transform.localScale = Vector3.one * baseScale;

                    placedForThisTile++;
                    totalSpawned++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[PopulateTerrainTrees] Successfully populated {totalSpawned} multi-scale realistic 3D trees across {terrains.Length} terrain tiles! (Massive Giants, Large Canopy, Standard, and Saplings).");
    }

    private static List<GameObject> LoadValidPrefabs(string[] paths)
    {
        List<GameObject> list = new List<GameObject>();
        for (int i = 0; i < paths.Length; i++)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (go != null) list.Add(go);
        }
        return list;
    }

    private static bool IsInExclusionZone(float wx, float wz)
    {
        // 1. Gas station store building, pump islands, customer lot & walkway:
        // X: -24 to +24, Z: -24 to +16
        if (wx >= -24f && wx <= 24f && wz >= -24f && wz <= 16f)
        {
            return true;
        }

        // 2. Roadway running across the front:
        // Road strip spanning across the valley
        if (wx >= -70f && wx <= 70f && wz >= -25f && wz <= -6f)
        {
            return true;
        }

        return false;
    }
}

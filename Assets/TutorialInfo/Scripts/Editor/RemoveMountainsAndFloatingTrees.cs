using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RemoveMountainsAndFloatingTrees
{
    [MenuItem("Tools/Gas Station/Remove Mountains and Floating Trees")]
    public static void Execute()
    {
        // CRITICAL SCENE OBJECT PROTECTION:
        // NEVER touch, move, rebuild, destroy, or modify Beverage_Service_Counter!
        // Treat Beverage_Service_Counter and all props on it as strictly immutable.

        Debug.Log("[RemoveMountains] Starting removal of mountains and floating trees...");

       
        string[] mountainTerrainNames = new string[] { "Terrain_1", "Terrain_2", "Terrain_3" };
        int removedTerrains = 0;

        foreach (string tName in mountainTerrainNames)
        {
            GameObject tObj = GameObject.Find(tName);
            if (tObj != null)
            {
                Undo.DestroyObjectImmediate(tObj);
                removedTerrains++;
                Debug.Log($"[RemoveMountains] Removed mountain terrain: {tName}");
            }
        }

        // 2. Flatten TerrainData_1, TerrainData_2, TerrainData_3 assets so they have zero height
        string[] tDataPaths = new string[]
        {
            "Assets/TerrainData/TerrainData_1.asset",
            "Assets/TerrainData/TerrainData_2.asset",
            "Assets/TerrainData/TerrainData_3.asset"
        };
        foreach (string path in tDataPaths)
        {
            TerrainData td = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (td != null)
            {
                int res = td.heightmapResolution;
                float[,] zeroHeights = new float[res, res];
                td.SetHeights(0, 0, zeroHeights);
                EditorUtility.SetDirty(td);
            }
        }
        AssetDatabase.SaveAssets();

        // 3. Find and remove all floating/mountain trees under Forest_Trees
        GameObject forest = GameObject.Find("Forest_Trees");
        int removedTrees = 0;
        int remainingTrees = 0;

        if (forest != null)
        {
            // Gather all direct tree instances (children of the tier groups)
            List<GameObject> treesToDelete = new List<GameObject>();
            List<Transform> treesToKeep = new List<Transform>();

            for (int i = 0; i < forest.transform.childCount; i++)
            {
                Transform tierGroup = forest.transform.GetChild(i);
                for (int j = 0; j < tierGroup.childCount; j++)
                {
                    Transform treeTr = tierGroup.GetChild(j);
                    Vector3 pos = treeTr.position;

                    // Sample the flat terrain height at this position
                    float groundY = -0.05f;
                    Terrain hitTerrain = null;
                    foreach (Terrain ter in Terrain.activeTerrains)
                    {
                        if (ter == null || ter.terrainData == null) continue;
                        Vector3 tp = ter.transform.position;
                        Vector3 ts = ter.terrainData.size;
                        if (pos.x >= tp.x && pos.x <= tp.x + ts.x && pos.z >= tp.z && pos.z <= tp.z + ts.z)
                        {
                            groundY = ter.SampleHeight(pos) + tp.y;
                            hitTerrain = ter;
                            break;
                        }
                    }

                    float heightDiff = pos.y - groundY;

                    // Condition to delete:
                    // 1. Perched high on mountain (Y > 0.5f)
                    // 2. Floating in the air (heightDiff > 0.35f)
                    // 3. Inside the western/northern mountain zone (X between -90 and 15, Z between -30 and 30) where Y was elevated
                    bool isFloatingOrMountain = (pos.y > 0.5f) || (heightDiff > 0.35f) || (pos.x <= 15f && pos.z >= -30f && pos.y > 0.2f);

                    if (isFloatingOrMountain)
                    {
                        treesToDelete.Add(treeTr.gameObject);
                    }
                    else
                    {
                        treesToKeep.Add(treeTr);
                    }
                }
            }

            foreach (GameObject deadTree in treesToDelete)
            {
                Undo.DestroyObjectImmediate(deadTree);
                removedTrees++;
            }

            // Snap all remaining trees cleanly to the flat ground level (Y = -0.05)
            foreach (Transform keptTree in treesToKeep)
            {
                if (keptTree == null) continue;
                Vector3 p = keptTree.position;
                keptTree.position = new Vector3(p.x, -0.05f, p.z);
                remainingTrees++;
            }
        }

        // 4. Save the modified scene
        Scene activeScene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[RemoveMountains] COMPLETE! Removed {removedTerrains} mountain terrains (Terrain_1, Terrain_2, Terrain_3). Removed {removedTrees} floating/mountain trees. {remainingTrees} grounded trees remain on flat grass terrain.");
    }
}

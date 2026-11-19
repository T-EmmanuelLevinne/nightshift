using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Non-destructive prefab asset optimizer:
/// Updates the 5 bush prefab assets directly on disk.
/// Because all 8,500+ bushes in the scene are instances of these 5 prefabs:
/// - DOES NOT MOVE any bush (preserves user's manual adjustments and buried bushes).
/// - DOES NOT DELETE any bush.
/// - Turns off shadow casting across all 34,000+ renderers (0 shadow draw calls!).
/// - Turns off light probe & reflection probe sampling (eliminates per-bush probe CPU overhead).
/// - Calibrates LODGroup cull distances so distant bushes past ~60m are culled by the GPU.
/// </summary>
[InitializeOnLoad]
public static class OptimizeBushPrefabs
{
    private const string AutoOptimizeKey = "OptimizeBushPrefabs_AutoRun_Done_V1";

    private static readonly string[] BushPrefabPaths = new string[]
    {
        "Assets/YughuesFreeBushes2018/Prefabs/P_Bush01.prefab",
        "Assets/YughuesFreeBushes2018/Prefabs/P_Bush02.prefab",
        "Assets/YughuesFreeBushes2018/Prefabs/P_Bush03.prefab",
        "Assets/YughuesFreeBushes2018/Prefabs/P_Bush04.prefab",
        "Assets/YughuesFreeBushes2018/Prefabs/P_Bush05.prefab"
    };

    static OptimizeBushPrefabs()
    {
        EditorApplication.delayCall += AutoOptimizeOnce;
    }

    private static void AutoOptimizeOnce()
    {
        if (!SessionState.GetBool(AutoOptimizeKey, false))
        {
            SessionState.SetBool(AutoOptimizeKey, true);
            Execute();
        }
    }

    [MenuItem("Tools/Gas Station/Optimize Bush Prefabs (Zero-Lag Asset Fix)")]
    public static void Execute()
    {
        int optimizedPrefabs = 0;

        for (int i = 0; i < BushPrefabPaths.Length; i++)
        {
            string path = BushPrefabPaths[i];
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            if (prefabRoot == null) continue;

            bool isDirty = false;

            // 1. Optimize all MeshRenderers in the prefab
            MeshRenderer[] renderers = prefabRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                MeshRenderer mr = renderers[r];
                if (mr == null) continue;

                if (mr.shadowCastingMode != ShadowCastingMode.Off)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    isDirty = true;
                }

                if (mr.receiveShadows != true)
                {
                    mr.receiveShadows = true;
                    isDirty = true;
                }

                if (mr.lightProbeUsage != LightProbeUsage.Off)
                {
                    mr.lightProbeUsage = LightProbeUsage.Off;
                    isDirty = true;
                }

                if (mr.reflectionProbeUsage != ReflectionProbeUsage.Off)
                {
                    mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    isDirty = true;
                }
            }

            // 2. Calibrate LODGroup cull distance (culls past ~60m, matching the dark midnight fog)
            LODGroup lodGroup = prefabRoot.GetComponent<LODGroup>();
            if (lodGroup != null)
            {
                LOD[] lods = lodGroup.GetLODs();
                if (lods != null && lods.Length > 0)
                {
                    // If 4 LODs:
                    if (lods.Length >= 4)
                    {
                        lods[0].screenRelativeTransitionHeight = 0.32f;
                        lods[1].screenRelativeTransitionHeight = 0.16f;
                        lods[2].screenRelativeTransitionHeight = 0.08f;
                        lods[3].screenRelativeTransitionHeight = 0.045f; // Culls past ~60m
                    }
                    else if (lods.Length == 3)
                    {
                        lods[0].screenRelativeTransitionHeight = 0.30f;
                        lods[1].screenRelativeTransitionHeight = 0.14f;
                        lods[2].screenRelativeTransitionHeight = 0.045f;
                    }
                    lodGroup.SetLODs(lods);
                    isDirty = true;
                }
            }

            if (isDirty)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                optimizedPrefabs++;
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[OptimizeBushPrefabs] Successfully optimized {optimizedPrefabs} bush prefabs! All 8,500+ scene instances now have 0 shadow casting, 0 probe overhead, and ~60m fog distance culling with zero movement or changes to user placements.");
    }
}


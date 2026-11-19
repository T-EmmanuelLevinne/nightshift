#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SetupAct3HouseScene
{
    private const string MonsterFbxPath = "Assets/psx-horror-creature/source/psx_horror_creature.fbx";
    private const string MonsterResourcePrefabPath = "Assets/Resources/Act3_Monster_LowQual.prefab";

    static SetupAct3HouseScene()
    {
        EditorApplication.delayCall += EnsureMonsterResourcePrefabAndSceneSetup;
    }

    [MenuItem("Tools/House Scene/Setup Act 3 Monster & Resources")]
    public static void EnsureMonsterResourcePrefabAndSceneSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // 1. Ensure Assets/Resources/Act3_Monster_LowQual.prefab exists so runtime fallback always works
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterFbxPath);
        if (fbxAsset != null && AssetDatabase.LoadAssetAtPath<GameObject>(MonsterResourcePrefabPath) == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            GameObject tempInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            if (tempInstance != null)
            {
                tempInstance.name = "monster_lowqual";
                NormalizeMonsterHeight(tempInstance, 1.74f);
                PrefabUtility.SaveAsPrefabAsset(tempInstance, MonsterResourcePrefabPath);
                Object.DestroyImmediate(tempInstance);
                AssetDatabase.SaveAssets();
                Debug.Log("[SetupAct3HouseScene] Created Assets/Resources/Act3_Monster_LowQual.prefab normalized to 1.74m height.");
            }
        }

        // 2. Refresh CabinWindowGlass_MAT.mat so window glass keeps crisp diagonal shine with zero white point-light glare spots
        Material glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FurnishedCabin/Meshes/Materials/CabinWindowGlass_MAT.mat");
        if (glassMat != null)
        {
            Texture2D sheenTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FurnishedCabin/Meshes/Materials/CabinWindowGlassSheen_TEX.png");
            CabinWindowUtility.ApplyGlassMaterialProperties(glassMat, sheenTex);
            EditorUtility.SetDirty(glassMat);
        }

        // 3. If monster_low / monster_lowqual is present in the open scene, normalize its height to ~1.74m
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            string n = t.name;
            if (n.IndexOf("monster_low", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("psx_horror_creature", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Transform rootMatch = t;
                while (rootMatch.parent != null &&
                       (rootMatch.parent.name.IndexOf("monster_low", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        rootMatch.parent.name.IndexOf("psx_horror_creature", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    rootMatch = rootMatch.parent;
                }
                NormalizeMonsterHeight(rootMatch.gameObject, 1.74f);
                break;
            }
        }
    }

    [MenuItem("Tools/House Scene/Start Next Play Mode in Act 3 (HouseScene)")]
    public static void EnableStartInAct3()
    {
        PlayerPrefs.SetInt("HouseScene_ForceAct3", 1);
        PlayerPrefs.Save();
        Debug.Log("[SetupAct3HouseScene] Next Play Mode in HouseScene will start directly in Act 3!");
    }

    private static void NormalizeMonsterHeight(GameObject monsterGo, float targetHeight)
    {
        if (monsterGo == null) return;
        Renderer[] renderers = monsterGo.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        bool prevEnabled = renderers[0].enabled;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = true;
        }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null) b.Encapsulate(renderers[i].bounds);
        }

        if (b.size.y > 0.05f)
        {
            float ratio = targetHeight / b.size.y;
            if (Mathf.Abs(ratio - 1f) > 0.03f)
            {
                Undo.RecordObject(monsterGo.transform, "Normalize monster_lowqual Height");
                monsterGo.transform.localScale *= ratio;
                EditorUtility.SetDirty(monsterGo.transform);
            }
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = prevEnabled;
        }
    }
}
#endif

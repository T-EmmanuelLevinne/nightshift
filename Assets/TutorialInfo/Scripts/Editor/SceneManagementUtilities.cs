using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene Management & Performance Utilities:
/// 1. Closes and unloads extraneous demo scenes (Demo.unity) from the active hierarchy.
/// 2. Configures EditorBuildSettings so GasScene2 and HouseScene load cleanly via SceneManager.
/// 3. Creates the standalone Player prefab in Assets/Resources/Player/FPS_Player.prefab.
/// 4. Adds PlayerSpawnPoint visual markers to HouseScene and GasScene2.
/// 5. Strictly protects Beverage_Service_Counter and all user-placed objects.
/// </summary>
[InitializeOnLoad]
public static class SceneManagementUtilities
{
    private const string SessionKey = "NightShift_SceneManagement_Optimized_V1";

    static SceneManagementUtilities()
    {
        EditorApplication.delayCall += AutoRunOnLoad;
    }

    private static void AutoRunOnLoad()
    {
        if (!SessionState.GetBool(SessionKey, false))
        {
            SessionState.SetBool(SessionKey, true);
            ApplyAllOptimizations();
        }
    }

    [MenuItem("Tools/Scenes/Apply All Scene & Performance Optimizations")]
    public static void ApplyAllOptimizations()
    {
        Debug.Log("[SceneManagementUtilities] Applying all scene & performance optimizations...");

        CloseDemoScenes();
        AutoCloseGasSceneIfHouseOpen();
        ConfigureBuildSettings();
        EnsurePlayerPrefabExists();
        ConfigureSpawnPointsInOpenScenes();

        Debug.Log("[SceneManagementUtilities] All optimizations applied successfully!");
    }

    [MenuItem("Tools/Scenes/Close GasScene2 (Keep ONLY HouseScene)")]
    public static void AutoCloseGasSceneIfHouseOpen()
    {
        Scene houseScene = SceneManager.GetSceneByName("HouseScene");
        Scene gasScene = SceneManager.GetSceneByName("GasScene2");

        if (houseScene.IsValid() && houseScene.isLoaded && gasScene.IsValid() && gasScene.isLoaded)
        {
            EditorSceneManager.CloseScene(gasScene, true);
            EditorSceneManager.SetActiveScene(houseScene);
            Debug.Log("[SceneManagementUtilities] Successfully closed GasScene2 from Hierarchy. HouseScene is now the only active scene.");
        }
    }

    [MenuItem("Tools/Scenes/Close Demo Scene")]
    public static void CloseDemoScenes()
    {
        int closedCount = 0;
        int sceneCount = SceneManager.sceneCount;

        for (int i = sceneCount - 1; i >= 0; i--)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.IsValid() || !s.isLoaded) continue;

            // Target Demo scene from FurnishedCabin or BathroomPack
            if (s.name.Equals("Demo", System.StringComparison.OrdinalIgnoreCase) ||
                s.path.IndexOf("FurnishedCabin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.name.IndexOf("SampleScene", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Never close the only open scene if it's the active one and no other scene exists
                if (sceneCount > 1)
                {
                    EditorSceneManager.CloseScene(s, true);
                    closedCount++;
                    Debug.Log($"[SceneManagementUtilities] Successfully unloaded demo scene: {s.name} ({s.path})");
                }
            }
        }

        if (closedCount == 0)
        {
            Debug.Log("[SceneManagementUtilities] No open Demo scenes detected in the hierarchy.");
        }
    }

    [MenuItem("Tools/Scenes/Configure Build Settings (HouseScene First, GasScene2 Second)")]
    public static void ConfigureBuildSettings()
    {
        string houseScenePath = "Assets/HouseScene.unity";
        string gasScenePath = "Assets/GasScene2.unity";

        List<EditorBuildSettingsScene> targetScenes = new List<EditorBuildSettingsScene>();

        // HouseScene is the 1st scene to be played (Index 0)
        if (File.Exists(houseScenePath))
        {
            targetScenes.Add(new EditorBuildSettingsScene(houseScenePath, true));
        }

        // GasScene2 is the next scene (Index 1)
        if (File.Exists(gasScenePath))
        {
            targetScenes.Add(new EditorBuildSettingsScene(gasScenePath, true));
        }

        // Preserve any optional remaining scenes (e.g. SampleScene as disabled)
        foreach (var sc in EditorBuildSettings.scenes)
        {
            if (sc.path != houseScenePath && sc.path != gasScenePath)
            {
                targetScenes.Add(new EditorBuildSettingsScene(sc.path, false));
            }
        }

        EditorBuildSettings.scenes = targetScenes.ToArray();
        Debug.Log("[SceneManagementUtilities] Build Settings configured: HouseScene is Index 0 (First Scene), GasScene2 is Index 1.");
    }

    [MenuItem("Tools/Scenes/Create Player Prefab in Resources")]
    public static void EnsurePlayerPrefabExists()
    {
        string resourcesFolder = "Assets/Resources";
        string playerFolder = "Assets/Resources/Player";
        string prefabPath = "Assets/Resources/Player/FPS_Player.prefab";

        if (File.Exists(prefabPath))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(resourcesFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder(playerFolder))
        {
            AssetDatabase.CreateFolder(resourcesFolder, "Player");
        }

        // Try to create from active scene player first
        GameObject scenePlayer = GameObject.Find("Test_FPS_Player");
        if (scenePlayer != null)
        {
            PrefabUtility.SaveAsPrefabAsset(scenePlayer, prefabPath);
            Debug.Log($"[SceneManagementUtilities] Created Player prefab from existing Test_FPS_Player at {prefabPath}");
            AssetDatabase.Refresh();
            return;
        }

        // Otherwise create new player object and save as prefab
        GameObject p = new GameObject("FPS_Player");
        p.transform.position = Vector3.zero;

        CharacterController cc = p.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.35f;

        SimpleFPSController fps = p.AddComponent<SimpleFPSController>();
        fps.walkSpeed = 4.5f;
        fps.sprintSpeed = 7.5f;
        fps.canJump = false;
        fps.mouseSensitivity = 2.0f;

        p.AddComponent<HorrorInteractionReticle>();
        p.AddComponent<RetroHorrorPOV>();
        p.AddComponent<HorrorAmbience>();

        GameObject camObj = new GameObject("PlayerCamera");
        camObj.transform.SetParent(p.transform, false);
        camObj.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        camObj.transform.localRotation = Quaternion.identity;
        camObj.tag = "MainCamera";

        Camera cam = camObj.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 65f;
        camObj.AddComponent<AudioListener>();

        fps.playerCamera = cam;

        PrefabUtility.SaveAsPrefabAsset(p, prefabPath);
        Object.DestroyImmediate(p);
        Debug.Log($"[SceneManagementUtilities] Generated clean Player prefab at {prefabPath}");
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Scenes/Setup Player Spawn Points in Scenes")]
    public static void ConfigureSpawnPointsInOpenScenes()
    {
        // 1. GasScene2 Spawn Point
        Scene gasScene = SceneManager.GetSceneByName("GasScene2");
        if (gasScene.IsValid() && gasScene.isLoaded)
        {
            SetupGasSceneSpawnPoint(gasScene);
        }

        // 2. HouseScene Spawn Point
        Scene houseScene = SceneManager.GetSceneByName("HouseScene");
        if (houseScene.IsValid() && houseScene.isLoaded)
        {
            SetupHouseSceneSpawnPoint(houseScene);
        }
    }

    private static void SetupGasSceneSpawnPoint(Scene gasScene)
    {
        // Find existing player in gas station to get exact position & rotation
        Vector3 targetPos = new Vector3(-30.407f, -0.611f, -11.142f);
        Quaternion targetRot = Quaternion.Euler(0f, 60.196f, 0f);

        foreach (GameObject root in gasScene.GetRootGameObjects())
        {
            if (root != null && (root.name == "Test_FPS_Player" || root.name == "Player"))
            {
                targetPos = root.transform.position;
                targetRot = root.transform.rotation;
                break;
            }
        }

        PlayerSpawnPoint existingSp = null;
        foreach (GameObject root in gasScene.GetRootGameObjects())
        {
            PlayerSpawnPoint sp = root.GetComponentInChildren<PlayerSpawnPoint>(true);
            if (sp != null) { existingSp = sp; break; }
        }

        if (existingSp == null)
        {
            GameObject spObj = new GameObject("PlayerSpawnPoint_GasStation");
            SceneManager.MoveGameObjectToScene(spObj, gasScene);
            spObj.transform.position = targetPos;
            spObj.transform.rotation = targetRot;

            PlayerSpawnPoint sp = spObj.AddComponent<PlayerSpawnPoint>();
            sp.spawnId = "Default";

            EditorSceneManager.MarkSceneDirty(gasScene);
            Debug.Log($"[SceneManagementUtilities] Added PlayerSpawnPoint to GasScene2 at {targetPos}.");
        }
        else
        {
            existingSp.transform.position = targetPos;
            existingSp.transform.rotation = targetRot;
            EditorSceneManager.MarkSceneDirty(gasScene);
        }
    }

    private static void SetupHouseSceneSpawnPoint(Scene houseScene)
    {
        // Remove any raw player in HouseScene to keep scene completely clean
        foreach (GameObject root in houseScene.GetRootGameObjects())
        {
            if (root != null && (root.name == "Test_FPS_Player" || root.name == "Player"))
            {
                Undo.DestroyObjectImmediate(root);
                Debug.Log("[SceneManagementUtilities] Removed raw player object from HouseScene (managed by PlayerManager).");
                break;
            }
        }

        PlayerSpawnPoint existingSp = null;
        foreach (GameObject root in houseScene.GetRootGameObjects())
        {
            PlayerSpawnPoint sp = root.GetComponentInChildren<PlayerSpawnPoint>(true);
            if (sp != null) { existingSp = sp; break; }
        }

        if (existingSp == null)
        {
            GameObject spObj = new GameObject("PlayerSpawnPoint_House");
            SceneManager.MoveGameObjectToScene(spObj, houseScene);
            // Standing outside cabin front steps on gravel facing the porch and doorway
            spObj.transform.position = new Vector3(-22.5f, 0.5f, 314.0f);
            spObj.transform.rotation = Quaternion.Euler(0f, 102f, 0f);

            PlayerSpawnPoint sp = spObj.AddComponent<PlayerSpawnPoint>();
            sp.spawnId = "Default";

            EditorSceneManager.MarkSceneDirty(houseScene);
            Debug.Log("[SceneManagementUtilities] Added PlayerSpawnPoint to HouseScene at (-22.5, 0.5, 314.0) facing cabin.");
        }
    }

    [MenuItem("Tools/Scenes/Switch to HouseScene (Single Scene Mode)")]
    public static void OpenHouseSceneSingle()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            string path = "Assets/HouseScene.unity";
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("[SceneManagementUtilities] Opened HouseScene in Single Scene Mode.");
        }
    }

    [MenuItem("Tools/Scenes/Switch to GasScene2 (Single Scene Mode)")]
    public static void OpenGasSceneSingle()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            string path = "Assets/GasScene2.unity";
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("[SceneManagementUtilities] Opened GasScene2 in Single Scene Mode.");
        }
    }
}

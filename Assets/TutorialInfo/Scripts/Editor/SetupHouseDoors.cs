using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Configures all doors in HouseScene with precision:
/// 1. Attaches CabinDoorInteractable directly to the rotating visual door meshes (e.g. door01..door05 in grp_drawsDoors/doors),
///    so colliders rotate WITH the doors, ensuring 100% accurate pointing whether open or closed.
/// 2. Disables static proxy colliders (doors_interact, draws_interact) that stole raycasts into empty holes.
/// 3. Removes all bogus 1x1x1 solid BoxColliders that caused bumps in front of doorways.
/// 4. Converts door interaction colliders to triggers (isTrigger = true) so they never bump, trip, or block the player.
/// 5. Strictly excludes and disables all drawers (draws, ChestOfDraws, BedsideTable).
/// </summary>
[InitializeOnLoad]
public static class SetupHouseDoors
{
    private const string SessionKey = "HouseDoors_Configured_V3_Precision";

    static SetupHouseDoors()
    {
        EditorApplication.delayCall += AutoRunOnLoad;
        EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            if (string.Equals(scene.name, "HouseScene", System.StringComparison.OrdinalIgnoreCase))
            {
                EditorApplication.delayCall += () => ConfigureAllHouseDoors(markDirty: true);
            }
        };
    }

    private static void AutoRunOnLoad()
    {
        EnsureHouseSceneIsPlayModeStart();

        if (!SessionState.GetBool(SessionKey, false))
        {
            SessionState.SetBool(SessionKey, true);
            ConfigureAllHouseDoors(markDirty: true);
        }
    }

    [MenuItem("Tools/House Scene/Set HouseScene as PlayMode Start Scene")]
    public static void EnsureHouseSceneIsPlayModeStart()
    {
        SceneAsset houseSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/HouseScene.unity");
        if (houseSceneAsset != null)
        {
            if (EditorSceneManager.playModeStartScene != houseSceneAsset)
            {
                EditorSceneManager.playModeStartScene = houseSceneAsset;
                Debug.Log("[SetupHouseDoors] Set EditorSceneManager.playModeStartScene to Assets/HouseScene.unity.");
            }
        }
    }

    [MenuItem("Tools/House Scene/Setup Interactive Doors (Zero Draws, Zero Bumps)")]
    public static void SetupInteractiveDoorsMenu()
    {
        EnsureHouseSceneIsPlayModeStart();
        CloseGasSceneIfOpen();
        ConfigureAllHouseDoors(markDirty: true);
    }

    private static void CloseGasSceneIfOpen()
    {
        Scene houseScene = SceneManager.GetSceneByName("HouseScene");
        Scene gasScene = SceneManager.GetSceneByName("GasScene2");

        if (houseScene.IsValid() && houseScene.isLoaded && gasScene.IsValid() && gasScene.isLoaded)
        {
            EditorSceneManager.CloseScene(gasScene, true);
            EditorSceneManager.SetActiveScene(houseScene);
            Debug.Log("[SetupHouseDoors] Closed GasScene2 from Hierarchy window. HouseScene is now single active.");
        }
    }

    public static void ConfigureAllHouseDoors(bool markDirty = true)
    {
        Scene houseScene = SceneManager.GetSceneByName("HouseScene");
        if (!houseScene.IsValid() || !houseScene.isLoaded) return;

        Debug.Log("[SetupHouseDoors] Configuring precision doors and removing floor bumps...");

        CloseGasSceneIfOpen();

        // 1. Deactivate ALL static legacy proxies (*interact) across the entire scene
        DisableAllStaticProxies(houseScene);

        // 2. Remove bogus solid 1x1x1 BoxColliders that were added to empty pivot transforms (the source of the floor bumps)
        RemoveBogus1x1BoxColliders(houseScene);

        // 3. Configure Kitchen Bench doors (grp_drawsDoors/doors)
        ConfigureKitchenBenchDoors(houseScene);

        // 4. Configure Cabin room doors (DoorDouble, DoorSingle, Closet, Pantry, BathroomSink, TV Stand)
        ConfigureCabinRoomDoors(houseScene);

        // 5. Strictly exclude all drawers
        ExcludeAllDrawers(houseScene);

        // 6. Ensure HouseSceneStoryController exists
        HouseSceneStoryController.EnsureExists();

        if (markDirty && !EditorApplication.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(houseScene);
        }

        Debug.Log("[SetupHouseDoors] Completed: All doors configured with precision triggers and zero floor bumps!");
    }

    private static void DisableAllStaticProxies(Scene scene)
    {
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform t in allTransforms)
        {
            if (t == null || t.gameObject.scene != scene) continue;
            string name = t.name.Trim();

            if (t.GetComponent<CabinLightSwitch>() != null || name.IndexOf("switch", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            if (name.IndexOf("interact", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                CabinDoorInteractable cdi = t.GetComponent<CabinDoorInteractable>();
                if (cdi != null) Object.DestroyImmediate(cdi);

                Collider[] cols = t.GetComponents<Collider>();
                foreach (var col in cols) Object.DestroyImmediate(col);

                t.gameObject.SetActive(false);
            }
        }
    }

    private static void RemoveBogus1x1BoxColliders(Scene scene)
    {
        BoxCollider[] allBoxColliders = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include);
        int removedCount = 0;

        foreach (BoxCollider bc in allBoxColliders)
        {
            if (bc == null || bc.gameObject.scene != scene) continue;

            // Skip light switches
            if (bc.GetComponent<CabinLightSwitch>() != null || bc.name.IndexOf("switch", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            // Detect bogus box collider on empty or non-mesh transforms
            MeshFilter mf = bc.GetComponent<MeshFilter>();
            bool isDefault1x1 = (Mathf.Approximately(bc.size.x, 1f) && Mathf.Approximately(bc.size.y, 1f) && Mathf.Approximately(bc.size.z, 1f));

            if (mf == null || mf.sharedMesh == null)
            {
                if (isDefault1x1 || bc.name.IndexOf("door", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Object.DestroyImmediate(bc);
                    removedCount++;
                }
            }
        }

        if (removedCount > 0)
        {
            Debug.Log($"[SetupHouseDoors] Removed {removedCount} bogus 1x1x1 solid BoxColliders that caused bumps in doorways.");
        }
    }

    private static void ConfigureKitchenBenchDoors(Scene scene)
    {
        // Locate PFB_KitchenBench
        GameObject kitchenBench = null;
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform t in allTransforms)
        {
            if (t != null && t.gameObject.scene == scene && t.name.Equals("PFB_KitchenBench", System.StringComparison.OrdinalIgnoreCase))
            {
                kitchenBench = t.gameObject;
                break;
            }
        }

        if (kitchenBench == null) return;

        Animator benchAnimator = kitchenBench.GetComponent<Animator>();
        if (benchAnimator != null) benchAnimator.enabled = true;

        // Disable legacy doors_interact and draws_interact static proxy parents
        foreach (Transform child in kitchenBench.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Equals("doors_interact", System.StringComparison.OrdinalIgnoreCase) ||
                child.name.Equals("draws_interact", System.StringComparison.OrdinalIgnoreCase))
            {
                // Remove CabinDoorInteractable from proxies so they never catch raycasts
                CabinDoorInteractable[] proxyDoors = child.GetComponentsInChildren<CabinDoorInteractable>(true);
                foreach (var pd in proxyDoors) Object.DestroyImmediate(pd);

                // Disable proxy colliders
                Collider[] proxyCols = child.GetComponentsInChildren<Collider>(true);
                foreach (var c in proxyCols) c.enabled = false;

                child.gameObject.SetActive(false);
            }
        }

        // Configure moving door meshes under grp_drawsDoors/doors (door01 through door05)
        for (int i = 1; i <= 5; i++)
        {
            string doorName = $"door0{i}";
            GameObject doorObj = null;

            foreach (Transform t in kitchenBench.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Equals(doorName, System.StringComparison.OrdinalIgnoreCase))
                {
                    // Ensure it's under the doors group, not draws
                    if (GetHierarchyPath(t).IndexOf("doors", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        doorObj = t.gameObject;
                        break;
                    }
                }
            }

            if (doorObj == null) continue;

            // Remove any old non-convex MeshCollider that could physically bump the player
            MeshCollider mc = doorObj.GetComponent<MeshCollider>();
            if (mc != null) Object.DestroyImmediate(mc);

            // Add fitted trigger BoxCollider
            MeshFilter mf = doorObj.GetComponent<MeshFilter>();
            BoxCollider bc = doorObj.GetComponent<BoxCollider>();
            if (bc == null) bc = doorObj.AddComponent<BoxCollider>();

            if (mf != null && mf.sharedMesh != null)
            {
                bc.size = mf.sharedMesh.bounds.size;
                bc.center = mf.sharedMesh.bounds.center;
            }
            else
            {
                bc.size = new Vector3(0.55f, 0.68f, 0.08f);
                bc.center = new Vector3(0.27f, 0.34f, 0f);
            }
            bc.isTrigger = true; // TRIGGER ONLY: 100% precision raycasting, ZERO physical bumps!

            // Add CabinDoorInteractable
            CabinDoorInteractable interactable = doorObj.GetComponent<CabinDoorInteractable>();
            if (interactable == null) interactable = doorObj.AddComponent<CabinDoorInteractable>();

            interactable.objectNumber = i;
            interactable.targetAnimator = benchAnimator;
            interactable.parameterPrefix = "isOpen_Obj_";

            // Remove legacy MoveableObject or MoveObjectController
            MoveObjectController moc = doorObj.GetComponentInParent<MoveObjectController>();
            if (moc != null) moc.enabled = false;

            Debug.Log($"[SetupHouseDoors] Attached precision trigger to moving door '{doorName}' (Obj #{i}).");
        }
    }

    private static void ConfigureCabinRoomDoors(Scene scene)
    {
        Animator[] allAnimators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include);

        foreach (Animator anim in allAnimators)
        {
            if (anim == null || anim.gameObject.scene != scene) continue;
            string parentPath = GetHierarchyPath(anim.transform);

            // Skip kitchen bench (handled above) and furniture with only drawers
            if (anim.name.Equals("PFB_KitchenBench", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (parentPath.IndexOf("ChestOfDraws", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (parentPath.IndexOf("BedsideTable", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            anim.enabled = true;

            // Remove giant trigger box on root door prefabs (PFB_DoorDouble, PFB_DoorSingle_Open)
            if (anim.name.IndexOf("DoorDouble", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                parentPath.IndexOf("DoorDouble", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                anim.name.IndexOf("DoorSingle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                parentPath.IndexOf("DoorSingle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                BoxCollider rootBox = anim.GetComponent<BoxCollider>();
                if (rootBox != null) Object.DestroyImmediate(rootBox);
            }

            // 1. PFB_DoorDouble (Front Entrance)
            if (anim.name.IndexOf("DoorDouble", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                parentPath.IndexOf("DoorDouble", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Both leaves on the entrance double door are driven together by isOpen_Obj_1
                ConfigureDoubleDoorLeaf(anim, new[] { "leftDoor", "doorL", "grp_doorsL" }, 1);
                ConfigureDoubleDoorLeaf(anim, new[] { "rightDoor", "doorR", "grp_doorsR" }, 1);
                continue;
            }

            // 2. PFB_DoorSingle_Open (Bedroom, Bathroom, etc.)
            if (anim.name.IndexOf("DoorSingle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                parentPath.IndexOf("DoorSingle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ConfigureSingleDoorLeaf(anim, new[] { "DoorSingle", "PFB_DoorSingle_Open", "door" }, 1);
                continue;
            }

            // 3. Other doors: Closet, Pantry, BathroomSink, TV Stand
            foreach (Transform child in anim.GetComponentsInChildren<Transform>(true))
            {
                if (child == anim.transform) continue;
                string childPath = GetHierarchyPath(child);
                if (IsDrawerObject(child, childPath)) continue;
                if (child.name.IndexOf("interact", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                // ONLY configure objects that have a real 3D mesh (door leaf mesh)
                MeshFilter mf = child.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                if (child.name.IndexOf("door", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Rigidbody rb = child.GetComponent<Rigidbody>();
                    if (rb != null) Object.DestroyImmediate(rb);

                    MeshCollider mc = child.GetComponent<MeshCollider>();
                    if (mc != null) Object.DestroyImmediate(mc);

                    BoxCollider bc = child.GetComponent<BoxCollider>();
                    if (bc == null) bc = child.gameObject.AddComponent<BoxCollider>();
                    bc.size = mf.sharedMesh.bounds.size;
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.isTrigger = true; // TRIGGER ONLY!

                    CabinDoorInteractable cdi = child.GetComponent<CabinDoorInteractable>();
                    if (cdi == null) cdi = child.gameObject.AddComponent<CabinDoorInteractable>();

                    MoveableObject mo = child.GetComponent<MoveableObject>();
                    int objNum = mo != null ? mo.objectNumber : 1;
                    if (child.name.Equals("rightDoor", System.StringComparison.OrdinalIgnoreCase) || child.name.Equals("doorR", System.StringComparison.OrdinalIgnoreCase))
                    {
                        objNum = 2;
                    }

                    cdi.objectNumber = objNum;
                    cdi.targetAnimator = anim;
                    cdi.parameterPrefix = "isOpen_Obj_";
                }
            }
        }
    }

    private static void ConfigureDoubleDoorLeaf(Animator anim, string[] leafNames, int objNum)
    {
        Transform leaf = null;
        foreach (Transform t in anim.GetComponentsInChildren<Transform>(true))
        {
            foreach (string name in leafNames)
            {
                if (t.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                {
                    leaf = t;
                    break;
                }
            }
            if (leaf != null) break;
        }
        if (leaf == null) return;

        // Clean up Rigidbody & solid mesh colliders
        Rigidbody rb = leaf.GetComponent<Rigidbody>();
        if (rb != null) Object.DestroyImmediate(rb);

        MeshCollider mc = leaf.GetComponent<MeshCollider>();
        if (mc != null) Object.DestroyImmediate(mc);

        // Add fitted trigger BoxCollider
        MeshFilter mf = leaf.GetComponent<MeshFilter>();
        BoxCollider bc = leaf.GetComponent<BoxCollider>();
        if (bc == null) bc = leaf.gameObject.AddComponent<BoxCollider>();

        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 meshSize = mf.sharedMesh.bounds.size;
            bc.size = new Vector3(meshSize.x, meshSize.y, Mathf.Max(meshSize.z, 0.50f));
            bc.center = mf.sharedMesh.bounds.center;
        }
        bc.isTrigger = true; // TRIGGER ONLY: generous targeting and zero physical bumps!

        CabinDoorInteractable cdi = leaf.GetComponent<CabinDoorInteractable>();
        if (cdi == null) cdi = leaf.gameObject.AddComponent<CabinDoorInteractable>();

        cdi.objectNumber = objNum;
        cdi.targetAnimator = anim;
        cdi.parameterPrefix = "isOpen_Obj_";
    }

    private static void ConfigureSingleDoorLeaf(Animator anim, string[] leafNames, int objNum)
    {
        Transform leaf = null;
        foreach (Transform t in anim.GetComponentsInChildren<Transform>(true))
        {
            foreach (string name in leafNames)
            {
                if (t.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                {
                    leaf = t;
                    break;
                }
            }
            if (leaf != null) break;
        }
        if (leaf == null) return;

        // Remove Rigidbody if present to prevent physics bumps
        Rigidbody rb = leaf.GetComponent<Rigidbody>();
        if (rb != null) Object.DestroyImmediate(rb);

        MeshCollider mc = leaf.GetComponent<MeshCollider>();
        if (mc != null) Object.DestroyImmediate(mc);

        MeshFilter mf = leaf.GetComponent<MeshFilter>();
        BoxCollider bc = leaf.GetComponent<BoxCollider>();
        if (bc == null) bc = leaf.gameObject.AddComponent<BoxCollider>();

        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 meshSize = mf.sharedMesh.bounds.size;
            bc.size = new Vector3(meshSize.x, meshSize.y, Mathf.Max(meshSize.z, 0.50f));
            bc.center = mf.sharedMesh.bounds.center;
        }
        bc.isTrigger = true; // TRIGGER ONLY: generous targeting and zero physical bumps!

        CabinDoorInteractable cdi = leaf.GetComponent<CabinDoorInteractable>();
        if (cdi == null) cdi = leaf.gameObject.AddComponent<CabinDoorInteractable>();

        cdi.objectNumber = objNum;
        cdi.targetAnimator = anim;
        cdi.parameterPrefix = "isOpen_Obj_";
    }

    private static void ExcludeAllDrawers(Scene scene)
    {
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform t in allTransforms)
        {
            if (t == null || t.gameObject.scene != scene) continue;
            string fullPath = GetHierarchyPath(t);

            if (IsDrawerObject(t, fullPath))
            {
                // Remove CabinDoorInteractable
                CabinDoorInteractable stray = t.GetComponent<CabinDoorInteractable>();
                if (stray != null) Object.DestroyImmediate(stray);

                // Disable legacy MoveObjectController
                MoveObjectController moc = t.GetComponentInParent<MoveObjectController>();
                if (moc != null) moc.enabled = false;
            }
        }
    }

    private static bool IsDrawerObject(Transform t, string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return false;

        if (fullPath.IndexOf("grp_drawsDoors/draws", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            fullPath.IndexOf("draws_interact", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            fullPath.IndexOf("ChestOfDraws", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            fullPath.IndexOf("BedsideTable", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (t != null && t.name.StartsWith("draw", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return "";
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}

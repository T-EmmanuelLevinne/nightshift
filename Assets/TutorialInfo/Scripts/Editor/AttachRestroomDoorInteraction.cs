using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// [InitializeOnLoad]
public static class AttachRestroomDoorInteraction
{
    // static AttachRestroomDoorInteraction()
    // {
    //     EditorApplication.delayCall += AttachNonDestructive;
    //     EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    // }

    // private static void OnPlayModeStateChanged(PlayModeStateChange state)
    // {
    //     if (state == PlayModeStateChange.EnteredEditMode)
    //     {
    //         EditorApplication.delayCall += AttachNonDestructive;
    //     }
    // }

    [MenuItem("Tools/Gas Station/Enable Restroom Door E-Interaction & Cashier (Non-Destructive)")]
    public static void AttachNonDestructive()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;

        GameObject stacja = GameObject.Find("stacja");
        if (stacja == null) return;

        bool modified = false;

        // 1. Non-destructively ensure Cashier objects are active (without touching Convenience_Store_V2 or any user-added props!)
        string[] cashierProps = new string[]
        {
            "Cashier", "Cashier2", "Cube.008", "Cube.049", "Cube.009", "Cube.021",
            "tuna can large.005", "Cube.010", "Cube.013", "Cube.029", "Cube.027", "Cube.028", "Cube.012"
        };
        HashSet<string> cashierSet = new HashSet<string>(cashierProps, StringComparer.OrdinalIgnoreCase);

        foreach (Transform t in stacja.GetComponentsInChildren<Transform>(true))
        {
            // Activate Cashier props if currently hidden
            if (cashierSet.Contains(t.name) && !t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && t.GetComponent<MeshCollider>() == null)
                {
                    MeshCollider mc = t.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
                EditorUtility.SetDirty(t.gameObject);
                modified = true;
            }

            // Rename Girl/Female Restroom objects to Stockroom
            if (t.name == "Female_Restroom")
            {
                t.name = "StockRoom";
                EditorUtility.SetDirty(t.gameObject);
                modified = true;
            }
            else if (t.name == "Female_Restroom_DoorPivot")
            {
                t.name = "Stockroom_DoorPivot";
                EditorUtility.SetDirty(t.gameObject);
                modified = true;
            }
            else if (t.name == "Female_Restroom_Door")
            {
                t.name = "Stockroom_Door";
                EditorUtility.SetDirty(t.gameObject);
                modified = true;
            }
            else if (t.name == "Partition_Wall_FemaleDoorHeader")
            {
                t.name = "Partition_Wall_StockroomDoorHeader";
                EditorUtility.SetDirty(t.gameObject);
                modified = true;
            }
            else if (t.name == "Light_Female_Restroom")
            {
                t.name = "Light_Stockroom";
                EditorUtility.SetDirty(t.gameObject);
                modified = true;
            }
            else if (t.name == "Light_Female_Restroom_PointLight")
            {
                t.name = "Light_Stockroom_PointLight";
                EditorUtility.SetDirty(t.gameObject);
                modified = true;
            }

            // 2. Non-destructively attach E-key interaction to Male Restroom & Stockroom door pivots
            if (t.name == "Male_Restroom_DoorPivot" || t.name == "Stockroom_DoorPivot" || t.name == "Female_Restroom_DoorPivot")
            {
                bool isMale = t.name.StartsWith("Male", StringComparison.OrdinalIgnoreCase);
                string targetLabel = isMale ? "Male Restroom Door" : "Stockroom Door";

                InteractableRestroomDoor interact = t.GetComponent<InteractableRestroomDoor>();
                if (interact == null)
                {
                    interact = t.gameObject.AddComponent<InteractableRestroomDoor>();
                    interact.doorLabel = targetLabel;
                    interact.closedAngleY = 0f;
                    interact.openAngleY = 75f;
                    interact.isOpen = false;
                    t.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    EditorUtility.SetDirty(t.gameObject);
                    modified = true;
                }
                else if (interact.doorLabel != targetLabel)
                {
                    interact.doorLabel = targetLabel;
                    EditorUtility.SetDirty(interact);
                    modified = true;
                }
            }
        }

        // 3. Ensure AutomaticSlidingDoors closes 100% flush with zero center gap
        AutomaticSlidingDoors[] slidingDoors = UnityEngine.Object.FindObjectsByType<AutomaticSlidingDoors>(FindObjectsInactive.Include);
        foreach (AutomaticSlidingDoors door in slidingDoors)
        {
            if (door == null) continue;
            door.EnsureFlushClosedPositions();
            if (!door.isOpen && door.leftDoor != null && door.rightDoor != null)
            {
                door.leftDoor.localPosition = new Vector3(-0.66f, 0f, 0f);
                door.rightDoor.localPosition = new Vector3(0.66f, 0f, 0f);
            }
            EditorUtility.SetDirty(door);
            if (door.leftDoor != null) EditorUtility.SetDirty(door.leftDoor);
            if (door.rightDoor != null) EditorUtility.SetDirty(door.rightDoor);
        }

        // 4. Ensure Mop_Return_Spot is on Ignore Raycast layer and sized strictly inside Stockroom
        GameObject returnSpot = GameObject.Find("Mop_Return_Spot");
        if (returnSpot != null)
        {
            if (returnSpot.layer != 2)
            {
                returnSpot.layer = 2;
                EditorUtility.SetDirty(returnSpot);
                modified = true;
            }
            BoxCollider bc = returnSpot.GetComponent<BoxCollider>();
            if (bc != null && (bc.size != new Vector3(1.1f, 2.0f, 1.1f) || bc.center != new Vector3(0.35f, 0f, 0.35f)))
            {
                bc.isTrigger = true;
                bc.size = new Vector3(1.1f, 2.0f, 1.1f);
                bc.center = new Vector3(0.35f, 0f, 0.35f);
                EditorUtility.SetDirty(bc);
                modified = true;
            }
        }

        // 5. Ensure HorrorInteractionReticle and upgraded RetroHorrorPOV on Test_FPS_Player
        GameObject player = GameObject.Find("Test_FPS_Player");
        if (player != null)
        {
            if (player.GetComponent<HorrorInteractionReticle>() == null)
            {
                player.AddComponent<HorrorInteractionReticle>();
                EditorUtility.SetDirty(player);
                modified = true;
            }
            RetroHorrorPOV pov = player.GetComponent<RetroHorrorPOV>();
            if (pov == null)
            {
                pov = player.AddComponent<RetroHorrorPOV>();
                modified = true;
            }
            pov.UpgradePresetIfNeeded();
            pov.ApplyHorrorVolumeOverrides();
            EditorUtility.SetDirty(pov);
        }

        if (modified && !EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[AttachRestroomDoorInteraction] Updated sliding doors, horror POV distortion, and Stockroom interactions!");
        }
    }
}

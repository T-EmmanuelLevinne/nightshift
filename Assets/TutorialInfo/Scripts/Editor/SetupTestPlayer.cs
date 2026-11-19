using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupTestPlayer
{
    [MenuItem("Tools/Spawn Test First Person Player")]
    public static void Execute()
    {
        Debug.Log("[SetupTestPlayer] Spawning test first person player...");

        GameObject stacja = GameObject.Find("stacja");
        if (stacja == null)
        {
            Debug.LogWarning("[SetupTestPlayer] 'stacja' not found in scene.");
            return;
        }

        Transform plane1 = null;
        foreach (Transform t in stacja.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Plane.001") { plane1 = t; break; }
        }

        if (plane1 == null)
        {
            Debug.LogWarning("[SetupTestPlayer] Plane.001 not found inside stacja.");
            return;
        }

        // 1. Ensure collision on floors and ground so player doesn't fall through (Plane only, NEVER Cube/curbs)
        foreach (Transform child in stacja.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.StartsWith("Cube"))
            {
                // Remove unwanted mesh colliders from ground curbs and cubes that create physical speedbumps
                MeshCollider mc = child.GetComponent<MeshCollider>();
                if (mc != null)
                {
                    UnityEngine.Object.DestroyImmediate(mc, true);
                }
            }
            else if (child.name == "Plane.001")
            {
                MeshFilter mf = child.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider mc = child.GetComponent<MeshCollider>();
                    if (mc == null)
                    {
                        mc = child.gameObject.AddComponent<MeshCollider>();
                    }
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
        }

        // 2. Calculate entrance position and orientation
        Vector3 localDoorCenter = new Vector3(4.46f, 8.115f, -0.385f);
        Vector3 worldDoorCenter = plane1.TransformPoint(localDoorCenter);
        Vector3 dirNormal = plane1.TransformDirection(Vector3.right); // Outward from entrance
        Vector3 dirUp = Vector3.up;

        // Player spawn position: 4.5 meters outside in front of the entrance door
        Vector3 playerPos = worldDoorCenter + dirNormal * 4.5f;

        // Raycast down to ground to get exact ground height
        if (Physics.Raycast(playerPos + Vector3.up * 5.0f, Vector3.down, out RaycastHit hit, 20f))
        {
            playerPos.y = hit.point.y + 0.05f;
        }
        else
        {
            playerPos.y = worldDoorCenter.y - 1.1f;
        }

        Quaternion playerRot = Quaternion.LookRotation(-dirNormal, dirUp); // Facing into the store

        // 3. Remove existing player if already present
        GameObject existingPlayer = GameObject.Find("Test_FPS_Player");
        if (existingPlayer != null)
        {
            UnityEngine.Object.DestroyImmediate(existingPlayer);
        }

        // 4. Create Player GameObject
        GameObject player = new GameObject("Test_FPS_Player");
        player.transform.position = playerPos;
        player.transform.rotation = playerRot;

        // Character Controller
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.35f;

        // FPS Controller
        SimpleFPSController fps = player.AddComponent<SimpleFPSController>();
        fps.walkSpeed = 4.5f;
        fps.sprintSpeed = 7.5f;
        fps.canJump = false; // Disallow jumping per user request
        fps.jumpHeight = 1.2f;
        fps.mouseSensitivity = 2.0f;

        // Horror POV, Reticle, Ambience, and Fridge Stocker
        player.AddComponent<HorrorInteractionReticle>();
        player.AddComponent<RetroHorrorPOV>();
        player.AddComponent<HorrorAmbience>();
        player.AddComponent<StockFridgesRuntime>();

        // 5. First Person Camera
        GameObject camObj = new GameObject("PlayerCamera");
        camObj.transform.SetParent(player.transform, false);
        camObj.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        camObj.transform.localRotation = Quaternion.identity;
        camObj.tag = "MainCamera";

        Camera cam = camObj.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 75f;
        camObj.AddComponent<AudioListener>();

        fps.playerCamera = cam;

        // 6. Disable other existing cameras to avoid conflicts
        Camera[] allCams = UnityEngine.Object.FindObjectsByType<Camera>();
        foreach (var c in allCams)
        {
            if (c != cam && c.gameObject.name != "SceneCamera")
            {
                c.enabled = false;
                AudioListener al = c.GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
            }
        }

        EditorUtility.SetDirty(player);
        Scene activeScene = SceneManager.GetActiveScene();
        if (!EditorApplication.isPlaying && !Application.isPlaying)
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log($"[SetupTestPlayer] Test FPS Player successfully spawned at {playerPos} facing store entrance!");
    }
}

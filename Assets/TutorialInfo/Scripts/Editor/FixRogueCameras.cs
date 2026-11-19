using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class FixRogueCameras
{
    static FixRogueCameras()
    {
        EditorApplication.delayCall += AutoFixRogueCameras;
    }

    [MenuItem("Tools/Gas Station/Fix Rogue Cameras (Disable skp_camera & Prioritize Player)")]
    public static void AutoFixRogueCameras()
    {
        bool changed = false;

        // 1. Disable importCameras on shelves-pack model.dae so SketchUp camera is never imported
        string daePath = "Assets/shelves-pack/source/1695/model.dae";
        ModelImporter importer = AssetImporter.GetAtPath(daePath) as ModelImporter;
        if (importer != null && importer.importCameras)
        {
            importer.importCameras = false;
            importer.SaveAndReimport();
            Debug.Log("[FixRogueCameras] Disabled importCameras on " + daePath);
        }

        // 2. Find and remove/disable any skp_camera in the active scene
        Camera[] allCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        foreach (Camera c in allCams)
        {
            if (c == null) continue;

            string objName = c.gameObject.name;
            if (objName.Contains("skp_camera") || objName.Contains("SketchUp"))
            {
                Debug.Log($"[FixRogueCameras] Removing rogue SketchUp camera GameObject '{objName}'.");
                c.enabled = false;
                GameObject go = c.gameObject;

                // Remove UniversalAdditionalCameraData before Camera to avoid dependency warning
                var uacData = go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (uacData != null)
                {
                    Object.DestroyImmediate(uacData);
                }

                // Destroy the rogue camera GameObject completely
                Object.DestroyImmediate(go);
                changed = true;
            }
            else if (objName == "Main Camera")
            {
                // Disable unused default Unity scene Main Camera if Test_FPS_Player exists
                GameObject player = GameObject.Find("Test_FPS_Player");
                if (player != null && c.transform.parent != player.transform)
                {
                    c.enabled = false;
                    AudioListener al = c.GetComponent<AudioListener>();
                    if (al != null) al.enabled = false;
                    changed = true;
                }
            }
        }

        // 3. Ensure Test_FPS_Player camera has highest depth and is enabled
        GameObject fpsPlayer = GameObject.Find("Test_FPS_Player");
        if (fpsPlayer != null)
        {
            Camera playerCam = fpsPlayer.GetComponentInChildren<Camera>(true);
            if (playerCam != null)
            {
                playerCam.gameObject.SetActive(true);
                playerCam.enabled = true;
                playerCam.tag = "MainCamera";
                playerCam.depth = 10; // Prioritize over any other cameras

                AudioListener al = playerCam.GetComponent<AudioListener>();
                if (al == null) al = playerCam.gameObject.AddComponent<AudioListener>();
                al.enabled = true;
                changed = true;
            }
        }

        if (changed)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && !EditorApplication.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log("[FixRogueCameras] Scene cameras cleaned up and saved successfully!");
            }
        }
    }
}

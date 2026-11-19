using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class AttachHorrorExperience
{
    [MenuItem("Tools/Gas Station/Setup Horror Experience (Reticle, Distorted POV, Ambience, No Jump)")]
    public static void SetupHorrorExperience()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;

        GameObject player = GameObject.Find("Test_FPS_Player");
        if (player == null) return;

        bool modified = false;

        // 1. Enable URP Post-Processing on PlayerCamera
        Camera cam = player.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null)
            {
                camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            if (!camData.renderPostProcessing)
            {
                camData.renderPostProcessing = true;
                EditorUtility.SetDirty(camData);
                modified = true;
            }
        }

        // 2. Disable player jumping in SimpleFPSController
        SimpleFPSController fps = player.GetComponent<SimpleFPSController>();
        if (fps != null && fps.canJump)
        {
            fps.canJump = false;
            EditorUtility.SetDirty(fps);
            modified = true;
        }

        // 3. Ensure circular dot reticle
        HorrorInteractionReticle reticle = player.GetComponent<HorrorInteractionReticle>();
        if (reticle == null)
        {
            reticle = player.AddComponent<HorrorInteractionReticle>();
            EditorUtility.SetDirty(player);
            modified = true;
        }
        if (reticle.circleDiameter < 4f)
        {
            reticle.circleDiameter = 8f;
            EditorUtility.SetDirty(reticle);
            modified = true;
        }

        // 4. Ensure RetroHorrorPOV has distorted low-quality horror settings enabled
        RetroHorrorPOV pov = player.GetComponent<RetroHorrorPOV>();
        if (pov == null)
        {
            pov = player.AddComponent<RetroHorrorPOV>();
            EditorUtility.SetDirty(player);
            modified = true;
        }
        pov.UpgradePresetIfNeeded();
        pov.ApplyHorrorVolumeOverrides();
        EditorUtility.SetDirty(pov);

        // 5. Attach Atmospheric Horror Ambience & AudioSource
        HorrorAmbience ambience = player.GetComponent<HorrorAmbience>();
        if (ambience == null)
        {
            ambience = player.AddComponent<HorrorAmbience>();
            ambience.volume = 0.65f;
            ambience.playOnStart = true;
            ambience.enableProceduralFallback = true;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/HorrorAmbience_GasStation.wav");
            if (clip != null)
            {
                ambience.ambienceClip = clip;
            }

            AudioSource audioSource = player.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = player.AddComponent<AudioSource>();
            }
            audioSource.loop = true;
            audioSource.playOnAwake = true;
            audioSource.volume = 0.65f;
            audioSource.spatialBlend = 0f;
            if (clip != null)
            {
                audioSource.clip = clip;
            }

            EditorUtility.SetDirty(player);
            modified = true;
        }

        if (modified && !EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[HorrorExperience] Player camera post-processing and low-quality distorted horror POV updated!");
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatically discovers and configures the 3 room light switches in HouseScene:
/// 1. PFB_Lightswitch_LivingRoom -> Controls the 6 Living Room & Kitchen ceiling lights
/// 2. PFB_Lightswitch_Bedroom    -> Controls the 2 Bedroom ceiling lights
/// 3. PFB_Lightswitch_Bathroom   -> Controls the 3 Bathroom ceiling lights
/// Adds BoxColliders, CabinLightSwitch components, and connects the exact room Light references.
/// </summary>
[InitializeOnLoad]
public static class SetupCabinLightSwitches
{
    private const string SessionKey = "CabinLightSwitches_Configured_V2_Saved";

    static SetupCabinLightSwitches()
    {
        EditorApplication.delayCall += AutoRunOnLoad;
        EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            if (string.Equals(scene.name, "HouseScene", System.StringComparison.OrdinalIgnoreCase))
            {
                EditorApplication.delayCall += () => ConfigureAllLightSwitches(markDirty: true);
            }
        };
    }

    private static void AutoRunOnLoad()
    {
        if (!SessionState.GetBool(SessionKey, false))
        {
            SessionState.SetBool(SessionKey, true);
            ConfigureAllLightSwitches(markDirty: true);
        }
    }

    [MenuItem("Tools/House Scene/Setup Light Switches (Living Room, Bedroom, Bathroom)")]
    public static void SetupLightSwitchesMenu()
    {
        ConfigureAllLightSwitches(markDirty: true);
    }

    public static void ConfigureAllLightSwitches(bool markDirty = true)
    {
        Scene houseScene = SceneManager.GetSceneByName("HouseScene");
        if (!houseScene.IsValid() || !houseScene.isLoaded)
        {
            return;
        }

        Debug.Log("[SetupCabinLightSwitches] Discovering and configuring light switches in HouseScene...");

        // 1. Locate the 3 switch GameObjects
        GameObject livingRoomSwitchGo = null;
        GameObject bedroomSwitchGo = null;
        GameObject bathroomSwitchGo = null;

        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform t in allTransforms)
        {
            if (t == null || t.gameObject.scene != houseScene) continue;

            string nm = t.name.Trim();
            if (nm.Equals("PFB_Lightswitch_LivingRoom", System.StringComparison.OrdinalIgnoreCase) ||
                nm.Equals("PFB_Lightswitch (1)", System.StringComparison.OrdinalIgnoreCase))
            {
                livingRoomSwitchGo = t.gameObject;
            }
            else if (nm.Equals("PFB_Lightswitch_Bedroom", System.StringComparison.OrdinalIgnoreCase) ||
                     nm.Equals("PFB_Lightswitch (2)", System.StringComparison.OrdinalIgnoreCase))
            {
                bedroomSwitchGo = t.gameObject;
            }
            else if (nm.Equals("PFB_Lightswitch_Bathroom", System.StringComparison.OrdinalIgnoreCase) ||
                     nm.Equals("PFB_Lightswitch (3)", System.StringComparison.OrdinalIgnoreCase))
            {
                bathroomSwitchGo = t.gameObject;
            }
        }

        if (livingRoomSwitchGo == null && bedroomSwitchGo == null && bathroomSwitchGo == null)
        {
            Debug.LogWarning("[SetupCabinLightSwitches] No light switch objects found in HouseScene!");
            return;
        }

        // 2. Discover all ceiling lights in the cabin
        // PFB_Building_Full root position is around (-13.51, -0.13, 308.30)
        List<Light> livingRoomLights = new List<Light>();
        List<Light> bedroomLights = new List<Light>();
        List<Light> bathroomLights = new List<Light>();

        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        foreach (Light l in allLights)
        {
            if (l == null || l.gameObject.scene != houseScene) continue;
            // Skip Directional Light
            if (l.type == LightType.Directional) continue;

            Transform targetTransform = l.transform.parent != null ? l.transform.parent : l.transform;
            CabinLightSwitch.RoomPlace room = HouseSceneStoryController.GetLightRoomPlace(targetTransform);

            switch (room)
            {
                case CabinLightSwitch.RoomPlace.LivingRoom:
                    livingRoomLights.Add(l);
                    break;
                case CabinLightSwitch.RoomPlace.Bedroom:
                    bedroomLights.Add(l);
                    break;
                case CabinLightSwitch.RoomPlace.Bathroom:
                    bathroomLights.Add(l);
                    break;
            }
        }

        Debug.Log($"[SetupCabinLightSwitches] Identified lights: LivingRoom={livingRoomLights.Count}, Bedroom={bedroomLights.Count}, Bathroom={bathroomLights.Count}.");

        // 3. Configure Living Room Switch
        if (livingRoomSwitchGo != null)
        {
            ConfigureSingleSwitch(livingRoomSwitchGo, CabinLightSwitch.RoomPlace.LivingRoom, livingRoomLights);
        }

        // 4. Configure Bedroom Switch
        if (bedroomSwitchGo != null)
        {
            ConfigureSingleSwitch(bedroomSwitchGo, CabinLightSwitch.RoomPlace.Bedroom, bedroomLights);
        }

        // 5. Configure Bathroom Switch
        if (bathroomSwitchGo != null)
        {
            ConfigureSingleSwitch(bathroomSwitchGo, CabinLightSwitch.RoomPlace.Bathroom, bathroomLights);
        }

        if (markDirty && !EditorApplication.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(houseScene);
            EditorSceneManager.SaveScene(houseScene);
        }

        Debug.Log("[SetupCabinLightSwitches] Successfully configured all 3 room light switches!");
    }

    private static void ConfigureSingleSwitch(GameObject switchGo, CabinLightSwitch.RoomPlace place, List<Light> lights)
    {
        // Remove any door interactable if erroneously attached
        CabinDoorInteractable strayDoor = switchGo.GetComponent<CabinDoorInteractable>();
        if (strayDoor != null) Object.DestroyImmediate(strayDoor);

        // Add or retrieve CabinLightSwitch
        CabinLightSwitch lightSwitch = switchGo.GetComponent<CabinLightSwitch>();
        if (lightSwitch == null)
        {
            lightSwitch = switchGo.AddComponent<CabinLightSwitch>();
        }

        lightSwitch.roomPlace = place;
        lightSwitch.roomDisplayName = place == CabinLightSwitch.RoomPlace.LivingRoom ? "Living Room" : place.ToString();
        lightSwitch.isLightOn = true;
        lightSwitch.controlledLights = new List<Light>(lights);

        // Box collider for reticle interaction
        BoxCollider col = switchGo.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = switchGo.AddComponent<BoxCollider>();
        }
        col.size = new Vector3(0.50f, 0.60f, 0.50f);
        col.center = Vector3.zero;
        col.isTrigger = true;

        // Audio Source
        AudioSource audioSource = switchGo.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = switchGo.AddComponent<AudioSource>();
        }
        audioSource.spatialBlend = 1.0f;
        audioSource.minDistance = 0.5f;
        audioSource.maxDistance = 10.0f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.volume = 0.85f;
        audioSource.playOnAwake = false;

        lightSwitch.audioSource = audioSource;

        Debug.Log($"[SetupCabinLightSwitches] Configured switch '{switchGo.name}' for {place} with {lights.Count} lights.");
    }
}

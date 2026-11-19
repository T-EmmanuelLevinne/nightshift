using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls interactive wall light switches for rooms in HouseScene (Living Room, Bedroom, Bathroom).
/// Implements IInteractable for reticle detection and interaction ("E to Turn Off Light" / "E to Turn On Light").
/// Toggles assigned Light components and plays procedural mechanical switch click audio.
/// </summary>
public class CabinLightSwitch : MonoBehaviour, IInteractable
{
    public enum RoomPlace
    {
        LivingRoom,
        Bedroom,
        Bathroom
    }

    [Header("Room Identity")]
    public RoomPlace roomPlace = RoomPlace.LivingRoom;
    public string roomDisplayName = "Living Room";

    [Header("Switch State")]
    public bool isLightOn = true;

    [Header("Controlled Light Sources")]
    public List<Light> controlledLights = new List<Light>();

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    private static AudioClip switchClickClip;
    private static bool audioGenerated = false;

    private Quaternion baseLocalRot;
    private bool rotInitialized = false;

    private void Awake()
    {
        if (!rotInitialized)
        {
            baseLocalRot = transform.localRotation;
            rotInitialized = true;
        }

        UpdateDisplayName();
        SetupCollider();
        SetupAudio();
    }

    private void Start()
    {
        if (controlledLights == null || controlledLights.Count == 0)
        {
            AutoDiscoverRoomLights();
        }

        ApplyLightState(isLightOn, playSound: false);
    }

    public void AutoDiscoverRoomLights()
    {
        if (controlledLights == null) controlledLights = new List<Light>();
        controlledLights.Clear();

        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        foreach (Light l in allLights)
        {
            if (l == null || l.gameObject.scene != gameObject.scene) continue;
            if (l.type == LightType.Directional) continue;

            Vector3 worldPos = l.transform.position;

            bool matches = false;
            switch (roomPlace)
            {
                case RoomPlace.LivingRoom:
                    matches = (worldPos.z > 306.0f);
                    break;
                case RoomPlace.Bedroom:
                    matches = (worldPos.z <= 306.0f && worldPos.x > -13.51f);
                    break;
                case RoomPlace.Bathroom:
                    matches = (worldPos.z <= 306.0f && worldPos.x <= -13.51f);
                    break;
            }

            if (matches)
            {
                controlledLights.Add(l);
            }
        }

        Debug.Log($"[CabinLightSwitch] {roomDisplayName} auto-discovered {controlledLights.Count} room lights.");
    }

    private void UpdateDisplayName()
    {
        switch (roomPlace)
        {
            case RoomPlace.LivingRoom:
                roomDisplayName = "Living Room";
                break;
            case RoomPlace.Bedroom:
                roomDisplayName = "Bedroom";
                break;
            case RoomPlace.Bathroom:
                roomDisplayName = "Bathroom";
                break;
        }
    }

    private void SetupCollider()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
        }
        // Generous trigger volume on the wall for effortless reticle targeting and wall penetration
        col.size = new Vector3(0.50f, 0.60f, 0.50f);
        col.center = Vector3.zero;
        col.isTrigger = true;
    }

    private void SetupAudio()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D spatial sound
            audioSource.minDistance = 0.5f;
            audioSource.maxDistance = 10.0f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.volume = 0.85f;
            audioSource.playOnAwake = false;
        }

        if (!audioGenerated)
        {
            GenerateProceduralSwitchAudio();
            audioGenerated = true;
        }
    }

    public string GetPromptText()
    {
        return isLightOn ? $"E to Turn Off {roomDisplayName} Light" : $"E to Turn On {roomDisplayName} Light";
    }

    public void Interact()
    {
        ToggleSwitch();
    }

    public void ToggleSwitch()
    {
        isLightOn = !isLightOn;
        ApplyLightState(isLightOn, playSound: true);

        if (HouseSceneStoryController.IsPowerOutageActive)
        {
            HouseSceneStoryController.NotifyLightSwitchToggledDuringOutage(this);
        }
    }

    public static void ForceAllCabinLightsOffForOutage()
    {
        CabinLightSwitch[] switches = Object.FindObjectsByType<CabinLightSwitch>(FindObjectsInactive.Include);
        for (int i = 0; i < switches.Length; i++)
        {
            if (switches[i] == null) continue;
            if (switches[i].controlledLights == null || switches[i].controlledLights.Count == 0)
            {
                switches[i].AutoDiscoverRoomLights();
            }
            switches[i].isLightOn = false;
            switches[i].ApplyLightState(false, playSound: false);
        }

        // Also turn off any other indoor point/spot lights inside the cabin
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        for (int i = 0; i < allLights.Length; i++)
        {
            Light l = allLights[i];
            if (l == null || l.type == LightType.Directional) continue;
            string lName = l.gameObject.name;
            if (lName.IndexOf("Candle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                lName.IndexOf("Phone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                lName.IndexOf("Lightning", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }
            l.enabled = false;
        }
    }

    public void ApplyLightState(bool on, bool playSound)
    {
        bool effectiveLightOn = HouseSceneStoryController.IsPowerOutageActive ? false : on;

        if (controlledLights != null)
        {
            for (int i = 0; i < controlledLights.Count; i++)
            {
                if (controlledLights[i] != null)
                {
                    controlledLights[i].enabled = effectiveLightOn;
                    if (controlledLights[i].gameObject != null)
                    {
                        controlledLights[i].gameObject.SetActive(effectiveLightOn);
                    }
                }
            }
        }

        // Slight mechanical rocker tilt for physical visual feedback
        if (rotInitialized)
        {
            float tiltAngle = on ? 4.5f : -4.5f;
            transform.localRotation = baseLocalRot * Quaternion.Euler(tiltAngle, 0f, 0f);
        }

        if (playSound && audioSource != null && switchClickClip != null)
        {
            audioSource.pitch = on ? 1.06f : 0.94f;
            audioSource.PlayOneShot(switchClickClip);
        }

        Debug.Log($"[CabinLightSwitch] {roomDisplayName} lights toggled to {(on ? "ON" : "OFF")} ({controlledLights?.Count ?? 0} lights).");
    }

    private static void GenerateProceduralSwitchAudio()
    {
        int sampleRate = 44100;
        float dur = 0.08f; // Sharp, crisp mechanical snap
        int sampleCount = Mathf.RoundToInt(dur * sampleRate);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            // Dual-frequency mechanical contact click + snap
            float snap = Mathf.Sin(2f * Mathf.PI * 1850f * t) * Mathf.Exp(-t * 120f);
            float body = Mathf.Sin(2f * Mathf.PI * 340f * t) * Mathf.Exp(-t * 50f);
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 90f) * 0.35f;

            samples[i] = Mathf.Clamp((snap * 0.7f + body * 0.4f + noise) * 0.9f, -1f, 1f);
        }

        switchClickClip = AudioClip.Create("LightSwitch_Click", sampleCount, 1, sampleRate, false);
        switchClickClip.SetData(samples, 0);
    }
}

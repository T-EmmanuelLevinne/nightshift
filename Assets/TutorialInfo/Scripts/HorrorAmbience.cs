using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class HorrorAmbience : MonoBehaviour
{
    [Header("Ambience Settings")]
    public AudioClip ambienceClip;
    [Range(0f, 1f)]
    public float volume = 0.70f;
    public bool playOnStart = true;

    [Header("Procedural Fallback & Layering")]
    [Tooltip("Synthesize realistic commercial cooler hum, 60Hz fluorescent buzz, and sinister dread drone via DSP")]
    public bool enableProceduralFallback = true;
    [Tooltip("Layer cooler drone and sinister sub-bass even when an ambient WAV is playing")]
    public bool layerWithAudioClip = true;

    [Header("Atmosphere Tuning")]
    [Range(0f, 1f)]
    public float refrigeratorHumLevel = 0.55f;
    [Range(0f, 1f)]
    public float subBassDreadLevel = 0.45f;
    [Range(0f, 1f)]
    public float hollowWindLevel = 0.35f;

    private AudioSource audioSource;
    private bool isPlayingProcedural = false;
    private volatile bool hasActiveClip = false;

    // Real-time procedural DSP state
    private float sampleRate = 48000f;
    private float timeIndex = 0f;
    private float b0 = 0f, b1 = 0f, b2 = 0f;
    private float compressorPhase = 0f;
    private uint audioRngState = 88675123;

    private void Awake()
    {
        SetupAudioSource();
    }

    private void Start()
    {
        SetupAudioSource();

        sampleRate = AudioSettings.outputSampleRate;
        if (sampleRate <= 0) sampleRate = 48000f;

#if UNITY_EDITOR
        if (ambienceClip == null)
        {
            ambienceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/HorrorAmbience_GasStation.wav");
        }
#endif

        if (ambienceClip != null)
        {
            audioSource.clip = ambienceClip;
            audioSource.loop = true;
            hasActiveClip = true;
            if (playOnStart)
            {
                audioSource.Play();
            }
        }
        else
        {
            hasActiveClip = false;
            isPlayingProcedural = enableProceduralFallback;
            if (isPlayingProcedural && playOnStart && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
    }

    private void Update()
    {
        hasActiveClip = (audioSource != null && audioSource.clip != null && audioSource.isPlaying);
    }

    private void SetupAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        audioSource.loop = true;
        audioSource.playOnAwake = playOnStart;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f; // 2D immersion across both ears
        audioSource.priority = 10; // High priority so ambient never cuts out
    }

    private void OnValidate()
    {
        if (audioSource != null)
        {
            audioSource.volume = volume;
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        // If playing an external audio clip and not layering, skip DSP
        if (hasActiveClip && !layerWithAudioClip)
        {
            return;
        }

        if (!isPlayingProcedural && !layerWithAudioClip) return;

        float dt = 1f / sampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            timeIndex += dt;

            // 1. Commercial Refrigerator Cooler Drone & Compressor Thrum
            // Heavy 60Hz fundamental + 120Hz secondary + 180Hz 3rd harmonic
            float humPhase = 2f * Mathf.PI * 60f * timeIndex;
            float fridgeTone = (Mathf.Sin(humPhase) * 0.40f +
                               Mathf.Sin(humPhase * 2f) * 0.30f +
                               Mathf.Sin(humPhase * 3f) * 0.15f);

            // Subtle 8Hz compressor mechanical thrum
            compressorPhase += dt * 8.2f;
            float thrum = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * compressorPhase);
            float coolerSound = fridgeTone * thrum * (refrigeratorHumLevel * 0.28f);

            // 2. Sinister Sub-Bass Dread Drone (38Hz - 44Hz slowly oscillating)
            float subPitchMod = 40f + 3f * Mathf.Sin(2f * Mathf.PI * 0.08f * timeIndex);
            float subPhase = 2f * Mathf.PI * subPitchMod * timeIndex;
            float subDrone = Mathf.Sin(subPhase) * (subBassDreadLevel * 0.30f);

            // 3. Hollow Night Wind & Subtle Refrigerant Hiss (Pink/Brown filtered noise via thread-safe Xorshift PRNG)
            audioRngState ^= audioRngState << 13;
            audioRngState ^= audioRngState >> 17;
            audioRngState ^= audioRngState << 5;
            float white = ((audioRngState & 0x7FFFFFFF) / 1073741824.0f) - 1.0f;
            b0 = 0.992f * b0 + white * 0.045f;
            b1 = 0.985f * b1 + b0 * 0.035f;
            b2 = 0.965f * b2 + b1 * 0.025f;

            // Modulated hollow wind gusting
            float windMod = 0.18f + 0.14f * Mathf.Sin(2f * Mathf.PI * 0.12f * timeIndex);
            float windSound = b2 * windMod * (hollowWindLevel * 1.2f);

            // Combine ambient layers
            float sample = (coolerSound + subDrone + windSound) * volume;

            for (int ch = 0; ch < channels; ch++)
            {
                data[i + ch] += sample;
            }
        }
    }
}

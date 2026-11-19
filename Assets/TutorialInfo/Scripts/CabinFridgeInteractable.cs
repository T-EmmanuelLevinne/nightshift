using System.Collections;
using UnityEngine;

/// <summary>
/// Controls interactive refrigerator in HouseScene cabin.
/// Handles smooth door swinging on right hinge, interior warm illumination,
/// tactile suction seal / magnetic gasket audio, and reticle prompts ("E to Open Fridge" / "E to Close Fridge").
/// </summary>
public class CabinFridgeInteractable : MonoBehaviour, IInteractable
{
    public static CabinFridgeInteractable Instance { get; private set; }

    public static CabinFridgeInteractable ResolveInstance()
    {
        if (Instance == null)
        {
            Instance = Object.FindAnyObjectByType<CabinFridgeInteractable>();
        }
        return Instance;
    }

    [Header("Door Pivot & Motion")]
    public Transform doorPivot;
    public bool isOpen = false;
    public float closedAngleY = 0f;
    public float openAngleY = -105f; // Swings wide open to the right
    public float swingSpeed = 3.2f;

    [Header("Interior Lighting")]
    public Light interiorLight;
    public float maxLightIntensity = 1.6f;
    public float lightFadeSpeed = 6.0f;

    [Header("Audio")]
    public AudioSource audioSource;
    private AudioClip suctionPopOpenClip;
    private AudioClip doorSwingClip;
    private AudioClip magneticLatchCloseClip;
    private Coroutine latchCoroutine;

    private const float MAX_INTERACT_DISTANCE = 2.6f;

    private void Awake()
    {
        Instance = this;
        if (doorPivot == null)
        {
            Transform foundPivot = transform.Find("Door_Pivot");
            if (foundPivot != null) doorPivot = foundPivot;
        }

        if (interiorLight == null)
        {
            interiorLight = GetComponentInChildren<Light>(true);
        }

        SetupAudioSource();
        GenerateProceduralAudioClips();

        // Apply initial state
        float startAngle = isOpen ? openAngleY : closedAngleY;
        if (doorPivot != null)
        {
            doorPivot.localRotation = Quaternion.Euler(0f, startAngle, 0f);
        }

        if (interiorLight != null)
        {
            interiorLight.intensity = isOpen ? maxLightIntensity : 0f;
            interiorLight.enabled = isOpen;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // 1. Smoothly swing door hinge toward target rotation
        if (doorPivot != null)
        {
            float targetAngle = isOpen ? openAngleY : closedAngleY;
            Quaternion targetRot = Quaternion.Euler(0f, targetAngle, 0f);
            doorPivot.localRotation = Quaternion.Slerp(doorPivot.localRotation, targetRot, Time.deltaTime * swingSpeed);
        }

        // 2. Smoothly fade interior light
        if (interiorLight != null)
        {
            float targetIntensity = isOpen ? maxLightIntensity : 0f;
            if (isOpen && !interiorLight.enabled) interiorLight.enabled = true;

            interiorLight.intensity = Mathf.MoveTowards(interiorLight.intensity, targetIntensity, Time.deltaTime * lightFadeSpeed);

            if (!isOpen && interiorLight.intensity <= 0.01f && interiorLight.enabled)
            {
                interiorLight.enabled = false;
            }
        }
    }

    public void ToggleFridge()
    {
        isOpen = !isOpen;

        if (!isOpen && HouseSceneStoryController.HasPutFoodBackInFridge)
        {
            HouseSceneStoryController.NotifyFridgeClosedAfterFoodReturn();
        }

        if (latchCoroutine != null)
        {
            StopCoroutine(latchCoroutine);
            latchCoroutine = null;
        }

        if (audioSource != null)
        {
            audioSource.pitch = Random.Range(0.97f, 1.03f);

            if (isOpen)
            {
                // Play realistic suction seal pop immediately on opening
                if (suctionPopOpenClip != null)
                {
                    audioSource.PlayOneShot(suctionPopOpenClip, 0.55f);
                }

                if (HouseSceneStoryController.Instance != null)
                {
                    HouseSceneStoryController.Instance.NotifyFridgeOpened();
                }
            }
            else
            {
                // Play gentle door swing glide on close start
                if (doorSwingClip != null)
                {
                    audioSource.PlayOneShot(doorSwingClip, 0.35f);
                }

                // Play tactile magnetic gasket thunk when reaching closed frame (~0.65s)
                latchCoroutine = StartCoroutine(PlayDelayedLatch(0.65f));
            }
        }
    }

    private IEnumerator PlayDelayedLatch(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!isOpen && audioSource != null && magneticLatchCloseClip != null)
        {
            audioSource.pitch = Random.Range(0.98f, 1.02f);
            audioSource.PlayOneShot(magneticLatchCloseClip, 0.60f);
        }
        latchCoroutine = null;
    }

    // IInteractable implementation
    public void Interact()
    {
        if (HouseSceneStoryController.IsDialogueActive) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > MAX_INTERACT_DISTANCE)
        {
            return;
        }

        if (!isOpen && !HouseSceneStoryController.IsFridgeUnlocked)
        {
            return;
        }

        ToggleFridge();
    }

    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > MAX_INTERACT_DISTANCE)
        {
            return string.Empty;
        }

        if (!isOpen && !HouseSceneStoryController.IsFridgeUnlocked)
        {
            return "Locked";
        }

        return isOpen ? "E to Close Fridge" : "E to Open Fridge";
    }

    private void SetupAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1.0f; // Full 3D spatial sound
        audioSource.minDistance = 1.0f;
        audioSource.maxDistance = 12.0f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    private void GenerateProceduralAudioClips()
    {
        int sampleRate = 44100;

        // 1. Refrigerator suction seal open sound ("psshh-pop" + soft gasket release)
        float openDur = 0.42f;
        int openSamples = Mathf.RoundToInt(openDur * sampleRate);
        float[] openData = new float[openSamples];

        for (int i = 0; i < openSamples; i++)
        {
            float t = (float)i / sampleRate;

            // Low-frequency air pressure pop (55Hz decaying quickly)
            float pop = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 28f) * 0.45f;

            // Pneumatic suction release hiss (filtered noise burst during first 0.25s)
            float hissEnv = Mathf.Clamp01(Mathf.Sin((t / 0.28f) * Mathf.PI));
            float noise = (Random.value * 2f - 1f) * hissEnv * 0.22f;

            // Soft rubber seal peel tick
            float tick = Mathf.Sin(2f * Mathf.PI * 480f * t) * Mathf.Exp(-t * 60f) * 0.18f;

            openData[i] = Mathf.Clamp(pop + noise + tick, -1f, 1f);
        }

        suctionPopOpenClip = AudioClip.Create("Fridge_SuctionPopOpen", openSamples, 1, sampleRate, false);
        suctionPopOpenClip.SetData(openData, 0);

        // 2. Door swing glide (soft air displacement)
        float swingDur = 0.50f;
        int swingSamples = Mathf.RoundToInt(swingDur * sampleRate);
        float[] swingData = new float[swingSamples];

        for (int i = 0; i < swingSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / swingDur;

            float tone = Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.10f;
            float softAir = (Random.value * 2f - 1f) * 0.05f;
            float env = Mathf.Sin(progress * Mathf.PI);

            swingData[i] = Mathf.Clamp((tone + softAir) * env * 0.30f, -1f, 1f);
        }

        doorSwingClip = AudioClip.Create("Fridge_DoorSwingGlide", swingSamples, 1, sampleRate, false);
        doorSwingClip.SetData(swingData, 0);

        // 3. Magnetic gasket latch thunk ("thud-click" when door meets frame)
        float latchDur = 0.22f;
        int latchSamples = Mathf.RoundToInt(latchDur * sampleRate);
        float[] latchData = new float[latchSamples];

        for (int i = 0; i < latchSamples; i++)
        {
            float t = (float)i / sampleRate;

            // Deep cabinet body thump (80Hz dampened)
            float bodyThump = Mathf.Sin(2f * Mathf.PI * 80f * t) * Mathf.Exp(-t * 32f) * 0.50f;

            // Soft rubber magnetic seal impact (180Hz)
            float sealImpact = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 55f) * 0.35f;

            // Crisp magnetic contact click (650Hz)
            float magnetClick = Mathf.Sin(2f * Mathf.PI * 650f * t) * Mathf.Exp(-t * 90f) * 0.22f;

            latchData[i] = Mathf.Clamp(bodyThump + sealImpact + magnetClick, -1f, 1f);
        }

        magneticLatchCloseClip = AudioClip.Create("Fridge_MagneticLatchClose", latchSamples, 1, sampleRate, false);
        magneticLatchCloseClip.SetData(latchData, 0);
    }
}


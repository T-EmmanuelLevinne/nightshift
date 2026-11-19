using UnityEngine;

public class InteractableRestroomDoor : MonoBehaviour, IInteractable
{
    [Header("Door Settings")]
    public string doorLabel = "Restroom Door";
    public bool isOpen = false;
    public float closedAngleY = 0f;
    public float openAngleY = 75f;
    public float swingSpeed = 2.8f;

    [Header("Act 4 Restroom Lock & Investigation")]
    public bool isLockedByJohnAct4 = false;
    public bool isUnlockedWithKey = false;
    public bool hasTriggeredAct4MurderReveal = false;
    public bool isLockedInStockroomAct4 = false;
    public bool isArmedForStockroomExitJumpscare = false;

    private AudioSource audioSource;
    private AudioClip doorClickClip;
    private AudioClip doorKnockClip;
    private AudioClip doorUnlockClip;
    private AudioClip doorSlamClip;
    private Collider[] doorColliders;
    private int lastToggleFrame = -1;
    private float nextToggleAllowedTime = 0f;

    public bool IsMaleRestroomDoor =>
        gameObject.name.IndexOf("Male", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
        (doorLabel != null && doorLabel.IndexOf("Male", System.StringComparison.OrdinalIgnoreCase) >= 0);

    public bool IsStockroomDoor => !IsMaleRestroomDoor;

    public void SlamShutAndLockForStockroomSequence()
    {
        isOpen = false;
        isLockedInStockroomAct4 = true;
        swingSpeed = 14.0f;
        if (audioSource != null)
        {
            AudioClip clip = doorSlamClip != null ? doorSlamClip : doorClickClip;
            if (clip != null)
            {
                audioSource.pitch = 0.78f;
                audioSource.PlayOneShot(clip, 1.0f);
            }
        }
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.85f;
            audioSource.volume = 0.7f;
        }

        if (transform.parent != null &&
            transform.parent.name.IndexOf("Stock", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            doorLabel = "Stockroom Door";
            if (gameObject.name.IndexOf("Female", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                gameObject.name = "Stockroom_DoorPivot";
            }
            Transform leaf = transform.Find("Female_Restroom_Door");
            if (leaf != null) leaf.name = "Stockroom_Door";
        }

        // Add a generous trigger collider on the pivot itself that moves with the door leaf
        // so raycasts easily hit the door at any open/closed angle without obstructing movement
        BoxCollider pivotTrigger = GetComponent<BoxCollider>();
        if (pivotTrigger == null)
        {
            pivotTrigger = gameObject.AddComponent<BoxCollider>();
        }
        pivotTrigger.isTrigger = true;
        pivotTrigger.center = new Vector3(0f, 1.05f, 0.45f);
        pivotTrigger.size = new Vector3(0.32f, 2.10f, 0.96f);

        doorColliders = GetComponentsInChildren<Collider>(true);
        GenerateProceduralDoorSound();
    }

    private void Start()
    {
        float targetY = isOpen ? openAngleY : closedAngleY;
        transform.localRotation = Quaternion.Euler(0f, targetY, 0f);
    }

    private void Update()
    {
        // Smoothly rotate door hinge pivot toward target open/closed angle
        float targetY = isOpen ? openAngleY : closedAngleY;
        Quaternion targetRot = Quaternion.Euler(0f, targetY, 0f);
        if (Quaternion.Angle(transform.localRotation, targetRot) > 0.05f)
        {
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * swingSpeed);
            Physics.SyncTransforms();
        }
        else if (transform.localRotation != targetRot)
        {
            transform.localRotation = targetRot;
            Physics.SyncTransforms();
        }
    }

    public void ToggleDoor()
    {
        if (lastToggleFrame == Time.frameCount || Time.unscaledTime < nextToggleAllowedTime)
        {
            return;
        }

        lastToggleFrame = Time.frameCount;
        nextToggleAllowedTime = Time.unscaledTime + 0.45f;
        isOpen = !isOpen;

        if (audioSource != null && doorClickClip != null)
        {
            audioSource.pitch = isOpen ? 1.05f : 0.95f;
            audioSource.PlayOneShot(doorClickClip, 0.75f);
        }
    }

    public void PlayKnockSound()
    {
        if (audioSource != null && doorKnockClip != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.04f);
            audioSource.PlayOneShot(doorKnockClip, 0.90f);
        }
    }

    private static bool IsPlayerHoldingMultipurposeKey()
    {
        if (HorrorHotbar.Instance == null) return false;
        string activeItem = HorrorHotbar.Instance.GetItemName(HorrorHotbar.Instance.activeSlotIndex);
        return !string.IsNullOrEmpty(activeItem) &&
               (string.Equals(activeItem, "KEY", System.StringComparison.OrdinalIgnoreCase) ||
                activeItem.IndexOf("Key", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    // IInteractable implementation
    public void Interact()
    {
        if (NightShiftStoryController.IsDialogueActive ||
            !HorrorInteractionReticle.IsCameraWithinColliderReach(doorColliders, transform.position, 2.15f, horizontalFallback: true))
        {
            return;
        }

        if (IsMaleRestroomDoor)
        {
            if (NightShiftStoryController.IsAct4KnockRestroomTaskActive && !NightShiftStoryController.HasAct4KnockedRestroomDoor)
            {
                PlayKnockSound();
                if (NightShiftStoryController.Instance != null)
                {
                    NightShiftStoryController.Instance.NotifyAct4RestroomDoorKnocked(this);
                }
                return;
            }

            if (isLockedByJohnAct4 && !isUnlockedWithKey)
            {
                if (IsPlayerHoldingMultipurposeKey())
                {
                    isUnlockedWithKey = true;
                    isLockedByJohnAct4 = false;
                    if (audioSource != null && doorUnlockClip != null)
                    {
                        audioSource.pitch = 1.0f;
                        audioSource.PlayOneShot(doorUnlockClip, 0.90f);
                    }
                    if (HorrorHotbar.Instance != null)
                    {
                        HorrorHotbar.Instance.ClearItem("KEY");
                    }
                }
                else
                {
                    if (audioSource != null && doorClickClip != null)
                    {
                        audioSource.pitch = 0.72f;
                        audioSource.PlayOneShot(doorClickClip, 0.55f);
                    }
                }
                return;
            }

            if (isUnlockedWithKey && !hasTriggeredAct4MurderReveal && !isOpen)
            {
                hasTriggeredAct4MurderReveal = true;
                ToggleDoor();
                if (NightShiftStoryController.Instance != null)
                {
                    NightShiftStoryController.Instance.NotifyAct4RestroomDoorOpened(this);
                }
                return;
            }
        }
        else
        {
            if (isLockedInStockroomAct4)
            {
                if (audioSource != null && doorClickClip != null)
                {
                    audioSource.pitch = 0.70f;
                    audioSource.PlayOneShot(doorClickClip, 0.60f);
                }
                return;
            }

            if (isArmedForStockroomExitJumpscare && !isOpen)
            {
                isArmedForStockroomExitJumpscare = false;
                swingSpeed = 3.2f;
                isOpen = true;
                if (audioSource != null && doorClickClip != null)
                {
                    audioSource.pitch = 1.05f;
                    audioSource.PlayOneShot(doorClickClip, 0.85f);
                }
                if (NightShiftStoryController.Instance != null)
                {
                    NightShiftStoryController.Instance.NotifyAct4StockroomExitDoorOpened(this);
                }
                return;
            }
        }

        ToggleDoor();
    }

    public string GetPromptText()
    {
        if (NightShiftStoryController.IsDialogueActive ||
            !HorrorInteractionReticle.IsCameraWithinColliderReach(doorColliders, transform.position, 2.15f, horizontalFallback: true))
        {
            return string.Empty;
        }

        if (IsMaleRestroomDoor)
        {
            if (NightShiftStoryController.IsAct4KnockRestroomTaskActive && !NightShiftStoryController.HasAct4KnockedRestroomDoor)
            {
                return "E to Knock";
            }

            if (isLockedByJohnAct4 && !isUnlockedWithKey)
            {
                if (IsPlayerHoldingMultipurposeKey())
                {
                    return "E to Unlock";
                }
                return "Locked";
            }
        }
        else if (isLockedInStockroomAct4)
        {
            return "Locked";
        }

        return isOpen ? "E to Close" : "E to Open";
    }

    private void GenerateProceduralDoorSound()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.22f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 25f);
            float latchTone = Mathf.Sin(2f * Mathf.PI * 420f * t) * 0.4f;
            float handleTone = Mathf.Sin(2f * Mathf.PI * 260f * t) * 0.35f;
            float creak = Mathf.Sin(2f * Mathf.PI * (120f + 60f * Mathf.Sin(t * 80f)) * t) * 0.25f;
            float click = Random.Range(-0.25f, 0.25f) * Mathf.Exp(-t * 50f);

            samples[i] = (latchTone + handleTone + creak + click) * envelope * 0.8f;
        }

        doorClickClip = AudioClip.Create("DoorLatchClick", sampleCount, 1, sampleRate, false);
        doorClickClip.SetData(samples, 0);

        // Triple wooden door knock
        int knockSamples = (int)(sampleRate * 0.72f);
        float[] knockBuf = new float[knockSamples];
        float[] hitTimes = { 0.02f, 0.24f, 0.46f };
        for (int i = 0; i < knockSamples; i++)
        {
            float t = (float)i / sampleRate;
            float s = 0f;
            for (int k = 0; k < hitTimes.Length; k++)
            {
                float dt = t - hitTimes[k];
                if (dt >= 0f && dt < 0.18f)
                {
                    float thud = Mathf.Sin(2f * Mathf.PI * (135f - dt * 190f) * dt) * Mathf.Exp(-dt * 30f) * 0.75f;
                    float wood = Mathf.Sin(2f * Mathf.PI * 280f * dt) * Mathf.Exp(-dt * 45f) * 0.40f;
                    s += thud + wood;
                }
            }
            knockBuf[i] = Mathf.Clamp(s, -1f, 1f);
        }
        doorKnockClip = AudioClip.Create("RestroomDoorKnock", knockSamples, 1, sampleRate, false);
        doorKnockClip.SetData(knockBuf, 0);

        // Metallic key unlock click
        int unlockSamples = (int)(sampleRate * 0.28f);
        float[] unlockBuf = new float[unlockSamples];
        for (int i = 0; i < unlockSamples; i++)
        {
            float t = (float)i / sampleRate;
            float c1 = (t < 0.12f) ? Mathf.Sin(2f * Mathf.PI * 1680f * t) * Mathf.Exp(-t * 45f) : 0f;
            float c2 = (t >= 0.12f) ? Mathf.Sin(2f * Mathf.PI * 920f * (t - 0.12f)) * Mathf.Exp(-(t - 0.12f) * 35f) : 0f;
            unlockBuf[i] = (c1 + c2) * 0.65f;
        }
        doorUnlockClip = AudioClip.Create("RestroomDoorUnlock", unlockSamples, 1, sampleRate, false);
        doorUnlockClip.SetData(unlockBuf, 0);

        // Heavy door slam behind player
        int slamSamples = (int)(sampleRate * 0.48f);
        float[] slamBuf = new float[slamSamples];
        for (int i = 0; i < slamSamples; i++)
        {
            float t = (float)i / sampleRate;
            float thump = Mathf.Sin(2f * Mathf.PI * (92f - t * 110f) * t) * Mathf.Exp(-t * 14f) * 0.85f;
            float crack = (Random.value * 2f - 1f) * Mathf.Exp(-t * 42f) * 0.65f;
            float rattle = Mathf.Sin(2f * Mathf.PI * 310f * t) * Mathf.Exp(-t * 24f) * 0.30f;
            slamBuf[i] = Mathf.Clamp(thump + crack + rattle, -1f, 1f);
        }
        doorSlamClip = AudioClip.Create("StockroomDoorSlam", slamSamples, 1, sampleRate, false);
        doorSlamClip.SetData(slamBuf, 0);
    }
}

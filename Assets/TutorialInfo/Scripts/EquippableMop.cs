using System.Collections.Generic;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EquippableMop : MonoBehaviour, IInteractable
{
    [Header("Prompt Settings")]
    [SerializeField] private string promptText = "E to equip";

    [Header("Equipped Viewmodel Transform (Handle Grip)")]
    [SerializeField] private Vector3 viewmodelOffset = new Vector3(0.24f, -0.22f, 0.44f);
    [SerializeField] private Vector3 viewmodelRotation = new Vector3(-65f, -15f, 5f);
    [SerializeField] private float viewmodelScale = 0.70f;

    [Header("Mopping Settings")]
    public float mopDuration = 2.0f;

    [Header("Audio")]
    private AudioSource audioSource;
    private AudioClip squelchClip;
    private AudioClip putBackClip;

    private bool isEquipped = false;
    private bool isMopping = false;
    private Transform playerCameraTransform;
    private Collider[] allColliders;
    private Renderer[] mopRenderers;

    // Saved original world transform to return mop where it came from
    private Vector3 originalWorldPos;
    private Quaternion originalWorldRot;
    private Vector3 originalWorldScale;

    public static EquippableMop Instance { get; private set; }
    public bool IsEquipped => isEquipped;
    public bool IsMopping => isMopping;
    public static bool IsPlayerMopping => Instance != null && Instance.isMopping;
    public Vector3 OriginalWorldPos => originalWorldPos;
    public Quaternion OriginalWorldRot => originalWorldRot;

    private void Awake()
    {
        Instance = this;

        allColliders = GetComponentsInChildren<Collider>(true);
        mopRenderers = GetComponentsInChildren<Renderer>(true);

        // Record initial spawn transform where the mop came from
        originalWorldPos = transform.position;
        originalWorldRot = transform.rotation;
        originalWorldScale = transform.lossyScale;

        // Fallback default if scene was zeroed
        if (originalWorldPos.sqrMagnitude < 0.01f)
        {
            originalWorldPos = new Vector3(1.879f, 1.2f, 2.475f);
            originalWorldRot = Quaternion.Euler(10.803f, -126.171f, -0.735f);
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.5f;
        audioSource.volume = 0.75f;

        GenerateProceduralMopAudio();
        GenerateProceduralPutBackAudio();
    }

    private void Start()
    {
        // Subscribe to hotbar slot change events
        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null)
        {
            hotbar.OnSlotChanged -= HandleHotbarSlotChanged;
            hotbar.OnSlotChanged += HandleHotbarSlotChanged;
        }
    }

    public bool IsNearOriginalSpot(float maxDist = 1.35f)
    {
        Transform pCam = playerCameraTransform;
        if (pCam == null && HorrorInteractionReticle.CurrentPlayerCamera != null)
        {
            pCam = HorrorInteractionReticle.CurrentPlayerCamera.transform;
        }
        if (pCam == null)
        {
            Camera cam = HorrorInteractionReticle.GetPlayerCamera();
            if (cam != null) pCam = cam.transform;
        }
        if (pCam == null) return false;

        Vector3 pPos = pCam.position;

        // Strictly require player to be physically inside the Stockroom walls:
        // Dividing wall between Male Restroom & Stockroom is at worldX = [1.63, 1.79]
        // Partition wall between Main Store & Stockroom is at worldZ = [2.00, 2.16]
        if (pPos.x < 1.78f || pPos.x > 5.05f || pPos.z < 2.15f || pPos.z > 6.88f)
        {
            return false;
        }

        float horizontalDist = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(originalWorldPos.x, originalWorldPos.z));
        float verticalDist = Mathf.Abs(pPos.y - originalWorldPos.y);

        return horizontalDist <= maxDist && verticalDist <= 2.2f;
    }

    private void Update()
    {
        if (!isEquipped) return;
        if (NightShiftStoryController.IsDialogueActive) return;

        // When player is near the return spot (where the mop came from) and not actively mopping, pressing Q returns it
        if (!isMopping && !FloorStain.IsAnyStainBeingMopped && IsNearOriginalSpot() && HorrorInteractionReticle.WasQPressed())
        {
            PutBack();
            return;
        }

        bool isSlot0Active = (HorrorHotbar.Instance == null || HorrorHotbar.Instance.IsSlotActive(0));

        // Listen for mouse click to trigger dramatic mopping or clean a nearby floor stain
        if (isSlot0Active && HorrorInteractionReticle.WasMouseClicked() && !isMopping)
        {
            FloorStain stain = null;
            if (HorrorInteractionReticle.CurrentInteractable is FloorStain directStain && !directStain.IsCleaned)
            {
                stain = directStain;
            }
            else
            {
                stain = FloorStain.GetNearestUncleanedStain(playerCameraTransform != null ? playerCameraTransform.position : transform.position, 1.75f);
            }

            if (stain != null)
            {
                stain.StartCleaning();
            }
            else
            {
                TriggerDramaticMop(mopDuration);
            }
        }
    }

    /// <summary>
    /// LateUpdate ensures the mop viewmodel is locked rigidly to the camera after
    /// mouse look and player movement have completed. This completely eliminates shaking/jitter!
    /// </summary>
    private void LateUpdate()
    {
        if (!isEquipped) return;

        bool isSlot0Active = (HorrorHotbar.Instance == null || HorrorHotbar.Instance.IsSlotActive(0));

        // When Slot 1 is active and player is not actively sweeping, lock rigidly to camera
        if (isSlot0Active && !isMopping)
        {
            transform.localPosition = viewmodelOffset;
            transform.localRotation = Quaternion.Euler(viewmodelRotation);
        }
    }

    /// <summary>
    /// Return the mop back to the exact spot where it came from in the store.
    /// </summary>
    public void PutBack()
    {
        if (!isEquipped) return;

        if (isMopping)
        {
            StopAllCoroutines();
            isMopping = false;
        }

        isEquipped = false;

        // Unparent and return to exact original spot
        transform.SetParent(null, true);
        transform.position = originalWorldPos;
        transform.rotation = originalWorldRot;
        transform.localScale = Vector3.one;

        // Re-enable world colliders
        if (allColliders != null)
        {
            for (int i = 0; i < allColliders.Length; i++)
            {
                if (allColliders[i] != null) allColliders[i].enabled = true;
            }
        }

        // Ensure renderers are visible in the world
        SetRenderersVisible(true);

        // Clear Slot 1 on hotbar
        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.ClearSlot(0);
        }

        PlayPutBackSound();
    }

    // --- IInteractable Implementation ---
    public string GetPromptText()
    {
        if (isMopping || FloorStain.IsAnyStainBeingMopped || NightShiftStoryController.IsDialogueActive)
        {
            return string.Empty;
        }

        if (isEquipped)
        {
            return IsNearOriginalSpot() ? "Q to return" : string.Empty;
        }

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > 1.75f)
        {
            return string.Empty;
        }

        return promptText;
    }

    public void Interact()
    {
        if (isEquipped)
        {
            if (IsNearOriginalSpot())
            {
                PutBack();
            }
            return;
        }

        // Obtain active player camera directly from HorrorInteractionReticle
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null)
        {
            cam = HorrorInteractionReticle.GetPlayerCamera();
        }

        if (cam != null)
        {
            if (Vector3.Distance(cam.transform.position, transform.position) > 1.75f) return;
            EquipToPlayer(cam.transform);
        }
        else
        {
            Debug.LogError("[EquippableMop] Could not find active player camera to equip mop to!");
        }
    }

    public void EquipToPlayer(Transform cameraTransform)
    {
        if (!isEquipped && transform.parent == null)
        {
            originalWorldPos = transform.position;
            originalWorldRot = transform.rotation;
            originalWorldScale = transform.lossyScale;
        }

        isEquipped = true;
        playerCameraTransform = cameraTransform;

        // Disable all world colliders so physics cannot push or jitter it
        if (allColliders != null)
        {
            for (int i = 0; i < allColliders.Length; i++)
            {
                if (allColliders[i] != null) allColliders[i].enabled = false;
            }
        }

        // Parent to player camera
        transform.SetParent(playerCameraTransform, false);
        transform.localPosition = viewmodelOffset;
        transform.localRotation = Quaternion.Euler(viewmodelRotation);
        transform.localScale = Vector3.one * viewmodelScale;

        // Put into Slot 1 of the hotbar
        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null)
        {
            hotbar.OnSlotChanged -= HandleHotbarSlotChanged;
            hotbar.OnSlotChanged += HandleHotbarSlotChanged;
            hotbar.SetSlot(0, "MOP", null);

            // If player is currently on Slot 1, show mop in hand.
            // If player is on Slot 2 or 3 (empty-handed), mop stays holstered until Slot 1 is chosen!
            bool isSlot0Active = hotbar.IsSlotActive(0);
            SetRenderersVisible(isSlot0Active);
        }
        else
        {
            SetRenderersVisible(true);
        }

        PlaySquelchSound(0.85f);
    }

    private void HandleHotbarSlotChanged(int newSlot)
    {
        if (!isEquipped) return;

        // Only show mop mesh if Slot 1 (index 0) is active.
        // If player selects Slot 2 or 3, player is empty-handed!
        bool isThisSlot = (newSlot == 0);
        SetRenderersVisible(isThisSlot);

        if (isThisSlot)
        {
            transform.localPosition = viewmodelOffset;
            transform.localRotation = Quaternion.Euler(viewmodelRotation);
        }
    }

    private void SetRenderersVisible(bool visible)
    {
        if (mopRenderers == null) return;
        for (int i = 0; i < mopRenderers.Length; i++)
        {
            if (mopRenderers[i] != null)
            {
                mopRenderers[i].enabled = visible;
            }
        }
    }

    public void TriggerDramaticMop(float duration = -1f)
    {
        if (duration > 0f) mopDuration = duration;
        if (!isMopping)
        {
            StartCoroutine(DramaticMoppingRoutine());
        }
    }

    private IEnumerator DramaticMoppingRoutine()
    {
        isMopping = true;
        float elapsed = 0f;

        PlaySquelchSound(1.0f);

        // 5-Phase Mopping Choreography across floor:
        // Phase 1 (0.00 - 0.20): Prepare & lower mop head firmly onto the floor
        // Phase 2 (0.20 - 0.48): Dramatic wide sweep to the LEFT across the floor
        // Phase 3 (0.48 - 0.76): Violent snap sweep to the RIGHT across the floor
        // Phase 4 (0.76 - 0.88): Rapid aggressive scrub jitter on the ground
        // Phase 5 (0.88 - 1.00): Smooth recovery back up to ready stance

        while (elapsed < mopDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / mopDuration);

            Vector3 targetPos;
            Quaternion targetRot;

            if (progress < 0.20f)
            {
                float t = progress / 0.20f;
                float ease = Mathf.SmoothStep(0f, 1f, t);
                targetPos = Vector3.Lerp(viewmodelOffset, new Vector3(0.18f, -0.26f, 0.46f), ease);
                targetRot = Quaternion.Slerp(Quaternion.Euler(viewmodelRotation), Quaternion.Euler(-68f, -12f, 2f), ease);
            }
            else if (progress < 0.48f)
            {
                if (elapsed - Time.deltaTime < mopDuration * 0.20f) PlaySquelchSound(0.92f);
                float t = (progress - 0.20f) / 0.28f;
                float ease = Mathf.SmoothStep(0f, 1f, t);
                targetPos = Vector3.Lerp(new Vector3(0.18f, -0.26f, 0.46f), new Vector3(-0.06f, -0.22f, 0.48f), ease);
                targetRot = Quaternion.Slerp(Quaternion.Euler(-68f, -12f, 2f), Quaternion.Euler(-52f, 38f, -35f), ease);
            }
            else if (progress < 0.76f)
            {
                if (elapsed - Time.deltaTime < mopDuration * 0.48f) PlaySquelchSound(1.12f);
                float t = (progress - 0.48f) / 0.28f;
                float ease = Mathf.SmoothStep(0f, 1f, t);
                targetPos = Vector3.Lerp(new Vector3(-0.06f, -0.22f, 0.48f), new Vector3(0.38f, -0.22f, 0.42f), ease);
                targetRot = Quaternion.Slerp(Quaternion.Euler(-52f, 38f, -35f), Quaternion.Euler(-58f, -45f, 30f), ease);
            }
            else if (progress < 0.88f)
            {
                if (elapsed - Time.deltaTime < mopDuration * 0.76f) PlaySquelchSound(1.05f);
                float t = (progress - 0.76f) / 0.12f;
                float jitterX = Mathf.Sin(t * Mathf.PI * 10f) * 0.02f;
                float jitterZ = Mathf.Cos(t * Mathf.PI * 10f) * 0.025f;
                targetPos = new Vector3(0.20f + jitterX, -0.25f, 0.46f + jitterZ);
                targetRot = Quaternion.Euler(-68f, -15f, 5f);
            }
            else
            {
                float t = (progress - 0.88f) / 0.12f;
                float ease = Mathf.SmoothStep(0f, 1f, t);
                targetPos = Vector3.Lerp(new Vector3(0.20f, -0.25f, 0.46f), viewmodelOffset, ease);
                targetRot = Quaternion.Slerp(Quaternion.Euler(-68f, -15f, 5f), Quaternion.Euler(viewmodelRotation), ease);
            }

            transform.localPosition = targetPos;
            transform.localRotation = targetRot;

            yield return null;
        }

        // Return rigidly to ready position
        transform.localPosition = viewmodelOffset;
        transform.localRotation = Quaternion.Euler(viewmodelRotation);

        isMopping = false;
    }

    private void PlaySquelchSound(float pitch)
    {
        if (audioSource != null && squelchClip != null)
        {
            audioSource.pitch = pitch + Random.Range(-0.08f, 0.08f);
            audioSource.PlayOneShot(squelchClip, 0.65f);
        }
    }

    private void PlayPutBackSound()
    {
        if (audioSource != null && putBackClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.08f);
            audioSource.PlayOneShot(putBackClip, 0.85f);
        }
    }

    private void GenerateProceduralMopAudio()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.45f);
        float[] samples = new float[sampleCount];

        float filterState = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Sin(t / 0.45f * Mathf.PI);

            float white = Random.Range(-1f, 1f);
            filterState += 0.12f * (white - filterState);
            float bubble = Mathf.Sin(2f * Mathf.PI * (160f + 80f * Mathf.Sin(t * 35f)) * t) * 0.35f;

            samples[i] = (filterState * 0.75f + bubble * 0.25f) * envelope * 0.7f;
        }

        squelchClip = AudioClip.Create("WetMopSquelch", sampleCount, 1, sampleRate, false);
        squelchClip.SetData(samples, 0);
    }

    private void GenerateProceduralPutBackAudio()
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * 0.25f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 22f);
            float tone1 = Mathf.Sin(2f * Mathf.PI * 180f * t);
            float tone2 = Mathf.Sin(2f * Mathf.PI * 340f * t) * 0.5f;
            float noise = Random.Range(-0.3f, 0.3f) * Mathf.Exp(-t * 40f);

            samples[i] = (tone1 + tone2 + noise) * envelope * 0.7f;
        }

        putBackClip = AudioClip.Create("MopPutBackClatter", sampleCount, 1, sampleRate, false);
        putBackClip.SetData(samples, 0);
    }

    private void OnDestroy()
    {
        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.OnSlotChanged -= HandleHotbarSlotChanged;
        }
    }
}

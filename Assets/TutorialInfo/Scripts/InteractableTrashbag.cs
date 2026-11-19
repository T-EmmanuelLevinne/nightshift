using UnityEngine;

/// <summary>
/// Attached to the stockroom Trashbag model (Trashbag_LOW).
/// Allows "E to pickup" strictly when the Take Out the Trash task is active,
/// equips the trashbag into HorrorHotbar so the player can switch to an empty slot
/// (empty-handed) to open SM_Dumpster, and then put the trash inside SM_Dumpster.
/// </summary>
public class InteractableTrashbag : MonoBehaviour, IInteractable
{
    public static InteractableTrashbag Instance { get; private set; }

    public static bool IsTrashPickedUp => Instance != null && Instance.isPickedUp && !Instance.isDisposed;
    public static bool IsTrashDisposed => Instance != null && Instance.isDisposed;
    public static bool IsHoldingTrashbagInHand => Instance != null && Instance.IsHeldInActiveHand;

    [Header("Viewmodel Settings")]
    public Vector3 viewmodelOffset = new Vector3(0.24f, -0.32f, 0.52f);
    public Vector3 viewmodelRotation = new Vector3(-80f, 15f, -20f);

    private bool isPickedUp = false;
    private bool isDisposed = false;
    private int assignedHotbarSlot = 0;

    private Vector3 originalWorldPos;
    private Quaternion originalWorldRot;
    private Vector3 originalLocalScale;
    private Vector3 equippedLocalScale;

    private Collider[] allColliders;
    private Renderer[] allRenderers;
    private BoxCollider interactionTrigger;
    private Transform playerCameraTransform;

    private AudioSource audioSource;
    private AudioClip pickupClip;

    public bool IsPickedUp => isPickedUp;
    public bool IsDisposed => isDisposed;
    public bool IsHeldInActiveHand =>
        isPickedUp && !isDisposed &&
        (HorrorHotbar.Instance == null || HorrorHotbar.Instance.IsSlotActive(assignedHotbarSlot));

    public Vector3 IndicatorWorldPosition => transform.position + Vector3.up * 0.45f;

    public static InteractableTrashbag EnsureAttachedToSceneTrashbag()
    {
        if (Instance != null) return Instance;

        InteractableTrashbag existing = Object.FindAnyObjectByType<InteractableTrashbag>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        GameObject targetGo = null;

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "Trashbag_LOW", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Trashbag", System.StringComparison.OrdinalIgnoreCase))
            {
                targetGo = t.gameObject;
                break;
            }
        }

        if (targetGo == null)
        {
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t != null && t.name.IndexOf("Trashbag", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetGo = t.gameObject;
                    break;
                }
            }
        }

        if (targetGo != null)
        {
            targetGo.SetActive(true);
            Instance = targetGo.AddComponent<InteractableTrashbag>();
            return Instance;
        }

        return null;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;

        originalWorldPos = transform.position;
        originalWorldRot = transform.rotation;
        originalLocalScale = transform.localScale;

        allRenderers = GetComponentsInChildren<Renderer>(true);
        Bounds worldBounds = ComputeWorldBounds();
        float maxExtent = Mathf.Max(0.1f, Mathf.Max(worldBounds.size.x, Mathf.Max(worldBounds.size.y, worldBounds.size.z)));
        float scaleRatio = 0.34f / maxExtent;
        equippedLocalScale = originalLocalScale * scaleRatio;

        EnsureInteractionCollider(worldBounds);
        allColliders = GetComponentsInChildren<Collider>(true);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.5f;
        audioSource.volume = 0.8f;

        GenerateProceduralAudio();
    }

    private void Start()
    {
        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null)
        {
            hotbar.OnSlotChanged -= HandleHotbarSlotChanged;
            hotbar.OnSlotChanged += HandleHotbarSlotChanged;
        }
    }

    private void OnDestroy()
    {
        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.OnSlotChanged -= HandleHotbarSlotChanged;
        }
    }

    private Bounds ComputeWorldBounds()
    {
        if (allRenderers != null && allRenderers.Length > 0)
        {
            Bounds b = allRenderers[0].bounds;
            for (int i = 1; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null) b.Encapsulate(allRenderers[i].bounds);
            }
            return b;
        }
        return new Bounds(transform.position, new Vector3(0.5f, 0.6f, 0.5f));
    }

    private void EnsureInteractionCollider(Bounds worldBounds)
    {
        interactionTrigger = GetComponent<BoxCollider>();
        if (interactionTrigger == null)
        {
            interactionTrigger = gameObject.AddComponent<BoxCollider>();
        }

        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 lossy = transform.lossyScale;
        float sx = Mathf.Max(0.001f, Mathf.Abs(lossy.x));
        float sy = Mathf.Max(0.001f, Mathf.Abs(lossy.y));
        float sz = Mathf.Max(0.001f, Mathf.Abs(lossy.z));

        interactionTrigger.center = localCenter;
        interactionTrigger.size = new Vector3(
            Mathf.Max(0.55f, worldBounds.size.x + 0.18f) / sx,
            Mathf.Max(0.55f, worldBounds.size.y + 0.18f) / sy,
            Mathf.Max(0.55f, worldBounds.size.z + 0.18f) / sz);
        interactionTrigger.isTrigger = true;
    }

    private void LateUpdate()
    {
        if (!isPickedUp || isDisposed) return;

        if (IsHeldInActiveHand)
        {
            transform.localPosition = viewmodelOffset;
            transform.localRotation = Quaternion.Euler(viewmodelRotation);
        }
    }

    private void HandleHotbarSlotChanged(int activeSlot)
    {
        if (!isPickedUp || isDisposed) return;

        bool isThisSlotActive = (activeSlot == assignedHotbarSlot);
        SetRenderersVisible(isThisSlotActive);
    }

    public string GetPromptText()
    {
        if (isPickedUp || isDisposed) return string.Empty;
        if (!NightShiftStoryController.IsTrashTaskActive) return string.Empty;
        if (NightShiftStoryController.IsDialogueActive) return string.Empty;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > 1.85f)
        {
            return string.Empty;
        }

        return "E to pickup";
    }

    public void Interact()
    {
        if (isPickedUp || isDisposed) return;
        if (!NightShiftStoryController.IsTrashTaskActive) return;
        if (NightShiftStoryController.IsDialogueActive) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return;
        if (Vector3.Distance(cam.transform.position, transform.position) > 1.85f) return;

        PickUpTrashbag(cam.transform);
    }

    private void PickUpTrashbag(Transform camTransform)
    {
        playerCameraTransform = camTransform;
        isPickedUp = true;

        if (allColliders != null)
        {
            for (int i = 0; i < allColliders.Length; i++)
            {
                if (allColliders[i] != null) allColliders[i].enabled = false;
            }
        }

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        assignedHotbarSlot = 0;
        if (hotbar != null)
        {
            // Place in Slot 0 ([1]) if empty, otherwise Slot 1 ([2]) so at least one slot ([2] or [3]) remains empty for empty-handed interaction
            if (hotbar.HasItem(0))
            {
                assignedHotbarSlot = 1;
            }
            hotbar.SetSlot(assignedHotbarSlot, "TRASH");
            hotbar.SetActiveSlot(assignedHotbarSlot);
        }

        transform.SetParent(playerCameraTransform, false);
        transform.localPosition = viewmodelOffset;
        transform.localRotation = Quaternion.Euler(viewmodelRotation);
        transform.localScale = equippedLocalScale;

        SetRenderersVisible(true);
        PlayPickupSound();

        if (NightShiftStoryController.Instance != null)
        {
            NightShiftStoryController.Instance.NotifyTrashPickedUp();
        }
    }

    public void PlaceInsideDumpster(Vector3 dumpsterInteriorWorldPos)
    {
        if (!isPickedUp || isDisposed) return;

        isDisposed = true;
        isPickedUp = false;

        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.ClearSlot(assignedHotbarSlot);
        }

        transform.SetParent(null, true);
        transform.position = dumpsterInteriorWorldPos;
        transform.rotation = originalWorldRot;
        transform.localScale = originalLocalScale * 0.85f;

        SetRenderersVisible(true);
    }

    private void SetRenderersVisible(bool visible)
    {
        if (allRenderers == null) return;
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null)
            {
                allRenderers[i].enabled = visible;
            }
        }
    }

    private void PlayPickupSound()
    {
        if (audioSource != null && pickupClip != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.06f);
            audioSource.PlayOneShot(pickupClip, 0.75f);
        }
    }

    private void GenerateProceduralAudio()
    {
        int sr = 44100;
        int len = (int)(sr * 0.16f);
        float[] samples = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Sin(Mathf.Clamp01(t / 0.16f) * Mathf.PI);
            float rustle = (Random.value * 2f - 1f) * 0.55f;
            float lowThud = Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 25f) * 0.45f;
            samples[i] = (rustle + lowThud) * env * 0.7f;
        }
        pickupClip = AudioClip.Create("TrashbagPickup", len, 1, sr, false);
        pickupClip.SetData(samples, 0);
    }
}


using System.Collections;
using UnityEngine;

/// <summary>
/// Interactive door controller for cabin and furniture doors in HouseScene.
/// Implements IInteractable for seamless integration with HorrorInteractionReticle ("E to Open Door" / "E to Close Door" / "E to Open" / "E to Close").
/// Animates doors via the parent Animator's "isOpen_Obj_<objectNumber>" parameter with ZERO delay.
/// Room doors temporarily use trigger pass-through during their swing so closing from close range never nudges or traps the player.
/// Generates atmospheric wooden creak and latch audio procedurally.
/// </summary>
public class CabinDoorInteractable : MonoBehaviour, IInteractable
{
    [Header("Animation Binding")]
    [Tooltip("The Animator that controls this door's open/close animation states.")]
    public Animator targetAnimator;

    [Tooltip("The objectNumber corresponding to isOpen_Obj_X in the FurnishedCabin animation controller.")]
    public int objectNumber = 1;

    [Tooltip("Animator parameter prefix.")]
    public string parameterPrefix = "isOpen_Obj_";

    [Header("State & Safety")]
    public bool isOpen = false;

    [Tooltip("Brief cooldown (seconds) to prevent rapid double-tapping E mid-swing.")]
    public float toggleCooldown = 0.85f;

    [Tooltip("Seconds to temporarily skip solid player collision while a room door swings.")]
    public float swingCollisionBypassDuration = 1.25f;

    [Header("Audio")]
    public AudioSource audioSource;
    private static AudioClip doorCreakOpenClip;
    private static AudioClip doorSwingCloseClip;
    private static AudioClip doorLatchCloseClip;
    private static bool clipsGenerated = false;

    private float nextToggleAllowedTime = 0f;
    private Coroutine delayedLatchCoroutine;
    private Coroutine swingCollisionCoroutine;

    private void Awake()
    {
        if (targetAnimator == null)
        {
            targetAnimator = GetComponentInParent<Animator>();
        }

        // Try to read objectNumber from existing MoveableObject if present
        MoveableObject mo = GetComponent<MoveableObject>();
        if (mo != null && mo.objectNumber > 0)
        {
            objectNumber = mo.objectNumber;
        }

        // Keep room doors solid (non-trigger) when settled so player cannot walk through closed doors!
        // Only small kitchen drawers/cupboards remain triggers.
        Collider col = GetComponent<Collider>();
        if (col != null && IsRoomDoor())
        {
            col.isTrigger = false;
        }

        SetupAudio();
    }

    private void Start()
    {
        // Keep animator enabled from start so SetBool executes instantly with zero initialization hitch
        if (targetAnimator != null)
        {
            targetAnimator.enabled = true;
        }
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
            audioSource.spatialBlend = 1.0f; // 3D positional sound
            audioSource.minDistance = 1.0f;
            audioSource.maxDistance = 14.0f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.volume = 0.75f;
            audioSource.playOnAwake = false;
        }

        if (!clipsGenerated)
        {
            GenerateProceduralAudioClips();
            clipsGenerated = true;
        }
    }

    private void OnDisable()
    {
        if (delayedLatchCoroutine != null)
        {
            StopCoroutine(delayedLatchCoroutine);
            delayedLatchCoroutine = null;
        }

        if (swingCollisionCoroutine != null)
        {
            StopCoroutine(swingCollisionCoroutine);
            swingCollisionCoroutine = null;
        }

        if (IsRoomDoor())
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false;
            }
        }
    }

    /// <summary>Returns true if this interactable belongs to a full-size room door (Single or Double).</summary>
    public bool IsRoomDoor()
    {
        string goName = gameObject.name;
        string animName = targetAnimator != null ? targetAnimator.name : string.Empty;
        string parentName = transform.parent != null ? transform.parent.name : string.Empty;

        return HasRoomDoorKeyword(goName) ||
               HasRoomDoorKeyword(animName) ||
               HasRoomDoorKeyword(parentName) ||
               goName.IndexOf("leftDoor", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               goName.IndexOf("rightDoor", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasRoomDoorKeyword(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.IndexOf("Single", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("Double", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public bool IsFrontEntranceDoubleDoor()
    {
        string goName = gameObject.name;
        string animName = targetAnimator != null ? targetAnimator.name : string.Empty;
        string parentName = transform.parent != null ? transform.parent.name : string.Empty;

        return (!string.IsNullOrEmpty(goName) && (goName.IndexOf("Double", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                goName.IndexOf("leftDoor", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                goName.IndexOf("rightDoor", System.StringComparison.OrdinalIgnoreCase) >= 0)) ||
               (!string.IsNullOrEmpty(animName) && animName.IndexOf("Double", System.StringComparison.OrdinalIgnoreCase) >= 0) ||
               (!string.IsNullOrEmpty(parentName) && parentName.IndexOf("Double", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    public void ForceClosedImmediate()
    {
        isOpen = false;
        if (targetAnimator == null)
        {
            targetAnimator = GetComponentInParent<Animator>();
        }
        if (targetAnimator != null)
        {
            targetAnimator.enabled = true;
            string param = parameterPrefix + objectNumber;
            targetAnimator.SetBool(param, false);
            targetAnimator.Update(0f);
        }
        Collider col = GetComponent<Collider>();
        if (col != null && IsRoomDoor())
        {
            col.isTrigger = false;
        }
    }

    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsDialogueActive || Time.time < nextToggleAllowedTime)
        {
            return string.Empty;
        }

        if (HouseSceneStoryController.IsFrontDoorLocked && IsFrontEntranceDoubleDoor())
        {
            return "Door Locked";
        }

        if (IsRoomDoor())
        {
            return isOpen ? "E to Close Door" : "E to Open Door";
        }

        return isOpen ? "E to Close" : "E to Open";
    }

    public void Interact()
    {
        ToggleDoor();
    }

    public void ToggleDoor()
    {
        if (Time.time < nextToggleAllowedTime || HouseSceneStoryController.IsDialogueActive)
        {
            return;
        }

        if (HouseSceneStoryController.IsFrontDoorLocked && IsFrontEntranceDoubleDoor())
        {
            ForceClosedImmediate();
            nextToggleAllowedTime = Time.time + 0.35f;
            if (audioSource != null && doorLatchCloseClip != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.05f);
                audioSource.PlayOneShot(doorLatchCloseClip, 0.65f);
            }
            return;
        }

        if (targetAnimator == null)
        {
            targetAnimator = GetComponentInParent<Animator>();
            if (targetAnimator == null)
            {
                Debug.LogWarning($"[CabinDoorInteractable] No Animator found for door '{name}'!");
                return;
            }
        }

        float effectiveCooldown = IsRoomDoor() ? Mathf.Max(toggleCooldown, 0.90f) : Mathf.Max(0.62f, toggleCooldown * 0.75f);
        float cooldownEnd = Time.time + effectiveCooldown;
        nextToggleAllowedTime = cooldownEnd;

        isOpen = !isOpen;
        targetAnimator.enabled = true;

        string param = parameterPrefix + objectNumber;
        targetAnimator.SetBool(param, isOpen);

        // Immediate evaluation forces the state transition to begin on this exact frame (zero delay)
        targetAnimator.Update(0f);

        BeginTemporaryCollisionSkip();

        // Cancel any pending latch sound if player re-opened or toggled early
        if (delayedLatchCoroutine != null)
        {
            StopCoroutine(delayedLatchCoroutine);
            delayedLatchCoroutine = null;
        }

        // Sync sibling door leaves sharing this animator and parameter (e.g. left and right leaves of entrance double doors)
        CabinDoorInteractable[] siblings = targetAnimator.GetComponentsInChildren<CabinDoorInteractable>(true);
        for (int s = 0; s < siblings.Length; s++)
        {
            if (siblings[s] != null && siblings[s] != this && siblings[s].objectNumber == objectNumber)
            {
                siblings[s].isOpen = isOpen;
                siblings[s].nextToggleAllowedTime = cooldownEnd;
                siblings[s].BeginTemporaryCollisionSkip();
            }
        }

        // Smooth, well-timed audio playback
        if (audioSource != null)
        {
            if (isOpen)
            {
                // Gentle wooden hinge creak on opening
                audioSource.pitch = Random.Range(0.96f, 1.04f);
                if (doorCreakOpenClip != null)
                {
                    audioSource.PlayOneShot(doorCreakOpenClip, 0.45f);
                }
            }
            else
            {
                // Gentle wooden hinge swing movement sound when closing begins
                audioSource.pitch = Random.Range(0.96f, 1.04f);
                if (doorSwingCloseClip != null)
                {
                    audioSource.PlayOneShot(doorSwingCloseClip, 0.35f);
                }

                // Tactile, gentle latch click only when the door reaches the frame (~1.05s for room doors, ~0.72s for cabinets)
                float closeDelay = IsRoomDoor() ? 1.05f : 0.72f;
                delayedLatchCoroutine = StartCoroutine(PlayDelayedLatch(closeDelay));
            }
        }
    }

    private void BeginTemporaryCollisionSkip()
    {
        if (!IsRoomDoor() || !gameObject.activeInHierarchy) return;

        Collider col = GetComponent<Collider>();
        if (col == null) return;

        if (swingCollisionCoroutine != null)
        {
            StopCoroutine(swingCollisionCoroutine);
            swingCollisionCoroutine = null;
        }

        swingCollisionCoroutine = StartCoroutine(TemporarySwingPassThroughRoutine(col));
    }

    private IEnumerator TemporarySwingPassThroughRoutine(Collider doorCollider)
    {
        doorCollider.isTrigger = true;
        yield return new WaitForSeconds(swingCollisionBypassDuration);

        CharacterController playerCc = Object.FindAnyObjectByType<CharacterController>();
        while (doorCollider != null && playerCc != null && IsPlayerOverlappingDoorCollider(doorCollider, playerCc))
        {
            yield return null;
        }

        if (doorCollider != null)
        {
            doorCollider.isTrigger = false;
        }

        swingCollisionCoroutine = null;
    }

    private static bool IsPlayerOverlappingDoorCollider(Collider doorCol, CharacterController playerCc)
    {
        if (doorCol == null || playerCc == null || !playerCc.enabled) return false;

        Bounds pBounds = playerCc.bounds;
        Bounds dBounds = doorCol.bounds;
        if (pBounds.max.y <= dBounds.min.y || pBounds.min.y >= dBounds.max.y)
        {
            return false;
        }

        Vector3 center = pBounds.center;
        Vector3 samplePoint = new Vector3(center.x, Mathf.Clamp(center.y, dBounds.min.y, dBounds.max.y), center.z);
        Vector3 closest = doorCol.ClosestPoint(samplePoint);

        float dx = closest.x - samplePoint.x;
        float dz = closest.z - samplePoint.z;
        float clearance = playerCc.radius + 0.06f;
        return (dx * dx + dz * dz) < (clearance * clearance);
    }

    private IEnumerator PlayDelayedLatch(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!isOpen && audioSource != null && doorLatchCloseClip != null)
        {
            audioSource.pitch = Random.Range(0.97f, 1.03f);
            audioSource.PlayOneShot(doorLatchCloseClip, 0.40f);
        }
        delayedLatchCoroutine = null;
    }

    private static void GenerateProceduralAudioClips()
    {
        int sampleRate = 44100;

        // 1. Procedural wooden creak (open) - gentle and natural (~1.15s matching weighty room door swing)
        float openDur = 1.15f;
        int openSamples = Mathf.RoundToInt(openDur * sampleRate);
        float[] openData = new float[openSamples];
        float phase = 0f;

        for (int i = 0; i < openSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / openDur;

            float freq = Mathf.Lerp(140f, 260f, progress) + Mathf.Sin(t * 70f) * 30f;
            phase += 2f * Mathf.PI * freq / sampleRate;

            float creak = Mathf.Sin(phase);
            float woodNoise = (Random.value * 2f - 1f) * 0.08f;
            float env = Mathf.Sin(progress * Mathf.PI);
            openData[i] = Mathf.Clamp((creak * 0.30f + woodNoise) * env * 0.4f, -1f, 1f);
        }

        doorCreakOpenClip = AudioClip.Create("CabinDoor_CreakOpen", openSamples, 1, sampleRate, false);
        doorCreakOpenClip.SetData(openData, 0);

        // 2. Procedural wooden swing (close start) - soft hinge glide (~0.88s)
        float swingDur = 0.88f;
        int swingSamples = Mathf.RoundToInt(swingDur * sampleRate);
        float[] swingData = new float[swingSamples];
        float swingPhase = 0f;

        for (int i = 0; i < swingSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / swingDur;

            float freq = Mathf.Lerp(180f, 110f, progress);
            swingPhase += 2f * Mathf.PI * freq / sampleRate;

            float tone = Mathf.Sin(swingPhase) * 0.15f;
            float softAir = (Random.value * 2f - 1f) * 0.06f;
            float env = Mathf.Sin(progress * Mathf.PI);
            swingData[i] = Mathf.Clamp((tone + softAir) * env * 0.35f, -1f, 1f);
        }

        doorSwingCloseClip = AudioClip.Create("CabinDoor_SwingClose", swingSamples, 1, sampleRate, false);
        doorSwingCloseClip.SetData(swingData, 0);

        // 3. Procedural tactile latch click (delayed close arrival) - gentle, satisfying wooden frame shut
        float latchDur = 0.18f;
        int latchSamples = Mathf.RoundToInt(latchDur * sampleRate);
        float[] latchData = new float[latchSamples];

        for (int i = 0; i < latchSamples; i++)
        {
            float t = (float)i / sampleRate;

            // Soft wooden frame touch (muffled, low energy, not a booming slam)
            float frameTouch = Mathf.Sin(2f * Mathf.PI * 130f * t) * Mathf.Exp(-t * 38f) * 0.28f;
            // Crisp small latch click
            float latchClick = Mathf.Sin(2f * Mathf.PI * 520f * t) * Mathf.Exp(-t * 70f) * 0.30f;
            float microClick = Mathf.Sin(2f * Mathf.PI * 1200f * t) * Mathf.Exp(-t * 120f) * 0.12f;

            latchData[i] = Mathf.Clamp(frameTouch + latchClick + microClick, -1f, 1f);
        }

        doorLatchCloseClip = AudioClip.Create("CabinDoor_LatchClose", latchSamples, 1, sampleRate, false);
        doorLatchCloseClip.SetData(latchData, 0);
    }
}

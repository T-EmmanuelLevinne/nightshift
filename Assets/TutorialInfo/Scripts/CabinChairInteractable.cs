using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Interactive chair controller for PFB_DiningChairUserInteract in HouseScene.
/// Flow:
/// 1. Chair tucked in -> Reticle prompt: "E to move the chair"
/// 2. Press E -> Animates chair backwards smoothly (~0.52m away from table)
/// 3. Chair pulled out -> Reticle prompt: "E to Sit"
/// 4. Press E -> Smoothly sits player into chair (eye height lowers, movement locked, head look active)
/// 5. While sitting -> Right-side horror UI prompt: "E to Unsit" (center reticle hidden)
/// 6. Press E -> Smoothly unsits player to standing position behind/beside chair, restores movement & reticle
/// </summary>
public class CabinChairInteractable : MonoBehaviour, IInteractable
{
    public static bool IsPlayerSitting { get; private set; }
    public static bool IsPlayerEating { get; set; }
    public static CabinChairInteractable CurrentChair { get; private set; }

    public enum ChairState
    {
        TuckedIn,
        MovingBack,
        PulledOut,
        Sitting,
        StandingUp
    }

    [Header("State")]
    public ChairState currentState = ChairState.TuckedIn;
    public bool isMovedBack = false;

    [Header("Behavior Settings")]
    [Tooltip("If true, requires 'E to move the chair' first (e.g. dining chair). If false, directly offers 'E to Sit' (e.g. sofas).")]
    public bool requiresMoveFirst = true;
    [Tooltip("Where player stands up: true = in front of sofa (+forward), false = behind chair (-forward).")]
    public bool standUpInFront = false;

    [Header("Chair Movement Settings")]
    [Tooltip("Distance the chair moves backwards away from table when pulled out.")]
    public float moveDistance = 0.52f;
    [Tooltip("Duration of chair sliding animation in seconds.")]
    public float moveDuration = 0.50f;

    [Header("Sitting Camera Settings")]
    [Tooltip("Camera eye height while sitting down in the chair.")]
    public float sittingEyeHeight = 1.15f;

    [Header("Audio")]
    public AudioSource audioSource;
    private static AudioClip chairSlideClip;
    private static AudioClip chairSitClip;
    private static AudioClip chairStandClip;
    private static bool clipsGenerated = false;

    // Runtime positions
    private Vector3 tuckedPosition;
    private Vector3 pulledPosition;
    private Vector3 standPosition;
    private float originalEyeHeight = 1.75f;
    private bool isTransitioning = false;

    // UI Styling
    private Texture2D bgTexture;
    private GUIStyle rightPromptStyle;
    private GUIStyle rightShadowStyle;
    private Font retroFont;

    private void Awake()
    {
        tuckedPosition = transform.position;
        // The chair's local -forward points backwards (away from table towards open room)
        pulledPosition = tuckedPosition - transform.forward * moveDistance;

        if (!requiresMoveFirst)
        {
            isMovedBack = true;
            currentState = ChairState.PulledOut;
        }

        // Ensure static flags don't freeze the transform
        gameObject.isStatic = false;

        SetupCollider();
        SetupAudio();
    }

    private void SetupCollider()
    {
        // Add a trigger box collider if missing so the reticle can aim at the chair with 100% precision
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
        }

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            box.center = b.center;
            box.size = new Vector3(
                Mathf.Max(b.size.x, 0.48f),
                Mathf.Max(b.size.y, 0.95f),
                Mathf.Max(b.size.z, 0.48f)
            );
        }
        else
        {
            box.center = new Vector3(0f, 0.48f, 0f);
            box.size = new Vector3(0.50f, 0.96f, 0.50f);
        }

        box.isTrigger = true;
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
            audioSource.volume = 0.70f;
            audioSource.playOnAwake = false;
        }

        if (!clipsGenerated)
        {
            GenerateProceduralAudioClips();
            clipsGenerated = true;
        }
    }

    public string GetPromptText()
    {
        if (IsPlayerSitting || isTransitioning)
        {
            return ""; // Center reticle prompt is suppressed while sitting
        }

        if (requiresMoveFirst && !isMovedBack)
        {
            return "E to move the chair";
        }

        return "E to Sit";
    }

    public void Interact()
    {
        if (isTransitioning) return;

        if (requiresMoveFirst && !isMovedBack)
        {
            StartCoroutine(AnimateChairSlide(pulledPosition, true));
        }
        else if (!IsPlayerSitting)
        {
            StartCoroutine(SitRoutine());
        }
    }

    public void UnsitPlayer()
    {
        if (!IsPlayerSitting || isTransitioning || IsPlayerEating || HouseSceneStoryController.IsSitLockedByTv) return;
        StartCoroutine(UnsitRoutine());
    }

    public static void ResetStaticState()
    {
        IsPlayerSitting = false;
        IsPlayerEating = false;
        CurrentChair = null;
    }

    private void OnDestroy()
    {
        if (CurrentChair == this)
        {
            ResetStaticState();
        }
    }

    private void Update()
    {
        // While player is sitting (and not actively eating or locked by TV story), monitor E press to trigger unsit
        if (IsPlayerSitting && CurrentChair == this && !isTransitioning && !IsPlayerEating)
        {
            if (HouseSceneStoryController.IsDialogueActive || HouseSceneStoryController.IsSitLockedByTv)
            {
                return;
            }

            if (HorrorInteractionReticle.CurrentInteractable != null)
            {
                return;
            }

            bool unsitRequested = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                unsitRequested = true;
            }
#else
            if (!unsitRequested && Input.GetKeyDown(KeyCode.E))
            {
                unsitRequested = true;
            }
#endif

            if (!unsitRequested && HorrorInteractionReticle.WasInteractPressed())
            {
                unsitRequested = true;
            }

            if (unsitRequested)
            {
                UnsitPlayer();
            }
        }
    }

    private IEnumerator AnimateChairSlide(Vector3 targetPos, bool movedBackState)
    {
        isTransitioning = true;
        currentState = ChairState.MovingBack;

        PlayClip(chairSlideClip, 0.55f);

        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector3.Lerp(startPos, targetPos, smooth);
            yield return null;
        }

        transform.position = targetPos;
        isMovedBack = movedBackState;
        currentState = isMovedBack ? ChairState.PulledOut : ChairState.TuckedIn;
        isTransitioning = false;
    }

    private IEnumerator SitRoutine()
    {
        isTransitioning = true;
        currentState = ChairState.Sitting;

        GameObject player = ResolvePlayer();
        if (player == null)
        {
            isTransitioning = false;
            yield break;
        }

        SimpleFPSController fps = player.GetComponent<SimpleFPSController>();
        CharacterController cc = player.GetComponent<CharacterController>();

        if (cc != null) cc.enabled = false;

        standPosition = player.transform.position;
        Vector3 startPos = player.transform.position;
        Quaternion startRot = player.transform.rotation;

        if (fps != null)
        {
            originalEyeHeight = fps.eyeHeight;
        }

        // Sit position: center of seat cushion
        Vector3 targetPos = transform.position + transform.forward * 0.06f;
        targetPos.y = startPos.y; // Keep vertical ground alignment
        Quaternion targetRot = Quaternion.LookRotation(transform.forward, Vector3.up);

        PlayClip(chairSitClip, 0.45f);

        float sitDuration = 0.42f;
        float elapsed = 0f;

        while (elapsed < sitDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / sitDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            player.transform.position = Vector3.Lerp(startPos, targetPos, smooth);
            player.transform.rotation = Quaternion.Slerp(startRot, targetRot, smooth);

            if (fps != null && fps.playerCamera != null)
            {
                float currentEye = Mathf.Lerp(originalEyeHeight, sittingEyeHeight, smooth);
                fps.playerCamera.transform.localPosition = new Vector3(0f, currentEye, 0f);
            }

            yield return null;
        }

        player.transform.position = targetPos;
        player.transform.rotation = targetRot;

        if (fps != null && fps.playerCamera != null)
        {
            fps.playerCamera.transform.localPosition = new Vector3(0f, sittingEyeHeight, 0f);
        }

        IsPlayerSitting = true;
        CurrentChair = this;
        isTransitioning = false;
    }

    private IEnumerator UnsitRoutine()
    {
        isTransitioning = true;
        currentState = ChairState.StandingUp;
        IsPlayerSitting = false;
        CurrentChair = null;

        GameObject player = ResolvePlayer();
        if (player == null)
        {
            isTransitioning = false;
            yield break;
        }

        SimpleFPSController fps = player.GetComponent<SimpleFPSController>();
        CharacterController cc = player.GetComponent<CharacterController>();

        Vector3 startPos = player.transform.position;
        Quaternion startRot = player.transform.rotation;

        // Stand position: in front for sofa (+forward), behind for dining chair (-forward)
        Vector3 standOffset = standUpInFront ? (transform.forward * 0.75f) : (-transform.forward * 0.58f);
        Vector3 targetPos = transform.position + standOffset;
        targetPos.y = standPosition.y;
        Quaternion targetRot = Quaternion.LookRotation(transform.forward, Vector3.up);

        PlayClip(chairStandClip, 0.40f);

        float standDuration = 0.38f;
        float elapsed = 0f;

        while (elapsed < standDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / standDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            player.transform.position = Vector3.Lerp(startPos, targetPos, smooth);
            player.transform.rotation = Quaternion.Slerp(startRot, targetRot, smooth);

            if (fps != null && fps.playerCamera != null)
            {
                float currentEye = Mathf.Lerp(sittingEyeHeight, originalEyeHeight, smooth);
                fps.playerCamera.transform.localPosition = new Vector3(0f, currentEye, 0f);
            }

            yield return null;
        }

        player.transform.position = targetPos;
        player.transform.rotation = targetRot;

        if (fps != null)
        {
            fps.eyeHeight = originalEyeHeight;
            if (fps.playerCamera != null)
            {
                fps.playerCamera.transform.localPosition = new Vector3(0f, originalEyeHeight, 0f);
            }
        }

        if (cc != null) cc.enabled = true;

        currentState = ChairState.PulledOut;
        isTransitioning = false;

        if (CabinCookingSystem.IsMealFinished && !HouseSceneStoryController.HasUnsatAfterEating)
        {
            HouseSceneStoryController.NotifyUnsatAfterEating();
        }
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.pitch = Random.Range(0.96f, 1.04f);
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private GameObject ResolvePlayer()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.activePlayer != null)
        {
            return PlayerManager.Instance.activePlayer;
        }

        var fps = Object.FindAnyObjectByType<SimpleFPSController>();
        if (fps != null) return fps.gameObject;

        return GameObject.Find("Test_FPS_Player");
    }

    private void OnDisable()
    {
        if (IsPlayerSitting && CurrentChair == this)
        {
            IsPlayerSitting = false;
            IsPlayerEating = false;
            CurrentChair = null;
        }
    }

    private void OnGUI()
    {
        // ONLY render this special prompt on the top right while the user is actively sitting in this chair
        if (!IsPlayerSitting || CurrentChair != this) return;
        if (Event.current.type != EventType.Repaint) return;
        if (InteractableCashierPC.IsPosScreenOpen) return;
        if (HouseSceneStoryController.IsDialogueActive) return;

        InitStyles();

        string prompt = IsPlayerEating
            ? "Eating Breakfast..."
            : (HouseSceneStoryController.IsSitLockedByTv ? "Watching TV..." : "Press E to Stand");
        float boxWidth = 240f;
        float boxHeight = 44f;
        float rightMargin = 32f;
        float topMargin = 32f;
        float x = Screen.width - boxWidth - rightMargin;
        float y = topMargin;

        Rect boxRect = new Rect(x, y, boxWidth, boxHeight);

        // Dark atmospheric horror background pill
        if (bgTexture == null)
        {
            bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, new Color(0.06f, 0.06f, 0.08f, 0.88f));
            bgTexture.Apply();
        }

        GUI.color = Color.white;
        GUI.DrawTexture(boxRect, bgTexture);

        // Subtle warm border
        GUI.color = new Color(0.60f, 0.55f, 0.38f, 0.75f);
        GUI.DrawTexture(new Rect(boxRect.x, boxRect.y, boxRect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(boxRect.x, boxRect.yMax - 1, boxRect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(boxRect.x, boxRect.y, 1, boxRect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(boxRect.xMax - 1, boxRect.y, 1, boxRect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Retro drop shadow / outline
        GUI.Label(new Rect(boxRect.x - 1f, boxRect.y, boxRect.width, boxRect.height), prompt, rightShadowStyle);
        GUI.Label(new Rect(boxRect.x + 1f, boxRect.y, boxRect.width, boxRect.height), prompt, rightShadowStyle);
        GUI.Label(new Rect(boxRect.x, boxRect.y - 1f, boxRect.width, boxRect.height), prompt, rightShadowStyle);
        GUI.Label(new Rect(boxRect.x, boxRect.y + 1f, boxRect.width, boxRect.height), prompt, rightShadowStyle);
        GUI.Label(new Rect(boxRect.x + 1.5f, boxRect.y + 1.5f, boxRect.width, boxRect.height), prompt, rightShadowStyle);

        // Foreground text
        GUI.Label(boxRect, prompt, rightPromptStyle);
    }

    private void InitStyles()
    {
        if (retroFont == null)
        {
            string[] fontNames = new string[] { "Lucida Console", "Courier New", "Consolas", "Courier" };
            retroFont = Font.CreateDynamicFontFromOSFont(fontNames, 17);
        }

        if (rightPromptStyle == null)
        {
            rightPromptStyle = new GUIStyle(GUI.skin.label)
            {
                font = retroFont,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            rightPromptStyle.normal.textColor = new Color(0.95f, 0.92f, 0.80f, 1.0f);
        }

        if (rightShadowStyle == null)
        {
            rightShadowStyle = new GUIStyle(rightPromptStyle);
            rightShadowStyle.normal.textColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        }
    }

    private static void GenerateProceduralAudioClips()
    {
        int sampleRate = 44100;

        // 1. Chair sliding / scraping across wooden floor (~0.48s)
        float slideDur = 0.48f;
        int slideSamples = Mathf.RoundToInt(slideDur * sampleRate);
        float[] slideData = new float[slideSamples];
        float slidePhase1 = 0f;
        float slidePhase2 = 0f;

        for (int i = 0; i < slideSamples; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / slideDur;

            slidePhase1 += 2f * Mathf.PI * 220f / sampleRate;
            slidePhase2 += 2f * Mathf.PI * 340f / sampleRate;

            float woodResonance = Mathf.Sin(slidePhase1) * 0.15f + Mathf.Sin(slidePhase2) * 0.10f;
            float frictionNoise = (Random.value * 2f - 1f) * 0.18f;
            float env = Mathf.Sin(progress * Mathf.PI);

            slideData[i] = Mathf.Clamp((woodResonance + frictionNoise) * env * 0.55f, -1f, 1f);
        }

        chairSlideClip = AudioClip.Create("CabinChair_Slide", slideSamples, 1, sampleRate, false);
        chairSlideClip.SetData(slideData, 0);

        // 2. Chair sit cushion / cloth settle (~0.28s)
        float sitDur = 0.28f;
        int sitSamples = Mathf.RoundToInt(sitDur * sampleRate);
        float[] sitData = new float[sitSamples];

        for (int i = 0; i < sitSamples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 20f);
            float cushionThud = Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.25f;
            float clothRustle = (Random.value * 2f - 1f) * 0.08f;

            sitData[i] = Mathf.Clamp((cushionThud + clothRustle) * env * 0.50f, -1f, 1f);
        }

        chairSitClip = AudioClip.Create("CabinChair_Sit", sitSamples, 1, sampleRate, false);
        chairSitClip.SetData(sitData, 0);

        // 3. Chair stand rise / subtle floor tap (~0.22s)
        float standDur = 0.22f;
        int standSamples = Mathf.RoundToInt(standDur * sampleRate);
        float[] standData = new float[standSamples];

        for (int i = 0; i < standSamples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 26f);
            float riseThud = Mathf.Sin(2f * Mathf.PI * 160f * t) * 0.20f;
            float softCloth = (Random.value * 2f - 1f) * 0.06f;

            standData[i] = Mathf.Clamp((riseThud + softCloth) * env * 0.45f, -1f, 1f);
        }

        chairStandClip = AudioClip.Create("CabinChair_Stand", standSamples, 1, sampleRate, false);
        chairStandClip.SetData(standData, 0);
    }
}


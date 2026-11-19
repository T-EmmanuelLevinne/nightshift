using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
[RequireComponent(typeof(Collider))]
public class FloorStain : MonoBehaviour, IInteractable
{
    public static List<FloorStain> AllStains = new List<FloorStain>();

    public static int TotalStainsCount => AllStains.Count;
    public static int CleanedStainsCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < AllStains.Count; i++)
            {
                if (AllStains[i] != null && AllStains[i].isCleaned) count++;
            }
            return count;
        }
    }

    public static bool AllStainsCleaned => TotalStainsCount > 0 && CleanedStainsCount >= TotalStainsCount;

    public static bool IsAnyStainBeingMopped
    {
        get
        {
            for (int i = 0; i < AllStains.Count; i++)
            {
                if (AllStains[i] != null && AllStains[i].isMopping) return true;
            }
            return false;
        }
    }

    [Header("Stain Identity")]
    public int stainIndex = 1;
    public bool isCleaned = false;

    [Header("Mopping Settings")]
    public float cleanDuration = 1.8f;

    [Header("Rendering")]
    public MeshRenderer stainRenderer;

    private bool isMopping = false;
    private Vector3 initialScale;
    private BoxCollider triggerCollider;

    private AudioSource audioSource;
    private AudioClip squelchClip;
    private AudioClip cleanSuccessClip;

    private Font horrorFont;
    private GUIStyle cachedMainStyle;
    private GUIStyle cachedShadowStyle;

    public bool IsCleaned => isCleaned;
    public bool IsMopping => isMopping;

    private void Awake()
    {
        initialScale = transform.localScale;
        if (initialScale.sqrMagnitude < 0.001f)
        {
            initialScale = Vector3.one;
        }

        if (stainRenderer == null)
        {
            stainRenderer = GetComponent<MeshRenderer>();
            if (stainRenderer == null) stainRenderer = GetComponentInChildren<MeshRenderer>();
        }

        triggerCollider = GetComponent<BoxCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }
        triggerCollider.isTrigger = true;
        triggerCollider.center = new Vector3(0f, 0.08f, 0f);
        triggerCollider.size = new Vector3(1.3f, 0.25f, 1.3f);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.75f;
            audioSource.volume = 0.85f;
        }

        GenerateProceduralAudio();
        EnsureFontLoaded();
    }

    private void OnEnable()
    {
        if (!AllStains.Contains(this))
        {
            AllStains.Add(this);
        }
    }

    private void OnDisable()
    {
        AllStains.Remove(this);
    }

    private void OnDestroy()
    {
        AllStains.Remove(this);
    }

    private void Update()
    {
        if (isCleaned || isMopping) return;

        // If player has mop equipped and clicks mouse while looking directly at this stain
        if (HorrorInteractionReticle.WasMouseClicked() && HasMopEquipped())
        {
            Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
            if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();

            if (cam != null)
            {
                float dist = Vector3.Distance(cam.transform.position, transform.position);
                if (dist <= 1.75f)
                {
                    // Check if aiming at this stain or if reticle is hovering on this stain
                    if (HorrorInteractionReticle.CurrentInteractable == (IInteractable)this)
                    {
                        StartCleaning();
                        return;
                    }

                    Vector3 dirToStain = (transform.position - cam.transform.position).normalized;
                    if (Vector3.Dot(cam.transform.forward, dirToStain) > 0.82f)
                    {
                        StartCleaning();
                        return;
                    }
                }
            }
        }
    }

    public static bool HasMopEquipped()
    {
        EquippableMop mop = EquippableMop.Instance;
        if (mop == null) mop = Object.FindAnyObjectByType<EquippableMop>();
        if (mop == null || !mop.IsEquipped) return false;

        bool isSlot0Active = (HorrorHotbar.Instance == null || HorrorHotbar.Instance.IsSlotActive(0));
        return isSlot0Active;
    }

    public static FloorStain GetNearestUncleanedStain(Vector3 fromPos, float maxDist = 1.75f)
    {
        FloorStain nearest = null;
        float bestDist = maxDist;

        for (int i = 0; i < AllStains.Count; i++)
        {
            FloorStain s = AllStains[i];
            if (s == null || s.isCleaned || s.isMopping) continue;

            float d = Vector3.Distance(fromPos, s.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                nearest = s;
            }
        }

        return nearest;
    }

    // --- IInteractable Implementation ---
    public string GetPromptText()
    {
        if (isCleaned || isMopping) return string.Empty;
        if (!NightShiftStoryController.IsCleaningTaskActive) return string.Empty;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > 1.75f)
        {
            return string.Empty;
        }

        if (HasMopEquipped())
        {
            return $"E or Click to mop stain ({CleanedStainsCount}/{TotalStainsCount})";
        }

        return "Mop required to clean";
    }

    public void Interact()
    {
        if (isCleaned || isMopping) return;
        if (!NightShiftStoryController.IsCleaningTaskActive) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > 1.75f)
        {
            return;
        }

        if (HasMopEquipped())
        {
            StartCleaning();
        }
    }

    public void StartCleaning()
    {
        if (isCleaned || isMopping) return;
        StartCoroutine(CleaningRoutine());
    }

    private IEnumerator CleaningRoutine()
    {
        isMopping = true;

        EquippableMop mop = EquippableMop.Instance;
        if (mop == null) mop = Object.FindAnyObjectByType<EquippableMop>();

        if (mop != null)
        {
            mop.TriggerDramaticMop(cleanDuration);
        }

        PlaySquelchSound(1.0f);

        float elapsed = 0f;
        Material mat = stainRenderer != null ? stainRenderer.material : null;
        Color initialColor = mat != null ? mat.color : Color.white;

        while (elapsed < cleanDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cleanDuration);

            // Progressive shrink and fade
            float scaleFactor = Mathf.Lerp(1.0f, 0.0f, Mathf.SmoothStep(0f, 1f, t));
            transform.localScale = initialScale * scaleFactor;

            if (mat != null)
            {
                Color c = initialColor;
                c.a = Mathf.Lerp(initialColor.a, 0f, t);
                mat.color = c;
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", c);
                }
            }

            if (elapsed > 0.45f && elapsed - Time.deltaTime <= 0.45f)
            {
                PlaySquelchSound(1.15f);
            }
            if (elapsed > 0.95f && elapsed - Time.deltaTime <= 0.95f)
            {
                PlaySquelchSound(0.92f);
            }
            if (elapsed > 1.45f && elapsed - Time.deltaTime <= 1.45f)
            {
                PlaySquelchSound(1.08f);
            }

            yield return null;
        }

        isCleaned = true;
        isMopping = false;

        PlayCleanSuccessSound();

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        if (stainRenderer != null)
        {
            stainRenderer.enabled = false;
        }

        // Keep GameObject active so AllStains tracking and cleanup state remains intact
        gameObject.SetActive(true);
    }

    private void PlaySquelchSound(float pitch)
    {
        if (audioSource != null && squelchClip != null)
        {
            audioSource.pitch = pitch + Random.Range(-0.06f, 0.06f);
            audioSource.PlayOneShot(squelchClip, 0.85f);
        }
    }

    private void PlayCleanSuccessSound()
    {
        if (audioSource != null && cleanSuccessClip != null)
        {
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(cleanSuccessClip, 0.95f);
        }
    }

    private void GenerateProceduralAudio()
    {
        int sampleRate = 44100;

        // 1. Wet Squelch Scrubbing sound
        int squelchCount = (int)(sampleRate * 0.35f);
        float[] squelchSamples = new float[squelchCount];
        float filterState = 0f;
        for (int i = 0; i < squelchCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Sin(t / 0.35f * Mathf.PI);

            float white = Random.Range(-1f, 1f);
            filterState += 0.15f * (white - filterState);
            float bubble = Mathf.Sin(2f * Mathf.PI * (170f + 90f * Mathf.Sin(t * 40f)) * t) * 0.3f;

            squelchSamples[i] = (filterState * 0.7f + bubble * 0.3f) * envelope * 0.75f;
        }
        squelchClip = AudioClip.Create("StainWetSquelch", squelchCount, 1, sampleRate, false);
        squelchClip.SetData(squelchSamples, 0);

        // 2. Crisp Clean Success Splash / Chime
        int chimeCount = (int)(sampleRate * 0.40f);
        float[] chimeSamples = new float[chimeCount];
        for (int i = 0; i < chimeCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 9f);
            float tone1 = Mathf.Sin(2f * Mathf.PI * 659.25f * t) * 0.45f; // E5
            float tone2 = Mathf.Sin(2f * Mathf.PI * 987.77f * t) * 0.35f; // B5
            float tone3 = Mathf.Sin(2f * Mathf.PI * 1318.51f * t) * 0.25f; // E6
            chimeSamples[i] = (tone1 + tone2 + tone3) * envelope * 0.7f;
        }
        cleanSuccessClip = AudioClip.Create("StainCleanedChime", chimeCount, 1, sampleRate, false);
        cleanSuccessClip.SetData(chimeSamples, 0);
    }

    private void EnsureFontLoaded()
    {
        if (horrorFont == null)
        {
            string[] fontNames = new string[] { "Courier New", "Lucida Console", "Consolas", "Courier" };
            horrorFont = Font.CreateDynamicFontFromOSFont(fontNames, 18);
        }
    }

    private void EnsureGuiStylesInOnGUI()
    {
        EnsureFontLoaded();

        if (cachedMainStyle == null || cachedShadowStyle == null)
        {
            cachedMainStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            if (horrorFont != null) cachedMainStyle.font = horrorFont;

            cachedShadowStyle = new GUIStyle(cachedMainStyle);
            cachedShadowStyle.normal.textColor = new Color(0.01f, 0.01f, 0.01f, 0.98f);
        }
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        // Do not show task to clean the mess until dialogue and 2s cooldown have completed!
        if (!NightShiftStoryController.IsCleaningTaskActive) return;

        // If this stain is cleaned, or if ALL 3 stains are cleaned, hide the text completely!
        if (isCleaned || AllStainsCleaned) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        float dist = Vector3.Distance(camPos, transform.position);
        if (dist > 9.0f) return;

        // Responsive anchor height when standing close so it never breaks or clips
        float closeT = Mathf.InverseLerp(2.5f, 0.8f, dist);
        float anchorY = Mathf.Lerp(0.35f, 0.18f, closeT);
        Vector3 anchorPos = transform.position + Vector3.up * anchorY;

        // In front of camera check
        Vector3 screenPos = cam.WorldToScreenPoint(anchorPos);
        if (screenPos.z <= 0.12f) return;

        EnsureGuiStylesInOnGUI();

        // Responsive font scaling with distance (identical aesthetic to ShelfRestockTarget)
        int responsiveFontSize = Mathf.Clamp(Mathf.RoundToInt(38f / Mathf.Max(1.4f, dist)), 13, 22);
        cachedMainStyle.fontSize = responsiveFontSize;
        cachedShadowStyle.fontSize = responsiveFontSize;

        float guiX = Mathf.Floor(screenPos.x);
        float guiY = Mathf.Floor(Screen.height - screenPos.y);

        // Keep comfortably within screen boundaries when standing close
        if (dist < 2.5f)
        {
            guiX = Mathf.Clamp(guiX, 140f, Screen.width - 140f);
            guiY = Mathf.Clamp(guiY, 32f, Screen.height - 90f);
        }

        // Stepped low-framerate horror jitter
        float steppedTime = Mathf.Floor(Time.unscaledTime * 7f);
        float jitterX = ((int)steppedTime % 3 == 0) ? 1f : (((int)steppedTime % 5 == 0) ? -1f : 0f);
        float jitterY = ((int)steppedTime % 4 == 0) ? 1f : 0f;

        Color horrorTextCol = new Color(0.94f, 0.88f, 0.74f, 0.98f);
        cachedMainStyle.normal.textColor = horrorTextCol;

        string statusText = $"MOP STAINS: {CleanedStainsCount} / {TotalStainsCount}";

        float boxW = 500f;
        float boxH = 45f;
        Rect labelRect = new Rect(guiX - boxW * 0.5f + jitterX, guiY - boxH * 0.5f + jitterY, boxW, boxH);

        // Chunky low-res drop shadow & outline
        GUI.Label(new Rect(labelRect.x - 1f, labelRect.y, labelRect.width, labelRect.height), statusText, cachedShadowStyle);
        GUI.Label(new Rect(labelRect.x + 1f, labelRect.y, labelRect.width, labelRect.height), statusText, cachedShadowStyle);
        GUI.Label(new Rect(labelRect.x, labelRect.y - 1f, labelRect.width, labelRect.height), statusText, cachedShadowStyle);
        GUI.Label(new Rect(labelRect.x, labelRect.y + 1f, labelRect.width, labelRect.height), statusText, cachedShadowStyle);
        GUI.Label(new Rect(labelRect.x + 2f, labelRect.y + 2f, labelRect.width, labelRect.height), statusText, cachedShadowStyle);

        // Main horror label
        GUI.Label(labelRect, statusText, cachedMainStyle);
    }
}

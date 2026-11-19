using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RetroHorrorPOV : MonoBehaviour
{
    public static RetroHorrorPOV Instance { get; private set; }
    [Header("Horror Camera Sway & Breathing (Comfortable & Grounded)")]
    public bool enableBreathingSway = true;
    public float breathingSpeed = 1.15f;
    public float breathingAmount = 0.007f;
    public float bobbingSpeed = 7.5f;
    public float bobbingAmount = 0.014f;
    public float rollSwayAmount = 0f;
    public float fovDistortionPulse = 0f;

    [Header("Balanced VHS Overlays")]
    [Range(0f, 0.3f)]
    public float scanlineIntensity = 0.075f;
    [Range(0f, 0.2f)]
    public float noiseIntensity = 0.025f;
    [Range(0f, 0.5f)]
    public float edgeVignetteIntensity = 0.16f;
    public Color tintColor = new Color(0.92f, 0.96f, 0.94f, 0.018f);

    [Header("VHS Tape Head-Switching Noise (Bottom Screen)")]
    public bool enableVhsTrackingBand = true;
    [Range(0f, 0.6f)]
    public float trackingBandIntensity = 0.20f;

    [Header("VHS Tape Resolution (Soft Analog Feel)")]
    public bool enableLowResPixelation = true;
    [Range(0.4f, 1.0f)]
    public float renderScale = 0.72f;

    [Header("Post-Processing Settings (Balanced VHS Optical Profile)")]
    [Range(-0.3f, 0f)]
    public float lensDistortionIntensity = -0.09f;
    [Range(0f, 1f)]
    public float chromaticAberrationIntensity = 0.42f;
    [Range(0f, 1f)]
    public float filmGrainIntensity = 0.72f;
    [Range(0f, 1f)]
    public float vignetteIntensity = 0.38f;
    [Range(0f, 1f)]
    public float paniniDistortion = 0f;

    [SerializeField]
    private int horrorPresetVersion = 0;

    private Camera targetCamera;
    private Vector3 initialCameraPos;
    private float baseFov = 60f;
    private float swayTimer = 0f;
    private float bobTimer = 0f;

    // Human startle jumpscare shake state
    private float jumpscareTimer = 0f;
    private float jumpscareDuration = 0.68f;
    private bool wasJumpscareShaking = false;

    private Texture2D scanlineTexture;
    private Texture2D[] noiseFrames;
    private int currentNoiseFrameIndex = 0;
    private Texture2D[] trackingFrames;
    private int currentTrackingFrameIndex = 0;
    private Texture2D vignetteTexture;
    private Texture2D tintTexture;
    private DepthOfField activeDepthOfField;
    private float currentRainEyeBlur = 0f;

    public static void EnsureActiveInstance(GameObject playerRoot)
    {
        if (playerRoot == null) return;

        RetroHorrorPOV pov = playerRoot.GetComponent<RetroHorrorPOV>();
        if (pov == null)
        {
            pov = playerRoot.AddComponent<RetroHorrorPOV>();
        }

        Instance = pov;
        pov.enabled = true;
        pov.ApplyFullHorrorFilter();
    }

    public void ApplyFullHorrorFilter()
    {
        UpgradePresetIfNeeded();
        FindTargetCamera();
        EnsurePostProcessingEnabled();
        ApplyHorrorVolumeOverrides();
        ApplyLowResRenderScale();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        UpgradePresetIfNeeded();
        FindTargetCamera();
        EnsurePostProcessingEnabled();
        ApplyHorrorVolumeOverrides();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        UpgradePresetIfNeeded();
        FindTargetCamera();
        EnsurePostProcessingEnabled();
        ApplyHorrorVolumeOverrides();

        if (targetCamera != null)
        {
            initialCameraPos = targetCamera.transform.localPosition;
            targetCamera.fieldOfView = 60f;
            baseFov = 60f;
        }

        ApplyLowResRenderScale();
    }

    private void OnValidate()
    {
        UpgradePresetIfNeeded();
    }

    public void UpgradePresetIfNeeded()
    {
        if (horrorPresetVersion < 4)
        {
            // Balanced VHS configuration: comfortable, non-dizzying, authentic analog tape aesthetic
            enableLowResPixelation = true;
            renderScale = 0.72f;

            scanlineIntensity = 0.075f;
            noiseIntensity = 0.025f;
            edgeVignetteIntensity = 0.16f;
            tintColor = new Color(0.92f, 0.96f, 0.94f, 0.018f);

            enableVhsTrackingBand = true;
            trackingBandIntensity = 0.20f;

            lensDistortionIntensity = -0.09f;
            chromaticAberrationIntensity = 0.42f;
            filmGrainIntensity = 0.72f;
            vignetteIntensity = 0.38f;
            paniniDistortion = 0f;

            breathingSpeed = 1.15f;
            breathingAmount = 0.007f;
            bobbingSpeed = 7.5f;
            bobbingAmount = 0.014f;
            rollSwayAmount = 0f;
            fovDistortionPulse = 0f;

            horrorPresetVersion = 4;
        }
    }

    private void ApplyLowResRenderScale()
    {
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            urpAsset.renderScale = enableLowResPixelation ? Mathf.Clamp(renderScale, 0.4f, 1.0f) : 1.0f;
            urpAsset.upscalingFilter = UpscalingFilterSelection.Auto;
        }
    }

    public void EnsurePostProcessingEnabled()
    {
        if (targetCamera == null) FindTargetCamera();
        if (targetCamera != null)
        {
            var camData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null)
            {
                camData = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            camData.renderPostProcessing = true;
        }
    }

    public void ApplyHorrorVolumeOverrides()
    {
        Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include);
        Scene activeScene = SceneManager.GetActiveScene();
        bool hasActiveGlobalVolume = false;

        foreach (Volume vol in volumes)
        {
            if (vol == null) continue;
            if (vol.isGlobal && vol.enabled && vol.gameObject.activeInHierarchy && vol.gameObject.scene == activeScene)
            {
                hasActiveGlobalVolume = true;
            }

            VolumeProfile profile = Application.isPlaying ? vol.profile : vol.sharedProfile;
            if (profile == null) profile = vol.sharedProfile;
            if (profile == null) continue;

            ConfigureVolumeProfile(profile);
        }

        // Ensure scenes without a placed Global Volume (such as HouseScene) still receive the exact same URP horror filter
        if (!hasActiveGlobalVolume && Application.isPlaying)
        {
            GameObject volGo = new GameObject("Global Volume");
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(volGo, activeScene);
            }

            Volume runtimeVol = volGo.AddComponent<Volume>();
            runtimeVol.isGlobal = true;
            runtimeVol.priority = 0f;
            runtimeVol.weight = 1f;

            VolumeProfile baseProfile = null;
#if UNITY_EDITOR
            baseProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
#endif
            VolumeProfile profile = baseProfile != null
                ? Instantiate(baseProfile)
                : ScriptableObject.CreateInstance<VolumeProfile>();

            ConfigureVolumeProfile(profile);
            runtimeVol.sharedProfile = profile;
        }
    }

    private void ConfigureVolumeProfile(VolumeProfile profile)
    {
        if (profile == null) return;

        if (!profile.TryGet(out Tonemapping tonemapping))
        {
            tonemapping = profile.Add<Tonemapping>(true);
        }
        tonemapping.active = true;
        tonemapping.mode.Override(TonemappingMode.Neutral);
        tonemapping.detectPaperWhite.Override(false);
        tonemapping.paperWhite.Override(234f);
        tonemapping.detectBrightnessLimits.Override(true);
        tonemapping.minNits.Override(0.005f);
        tonemapping.maxNits.Override(647f);

        if (!profile.TryGet(out LensDistortion lensDistortion))
        {
            lensDistortion = profile.Add<LensDistortion>(true);
        }
        lensDistortion.active = true;
        lensDistortion.intensity.Override(lensDistortionIntensity);
        lensDistortion.xMultiplier.Override(1f);
        lensDistortion.yMultiplier.Override(1f);
        lensDistortion.center.Override(new Vector2(0.5f, 0.5f));
        lensDistortion.scale.Override(1.01f);

        if (!profile.TryGet(out ChromaticAberration chromatic))
        {
            chromatic = profile.Add<ChromaticAberration>(true);
        }
        chromatic.active = true;
        chromatic.intensity.Override(chromaticAberrationIntensity);

        if (!profile.TryGet(out FilmGrain grain))
        {
            grain = profile.Add<FilmGrain>(true);
        }
        grain.active = true;
        grain.type.Override(FilmGrainLookup.Large01);
        grain.intensity.Override(filmGrainIntensity);
        grain.response.Override(0.72f);

        if (!profile.TryGet(out Vignette vignette))
        {
            vignette = profile.Add<Vignette>(true);
        }
        vignette.active = true;
        vignette.color.Override(Color.black);
        vignette.intensity.Override(vignetteIntensity);
        vignette.smoothness.Override(0.45f);
        vignette.rounded.Override(false);

        if (profile.TryGet(out PaniniProjection panini))
        {
            panini.active = paniniDistortion > 0.001f;
            panini.distance.Override(paniniDistortion);
            panini.cropToFit.Override(1.0f);
        }

        if (!profile.TryGet(out DepthOfField dof))
        {
            dof = profile.Add<DepthOfField>(true);
        }
        activeDepthOfField = dof;
        ApplyRainEyeBlurToVolume();
    }

    public void SetRainEyeBlur(float blurAmount)
    {
        float clamped = Mathf.Clamp01(blurAmount);
        if (Mathf.Approximately(clamped, currentRainEyeBlur)) return;
        currentRainEyeBlur = clamped;
        ApplyRainEyeBlurToVolume();
    }

    private void ApplyRainEyeBlurToVolume()
    {
        if (activeDepthOfField == null) return;
        if (currentRainEyeBlur <= 0.01f)
        {
            activeDepthOfField.active = false;
            return;
        }

        activeDepthOfField.active = true;
        activeDepthOfField.mode.Override(DepthOfFieldMode.Gaussian);
        activeDepthOfField.gaussianStart.Override(Mathf.Lerp(12.0f, 1.8f, currentRainEyeBlur));
        activeDepthOfField.gaussianEnd.Override(Mathf.Lerp(26.0f, 9.5f, currentRainEyeBlur));
        activeDepthOfField.gaussianMaxRadius.Override(Mathf.Lerp(0.05f, 0.72f, currentRainEyeBlur));
    }

    private void FindTargetCamera()
    {
        if (targetCamera != null && (!targetCamera.enabled || !targetCamera.gameObject.activeInHierarchy))
        {
            targetCamera = null;
        }
        if (targetCamera == null)
        {
            Camera childCam = GetComponentInChildren<Camera>();
            if (childCam != null && childCam.enabled && childCam.gameObject.activeInHierarchy)
            {
                targetCamera = childCam;
            }
        }
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
        if (targetCamera != null)
        {
            initialCameraPos = targetCamera.transform.localPosition;
            baseFov = 60f;
        }
    }

    private void OnDisable()
    {
        if (activeDepthOfField != null)
        {
            activeDepthOfField.active = false;
        }

        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            urpAsset.renderScale = 1.0f;
            urpAsset.upscalingFilter = UpscalingFilterSelection.Auto;
        }
    }

    public void TriggerStartleJumpscareShake(float duration = 0.68f)
    {
        jumpscareDuration = Mathf.Max(0.15f, duration);
        jumpscareTimer = jumpscareDuration;
        wasJumpscareShaking = true;
    }

    private void LateUpdate()
    {
        if (Instance != null && Instance != this) return;
        if (HouseSceneStoryController.IsSleepingSequenceActive) return;

        if (targetCamera == null)
        {
            FindTargetCamera();
            if (targetCamera == null) return;
        }

        if (enableBreathingSway)
        {
            bool isMoving = false;
            var controller = GetComponent<CharacterController>();
            if (controller != null)
            {
                Vector3 horizVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
                isMoving = horizVelocity.magnitude > 0.15f;
            }

            swayTimer += Time.deltaTime * breathingSpeed;
            float swayY = Mathf.Sin(swayTimer) * breathingAmount;
            float swayX = Mathf.Cos(swayTimer * 0.5f) * (breathingAmount * 0.5f);

            if (isMoving)
            {
                bobTimer += Time.deltaTime * bobbingSpeed;
                float bobY = Mathf.Sin(bobTimer) * bobbingAmount;
                float bobX = Mathf.Cos(bobTimer * 0.5f) * (bobbingAmount * 0.45f);
                targetCamera.transform.localPosition = initialCameraPos + new Vector3(swayX + bobX, swayY + bobY, 0f);
            }
            else
            {
                bobTimer = 0f;
                targetCamera.transform.localPosition = Vector3.Lerp(
                    targetCamera.transform.localPosition,
                    initialCameraPos + new Vector3(swayX, swayY, 0f),
                    Time.deltaTime * 4f
                );
            }

            if (rollSwayAmount > 0.001f)
            {
                float rollZ = Mathf.Sin(swayTimer * 0.7f) * rollSwayAmount;
                Vector3 euler = targetCamera.transform.localEulerAngles;
                targetCamera.transform.localEulerAngles = new Vector3(euler.x, euler.y, rollZ);
            }

            if (baseFov > 30f && fovDistortionPulse > 0.001f)
            {
                targetCamera.fieldOfView = baseFov + Mathf.Sin(swayTimer * 0.85f) * fovDistortionPulse;
            }
        }

        // Apply human startle jumpscare camera flinch & shake, then return cleanly to normal state
        if (jumpscareTimer > 0f)
        {
            jumpscareTimer = Mathf.Max(0f, jumpscareTimer - Time.deltaTime);
            float u = Mathf.Clamp01(jumpscareTimer / jumpscareDuration); // 1 -> 0 decay
            float env = u * u;

            float t = Time.unscaledTime;
            float shakeX = (Mathf.Sin(t * 72f) * 0.065f + Mathf.Cos(t * 113f) * 0.032f) * env;
            float flinchUp = Mathf.Sin(Mathf.Clamp01((1f - u) * 3.0f) * Mathf.PI) * 0.055f * u;
            float shakeY = (Mathf.Cos(t * 64f) * 0.068f + Mathf.Sin(t * 97f) * 0.030f) * env + flinchUp;

            targetCamera.transform.localPosition += new Vector3(shakeX, shakeY, 0f);

            Vector3 euler = targetCamera.transform.localEulerAngles;
            float rollShake = Mathf.Sin(t * 54f) * 5.2f * env;
            float pitchJolt = -Mathf.Sin(Mathf.Clamp01((1f - u) * 2.8f) * Mathf.PI) * 3.2f * u;
            targetCamera.transform.localEulerAngles = new Vector3(euler.x + pitchJolt, euler.y, rollShake);

            float fovPunch = Mathf.Sin(Mathf.Clamp01((1f - u) * 2.4f) * Mathf.PI) * 7.5f * u;
            targetCamera.fieldOfView = Mathf.Clamp(baseFov - fovPunch, 46f, 70f);
        }
        else if (wasJumpscareShaking)
        {
            wasJumpscareShaking = false;
            Vector3 euler = targetCamera.transform.localEulerAngles;
            targetCamera.transform.localEulerAngles = new Vector3(euler.x, euler.y, 0f);
            targetCamera.fieldOfView = baseFov > 30f ? baseFov : 60f;
        }
    }

    private void OnGUI()
    {
        if (Instance != null && Instance != this) return;
        if (HorrorMainMenuController.IsMainMenuActive) return;
        if (Event.current.type != EventType.Repaint) return;
        InitTextures();

        float sw = Screen.width;
        float sh = Screen.height;
        Rect screenRect = new Rect(0, 0, sw, sh);

        // 1. Subtle VHS phosphor tint
        if (tintColor.a > 0f && tintTexture != null)
        {
            GUI.color = tintColor;
            GUI.DrawTexture(screenRect, tintTexture);
        }

        // 2. Horizontal CRT / tape scanlines
        if (scanlineIntensity > 0f && scanlineTexture != null)
        {
            GUI.color = new Color(0f, 0f, 0f, scanlineIntensity);
            float scanlineTileY = sh / 3f;
            Rect texCoords = new Rect(0f, 0f, 1f, scanlineTileY);
            GUI.DrawTextureWithTexCoords(screenRect, scanlineTexture, texCoords);
        }

        // 3. Analog tape noise / static snow (cycles pre-baked frames with 0 CPU SetPixel/Apply overhead)
        if (noiseIntensity > 0f && noiseFrames != null && noiseFrames.Length > 0)
        {
            if (Random.value < 0.45f)
            {
                currentNoiseFrameIndex = (currentNoiseFrameIndex + 1) % noiseFrames.Length;
            }
            Texture2D activeNoise = noiseFrames[currentNoiseFrameIndex];
            GUI.color = new Color(1f, 1f, 1f, noiseIntensity);
            float tileX = sw / 96f;
            float tileY = sh / 96f;
            Rect noiseCoords = new Rect(Random.value, Random.value, tileX, tileY);
            GUI.DrawTextureWithTexCoords(screenRect, activeNoise, noiseCoords);
        }

        // 4. VHS bottom tape tracking / head switching noise
        if (enableVhsTrackingBand && trackingFrames != null && trackingFrames.Length > 0 && trackingBandIntensity > 0f)
        {
            if (Random.value < 0.45f)
            {
                currentTrackingFrameIndex = (currentTrackingFrameIndex + 1) % trackingFrames.Length;
            }
            Texture2D activeTracking = trackingFrames[currentTrackingFrameIndex];
            float bandHeight = Mathf.Clamp(sh * 0.022f, 10f, 20f);
            Rect bandRect = new Rect(0, sh - bandHeight, sw, bandHeight);
            GUI.color = new Color(1f, 1f, 1f, trackingBandIntensity * (0.75f + Random.value * 0.25f));
            Rect bandCoords = new Rect(Random.value, 0f, sw / 32f, 1f);
            GUI.DrawTextureWithTexCoords(bandRect, activeTracking, bandCoords);
        }

        // 5. Soft tube corner vignette
        if (edgeVignetteIntensity > 0f && vignetteTexture != null)
        {
            GUI.color = new Color(0f, 0f, 0f, edgeVignetteIntensity);
            GUI.DrawTexture(screenRect, vignetteTexture);
        }

        GUI.color = Color.white;
    }

    private void InitTextures()
    {
        if (scanlineTexture == null)
        {
            scanlineTexture = new Texture2D(1, 4, TextureFormat.RGBA32, false);
            scanlineTexture.wrapMode = TextureWrapMode.Repeat;
            scanlineTexture.filterMode = FilterMode.Point;
            scanlineTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 1f));
            scanlineTexture.SetPixel(0, 1, new Color(0f, 0f, 0f, 0.50f));
            scanlineTexture.SetPixel(0, 2, Color.clear);
            scanlineTexture.SetPixel(0, 3, Color.clear);
            scanlineTexture.Apply();
        }

        // Pre-bake 4 noise frames once to completely avoid per-frame CPU SetPixel/Apply stalls
        if (noiseFrames == null || noiseFrames.Length == 0)
        {
            int res = 64;
            noiseFrames = new Texture2D[4];
            for (int f = 0; f < noiseFrames.Length; f++)
            {
                Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.filterMode = FilterMode.Point;
                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float val = Random.value;
                        tex.SetPixel(x, y, new Color(val, val, val, Random.Range(0.40f, 0.90f)));
                    }
                }
                tex.Apply();
                noiseFrames[f] = tex;
            }
        }

        // Pre-bake 4 tracking band frames once
        if (trackingFrames == null || trackingFrames.Length == 0)
        {
            int w = 64;
            int h = 8;
            trackingFrames = new Texture2D[4];
            for (int f = 0; f < trackingFrames.Length; f++)
            {
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.filterMode = FilterMode.Point;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float val = Random.value > 0.45f ? Random.Range(0.6f, 1f) : 0f;
                        tex.SetPixel(x, y, new Color(val, val, val, val > 0f ? Random.Range(0.5f, 0.9f) : 0f));
                    }
                }
                tex.Apply();
                trackingFrames[f] = tex;
            }
        }

        if (vignetteTexture == null)
        {
            int res = 128;
            vignetteTexture = new Texture2D(res, res, TextureFormat.RGBA32, false);
            vignetteTexture.wrapMode = TextureWrapMode.Clamp;
            vignetteTexture.filterMode = FilterMode.Bilinear;
            float center = (res - 1) * 0.5f;
            float maxDist = Mathf.Sqrt(center * center * 2f);
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;
                    float a = Mathf.SmoothStep(0.24f, 0.96f, d);
                    vignetteTexture.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }
            vignetteTexture.Apply();
        }

        if (tintTexture == null)
        {
            tintTexture = new Texture2D(1, 1);
            tintTexture.SetPixel(0, 0, Color.white);
            tintTexture.Apply();
        }
    }
}

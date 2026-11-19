using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Universal Player & Scene Lifecycle Manager:
/// - Manages a single persistent player instance (DontDestroyOnLoad).
/// - Automatically positions/teleports the player at the active scene's PlayerSpawnPoint.
/// - If testing a scene directly (e.g. hitting Play in HouseScene), instantiates the player prefab.
/// - Guarantees zero duplicate cameras, zero duplicate AudioListeners, and clean single-scene memory.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Player Prefab Reference")]
    public GameObject playerPrefab;

    [Header("Runtime State")]
    public GameObject activePlayer;
    public string nextSpawnId = "Default";
    private bool hasHandledInitialSetup = false;
    private float nextSceneJumpAllowedTime = 0f;

    [Header("Story Progression (5-Act Structure)")]
    [Tooltip("1 = House (Intro), 2 = Gas Station (Shift 1), 3 = House (Mid-Story), 4 = Gas Station (Shift 2), 5 = House (Ending)")]
    public int currentStoryAct = 1;

    public void AdvanceStoryAct()
    {
        currentStoryAct++;
        Debug.Log($"[PlayerManager] Advanced story progression to Act {currentStoryAct}.");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBoot()
    {
        EnsureExists();
    }

    public static PlayerManager EnsureExists()
    {
        if (Instance != null) return Instance;

        PlayerManager existing = Object.FindAnyObjectByType<PlayerManager>();
        if (existing != null)
        {
            Instance = existing;
            DontDestroyOnLoad(Instance.gameObject);
            return Instance;
        }

        GameObject go = new GameObject("PlayerManager");
        Instance = go.AddComponent<PlayerManager>();
        DontDestroyOnLoad(go);
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load prefab if not assigned
        if (playerPrefab == null)
        {
            playerPrefab = Resources.Load<GameObject>("Player/FPS_Player");
        }

        HorrorMainMenuController.EnsureMainMenuShown();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Fallback initial setup if Awake subscribed after initial scene load
        if (!hasHandledInitialSetup)
        {
            HandleScenePlayerSetup();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleScenePlayerSetup();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextSceneJumpAllowedTime) return;

        bool jumpPressed = false;
#if ENABLE_INPUT_SYSTEM
        foreach (var device in InputSystem.devices)
        {
            if (device is Keyboard kb && kb.jKey.wasPressedThisFrame)
            {
                jumpPressed = true;
                break;
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.J))
        {
            jumpPressed = true;
        }
#endif
        if (!jumpPressed) return;

        nextSceneJumpAllowedTime = Time.unscaledTime + 0.35f;
        string currentScene = SceneManager.GetActiveScene().name;
        bool inHouse = string.Equals(currentScene, "HouseScene", System.StringComparison.OrdinalIgnoreCase);
        string targetScene = inHouse ? "GasScene2" : "HouseScene";

        if (inHouse)
        {
            currentStoryAct = (currentStoryAct == 3) ? 4 : 2;
        }
        else
        {
            currentStoryAct = (currentStoryAct <= 2) ? 3 : 1;
        }

        TransitionToScene(targetScene, "Default");
    }

    private void PrepareForSceneJump()
    {
        CabinTvRemoteInteractable.ForceClearForSceneTransition();
        CabinChairInteractable.ResetStaticState();
        CabinKitchenSinkStation.ResetStaticWashState();
        HouseAct3PropController.ResetStaticAct3State();
        SimpleFPSController.IsExternalCameraAnimationActive = false;

        if (InteractableCashierPC.Instance != null && InteractableCashierPC.IsPosScreenOpen)
        {
            InteractableCashierPC.Instance.ClosePosUI();
        }

        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.ClearAllSlots();
        }

        if (activePlayer != null)
        {
            SimpleFPSController fps = activePlayer.GetComponent<SimpleFPSController>();
            Camera cam = fps != null && fps.playerCamera != null
                ? fps.playerCamera
                : activePlayer.GetComponentInChildren<Camera>(true);

            if (cam != null)
            {
                for (int i = cam.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = cam.transform.GetChild(i);
                    if (child == null) continue;
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }

            if (fps != null)
            {
                fps.ResetAfterExternalPose();
            }
        }

        if (!HorrorMainMenuController.IsMainMenuActive)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void TransitionToScene(string sceneName, string spawnId = "Default")
    {
        PrepareForSceneJump();
        nextSpawnId = spawnId;
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    public void HandleScenePlayerSetup()
    {
        hasHandledInitialSetup = true;

        // 0. Ensure clean single-scene isolation:
        // If HouseScene is loaded as the starting scene, set it active and unload GasScene2
        // so GasScene2 objects, lights, and storyline do not run while in HouseScene!
        Scene houseScene = SceneManager.GetSceneByName("HouseScene");
        if (houseScene.IsValid() && houseScene.isLoaded)
        {
            SceneManager.SetActiveScene(houseScene);

            Scene gasScene = SceneManager.GetSceneByName("GasScene2");
            if (gasScene.IsValid() && gasScene.isLoaded)
            {
                Debug.Log("[PlayerManager] Unloading GasScene2 from runtime because player is in HouseScene.");
                SceneManager.UnloadSceneAsync(gasScene);
            }
        }

        Scene activeScene = SceneManager.GetActiveScene();

        // If in HouseScene, purge any stray NightShiftStoryController or Billy from memory
        if (string.Equals(activeScene.name, "HouseScene", System.StringComparison.OrdinalIgnoreCase))
        {
            NightShiftStoryController strayStory = Object.FindAnyObjectByType<NightShiftStoryController>();
            if (strayStory != null)
            {
                strayStory.enabled = false;
                Destroy(strayStory.gameObject);
            }
        }

        // 1. Locate matching spawn point strictly in the ACTIVE SCENE
        PlayerSpawnPoint[] spawnPoints = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Exclude);
        PlayerSpawnPoint targetPoint = null;

        // Pass 1: exact match on spawnId and activeScene
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i].gameObject.scene == activeScene && spawnPoints[i].spawnId == nextSpawnId)
            {
                targetPoint = spawnPoints[i];
                break;
            }
        }

        // Pass 2: any spawn point belonging to the activeScene
        if (targetPoint == null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i].gameObject.scene == activeScene)
                {
                    targetPoint = spawnPoints[i];
                    break;
                }
            }
        }

        // Fallback: If HouseScene but no active spawn found, use hardcoded house coordinates
        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (targetPoint != null)
        {
            spawnPos = targetPoint.transform.position;
            spawnRot = targetPoint.transform.rotation;
        }
        else if (string.Equals(activeScene.name, "HouseScene", System.StringComparison.OrdinalIgnoreCase))
        {
            spawnPos = new Vector3(-22.5f, 0.5f, 314.0f);
            spawnRot = Quaternion.Euler(0f, 102f, 0f);
        }
        else if (string.Equals(activeScene.name, "GasScene2", System.StringComparison.OrdinalIgnoreCase))
        {
            spawnPos = new Vector3(-30.407f, -0.611f, -11.142f);
            spawnRot = Quaternion.Euler(0f, 60.196f, 0f);
        }

        SimpleFPSController[] existingPlayers = Object.FindObjectsByType<SimpleFPSController>(FindObjectsInactive.Include);

        if (activePlayer == null)
        {
            // Adopt an existing player already placed in the active scene (e.g. direct Play in GasScene2)
            SimpleFPSController scenePlayer = FindPlayerInScene(existingPlayers, activeScene);
            if (scenePlayer != null)
            {
                activePlayer = scenePlayer.gameObject;
                if (activePlayer.transform.parent != null)
                {
                    activePlayer.transform.SetParent(null, true);
                }

                if (targetPoint != null && Vector3.Distance(activePlayer.transform.position, spawnPos) > 0.05f)
                {
                    TeleportPlayer(activePlayer, spawnPos, spawnRot);
                }
            }
            else
            {
                // Instantiate from prefab if available
                if (playerPrefab == null)
                {
                    playerPrefab = Resources.Load<GameObject>("Player/FPS_Player");
                }

                if (playerPrefab != null)
                {
                    activePlayer = Instantiate(playerPrefab, spawnPos, spawnRot);
                    activePlayer.name = "Test_FPS_Player";
                }
                else
                {
                    // Fallback: create dynamically if prefab is missing
                    activePlayer = CreateDefaultPlayer(spawnPos, spawnRot);
                }
            }
        }
        else
        {
            // Player already exists - teleport to spawn point
            if (targetPoint != null)
            {
                TeleportPlayer(activePlayer, spawnPos, spawnRot);
            }
        }

        // Remove any extra scene-placed FPS players so duplicate reticles cannot double-toggle doors/items
        RemoveDuplicatePlayers(existingPlayers, activePlayer);

        if (activePlayer != null)
        {
            DontDestroyOnLoad(activePlayer);

            // Re-activate controller camera and single audio listener
            SimpleFPSController fps = activePlayer.GetComponent<SimpleFPSController>();
            if (fps != null)
            {
                fps.EnsurePlayerCameraActive();
            }

            HorrorInteractionReticle.EnsureActiveInstance(activePlayer);
            RetroHorrorPOV.EnsureActiveInstance(activePlayer);
        }

        // Reset nextSpawnId
        nextSpawnId = "Default";

        // Ensure performance optimizations apply to this scene
        NightShiftPerformanceOptimizer.EnsureExists();

        // Boot appropriate story controller based on active scene
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(sceneName, "HouseScene", System.StringComparison.OrdinalIgnoreCase))
        {
            HouseSceneStoryController.EnsureExists();
        }
        else if (string.Equals(sceneName, "GasScene2", System.StringComparison.OrdinalIgnoreCase))
        {
            NightShiftStoryController.EnsureExists();
        }
    }

    private static SimpleFPSController FindPlayerInScene(SimpleFPSController[] candidates, Scene targetScene)
    {
        if (candidates == null) return null;

        SimpleFPSController fallback = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            SimpleFPSController c = candidates[i];
            if (c == null || c.gameObject.scene != targetScene) continue;

            if (c.gameObject.activeInHierarchy)
            {
                return c;
            }
            if (fallback == null)
            {
                fallback = c;
            }
        }
        return fallback;
    }

    private static void TeleportPlayer(GameObject playerObj, Vector3 pos, Quaternion rot)
    {
        if (playerObj == null) return;

        CharacterController cc = playerObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        playerObj.transform.position = pos;
        playerObj.transform.rotation = rot;

        if (cc != null) cc.enabled = true;

        SimpleFPSController fps = playerObj.GetComponent<SimpleFPSController>();
        if (fps != null)
        {
            fps.ResetAfterExternalPose();
        }
    }

    private static void RemoveDuplicatePlayers(SimpleFPSController[] candidates, GameObject keeper)
    {
        if (candidates == null) return;

        for (int i = 0; i < candidates.Length; i++)
        {
            SimpleFPSController c = candidates[i];
            if (c == null || c.gameObject == keeper) continue;

            // Deactivate immediately so duplicate components cannot execute in the current frame
            c.gameObject.SetActive(false);
            Destroy(c.gameObject);
        }
    }

    private GameObject CreateDefaultPlayer(Vector3 pos, Quaternion rot)
    {
        GameObject p = new GameObject("Test_FPS_Player");
        p.transform.position = pos;
        p.transform.rotation = rot;

        CharacterController cc = p.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.35f;

        SimpleFPSController fps = p.AddComponent<SimpleFPSController>();
        fps.walkSpeed = 4.5f;
        fps.sprintSpeed = 7.5f;
        fps.canJump = false;
        fps.mouseSensitivity = 2.0f;

        p.AddComponent<HorrorInteractionReticle>();
        p.AddComponent<RetroHorrorPOV>();
        p.AddComponent<HorrorAmbience>();

        GameObject camObj = new GameObject("PlayerCamera");
        camObj.transform.SetParent(p.transform, false);
        camObj.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        camObj.transform.localRotation = Quaternion.identity;
        camObj.tag = "MainCamera";

        Camera cam = camObj.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 65f;
        camObj.AddComponent<AudioListener>();

        fps.playerCamera = cam;
        return p;
    }
}

/// <summary>
/// Eerie Main Menu shown as the very first screen when the game launches.
/// Features:
/// - Uses the gas station night screenshot as the background, enhanced with an eerie horror color grade,
///   vignette, slow breathing camera drift, dying fluorescent light flicker, drifting fog/rain streaks, and VHS scanlines.
/// - "Start" button: Starts the game at Scene 1 (HouseScene - Act 1).
/// - "Exit" button: Exits the game (both in standalone builds and Unity Editor Play Mode).
/// </summary>
public class HorrorMainMenuController : MonoBehaviour
{
    public static HorrorMainMenuController Instance { get; private set; }

    private static bool hasStartedGameFromMainMenu = false;
    public static bool IsMainMenuActive => Instance != null && Instance.isMenuOpen;

    private bool isMenuOpen = true;
    private bool isTransitioningToStart = false;
    private float startFadeOutAlpha = 0f;
    private float menuOpenTime = 0f;

    private Texture2D eerieBgTexture;
    private Texture2D titleLogoTexture;
    private Texture2D topSkyShadeTexture;
    private Texture2D pixelTexture;
    private Texture2D vignetteTexture;
    private Texture2D scanlineTexture;
    private Texture2D fogNoiseTexture;
    private Texture2D leftShadeTexture;
    private Texture2D staticNoiseTexture;

    private Font vhsMenuFont;
    private GUIStyle vhsButtonCoreStyle;
    private GUIStyle vhsButtonBlueStyle;
    private GUIStyle vhsButtonRedStyle;
    private GUIStyle vhsButtonGlowStyle;
    private GUIStyle vhsButtonShadowStyle;

    private int hoveredButtonIndex = -1;
    private int keyboardSelectedIndex = 0;
    private int lastHoveredIndex = -1;

    private AudioSource ambientSource;
    private AudioSource uiSfxSource;
    private AudioClip eerieDroneClip;
    private AudioClip hoverTickClip;
    private AudioClip startClickClip;
    private AudioClip glitchBurstClip;

    private struct RainStreak
    {
        public float x;
        public float y;
        public float speed;
        public float length;
        public float alpha;
    }

    private struct GlitchSlice
    {
        public float fracStart;
        public float fracEnd;
        public float offsetX;
        public float chromaSplit;
        public float brightness;
        public bool dropOut;
    }

    private struct GlitchLine
    {
        public float yNorm;
        public float heightPx;
        public float xNorm;
        public float widthNorm;
        public float alpha;
        public bool isRed;
    }

    private RainStreak[] rainStreaks;
    private GlitchSlice[] titleGlitchSlices;
    private int activeTitleSliceCount = 0;
    private GlitchSlice[] bgGlitchSlices;
    private int activeBgSliceCount = 0;
    private GlitchLine[] vhsGlitchLines;
    private int activeGlitchLineCount = 0;

    private float flickerMultiplier = 1f;
    private float nextFlickerTime = 0f;
    private float glitchEndTime = 0f;
    private float nextGlitchTime = 1.6f;
    private float microGlitchEndTime = 0f;
    private float nextMicroGlitchTime = 0.75f;
    private float hoverGlitchEndTime = 0f;
    private float nextGlitchFrameStepTime = 0f;
    private float currentGlitchStrength = 0f;
    private float titleWholeJitterX = 0f;
    private float titleWholeJitterY = 0f;
    private float trackingBarY = 0.22f;
    private float staticUvOffsetX = 0f;
    private float staticUvOffsetY = 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnSubsystemRegistration()
    {
        Instance = null;
        hasStartedGameFromMainMenu = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeFirstSceneLoad()
    {
        Instance = null;
        hasStartedGameFromMainMenu = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterFirstSceneLoad()
    {
        if (!hasStartedGameFromMainMenu)
        {
            EnsureMainMenuShown();
        }
    }

    public static HorrorMainMenuController EnsureMainMenuShown()
    {
        if (hasStartedGameFromMainMenu) return Instance;

        if (Instance != null)
        {
            return Instance;
        }

        HorrorMainMenuController existing = Object.FindAnyObjectByType<HorrorMainMenuController>();
        if (existing != null)
        {
            Instance = existing;
            DontDestroyOnLoad(Instance.gameObject);
            return Instance;
        }

        GameObject go = new GameObject("HorrorMainMenuController");
        Instance = go.AddComponent<HorrorMainMenuController>();
        DontDestroyOnLoad(go);
        return Instance;
    }

    public static void ShowMainMenu()
    {
        hasStartedGameFromMainMenu = false;
        HorrorMainMenuController ctrl = EnsureMainMenuShown();
        if (ctrl != null)
        {
            ctrl.isMenuOpen = true;
            ctrl.isTransitioningToStart = false;
            ctrl.startFadeOutAlpha = 0f;
            ctrl.menuOpenTime = Time.unscaledTime;
            if (ctrl.ambientSource != null && !ctrl.ambientSource.isPlaying)
            {
                ctrl.ambientSource.volume = 0.55f;
                ctrl.ambientSource.Play();
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        isMenuOpen = !hasStartedGameFromMainMenu;
        menuOpenTime = Time.unscaledTime;

        titleGlitchSlices = new GlitchSlice[10];
        bgGlitchSlices = new GlitchSlice[6];
        vhsGlitchLines = new GlitchLine[10];

        InitTextures();
        InitRainParticles();
        InitAudio();
    }

    private void InitTextures()
    {
        pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        pixelTexture.SetPixel(0, 0, Color.white);
        pixelTexture.Apply();

        eerieBgTexture = LoadAndEnhanceBackgroundScreenshot();
        titleLogoTexture = LoadTitleLogoTexture();
        topSkyShadeTexture = BuildTopSkyGradientTexture(16, 256);
        vignetteTexture = BuildRadialVignetteTexture(256, 256);
        scanlineTexture = BuildScanlineTexture(4, 256);
        fogNoiseTexture = BuildEerieFogTexture(256, 128);
        leftShadeTexture = BuildLeftSidebarGradientTexture(256, 16);
        staticNoiseTexture = BuildAnalogStaticTexture(128, 128);
    }

    private Texture2D LoadTitleLogoTexture()
    {
        string filePath = Path.Combine(Application.dataPath, "Resources/MainMenu/NightShiftTitleLogo.png");
        if (File.Exists(filePath))
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes != null && bytes.Length > 0)
            {
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.LoadImage(bytes);
                return tex;
            }
        }

        Texture2D resTex = Resources.Load<Texture2D>("MainMenu/NightShiftTitleLogo");
        if (resTex != null) return resTex;
        return null;
    }

    private static Texture2D BuildTopSkyGradientTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        // y = h-1 is top of texture (darkest behind the slasher logo), fading smoothly into the eerie gas station below
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            float a = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01((v - 0.34f) / 0.56f)) * 0.88f;
            Color c = new Color(0.01f, 0.01f, 0.015f, a);
            for (int x = 0; x < w; x++)
            {
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    private Texture2D LoadAndEnhanceBackgroundScreenshot()
    {
        Texture2D rawTex = null;

        // 1. Load directly from PNG file bytes first so we always have readable pixels and immediate availability
        string filePath = Path.Combine(Application.dataPath, "Resources/MainMenu/MainMenuBackground.png");
        if (File.Exists(filePath))
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes != null && bytes.Length > 0)
            {
                rawTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                rawTex.LoadImage(bytes);
            }
        }

        // 2. Fallback to Resources.Load for standalone builds
        if (rawTex == null)
        {
            Texture2D resTex = Resources.Load<Texture2D>("MainMenu/MainMenuBackground");
            if (resTex != null)
            {
                rawTex = DuplicateReadableTexture(resTex);
            }
        }

        if (rawTex == null)
        {
            rawTex = new Texture2D(512, 256, TextureFormat.RGBA32, false);
            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 512; x++)
                {
                    rawTex.SetPixel(x, y, new Color(0.02f, 0.03f, 0.04f, 1f));
                }
            }
            rawTex.Apply();
            return rawTex;
        }

        // Enhance the screenshot into a more eerie, unsettling midnight horror atmosphere:
        // - Deepen cold shadows, add eerie desaturated moonlight cyan/crimson undertone,
        // - Amplify the haunting isolated glow of the distant gas station windows & fuel pumps,
        // - Bake subtle analog film grain & dark edge falloff.
        int w = rawTex.width;
        int h = rawTex.height;
        Color[] pixels = rawTex.GetPixels();

        for (int y = 0; y < h; y++)
        {
            float v = y / (float)Mathf.Max(1, h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)Mathf.Max(1, w - 1);
                int idx = y * w + x;
                Color c = pixels[idx];

                float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

                // Desaturate slightly for bleak psychological horror tone while preserving gas station clarity
                Color desat = Color.Lerp(new Color(lum, lum, lum, 1f), c, 0.72f);

                // Subtle eerie contrast boost that keeps the dark pumps and forecourt clearly visible
                float r = Mathf.Pow(Mathf.Clamp01(desat.r), 1.06f);
                float g = Mathf.Pow(Mathf.Clamp01(desat.g), 1.05f);
                float b = Mathf.Pow(Mathf.Clamp01(desat.b), 1.02f);

                // Eerie color grading: cold dead-of-night moonlight shadows + haunting sickly fluorescent glow on the gas station
                if (lum < 0.22f)
                {
                    r = Mathf.Clamp01(r * 0.94f);
                    g = Mathf.Clamp01(g * 0.98f + 0.008f);
                    b = Mathf.Clamp01(b * 1.10f + 0.018f);
                }
                else
                {
                    r = Mathf.Clamp01(r * 1.22f);
                    g = Mathf.Clamp01(g * 1.18f);
                    b = Mathf.Clamp01(b * 0.95f);
                }

                // Subtle organic analog film grain & horizontal VHS scanline modulation
                float grain = (Mathf.PerlinNoise(u * 320f + 7.1f, v * 180f + 3.4f) - 0.5f) * 0.028f;
                float scan = (y % 2 == 0) ? 0.95f : 1.03f;

                // Soft outer vignette framing the distant gas station without hiding it
                float dx = (u - 0.50f) * 1.65f;
                float dy = (v - 0.50f) * 1.75f;
                float rad = Mathf.Sqrt(dx * dx + dy * dy);
                float edgeDarken = Mathf.Clamp01(1.0f - Mathf.Pow(Mathf.Max(0f, rad - 0.42f), 1.6f) * 0.52f);

                r = Mathf.Clamp01((r + grain) * scan * edgeDarken);
                g = Mathf.Clamp01((g + grain) * scan * edgeDarken);
                b = Mathf.Clamp01((b + grain) * scan * edgeDarken);

                pixels[idx] = new Color(r, g, b, 1f);
            }
        }

        Texture2D enhanced = new Texture2D(w, h, TextureFormat.RGBA32, false);
        enhanced.filterMode = FilterMode.Bilinear;
        enhanced.wrapMode = TextureWrapMode.Clamp;
        enhanced.SetPixels(pixels);
        enhanced.Apply();
        return enhanced;
    }

    private static Texture2D DuplicateReadableTexture(Texture2D source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readable.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return readable;
    }

    private static Texture2D BuildRadialVignetteTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < h; y++)
        {
            float ny = (y / (float)(h - 1) - 0.5f) * 2f;
            for (int x = 0; x < w; x++)
            {
                float nx = (x / (float)(w - 1) - 0.5f) * 2f;
                float dist = Mathf.Sqrt(nx * nx * 0.92f + ny * ny * 1.08f);
                float a = Mathf.SmoothStep(0.55f, 1.38f, dist);
                a = Mathf.Pow(a, 1.45f) * 0.86f;
                tex.SetPixel(x, y, new Color(0.01f, 0.0f, 0.0f, Mathf.Clamp01(a)));
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildScanlineTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;

        for (int y = 0; y < h; y++)
        {
            float a = (y % 2 == 0) ? 0.22f : 0.0f;
            Color c = new Color(0f, 0f, 0f, a);
            for (int x = 0; x < w; x++)
            {
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildAnalogStaticTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;

        System.Random rng = new System.Random(1987);
        for (int y = 0; y < h; y++)
        {
            float rowStreak = (float)(rng.NextDouble() * 0.25);
            for (int x = 0; x < w; x++)
            {
                float v = (float)rng.NextDouble();
                float lum = Mathf.Clamp01(v * 0.78f + rowStreak);
                // Occasional faint chroma VHS snow speck
                float r = lum;
                float g = lum;
                float b = lum;
                if (v > 0.965f)
                {
                    r = 1.0f;
                    g = 0.25f;
                    b = 0.22f;
                }
                else if (v < 0.03f)
                {
                    r = 0.30f;
                    g = 0.90f;
                    b = 0.85f;
                }
                float alpha = (v > 0.62f || v < 0.18f) ? Mathf.Lerp(0.18f, 0.85f, Mathf.Abs(v - 0.5f) * 2f) : 0.08f;
                tex.SetPixel(x, y, new Color(r, g, b, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildEerieFogTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;

        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            float verticalMask = Mathf.Sin(Mathf.Clamp01(v) * Mathf.PI) * Mathf.Clamp01(1.3f - v * 0.9f);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                float n1 = Mathf.PerlinNoise(u * 4.0f + 1.3f, v * 3.0f + 2.7f);
                float n2 = Mathf.PerlinNoise(u * 9.0f + 5.1f, v * 6.0f + 8.4f) * 0.5f;
                float f = Mathf.Clamp01((n1 + n2) / 1.5f - 0.25f);
                float alpha = f * verticalMask * 0.22f;
                tex.SetPixel(x, y, new Color(0.55f, 0.62f, 0.68f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    private void InitRainParticles()
    {
        int count = 56;
        rainStreaks = new RainStreak[count];
        for (int i = 0; i < count; i++)
        {
            rainStreaks[i] = new RainStreak
            {
                x = Random.value,
                y = Random.value,
                speed = Random.Range(0.75f, 1.35f),
                length = Random.Range(24f, 54f),
                alpha = Random.Range(0.06f, 0.16f)
            };
        }
    }

    private void InitAudio()
    {
        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.loop = true;
        ambientSource.spatialBlend = 0f;
        ambientSource.volume = 0.55f;
        ambientSource.playOnAwake = false;
        ambientSource.ignoreListenerPause = true;

        uiSfxSource = gameObject.AddComponent<AudioSource>();
        uiSfxSource.loop = false;
        uiSfxSource.spatialBlend = 0f;
        uiSfxSource.volume = 0.80f;
        uiSfxSource.playOnAwake = false;
        uiSfxSource.ignoreListenerPause = true;

        int sr = 44100;

        int droneLen = sr * 6;
        float[] droneBuf = new float[droneLen];
        float lpNoise = 0f;
        for (int i = 0; i < droneLen; i++)
        {
            float t = i / (float)sr;
            float loopPhase = (t / 6.0f) * Mathf.PI * 2f;

            float sub1 = Mathf.Sin(2f * Mathf.PI * 45f * t);
            float sub2 = Mathf.Sin(2f * Mathf.PI * 67.5f * t + Mathf.Sin(loopPhase) * 1.5f) * 0.45f;
            float eerieChoir = Mathf.Sin(2f * Mathf.PI * 135f * t + Mathf.Cos(loopPhase * 2f) * 0.8f) * 0.18f;

            float humMod = 0.65f + 0.35f * Mathf.Sin(loopPhase * 3f);
            float buzz = (Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.15f +
                          Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.08f +
                          Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.04f) * humMod;

            float white = Random.Range(-1f, 1f);
            float cutoff = 0.018f + 0.010f * Mathf.Sin(loopPhase);
            lpNoise += cutoff * (white - lpNoise);

            droneBuf[i] = Mathf.Clamp((sub1 * 0.35f + sub2 + eerieChoir + buzz + lpNoise * 0.65f) * 0.42f, -1f, 1f);
        }
        eerieDroneClip = AudioClip.Create("MainMenu_EerieDrone", droneLen, 1, sr, false);
        eerieDroneClip.SetData(droneBuf, 0);

        int tickLen = (int)(sr * 0.055f);
        float[] tickBuf = new float[tickLen];
        for (int i = 0; i < tickLen; i++)
        {
            float t = i / (float)sr;
            float env = Mathf.Exp(-t * 95f);
            tickBuf[i] = (Mathf.Sin(2f * Mathf.PI * 420f * t) * 0.55f + Random.Range(-0.25f, 0.25f)) * env * 0.5f;
        }
        hoverTickClip = AudioClip.Create("MainMenu_HoverTick", tickLen, 1, sr, false);
        hoverTickClip.SetData(tickBuf, 0);

        int startLen = (int)(sr * 0.65f);
        float[] startBuf = new float[startLen];
        for (int i = 0; i < startLen; i++)
        {
            float t = i / (float)sr;
            float env = Mathf.Exp(-t * 6.5f);
            float impact = Mathf.Sin(2f * Mathf.PI * (82f - t * 48f) * t) * 0.75f;
            float metallic = Mathf.Sin(2f * Mathf.PI * 218f * t) * Mathf.Exp(-t * 14f) * 0.25f;
            startBuf[i] = Mathf.Clamp((impact + metallic) * env, -1f, 1f);
        }
        startClickClip = AudioClip.Create("MainMenu_StartThud", startLen, 1, sr, false);
        startClickClip.SetData(startBuf, 0);

        // Short procedural VHS static / tape-head crackle burst for menu glitches
        int glitchLen = (int)(sr * 0.22f);
        float[] glitchBuf = new float[glitchLen];
        float hpPrev = 0f;
        for (int i = 0; i < glitchLen; i++)
        {
            float t = i / (float)sr;
            float env = Mathf.Sin(Mathf.Clamp01(t / 0.22f) * Mathf.PI) * (0.65f + 0.35f * Mathf.Sin(t * 210f));
            float rawNoise = Random.Range(-1f, 1f);
            float highPass = rawNoise - hpPrev * 0.72f;
            hpPrev = rawNoise;
            float buzz60 = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.22f + Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.14f;
            float crackle = (Random.value > 0.94f) ? Random.Range(-1f, 1f) * 0.75f : 0f;
            glitchBuf[i] = Mathf.Clamp((highPass * 0.42f + buzz60 + crackle) * env * 0.52f, -1f, 1f);
        }
        glitchBurstClip = AudioClip.Create("MainMenu_VhsGlitch", glitchLen, 1, sr, false);
        glitchBurstClip.SetData(glitchBuf, 0);

        if (isMenuOpen)
        {
            ambientSource.clip = eerieDroneClip;
            ambientSource.Play();
        }
    }

    private void Update()
    {
        if (!isMenuOpen) return;

        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        if (rainStreaks != null)
        {
            for (int i = 0; i < rainStreaks.Length; i++)
            {
                rainStreaks[i].y += rainStreaks[i].speed * dt;
                rainStreaks[i].x -= rainStreaks[i].speed * 0.08f * dt;
                if (rainStreaks[i].y > 1.08f)
                {
                    rainStreaks[i].y = -0.08f;
                    rainStreaks[i].x = Random.value;
                }
                if (rainStreaks[i].x < -0.05f)
                {
                    rainStreaks[i].x += 1.10f;
                }
            }
        }

        if (now >= nextFlickerTime)
        {
            if (Random.value < 0.24f)
            {
                flickerMultiplier = Random.Range(0.62f, 0.88f);
                nextFlickerTime = now + Random.Range(0.03f, 0.09f);
            }
            else
            {
                flickerMultiplier = Random.Range(0.95f, 1.03f);
                nextFlickerTime = now + Random.Range(0.12f, 0.58f);
            }
        }

        // Slowly roll analog VHS tracking distortion bar down the screen
        trackingBarY += dt * 0.11f;
        if (trackingBarY > 1.15f)
        {
            trackingBarY = -0.15f;
        }

        // Schedule frequent short VHS micro-glitches (subtle tape hiccups)
        if (now >= nextMicroGlitchTime)
        {
            microGlitchEndTime = now + Random.Range(0.05f, 0.12f);
            nextMicroGlitchTime = now + Random.Range(0.95f, 2.35f);
        }

        // Schedule dramatic heavy VHS glitch bursts (multi-strip title tear + static burst + audio)
        if (now >= nextGlitchTime)
        {
            TriggerHeavyVhsGlitch(Random.Range(0.16f, 0.34f), true);
            nextGlitchTime = now + Random.Range(2.6f, 5.4f);
        }

        bool isHeavyGlitch = (now < glitchEndTime) || isTransitioningToStart;
        bool isMicroGlitch = (now < microGlitchEndTime) || (now < hoverGlitchEndTime);
        float targetStrength = isTransitioningToStart ? 1.15f : (isHeavyGlitch ? 1.0f : (isMicroGlitch ? 0.42f : 0f));
        currentGlitchStrength = targetStrength;

        // Step glitch geometry and VHS static grain at an authentic ~22 FPS tape cadence
        if (now >= nextGlitchFrameStepTime)
        {
            nextGlitchFrameStepTime = now + (isHeavyGlitch ? 0.038f : 0.048f);
            staticUvOffsetX = Random.value;
            staticUvOffsetY = Random.value;

            if (targetStrength > 0.01f)
            {
                RebuildGlitchFrame(targetStrength, isHeavyGlitch);
                if (isHeavyGlitch && Random.value < 0.35f)
                {
                    trackingBarY = Random.Range(0.05f, 0.92f);
                }
            }
            else
            {
                activeTitleSliceCount = 0;
                activeBgSliceCount = 0;
                activeGlitchLineCount = 0;
                titleWholeJitterX = 0f;
                titleWholeJitterY = 0f;
            }
        }

        if (isTransitioningToStart) return;

        if (WasUpPressedThisFrame())
        {
            keyboardSelectedIndex = 0;
            PlayHoverSound(0);
        }
        else if (WasDownPressedThisFrame())
        {
            keyboardSelectedIndex = 1;
            PlayHoverSound(1);
        }
        else if (WasSubmitPressedThisFrame())
        {
            int activeIdx = hoveredButtonIndex >= 0 ? hoveredButtonIndex : keyboardSelectedIndex;
            if (activeIdx == 0)
            {
                OnStartClicked();
            }
            else
            {
                OnExitClicked();
            }
        }
    }

    private void TriggerHeavyVhsGlitch(float duration, bool playSfx)
    {
        float now = Time.unscaledTime;
        glitchEndTime = Mathf.Max(glitchEndTime, now + duration);
        nextGlitchFrameStepTime = 0f; // Force immediate slice generation
        if (playSfx && uiSfxSource != null && glitchBurstClip != null)
        {
            uiSfxSource.pitch = Random.Range(0.84f, 1.24f);
            uiSfxSource.PlayOneShot(glitchBurstClip, Random.Range(0.22f, 0.36f));
        }
    }

    private void RebuildGlitchFrame(float strength, bool heavy)
    {
        if (titleGlitchSlices == null || bgGlitchSlices == null || vhsGlitchLines == null) return;

        // 1. Horizontal VHS tape-tear slices across the NIGHTSHIFT title logo
        int sliceCount = heavy ? Random.Range(5, 9) : Random.Range(3, 5);
        sliceCount = Mathf.Clamp(sliceCount, 1, titleGlitchSlices.Length);
        activeTitleSliceCount = sliceCount;

        titleWholeJitterX = heavy ? Random.Range(-5.5f, 5.5f) * strength : Random.Range(-1.8f, 1.8f);
        titleWholeJitterY = heavy ? Random.Range(-2.5f, 2.5f) * strength : 0f;

        float cursor = 0f;
        for (int i = 0; i < sliceCount; i++)
        {
            float remaining = 1f - cursor;
            float sliceFrac = (i == sliceCount - 1)
                ? remaining
                : Mathf.Clamp(remaining / (sliceCount - i) * Random.Range(0.45f, 1.55f), 0.045f, remaining - 0.02f * (sliceCount - 1 - i));
            float nextCursor = (i == sliceCount - 1) ? 1f : Mathf.Clamp01(cursor + sliceFrac);

            bool displaced = heavy ? (Random.value < 0.72f) : (Random.value < 0.42f);
            float maxShift = heavy ? 42f * strength : 16f * strength;
            float shift = displaced ? Random.Range(-maxShift, maxShift) : Random.Range(-2.0f, 2.0f);

            // Occasional dramatic long horizontal VHS tear ribbon
            if (heavy && Random.value < 0.22f)
            {
                shift *= 1.65f;
            }

            titleGlitchSlices[i] = new GlitchSlice
            {
                fracStart = cursor,
                fracEnd = nextCursor,
                offsetX = shift,
                chromaSplit = displaced ? Random.Range(4.5f, heavy ? 13.5f : 7.5f) * strength : 2.2f,
                brightness = displaced ? Random.Range(0.75f, 1.18f) : 1.0f,
                dropOut = heavy && Random.value < 0.11f
            };
            cursor = nextCursor;
        }

        // 2. Background horizontal tear strips
        activeBgSliceCount = heavy ? Random.Range(3, 6) : Random.Range(1, 3);
        activeBgSliceCount = Mathf.Clamp(activeBgSliceCount, 0, bgGlitchSlices.Length);
        for (int i = 0; i < activeBgSliceCount; i++)
        {
            float f0 = Random.Range(0.02f, 0.90f);
            float fh = Random.Range(0.015f, heavy ? 0.075f : 0.032f);
            bgGlitchSlices[i] = new GlitchSlice
            {
                fracStart = f0,
                fracEnd = Mathf.Min(1f, f0 + fh),
                offsetX = Random.Range(-36f, 36f) * strength,
                chromaSplit = Random.Range(4f, 12f) * strength,
                brightness = Random.Range(0.85f, 1.25f),
                dropOut = false
            };
        }

        // 3. Sharp analog VHS signal dropout / static tracking streaks across screen
        activeGlitchLineCount = heavy ? Random.Range(4, 10) : Random.Range(1, 4);
        activeGlitchLineCount = Mathf.Clamp(activeGlitchLineCount, 0, vhsGlitchLines.Length);
        for (int i = 0; i < activeGlitchLineCount; i++)
        {
            vhsGlitchLines[i] = new GlitchLine
            {
                yNorm = Random.Range(0.02f, 0.98f),
                heightPx = Random.Range(1.2f, heavy ? 4.5f : 2.2f),
                xNorm = Random.Range(-0.05f, 0.45f),
                widthNorm = Random.Range(0.28f, 1.05f),
                alpha = Random.Range(0.14f, heavy ? 0.48f : 0.24f),
                isRed = Random.value < 0.28f
            };
        }
    }

    private void PlayHoverSound(int index)
    {
        if (lastHoveredIndex == index) return;
        lastHoveredIndex = index;
        if (index >= 0)
        {
            hoverGlitchEndTime = Time.unscaledTime + 0.11f;
            nextGlitchFrameStepTime = 0f;
            if (uiSfxSource != null && hoverTickClip != null)
            {
                uiSfxSource.pitch = index == 0 ? 1.02f : 0.90f;
                uiSfxSource.PlayOneShot(hoverTickClip, 0.65f);
            }
        }
    }

    public void OnStartClicked()
    {
        if (isTransitioningToStart) return;
        StartCoroutine(StartGameSequenceRoutine());
    }

    private IEnumerator StartGameSequenceRoutine()
    {
        isTransitioningToStart = true;
        TriggerHeavyVhsGlitch(0.45f, true);

        if (uiSfxSource != null && startClickClip != null)
        {
            uiSfxSource.pitch = 1.0f;
            uiSfxSource.PlayOneShot(startClickClip, 0.95f);
        }

        float dur = 0.85f;
        float elapsed = 0f;
        float startVol = ambientSource != null ? ambientSource.volume : 0.55f;

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(elapsed / dur);
            startFadeOutAlpha = Mathf.SmoothStep(0f, 1f, u);
            if (ambientSource != null)
            {
                ambientSource.volume = Mathf.Lerp(startVol, 0f, u);
            }
            yield return null;
        }

        if (ambientSource != null)
        {
            ambientSource.Stop();
        }

        // Clear any forced Act 3 / Act 5 flags and start strictly at Scene 1 (HouseScene - Act 1)
        PlayerPrefs.DeleteKey("HouseScene_ForceAct3");
        PlayerPrefs.DeleteKey("HouseScene_ForceAct5");
        PlayerPrefs.Save();

        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.currentStoryAct = 1;
        }

        hasStartedGameFromMainMenu = true;
        isMenuOpen = false;
        isTransitioningToStart = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Scene activeScene = SceneManager.GetActiveScene();
        bool alreadyInHouseScene = string.Equals(activeScene.name, "HouseScene", System.StringComparison.OrdinalIgnoreCase);

        if (!alreadyInHouseScene ||
            (HouseSceneStoryController.Instance != null && (HouseSceneStoryController.Instance.isAct3Active || HouseSceneStoryController.Instance.isAct5Active)))
        {
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.currentStoryAct = 1;
                PlayerManager.Instance.TransitionToScene("HouseScene", "Default");
            }
            else
            {
                SceneManager.LoadScene("HouseScene", LoadSceneMode.Single);
            }
        }
    }

    public void OnExitClicked()
    {
        if (isTransitioningToStart) return;

        if (uiSfxSource != null && startClickClip != null)
        {
            uiSfxSource.PlayOneShot(startClickClip, 0.85f);
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static Texture2D BuildLeftSidebarGradientTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int x = 0; x < w; x++)
        {
            float u = x / (float)(w - 1);
            float a = Mathf.Pow(1f - Mathf.SmoothStep(0.0f, 1.0f, u), 1.25f) * 0.90f;
            Color c = new Color(0.01f, 0.012f, 0.012f, a);
            for (int y = 0; y < h; y++)
            {
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildBloodyTitleBackdropTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        // Deterministic organic hanging blood drips under the title line
        float[] dripU = { 0.05f, 0.11f, 0.18f, 0.24f, 0.33f, 0.41f, 0.48f, 0.57f, 0.65f, 0.74f, 0.82f, 0.90f };
        float[] dripLen = { 0.36f, 0.56f, 0.28f, 0.64f, 0.42f, 0.74f, 0.30f, 0.58f, 0.46f, 0.68f, 0.34f, 0.48f };
        float[] dripRad = { 0.009f, 0.011f, 0.008f, 0.012f, 0.009f, 0.013f, 0.008f, 0.011f, 0.010f, 0.011f, 0.008f, 0.009f };

        Color deepClot = new Color(0.16f, 0.006f, 0.006f, 0.96f);
        Color arterialRed = new Color(0.46f, 0.022f, 0.018f, 0.94f);
        Color brightBlood = new Color(0.64f, 0.040f, 0.030f, 0.92f);

        for (int y = 0; y < h; y++)
        {
            // Note: Texture2D y=0 is bottom, y=h-1 is top
            float v = y / (float)(h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);

                float n1 = Mathf.PerlinNoise(u * 8.5f + 4.2f, v * 7.5f + 1.9f);
                float n2 = Mathf.PerlinNoise(u * 24f + 9.1f, v * 22f + 6.4f) * 0.5f;
                float n3 = Mathf.PerlinNoise(u * 56f + 2.7f, v * 48f + 11.3f) * 0.25f;
                float fbm = (n1 + n2 + n3) / 1.75f;

                float alpha = 0f;
                float clotFactor = 0f;

                // 1. Wide organic bloody smear behind the top letters (v = 0.58 .. 0.92)
                float smearBandDist = Mathf.Abs(v - 0.74f) / 0.19f;
                float horizFade = Mathf.Clamp01(1f - Mathf.Pow(Mathf.Abs(u - 0.44f) / 0.52f, 2.2f));
                float smearVal = (1f - smearBandDist) * horizFade + (fbm - 0.48f) * 0.65f;
                if (smearVal > 0.26f)
                {
                    float a = Mathf.Clamp01((smearVal - 0.26f) * 2.2f) * 0.58f;
                    alpha = Mathf.Max(alpha, a);
                    clotFactor = Mathf.Max(clotFactor, 1f - smearBandDist);
                }

                // 2. Jagged horizontal bloody slash line beneath the title (v ~ 0.60)
                float slashY = 0.60f + (n1 - 0.5f) * 0.045f;
                float slashThick = Mathf.Lerp(0.028f, 0.008f, u) * (0.65f + fbm * 0.7f);
                float slashDist = Mathf.Abs(v - slashY) / Mathf.Max(0.003f, slashThick);
                if (slashDist < 1.0f && u > 0.01f && u < 0.97f)
                {
                    float a = Mathf.Clamp01((1f - slashDist) * 2.6f) * Mathf.Clamp01(1.08f - u * 0.45f);
                    alpha = Mathf.Max(alpha, a * 0.94f);
                    clotFactor = Mathf.Max(clotFactor, 0.85f);
                }

                // 3. Realistic viscous hanging blood drips with bulbous tear-drop tips
                for (int d = 0; d < dripU.Length; d++)
                {
                    float topV = 0.61f + (d % 3) * 0.03f;
                    float botV = Mathf.Max(0.04f, topV - dripLen[d] * 0.72f);
                    if (v <= topV && v >= botV - 0.035f)
                    {
                        float tDrip = Mathf.Clamp01((topV - v) / Mathf.Max(0.01f, topV - botV));
                        float wobble = (Mathf.PerlinNoise(v * 14f + d * 3.7f, d * 5.1f) - 0.5f) * 0.007f;
                        // Thin neck in middle, bulbous glistening blood droplet at tip
                        float widthCurve = Mathf.Lerp(1.25f, 0.62f, Mathf.Sin(tDrip * Mathf.PI * 0.85f));
                        if (tDrip > 0.82f)
                        {
                            float bulb = Mathf.Sin(Mathf.Clamp01((tDrip - 0.82f) / 0.18f) * Mathf.PI);
                            widthCurve += bulb * 0.85f;
                        }
                        float rad = dripRad[d] * widthCurve;
                        float dx = Mathf.Abs(u - (dripU[d] + wobble)) / Mathf.Max(0.001f, rad);
                        float tipCap = (v < botV) ? ((botV - v) / 0.025f) : 0f;
                        float dist = Mathf.Sqrt(dx * dx + tipCap * tipCap);
                        if (dist < 1.0f)
                        {
                            float dripA = Mathf.Clamp01((1f - dist) * 4.5f) * 0.96f;
                            alpha = Mathf.Max(alpha, dripA);
                            clotFactor = Mathf.Max(clotFactor, Mathf.Clamp01(1f - dist * 0.7f));
                        }
                    }
                }

                // 4. Fine high-velocity blood mist splatters around the title
                if (alpha < 0.25f && v > 0.22f && v < 0.95f && u > 0.02f && u < 0.96f)
                {
                    float sp = Mathf.PerlinNoise(u * 46f + 13.4f, v * 32f + 7.2f);
                    float cluster = Mathf.PerlinNoise(u * 7.5f + 2.1f, v * 5.5f + 8.3f);
                    if (sp > 0.79f && cluster > 0.50f)
                    {
                        float dropA = Mathf.Clamp01((sp - 0.79f) * 9.0f) * 0.85f;
                        alpha = Mathf.Max(alpha, dropA);
                    }
                }

                if (alpha > 0.01f)
                {
                    Color col = Color.Lerp(arterialRed, deepClot, Mathf.Clamp01(clotFactor * 0.78f + (1f - fbm) * 0.3f));
                    col = Color.Lerp(col, brightBlood, Mathf.Clamp01((fbm - 0.55f) * 1.6f));
                    col.a = Mathf.Clamp01(alpha);
                    tex.SetPixel(x, y, col);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D BuildRealisticWhiteHorrorButtonTexture(int w, int h, bool isHovered)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float seed = isHovered ? 27.4f : 8.6f;

        // Weathered bone-white / distressed plaster-parchment horror palette
        Color brightBoneWhite = isHovered ? new Color(0.95f, 0.93f, 0.90f, 1f) : new Color(0.86f, 0.85f, 0.82f, 1f);
        Color weatheredAsh = isHovered ? new Color(0.76f, 0.73f, 0.69f, 1f) : new Color(0.64f, 0.63f, 0.60f, 1f);
        Color grimeCharcoal = new Color(0.22f, 0.21f, 0.20f, 1f);
        Color crackDark = new Color(0.11f, 0.10f, 0.10f, 1f);
        Color dryBlood = new Color(0.32f, 0.020f, 0.018f, 1f);
        Color wetArterial = new Color(0.56f, 0.025f, 0.020f, 1f);
        Color deepClotBlood = new Color(0.18f, 0.008f, 0.008f, 1f);

        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);

                // Multi-octave tactile paper/plaster/concrete grunge noise
                float n1 = Mathf.PerlinNoise(u * 6.5f + seed, v * 3.8f + seed);
                float n2 = Mathf.PerlinNoise(u * 18.0f + seed * 1.3f, v * 10.5f + seed * 0.9f) * 0.5f;
                float n3 = Mathf.PerlinNoise(u * 48.0f + seed * 2.1f, v * 28.0f + seed * 1.7f) * 0.25f;
                float fineGrain = Mathf.PerlinNoise(u * 130f + seed, v * 75f + seed) * 0.14f;
                float fbm = (n1 + n2 + n3) / 1.75f;

                // Organic rough/torn weathered edges (jagged tattered parchment/painted-steel silhouette)
                float edgeNoiseX = (Mathf.PerlinNoise(v * 14f + seed, 3.1f) - 0.5f) * 0.045f +
                                   (Mathf.PerlinNoise(v * 38f + seed, 9.7f) - 0.5f) * 0.018f;
                float edgeNoiseY = (Mathf.PerlinNoise(u * 18f + seed, 5.4f) - 0.5f) * 0.11f +
                                   (Mathf.PerlinNoise(u * 46f + seed, 12.2f) - 0.5f) * 0.045f;

                // Subtle slanted torn right edge for dramatic indie-horror UI silhouette
                float rightCut = 0.965f - (1f - v) * 0.035f + edgeNoiseX * 1.35f;
                float leftCut = 0.022f + edgeNoiseX * 0.7f;
                float botCut = 0.085f + edgeNoiseY;
                float topCut = 0.915f + edgeNoiseY;

                float dLeft = (u - leftCut) * 42f;
                float dRight = (rightCut - u) * 38f;
                float dBot = (v - botCut) * 18f;
                float dTop = (topCut - v) * 18f;
                float minEdge = Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dBot, dTop));

                if (minEdge <= 0f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                float edgeAlpha = Mathf.Clamp01(minEdge * 1.6f);

                // Base weathered distressed white plaster/bone texture
                Color col = Color.Lerp(weatheredAsh, brightBoneWhite, Mathf.Clamp01(fbm * 1.15f + fineGrain - 0.12f));

                // Horizontal & vertical distress scratches (chipped white horror paint)
                float hScratch = Mathf.PerlinNoise(u * 7.5f + seed, v * 62f + seed);
                if (hScratch > 0.73f)
                {
                    float s = (hScratch - 0.73f) * 2.8f;
                    col = Color.Lerp(col, brightBoneWhite * 1.06f, Mathf.Clamp01(s * 0.55f));
                }
                else if (hScratch < 0.24f)
                {
                    float s = (0.24f - hScratch) * 2.6f;
                    col = Color.Lerp(col, grimeCharcoal, Mathf.Clamp01(s * 0.35f));
                }

                // Hairline weathered surface cracks
                float crackNoise = Mathf.Abs(Mathf.PerlinNoise(u * 14f + seed * 0.5f, v * 9f + seed * 1.8f) - 0.5f);
                if (crackNoise < 0.014f && n1 > 0.46f)
                {
                    float cStrength = (0.014f - crackNoise) / 0.014f;
                    col = Color.Lerp(col, crackDark, cStrength * 0.55f);
                }

                // Dark charred/grimy edge wear & aged soot along torn borders
                float borderWear = Mathf.Clamp01(1f - minEdge * 0.42f);
                float sootNoise = Mathf.Clamp01(fbm * 1.25f);
                col = Color.Lerp(col, grimeCharcoal, borderWear * (0.45f + sootNoise * 0.40f));

                // Subtle aged dark accent bar on the left edge (blood-red when hovered, weathered charcoal when idle)
                if (u < 0.055f + edgeNoiseX * 0.4f)
                {
                    Color accent = isHovered ? wetArterial : new Color(0.28f, 0.04f, 0.04f, 1f);
                    float accentMask = Mathf.Clamp01((0.055f - u) * 35f);
                    col = Color.Lerp(col, accent, accentMask * (0.72f + fbm * 0.28f));
                }

                // Realistic arterial blood smears & splatter stains
                float bloodField = Mathf.PerlinNoise(u * 5.2f + 19.3f, v * 4.5f + 7.8f);
                float bloodDetail = Mathf.PerlinNoise(u * 22f + 3.4f, v * 18f + 14.1f);
                float bloodMask = 0f;

                if (isHovered)
                {
                    // On hover: dramatic creeping arterial blood smears across left edge, lower rim, and right tail + splatters
                    float cornerBlood = Mathf.Clamp01((u - 0.52f) * 1.55f) * Mathf.Clamp01(bloodField * 1.35f - 0.30f);
                    float rimBlood = Mathf.Clamp01((0.30f - v) * 3.5f) * Mathf.Clamp01(bloodField * 1.45f - 0.35f);
                    float leftBleed = Mathf.Clamp01((0.24f - u) * 3.2f) * Mathf.Clamp01(bloodField * 1.40f - 0.38f);
                    float spatter = (bloodDetail > 0.74f && bloodField > 0.45f) ? ((bloodDetail - 0.74f) * 4.5f) : 0f;
                    bloodMask = Mathf.Clamp01(Mathf.Max(Mathf.Max(cornerBlood, rimBlood), Mathf.Max(leftBleed, spatter)));
                }
                else
                {
                    // Idle state: subtle eerie dried blood smudges & tiny splatters on the weathered white plate
                    float subtleEdgeBlood = Mathf.Clamp01((u - 0.76f) * 3.2f) * Mathf.Clamp01(bloodField * 1.35f - 0.52f);
                    float tinySpeck = (bloodDetail > 0.81f && bloodField > 0.56f) ? ((bloodDetail - 0.81f) * 3.8f) : 0f;
                    bloodMask = Mathf.Clamp01(Mathf.Max(subtleEdgeBlood * 0.65f, tinySpeck * 0.60f));
                }

                if (bloodMask > 0.02f)
                {
                    Color bCol = Color.Lerp(wetArterial, deepClotBlood, Mathf.Clamp01(bloodMask * 0.75f + (1f - bloodDetail) * 0.35f));
                    if (!isHovered) bCol = Color.Lerp(bCol, dryBlood, 0.65f);
                    col = Color.Lerp(col, bCol, Mathf.Clamp01(bloodMask * 0.92f));
                }

                // Subtle vertical rain-streaked aging & overall translucent blend with dark background
                float baseOpacity = isHovered ? 0.96f : 0.86f;
                col.a = Mathf.Clamp01(edgeAlpha * baseOpacity);
                tex.SetPixel(x, y, col);
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D BuildRealisticBloodDripTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float[] dripU = { 0.07f, 0.14f, 0.27f, 0.44f, 0.62f, 0.76f, 0.86f };
        float[] dripLen = { 0.62f, 0.88f, 0.48f, 0.76f, 0.54f, 0.92f, 0.66f };
        float[] dripRad = { 0.011f, 0.014f, 0.009f, 0.012f, 0.010f, 0.014f, 0.011f };

        Color deepClot = new Color(0.16f, 0.006f, 0.006f, 0.96f);
        Color wetCrimson = new Color(0.50f, 0.020f, 0.018f, 0.94f);

        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                float alpha = 0f;
                float core = 0f;

                for (int d = 0; d < dripU.Length; d++)
                {
                    float topV = 1.0f;
                    float botV = Mathf.Max(0.08f, 1.0f - dripLen[d]);
                    if (v >= botV - 0.08f)
                    {
                        float t = Mathf.Clamp01((topV - v) / Mathf.Max(0.01f, topV - botV));
                        float widthCurve = Mathf.Lerp(1.35f, 0.58f, Mathf.Sin(t * Mathf.PI * 0.85f));
                        if (t > 0.78f)
                        {
                            widthCurve += Mathf.Sin(Mathf.Clamp01((t - 0.78f) / 0.22f) * Mathf.PI) * 0.85f;
                        }
                        float wobble = (Mathf.PerlinNoise(v * 10f + d * 4.1f, d * 2.9f) - 0.5f) * 0.006f;
                        float dx = Mathf.Abs(u - (dripU[d] + wobble)) / Mathf.Max(0.001f, dripRad[d] * widthCurve);
                        float tipCap = (v < botV) ? ((botV - v) / 0.07f) : 0f;
                        float dist = Mathf.Sqrt(dx * dx + tipCap * tipCap);
                        if (dist < 1.0f)
                        {
                            alpha = Mathf.Max(alpha, Mathf.Clamp01((1f - dist) * 4.0f) * 0.95f);
                            core = Mathf.Max(core, 1f - dist);
                        }
                    }
                }

                if (alpha > 0.01f)
                {
                    Color c = Color.Lerp(wetCrimson, deepClot, core * 0.72f);
                    c.a = alpha;
                    tex.SetPixel(x, y, c);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }

        tex.Apply();
        return tex;
    }

    private void EnsureStyles()
    {
        if (vhsMenuFont == null)
        {
            // Match the classic analog-horror serif typography of "NIGHTSHIFT: JOJO'S GAS STATION"
            string[] serifHorrorFonts = { "Constantia", "Georgia", "Palatino Linotype", "Times New Roman" };
            vhsMenuFont = Font.CreateDynamicFontFromOSFont(serifHorrorFonts, 34);
            if (vhsMenuFont == null) vhsMenuFont = HorrorHudStyle.GetRetroFont();
        }

        if (vhsButtonCoreStyle == null)
        {
            vhsButtonCoreStyle = new GUIStyle(GUI.skin.label)
            {
                font = vhsMenuFont,
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            vhsButtonCoreStyle.normal.textColor = new Color(0.92f, 0.91f, 0.87f, 0.98f);

            vhsButtonBlueStyle = new GUIStyle(vhsButtonCoreStyle);
            vhsButtonBlueStyle.normal.textColor = new Color(0.36f, 0.54f, 0.50f, 0.75f);

            vhsButtonRedStyle = new GUIStyle(vhsButtonCoreStyle);
            vhsButtonRedStyle.normal.textColor = new Color(0.76f, 0.16f, 0.14f, 0.78f);

            vhsButtonGlowStyle = new GUIStyle(vhsButtonCoreStyle);
            vhsButtonGlowStyle.normal.textColor = new Color(0.92f, 0.93f, 0.88f, 0.30f);

            vhsButtonShadowStyle = new GUIStyle(vhsButtonCoreStyle);
            vhsButtonShadowStyle.normal.textColor = new Color(0.0f, 0.0f, 0.0f, 0.95f);
        }
    }

    private void OnGUI()
    {
        if (!isMenuOpen) return;

        EnsureStyles();
        GUI.depth = -2000;

        float sw = Screen.width;
        float sh = Screen.height;
        float t = Time.unscaledTime - menuOpenTime;
        bool isGlitching = currentGlitchStrength > 0.01f;

        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, sw, sh), pixelTexture);

        if (eerieBgTexture != null)
        {
            float zoom = 1.05f + Mathf.Sin(t * 0.38f) * 0.022f;
            float panX = Mathf.Sin(t * 0.27f) * 14f;
            float panY = Mathf.Cos(t * 0.21f) * 9f;

            float texAspect = eerieBgTexture.width / (float)Mathf.Max(1, eerieBgTexture.height);
            float screenAspect = sw / Mathf.Max(1f, sh);
            float drawW = sw * zoom;
            float drawH = sh * zoom;
            if (screenAspect > texAspect)
            {
                drawH = drawW / texAspect;
            }
            else
            {
                drawW = drawH * texAspect;
            }

            float drawX = (sw - drawW) * 0.5f + panX;
            float drawY = (sh - drawH) * 0.5f + panY;

            float b = flickerMultiplier;
            float dreadPulse = 0.5f + 0.5f * Mathf.Sin(t * 0.65f);
            GUI.color = new Color(
                Mathf.Clamp01(b * (0.94f + dreadPulse * 0.06f)),
                Mathf.Clamp01(b * 0.92f),
                Mathf.Clamp01(b * 0.91f),
                1f);
            GUI.DrawTexture(new Rect(drawX, drawY, drawW, drawH), eerieBgTexture, ScaleMode.StretchToFill);

            // UV-accurate horizontal VHS tear slices across the background during glitch bursts
            if (isGlitching && activeBgSliceCount > 0 && bgGlitchSlices != null)
            {
                for (int g = 0; g < activeBgSliceCount; g++)
                {
                    GlitchSlice sl = bgGlitchSlices[g];
                    float sliceY = drawY + sl.fracStart * drawH;
                    float sliceH = (sl.fracEnd - sl.fracStart) * drawH;
                    Rect uv = new Rect(0f, 1f - sl.fracEnd, 1f, sl.fracEnd - sl.fracStart);

                    // Subtle cyan-green left ghost strip
                    GUI.color = new Color(0.45f, 0.85f, 0.80f, 0.26f * currentGlitchStrength);
                    GUI.DrawTextureWithTexCoords(new Rect(drawX + sl.offsetX - sl.chromaSplit, sliceY, drawW, sliceH), eerieBgTexture, uv);

                    // Subtle crimson-red right ghost strip
                    GUI.color = new Color(0.95f, 0.22f, 0.18f, 0.30f * currentGlitchStrength);
                    GUI.DrawTextureWithTexCoords(new Rect(drawX + sl.offsetX + sl.chromaSplit, sliceY, drawW, sliceH), eerieBgTexture, uv);

                    // Core displaced strip
                    GUI.color = new Color(sl.brightness, sl.brightness, sl.brightness, 0.82f);
                    GUI.DrawTextureWithTexCoords(new Rect(drawX + sl.offsetX, sliceY, drawW, sliceH), eerieBgTexture, uv);
                }
            }
        }

        if (fogNoiseTexture != null)
        {
            float fogScroll1 = (t * 0.025f) % 1f;
            float fogScroll2 = (-t * 0.018f) % 1f;
            GUI.color = new Color(0.65f, 0.72f, 0.76f, 0.22f);
            GUI.DrawTextureWithTexCoords(new Rect(0f, sh * 0.25f, sw, sh * 0.75f), fogNoiseTexture, new Rect(fogScroll1, 0f, 1.5f, 1f));
            GUI.color = new Color(0.45f, 0.50f, 0.56f, 0.16f);
            GUI.DrawTextureWithTexCoords(new Rect(0f, sh * 0.35f, sw, sh * 0.65f), fogNoiseTexture, new Rect(fogScroll2, 0.2f, 2.0f, 1f));
        }

        if (rainStreaks != null)
        {
            for (int i = 0; i < rainStreaks.Length; i++)
            {
                float rx = rainStreaks[i].x * sw;
                float ry = rainStreaks[i].y * sh;
                GUI.color = new Color(0.72f, 0.78f, 0.84f, rainStreaks[i].alpha);
                GUI.DrawTexture(new Rect(rx, ry, 1.5f, rainStreaks[i].length), pixelTexture);
            }
        }

        if (vignetteTexture != null)
        {
            float vigPulse = 0.92f + 0.08f * Mathf.Sin(t * 1.15f);
            GUI.color = new Color(1f, 1f, 1f, vigPulse);
            GUI.DrawTexture(new Rect(0f, 0f, sw, sh), vignetteTexture, ScaleMode.StretchToFill);
        }

        // Deep dark atmospheric backdrop behind the left-aligned title & menu buttons
        if (topSkyShadeTexture != null)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.82f);
            GUI.DrawTexture(new Rect(0f, 0f, sw, sh * 0.56f), topSkyShadeTexture, ScaleMode.StretchToFill);
        }

        if (leftShadeTexture != null)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0f, 0f, sw * 0.64f, sh), leftShadeTexture, ScaleMode.StretchToFill);
        }

        float uiScale = Mathf.Clamp(sh / 720f, 0.72f, 1.38f);
        float menuLeftX = Mathf.Max(46f, sw * 0.055f);

        // ---------------------------------------------------------------------
        // TOP-LEFT ANALOG HORROR SERIF TITLE: "NIGHTSHIFT: / JOJO'S GAS / STATION"
        // WITH REAL-TIME HORIZONTAL VHS TAPE-TEAR & CHROMATIC GLITCH SLICING
        // ---------------------------------------------------------------------
        float logoBottomY = sh * 0.50f;
        if (titleLogoTexture != null)
        {
            float logoAspect = titleLogoTexture.width / (float)Mathf.Max(1, titleLogoTexture.height);
            float maxLogoW = Mathf.Clamp(sw * 0.50f, 360f, 760f);
            float maxLogoH = sh * 0.43f;
            float logoW = maxLogoW;
            float logoH = logoW / logoAspect;
            if (logoH > maxLogoH)
            {
                logoH = maxLogoH;
                logoW = logoH * logoAspect;
            }

            float logoX = menuLeftX;
            float logoY = Mathf.Max(26f, sh * 0.085f);
            logoBottomY = logoY + logoH;

            DrawGlitchedTitleLogo(new Rect(logoX, logoY, logoW, logoH), t);
        }

        // ---------------------------------------------------------------------
        // LEFT-ALIGNED ANALOG SERIF MENU OPTIONS ("START GAME" & "EXIT")
        // ---------------------------------------------------------------------
        int vhsFontSize = Mathf.RoundToInt(34f * uiScale);
        vhsButtonCoreStyle.fontSize = vhsFontSize;
        vhsButtonBlueStyle.fontSize = vhsFontSize;
        vhsButtonRedStyle.fontSize = vhsFontSize;
        vhsButtonGlowStyle.fontSize = vhsFontSize;
        vhsButtonShadowStyle.fontSize = vhsFontSize;

        float btnWidth = Mathf.Clamp(sw * 0.34f, 290f, 440f);
        float btnHeight = Mathf.Round(50f * uiScale);
        float startBtnY = Mathf.Clamp(logoBottomY + Mathf.Round(44f * uiScale), sh * 0.54f, sh * 0.68f);
        float exitBtnY = startBtnY + btnHeight + Mathf.Round(16f * uiScale);

        Rect startRect = new Rect(menuLeftX, startBtnY, btnWidth, btnHeight);
        Rect exitRect = new Rect(menuLeftX, exitBtnY, btnWidth, btnHeight);

        Vector2 mousePos = Event.current.mousePosition;
        hoveredButtonIndex = -1;
        if (startRect.Contains(mousePos))
        {
            hoveredButtonIndex = 0;
            keyboardSelectedIndex = 0;
        }
        else if (exitRect.Contains(mousePos))
        {
            hoveredButtonIndex = 1;
            keyboardSelectedIndex = 1;
        }

        if (Event.current.type == EventType.Repaint)
        {
            PlayHoverSound(hoveredButtonIndex);
        }

        bool isStartHighlighted = (hoveredButtonIndex == 0) || (hoveredButtonIndex == -1 && keyboardSelectedIndex == 0);
        bool isExitHighlighted = (hoveredButtonIndex == 1) || (hoveredButtonIndex == -1 && keyboardSelectedIndex == 1);

        DrawVhsMenuOption(startRect, "START GAME", isStartHighlighted, t);
        DrawVhsMenuOption(exitRect, "EXIT", isExitHighlighted, t);

        // ---------------------------------------------------------------------
        // FULL-SCREEN ANALOG CRT SCANLINES, ROLLING TRACKING BAR & VHS GLITCH NOISE
        // ---------------------------------------------------------------------
        DrawVhsOverlayEffects(sw, sh, t);

        if (!isTransitioningToStart && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            if (startRect.Contains(mousePos))
            {
                Event.current.Use();
                OnStartClicked();
            }
            else if (exitRect.Contains(mousePos))
            {
                Event.current.Use();
                OnExitClicked();
            }
        }

        if (startFadeOutAlpha > 0.001f)
        {
            GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(startFadeOutAlpha));
            GUI.DrawTexture(new Rect(0f, 0f, sw, sh), pixelTexture);
        }

        GUI.color = Color.white;
    }

    private void DrawGlitchedTitleLogo(Rect rect, float time)
    {
        float logoFlicker = Mathf.Lerp(0.84f, 1.0f, flickerMultiplier);
        float baseX = rect.x + titleWholeJitterX;
        float baseY = rect.y + titleWholeJitterY;

        if (currentGlitchStrength > 0.01f && activeTitleSliceCount > 0 && titleGlitchSlices != null)
        {
            // Render title split into jagged horizontal VHS tape-tear strips with RGB chromatic separation
            for (int i = 0; i < activeTitleSliceCount; i++)
            {
                GlitchSlice sl = titleGlitchSlices[i];
                if (sl.dropOut) continue;

                float sliceY = baseY + sl.fracStart * rect.height;
                float sliceH = (sl.fracEnd - sl.fracStart) * rect.height;
                Rect uv = new Rect(0f, 1f - sl.fracEnd, 1f, sl.fracEnd - sl.fracStart);

                // Cool analog cyan-green displaced ghost
                GUI.color = new Color(0.55f, 0.92f, 0.84f, 0.42f * currentGlitchStrength);
                GUI.DrawTextureWithTexCoords(
                    new Rect(baseX + sl.offsetX - sl.chromaSplit, sliceY, rect.width, sliceH),
                    titleLogoTexture,
                    uv);

                // Warm eerie crimson-red displaced ghost
                GUI.color = new Color(0.95f, 0.24f, 0.20f, 0.46f * currentGlitchStrength);
                GUI.DrawTextureWithTexCoords(
                    new Rect(baseX + sl.offsetX + sl.chromaSplit, sliceY, rect.width, sliceH),
                    titleLogoTexture,
                    uv);

                // Main displaced VHS serif strip
                float lum = Mathf.Clamp01(logoFlicker * sl.brightness);
                GUI.color = new Color(lum, lum, lum, 0.98f);
                GUI.DrawTextureWithTexCoords(
                    new Rect(baseX + sl.offsetX, sliceY, rect.width, sliceH),
                    titleLogoTexture,
                    uv);
            }
        }
        else
        {
            // Idle CRT state: gentle analog breathing aberration + subtle scanline shimmer
            float idleChroma = 1.6f + Mathf.Sin(time * 2.4f) * 0.55f;

            GUI.color = new Color(0.54f, 0.80f, 0.74f, 0.16f * logoFlicker);
            GUI.DrawTexture(new Rect(baseX - idleChroma, baseY, rect.width, rect.height), titleLogoTexture, ScaleMode.StretchToFill);

            GUI.color = new Color(0.88f, 0.26f, 0.22f, 0.18f * logoFlicker);
            GUI.DrawTexture(new Rect(baseX + idleChroma, baseY, rect.width, rect.height), titleLogoTexture, ScaleMode.StretchToFill);

            GUI.color = new Color(logoFlicker, logoFlicker, logoFlicker, 1f);
            GUI.DrawTexture(new Rect(baseX, baseY, rect.width, rect.height), titleLogoTexture, ScaleMode.StretchToFill);
        }

        GUI.color = Color.white;
    }

    private void DrawVhsOverlayEffects(float sw, float sh, float time)
    {
        // 1. Subtle full-screen CRT horizontal scanlines over both background & typography
        if (scanlineTexture != null)
        {
            float scanAlpha = 0.44f + currentGlitchStrength * 0.18f;
            GUI.color = new Color(1f, 1f, 1f, scanAlpha);
            GUI.DrawTextureWithTexCoords(new Rect(0f, 0f, sw, sh), scanlineTexture, new Rect(0f, 0f, 1f, sh / 4f));
        }

        // 2. Animated analog VHS snow / film grain static (subtle when idle, spikes on glitches)
        if (staticNoiseTexture != null)
        {
            float staticAlpha = 0.048f + currentGlitchStrength * 0.17f;
            GUI.color = new Color(0.92f, 0.94f, 0.90f, staticAlpha);
            GUI.DrawTextureWithTexCoords(
                new Rect(0f, 0f, sw, sh),
                staticNoiseTexture,
                new Rect(staticUvOffsetX, staticUvOffsetY, sw / 192f, sh / 192f));
        }

        // 3. Slow-rolling VHS tracking distortion band
        float barY = trackingBarY * sh;
        float barH = Mathf.Lerp(14f, 34f, currentGlitchStrength);
        if (barY > -barH && barY < sh + barH)
        {
            GUI.color = new Color(0.85f, 0.88f, 0.84f, 0.045f + currentGlitchStrength * 0.09f);
            GUI.DrawTexture(new Rect(0f, barY, sw, barH), pixelTexture);
            if (staticNoiseTexture != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.11f + currentGlitchStrength * 0.22f);
                GUI.DrawTextureWithTexCoords(
                    new Rect(0f, barY, sw, barH),
                    staticNoiseTexture,
                    new Rect(staticUvOffsetX * 3.1f, staticUvOffsetY, sw / 128f, 0.35f));
            }
            GUI.color = new Color(0.95f, 0.95f, 0.92f, 0.08f + currentGlitchStrength * 0.16f);
            GUI.DrawTexture(new Rect(0f, barY, sw, 1.5f), pixelTexture);
        }

        // 4. Crisp horizontal VHS signal tear streaks during glitch bursts
        if (currentGlitchStrength > 0.01f && activeGlitchLineCount > 0 && vhsGlitchLines != null)
        {
            for (int i = 0; i < activeGlitchLineCount; i++)
            {
                GlitchLine gl = vhsGlitchLines[i];
                GUI.color = gl.isRed
                    ? new Color(0.92f, 0.18f, 0.16f, gl.alpha)
                    : new Color(0.88f, 0.92f, 0.86f, gl.alpha);
                GUI.DrawTexture(new Rect(gl.xNorm * sw, gl.yNorm * sh, gl.widthNorm * sw, gl.heightPx), pixelTexture);
            }
        }

        GUI.color = Color.white;
    }

    private void DrawVhsMenuOption(Rect rect, string label, bool highlighted, float time)
    {
        float glitchBoost = (highlighted || currentGlitchStrength > 0.5f) ? currentGlitchStrength : (currentGlitchStrength * 0.35f);
        float jitterX = glitchBoost > 0.01f ? titleWholeJitterX * (highlighted ? 1.55f : 0.65f) : 0f;
        float hoverShiftX = highlighted ? 16f : 0f;
        Rect r = new Rect(rect.x + hoverShiftX + jitterX, rect.y, rect.width, rect.height);

        // Analog VHS chromatic aberration matching the aged ivory/sage title palette
        float chromaSpread = highlighted
            ? (2.4f + Mathf.Sin(time * 7.5f) * 0.45f + glitchBoost * 5.2f)
            : (1.2f + glitchBoost * 2.8f);
        float coreFlicker = Mathf.Lerp(0.88f, 1.0f, flickerMultiplier);

        vhsButtonBlueStyle.normal.textColor = highlighted
            ? new Color(0.42f, 0.68f, 0.64f, 0.65f + glitchBoost * 0.25f)
            : new Color(0.35f, 0.52f, 0.48f, 0.36f);

        vhsButtonRedStyle.normal.textColor = highlighted
            ? new Color(0.88f, 0.18f, 0.15f, 0.72f + glitchBoost * 0.25f)
            : new Color(0.68f, 0.20f, 0.18f, 0.34f);

        vhsButtonCoreStyle.normal.textColor = highlighted
            ? new Color(0.95f * coreFlicker, 0.94f * coreFlicker, 0.90f * coreFlicker, 1.0f)
            : new Color(0.66f * coreFlicker, 0.68f * coreFlicker, 0.62f * coreFlicker, 0.92f);

        vhsButtonGlowStyle.normal.textColor = highlighted
            ? new Color(0.95f, 0.94f, 0.90f, 0.32f)
            : new Color(0.75f, 0.78f, 0.72f, 0.12f);

        // 1. Deep CRT drop shadow
        GUI.Label(new Rect(r.x + 2.5f, r.y + 2.5f, r.width, r.height), label, vhsButtonShadowStyle);

        // 2. Cool VHS cyan-sage left fringe
        GUI.Label(new Rect(r.x - chromaSpread, r.y + 0.5f, r.width, r.height), label, vhsButtonBlueStyle);

        // 3. Muted blood-red right fringe
        GUI.Label(new Rect(r.x + chromaSpread, r.y - 0.5f, r.width, r.height), label, vhsButtonRedStyle);

        // 4. Soft CRT phosphor halation halo
        GUI.Label(new Rect(r.x - 1f, r.y - 1f, r.width, r.height), label, vhsButtonGlowStyle);
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), label, vhsButtonGlowStyle);

        // 5. Crisp ivory/sage analog serif foreground text
        GUI.Label(r, label, vhsButtonCoreStyle);

        // Glowing analog crimson selection bar on the left when hovered/selected
        if (highlighted)
        {
            float pulse = 0.76f + 0.24f * Mathf.Sin(time * 6.5f);
            float barJitterY = glitchBoost > 0.01f ? titleWholeJitterY : 0f;
            GUI.color = new Color(0.86f, 0.14f, 0.12f, 0.92f * pulse);
            GUI.DrawTexture(new Rect(r.x - 16f, r.y + r.height * 0.22f + barJitterY, 4f, r.height * 0.54f), pixelTexture);
            GUI.color = Color.white;
        }
    }

    private static bool WasUpPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
#endif
    }

    private static bool WasDownPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
#endif
    }

    private static bool WasSubmitPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.enterKey.wasPressedThisFrame ||
                   Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                   Keyboard.current.spaceKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
#endif
    }
}

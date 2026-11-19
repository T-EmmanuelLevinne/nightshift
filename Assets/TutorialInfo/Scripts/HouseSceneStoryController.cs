using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Story and typewriter controller for HouseScene (Act 1: Prologue / Opening).
/// Runs the opening sequence:
/// 1. Black screen
/// 2. Typewriter: " At home... "
/// 3. 1.3 seconds cooldown
/// 4. Typewriter: " Tired.. Hungry.. "
/// 5. 2 seconds cooldown
/// 6. Smooth fade in to reveal the game world
/// </summary>
public class HouseSceneStoryController : MonoBehaviour
{
    public static HouseSceneStoryController Instance { get; private set; }

    public static bool IsIntroActive => Instance != null && Instance.isIntroActive;
    public static bool IsSleepingSequenceActive => Instance != null && Instance.isSleepingSequenceActive;
    public static bool IsDialogueActive =>
        HorrorMainMenuController.IsMainMenuActive ||
        (Instance != null && (Instance.isIntroActive || Instance.isMovementLocked || Instance.isSleepingSequenceActive));
    public static bool IsCrosshairHidden =>
        HorrorMainMenuController.IsMainMenuActive ||
        (Instance != null &&
         (Instance.isIntroActive ||
          Instance.isSleepingSequenceActive ||
          Instance.blackOverlayAlpha > 0.01f ||
          Instance.eyelidCloseProgress > 0.15f ||
          SimpleFPSController.IsExternalCameraAnimationActive));

    public enum HouseStoryStage
    {
        InitialDarkness,
        IntroTypingAtHome,
        IntroCooldownAtHome,
        IntroTypingTiredHungry,
        IntroCooldownTiredHungry,
        IntroFadingInToGame,
        TaskEnterCabin,
        InsideCabinCooldown,
        DialogueWhatALongNight,
        DialogueImHungry,
        TaskFindSomethingInFridge,
        DialogueAlrightTheseWillDo,
        TasksCookingActive,
        DialogueThatWasGood,
        TaskWashPlate,
        TaskPutPlateBack,
        PlatePutBackComplete,
        DialoguePutFoodBackInFridge,
        TaskPutFoodBackInFridge,
        PutFoodBackInFridgeComplete,
        FridgeClosedAfterFoodReturn,
        DialogueWatchTvIntro,
        PostFridgeTvDialogueComplete,
        TaskTurnOnTv,
        TvTurnedOn,
        DialogueTvNewsCrash,
        TaskKeepWatching,
        DialogueGoToSleepYawn,
        DialogueGoToSleepTime,
        TaskTurnOffAllLights,
        LightsTurnedOffComplete,
        TaskGoToBed,
        DialogueForgotCloseDoor,
        TaskCloseFrontDoor,
        SleepingInBed
    }

    [Header("Current Narrative Stage")]
    public HouseStoryStage currentStage = HouseStoryStage.InitialDarkness;

    public enum Act3Stage
    {
        None,
        IntroBlackScreen,
        WakingUpInBed,
        PostWakeupDialogue,
        WalkingToFrontDoor,
        JuanFrontDoorDialogue,
        PostJuanWalkCooldown,
        TaskWashFace,
        WashingFaceAndOutage,
        TaskTryLightSwitch,
        LightSwitchCompleted,
        TaskFindCandle,
        CandleFoundDialogue,
        TaskLightCandle,
        CandleLitDialogue,
        TaskFindPhone,
        PhoneFoundDialogue,
        TaskMakeCall,
        ManagerCallInProgress,
        PostCallDialogue,
        TaskFindNoise,
        LookingAtMonsterAndBlink,
        LookingAtDeadPigeon,
        FadingOutToAct4
    }

    [Header("Act 3 & Act 5 State")]
    public bool isAct3Active = false;
    public bool isAct5Active = false;
    public bool act5ShowWashFaceTask = false;
    public Act3Stage act3Stage = Act3Stage.None;
    public bool isFrontDoorLocked = false;
    public bool isPowerOutageActive = false;
    public int outageSwitchToggleCount = 0;
    public float firstOutageSwitchToggleTime = -1f;
    public bool hasCompletedWashFace = false;
    public bool hasPickedUpCandle = false;
    public bool hasLitCandle = false;
    public bool hasPickedUpPhone = false;
    public bool hasInitiatedCall = false;
    public bool hasPlayerPressedInspectWindow = false;
    public bool hasInvestigatedBrokenWindow = false;
    private bool isWashingFaceRoutineRunning = false;

    public static bool IsFrontDoorLocked => Instance != null && (Instance.isFrontDoorLocked || Instance.isAct3Active || Instance.isAct5Active);
    public static bool IsPowerOutageActive => Instance != null && Instance.isPowerOutageActive;
    public static bool IsAct3WashFaceTaskActive => Instance != null && Instance.isAct3Active && Instance.act3Stage == Act3Stage.TaskWashFace && !Instance.isWashingFaceRoutineRunning;
    public static bool IsAct3FindCandleTaskActive => Instance != null && Instance.isAct3Active && Instance.act3Stage == Act3Stage.TaskFindCandle && !Instance.hasPickedUpCandle;
    public static bool IsAct3LightCandleTaskActive => Instance != null && Instance.isAct3Active && Instance.act3Stage == Act3Stage.TaskLightCandle && !Instance.hasLitCandle;
    public static bool IsAct3FindPhoneTaskActive => Instance != null && Instance.isAct3Active && Instance.act3Stage == Act3Stage.TaskFindPhone && !Instance.hasPickedUpPhone;
    public static bool IsAct3MakeCallTaskActive => Instance != null && Instance.isAct3Active && Instance.act3Stage == Act3Stage.TaskMakeCall && !Instance.hasInitiatedCall;
    public static bool IsAct3InspectWindowTaskActive => Instance != null && Instance.isAct3Active && Instance.act3Stage == Act3Stage.TaskFindNoise && !Instance.hasPlayerPressedInspectWindow;

    [Header("Story Flags")]
    public bool isFridgeUnlocked = false;
    public bool isFoodPickupUnlocked = false;
    public bool isStoveAndPanUnlocked = false;
    public bool hasPickedUpAtLeastOneFood = false;
    public bool hasPlacedFoodOnCounter = false;
    public bool hasCookedFood = false;
    public bool hasCookedAndPlatedFood = false;
    public bool hasEatenMeal = false;
    public bool hasUnsatAfterEating = false;
    public bool hasWashedPlate = false;
    public bool hasPutPlateBack = false;
    public bool isReturnFoodToFridgeUnlocked = false;
    public bool hasPutFoodBackInFridge = false;
    public bool hasClosedFridgeAfterFoodReturn = false;
    public bool isTvTaskUnlocked = false;
    public bool hasTurnedOnTvOnce = false;
    public bool hasWatchedTvCrashNews = false;
    public bool hasTurnedOffAllLights = false;
    public bool isBedSleepUnlocked = false;

    private bool hasTriggeredCabinEntry = false;
    private bool hasOpenedFridgeForStory = false;
    private bool isMonitoringFridgeClose = false;
    private bool hasStartedAfterEatingSequence = false;
    private bool hasStartedTvNewsSequence = false;
    private bool isSleepingSequenceActive = false;
    private bool hasTriggeredLightsComplete = false;
    private bool isDoorReminderPlaying = false;

    private static readonly string[] CloseFridgeReminderLines = new string[]
    {
        "You: I should close the fridge first..",
        "You: I should save electricity.. I need to close the fridge."
    };

    public static bool IsFridgeUnlocked => Instance != null && (Instance.isFridgeUnlocked || Instance.currentStage >= HouseStoryStage.TaskFindSomethingInFridge);
    public static bool IsFoodPickupUnlocked => Instance != null && Instance.isFoodPickupUnlocked;
    public static bool IsStoveAndPanUnlocked => Instance != null && Instance.isStoveAndPanUnlocked;
    public static bool IsDirtyPlatePickupUnlocked => Instance != null && Instance.currentStage >= HouseStoryStage.TaskWashPlate;
    public static bool IsReturnFoodToFridgeUnlocked => Instance != null && Instance.isReturnFoodToFridgeUnlocked;

    public static bool HasPlacedFoodOnCounter => (Instance != null && Instance.hasPlacedFoodOnCounter) ||
        InteractableFoodItem.AreBothEggAndBaconPlacedOnCounter() ||
        (Instance != null && (Instance.hasCookedFood || Instance.hasCookedAndPlatedFood || Instance.hasEatenMeal));
    public static bool HasCookedFood => Instance != null && (Instance.hasCookedFood || Instance.hasCookedAndPlatedFood);
    public static bool HasCookedAndPlatedFood => Instance != null && Instance.hasCookedAndPlatedFood;
    public static bool HasEatenMeal => Instance != null && Instance.hasEatenMeal;
    public static bool HasUnsatAfterEating => Instance != null && Instance.hasUnsatAfterEating;
    public static bool HasWashedPlate => Instance != null && Instance.hasWashedPlate;
    public static bool HasPutPlateBack => Instance != null && Instance.hasPutPlateBack;
    public static bool HasPutFoodBackInFridge => Instance != null && Instance.hasPutFoodBackInFridge;
    public static bool HasClosedFridgeAfterFoodReturn => Instance != null && Instance.hasClosedFridgeAfterFoodReturn;
    public static bool IsTvTaskUnlocked => Instance != null && Instance.isTvTaskUnlocked;
    public static bool HasTurnedOnTvOnce => Instance != null && Instance.hasTurnedOnTvOnce;
    public static bool HasWatchedTvCrashNews => Instance != null && Instance.hasWatchedTvCrashNews;
    public static bool IsBedSleepUnlocked => Instance != null && Instance.isBedSleepUnlocked;

    public static bool IsSitLockedByTv
    {
        get
        {
            if (Instance == null || !CabinChairInteractable.IsPlayerSitting) return false;
            if (CabinTvController.Instance == null || !CabinTvController.Instance.isPoweredOn) return false;
            return Instance.currentStage >= HouseStoryStage.TvTurnedOn &&
                   Instance.currentStage < HouseStoryStage.TaskTurnOffAllLights;
        }
    }

    public static bool IsTvPowerOffLocked
    {
        get
        {
            if (Instance == null || CabinTvController.Instance == null || !CabinTvController.Instance.isPoweredOn)
            {
                return false;
            }

            if (Instance.currentStage < HouseStoryStage.TvTurnedOn ||
                Instance.currentStage >= HouseStoryStage.TaskTurnOffAllLights)
            {
                return false;
            }

            return CabinChairInteractable.IsPlayerSitting ||
                   Instance.currentStage >= HouseStoryStage.DialogueTvNewsCrash;
        }
    }

    // Intro Black Screen & Typewriter state
    private bool isIntroActive = true;
    private bool isMovementLocked = true;
    private float blackOverlayAlpha = 1.0f;
    private float eyelidCloseProgress = 0.0f;
    private float textAlpha = 0.0f;
    private string displayedText = "";
    private bool isTyping = false;

    // Lower-Third Dialogue
    private bool isDialogueVisible = false;
    private bool isDialogueTyping = false;
    private float dialogueAlpha = 0.0f;
    private string displayedDialogueText = "";
    private bool skipCurrentDialogueRequested = false;

    // GUI & Audio
    private Texture2D pixelTexture;
    private Texture2D upperEyelidTexture;
    private Texture2D lowerEyelidTexture;
    private GUIStyle titleStyle;
    private GUIStyle titleShadowStyle;
    private GUIStyle dialogueStyle;
    private GUIStyle dialogueShadowStyle;
    private GUIStyle taskHeaderStyle;
    private GUIStyle taskHeaderShadowStyle;
    private GUIStyle taskSubStyle;
    private GUIStyle taskSubShadowStyle;
    private Font retroFont;
    private AudioSource audioSource;
    private AudioClip typeClickClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(sceneName, "HouseScene", System.StringComparison.OrdinalIgnoreCase))
        {
            EnsureExists();
        }
    }

    public static HouseSceneStoryController EnsureExists()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!string.Equals(sceneName, "HouseScene", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Instance != null) return Instance;

        HouseSceneStoryController found = Object.FindAnyObjectByType<HouseSceneStoryController>();
        if (found != null)
        {
            Instance = found;
            return Instance;
        }

        GameObject go = new GameObject("HouseScene_StoryController");
        Instance = go.AddComponent<HouseSceneStoryController>();
        return Instance;
    }

    private void Awake()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!string.Equals(sceneName, "HouseScene", System.StringComparison.OrdinalIgnoreCase))
        {
            Destroy(gameObject);
            return;
        }

        if (Instance == null) Instance = this;

        InputSystemSanitizer.SanitizeAllEventSystems();
        SetupAudio();
        InitTextures();

        // Ensure precision door triggers and zero floor bumps at runtime
        RemoveRogueGasStationObjects();
        ConfigureDoorsAndRemoveFloorBumps();
        ConfigureLightSwitches();
        ConfigureDiningChair();
        ConfigureSofaSmall();
        ConfigureFridge();
        CabinWindowUtility.EnsureRuntimeCabinWindowsConfigured();
        CabinCookingSystem.ConfigureKitchenAndCooking();
        CabinTvController.ConfigureLivingRoomTvAndRemote();
        CabinBedInteractable.ConfigureBedroomBed();

        // Always normalize and hide monster_low / monster_lowqual from frame 0 in both Act 1 and Act 3
        HouseAct3PropController.EnsureExists(FindCabinBuildingRoot());

        isIntroActive = true;
        isMovementLocked = true;
        isSleepingSequenceActive = false;
        blackOverlayAlpha = 1.0f;
        eyelidCloseProgress = 0.0f;
        textAlpha = 0.0f;
        displayedText = "";
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            isSleepingSequenceActive = false;
            isMovementLocked = false;
            isIntroActive = false;
            isPowerOutageActive = false;
            isFrontDoorLocked = false;
            SimpleFPSController.IsExternalCameraAnimationActive = false;
            Instance = null;
        }
    }

    public static void RemoveRogueGasStationObjects()
    {
        string[] gasObjectNames = new string[]
        {
            "UserBoxPutter",
            "Cardboard_Box",
            "Floor_Stain_1",
            "Floor_Stain_2",
            "Floor_Stain_3"
        };

        for (int i = 0; i < gasObjectNames.Length; i++)
        {
            GameObject go = GameObject.Find(gasObjectNames[i]);
            if (go != null)
            {
                Destroy(go);
            }
        }
    }

    public static void ConfigureDoorsAndRemoveFloorBumps()
    {
        // 1. Deactivate and strip ALL legacy static proxy colliders (*interact) across the entire scene
        Transform[] allSceneTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int t = 0; t < allSceneTransforms.Length; t++)
        {
            Transform tr = allSceneTransforms[t];
            if (tr == null) continue;
            string tName = tr.name.Trim();

            // Ignore light switches, dining chairs, sofas, and fridge
            if (tr.GetComponent<CabinLightSwitch>() != null ||
                tr.GetComponent<CabinChairInteractable>() != null ||
                tr.GetComponent<CabinFridgeInteractable>() != null ||
                tName.IndexOf("switch", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                tName.IndexOf("DiningChair", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                tName.IndexOf("Sofa", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                tName.IndexOf("Fridge", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            if (tName.IndexOf("interact", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                CabinDoorInteractable cdi = tr.GetComponent<CabinDoorInteractable>();
                if (cdi != null) Destroy(cdi);

                Collider[] cols = tr.GetComponents<Collider>();
                for (int c = 0; c < cols.Length; c++) Destroy(cols[c]);

                tr.gameObject.SetActive(false);
            }
        }

        // Disable legacy MoveObjectController scripts so they don't consume input or draw duplicate GUI
        MoveObjectController[] legacyControllers = Object.FindObjectsByType<MoveObjectController>(FindObjectsInactive.Include);
        for (int m = 0; m < legacyControllers.Length; m++)
        {
            if (legacyControllers[m] != null) legacyControllers[m].enabled = false;
        }

        // 2. Remove all bogus 1x1x1 BoxColliders on empty objects that caused floor bumps
        BoxCollider[] allBoxes = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include);
        for (int i = 0; i < allBoxes.Length; i++)
        {
            BoxCollider bc = allBoxes[i];
            if (bc == null) continue;
            if (bc.GetComponent<CabinLightSwitch>() != null ||
                bc.GetComponent<CabinChairInteractable>() != null ||
                bc.GetComponent<CabinFridgeInteractable>() != null ||
                bc.GetComponent<CabinFridgeReturnSpot>() != null ||
                bc.GetComponent<CabinKitchenCounterSurface>() != null ||
                bc.GetComponent<CabinHeldCookingItem>() != null ||
                bc.GetComponent<CabinPlateStackInteractable>() != null ||
                bc.GetComponent<CabinDrawerForkInteractable>() != null ||
                bc.GetComponent<CabinStoveStation>() != null ||
                bc.GetComponent<CabinDiningTablePlaceSpot>() != null ||
                bc.GetComponent<CabinKitchenSinkStation>() != null ||
                bc.name.IndexOf("switch", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                bc.name.IndexOf("DiningChair", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                bc.name.IndexOf("Sofa", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                bc.name.IndexOf("Fridge", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            MeshFilter mf = bc.GetComponent<MeshFilter>();
            bool isDefault1x1 = Mathf.Approximately(bc.size.x, 1f) && Mathf.Approximately(bc.size.y, 1f) && Mathf.Approximately(bc.size.z, 1f);
            if (mf == null || mf.sharedMesh == null)
            {
                // Destroy empty parent boxes that cause bumps or phantom clicks
                if (isDefault1x1 || bc.name.IndexOf("door", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Destroy(bc);
                }
            }
        }

        // 3. Configure Kitchen Bench (grp_drawsDoors/doors) for 100% precision pointing
        GameObject kitchenBench = GameObject.Find("PFB_KitchenBench");
        if (kitchenBench != null)
        {
            Animator benchAnim = kitchenBench.GetComponent<Animator>();
            if (benchAnim != null) benchAnim.enabled = true;

            // Directly attach trigger colliders & CabinDoorInteractable to the moving door meshes
            for (int i = 1; i <= 5; i++)
            {
                string dName = $"door0{i}";
                Transform doorLeaf = null;

                foreach (Transform t in kitchenBench.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Equals(dName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        doorLeaf = t;
                        break;
                    }
                }

                if (doorLeaf != null)
                {
                    MeshCollider mc = doorLeaf.GetComponent<MeshCollider>();
                    if (mc != null) Destroy(mc);

                    Rigidbody rb = doorLeaf.GetComponent<Rigidbody>();
                    if (rb != null) Destroy(rb);

                    MeshFilter mf = doorLeaf.GetComponent<MeshFilter>();
                    BoxCollider bc = doorLeaf.GetComponent<BoxCollider>();
                    if (bc == null) bc = doorLeaf.gameObject.AddComponent<BoxCollider>();

                    if (mf != null && mf.sharedMesh != null)
                    {
                        bc.size = mf.sharedMesh.bounds.size;
                        bc.center = mf.sharedMesh.bounds.center;
                    }
                    else
                    {
                        bc.size = new Vector3(0.55f, 0.68f, 0.08f);
                        bc.center = new Vector3(0.27f, 0.34f, 0f);
                    }
                    bc.isTrigger = true; // Trigger only: 100% precision, zero bumps!

                    CabinDoorInteractable cdi = doorLeaf.GetComponent<CabinDoorInteractable>();
                    if (cdi == null) cdi = doorLeaf.gameObject.AddComponent<CabinDoorInteractable>();

                    cdi.objectNumber = i;
                    cdi.targetAnimator = benchAnim;
                    cdi.parameterPrefix = "isOpen_Obj_";
                }
            }
        }

        // 4. Configure all cabin room doors (DoorDouble, DoorSingle, Closet, Pantry, BathroomSink, TVStand)
        Animator[] allAnimators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include);
        for (int a = 0; a < allAnimators.Length; a++)
        {
            Animator anim = allAnimators[a];
            if (anim == null) continue;
            string animName = anim.name;
            if (animName.Equals("PFB_KitchenBench", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (animName.IndexOf("ChestOfDraws", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (animName.IndexOf("BedsideTable", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            anim.enabled = true;

            // Remove giant trigger boxes on root door prefabs
            if (animName.IndexOf("DoorDouble", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                animName.IndexOf("DoorSingle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                BoxCollider rootBox = anim.GetComponent<BoxCollider>();
                if (rootBox != null) Destroy(rootBox);
            }

            foreach (Transform child in anim.GetComponentsInChildren<Transform>(true))
            {
                if (child == anim.transform) continue;
                string cName = child.name;

                // Exclude drawers and legacy proxies
                if (cName.StartsWith("draw", System.StringComparison.OrdinalIgnoreCase) ||
                    cName.IndexOf("interact", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    CabinDoorInteractable cdi = child.GetComponent<CabinDoorInteractable>();
                    if (cdi != null) Destroy(cdi);
                    continue;
                }

                // ONLY configure objects that have a real 3D mesh (door leaf mesh)
                MeshFilter mf = child.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                if (cName.IndexOf("door", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Rigidbody rb = child.GetComponent<Rigidbody>();
                    if (rb != null) Destroy(rb);

                    MeshCollider mc = child.GetComponent<MeshCollider>();
                    if (mc != null) Destroy(mc);

                    BoxCollider bc = child.GetComponent<BoxCollider>();
                    if (bc == null) bc = child.gameObject.AddComponent<BoxCollider>();

                    Vector3 meshSize = mf.sharedMesh.bounds.size;
                    Vector3 meshCenter = mf.sharedMesh.bounds.center;

                    bool isRoomDoor = animName.IndexOf("DoorSingle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      animName.IndexOf("DoorDouble", System.StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isRoomDoor)
                    {
                        // Room doors are solid obstacles when settled; CabinDoorInteractable temporarily skips collision mid-swing.
                        // Bottom is lifted 0.05m above floor so it never clips the threshold (zero floor bumps).
                        float bottomLift = 0.05f;
                        bc.size = new Vector3(meshSize.x, Mathf.Max(0.5f, meshSize.y - bottomLift), Mathf.Max(meshSize.z, 0.08f));
                        bc.center = new Vector3(meshCenter.x, meshCenter.y + bottomLift * 0.5f, meshCenter.z);
                        bc.isTrigger = false;
                    }
                    else
                    {
                        bc.size = new Vector3(meshSize.x, meshSize.y, Mathf.Max(meshSize.z, 0.12f));
                        bc.center = meshCenter;
                        bc.isTrigger = true;
                    }

                    CabinDoorInteractable cdi = child.GetComponent<CabinDoorInteractable>();
                    if (cdi == null) cdi = child.gameObject.AddComponent<CabinDoorInteractable>();

                    MoveableObject mo = child.GetComponent<MoveableObject>();
                    int objNum = mo != null ? mo.objectNumber : 1;

                    // For entrance DoorDouble (DoorDoubleAnimator), BOTH leaves are controlled by isOpen_Obj_1!
                    if (anim.name.IndexOf("DoorDouble", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        objNum = 1;
                    }
                    else if (cName.Equals("rightDoor", System.StringComparison.OrdinalIgnoreCase) || cName.Equals("doorR", System.StringComparison.OrdinalIgnoreCase))
                    {
                        objNum = 2;
                    }

                    cdi.objectNumber = objNum;
                    cdi.targetAnimator = anim;
                    cdi.parameterPrefix = "isOpen_Obj_";
                }
            }
        }
    }

    public static CabinLightSwitch.RoomPlace GetLightRoomPlace(Transform t)
    {
        string name = t.name.Trim();
        string parentName = t.parent != null ? t.parent.name.Trim() : "";
        string combo = name + " " + parentName;

        // 1. Bedroom lights (PFB_LightCoverBedroom, PFB_LightCoverBedroom2, or indices 4 and 11)
        if (combo.IndexOf("bedroom", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.Equals("PFB_LightCover (4)", System.StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PFB_LightCover (11)", System.StringComparison.OrdinalIgnoreCase))
        {
            return CabinLightSwitch.RoomPlace.Bedroom;
        }

        // 2. Bathroom lights (PFB_LightCover (5), (6), (10) or bathroom keywords)
        if (combo.IndexOf("bathroom", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.Equals("PFB_LightCover (5)", System.StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PFB_LightCover (6)", System.StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PFB_LightCover (10)", System.StringComparison.OrdinalIgnoreCase))
        {
            return CabinLightSwitch.RoomPlace.Bathroom;
        }

        // 3. Living Room lights ONLY (1, 2, 3, 7, 8, 9)
        if (combo.IndexOf("living", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.Equals("PFB_LightCover (1)", System.StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PFB_LightCover (2)", System.StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PFB_LightCover (3)", System.StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PFB_LightCover (7)", System.StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PFB_LightCover (8)", System.StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PFB_LightCover (9)", System.StringComparison.OrdinalIgnoreCase))
        {
            return CabinLightSwitch.RoomPlace.LivingRoom;
        }

        // Position fallback
        Vector3 pos = t.position;
        if (pos.z > 306.0f) return CabinLightSwitch.RoomPlace.LivingRoom;
        if (pos.x > -13.51f) return CabinLightSwitch.RoomPlace.Bedroom;
        return CabinLightSwitch.RoomPlace.Bathroom;
    }

    public static void ConfigureLightSwitches()
    {
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);

        GameObject livingRoomSwitch = null;
        GameObject bedroomSwitch = null;
        GameObject bathroomSwitch = null;

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null) continue;
            string nm = t.name.Trim();

            if (nm.IndexOf("switch", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (nm.IndexOf("LivingRoom", System.StringComparison.OrdinalIgnoreCase) >= 0 || nm.Equals("PFB_Lightswitch (1)", System.StringComparison.OrdinalIgnoreCase))
                {
                    livingRoomSwitch = t.gameObject;
                }
                else if (nm.IndexOf("Bedroom", System.StringComparison.OrdinalIgnoreCase) >= 0 || nm.Equals("PFB_Lightswitch (2)", System.StringComparison.OrdinalIgnoreCase))
                {
                    bedroomSwitch = t.gameObject;
                }
                else if (nm.IndexOf("Bathroom", System.StringComparison.OrdinalIgnoreCase) >= 0 || nm.Equals("PFB_Lightswitch (3)", System.StringComparison.OrdinalIgnoreCase))
                {
                    bathroomSwitch = t.gameObject;
                }
            }
        }

        // Configure each switch
        SetupSwitch(livingRoomSwitch, CabinLightSwitch.RoomPlace.LivingRoom, "Living Room");
        SetupSwitch(bedroomSwitch, CabinLightSwitch.RoomPlace.Bedroom, "Bedroom");
        SetupSwitch(bathroomSwitch, CabinLightSwitch.RoomPlace.Bathroom, "Bathroom");

        CabinLightSwitch livingSw = livingRoomSwitch != null ? livingRoomSwitch.GetComponent<CabinLightSwitch>() : null;
        CabinLightSwitch bedSw = bedroomSwitch != null ? bedroomSwitch.GetComponent<CabinLightSwitch>() : null;
        CabinLightSwitch bathSw = bathroomSwitch != null ? bathroomSwitch.GetComponent<CabinLightSwitch>() : null;

        if (livingSw != null && livingSw.controlledLights == null) livingSw.controlledLights = new List<Light>();
        if (bedSw != null && bedSw.controlledLights == null) bedSw.controlledLights = new List<Light>();
        if (bathSw != null && bathSw.controlledLights == null) bathSw.controlledLights = new List<Light>();

        if (livingSw != null) livingSw.controlledLights.Clear();
        if (bedSw != null) bedSw.controlledLights.Clear();
        if (bathSw != null) bathSw.controlledLights.Clear();

        // Exact room light assignment based on fixture identity
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        for (int l = 0; l < allLights.Length; l++)
        {
            Light lightComp = allLights[l];
            if (lightComp == null || lightComp.type == LightType.Directional) continue;

            Transform targetTransform = lightComp.transform.parent != null ? lightComp.transform.parent : lightComp.transform;
            CabinLightSwitch.RoomPlace targetRoom = GetLightRoomPlace(targetTransform);

            switch (targetRoom)
            {
                case CabinLightSwitch.RoomPlace.LivingRoom:
                    if (livingSw != null && !livingSw.controlledLights.Contains(lightComp))
                    {
                        livingSw.controlledLights.Add(lightComp);
                    }
                    break;
                case CabinLightSwitch.RoomPlace.Bedroom:
                    if (bedSw != null && !bedSw.controlledLights.Contains(lightComp))
                    {
                        bedSw.controlledLights.Add(lightComp);
                    }
                    break;
                case CabinLightSwitch.RoomPlace.Bathroom:
                    if (bathSw != null && !bathSw.controlledLights.Contains(lightComp))
                    {
                        bathSw.controlledLights.Add(lightComp);
                    }
                    break;
            }
        }

        if (livingSw != null) Debug.Log($"[HouseSceneStoryController] Living Room Switch configured with {livingSw.controlledLights.Count} lights (strictly Living Room).");
        if (bedSw != null) Debug.Log($"[HouseSceneStoryController] Bedroom Switch configured with {bedSw.controlledLights.Count} lights.");
        if (bathSw != null) Debug.Log($"[HouseSceneStoryController] Bathroom Switch configured with {bathSw.controlledLights.Count} lights.");
    }

    private static void SetupSwitch(GameObject switchGo, CabinLightSwitch.RoomPlace place, string displayName)
    {
        if (switchGo == null) return;
        switchGo.SetActive(true);

        CabinLightSwitch cls = switchGo.GetComponent<CabinLightSwitch>();
        if (cls == null) cls = switchGo.AddComponent<CabinLightSwitch>();
        cls.roomPlace = place;
        cls.roomDisplayName = displayName;
        cls.isLightOn = true;

        BoxCollider col = switchGo.GetComponent<BoxCollider>();
        if (col == null) col = switchGo.AddComponent<BoxCollider>();
        col.size = new Vector3(0.50f, 0.60f, 0.50f);
        col.center = Vector3.zero;
        col.isTrigger = true;

        AudioSource audioSource = switchGo.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = switchGo.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f;
        audioSource.minDistance = 0.5f;
        audioSource.maxDistance = 10.0f;
        audioSource.volume = 0.85f;
        audioSource.playOnAwake = false;
        cls.audioSource = audioSource;
    }

    public static void ConfigureDiningChair()
    {
        GameObject chairObj = GameObject.Find("PFB_DiningChairUserInteract");
        if (chairObj == null)
        {
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < allObjects.Length; i++)
            {
                if (allObjects[i].name.IndexOf("DiningChairUserInteract", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    chairObj = allObjects[i];
                    break;
                }
            }
        }

        if (chairObj != null)
        {
            chairObj.SetActive(true);
            chairObj.isStatic = false;

            CabinChairInteractable cci = chairObj.GetComponent<CabinChairInteractable>();
            if (cci == null)
            {
                cci = chairObj.AddComponent<CabinChairInteractable>();
            }

            BoxCollider box = chairObj.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = chairObj.AddComponent<BoxCollider>();
            }

            MeshFilter mf = chairObj.GetComponent<MeshFilter>();
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

            Debug.Log("[HouseSceneStoryController] Successfully configured PFB_DiningChairUserInteract for Move & Sit.");
        }

        ConfigureSofaSmall();

        // Deactivate any rogue red placeholder UI Image crosshairs in Canvas to keep the reticle 100% consistent
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        for (int c = 0; c < canvases.Length; c++)
        {
            if (canvases[c] != null && canvases[c].name.Equals("Canvas", System.StringComparison.OrdinalIgnoreCase))
            {
                foreach (UnityEngine.UI.Image img in canvases[c].GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    if (img.color.r > 0.8f && img.color.g < 0.2f && img.color.b < 0.2f)
                    {
                        img.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    public static void ConfigureSofaSmall()
    {
        GameObject sofaObj = GameObject.Find("PFB_SofaSmall");
        if (sofaObj == null)
        {
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < allObjects.Length; i++)
            {
                if (allObjects[i].name.Equals("PFB_SofaSmall", System.StringComparison.OrdinalIgnoreCase))
                {
                    sofaObj = allObjects[i];
                    break;
                }
            }
        }

        if (sofaObj != null)
        {
            sofaObj.isStatic = false;

            CabinChairInteractable cci = sofaObj.GetComponent<CabinChairInteractable>();
            if (cci == null)
            {
                cci = sofaObj.AddComponent<CabinChairInteractable>();
            }

            cci.requiresMoveFirst = false; // Directly "E to Sit"
            cci.standUpInFront = true;     // Stand up in front of sofa into living room
            cci.sittingEyeHeight = 1.08f;  // Relaxed lounge sitting eye height

            BoxCollider box = sofaObj.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = sofaObj.AddComponent<BoxCollider>();
            }

            MeshFilter mf = sofaObj.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                box.center = b.center;
                box.size = new Vector3(
                    Mathf.Max(b.size.x, 0.70f),
                    Mathf.Max(b.size.y, 0.85f),
                    Mathf.Max(b.size.z, 0.70f)
                );
            }
            else
            {
                box.center = new Vector3(0f, 0.42f, 0f);
                box.size = new Vector3(0.85f, 0.85f, 0.85f);
            }
            box.isTrigger = true;

            Debug.Log("[HouseSceneStoryController] Successfully configured PFB_SofaSmall for Sit.");
        }
    }

    public static void ConfigureFridge()
    {
        GameObject fridgeObj = GameObject.Find("PFB_Fridge");
        if (fridgeObj == null)
        {
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < allObjects.Length; i++)
            {
                if (allObjects[i].name.Equals("PFB_Fridge", System.StringComparison.OrdinalIgnoreCase))
                {
                    fridgeObj = allObjects[i];
                    break;
                }
            }
        }

        if (fridgeObj != null)
        {
            fridgeObj.isStatic = false;
            InteractableFoodItem.RepairAndPopulateFridge(fridgeObj);
            Debug.Log("[HouseSceneStoryController] Successfully repaired and configured PFB_Fridge with right-hand hinge, Egg Tray (8 eggs), and Pack of Bacon.");
        }
    }

    public static Transform FindCabinBuildingRoot()
    {
        GameObject full = GameObject.Find("PFB_Building_Full");
        if (full != null) return full.transform;
        GameObject baked = GameObject.Find("BakedLightSet_01");
        if (baked != null) return baked.transform;
        return null;
    }

    private IEnumerator Start()
    {
        HorrorMainMenuController.EnsureMainMenuShown();
        while (HorrorMainMenuController.IsMainMenuActive)
        {
            isIntroActive = true;
            blackOverlayAlpha = 1.0f;
            isMovementLocked = true;
            yield return null;
        }

        bool startInAct5 = isAct5Active
            || (PlayerManager.Instance != null && PlayerManager.Instance.currentStoryAct == 5)
            || PlayerPrefs.GetInt("HouseScene_ForceAct5", 0) == 1;

        bool startInAct3 = !startInAct5 && (isAct3Active
            || (PlayerManager.Instance != null && PlayerManager.Instance.currentStoryAct == 3)
            || PlayerPrefs.GetInt("HouseScene_ForceAct3", 0) == 1);

        if (startInAct5)
        {
            if (PlayerPrefs.GetInt("HouseScene_ForceAct5", 0) == 1)
            {
                PlayerPrefs.DeleteKey("HouseScene_ForceAct5");
            }
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.currentStoryAct = 5;
            }
            StartCoroutine(Act5StorySequenceRoutine());
        }
        else if (startInAct3)
        {
            if (PlayerPrefs.GetInt("HouseScene_ForceAct3", 0) == 1)
            {
                PlayerPrefs.DeleteKey("HouseScene_ForceAct3");
            }
            if (PlayerManager.Instance != null)
            {
                PlayerManager.Instance.currentStoryAct = 3;
            }
            StartCoroutine(Act3StorySequenceRoutine());
        }
        else
        {
            StartCoroutine(Act1IntroSequenceRoutine());
        }
    }

    private void Update()
    {
        if ((isDialogueVisible || isTyping || textAlpha > 0.01f) && HorrorHudStyle.WasSkipDialoguePressedThisFrame())
        {
            skipCurrentDialogueRequested = true;
        }

        if (isAct5Active)
        {
            return;
        }

        if (isAct3Active)
        {
            if (IsAct3LightCandleTaskActive && !IsDialogueActive)
            {
                bool ePressed = false;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ePressed = true;
#else
                if (Input.GetKeyDown(KeyCode.E)) ePressed = true;
#endif
                if (ePressed && CabinCandleInteractable.HeldCandle != null)
                {
                    CabinCandleInteractable.HeldCandle.LightUpCandle();
                }
            }
            return;
        }

        if (!hasPlacedFoodOnCounter && (InteractableFoodItem.AreBothEggAndBaconPlacedOnCounter() || hasCookedFood || hasCookedAndPlatedFood || hasEatenMeal))
        {
            hasPlacedFoodOnCounter = true;
        }

        if (currentStage == HouseStoryStage.TaskEnterCabin && !hasTriggeredCabinEntry)
        {
            Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
            if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
            if (cam != null)
            {
                Vector3 pos = cam.transform.position;
                // Cabin entrance threshold: player spawned at X=-29.8, double door is at X=-15.37.
                // Stepping into the cabin living room: X > -15.0f and Z between 303.0f and 325.0f.
                if (pos.x > -15.0f && pos.z >= 303.0f && pos.z <= 325.0f && pos.y > -0.5f)
                {
                    hasTriggeredCabinEntry = true;
                    StartCoroutine(InsideCabinSequenceRoutine());
                }
            }
        }

        if (currentStage == HouseStoryStage.TaskTurnOffAllLights && !hasTriggeredLightsComplete)
        {
            if (AreAllCabinLightsOff())
            {
                hasTriggeredLightsComplete = true;
                hasTurnedOffAllLights = true;
                StartCoroutine(AllLightsTurnedOffRoutine());
            }
        }

        if (currentStage == HouseStoryStage.TaskCloseFrontDoor && !IsFrontDoubleDoorOpen())
        {
            currentStage = HouseStoryStage.TaskGoToBed;
        }
    }

    private IEnumerator Act1IntroSequenceRoutine()
    {
        currentStage = HouseStoryStage.InitialDarkness;
        isMovementLocked = true;
        isIntroActive = true;
        blackOverlayAlpha = 1.0f;
        textAlpha = 0f;
        displayedText = "";

        // Brief dark pause before typing begins
        yield return new WaitForSeconds(0.5f);

        // --- LINE 1: " At home... " ---
        currentStage = HouseStoryStage.IntroTypingAtHome;
        yield return StartCoroutine(TypeTextRoutine(" At home... "));

        // 1.3 seconds cooldown (as specified by user)
        currentStage = HouseStoryStage.IntroCooldownAtHome;
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(1.3f, () => skipCurrentDialogueRequested));

        // Smoothly fade out Line 1 text
        yield return StartCoroutine(FadeOutTextRoutine(0.4f));

        yield return new WaitForSeconds(0.2f);

        // --- LINE 2: " Tired.. Hungry.. " ---
        currentStage = HouseStoryStage.IntroTypingTiredHungry;
        yield return StartCoroutine(TypeTextRoutine(" Tired.. Hungry.. "));

        // 1.5 seconds cooldown (as specified by user: "after Tired.. Hungry.. 1.5 seconds cooldown")
        currentStage = HouseStoryStage.IntroCooldownTiredHungry;
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(1.5f, () => skipCurrentDialogueRequested));

        // Smoothly fade out Line 2 text
        yield return StartCoroutine(FadeOutTextRoutine(0.40f));

        // --- PROCEED TO FADE IN TO GAME ---
        currentStage = HouseStoryStage.IntroFadingInToGame;
        float fadeDuration = 1.8f;
        float elapsedFade = 0f;
        while (elapsedFade < fadeDuration)
        {
            elapsedFade += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedFade / fadeDuration);
            blackOverlayAlpha = Mathf.SmoothStep(1.0f, 0.0f, t);
            yield return null;
        }

        blackOverlayAlpha = 0.0f;
        isIntroActive = false;
        isMovementLocked = false;
        currentStage = HouseStoryStage.TaskEnterCabin;

        Debug.Log("[HouseSceneStoryController] Act 1 Intro Complete. Task: Enter the cabin.");
    }

    private IEnumerator InsideCabinSequenceRoutine()
    {
        currentStage = HouseStoryStage.InsideCabinCooldown;
        // "when it enters the cabin, give it 3 seconds cooldown."
        yield return new WaitForSeconds(3.0f);

        // Lock player movement when the dialogue starts (player can still look around, matching GasScene2)
        isMovementLocked = true;

        // "You: What a long night.. "
        // "1.5 seconds"
        currentStage = HouseStoryStage.DialogueWhatALongNight;
        yield return StartCoroutine(PlayDialogueRoutine("You: What a long night.. ", 2.2f));
        yield return new WaitForSeconds(0.25f);

        // "You: I'm hungry, I wanna eat something. "
        // "1 second"
        currentStage = HouseStoryStage.DialogueImHungry;
        yield return StartCoroutine(PlayDialogueRoutine("You: I'm hungry, I wanna eat something. ", 2.0f));

        // "(unlocks the refrigerator)"
        // "Task: Find something in the fridge."
        isFridgeUnlocked = true;
        currentStage = HouseStoryStage.TaskFindSomethingInFridge;
        isMovementLocked = false;
    }

    public void NotifyFridgeOpened()
    {
        if (currentStage >= HouseStoryStage.TaskFindSomethingInFridge && !hasOpenedFridgeForStory)
        {
            hasOpenedFridgeForStory = true;
            StartCoroutine(FridgeOpenedSequenceRoutine());
        }
    }

    private IEnumerator FridgeOpenedSequenceRoutine()
    {
        currentStage = HouseStoryStage.DialogueAlrightTheseWillDo;
        // Wait 1 second after opening the fridge before speaking.
        // Movement stays unlocked here — opening the fridge is the only dialogue where the player can move.
        isMovementLocked = false;
        yield return new WaitForSeconds(1.0f);

        // "After opening the fridge"
        // "You: Alright, these will do I guess."
        // "1 second cooldown"
        yield return StartCoroutine(PlayDialogueRoutine("You: Alright, these will do I guess.", 2.0f));

        // "And allows to pick up now the items inside the refrigerator."
        isFoodPickupUnlocked = true;
    }

    public static void NotifyFoodItemPickedUp()
    {
        if (Instance != null)
        {
            Instance.hasPickedUpAtLeastOneFood = true;
            Instance.isStoveAndPanUnlocked = true;
            if (Instance.currentStage < HouseStoryStage.TasksCookingActive)
            {
                Instance.currentStage = HouseStoryStage.TasksCookingActive;
            }
        }
    }

    public static void NotifyFoodPlacedOnCounter()
    {
        if (Instance != null)
        {
            Instance.hasPlacedFoodOnCounter = true;
        }
    }

    public static void NotifyFoodCooked()
    {
        if (Instance != null)
        {
            Instance.hasCookedFood = true;
        }
    }

    public static void NotifyFoodCookedAndPlated()
    {
        if (Instance != null)
        {
            Instance.hasCookedFood = true;
            Instance.hasCookedAndPlatedFood = true;
        }
    }

    public static void NotifyMealFinished()
    {
        if (Instance != null)
        {
            Instance.hasEatenMeal = true;
            if (!Instance.hasStartedAfterEatingSequence)
            {
                Instance.hasStartedAfterEatingSequence = true;
                Instance.StartCoroutine(Instance.AfterEatingSequenceRoutine());
            }
        }
    }

    public static void NotifyUnsatAfterEating()
    {
        if (Instance != null && Instance.hasEatenMeal && !Instance.hasUnsatAfterEating)
        {
            Instance.hasUnsatAfterEating = true;
            if (!Instance.hasStartedAfterEatingSequence)
            {
                Instance.hasStartedAfterEatingSequence = true;
                Instance.StartCoroutine(Instance.AfterEatingSequenceRoutine());
            }
        }
    }

    private IEnumerator AfterEatingSequenceRoutine()
    {
        isMovementLocked = false;
        yield return new WaitForSeconds(1.8f);

        while (isDialogueVisible)
        {
            yield return null;
        }

        currentStage = HouseStoryStage.DialogueThatWasGood;
        isMovementLocked = true;
        // After finishing the meal on the dining table:
        // Dialouge will say:
        // "You: That was good. "
        yield return StartCoroutine(PlayDialogueRoutine("You: That was good. ", 2.2f));

        // "Task: Wash the plate"
        currentStage = HouseStoryStage.TaskWashPlate;
        isMovementLocked = false;
    }

    public static void NotifyPlateWashed()
    {
        if (Instance != null)
        {
            Instance.hasWashedPlate = true;
            if (Instance.currentStage < HouseStoryStage.TaskPutPlateBack)
            {
                Instance.currentStage = HouseStoryStage.TaskPutPlateBack;
            }
        }
    }

    public static void NotifyPlatePutBack()
    {
        if (Instance != null && !Instance.hasPutPlateBack)
        {
            Instance.hasPutPlateBack = true;
            Instance.currentStage = HouseStoryStage.PlatePutBackComplete;
            Instance.StartCoroutine(Instance.AfterPlatePutBackSequenceRoutine());
        }
    }

    private IEnumerator AfterPlatePutBackSequenceRoutine()
    {
        isMovementLocked = false;
        yield return new WaitForSeconds(1.8f);

        while (isDialogueVisible)
        {
            yield return null;
        }

        isMovementLocked = true;
        currentStage = HouseStoryStage.DialoguePutFoodBackInFridge;
        yield return StartCoroutine(PlayDialogueRoutine("You: I should put the eggs and bacon back in the fridge.", 2.0f));

        isMovementLocked = false;
        isReturnFoodToFridgeUnlocked = true;
        currentStage = HouseStoryStage.TaskPutFoodBackInFridge;
    }

    public static void NotifyFoodReturnedToFridge()
    {
        if (Instance == null) return;

        Instance.hasPutFoodBackInFridge = true;
        if (Instance.currentStage < HouseStoryStage.PutFoodBackInFridgeComplete)
        {
            Instance.currentStage = HouseStoryStage.PutFoodBackInFridgeComplete;
        }

        CabinFridgeInteractable fridge = CabinFridgeInteractable.ResolveInstance();
        if (fridge == null || !fridge.isOpen)
        {
            NotifyFridgeClosedAfterFoodReturn();
        }
        else if (!Instance.isMonitoringFridgeClose)
        {
            Instance.StartCoroutine(Instance.MonitorFridgeCloseRoutine());
        }
    }

    public static void NotifyFridgeClosedAfterFoodReturn()
    {
        if (Instance == null || !Instance.hasPutFoodBackInFridge || Instance.hasClosedFridgeAfterFoodReturn) return;

        Instance.hasClosedFridgeAfterFoodReturn = true;
        if (Instance.currentStage < HouseStoryStage.FridgeClosedAfterFoodReturn)
        {
            Instance.currentStage = HouseStoryStage.FridgeClosedAfterFoodReturn;
        }
        Instance.StartCoroutine(Instance.AfterFridgeClosedSequenceRoutine());
    }

    private IEnumerator AfterFridgeClosedSequenceRoutine()
    {
        // Wait 5 seconds cooldown after closing the fridge before TV thought dialogue begins
        yield return new WaitForSeconds(5.0f);

        while (isDialogueVisible)
        {
            yield return null;
        }

        // Lock player movement when the dialogue starts
        isMovementLocked = true;
        currentStage = HouseStoryStage.DialogueWatchTvIntro;

        yield return StartCoroutine(PlayDialogueRoutine("You: I wonder what channels are available on TV right now.", 2.2f));
        yield return new WaitForSeconds(0.25f);
        yield return StartCoroutine(PlayDialogueRoutine("You: Maybe I should watch some TV before I go to bed.", 2.2f));

        isMovementLocked = false;
        isTvTaskUnlocked = true;
        currentStage = HouseStoryStage.TaskTurnOnTv;
    }

    public static void NotifyTvTurnedOn()
    {
        if (Instance == null || !Instance.isTvTaskUnlocked) return;

        Instance.hasTurnedOnTvOnce = true;
        if (Instance.currentStage < HouseStoryStage.TvTurnedOn)
        {
            Instance.currentStage = HouseStoryStage.TvTurnedOn;
        }
    }

    public static void NotifyTvChannel5Reached()
    {
        if (Instance == null || !Instance.isTvTaskUnlocked || Instance.hasStartedTvNewsSequence) return;

        Instance.hasStartedTvNewsSequence = true;
        Instance.StartCoroutine(Instance.TvNewsCrashSequenceRoutine());
    }

    private IEnumerator TvNewsCrashSequenceRoutine()
    {
        // Let the player see the Channel 5 Breaking News visual and hear the stinger first
        yield return new WaitForSeconds(1.2f);

        while (isDialogueVisible)
        {
            yield return null;
        }

        isMovementLocked = true;
        currentStage = HouseStoryStage.DialogueTvNewsCrash;

        yield return StartCoroutine(PlayDialogueRoutine("TV: We interrupt tonight's broadcast with breaking news.", 2.2f));
        yield return new WaitForSeconds(0.25f);
        yield return StartCoroutine(PlayDialogueRoutine("TV: A serious crash was just reported near Jojo's Gas Station.", 2.4f));
        yield return new WaitForSeconds(0.25f);
        yield return StartCoroutine(PlayDialogueRoutine("TV: Police say the driver swerved off Route 9 in the heavy fog.", 2.2f));
        yield return new WaitForSeconds(0.25f);
        yield return StartCoroutine(PlayDialogueRoutine("TV: Authorities urge locals to stay alert near Route 9 tonight.", 2.4f));
        yield return new WaitForSeconds(0.40f);
        yield return StartCoroutine(PlayDialogueRoutine("You: Wait... Jojo's Gas Station? Isn't that near where I work?", 2.4f));
        yield return new WaitForSeconds(0.25f);
        yield return StartCoroutine(PlayDialogueRoutine("You: I better be careful out there on my shift tomorrow.", 2.4f));

        hasWatchedTvCrashNews = true;
        isMovementLocked = false;
        currentStage = HouseStoryStage.TaskKeepWatching;

        // 15-second cooldown while keeping watching (player remains seated & TV stays on)
        yield return new WaitForSeconds(15.0f);

        isMovementLocked = true;
        currentStage = HouseStoryStage.DialogueGoToSleepYawn;
        yield return StartCoroutine(PlayDialogueRoutine("You: *yawns*..", 2.0f));
        yield return new WaitForSeconds(0.25f);

        currentStage = HouseStoryStage.DialogueGoToSleepTime;
        yield return StartCoroutine(PlayDialogueRoutine("You: Alright.. I gotta go to sleep..", 2.2f));
        yield return new WaitForSeconds(2.0f);

        isMovementLocked = false;
        currentStage = HouseStoryStage.TaskTurnOffAllLights;

        // If the player had already turned off every cabin switch earlier, complete immediately
        if (AreAllCabinLightsOff() && !hasTriggeredLightsComplete)
        {
            hasTriggeredLightsComplete = true;
            hasTurnedOffAllLights = true;
            StartCoroutine(AllLightsTurnedOffRoutine());
        }
    }

    private IEnumerator AllLightsTurnedOffRoutine()
    {
        currentStage = HouseStoryStage.LightsTurnedOffComplete;
        yield return new WaitForSeconds(1.2f);

        isBedSleepUnlocked = true;
        currentStage = HouseStoryStage.TaskGoToBed;
    }

    public static bool AreAllCabinLightsOff()
    {
        CabinLightSwitch[] switches = Object.FindObjectsByType<CabinLightSwitch>(FindObjectsInactive.Exclude);
        if (switches == null || switches.Length == 0) return false;

        for (int i = 0; i < switches.Length; i++)
        {
            if (switches[i] != null && switches[i].isLightOn)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsFrontDoubleDoorOpen()
    {
        CabinDoorInteractable[] doors = Object.FindObjectsByType<CabinDoorInteractable>(FindObjectsInactive.Include);
        if (doors == null) return false;

        for (int i = 0; i < doors.Length; i++)
        {
            CabinDoorInteractable d = doors[i];
            if (d == null) continue;

            bool isDoubleDoor =
                (d.targetAnimator != null && d.targetAnimator.name.IndexOf("DoorDouble", System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                d.gameObject.name.IndexOf("DoorDouble", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isDoubleDoor && d.isOpen)
            {
                return true;
            }
        }

        return false;
    }

    public static void NotifyTriedSleepWithOpenDoor()
    {
        if (Instance == null || !Instance.isBedSleepUnlocked || Instance.isSleepingSequenceActive || Instance.isDoorReminderPlaying)
        {
            return;
        }

        Instance.StartCoroutine(Instance.ForgotDoorReminderRoutine());
    }

    private IEnumerator ForgotDoorReminderRoutine()
    {
        isDoorReminderPlaying = true;
        isMovementLocked = true;

        while (isDialogueVisible)
        {
            yield return null;
        }

        currentStage = HouseStoryStage.DialogueForgotCloseDoor;
        yield return StartCoroutine(PlayDialogueRoutine("You: I forgot to close the door.. I should go and check.", 2.6f));

        isMovementLocked = false;
        currentStage = IsFrontDoubleDoorOpen() ? HouseStoryStage.TaskCloseFrontDoor : HouseStoryStage.TaskGoToBed;
        isDoorReminderPlaying = false;
    }

    public static void NotifyBedSleepStarted(CabinBedInteractable bed)
    {
        if (Instance == null || !Instance.isBedSleepUnlocked || Instance.isSleepingSequenceActive)
        {
            return;
        }

        Instance.isSleepingSequenceActive = true;
        Instance.StartCoroutine(Instance.SleepAndTransitionRoutine(bed));
    }

    private IEnumerator SleepAndTransitionRoutine(CabinBedInteractable bed)
    {
        isSleepingSequenceActive = true;
        isMovementLocked = true;
        currentStage = HouseStoryStage.SleepingInBed;
        isDialogueVisible = false;
        dialogueAlpha = 0f;
        displayedDialogueText = "";
        eyelidCloseProgress = 0f;
        blackOverlayAlpha = 0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Camera playerCam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (playerCam == null) playerCam = HorrorInteractionReticle.GetPlayerCamera();
        Transform playerTransform = playerCam != null && playerCam.transform.parent != null
            ? playerCam.transform.parent
            : (playerCam != null ? playerCam.transform : null);

        if (bed != null)
        {
            yield return StartCoroutine(bed.AnimateLieDownAndSleepRoutine(
                playerTransform,
                playerCam,
                progress =>
                {
                    eyelidCloseProgress = Mathf.Clamp01(progress);
                    if (eyelidCloseProgress > 0.72f)
                    {
                        float k = Mathf.InverseLerp(0.72f, 1.0f, eyelidCloseProgress);
                        blackOverlayAlpha = Mathf.SmoothStep(0f, 1f, k);
                    }
                    else
                    {
                        blackOverlayAlpha = 0f;
                    }
                }));
        }

        eyelidCloseProgress = 1.0f;
        blackOverlayAlpha = 1.0f;

        // Quietly power down the TV once eyes are fully shut
        if (CabinTvController.Instance != null)
        {
            CabinTvController.Instance.PowerOffSilentlyForSleep();
        }

        // Wait 4.5 seconds once eyes are completely closed before typing sleep message
        yield return new WaitForSeconds(4.5f);

        yield return StartCoroutine(TypeTextRoutine(" You slept the whole day.. "));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(2.0f, () => skipCurrentDialogueRequested));

        yield return StartCoroutine(FadeOutTextRoutine(0.45f));
        yield return new WaitForSeconds(0.2f);

        yield return StartCoroutine(TypeTextRoutine(" You woke up.. and almost start with your night shift.. "));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(1.5f, () => skipCurrentDialogueRequested));

        yield return StartCoroutine(FadeOutTextRoutine(0.45f));
        yield return new WaitForSeconds(0.25f);

        // Cleanly reset FPS controller pose & House-only held remote before loading GasScene2
        CabinTvRemoteInteractable.ForceClearForSceneTransition();
        if (playerTransform != null)
        {
            SimpleFPSController fps = playerTransform.GetComponent<SimpleFPSController>();
            if (fps != null)
            {
                fps.ResetAfterExternalPose();
            }
        }

        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.AdvanceStoryAct();
            PlayerManager.Instance.TransitionToScene("GasScene2", "Default");
        }
        else
        {
            SceneManager.LoadScene("GasScene2");
        }
    }

    private IEnumerator MonitorFridgeCloseRoutine()
    {
        if (isMonitoringFridgeClose) yield break;
        isMonitoringFridgeClose = true;

        float awayOpenTimer = 0f;
        const float reminderCooldownSeconds = 5.0f;
        const float walkAwayDistanceXZ = 2.25f;

        while (!hasClosedFridgeAfterFoodReturn)
        {
            CabinFridgeInteractable fridge = CabinFridgeInteractable.ResolveInstance();
            if (fridge == null || !fridge.isOpen)
            {
                NotifyFridgeClosedAfterFoodReturn();
                isMonitoringFridgeClose = false;
                yield break;
            }

            bool walkedAway = IsPlayerWalkedAwayFromFridge(fridge, walkAwayDistanceXZ);
            if (!isDialogueVisible && !isMovementLocked && walkedAway)
            {
                awayOpenTimer += Time.deltaTime;
                if (awayOpenTimer >= reminderCooldownSeconds)
                {
                    awayOpenTimer = 0f;
                    string line = CloseFridgeReminderLines[Random.Range(0, CloseFridgeReminderLines.Length)];

                    isMovementLocked = true;
                    yield return StartCoroutine(PlayDialogueRoutine(line, 2.0f));
                    isMovementLocked = false;

                    // Re-check close state immediately and enforce a fresh 5s cooldown before repeating
                    awayOpenTimer = 0f;
                    continue;
                }
            }
            else if (!walkedAway)
            {
                // Player returned near the fridge — reset timer so closing it isn't interrupted
                awayOpenTimer = 0f;
            }

            yield return null;
        }

        isMonitoringFridgeClose = false;
    }

    private static bool IsPlayerWalkedAwayFromFridge(CabinFridgeInteractable fridge, float thresholdXZ)
    {
        if (fridge == null) return false;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        Vector2 playerXZ = new Vector2(cam.transform.position.x, cam.transform.position.z);
        Vector2 fridgeXZ = new Vector2(fridge.transform.position.x, fridge.transform.position.z);
        return Vector2.Distance(playerXZ, fridgeXZ) > thresholdXZ;
    }

    public IEnumerator PlayDialogueRoutine(string line, float holdTime)
    {
        skipCurrentDialogueRequested = false;
        isDialogueVisible = true;
        dialogueAlpha = 1.0f;
        displayedDialogueText = "";
        isDialogueTyping = true;

        int colonIdx = line.IndexOf(':');
        int speechStartIdx = (colonIdx >= 0 && colonIdx < 16) ? colonIdx + 1 : 0;
        int spokenCharCount = 0;

        for (int i = 0; i < line.Length; i++)
        {
            if (skipCurrentDialogueRequested) break;

            displayedDialogueText = line.Substring(0, i + 1);
            char c = line[i];
            if (i >= speechStartIdx && char.IsLetterOrDigit(c))
            {
                if (spokenCharCount % 2 == 0)
                {
                    PlayTypeClick(0.14f, 0.85f, 1.02f);
                }
                spokenCharCount++;
            }

            float delay = 0.062f;
            if (c == '.' || c == '!' || c == '?') delay = 0.24f;
            else if (c == ',') delay = 0.12f;

            yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(delay, () => skipCurrentDialogueRequested));
        }

        isDialogueTyping = false;

        if (!skipCurrentDialogueRequested)
        {
            displayedDialogueText = line;
            yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(holdTime, () => skipCurrentDialogueRequested));
        }

        // Smooth fade out
        float fadeTime = 0.70f;
        float elapsed = 0f;
        while (!skipCurrentDialogueRequested && elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            dialogueAlpha = Mathf.Clamp01(1.0f - (elapsed / fadeTime));
            yield return null;
        }

        dialogueAlpha = 0f;
        displayedDialogueText = "";
        isDialogueVisible = false;
        skipCurrentDialogueRequested = false;
    }

    private IEnumerator TypeTextRoutine(string fullText)
    {
        skipCurrentDialogueRequested = false;
        displayedText = "";
        textAlpha = 1.0f;
        isTyping = true;

        for (int i = 0; i < fullText.Length; i++)
        {
            if (skipCurrentDialogueRequested) break;

            displayedText = fullText.Substring(0, i + 1);

            char c = fullText[i];
            if (c != ' ')
            {
                PlayTypeClick();
            }

            float delay = 0.085f;
            if (c == '.') delay = 0.22f;
            else if (c == ',') delay = 0.12f;

            yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(delay, () => skipCurrentDialogueRequested));
        }

        if (skipCurrentDialogueRequested)
        {
            displayedText = fullText;
        }
        isTyping = false;
    }

    private IEnumerator FadeOutTextRoutine(float duration)
    {
        float elapsed = 0f;
        while (!skipCurrentDialogueRequested && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            textAlpha = Mathf.Clamp01(1.0f - (elapsed / duration));
            yield return null;
        }
        textAlpha = 0f;
        displayedText = "";
        skipCurrentDialogueRequested = false;
    }

    private void SetupAudio()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.0f;

        // Procedural crisp retro typewriter key click
        int sampleRate = 44100;
        int clickCount = (int)(sampleRate * 0.024f);
        float[] clickSamples = new float[clickCount];

        for (int i = 0; i < clickCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 220f);
            float noise = (Random.value * 2f - 1f) * 0.6f;
            float tone = Mathf.Sin(2f * Mathf.PI * 1850f * t) * 0.4f;
            clickSamples[i] = (noise + tone) * env * 0.45f;
        }

        typeClickClip = AudioClip.Create("HouseTypewriterClick", clickCount, 1, sampleRate, false);
        typeClickClip.SetData(clickSamples, 0);
    }

    private void PlayTypeClick(float volume = 0.18f, float minPitch = 0.92f, float maxPitch = 1.15f)
    {
        if (audioSource != null && typeClickClip != null)
        {
            audioSource.pitch = Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(typeClickClip, volume);
        }
    }

    private void InitTextures()
    {
        if (pixelTexture == null)
        {
            pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixelTexture.SetPixel(0, 0, Color.white);
            pixelTexture.Apply();
        }

        if (upperEyelidTexture == null)
        {
            const int h = 64;
            const int featherRows = 16;
            upperEyelidTexture = new Texture2D(1, h, TextureFormat.RGBA32, false);
            upperEyelidTexture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                // Bottom rows (y = 0..featherRows-1) feather softly from transparent to solid black
                float a = y < featherRows
                    ? Mathf.SmoothStep(0f, 1f, y / (float)featherRows)
                    : 1f;
                upperEyelidTexture.SetPixel(0, y, new Color(0f, 0f, 0f, a));
            }
            upperEyelidTexture.Apply();
        }

        if (lowerEyelidTexture == null)
        {
            const int h = 64;
            const int featherRows = 16;
            lowerEyelidTexture = new Texture2D(1, h, TextureFormat.RGBA32, false);
            lowerEyelidTexture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                // Top rows (y = h-featherRows..h-1) feather softly from solid black to transparent
                int distFromTop = (h - 1) - y;
                float a = distFromTop < featherRows
                    ? Mathf.SmoothStep(0f, 1f, distFromTop / (float)featherRows)
                    : 1f;
                lowerEyelidTexture.SetPixel(0, y, new Color(0f, 0f, 0f, a));
            }
            lowerEyelidTexture.Apply();
        }
    }

    private void InitFont()
    {
        if (retroFont == null)
        {
            retroFont = HorrorHudStyle.GetRetroFont();
        }
    }

    private void SetupStyles()
    {
        InitFont();

        if (titleStyle == null)
        {
            titleStyle = HorrorHudStyle.CreateTitleStyle();
            titleShadowStyle = new GUIStyle(titleStyle);
        }

        if (dialogueStyle == null)
        {
            dialogueStyle = HorrorHudStyle.CreateDialogueStyle();
            dialogueShadowStyle = new GUIStyle(dialogueStyle);
        }

        if (taskHeaderStyle == null)
        {
            taskHeaderStyle = HorrorHudStyle.CreateTaskHeaderStyle();
            taskHeaderShadowStyle = new GUIStyle(taskHeaderStyle);
        }

        if (taskSubStyle == null)
        {
            taskSubStyle = HorrorHudStyle.CreateTaskSubStyle(taskHeaderStyle);
            taskSubShadowStyle = new GUIStyle(taskSubStyle);
        }
    }

    private void DrawDialogueBox()
    {
        HorrorHudStyle.DrawDialogueBox(
            pixelTexture,
            displayedDialogueText,
            isDialogueTyping,
            dialogueAlpha,
            dialogueStyle,
            dialogueShadowStyle);
    }

    private void DrawTopLeftTaskHUD()
    {
        float startX = HorrorHudStyle.TaskStartX;
        float startY = HorrorHudStyle.TaskStartY;

        Color goldColor = HorrorHudStyle.ActiveTaskColor;
        Color greenColor = HorrorHudStyle.CompletedTaskColor;
        Color shadowCol = HorrorHudStyle.TaskShadowColor;

        if (isAct5Active)
        {
            if (act5ShowWashFaceTask)
            {
                DrawSingleTaskLine(startX, startY, "Task: Wash your face", goldColor, shadowCol);
                HorrorHudStyle.DrawTaskLine(startX + 10f, startY + 24f, "- Severe your head.", HorrorHudStyle.TaskSubColor, taskSubStyle, taskSubShadowStyle);
            }
            return;
        }

        if (isAct3Active)
        {
            DrawAct3TaskHUD(startX, startY, goldColor, greenColor, shadowCol);
            return;
        }

        if (currentStage == HouseStoryStage.TaskEnterCabin)
        {
            DrawSingleTaskLine(startX, startY, "Task: Enter the cabin", goldColor, shadowCol);
        }
        else if (currentStage == HouseStoryStage.InsideCabinCooldown ||
                 currentStage == HouseStoryStage.DialogueWhatALongNight ||
                 currentStage == HouseStoryStage.DialogueImHungry)
        {
            DrawSingleTaskLine(startX, startY, "Task: Enter the cabin [COMPLETED]", greenColor, shadowCol);
        }
        else if (currentStage == HouseStoryStage.TaskFindSomethingInFridge ||
                 currentStage == HouseStoryStage.DialogueAlrightTheseWillDo)
        {
            DrawSingleTaskLine(startX, startY, "Task: Find something in the fridge.", goldColor, shadowCol);
        }
        else if (currentStage >= HouseStoryStage.TasksCookingActive && currentStage < HouseStoryStage.DialogueThatWasGood)
        {
            bool eatTaskDone = hasEatenMeal;
            string eatTaskText = eatTaskDone ? "Task: Find a way to eat. [COMPLETED]" : "Task: Find a way to eat.";
            DrawSingleTaskLine(startX, startY, eatTaskText, eatTaskDone ? greenColor : goldColor, shadowCol);
        }
        else if (currentStage >= HouseStoryStage.TaskWashPlate && currentStage < HouseStoryStage.TaskPutFoodBackInFridge)
        {
            float y = startY;
            bool washDone = hasWashedPlate;
            string washText = washDone ? "Task: Wash the plate [COMPLETED]" : "Task: Wash the plate";
            DrawSingleTaskLine(startX, y, washText, washDone ? greenColor : goldColor, shadowCol);
            y += 24f;

            if (hasWashedPlate || currentStage >= HouseStoryStage.TaskPutPlateBack)
            {
                bool putBackDone = hasPutPlateBack;
                string putBackText = putBackDone ? "Task: Put the plate back [COMPLETED]" : "Task: Put the plate back";
                DrawSingleTaskLine(startX, y, putBackText, putBackDone ? greenColor : goldColor, shadowCol);
            }
        }
        else if (currentStage >= HouseStoryStage.TaskPutFoodBackInFridge && currentStage < HouseStoryStage.TaskTurnOnTv)
        {
            bool fridgeReturnDone = hasPutFoodBackInFridge || currentStage >= HouseStoryStage.PutFoodBackInFridgeComplete;
            string fridgeReturnText;
            if (fridgeReturnDone)
            {
                fridgeReturnText = "Task: Put the Egg Tray and Bacon Pack back inside the fridge. [COMPLETED]";
            }
            else
            {
                bool eggBack = InteractableFoodItem.IsItemReturnedToFridge("EGGS");
                bool baconBack = InteractableFoodItem.IsItemReturnedToFridge("BACON");
                if (eggBack && !baconBack)
                {
                    fridgeReturnText = "Task: Put the Bacon Pack back inside the fridge (1/2 placed).";
                }
                else if (!eggBack && baconBack)
                {
                    fridgeReturnText = "Task: Put the Egg Tray back inside the fridge (1/2 placed).";
                }
                else
                {
                    fridgeReturnText = "Task: Put the Egg Tray and Bacon Pack back inside the fridge.";
                }
            }

            DrawSingleTaskLine(startX, startY, fridgeReturnText, fridgeReturnDone ? greenColor : goldColor, shadowCol);
        }
        else if (currentStage >= HouseStoryStage.TaskTurnOnTv && currentStage < HouseStoryStage.TaskKeepWatching)
        {
            bool tvDone = hasTurnedOnTvOnce || currentStage >= HouseStoryStage.TvTurnedOn;
            string tvText = tvDone ? "Task: Turn on the TV [COMPLETED]" : "Task: Turn on the TV";
            DrawSingleTaskLine(startX, startY, tvText, tvDone ? greenColor : goldColor, shadowCol);
        }
        else if (currentStage >= HouseStoryStage.TaskKeepWatching && currentStage < HouseStoryStage.TaskTurnOffAllLights)
        {
            bool keepDone = currentStage >= HouseStoryStage.DialogueGoToSleepYawn;
            string keepText = keepDone ? "Task: Keep Watching [COMPLETED]" : "Task: Keep Watching";
            DrawSingleTaskLine(startX, startY, keepText, keepDone ? greenColor : goldColor, shadowCol);
        }
        else if (currentStage >= HouseStoryStage.TaskTurnOffAllLights && currentStage < HouseStoryStage.TaskGoToBed)
        {
            bool lightsDone = hasTurnedOffAllLights || currentStage >= HouseStoryStage.LightsTurnedOffComplete;
            string lightsText = lightsDone ? "Task: Turn off all the lights [COMPLETED]" : "Task: Turn off all the lights";
            DrawSingleTaskLine(startX, startY, lightsText, lightsDone ? greenColor : goldColor, shadowCol);
        }
        else if (currentStage == HouseStoryStage.TaskCloseFrontDoor)
        {
            DrawSingleTaskLine(startX, startY, "Task: Close the front door.", goldColor, shadowCol);
        }
        else if (currentStage >= HouseStoryStage.TaskGoToBed && currentStage < HouseStoryStage.SleepingInBed)
        {
            DrawSingleTaskLine(startX, startY, "Task: Go to bed.", goldColor, shadowCol);
        }
    }

    private void DrawSingleTaskLine(float x, float y, string text, Color textColor, Color shadowColor)
    {
        HorrorHudStyle.DrawTaskLine(x, y, text, textColor, taskHeaderStyle, taskHeaderShadowStyle);
    }

    private void OnGUI()
    {
        if (HorrorMainMenuController.IsMainMenuActive) return;
        if (Event.current.type != EventType.Repaint) return;

        SetupStyles();
        InitTextures();

        int prevDepth = GUI.depth;
        GUI.depth = -1000;

        float sw = Screen.width;
        float sh = Screen.height;

        // 1. Draw Realistic Closing Eyelids (when sleeping)
        if (eyelidCloseProgress > 0.001f && blackOverlayAlpha < 0.999f)
        {
            float p = Mathf.Clamp01(eyelidCloseProgress);
            float drowsyDim = Mathf.Clamp01(p * 0.42f);
            if (drowsyDim > 0.001f)
            {
                GUI.color = new Color(0f, 0f, 0f, drowsyDim);
                GUI.DrawTexture(new Rect(0f, 0f, sw, sh), pixelTexture);
            }

            float lidReach = p * (sh * 0.68f);

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0f, 0f, sw, lidReach), upperEyelidTexture);
            GUI.DrawTexture(new Rect(0f, sh - lidReach, sw, lidReach), lowerEyelidTexture);

            if (p >= 0.96f)
            {
                float fullClosedAlpha = Mathf.Clamp01((p - 0.96f) / 0.04f);
                GUI.color = new Color(0f, 0f, 0f, fullClosedAlpha);
                GUI.DrawTexture(new Rect(0f, 0f, sw, sh), pixelTexture);
                GUI.color = Color.white;
            }
        }

        // 2. Draw Full-Screen Black Overlay
        if (blackOverlayAlpha > 0.001f)
        {
            GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(blackOverlayAlpha));
            GUI.DrawTexture(new Rect(0f, 0f, sw, sh), pixelTexture);
            GUI.color = Color.white;
        }

        // 3. Draw Typewriter Text (Centered)
        if (textAlpha > 0.001f && !string.IsNullOrEmpty(displayedText))
        {
            HorrorHudStyle.DrawCenteredTitle(displayedText, isTyping, textAlpha, titleStyle, titleShadowStyle);
        }

        // 4. Draw In-Game Top-Left Task HUD
        if (!isIntroActive && !isSleepingSequenceActive && blackOverlayAlpha <= 0.85f)
        {
            DrawTopLeftTaskHUD();

            if (IsAct3LightCandleTaskActive && !IsDialogueActive)
            {
                GUIStyle promptStyle = HorrorHudStyle.CreatePromptStyle();
                GUIStyle promptShadow = new GUIStyle(promptStyle);
                Rect promptRect = new Rect(sw * 0.5f - 160f, sh * 0.5f + 22f, 320f, 30f);
                HorrorHudStyle.DrawOutlinedLabel(
                    promptRect,
                    "E to Light it up",
                    promptShadow,
                    HorrorHudStyle.PromptShadowColor,
                    promptStyle,
                    HorrorHudStyle.PromptTextColor);
            }
        }

        // 5. Draw Lower-Third Dialogue Box (also renders on top of black screen for Act 5 opening & ending dialogue)
        if (isDialogueVisible && dialogueAlpha > 0.001f && !string.IsNullOrEmpty(displayedDialogueText))
        {
            DrawDialogueBox();
        }

        GUI.depth = prevDepth;
    }

    // =========================================================================
    // ACT 3: HOUSE SCENE (RETURN HOME - MID-GAME STORY PROGRESSION)
    // =========================================================================

    private void DrawAct3TaskHUD(float startX, float startY, Color goldColor, Color greenColor, Color shadowCol)
    {
        Color subColor = HorrorHudStyle.TaskSubColor;
        Color subDoneColor = HorrorHudStyle.CompletedTaskSubColor;

        if (act3Stage == Act3Stage.TaskWashFace || act3Stage == Act3Stage.WashingFaceAndOutage)
        {
            bool done = hasCompletedWashFace;
            DrawSingleTaskLine(startX, startY, done ? "Task: Wash your face [COMPLETED]" : "Task: Wash your face", done ? greenColor : goldColor, shadowCol);
        }
        else if (act3Stage == Act3Stage.TaskTryLightSwitch || act3Stage == Act3Stage.LightSwitchCompleted)
        {
            bool done = act3Stage == Act3Stage.LightSwitchCompleted;
            DrawSingleTaskLine(startX, startY, done ? "Task: Try turning on and off any light. [COMPLETED]" : "Task: Try turning on and off any light.", done ? greenColor : goldColor, shadowCol);
        }
        else if (act3Stage == Act3Stage.TaskFindCandle || act3Stage == Act3Stage.CandleFoundDialogue)
        {
            bool done = hasPickedUpCandle;
            DrawSingleTaskLine(startX, startY, done ? "Task: Find your candle somewhere. [COMPLETED]" : "Task: Find your candle somewhere.", done ? greenColor : goldColor, shadowCol);
            HorrorHudStyle.DrawTaskLine(startX + 10f, startY + 24f, "- Somewhere in the living room.", done ? subDoneColor : subColor, taskSubStyle, taskSubShadowStyle);
        }
        else if (act3Stage == Act3Stage.TaskLightCandle || act3Stage == Act3Stage.CandleLitDialogue)
        {
            bool done = hasLitCandle;
            DrawSingleTaskLine(startX, startY, done ? "Task: Light up the candle [COMPLETED]" : "Task: Light up the candle", done ? greenColor : goldColor, shadowCol);
        }
        else if (act3Stage == Act3Stage.TaskFindPhone || act3Stage == Act3Stage.PhoneFoundDialogue)
        {
            bool done = hasPickedUpPhone;
            DrawSingleTaskLine(startX, startY, done ? "Task: Find your phone [COMPLETED]" : "Task: Find your phone", done ? greenColor : goldColor, shadowCol);
            HorrorHudStyle.DrawTaskLine(startX + 10f, startY + 24f, "- Somewhere in the bedroom", done ? subDoneColor : subColor, taskSubStyle, taskSubShadowStyle);
        }
        else if (act3Stage == Act3Stage.TaskMakeCall || act3Stage == Act3Stage.ManagerCallInProgress)
        {
            bool done = act3Stage == Act3Stage.ManagerCallInProgress && !IsAct3MakeCallTaskActive;
            DrawSingleTaskLine(startX, startY, done ? "Task: Make a call. [COMPLETED]" : "Task: Make a call.", done ? greenColor : goldColor, shadowCol);
        }
        else if (act3Stage == Act3Stage.TaskFindNoise ||
                 act3Stage == Act3Stage.LookingAtMonsterAndBlink ||
                 act3Stage == Act3Stage.LookingAtDeadPigeon)
        {
            bool done = hasInvestigatedBrokenWindow;
            DrawSingleTaskLine(startX, startY, done ? "Task: Find where the noise is. [COMPLETED]" : "Task: Find where the noise is.", done ? greenColor : goldColor, shadowCol);
        }
    }

    public static void NotifyLightSwitchToggledDuringOutage(CabinLightSwitch sw)
    {
        if (Instance == null || !Instance.isAct3Active) return;
        Instance.outageSwitchToggleCount++;
        if (Instance.firstOutageSwitchToggleTime < 0f)
        {
            Instance.firstOutageSwitchToggleTime = Time.time;
        }
    }

    public void TriggerAct3FaceWash(Transform sinkTransform, CabinKitchenSinkStation kitchenSink = null, CabinBathroomSinkFaceWash bathroomSink = null)
    {
        if (!isAct3Active || act3Stage != Act3Stage.TaskWashFace || isWashingFaceRoutineRunning) return;
        StartCoroutine(Act3WashFaceAndOutageRoutine(sinkTransform, kitchenSink, bathroomSink));
    }

    public void NotifyAct3CandlePickedUp(CabinCandleInteractable candle)
    {
        hasPickedUpCandle = true;
    }

    public void NotifyAct3CandleLit(CabinCandleInteractable candle)
    {
        hasLitCandle = true;
    }

    public void NotifyAct3PhonePickedUp(CabinPhoneInteractable phone)
    {
        hasPickedUpPhone = true;
    }

    public void NotifyAct3CallInitiated()
    {
        hasInitiatedCall = true;
    }

    public void NotifyAct3BrokenWindowInspected()
    {
        if (!isAct3Active || act3Stage != Act3Stage.TaskFindNoise) return;
        hasPlayerPressedInspectWindow = true;
    }

    private void LockAndCloseFrontDoorForAct3()
    {
        isFrontDoorLocked = true;
        CabinDoorInteractable[] doors = Object.FindObjectsByType<CabinDoorInteractable>(FindObjectsInactive.Include);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null && doors[i].IsFrontEntranceDoubleDoor())
            {
                doors[i].ForceClosedImmediate();
            }
        }
    }

    private void EnsureAllCabinLightsOnBeforeOutage()
    {
        isPowerOutageActive = false;
        CabinLightSwitch[] switches = Object.FindObjectsByType<CabinLightSwitch>(FindObjectsInactive.Include);
        for (int i = 0; i < switches.Length; i++)
        {
            if (switches[i] == null) continue;
            if (switches[i].controlledLights == null || switches[i].controlledLights.Count == 0)
            {
                switches[i].AutoDiscoverRoomLights();
            }
            switches[i].isLightOn = true;
            switches[i].ApplyLightState(true, playSound: false);
        }
    }

    private IEnumerator Act3StorySequenceRoutine()
    {
        isAct3Active = true;
        act3Stage = Act3Stage.IntroBlackScreen;
        isIntroActive = true;
        isMovementLocked = true;
        isSleepingSequenceActive = false;
        SimpleFPSController.IsExternalCameraAnimationActive = true;

        blackOverlayAlpha = 1.0f;
        eyelidCloseProgress = 1.0f;
        hasPlayerPressedInspectWindow = false;
        hasInvestigatedBrokenWindow = false;
        textAlpha = 0f;
        displayedText = "";

        Transform buildingRoot = FindCabinBuildingRoot();

        // 1. Lock & close front double door so it cannot be opened starting in Act 3
        LockAndCloseFrontDoorForAct3();

        // 2. Ensure cabin lights are initially ON (they will go off suddenly after washing face)
        EnsureAllCabinLightsOnBeforeOutage();

        // 3. Setup heavy realistic rain with thunder, monster_lowqual (hidden), candle on TV stand, phone on bedroom nightstand
        HouseAct3PropController propCtrl = HouseAct3PropController.EnsureExists(buildingRoot);
        propCtrl.SetupAct3PropsAndWeather(buildingRoot);

        // 4. Place player sleeping in PFB_Bed
        SimpleFPSController fps = Object.FindAnyObjectByType<SimpleFPSController>();
        CharacterController cc = fps != null ? fps.GetComponent<CharacterController>() : null;
        Camera cam = fps != null && fps.playerCamera != null ? fps.playerCamera : Camera.main;
        CabinBedInteractable bed = Object.FindAnyObjectByType<CabinBedInteractable>();

        if (cc != null) cc.enabled = false;

        Vector3 bedHeadWorld = Vector3.zero;
        Quaternion bedLieRot = Quaternion.identity;
        Vector3 bedEdgeSitPos = Vector3.zero;
        Quaternion bedSitRot = Quaternion.identity;
        Vector3 bedStandFootPos = Vector3.zero;
        Quaternion bedStandEyeRot = Quaternion.identity;

        if (bed != null)
        {
            bedHeadWorld = bed.transform.TransformPoint(new Vector3(0.0f, 0.80f, -0.78f));
            Vector3 footDir = bed.transform.TransformDirection(new Vector3(-0.12f, 0.30f, 0.94f)).normalized;
            bedLieRot = Quaternion.LookRotation(footDir, Vector3.up) * Quaternion.Euler(0f, 0f, 7.5f);

            bedEdgeSitPos = bed.transform.TransformPoint(new Vector3(-0.58f, 1.18f, -0.25f));
            Vector3 sitLookDir = bed.transform.TransformDirection(new Vector3(-1f, -0.05f, 0.15f)).normalized;
            bedSitRot = Quaternion.LookRotation(sitLookDir, Vector3.up);

            bedStandFootPos = bed.transform.TransformPoint(new Vector3(-1.08f, 0.02f, -0.20f));
            Vector3 standLookDir = bed.transform.TransformDirection(new Vector3(-0.92f, 0.0f, 0.38f)).normalized;
            bedStandEyeRot = Quaternion.LookRotation(standLookDir, Vector3.up);
        }
        else if (buildingRoot != null)
        {
            bedHeadWorld = buildingRoot.TransformPoint(new Vector3(2.843f, 0.92f, -8.95f));
            bedLieRot = Quaternion.Euler(-15f, 0f, 7.5f);
            bedEdgeSitPos = buildingRoot.TransformPoint(new Vector3(2.26f, 1.30f, -8.40f));
            bedSitRot = Quaternion.Euler(4f, -90f, 0f);
            bedStandFootPos = buildingRoot.TransformPoint(new Vector3(1.76f, 0.12f, -8.35f));
            bedStandEyeRot = Quaternion.Euler(0f, -70f, 0f);
        }

        if (fps != null && cam != null)
        {
            fps.transform.position = bedStandFootPos;
            cam.transform.position = bedHeadWorld;
            cam.transform.rotation = bedLieRot;
        }

        // --- BLACK SCREEN INTRO SEQUENCE ---
        // "2 seconds cooldown"
        yield return new WaitForSeconds(2.0f);

        // "At home..."
        yield return StartCoroutine(TypeTextRoutine(" At home... "));
        // "2 seconds cooldown"
        yield return new WaitForSeconds(2.0f);
        yield return StartCoroutine(FadeOutTextRoutine(0.4f));
        yield return new WaitForSeconds(0.15f);

        // "Again.."
        yield return StartCoroutine(TypeTextRoutine(" Again.. "));
        // "2 seconds.."
        yield return new WaitForSeconds(2.0f);
        yield return StartCoroutine(FadeOutTextRoutine(0.4f));
        yield return new WaitForSeconds(0.15f);

        // Trigger a distant thunder / knock noise right as "You wake up with a noise.." appears
        if (propCtrl.rainStormController != null)
        {
            propCtrl.rainStormController.TriggerImmediateLightningAndThunder();
        }
        propCtrl.PlayDoorKnockSound(buildingRoot);

        // "You wake up with a noise.."
        yield return StartCoroutine(TypeTextRoutine(" You wake up with a noise.. "));
        yield return new WaitForSeconds(2.0f);
        yield return StartCoroutine(FadeOutTextRoutine(0.45f));

        // --- REALISTIC SLOW WAKING UP & GETTING OUT OF BED ANIMATION ---
        act3Stage = Act3Stage.WakingUpInBed;
        isIntroActive = false;

        // Phase 1: Fade from black while eyelids slowly open and blink drowsily on the pillow (2.2s)
        float wakeDur = 2.2f;
        float elapsed = 0f;
        while (elapsed < wakeDur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / wakeDur);
            blackOverlayAlpha = Mathf.SmoothStep(1.0f, 0.0f, Mathf.Clamp01(u * 1.4f));

            // Drowsy eyelid flutter before opening
            if (u < 0.45f)
            {
                eyelidCloseProgress = Mathf.Lerp(1.0f, 0.35f, Mathf.SmoothStep(0f, 1f, u / 0.45f));
            }
            else if (u < 0.65f)
            {
                eyelidCloseProgress = Mathf.Lerp(0.35f, 0.72f, Mathf.SmoothStep(0f, 1f, (u - 0.45f) / 0.20f));
            }
            else
            {
                eyelidCloseProgress = Mathf.Lerp(0.72f, 0.0f, Mathf.SmoothStep(0f, 1f, (u - 0.65f) / 0.35f));
            }

            if (cam != null)
            {
                cam.transform.position = bedHeadWorld + new Vector3(0f, Mathf.Sin(u * Mathf.PI) * 0.03f, 0f);
                cam.transform.rotation = Quaternion.Slerp(bedLieRot, bedLieRot * Quaternion.Euler(-6f, -12f, -4f), u);
            }
            yield return null;
        }
        blackOverlayAlpha = 0.0f;
        eyelidCloseProgress = 0.0f;

        // Phase 2: Slowly push up from pillow to sitting on the edge of the mattress (2.1s)
        Vector3 p2StartPos = cam != null ? cam.transform.position : bedHeadWorld;
        Quaternion p2StartRot = cam != null ? cam.transform.rotation : bedLieRot;
        float sitUpDur = 2.1f;
        elapsed = 0f;
        while (elapsed < sitUpDur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / sitUpDur));
            if (cam != null)
            {
                Vector3 pos = Vector3.Lerp(p2StartPos, bedEdgeSitPos, u);
                pos.y += Mathf.Sin(u * Mathf.PI) * 0.06f;
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.Slerp(p2StartRot, bedSitRot, u);
            }
            yield return null;
        }

        // Brief pause sitting on the edge of the bed
        yield return new WaitForSeconds(0.35f);

        // Phase 3: Slowly rise from sitting on the mattress edge to standing beside the bed (1.8s)
        float eyeH = fps != null ? fps.eyeHeight : 1.75f;
        Vector3 standEyePos = bedStandFootPos + new Vector3(0f, eyeH, 0f);
        float standDur = 1.8f;
        elapsed = 0f;
        while (elapsed < standDur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / standDur));
            if (cam != null)
            {
                Vector3 pos = Vector3.Lerp(bedEdgeSitPos, standEyePos, u);
                // Slight forward weight shift as standing up
                pos += (bedStandEyeRot * Vector3.forward) * (Mathf.Sin(u * Mathf.PI) * 0.08f);
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.Slerp(bedSitRot, bedStandEyeRot, u);
            }
            yield return null;
        }

        if (fps != null)
        {
            fps.transform.position = bedStandFootPos;
            fps.SyncLookAnglesFromCamera();
        }
        SimpleFPSController.IsExternalCameraAnimationActive = false;

        // --- POST-WAKEUP DIALOGUE ---
        act3Stage = Act3Stage.PostWakeupDialogue;
        propCtrl.PlayDoorKnockSound(buildingRoot);
        yield return StartCoroutine(PlayDialogueRoutine("??: knocks..", 1.2f));

        // [1 seconds cooldown]
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Who's that?", 1.8f));

        // [2 seconds cooldown]
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: It's raining so badly. Who could that be?", 2.4f));

        // 2 seconds cooldown then allow player to walk toward the front door
        yield return new WaitForSeconds(2.0f);
        isMovementLocked = false;
        act3Stage = Act3Stage.WalkingToFrontDoor;

        // Wait until player is detected near the front double door
        while (true)
        {
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Vector3 doorPos = propCtrl.GetFrontDoorWorldPosition(buildingRoot);
                float horizDist = Vector2.Distance(
                    new Vector2(cam.transform.position.x, cam.transform.position.z),
                    new Vector2(doorPos.x, doorPos.z));
                if (horizDist <= 3.8f)
                {
                    break;
                }
            }
            yield return null;
        }

        // --- FRONT DOOR DIALOGUE WITH JUAN ---
        act3Stage = Act3Stage.JuanFrontDoorDialogue;
        isMovementLocked = true;

        propCtrl.PlayDoorKnockSound(buildingRoot);
        yield return StartCoroutine(PlayDialogueRoutine("?? : knocks again..", 1.4f));

        // 2 second cooldown
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("?? : Hello? Is anyone there?", 2.2f));
        yield return StartCoroutine(PlayDialogueRoutine("Juan: I'm Juan. Could you please let me in? It's raining really hard outside!", 3.2f));

        // [2 seconds cooldown]
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Let you in? Sorry, but I don't usually let strangers into my house.", 3.0f));
        yield return StartCoroutine(PlayDialogueRoutine("Juan: Please, I just need somewhere to stay until the rain stops.", 2.8f));
        yield return StartCoroutine(PlayDialogueRoutine("You: I don't know you. How am I supposed to trust you?", 2.6f));
        yield return StartCoroutine(PlayDialogueRoutine("Juan: I understand. I'm not asking for much. Just a little shelter from the rain.", 3.0f));
        yield return StartCoroutine(PlayDialogueRoutine("You: Sorry, but I can't let you in. I hope you find somewhere else to stay.", 3.0f));
        yield return StartCoroutine(PlayDialogueRoutine("Juan: Oh... Alright. Sorry for bothering you.", 2.6f));

        // "for 5 seconds cooldown and allows to walk, then:"
        act3Stage = Act3Stage.PostJuanWalkCooldown;
        isMovementLocked = false;
        yield return new WaitForSeconds(5.0f);

        yield return StartCoroutine(PlayNonBlockingDialogueRoutine("You: That was weird, people don't usually come looking for places to stay especially the neighborhood is far from Route 8.", 3.4f));

        // 2 seconds cooldown
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayNonBlockingDialogueRoutine("You: I gotta wash my face, I don't know if I should go to work or not..", 2.8f));

        // 2 seconds
        yield return new WaitForSeconds(2.0f);

        // --- TASK: WASH YOUR FACE ---
        hasCompletedWashFace = false;
        act3Stage = Act3Stage.TaskWashFace;

        while (!hasCompletedWashFace)
        {
            yield return null;
        }

        // 2 second cooldown after "What just happened?" animation returns to normal state
        yield return new WaitForSeconds(2.0f);

        // --- TASK: TRY TURNING ON AND OFF ANY LIGHT ---
        outageSwitchToggleCount = 0;
        firstOutageSwitchToggleTime = -1f;
        act3Stage = Act3Stage.TaskTryLightSwitch;

        while (true)
        {
            if (outageSwitchToggleCount >= 2)
            {
                break;
            }
            if (outageSwitchToggleCount >= 1 && firstOutageSwitchToggleTime > 0f && (Time.time - firstOutageSwitchToggleTime) >= 2.4f)
            {
                break;
            }
            yield return null;
        }

        // Brief tense pause in the dark after clicking the dead switch
        yield return new WaitForSeconds(1.1f);
        act3Stage = Act3Stage.LightSwitchCompleted;
        yield return new WaitForSeconds(0.6f);

        yield return StartCoroutine(PlayNonBlockingDialogueRoutine("You: Aw men.. a power outage? probably because of this heavy rain..", 2.8f));

        // 2 seconds cooldown
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayNonBlockingDialogueRoutine("You: I guess I'll use candle then.", 2.2f));

        // 2 seconds cooldown
        yield return new WaitForSeconds(2.0f);

        // --- TASK: FIND YOUR CANDLE SOMEWHERE (-- Somewhere in the living room.) ---
        hasPickedUpCandle = false;
        act3Stage = Act3Stage.TaskFindCandle;

        while (!hasPickedUpCandle)
        {
            yield return null;
        }

        act3Stage = Act3Stage.CandleFoundDialogue;
        // 1 second
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayNonBlockingDialogueRoutine("You: Found it, now let me light this up..", 2.2f));

        // 2 seconds
        yield return new WaitForSeconds(2.0f);

        // --- TASK: ALLOWS TO LIGHT IT WITH E TO LIGHT IT UP ---
        hasLitCandle = false;
        act3Stage = Act3Stage.TaskLightCandle;

        while (!hasLitCandle)
        {
            yield return null;
        }

        act3Stage = Act3Stage.CandleLitDialogue;
        yield return StartCoroutine(PlayNonBlockingDialogueRoutine("You: Phew.. this will do.", 2.0f));

        // 2 second cd
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayNonBlockingDialogueRoutine("You: Where's my phone anyway? I need to call my manager about the situation here right now.", 3.0f));

        // 2 seconds
        yield return new WaitForSeconds(2.0f);

        // --- TASK: FIND YOUR PHONE (-- Somewhere in the bedroom) ---
        hasPickedUpPhone = false;
        act3Stage = Act3Stage.TaskFindPhone;

        while (!hasPickedUpPhone)
        {
            yield return null;
        }

        act3Stage = Act3Stage.PhoneFoundDialogue;
        isMovementLocked = true;
        // 1 second dialouge
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Found it..", 1.6f));

        // --- TASK: MAKE A CALL ---
        hasInitiatedCall = false;
        act3Stage = Act3Stage.TaskMakeCall;

        // "1 second (Animation will be played once the phone is picked up on the right, animation like pressing the phone realistically then putting it near its ear animation then dialouge)"
        float waitCallTimer = 0f;
        while (waitCallTimer < 1.0f && !hasInitiatedCall)
        {
            waitCallTimer += Time.deltaTime;
            yield return null;
        }
        hasInitiatedCall = true;
        act3Stage = Act3Stage.ManagerCallInProgress;
        isMovementLocked = true;

        CabinPhoneInteractable heldPhone = CabinPhoneInteractable.HeldPhone;
        if (heldPhone != null)
        {
            yield return StartCoroutine(heldPhone.PlayDialAndRaiseToEarRoutine());
        }

        // "Ringing dialouge / 3 seconds of ringing"
        if (heldPhone != null) heldPhone.PlayRingbackToneOneShot();
        skipCurrentDialogueRequested = false;
        isDialogueVisible = true;
        dialogueAlpha = 1f;
        isDialogueTyping = false;
        displayedDialogueText = "Ringing...";
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(1.5f, () => skipCurrentDialogueRequested));
        if (!skipCurrentDialogueRequested)
        {
            if (heldPhone != null) heldPhone.PlayRingbackToneOneShot();
            yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(1.5f, () => skipCurrentDialogueRequested));
        }
        yield return StartCoroutine(FadeOutDialogueRoutine(0.25f));

        // "Then 1 seconds:"
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("Manager: Hello?", 1.6f));
        yield return StartCoroutine(PlayDialogueRoutine("You: Sir? I don't know if I can still go to work right now..", 2.6f));

        // 1.5 seconds
        yield return new WaitForSeconds(1.5f);

        yield return StartCoroutine(PlayDialogueRoutine("You: It's really raining in here.", 2.0f));

        // 1.9 seconds
        yield return new WaitForSeconds(1.9f);

        yield return StartCoroutine(PlayDialogueRoutine("Manager: Ahh..", 1.4f));

        // 1 seconds
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("Manager: I'm sorry son, but you really have to work with your nightshift.", 2.8f));

        // 2 seconds:
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("Manager: As of right now, Billy is also not available to take over for the rest of the night.", 3.0f));

        // 2 seconds
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("Manager: But I'll make you better, let's 2x your salary bonus for this day.", 2.8f));

        // 1 second
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("Manager: Deal?", 1.5f));

        // 2 seconds
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Oh.. a salary bonus..?", 2.0f));

        // 1 seconds:
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Alright, deal.", 1.6f));

        // 2 seconds
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: I badly need the money anyways..", 2.2f));

        // 1 seconds
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("Manager: Alright, keep safe and I hope you can find a way to get there as soon as possible.", 3.0f));
        yield return StartCoroutine(PlayDialogueRoutine("You: Alright..", 1.6f));

        // "ends the call and the phone animation will slowly holding to normal state animation"
        if (heldPhone != null)
        {
            yield return StartCoroutine(heldPhone.PlayEndCallAndLowerToNormalRoutine());
        }

        act3Stage = Act3Stage.PostCallDialogue;
        isMovementLocked = true;

        // 1 second
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: I guess I'll be working.", 2.0f));

        // Keep the player frozen right after "I guess I'll be working." as the window shatters
        yield return new WaitForSeconds(0.45f);

        // --- SUDDEN BREAKING GLASS NOISE & MONSTER OUTSIDE LEFT LIVING ROOM WINDOW ---
        propCtrl.TriggerLivingRoomWindowBreak(buildingRoot);

        yield return StartCoroutine(PlayDialogueRoutine("You: Huh?", 1.4f));

        // 1 second
        yield return new WaitForSeconds(1.0f);

        hasPlayerPressedInspectWindow = false;
        hasInvestigatedBrokenWindow = false;
        act3Stage = Act3Stage.TaskFindNoise;
        isMovementLocked = false;

        Vector3 windowCenter;
        Vector3 outwardDir;
        float cabinFloorY;
        propCtrl.GetLeftLivingRoomWindowFrame(buildingRoot, out windowCenter, out outwardDir, out cabinFloorY);

        // Wait until the player walks over and presses [E] to inspect the broken window
        while (!hasPlayerPressedInspectWindow)
        {
            yield return null;
        }

        // --- INSPECT WINDOW -> STARE INTO DARKNESS -> THUNDER LIGHT REVEAL -> DISAPPEAR ---
        act3Stage = Act3Stage.LookingAtMonsterAndBlink;
        isMovementLocked = true;
        SimpleFPSController.IsExternalCameraAnimationActive = true;
        if (cam == null) cam = Camera.main;

        // Ensure monster starts hidden in the dark until lightning flashes
        propCtrl.SetMonsterVisible(false);

        Vector3 monsterLookPoint = propCtrl.monsterHeadLookTarget != null
            ? propCtrl.monsterHeadLookTarget.position
            : (windowCenter + outwardDir * 8.5f + Vector3.up * 0.05f);
        Quaternion targetMonsterRot = cam != null ? cam.transform.rotation : Quaternion.identity;

        if (cam != null)
        {
            Quaternion startLookRot = cam.transform.rotation;
            Vector3 toMonster = (monsterLookPoint - cam.transform.position).normalized;
            targetMonsterRot = toMonster.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toMonster, Vector3.up)
                : startLookRot;

            float turnDur = 0.82f;
            elapsed = 0f;
            while (elapsed < turnDur)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / turnDur));
                cam.transform.rotation = Quaternion.Slerp(startLookRot, targetMonsterRot, u);
                yield return null;
            }
            cam.transform.rotation = targetMonsterRot;
        }

        // Brief tense beat gazing out into the dark rain before lightning strikes
        yield return new WaitForSeconds(0.28f);

        // Reveal monster ONLY during the thunder lightning flash, then vanish as darkness returns
        if (propCtrl.rainStormController != null)
        {
            yield return propCtrl.rainStormController.TriggerMonsterLightningReveal(propCtrl);
        }
        else
        {
            propCtrl.SetMonsterVisible(true);
            yield return new WaitForSeconds(0.75f);
            propCtrl.SetMonsterVisible(false);
        }

        hasInvestigatedBrokenWindow = true;

        // Brief startled double-blink in the darkness right after the lightning vanishes
        yield return new WaitForSeconds(0.14f);
        float closeDur = 0.16f;
        elapsed = 0f;
        while (elapsed < closeDur)
        {
            elapsed += Time.deltaTime;
            eyelidCloseProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / closeDur));
            yield return null;
        }
        eyelidCloseProgress = 1.0f;
        yield return new WaitForSeconds(0.08f);

        float openDur = 0.22f;
        elapsed = 0f;
        while (elapsed < openDur)
        {
            elapsed += Time.deltaTime;
            eyelidCloseProgress = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(elapsed / openDur));
            yield return null;
        }
        eyelidCloseProgress = 0.0f;

        // Post-blink dialogue sequence
        yield return StartCoroutine(PlayDialogueRoutine("You: Huh..? am I seeing things or no..?", 2.4f));

        // 2 seconds
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: I'm probably just tired or something?", 2.4f));

        // 2 seconds
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: But I saw it with my own eyes..", 2.2f));

        // 1 second
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: What even was that?", 2.0f));

        // 1 seconds.
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: And there's no way the window just randomly breaks?", 2.4f));

        // 2 seconds
        yield return new WaitForSeconds(2.0f);

        // --- ANIMATE LOOKING AT THE BOTTOM AND SEE A DEAD PIGEON ---
        act3Stage = Act3Stage.LookingAtDeadPigeon;
        Vector3 pigeonWorldPos = propCtrl.GetDeadPigeonWorldPosition(buildingRoot);
        if (cam != null)
        {
            Quaternion startRot = cam.transform.rotation;
            Vector3 toPigeon = (pigeonWorldPos - cam.transform.position).normalized;
            Quaternion targetPigeonRot = toPigeon.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toPigeon, Vector3.up)
                : startRot;

            float lookDownDur = 0.90f;
            elapsed = 0f;
            while (elapsed < lookDownDur)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / lookDownDur));
                cam.transform.rotation = Quaternion.Slerp(startRot, targetPigeonRot, u);
                yield return null;
            }
            cam.transform.rotation = targetPigeonRot;
        }

        yield return new WaitForSeconds(0.45f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Maybe I'm hallucinating things.. or maybe I'm just tired.", 2.2f));

        // 1 second:
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Poor pigeon..", 2.2f));

        // 2 seconds:
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: I guess this concludes everything?", 2.2f));

        // 2 second
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Another headache to fix.. but anyways I'll fix this tomorrow and I'll get ready for my nightshift..", 3.2f));

        // --- SLOWLY FADES OUT -> BLACK SCREEN -> AT WORK.. / AGAIN.. / RAINING.. -> ACT 4 ---
        act3Stage = Act3Stage.FadingOutToAct4;
        isIntroActive = true;
        textAlpha = 0f;
        displayedText = "";

        float fadeOutDur = 2.2f;
        elapsed = 0f;
        while (elapsed < fadeOutDur)
        {
            elapsed += Time.deltaTime;
            blackOverlayAlpha = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(elapsed / fadeOutDur));
            yield return null;
        }
        blackOverlayAlpha = 1.0f;

        yield return new WaitForSeconds(0.5f);

        // "At work.." -> 2 sec
        yield return StartCoroutine(TypeTextRoutine(" At work.. "));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(2.0f, () => skipCurrentDialogueRequested));
        yield return StartCoroutine(FadeOutTextRoutine(0.4f));
        yield return new WaitForSeconds(0.15f);

        // "Again.." -> 2 sec
        yield return StartCoroutine(TypeTextRoutine(" Again.. "));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(2.0f, () => skipCurrentDialogueRequested));
        yield return StartCoroutine(FadeOutTextRoutine(0.4f));
        yield return new WaitForSeconds(0.15f);

        // "Raining.." -> 2 sec
        yield return StartCoroutine(TypeTextRoutine(" Raining.. "));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(2.0f, () => skipCurrentDialogueRequested));
        yield return StartCoroutine(FadeOutTextRoutine(0.45f));

        SimpleFPSController.IsExternalCameraAnimationActive = false;
        isMovementLocked = false;
        isIntroActive = false;

        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.currentStoryAct = 4;
            PlayerManager.Instance.TransitionToScene("GasScene2", "Default");
        }
        else
        {
            SceneManager.LoadScene("GasScene2", LoadSceneMode.Single);
        }
    }

    private IEnumerator Act3WashFaceAndOutageRoutine(
        Transform sinkTransform,
        CabinKitchenSinkStation kitchenSink,
        CabinBathroomSinkFaceWash bathroomSink)
    {
        isWashingFaceRoutineRunning = true;
        act3Stage = Act3Stage.WashingFaceAndOutage;
        isMovementLocked = true;
        SimpleFPSController.IsExternalCameraAnimationActive = true;

        SimpleFPSController fps = Object.FindAnyObjectByType<SimpleFPSController>();
        Camera cam = fps != null && fps.playerCamera != null ? fps.playerCamera : Camera.main;

        Vector3 baseCamLocalPos = cam != null ? cam.transform.localPosition : new Vector3(0f, 1.75f, 0f);
        Quaternion baseCamWorldRot = cam != null ? cam.transform.rotation : Quaternion.identity;

        // Resolve exact sink basin look target
        Vector3 basinTarget;
        if (bathroomSink != null)
        {
            basinTarget = bathroomSink.GetBasinWorldPosition();
        }
        else if (sinkTransform != null)
        {
            basinTarget = sinkTransform.position + Vector3.up * 0.86f;
        }
        else
        {
            basinTarget = cam != null ? cam.transform.position + cam.transform.forward * 0.7f - Vector3.up * 0.45f : Vector3.zero;
        }

        Quaternion flatBaseRot = baseCamWorldRot;
        Quaternion basinLookRot = baseCamWorldRot;
        if (cam != null)
        {
            Vector3 toBasin = basinTarget - cam.transform.position;
            Vector3 flatToBasin = new Vector3(toBasin.x, 0f, toBasin.z);
            if (flatToBasin.sqrMagnitude > 0.0004f)
            {
                flatBaseRot = Quaternion.LookRotation(flatToBasin.normalized, Vector3.up);
            }
            // Calm, natural ~26 degree downward gaze toward the basin
            basinLookRot = flatBaseRot * Quaternion.Euler(26f, 0f, 0f);
        }

        Vector3 leanLocalPos = baseCamLocalPos + new Vector3(0f, -0.14f, 0f);
        Vector3 dipLocalPos = baseCamLocalPos + new Vector3(0f, -0.22f, 0f);
        Quaternion dipLookRot = flatBaseRot * Quaternion.Euler(35f, 0f, 0f);

        // ---------------------------------------------------------------------
        // PHASE 1 (1.10s): Turn on faucet & calmly lean over the basin
        // ---------------------------------------------------------------------
        if (bathroomSink != null) bathroomSink.PlayFaucetTurnOneShot();
        float elapsed = 0f;
        float leanDur = 1.10f;
        bool waterStarted = false;
        while (elapsed < leanDur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / leanDur));

            if (!waterStarted && elapsed >= 0.35f)
            {
                waterStarted = true;
                if (bathroomSink != null) bathroomSink.SetWaterRunning(true);
                if (kitchenSink != null) kitchenSink.SetWaterRunning(true);
            }

            if (cam != null)
            {
                cam.transform.localPosition = Vector3.Lerp(baseCamLocalPos, leanLocalPos, u);
                cam.transform.rotation = Quaternion.Slerp(baseCamWorldRot, basinLookRot, u);
            }
            yield return null;
        }

        // ---------------------------------------------------------------------
        // PHASE 2 (0.65s): Calm pause watching the running water & cupping water
        // ---------------------------------------------------------------------
        if (bathroomSink != null) bathroomSink.PlayScoopWaterOneShot();
        yield return new WaitForSeconds(0.65f);

        // ---------------------------------------------------------------------
        // PHASE 3: Wash face -> close eyes ENTIRELY, then open eyes timed right as the lights turn off
        // ---------------------------------------------------------------------
        if (bathroomSink != null) bathroomSink.PlayFaceSplashOneShot();

        // 3a. Dip down & close eyes ENTIRELY (1.0f = full black)
        elapsed = 0f;
        float closeDur = 0.55f;
        while (elapsed < closeDur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / closeDur));
            if (cam != null)
            {
                cam.transform.localPosition = Vector3.Lerp(leanLocalPos, dipLocalPos, u);
                cam.transform.rotation = Quaternion.Slerp(basinLookRot, dipLookRot, u);
            }
            eyelidCloseProgress = u;
            yield return null;
        }
        eyelidCloseProgress = 1.0f;

        // 3b. Hold eyes completely closed while washing face (0.65s)
        yield return new WaitForSeconds(0.65f);

        // 3c. Open eyes timed directly with the lights turning off
        bool outageTriggeredDuringOpen = false;
        elapsed = 0f;
        float openDur = 0.58f;
        while (elapsed < openDur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / openDur));

            if (!outageTriggeredDuringOpen && u >= 0.18f)
            {
                outageTriggeredDuringOpen = true;
                if (bathroomSink != null) bathroomSink.SetWaterRunning(false);
                if (kitchenSink != null) kitchenSink.SetWaterRunning(false);

                isPowerOutageActive = true;
                CabinLightSwitch.ForceAllCabinLightsOffForOutage();
                if (HouseAct3PropController.Instance != null && cam != null)
                {
                    HouseAct3PropController.Instance.PlayPowerOutageSound(cam.transform.position);
                }
            }

            eyelidCloseProgress = 1.0f - u;
            yield return null;
        }
        eyelidCloseProgress = 0.0f;

        // Calmly straighten up and look slightly up toward the dark ceiling light
        if (cam != null)
        {
            Vector3 startLocalPos = cam.transform.localPosition;
            Quaternion startRot = cam.transform.rotation;

            Quaternion lookUpCalmRot = flatBaseRot * Quaternion.Euler(-22f, 0f, 0f);
            Quaternion lookSideCalmRot = flatBaseRot * Quaternion.Euler(-18f, 14f, 0f);

            // 1. Smoothly rise to standing height & look up at dead light (1.05s)
            float raiseDur = 1.05f;
            elapsed = 0f;
            while (elapsed < raiseDur)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / raiseDur));
                cam.transform.localPosition = Vector3.Lerp(startLocalPos, baseCamLocalPos, u);
                cam.transform.rotation = Quaternion.Slerp(startRot, lookUpCalmRot, u);
                yield return null;
            }

            yield return new WaitForSeconds(0.35f);

            // 2. Subtle, calm glance toward the dark doorway/ceiling (0.80s)
            float glanceDur = 0.80f;
            elapsed = 0f;
            while (elapsed < glanceDur)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / glanceDur));
                cam.transform.rotation = Quaternion.Slerp(lookUpCalmRot, lookSideCalmRot, u);
                yield return null;
            }

            // "then You: " What just happened? "( slowly animation realistically return to normal state)"
            Quaternion returnStartRot = cam.transform.rotation;
            Coroutine returnPoseCo = StartCoroutine(SlowlyReturnCameraToNormalRoutine(cam, returnStartRot, flatBaseRot, baseCamLocalPos, 1.9f));
            yield return StartCoroutine(PlayDialogueRoutine("You: What just happened?", 2.0f));
            if (returnPoseCo != null) yield return returnPoseCo;
        }
        else
        {
            yield return StartCoroutine(PlayDialogueRoutine("You: What just happened?", 2.0f));
        }

        if (fps != null)
        {
            fps.SyncLookAnglesFromCamera();
        }

        SimpleFPSController.IsExternalCameraAnimationActive = false;
        isMovementLocked = false;
        hasCompletedWashFace = true;
        isWashingFaceRoutineRunning = false;
    }

    private static void SetAllCabinLightsTemporarilyDimmed(bool on)
    {
        CabinLightSwitch[] switches = Object.FindObjectsByType<CabinLightSwitch>(FindObjectsInactive.Include);
        for (int i = 0; i < switches.Length; i++)
        {
            if (switches[i] == null || switches[i].controlledLights == null) continue;
            for (int j = 0; j < switches[i].controlledLights.Count; j++)
            {
                Light l = switches[i].controlledLights[j];
                if (l != null) l.enabled = on;
            }
        }
    }

    private IEnumerator SlowlyReturnCameraToNormalRoutine(
        Camera cam,
        Quaternion fromRot,
        Quaternion toRot,
        Vector3 targetLocalPos,
        float duration)
    {
        if (cam == null) yield break;
        Vector3 fromLocalPos = cam.transform.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            cam.transform.localPosition = Vector3.Lerp(fromLocalPos, targetLocalPos, u);
            cam.transform.rotation = Quaternion.Slerp(fromRot, toRot, u);
            yield return null;
        }
        cam.transform.localPosition = targetLocalPos;
        cam.transform.rotation = toRot;
    }

    private IEnumerator PlayNonBlockingDialogueRoutine(string fullLine, float holdTime = 1.8f)
    {
        isMovementLocked = false;
        yield return StartCoroutine(PlayDialogueRoutine(fullLine, holdTime));
    }

    private IEnumerator FadeOutDialogueRoutine(float duration)
    {
        float elapsed = 0f;
        while (!skipCurrentDialogueRequested && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            dialogueAlpha = Mathf.Clamp01(1.0f - (elapsed / Mathf.Max(0.01f, duration)));
            yield return null;
        }
        dialogueAlpha = 0f;
        displayedDialogueText = "";
        isDialogueVisible = false;
        skipCurrentDialogueRequested = false;
    }

    // =========================================================================
    // ACT 5: HOUSE SCENE (FINALE - NIGHTMARE AWAKENING, MONSTER JUMPSCARE & ENDING)
    // =========================================================================

    private CabinDoorInteractable FindAndCloseBedroomDoorImmediate(Transform buildingRoot, Vector3 bedStandFootPos)
    {
        CabinDoorInteractable[] allDoors = Object.FindObjectsByType<CabinDoorInteractable>(FindObjectsInactive.Include);
        CabinDoorInteractable bestBedroomDoor = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < allDoors.Length; i++)
        {
            CabinDoorInteractable d = allDoors[i];
            if (d == null || !d.IsRoomDoor() || d.IsFrontEntranceDoubleDoor()) continue;

            float dist = Vector3.Distance(d.transform.position, bedStandFootPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestBedroomDoor = d;
            }
        }

        if (bestBedroomDoor != null)
        {
            bestBedroomDoor.ForceClosedImmediate();
        }
        return bestBedroomDoor;
    }

    private void PlayAct5WakeupGaspAndHeartbeat()
    {
        if (audioSource == null) return;
        int sr = 44100;
        int len = Mathf.RoundToInt(0.65f * sr);
        float[] buf = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sr;
            // Sharp intake gasp + double heartbeat thump
            float gaspEnv = Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI) * Mathf.Exp(-t * 4.5f);
            float breath = (Random.value * 2f - 1f) * 0.35f * gaspEnv;
            float hb1 = (t < 0.18f) ? Mathf.Sin(2f * Mathf.PI * 62f * t) * Mathf.Exp(-t * 22f) * 0.85f : 0f;
            float dt2 = t - 0.22f;
            float hb2 = (dt2 >= 0f && dt2 < 0.20f) ? Mathf.Sin(2f * Mathf.PI * 50f * dt2) * Mathf.Exp(-dt2 * 26f) * 0.68f : 0f;
            buf[i] = Mathf.Clamp(breath + hb1 + hb2, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("Act5_WakeupGasp", len, 1, sr, false);
        clip.SetData(buf, 0);
        audioSource.pitch = 1.0f;
        audioSource.PlayOneShot(clip, 0.90f);
    }

    private IEnumerator Act5StorySequenceRoutine()
    {
        isAct5Active = true;
        isAct3Active = false;
        act5ShowWashFaceTask = false;
        isIntroActive = true;
        isMovementLocked = true;
        isSleepingSequenceActive = false;
        SimpleFPSController.IsExternalCameraAnimationActive = true;
        SimpleFPSController.ExternalMovementSpeedMultiplier = 1.0f;

        blackOverlayAlpha = 1.0f;
        eyelidCloseProgress = 1.0f;
        textAlpha = 0f;
        displayedText = "";

        Transform buildingRoot = FindCabinBuildingRoot();

        // 1. Lock & close front double door and ensure all cabin lights are ON
        LockAndCloseFrontDoorForAct3();
        EnsureAllCabinLightsOnBeforeOutage();

        HouseAct3PropController propCtrl = HouseAct3PropController.EnsureExists(buildingRoot);
        propCtrl.SetupAct3PropsAndWeather(buildingRoot);
        propCtrl.SetMonsterVisible(false);

        // 2. Place player lying in PFB_Bed and ensure Bedroom Door is CLOSED
        SimpleFPSController fps = Object.FindAnyObjectByType<SimpleFPSController>();
        CharacterController cc = fps != null ? fps.GetComponent<CharacterController>() : null;
        Camera cam = fps != null && fps.playerCamera != null ? fps.playerCamera : Camera.main;
        CabinBedInteractable bed = Object.FindAnyObjectByType<CabinBedInteractable>();

        if (cc != null) cc.enabled = false;

        Vector3 bedHeadWorld = Vector3.zero;
        Quaternion bedLieRot = Quaternion.identity;
        Vector3 bedEdgeSitPos = Vector3.zero;
        Quaternion bedSitRot = Quaternion.identity;
        Vector3 bedStandFootPos = Vector3.zero;
        Quaternion bedStandEyeRot = Quaternion.identity;

        if (bed != null)
        {
            bedHeadWorld = bed.transform.TransformPoint(new Vector3(0.0f, 0.80f, -0.78f));
            Vector3 footDir = bed.transform.TransformDirection(new Vector3(-0.12f, 0.30f, 0.94f)).normalized;
            bedLieRot = Quaternion.LookRotation(footDir, Vector3.up) * Quaternion.Euler(0f, 0f, 7.5f);

            bedEdgeSitPos = bed.transform.TransformPoint(new Vector3(-0.58f, 1.18f, -0.25f));
            Vector3 sitLookDir = bed.transform.TransformDirection(new Vector3(-1f, -0.05f, 0.15f)).normalized;
            bedSitRot = Quaternion.LookRotation(sitLookDir, Vector3.up);

            bedStandFootPos = bed.transform.TransformPoint(new Vector3(-1.08f, 0.02f, -0.20f));
            Vector3 standLookDir = bed.transform.TransformDirection(new Vector3(-0.92f, 0.0f, 0.38f)).normalized;
            bedStandEyeRot = Quaternion.LookRotation(standLookDir, Vector3.up);
        }
        else if (buildingRoot != null)
        {
            bedHeadWorld = buildingRoot.TransformPoint(new Vector3(2.843f, 0.92f, -8.95f));
            bedLieRot = Quaternion.Euler(-15f, 0f, 7.5f);
            bedEdgeSitPos = buildingRoot.TransformPoint(new Vector3(2.26f, 1.30f, -8.40f));
            bedSitRot = Quaternion.Euler(4f, -90f, 0f);
            bedStandFootPos = buildingRoot.TransformPoint(new Vector3(1.76f, 0.12f, -8.35f));
            bedStandEyeRot = Quaternion.Euler(0f, -70f, 0f);
        }

        if (fps != null && cam != null)
        {
            fps.transform.position = bedStandFootPos;
            cam.transform.position = bedHeadWorld;
            cam.transform.rotation = bedLieRot;
        }

        // Ensure the bedroom door is closed!
        CabinDoorInteractable bedroomDoor = FindAndCloseBedroomDoorImmediate(buildingRoot, bedStandFootPos);

        // 3. Black screen with centered middle text: "Sumne re vera mortuus?" (2s) -> "Reality?" (2s)
        yield return new WaitForSeconds(0.8f);

        yield return StartCoroutine(TypeTextRoutine("Sumne re vera mortuus?"));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(2.0f, () => skipCurrentDialogueRequested));
        yield return StartCoroutine(FadeOutTextRoutine(0.45f));
        yield return new WaitForSeconds(0.25f);

        yield return StartCoroutine(TypeTextRoutine("Reality?"));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(2.0f, () => skipCurrentDialogueRequested));
        yield return StartCoroutine(FadeOutTextRoutine(0.45f));
        yield return new WaitForSeconds(0.25f);

        // 4. Sudden nightmare wake-up animation from bed ("animation of a sudden get up because of the dream")
        isIntroActive = false;
        PlayAct5WakeupGaspAndHeartbeat();

        // Phase 1 (0.48s): Snap eyes open & jolt upright from pillow to sitting on the edge of the bed
        float joltSitDur = 0.48f;
        float elapsed = 0f;
        while (elapsed < joltSitDur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / joltSitDur);
            // Rapidly snap black overlay & eyelids open
            blackOverlayAlpha = Mathf.Clamp01(1.0f - u * 3.2f);
            eyelidCloseProgress = Mathf.Clamp01(1.0f - u * 2.8f);

            // Fast spring-like sit-up with slight startled overshoot
            float fastT = 1f - Mathf.Pow(1f - u, 3f);
            if (cam != null)
            {
                Vector3 pos = Vector3.Lerp(bedHeadWorld, bedEdgeSitPos, fastT);
                pos.y += Mathf.Sin(u * Mathf.PI) * 0.08f;
                cam.transform.position = pos;

                Quaternion joltRot = Quaternion.Slerp(bedLieRot, bedSitRot, fastT) *
                                     Quaternion.Euler(Mathf.Sin(u * Mathf.PI * 2f) * -5.5f, 0f, Mathf.Sin(u * Mathf.PI * 3f) * 3.2f);
                cam.transform.rotation = joltRot;
            }
            yield return null;
        }
        blackOverlayAlpha = 0f;
        eyelidCloseProgress = 0f;

        // Phase 2 (0.45s): Panicked catch-breath on edge of bed
        elapsed = 0f;
        while (elapsed < 0.45f)
        {
            elapsed += Time.deltaTime;
            if (cam != null)
            {
                float breathBob = Mathf.Sin(elapsed * 18f) * 0.014f;
                cam.transform.position = bedEdgeSitPos + new Vector3(0f, breathBob, 0f);
                cam.transform.rotation = bedSitRot * Quaternion.Euler(breathBob * 85f, 0f, 0f);
            }
            yield return null;
        }

        // Phase 3 (0.85s): Quickly rise from bed edge to standing beside the bed
        float eyeH = fps != null ? fps.eyeHeight : 1.75f;
        Vector3 standEyePos = bedStandFootPos + new Vector3(0f, eyeH, 0f);
        float standDur = 0.85f;
        elapsed = 0f;
        while (elapsed < standDur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / standDur));
            if (cam != null)
            {
                Vector3 pos = Vector3.Lerp(bedEdgeSitPos, standEyePos, u);
                pos += (bedStandEyeRot * Vector3.forward) * (Mathf.Sin(u * Mathf.PI) * 0.07f);
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.Slerp(bedSitRot, bedStandEyeRot, u);
            }
            yield return null;
        }

        if (fps != null)
        {
            fps.transform.position = bedStandFootPos;
            fps.SyncLookAnglesFromCamera();
        }
        SimpleFPSController.IsExternalCameraAnimationActive = false;

        // 5. Show Task ("Task: Wash your face / - Severe your head.") & Play Bedroom Dialogue
        act5ShowWashFaceTask = true;
        isMovementLocked = true;

        yield return StartCoroutine(PlayDialogueRoutine("You: What's going on..?  I thought I'm working..", 2.2f));
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: What is all just a nightmare..?", 2.0f));
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: I gotta wash my face..", 1.8f));

        // Allow player to walk to the closed bedroom door and open it
        isMovementLocked = false;
        SimpleFPSController.ExternalMovementSpeedMultiplier = 1.0f;

        Vector3 doorWorldPos = bedroomDoor != null ? bedroomDoor.transform.position : (bedStandFootPos + new Vector3(-1.8f, 0f, 2.2f));

        // 6. Wait until the player opens the bedroom door and steps outside into the hallway/living room
        while (true)
        {
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                bool doorOpened = bedroomDoor == null || bedroomDoor.isOpen;
                Vector3 p = cam.transform.position;
                float distFromBed = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(bedStandFootPos.x, bedStandFootPos.z));

                bool steppedOutsideBedroom = false;
                if (buildingRoot != null)
                {
                    Vector3 lp = buildingRoot.InverseTransformPoint(p);
                    // Bedroom interior is localZ < -5.85; stepping past the bedroom door puts localZ > -5.55
                    if (lp.z > -5.55f && distFromBed > 2.1f)
                    {
                        steppedOutsideBedroom = true;
                    }
                }
                else if (distFromBed > 2.65f && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(doorWorldPos.x, doorWorldPos.z)) > 0.45f)
                {
                    steppedOutsideBedroom = true;
                }

                if (doorOpened && steppedOutsideBedroom)
                {
                    break;
                }
            }
            yield return null;
        }

        // 7. Once outside bedroom: very slow pace until player cannot move at all
        float slowDownDuration = 2.4f;
        elapsed = 0f;
        float nextHb = 0f;
        while (elapsed < slowDownDuration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / slowDownDuration);
            SimpleFPSController.ExternalMovementSpeedMultiplier = Mathf.Lerp(0.38f, 0.0f, u);

            if (elapsed >= nextHb)
            {
                nextHb = elapsed + 0.65f;
                PlayAct5WakeupGaspAndHeartbeat();
            }
            yield return null;
        }

        SimpleFPSController.ExternalMovementSpeedMultiplier = 0.0f;
        isMovementLocked = true;

        // 8. Lights flicker abnormally again -> 2 seconds -> "You: What is going on..?!?!?"
        Coroutine abnormalFlickerCo = StartCoroutine(FlickerCabinLightsAbnormallyRoutine(propCtrl));
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: What is going on..?!?!?", 1.8f));

        // 9. MonsterNPC appears in front alongside the flickering lights and JUMPSCARES DRAMATICALLY -> BLACK SCREEN
        yield return StartCoroutine(PlayAct5DramaticMonsterJumpscareRoutine(propCtrl, buildingRoot));

        if (abnormalFlickerCo != null) StopCoroutine(abnormalFlickerCo);
        CabinLightSwitch.ForceAllCabinLightsOffForOutage();

        // Cut immediately to solid black screen & silence background storm audio
        act5ShowWashFaceTask = false;
        isIntroActive = true;
        blackOverlayAlpha = 1.0f;
        eyelidCloseProgress = 1.0f;
        textAlpha = 0f;
        displayedText = "";

        if (propCtrl != null && propCtrl.rainStormController != null)
        {
            AudioSource[] stormSources = propCtrl.rainStormController.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < stormSources.Length; i++)
            {
                if (stormSources[i] != null) stormSources[i].Stop();
            }
        }

        // 10. Final Black Screen Centered Text Sequence (middle of the screen, not bottom dialogue box)
        isDialogueVisible = false;
        dialogueAlpha = 0f;
        displayedDialogueText = "";
        yield return new WaitForSeconds(1.2f);

        yield return StartCoroutine(TypeTextRoutine("You."));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(3.0f, () => skipCurrentDialogueRequested));
        yield return StartCoroutine(FadeOutTextRoutine(0.45f));
        yield return new WaitForSeconds(0.25f);

        yield return StartCoroutine(TypeTextRoutine("Are."));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(3.0f, () => skipCurrentDialogueRequested));
        yield return StartCoroutine(FadeOutTextRoutine(0.45f));
        yield return new WaitForSeconds(0.25f);

        yield return StartCoroutine(TypeTextRoutine("Dead."));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(3.0f, () => skipCurrentDialogueRequested));
        yield return StartCoroutine(FadeOutTextRoutine(0.45f));
        yield return new WaitForSeconds(0.25f);

        yield return StartCoroutine(TypeTextRoutine("You got into a car accident."));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(2.0f, () => skipCurrentDialogueRequested));
        yield return StartCoroutine(FadeOutTextRoutine(0.45f));
        yield return new WaitForSeconds(0.25f);

        yield return StartCoroutine(TypeTextRoutine("Near Jojo's station."));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(3.0f, () => skipCurrentDialogueRequested));
        yield return StartCoroutine(FadeOutTextRoutine(0.45f));
        yield return new WaitForSeconds(0.25f);

        yield return StartCoroutine(TypeTextRoutine("Mortuus es... id non animadvertisti."));
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(2.0f, () => skipCurrentDialogueRequested));
        yield return StartCoroutine(FadeOutTextRoutine(0.65f));
        yield return new WaitForSeconds(1.2f);

        // Return cleanly to the Main Menu after the Act 5 finale finishes
        HorrorMainMenuController.ShowMainMenu();
    }

    private IEnumerator FlickerCabinLightsAbnormallyRoutine(HouseAct3PropController propCtrl)
    {
        CabinLightSwitch[] switches = Object.FindObjectsByType<CabinLightSwitch>(FindObjectsInactive.Include);
        List<Light> cabinLights = new List<Light>();
        List<float> baseIntensities = new List<float>();

        for (int i = 0; i < switches.Length; i++)
        {
            if (switches[i] == null || switches[i].controlledLights == null) continue;
            for (int j = 0; j < switches[i].controlledLights.Count; j++)
            {
                Light l = switches[i].controlledLights[j];
                if (l != null && !cabinLights.Contains(l))
                {
                    cabinLights.Add(l);
                    baseIntensities.Add(Mathf.Max(0.4f, l.intensity));
                }
            }
        }

        float timer = 0f;
        float nextStep = 0f;
        while (true)
        {
            timer += Time.deltaTime;
            if (timer >= nextStep)
            {
                nextStep = timer + Random.Range(0.035f, 0.11f);
                bool on = Random.value > 0.42f;
                float mult = on ? Random.Range(0.35f, 1.85f) : 0f;

                for (int i = 0; i < cabinLights.Count; i++)
                {
                    if (cabinLights[i] != null)
                    {
                        cabinLights[i].enabled = on;
                        cabinLights[i].intensity = baseIntensities[i] * mult;
                    }
                }

                if (!on && audioSource != null && typeClickClip != null && Random.value < 0.6f)
                {
                    audioSource.pitch = Random.Range(0.50f, 0.80f);
                    audioSource.PlayOneShot(typeClickClip, 0.28f);
                }
            }
            yield return null;
        }
    }

    private IEnumerator PlayAct5DramaticMonsterJumpscareRoutine(HouseAct3PropController propCtrl, Transform buildingRoot)
    {
        SimpleFPSController fps = Object.FindAnyObjectByType<SimpleFPSController>();
        Camera cam = fps != null && fps.playerCamera != null ? fps.playerCamera : Camera.main;
        SimpleFPSController.IsExternalCameraAnimationActive = true;

        if (propCtrl != null)
        {
            propCtrl.EnsureMonsterNormalizedAndHidden(buildingRoot);
        }

        GameObject monster = propCtrl != null ? propCtrl.monsterInstance : null;
        if (monster == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Act3_Monster_LowQual");
            if (prefab != null) monster = Instantiate(prefab);
        }

        AudioClip screamClip = Act4StockroomHorrorSequence.BuildDramaticJumpscareScreamClip(44100);
        if (audioSource != null && screamClip != null)
        {
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(screamClip, 1.0f);
        }

        Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
        Vector3 baseCamLocalPos = cam != null ? cam.transform.localPosition : new Vector3(0f, 1.75f, 0f);
        float baseFov = cam != null ? cam.fieldOfView : 60f;
        float floorY = camPos.y - (fps != null ? fps.eyeHeight : 1.75f);

        Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 lungeStartFloor = new Vector3(camPos.x, floorY, camPos.z) + fwd * 1.75f;
        Vector3 lungeEndFloor = new Vector3(camPos.x, floorY, camPos.z) + fwd * 0.24f;

        Transform pelvisBone = null;
        Transform neckBone = null;
        Transform headBone = null;
        Transform leftArmBone = null;
        Transform rightArmBone = null;
        Quaternion pelvisBaseRot = Quaternion.identity;
        Quaternion neckBaseRot = Quaternion.identity;
        Quaternion headBaseRot = Quaternion.identity;
        Quaternion leftArmBaseRot = Quaternion.identity;
        Quaternion rightArmBaseRot = Quaternion.identity;
        Renderer[] monsterRends = null;

        if (monster != null)
        {
            Act3MonsterStandingAnimator idleAnim = monster.GetComponent<Act3MonsterStandingAnimator>();
            if (idleAnim != null) idleAnim.enabled = false;

            monsterRends = monster.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < monsterRends.Length; i++)
            {
                if (monsterRends[i] != null) monsterRends[i].enabled = true;
            }

            Transform[] children = monster.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform t = children[i];
                if (t == null) continue;
                string n = t.name;
                if (n == "Bone") { pelvisBone = t; pelvisBaseRot = t.localRotation; }
                else if (n == "Bone.007") { neckBone = t; neckBaseRot = t.localRotation; }
                else if (n == "Bone.008") { headBone = t; headBaseRot = t.localRotation; }
                else if (n == "Bone.001") { leftArmBone = t; leftArmBaseRot = t.localRotation; }
                else if (n == "Bone.004") { rightArmBone = t; rightArmBaseRot = t.localRotation; }
            }
        }

        GameObject jlGo = new GameObject("Act5_Jumpscare_StrobeLight");
        Light jl = jlGo.AddComponent<Light>();
        jl.type = LightType.Point;
        jl.range = 5.5f;
        jl.shadows = LightShadows.None;

        float dur = 0.72f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / dur);

            // Strobe visibility for first 0.22s, then full lunge into camera
            bool visible = u > 0.25f || (Random.value > 0.25f);
            float lungeT = u < 0.22f ? (u * 0.3f) : Mathf.Lerp(0.066f, 1.0f, 1f - Mathf.Pow(1f - ((u - 0.22f) / 0.78f), 3f));

            Vector3 mPos = Vector3.Lerp(lungeStartFloor, lungeEndFloor, lungeT);
            if (monster != null)
            {
                Vector3 toPlayer = camPos - mPos;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.001f)
                {
                    monster.transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
                }

                monster.transform.position = mPos;
                if (monsterRends != null && monsterRends.Length > 0)
                {
                    for (int i = 0; i < monsterRends.Length; i++)
                    {
                        if (monsterRends[i] != null) monsterRends[i].enabled = true;
                    }
                    Bounds b = monsterRends[0].bounds;
                    for (int i = 1; i < monsterRends.Length; i++)
                    {
                        if (monsterRends[i] != null) b.Encapsulate(monsterRends[i].bounds);
                    }
                    float dy = (mPos.y - b.min.y) - 0.05f;
                    monster.transform.position = new Vector3(mPos.x, monster.transform.position.y + dy, mPos.z);

                    for (int i = 0; i < monsterRends.Length; i++)
                    {
                        if (monsterRends[i] != null) monsterRends[i].enabled = visible;
                    }
                }

                if (pelvisBone != null) pelvisBone.localRotation = pelvisBaseRot * Quaternion.Euler(0f, 0f, -22f);
                if (neckBone != null) neckBone.localRotation = neckBaseRot * Quaternion.Euler(Random.Range(-32f, 32f), Random.Range(-36f, 36f), -24f);
                if (headBone != null) headBone.localRotation = headBaseRot * Quaternion.Euler(Random.Range(-45f, 45f), Random.Range(-55f, 55f), Random.Range(-45f, 45f));
                if (leftArmBone != null) leftArmBone.localRotation = leftArmBaseRot * Quaternion.Euler(-48f, 0f, -58f);
                if (rightArmBone != null) rightArmBone.localRotation = rightArmBaseRot * Quaternion.Euler(48f, 0f, 58f);
            }

            Vector3 monsterEye = headBone != null
                ? (headBone.position + Vector3.up * 0.05f)
                : (mPos + Vector3.up * 1.58f);

            jl.transform.position = Vector3.Lerp(camPos, monsterEye, 0.5f) + Vector3.up * 0.1f;
            jl.color = (Random.value > 0.38f) ? new Color(1.0f, 0.90f, 0.85f) : new Color(0.95f, 0.15f, 0.12f);
            jl.intensity = visible ? Random.Range(2.5f, 5.2f) : 0.2f;

            if (cam != null)
            {
                Vector3 toFace = (monsterEye - cam.transform.position).normalized;
                if (toFace.sqrMagnitude > 0.001f)
                {
                    Quaternion snapRot = Quaternion.LookRotation(toFace, Vector3.up) *
                                         Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-7f, 7f));
                    cam.transform.rotation = snapRot;
                }
                cam.transform.localPosition = baseCamLocalPos + Random.insideUnitSphere * 0.038f;
                cam.fieldOfView = Mathf.Lerp(baseFov, 34f, lungeT);
            }

            yield return null;
        }

        Destroy(jlGo);
        if (monsterRends != null)
        {
            for (int i = 0; i < monsterRends.Length; i++)
            {
                if (monsterRends[i] != null) monsterRends[i].enabled = false;
            }
        }
        if (cam != null)
        {
            cam.transform.localPosition = baseCamLocalPos;
            cam.fieldOfView = baseFov;
        }
    }
}

/// <summary>
/// Automatically intercepts and sanitizes legacy UI input modules (StandaloneInputModule)
/// across all scenes before and after scene load, replacing them with modern InputSystemUIInputModule.
/// This prevents the infinite loop of InvalidOperationException: UnityEngine.Input.get_mousePosition().
/// </summary>
public static class InputSystemSanitizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        SanitizeAllEventSystems();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        SanitizeAllEventSystems();
    }

    public static void SanitizeAllEventSystems()
    {
        // 1. Sanitize all EventSystems and strip legacy StandaloneInputModule
        EventSystem[] allEventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        for (int i = 0; i < allEventSystems.Length; i++)
        {
            EventSystem es = allEventSystems[i];
            if (es == null) continue;

            StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                legacy.enabled = false;
                Object.Destroy(legacy);
            }

#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule newModule = es.GetComponent<InputSystemUIInputModule>();
            if (newModule == null)
            {
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#endif
        }

        // 2. Also search for any stray StandaloneInputModule components anywhere in scene
        StandaloneInputModule[] strayModules = Object.FindObjectsByType<StandaloneInputModule>(FindObjectsInactive.Include);
        for (int i = 0; i < strayModules.Length; i++)
        {
            if (strayModules[i] != null)
            {
                strayModules[i].enabled = false;
                Object.Destroy(strayModules[i]);
            }
        }
    }
}


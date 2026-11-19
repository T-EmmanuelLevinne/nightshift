using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls narrative pacing, typewriter dialogues, and task progression.
/// Auto-initializes in any scene (SampleScene, Scene2, etc.).
/// </summary>
public class NightShiftStoryController : MonoBehaviour
{
    public static NightShiftStoryController Instance { get; private set; }

    public static bool IsTalkToBillyTaskActive { get; private set; } = false;
    public static bool HasTalkedToBilly { get; private set; } = false;
    public static bool IsCleaningTaskActive { get; private set; } = false;
    public static bool IsInspectShelfTaskActive { get; private set; } = false;
    public static bool HasInspectedEmptyShelf { get; private set; } = false;
    public static bool IsTrashTaskActive { get; private set; } = false;
    public static bool HasDisposedTrash { get; private set; } = false;
    public static bool IsReturnToCounterTaskActive { get; private set; } = false;
    public static bool HasReturnedToCounter { get; private set; } = false;
    public static bool IsStockingTaskActive { get; private set; } = false;
    public static bool IsWaitingForBoxReturnToOrigin { get; private set; } = false;
    public static bool IsDialogueActive =>
        HorrorMainMenuController.IsMainMenuActive ||
        (Instance != null && (Instance.isDialogueVisible || Instance.isMovementLockedForSequence));
    public static bool IsIntroOverlayBlockingUI =>
        HorrorMainMenuController.IsMainMenuActive ||
        (Instance != null && Instance.isIntroOverlayActive && Instance.introBlackAlpha > 0.35f);

    // Act 4 (Shift 2) Task & Event State
    public static bool IsAct4Active { get; private set; } = false;
    public static bool IsAct4KnockRestroomTaskActive { get; private set; } = false;
    public static bool HasAct4KnockedRestroomDoor { get; private set; } = false;
    public static bool IsAct4ForceOpenRestroomTaskActive { get; private set; } = false;
    public static bool HasAct4OpenedRestroomDoor { get; private set; } = false;
    public static bool IsAct4Call911TaskActive { get; private set; } = false;
    public static bool HasAct4Dialed911 { get; private set; } = false;
    public static bool IsAct4WaitForHelpTaskActive { get; private set; } = false;

    private string act4TaskHeaderOverride = "";
    private string act4TaskSubOverride = "";
    private bool act4TaskCompletedFlag = false;
    private bool act4ShowCounterStandIndicator = false;
    private InteractableRestroomDoor act4MaleRestroomDoorRef;

    public enum StoryStage
    {
        InitialDelay,
        IntroAtWorkFade,
        Waiting2sForTalkToBillyTask,
        TalkToBillyTaskActive,
        TalkingWithBilly,
        Waiting4sAfterBillyConversation,
        Dialogue1_Opening,
        WaitingToEnterStoreAndSeeMess,
        Dialogue2_MessDiscovered,
        MessDialogueCooldown2s,
        Dialogue2_MessDiscovered2,
        MessCooldown,
        CleaningTaskActive,
        CleaningTaskCompleted,
        Dialogue3_CleaningFinished,
        Waiting15sForCustomer,
        CustomerSteveActive,
        Waiting7sForShelfInspect,
        Dialogue_HoldOn,
        HoldOnCooldown,
        InspectShelfTaskActive,
        Dialogue4_EmptyShelfInspected,
        PostInspect2sCooldown,
        Dialogue5_RestockTomorrow,
        Waiting4sForCustomerMelia,
        CustomerMeliaActive,
        Waiting7_5sAfterMeliaLeaves,
        Dialogue6_RealizedTrash1,
        TrashDialogueCooldown2s,
        Dialogue7_RealizedTrash2,
        TrashDialogueCooldown1s,
        Dialogue8_RealizedTrash3,
        TrashTaskActive,
        TrashTaskCompleted,
        JunkBehindPlayerFreeze,
        JunkWaitingForLookBack,
        JunkConversationActive,
        Waiting3sAfterJunkWalks,
        Dialogue_ThatWasWeird,
        Waiting1sBeforeBackToCounter,
        Dialogue_GetBackToCounter,
        ReturnToCounterTaskActive,
        Dialogue_TimeToWaitItOutAgain,
        Waiting2sBeforeEndingFade,
        EndingFadeToBlack,
        EndingEpilogueLine1,
        EndingEpilogueCooldown,
        EndingEpilogueLine2,
        ShiftEndedBlackScreen,
        WaitingForFurtherInstructions,
        StockingTaskActive,
        StockingTaskCompleted
    }

    [Header("Current Narrative Stage")]
    public StoryStage currentStage = StoryStage.InitialDelay;

    [Header("Dialogue Timing")]
    public float typingCharDelay = 0.042f;
    public float dialogueStayDuration = 3.0f;
    public float dialogueFadeDuration = 1.3f;
    public float taskCooldownDuration = 2.0f;
    public float customerArrivalDelay = 15.0f;
    public float shelfInspectDelay = 7.0f;

    [Header("Gas Station Store Bounds (Interior Detection)")]
    public float storeMinX = -1.65f;
    public float storeMaxX = 5.15f;
    public float storeMinZ = -8.95f;
    public float storeMaxZ = 6.80f;
    public float storeMinY = -0.2f;
    public float storeMaxY = 3.5f;

    // Dialogue playback variables
    private string targetDialogueText = "";
    private string displayedDialogueText = "";
    private float dialogueAlpha = 0f;
    private bool isTyping = false;
    private bool isDialogueVisible = false;
    private bool isMovementLockedForSequence = false;
    private bool skipCurrentDialogueRequested = false;

    // Opening "At work..." fade-in / typing / fade-out variables
    private bool isIntroOverlayActive = true;
    private float introBlackAlpha = 1.0f;
    private float introTextAlpha = 0.0f;
    private string introDisplayedText = "";
    private bool isIntroTyping = false;

    // Return-to-Counter target spot
    private Transform counterReturnSpotTransform;
    private Collider counterReturnSpotCollider;

    // Audio & Styling
    private AudioSource audioSource;
    private AudioClip typeClickClip;
    private AudioClip taskChimeClip;
    private Font retroFont;
    private Texture2D pixelTexture;

    private GUIStyle introTitleStyle;
    private GUIStyle introTitleShadowStyle;
    private GUIStyle dialogueStyle;
    private GUIStyle dialogueShadowStyle;
    private GUIStyle taskHeaderStyle;
    private GUIStyle taskHeaderShadowStyle;
    private GUIStyle taskSubStyle;
    private GUIStyle taskSubShadowStyle;
    private GUIStyle indicatorStyle;
    private GUIStyle indicatorShadowStyle;

    public static bool IsPlayerInHouseScene()
    {
        // 1. If active scene is explicitly HouseScene
        Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.IsValid() && string.Equals(activeScene.name, "HouseScene", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. If HouseScene is loaded in the scene hierarchy
        Scene houseScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("HouseScene");
        if (houseScene.IsValid() && houseScene.isLoaded)
        {
            // If active player is located at the house (cabin bounds are Z ~308m, gas station is Z ~ -11m)
            GameObject player = GameObject.Find("Test_FPS_Player");
            if (player == null) player = GameObject.FindWithTag("Player");
            if (player != null && player.transform.position.z > 60f)
            {
                return true;
            }

            // If player is not yet spawned or at HouseScene
            if (activeScene.name != "GasScene2")
            {
                return true;
            }
        }

        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (IsPlayerInHouseScene())
        {
            return;
        }

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        // Strictly only auto-initialize in GasScene2 (HouseScene has its own separate storyline coming soon)
        if (string.Equals(sceneName, "GasScene2", System.StringComparison.OrdinalIgnoreCase))
        {
            EnsureExists();
        }
    }

    public static NightShiftStoryController EnsureExists()
    {
        if (IsPlayerInHouseScene())
        {
            return null;
        }

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        // Strictly only run the Gas Station storyline in GasScene2
        if (!string.Equals(sceneName, "GasScene2", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Instance != null) return Instance;

        NightShiftStoryController found = Object.FindAnyObjectByType<NightShiftStoryController>();
        if (found != null)
        {
            Instance = found;
            return Instance;
        }

        GameObject go = new GameObject("NightShift_StoryController");
        Instance = go.AddComponent<NightShiftStoryController>();
        return Instance;
    }

    private void Awake()
    {
        if (IsPlayerInHouseScene())
        {
            isIntroOverlayActive = false;
            introBlackAlpha = 0f;
            introTextAlpha = 0f;
            IsTalkToBillyTaskActive = false;
            enabled = false;
            Destroy(gameObject);
            return;
        }

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!string.Equals(sceneName, "GasScene2", System.StringComparison.OrdinalIgnoreCase))
        {
            // HouseScene has its own separate storyline - do not run gas station storyline here!
            isIntroOverlayActive = false;
            introBlackAlpha = 0f;
            introTextAlpha = 0f;
            IsTalkToBillyTaskActive = false;
            enabled = false;
            Destroy(gameObject);
            return;
        }

        if (Instance == null || !Instance.gameObject.activeInHierarchy)
        {
            Instance = this;
        }

        IsTalkToBillyTaskActive = false;
        HasTalkedToBilly = false;
        IsCleaningTaskActive = false;
        IsInspectShelfTaskActive = false;
        HasInspectedEmptyShelf = false;
        IsTrashTaskActive = false;
        HasDisposedTrash = false;
        IsReturnToCounterTaskActive = false;
        HasReturnedToCounter = false;
        IsStockingTaskActive = false;

        // Start on a full black screen for the "At work..." opening sequence
        isIntroOverlayActive = true;
        introBlackAlpha = 1.0f;
        introTextAlpha = 0.0f;
        introDisplayedText = "";
        isIntroTyping = false;
        isMovementLockedForSequence = true;

        if (shelfInspectDelay <= 0.1f)
        {
            shelfInspectDelay = 7.0f;
        }

        // Ensure accurate store interior bounds even if older values were serialized on scene component
        storeMinX = -1.65f;
        storeMaxX = 5.15f;
        storeMinZ = -8.95f;
        storeMaxZ = 6.80f;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D crisp stereo sound
            audioSource.volume = 0.65f;
        }

        InitTextures();
        InitFont();
        GenerateProceduralAudio();

        // Ensure Midnight atmosphere, Shelf1 empty bay target, NPCs ("BillyNPC", "Steve", "MeliaNPC", "JunkNPC"), CashierPC, Trashbag, and Dumpster are initialized cleanly
        MidnightAtmosphereController.EnsureExistsAndApply();
        ShelfRestockTarget.EnsureExists();
        InteractableCashierPC.EnsureAttachedToScenePC();
        CoworkerNpcBilly.EnsureAttachedToSceneNpc();
        CustomerNpcSteve.EnsureAttachedToSceneNpc();
        CustomerNpcMelia.EnsureAttachedToSceneNpc();
        StrangeNpcJunk.EnsureAttachedToSceneNpc();
        Act4JohnNpc.EnsureExistsInScene();
        InteractableMultipurposeKey.EnsureAttachedToSceneKey();
        InteractableTrashbag.EnsureAttachedToSceneTrashbag();
        InteractableDumpster.EnsureAttachedToSceneDumpsters();
        ResolveCounterReturnSpot();
        Act4UmbrellaStandSpot.EnsureCreatedNearCashier(GetCounterSpotWorldPosition());
    }

    private IEnumerator Start()
    {
        HorrorMainMenuController.EnsureMainMenuShown();
        while (HorrorMainMenuController.IsMainMenuActive)
        {
            yield return null;
        }

        // Note: In normal progression from HouseScene Act 1, currentStoryAct is 2 (Shift 1) and from HouseScene Act 3 it is 4 (Shift 2).
        if (PlayerManager.Instance != null && PlayerManager.Instance.currentStoryAct >= 4)
        {
            StartCoroutine(Act4Shift2RainEntryRoutine());
        }
        else
        {
            StartCoroutine(StorySequenceRoutine());
        }
    }

    private void Update()
    {
        if ((isDialogueVisible || isIntroOverlayActive) && HorrorHudStyle.WasSkipDialoguePressedThisFrame())
        {
            skipCurrentDialogueRequested = true;
        }
    }

    public static bool IsPlayerInsideStoreStatic()
    {
        if (Instance == null) return false;
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        Vector3 pPos = cam.transform.position;
        return pPos.x >= Instance.storeMinX && pPos.x <= Instance.storeMaxX &&
               pPos.z >= Instance.storeMinZ && pPos.z <= Instance.storeMaxZ &&
               pPos.y >= Instance.storeMinY && pPos.y <= Instance.storeMaxY;
    }

    private void HideAllFloorStainsForAct4()
    {
        FloorStain[] stains = Object.FindObjectsByType<FloorStain>(FindObjectsInactive.Include);
        for (int i = 0; i < stains.Length; i++)
        {
            if (stains[i] != null)
            {
                stains[i].isCleaned = true;
                stains[i].gameObject.SetActive(false);
            }
        }

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            string nm = t.name.Trim();
            if (nm.StartsWith("Floor_Stain_", System.StringComparison.OrdinalIgnoreCase))
            {
                t.gameObject.SetActive(false);
            }
        }
    }

    public void NotifyAct4RestroomDoorKnocked(InteractableRestroomDoor door)
    {
        act4MaleRestroomDoorRef = door;
        HasAct4KnockedRestroomDoor = true;
    }

    public void NotifyAct4RestroomDoorOpened(InteractableRestroomDoor door)
    {
        act4MaleRestroomDoorRef = door;
        HasAct4OpenedRestroomDoor = true;
    }

    public void NotifyAct4Dialed911(Act4CounterPhoneAndDialpad phone)
    {
        HasAct4Dialed911 = true;
    }

    public static bool HasAct4TriggeredStockroomExitJumpscare { get; private set; } = false;

    public void NotifyAct4StockroomExitDoorOpened(InteractableRestroomDoor door)
    {
        HasAct4TriggeredStockroomExitJumpscare = true;
    }

    private IEnumerator Act4Shift2RainEntryRoutine()
    {
        IsAct4Active = true;
        IsTalkToBillyTaskActive = false;
        HasTalkedToBilly = false;
        IsCleaningTaskActive = false;
        IsInspectShelfTaskActive = false;
        IsTrashTaskActive = false;
        IsReturnToCounterTaskActive = false;
        IsStockingTaskActive = false;
        IsAct4KnockRestroomTaskActive = false;
        HasAct4KnockedRestroomDoor = false;
        IsAct4ForceOpenRestroomTaskActive = false;
        HasAct4OpenedRestroomDoor = false;
        IsAct4Call911TaskActive = false;
        HasAct4Dialed911 = false;
        IsAct4WaitForHelpTaskActive = false;
        act4TaskHeaderOverride = "";
        act4TaskSubOverride = "";
        act4TaskCompletedFlag = false;
        act4ShowCounterStandIndicator = false;

        // 0. Ensure 3 stains and Billy are already gone on Act 4
        HideAllFloorStainsForAct4();
        CoworkerNpcBilly billy = Object.FindAnyObjectByType<CoworkerNpcBilly>(FindObjectsInactive.Include);
        if (billy != null)
        {
            billy.gameObject.SetActive(false);
        }

        // Ensure Rain Storm, Umbrella in Player's Hand, Counter Umbrella Stand, KeyMultiPurpose, and JohnNPC are ready
        Act3RainStormController.EnsureActive(transform);
        ResolveCounterReturnSpot();
        Act4PlayerUmbrella.EnsureEquippedOnPlayer();
        Act4UmbrellaStandSpot.EnsureCreatedNearCashier(GetCounterSpotWorldPosition());
        InteractableMultipurposeKey.EnsureAttachedToSceneKey();
        Act4JohnNpc.EnsureExistsInScene();

        // 1. Fade in from black screen
        isMovementLockedForSequence = true;
        isIntroOverlayActive = true;
        introBlackAlpha = 1.0f;
        introTextAlpha = 0.0f;
        introDisplayedText = "";

        float fadeDur = 2.0f;
        float elapsed = 0f;
        while (elapsed < fadeDur)
        {
            elapsed += Time.deltaTime;
            introBlackAlpha = Mathf.SmoothStep(1.0f, 0.0f, Mathf.Clamp01(elapsed / fadeDur));
            yield return null;
        }

        introBlackAlpha = 0.0f;
        isIntroOverlayActive = false;
        isMovementLockedForSequence = false;

        // 2. 2 seconds cooldown -> Task: Go inside.
        yield return new WaitForSeconds(2.0f);

        act4TaskHeaderOverride = "Task: Go inside.";
        act4TaskSubOverride = "";
        act4TaskCompletedFlag = false;
        PlayTaskChime();

        // Wait until player enters the store
        while (!IsPlayerInsideStoreStatic())
        {
            yield return null;
        }

        // Once inside, allow sheathing and putting down the umbrella near the cashier counter (world indicator shown automatically)
        while (Act4PlayerUmbrella.Instance != null && !Act4PlayerUmbrella.Instance.isPutDown)
        {
            yield return null;
        }

        // Spawn the phone on top of the flat basket on the counter only once the umbrella is placed in its stand
        Act4CounterPhoneAndDialpad.EnsurePlacedOnCounter(GetCounterSpotWorldPosition());

        act4TaskHeaderOverride = "Task: Go inside. [COMPLETED]";
        act4TaskCompletedFlag = true;
        PlayTaskChime();

        // 3. After putting down the umbrella sheath, 3 seconds cooldown -> completely freeze player -> Canned food monologue
        yield return new WaitForSeconds(3.0f);

        act4TaskHeaderOverride = "";
        act4TaskSubOverride = "";
        act4TaskCompletedFlag = false;

        // Completely freeze character user
        isMovementLockedForSequence = true;
        SimpleFPSController.IsExternalCameraAnimationActive = true;

        yield return StartCoroutine(PlayDialogueRoutine("You: I almost forgot.."));
        yield return new WaitForSeconds(2.0f);
        yield return StartCoroutine(PlayDialogueRoutine("You: I have to restock the missing canned foods.."));

        SimpleFPSController.IsExternalCameraAnimationActive = false;
        isMovementLockedForSequence = false;

        // 4. Task: Restock the missing stock. / - Find a way to restock the shelve.
        IsStockingTaskActive = true;
        IsWaitingForBoxReturnToOrigin = false;
        currentStage = StoryStage.StockingTaskActive;
        act4TaskHeaderOverride = "Task: Restock the missing stock.";
        act4TaskSubOverride = "- Find a way to restock the shelve.";
        act4TaskCompletedFlag = false;
        PlayTaskChime();

        while (ShelfRestockTarget.Instance == null || !ShelfRestockTarget.Instance.IsFullyStocked)
        {
            yield return null;
        }

        // Require the player to put the empty box back where it came from before proceeding;
        // repeat "You: I should put the box where it came from.." every 5 seconds until returned
        RestockCardboardBox restockBox = RestockCardboardBox.Instance != null
            ? RestockCardboardBox.Instance
            : Object.FindAnyObjectByType<RestockCardboardBox>();
        if (restockBox != null && !restockBox.IsAtOrigin)
        {
            IsWaitingForBoxReturnToOrigin = true;
            while (restockBox != null && !restockBox.IsAtOrigin)
            {
                yield return StartCoroutine(PlayDialogueRoutine("You: I should put the box where it came from.."));
                if (restockBox == null || restockBox.IsAtOrigin)
                {
                    break;
                }

                float boxReminderWait = 0f;
                while (boxReminderWait < 5.0f && restockBox != null && !restockBox.IsAtOrigin)
                {
                    boxReminderWait += Time.deltaTime;
                    yield return null;
                }
            }
            IsWaitingForBoxReturnToOrigin = false;
        }

        act4TaskHeaderOverride = "Task: Restock the missing stock. [COMPLETED]";
        act4TaskSubOverride = "- Find a way to restock the shelve.";
        act4TaskCompletedFlag = true;
        PlayTaskChime();

        // 5. Wait for 10 seconds cooldown (ONLY counts down while player stands on PlayerNpcInteract; show Billy-style indicator)
        act4ShowCounterStandIndicator = true;
        float standOnCounterTimer = 0f;
        while (standOnCounterTimer < 10.0f)
        {
            if (IsPlayerAtCounterSpot())
            {
                standOnCounterTimer += Time.deltaTime;
            }
            yield return null;
        }
        act4ShowCounterStandIndicator = false;
        IsStockingTaskActive = false;
        act4TaskHeaderOverride = "";
        act4TaskSubOverride = "";
        act4TaskCompletedFlag = false;

        // 6. SteveNPC walks to store with umbrella, leans it on Pillar_Front_Center, browses chipShelve 5s, grabs MamaChip, places on counter
        CustomerNpcSteve steve = CustomerNpcSteve.EnsureAttachedToSceneNpc();
        if (steve != null)
        {
            currentStage = StoryStage.CustomerSteveActive;
            steve.StartAct4CustomerVisit();

            while (!steve.HasAct4SteveStartedLeaving)
            {
                if (steve.currentState == CustomerNpcSteve.NpcState.WaitingForPlayerScan ||
                    steve.currentState == CustomerNpcSteve.NpcState.WaitingForPlayerCashierPCCheckout)
                {
                    act4TaskHeaderOverride = "Task: Check out.";
                    act4TaskSubOverride = "";
                    act4TaskCompletedFlag = false;
                }
                yield return null;
            }

            act4TaskHeaderOverride = "Task: Check out. [COMPLETED]";
            act4TaskSubOverride = "";
            act4TaskCompletedFlag = true;
            PlayTaskChime();
        }

        // 7. While Steve walks out, start 12 seconds cooldown timer
        yield return new WaitForSeconds(12.0f);

        act4TaskHeaderOverride = "";
        act4TaskSubOverride = "";
        act4TaskCompletedFlag = false;

        // 8. *Lights starts flickering and turn back to normal* -> 2s -> You: Oh? -> 2s
        yield return StartCoroutine(FlickerGasStationLightsRoutine(1.85f, turnOffAtEnd: false));
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Oh?"));
        yield return new WaitForSeconds(2.0f);

        // 9. JohnNPC walks inside store in front of cashier counter -> bathroom request -> walks into Male_Restroom and locks door
        Act4JohnNpc john = Act4JohnNpc.EnsureExistsInScene();
        if (john != null)
        {
            yield return StartCoroutine(john.WalkInAndRequestBathroomRoutine());
        }

        yield return new WaitForSeconds(2.0f);

        // 10. Task: Remain in the counter. (must remain in the counter on PlayerNpcInteract)
        act4TaskHeaderOverride = "Task: Remain in the counter.";
        act4TaskSubOverride = "";
        act4TaskCompletedFlag = false;
        act4ShowCounterStandIndicator = true;
        PlayTaskChime();

        while (!IsPlayerAtCounterSpot())
        {
            yield return null;
        }

        // 11. MeliaNPC walks in -> HersheyShelve 5s -> grabs Hershey -> puts on counter -> Task: Check out Melia ($5, "Mortuus es.")
        CustomerNpcMelia melia = CustomerNpcMelia.EnsureAttachedToSceneNpc();
        if (melia != null)
        {
            currentStage = StoryStage.CustomerMeliaActive;
            Coroutine meliaVisit = melia.StartAct4CustomerVisit();

            while (melia.currentState != CustomerNpcMelia.NpcState.WaitingForPlayerCashierTalk &&
                   melia.currentState != CustomerNpcMelia.NpcState.DepartedHidden)
            {
                yield return null;
            }

            act4ShowCounterStandIndicator = false;
            act4TaskHeaderOverride = "Task: Check out Melia";
            act4TaskSubOverride = "";
            act4TaskCompletedFlag = false;
            PlayTaskChime();

            while (!melia.IsAct4CheckoutCompleted && melia.currentState != CustomerNpcMelia.NpcState.DepartedHidden)
            {
                yield return null;
            }

            act4TaskHeaderOverride = "Task: Check out Melia [COMPLETED]";
            act4TaskSubOverride = "";
            act4TaskCompletedFlag = true;
            PlayTaskChime();

            yield return meliaVisit;
        }

        // 12. 6 seconds after Melia leaves -> 4 monologue lines -> Task: Knock the restroom door.
        yield return new WaitForSeconds(6.0f);

        act4TaskHeaderOverride = "";
        act4TaskSubOverride = "";
        act4TaskCompletedFlag = false;

        yield return StartCoroutine(PlayDialogueRoutine("You: What happened to melia?"));
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: That was weird.."));
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Hold on.. I gotta check the guy in the bathroom."));
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: He's been there for ages.."));
        yield return new WaitForSeconds(2.0f);

        IsAct4KnockRestroomTaskActive = true;
        HasAct4KnockedRestroomDoor = false;
        act4TaskHeaderOverride = "Task: Knock the restroom door.";
        act4TaskSubOverride = "";
        act4TaskCompletedFlag = false;
        PlayTaskChime();

        while (!HasAct4KnockedRestroomDoor)
        {
            yield return null;
        }

        IsAct4KnockRestroomTaskActive = false;
        act4TaskHeaderOverride = "Task: Knock the restroom door. [COMPLETED]";
        act4TaskCompletedFlag = true;
        PlayTaskChime();

        // 13. Knocking dialogue sequence
        yield return new WaitForSeconds(2.0f);
        yield return StartCoroutine(PlayDialogueRoutine("You: Sir? Are you okay there?"));
        yield return new WaitForSeconds(3.0f);

        if (act4MaleRestroomDoorRef != null)
        {
            act4MaleRestroomDoorRef.PlayKnockSound();
        }
        yield return StartCoroutine(PlayDialogueRoutine("You: *knocks*.."));
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("no response.."));
        yield return StartCoroutine(PlayDialogueRoutine("You: Sir? Are you still in there?"));
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Still no response.."));
        yield return new WaitForSeconds(2.0f);

        // 14. Task: Force to open the door / - Find a way to get in -> KeyMultiPurpose -> Unlock & Open
        IsAct4ForceOpenRestroomTaskActive = true;
        HasAct4OpenedRestroomDoor = false;
        act4TaskHeaderOverride = "Task: Force to open the door";
        act4TaskSubOverride = "- Find a way to get in";
        act4TaskCompletedFlag = false;
        PlayTaskChime();

        // Prepare JohnNPC lying dead on the restroom floor with 2 blood stains before the player opens the door
        if (john != null)
        {
            john.PrepareMurderSceneInRestroom();
        }

        yield return new WaitForSeconds(2.0f);
        yield return StartCoroutine(PlayDialogueRoutine("You: I'll force to open the door okay?"));

        while (!HasAct4OpenedRestroomDoor)
        {
            yield return null;
        }

        IsAct4ForceOpenRestroomTaskActive = false;
        act4TaskHeaderOverride = "Task: Force to open the door [COMPLETED]";
        act4TaskSubOverride = "- Find a way to get in";
        act4TaskCompletedFlag = true;
        PlayTaskChime();

        // 15. Door opened: lights flicker, suspenseful slow camera look animation at the 2 blood stains & dead John
        Coroutine continuousFlicker = StartCoroutine(FlickerGasStationLightsRoutine(3.6f, turnOffAtEnd: false));
        yield return StartCoroutine(PlaySuspenseRestroomBloodLookRoutine(john));
        if (continuousFlicker != null) StopCoroutine(continuousFlicker);

        yield return StartCoroutine(PlayDialogueRoutine("You: What the f***?!"));
        yield return new WaitForSeconds(2.0f);

        // 16. Lights flicker and proceed to total dark (turn off ALL lights in the store and gas station)
        yield return StartCoroutine(FlickerGasStationLightsRoutine(1.35f, turnOffAtEnd: true));

        SimpleFPSController.IsExternalCameraAnimationActive = false;
        isMovementLockedForSequence = false;

        yield return StartCoroutine(PlayDialogueRoutine("You: What happened to him?!.. What's going on here?!"));
        yield return new WaitForSeconds(2.0f);

        // 17. Task: Remain in the counter / - Find your phone and call 911
        Act4CounterPhoneAndDialpad phone = Act4CounterPhoneAndDialpad.EnsurePlacedOnCounter(GetCounterSpotWorldPosition());
        IsAct4Call911TaskActive = true;
        HasAct4Dialed911 = false;
        act4ShowCounterStandIndicator = true;
        act4TaskHeaderOverride = "Task: Remain in the counter";
        act4TaskSubOverride = "- Find your phone and call 911";
        act4TaskCompletedFlag = false;
        PlayTaskChime();

        while (!HasAct4Dialed911)
        {
            yield return null;
        }

        IsAct4Call911TaskActive = false;
        act4ShowCounterStandIndicator = false;

        // 18. *ringing* dialogue for 4 seconds -> 911 Emergency Call Conversation
        if (phone != null) phone.PlayRingbackTone();
        yield return StartCoroutine(PlayDialogueRoutine("*ringing*"));
        yield return new WaitForSeconds(4.0f);

        yield return StartCoroutine(PlayDialogueRoutine("Caller: 911, what is your emergency do you require?"));
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Send me an ambulance now! At route 9 gas station please!"));
        yield return StartCoroutine(PlayDialogueRoutine("Caller: Help will arrive there shortly."));
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Thank you! and please hurry up!"));

        // 19. Task: Remain in the counter / - Wait for help (12 seconds)
        IsAct4WaitForHelpTaskActive = true;
        HasAct4TriggeredStockroomExitJumpscare = false;
        act4ShowCounterStandIndicator = true;
        act4TaskHeaderOverride = "Task: Remain in the counter";
        act4TaskSubOverride = "- Wait for help";
        act4TaskCompletedFlag = false;
        PlayTaskChime();

        yield return new WaitForSeconds(12.0f);

        IsAct4WaitForHelpTaskActive = false;
        act4ShowCounterStandIndicator = false;

        // 20. After 12 seconds -> Task: ??? / - Follow the footstep (with corrupted/glitching SFX & walking bloody footsteps to Stockroom)
        Act4StockroomHorrorSequence stockroomHorror = Act4StockroomHorrorSequence.EnsureExists();
        if (stockroomHorror != null)
        {
            stockroomHorror.PlayCorruptedTaskGlitchSfx();
            StartCoroutine(stockroomHorror.SpawnWalkingBloodyFootstepsRoutine());
        }

        act4TaskHeaderOverride = "Task: ???";
        act4TaskSubOverride = "- Follow the footstep";
        act4TaskCompletedFlag = false;

        // Wait until the player opens the Stockroom door and steps inside the Stockroom
        while (stockroomHorror != null && !stockroomHorror.IsPlayerInsideStockroom())
        {
            yield return null;
        }

        // 21. Door shuts from behind, slow look-back animation, flickering light + MonsterNPC head seizure & slow creep to POV
        isMovementLockedForSequence = true;
        act4TaskHeaderOverride = "";
        act4TaskSubOverride = "";

        if (stockroomHorror != null)
        {
            yield return StartCoroutine(stockroomHorror.PlayStockroomDoorShutAndMonsterCreepRoutine(this));
        }

        // 22. Light is back ON -> 2s -> "You: Mortuus sum." -> 2s -> Task: Go outside. / - DIE, DIE, DIE, DIE
        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(PlayDialogueRoutine("You: Mortuus sum."));
        yield return new WaitForSeconds(2.0f);

        isMovementLockedForSequence = false;
        SimpleFPSController.IsExternalCameraAnimationActive = false;

        if (stockroomHorror != null)
        {
            stockroomHorror.PlayCorruptedTaskGlitchSfx();
        }
        act4TaskHeaderOverride = "Task: Go outside.";
        act4TaskSubOverride = "- DIE, DIE, DIE, DIE";
        act4TaskCompletedFlag = false;

        // 23. Wait until the player opens the Stockroom door, the door fully opens, and the player walks outside the Stockroom
        while (!HasAct4TriggeredStockroomExitJumpscare ||
               (stockroomHorror != null && !stockroomHorror.HasPlayerFullyExitedStockroom()))
        {
            yield return null;
        }

        // 2 seconds cooldown after leaving the Stockroom before MonsterNPC triggers the immediate lunging jumpscare
        yield return new WaitForSeconds(2.0f);

        isMovementLockedForSequence = true;
        act4TaskHeaderOverride = "";
        act4TaskSubOverride = "";

        if (stockroomHorror != null)
        {
            yield return StartCoroutine(stockroomHorror.PlayStockroomExitJumpscareRoutine());
        }

        // Cut immediately to solid black screen after jumpscare
        isIntroOverlayActive = true;
        introBlackAlpha = 1.0f;
        introTextAlpha = 0.0f;
        introDisplayedText = "";
        yield return new WaitForSeconds(1.2f);

        UnityEngine.PlayerPrefs.SetInt("HouseScene_ForceAct5", 1);
        UnityEngine.PlayerPrefs.Save();

        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.currentStoryAct = 5;
            PlayerManager.Instance.TransitionToScene("HouseScene", "Default");
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("HouseScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private IEnumerator FlickerGasStationLightsRoutine(float duration, bool turnOffAtEnd)
    {
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        List<Light> stationLights = new List<Light>();
        List<float> baseIntensities = new List<float>();

        for (int i = 0; i < allLights.Length; i++)
        {
            Light l = allLights[i];
            if (l == null || l.type == LightType.Directional) continue;
            if (l.name.IndexOf("Lightning", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                l.name.IndexOf("Phone", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            stationLights.Add(l);
            baseIntensities.Add(Mathf.Max(0.2f, l.intensity));
        }

        float elapsed = 0f;
        float nextStep = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= nextStep)
            {
                nextStep = elapsed + Random.Range(0.04f, 0.12f);
                bool flickerOn = Random.value > 0.45f;
                float mult = flickerOn ? Random.Range(0.35f, 1.15f) : 0.0f;

                for (int i = 0; i < stationLights.Count; i++)
                {
                    if (stationLights[i] != null)
                    {
                        stationLights[i].enabled = mult > 0.01f;
                        stationLights[i].intensity = baseIntensities[i] * mult;
                    }
                }

                if (audioSource != null && typeClickClip != null && !flickerOn)
                {
                    audioSource.pitch = Random.Range(0.55f, 0.75f);
                    audioSource.PlayOneShot(typeClickClip, 0.25f);
                }
            }
            yield return null;
        }

        for (int i = 0; i < stationLights.Count; i++)
        {
            if (stationLights[i] != null)
            {
                if (turnOffAtEnd)
                {
                    stationLights[i].intensity = 0f;
                    stationLights[i].enabled = false;
                }
                else
                {
                    stationLights[i].enabled = true;
                    stationLights[i].intensity = baseIntensities[i];
                }
            }
        }

        if (turnOffAtEnd)
        {
            // Darken emissive ceiling fixtures across the store & gas station canopy
            Renderer[] allRends = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            for (int i = 0; i < allRends.Length; i++)
            {
                Renderer r = allRends[i];
                if (r == null || r.sharedMaterial == null) continue;
                string matName = r.sharedMaterial.name;
                if (matName.IndexOf("glowin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.name.IndexOf("Light_", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    r.enabled = false;
                }
            }
        }
    }

    private IEnumerator PlaySuspenseRestroomBloodLookRoutine(Act4JohnNpc john)
    {
        isMovementLockedForSequence = true;
        SimpleFPSController.IsExternalCameraAnimationActive = true;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null || john == null) yield break;

        Quaternion startRot = cam.transform.rotation;
        Vector3 stain1 = john.PrimaryBloodStainWorldPos;
        Vector3 stain2 = john.SecondaryBloodStainWorldPos;
        Vector3 body = john.DeadBodyLookWorldPos + Vector3.up * 0.15f;

        Quaternion rotToStain1 = Quaternion.LookRotation((stain1 - cam.transform.position).normalized, Vector3.up);
        Quaternion rotToStain2 = Quaternion.LookRotation((stain2 - cam.transform.position).normalized, Vector3.up);
        Quaternion rotToBody = Quaternion.LookRotation((body - cam.transform.position).normalized, Vector3.up);

        // Slow suspenseful pan down to Blood Stain 1 (1.15s)
        float elapsed = 0f;
        float d1 = 1.15f;
        while (elapsed < d1)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, elapsed / d1);
            cam.transform.rotation = Quaternion.Slerp(startRot, rotToStain1, u);
            yield return null;
        }

        yield return new WaitForSeconds(0.25f);

        // Slow suspenseful pan across to Blood Stain 2 (1.15s)
        elapsed = 0f;
        float d2 = 1.15f;
        while (elapsed < d2)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, elapsed / d2);
            cam.transform.rotation = Quaternion.Slerp(rotToStain1, rotToStain2, u);
            yield return null;
        }

        // Settle on John's body lying in the blood (0.85s)
        elapsed = 0f;
        float d3 = 0.85f;
        while (elapsed < d3)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, elapsed / d3);
            cam.transform.rotation = Quaternion.Slerp(rotToStain2, rotToBody, u);
            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            IsTalkToBillyTaskActive = false;
            HasTalkedToBilly = false;
            IsCleaningTaskActive = false;
            IsInspectShelfTaskActive = false;
            HasInspectedEmptyShelf = false;
            IsTrashTaskActive = false;
            HasDisposedTrash = false;
            IsReturnToCounterTaskActive = false;
            HasReturnedToCounter = false;
            IsStockingTaskActive = false;
            isIntroOverlayActive = false;
            isDialogueVisible = false;
            isMovementLockedForSequence = false;
            Instance = null;
        }
    }

    private IEnumerator StorySequenceRoutine()
    {
        // 0. Opening "At work..." fade-in typing animation & slow fade-out to reveal the game
        currentStage = StoryStage.IntroAtWorkFade;
        isMovementLockedForSequence = true;
        yield return StartCoroutine(PlayOpeningAtWorkIntroRoutine());
        isMovementLockedForSequence = false;

        // 1. Wait 2 seconds after fade-out, then show "Talk to Billy" task + world indicator pointing to BillyNPC
        currentStage = StoryStage.Waiting2sForTalkToBillyTask;
        yield return new WaitForSeconds(2.0f);

        CoworkerNpcBilly billy = CoworkerNpcBilly.EnsureAttachedToSceneNpc();
        if (billy != null)
        {
            HasTalkedToBilly = false;
            IsTalkToBillyTaskActive = true;
            currentStage = StoryStage.TalkToBillyTaskActive;
            PlayTaskChime();

            billy.BeginWaitingForPlayerTalk();
            while (billy.currentState == CoworkerNpcBilly.NpcState.WaitingForPlayerTalk && !billy.HasPlayerTriggeredTalk)
            {
                yield return null;
            }

            // Lock player movement during the handover dialogue with Billy
            currentStage = StoryStage.TalkingWithBilly;
            isMovementLockedForSequence = true;
            yield return StartCoroutine(billy.RunHandoverDialogueRoutine());
            isMovementLockedForSequence = false;

            HasTalkedToBilly = true;
            IsTalkToBillyTaskActive = false;
            PlayTaskChime();

            // Billy walks towards SpawnerNPC and disappears after the dialogue ends
            billy.StartWalkToSpawnerAndDisappear();
        }

        // 4 seconds cooldown after "You: Thanks.", then "You: Alright... another night, another shift.."
        currentStage = StoryStage.Waiting4sAfterBillyConversation;
        yield return new WaitForSeconds(4.0f);

        currentStage = StoryStage.Dialogue1_Opening;
        yield return StartCoroutine(PlayDialogueRoutine("You: Alright... another night, another shift.."));

        // 2. Wait for player to enter store and look at / see the mess
        currentStage = StoryStage.WaitingToEnterStoreAndSeeMess;
        while (!HasPlayerEnteredStoreAndSeenMess())
        {
            yield return null;
        }

        // Small dramatic heartbeat pause before speaking
        isMovementLockedForSequence = true;
        yield return new WaitForSeconds(0.35f);

        // 3. Gas station mess discovered dialogue sequence
        currentStage = StoryStage.Dialogue2_MessDiscovered;
        yield return StartCoroutine(PlayDialogueRoutine("You: Seriously? that billy guy didn't even clean the store?"));

        // 2 seconds cooldown
        currentStage = StoryStage.MessDialogueCooldown2s;
        yield return new WaitForSeconds(2.0f);

        currentStage = StoryStage.Dialogue2_MessDiscovered2;
        yield return StartCoroutine(PlayDialogueRoutine("You: Whatever... I better get started before things pile up."));

        // 1 second cooldown before mop task begins
        currentStage = StoryStage.MessCooldown;
        yield return new WaitForSeconds(1.0f);
        isMovementLockedForSequence = false;

        // 5. Activate Cleaning Task & show top-left UI text
        IsCleaningTaskActive = true;
        currentStage = StoryStage.CleaningTaskActive;
        PlayTaskChime();

        // 6. Monitor floor stains until all stains are cleaned
        while (!FloorStain.AllStainsCleaned)
        {
            yield return null;
        }

        currentStage = StoryStage.CleaningTaskCompleted;
        PlayTaskChime();
        yield return new WaitForSeconds(0.45f);

        // 7. Dialogue 3: All stains cleaned
        currentStage = StoryStage.Dialogue3_CleaningFinished;
        yield return StartCoroutine(PlayDialogueRoutine("You: Ahh, that should do it, time to wait it out I guess.."));

        // Hide completed cleaning HUD while waiting for the next event
        IsCleaningTaskActive = false;

        // 8. Wait 15 seconds, then launch Customer NPC ("Steve") sequence
        currentStage = StoryStage.Waiting15sForCustomer;
        yield return new WaitForSeconds(customerArrivalDelay);

        CustomerNpcSteve steve = CustomerNpcSteve.EnsureAttachedToSceneNpc();
        if (steve != null)
        {
            currentStage = StoryStage.CustomerSteveActive;
            yield return steve.StartCustomerVisit();
        }

        // 9. After Steve leaves and disappears at his spawnpoint, wait 7 seconds
        currentStage = StoryStage.Waiting7sForShelfInspect;
        yield return new WaitForSeconds(shelfInspectDelay);

        // Monologue before inspecting shelf: "You: Hold on.. "
        currentStage = StoryStage.Dialogue_HoldOn;
        yield return StartCoroutine(PlayDialogueRoutine("You: Hold on.. "));

        // 2 seconds cooldown then proceed to inspect task
        currentStage = StoryStage.HoldOnCooldown;
        yield return new WaitForSeconds(taskCooldownDuration);

        // 10. Activate "Task: Inspect the shelf" on the empty canned-food bay
        ShelfRestockTarget.EnsureExists();
        HasInspectedEmptyShelf = false;
        IsInspectShelfTaskActive = true;
        currentStage = StoryStage.InspectShelfTaskActive;
        PlayTaskChime();

        while (!HasInspectedEmptyShelf)
        {
            yield return null;
        }

        // 11. Dialogue 4: Empty best-selling canned food spot inspected
        currentStage = StoryStage.Dialogue4_EmptyShelfInspected;
        yield return StartCoroutine(PlayDialogueRoutine("You: Oh, I guess we ran out of the best selling canned food.."));

        // 12. Wait 2 seconds cooldown, then Dialogue 5: Decide to restock tomorrow
        currentStage = StoryStage.PostInspect2sCooldown;
        yield return new WaitForSeconds(2.0f);

        currentStage = StoryStage.Dialogue5_RestockTomorrow;
        yield return StartCoroutine(PlayDialogueRoutine("You: Nah, I'll restock it tomorrow."));

        // Hide inspect task HUD
        IsInspectShelfTaskActive = false;

        // 13. Wait 4 seconds cooldown, then launch Customer Melia ("MeliaNPC") sequence
        currentStage = StoryStage.Waiting4sForCustomerMelia;
        yield return new WaitForSeconds(4.0f);

        CustomerNpcMelia melia = CustomerNpcMelia.EnsureAttachedToSceneNpc();
        if (melia != null)
        {
            currentStage = StoryStage.CustomerMeliaActive;
            yield return melia.StartCustomerVisit();
        }

        // 14. After Melia leaves the store, wait 7.5 seconds cooldown
        currentStage = StoryStage.Waiting7_5sAfterMeliaLeaves;
        yield return new WaitForSeconds(7.5f);

        // 15. Self-realization dialogues about the stockroom trash
        currentStage = StoryStage.Dialogue6_RealizedTrash1;
        yield return StartCoroutine(PlayDialogueRoutine("You: Wait... I just realized. I haven't taken out the trash from the stockroom yet."));

        currentStage = StoryStage.TrashDialogueCooldown2s;
        yield return new WaitForSeconds(2.0f);

        currentStage = StoryStage.Dialogue7_RealizedTrash2;
        yield return StartCoroutine(PlayDialogueRoutine("You: I was supposed to take the stockroom trash out before my shift ended."));

        currentStage = StoryStage.TrashDialogueCooldown1s;
        yield return new WaitForSeconds(1.0f);

        currentStage = StoryStage.Dialogue8_RealizedTrash3;
        yield return StartCoroutine(PlayDialogueRoutine("You: Ugh... I better get it done before I forget."));

        // 16. Activate Task: Take out the stockroom trash (pointing to Trashbag -> then SM_Dumpster)
        InteractableTrashbag.EnsureAttachedToSceneTrashbag();
        InteractableDumpster.EnsureAttachedToSceneDumpsters();
        HasDisposedTrash = false;
        IsTrashTaskActive = true;
        currentStage = StoryStage.TrashTaskActive;
        PlayTaskChime();

        while (!HasDisposedTrash)
        {
            yield return null;
        }

        currentStage = StoryStage.TrashTaskCompleted;
        PlayTaskChime();
        yield return new WaitForSeconds(0.55f);

        IsTrashTaskActive = false;

        // 17. Immediately after throwing the trash, JunkNPC appears behind the player and freezes player movement
        StrangeNpcJunk junk = StrangeNpcJunk.EnsureAttachedToSceneNpc();
        currentStage = StoryStage.JunkBehindPlayerFreeze;
        isMovementLockedForSequence = true;

        if (junk != null)
        {
            junk.AppearBehindPlayer();
        }

        // Play "???: Hi, behind you" while player is frozen; if player turns around while it's on screen or after, proceed immediately
        yield return StartCoroutine(PlayBehindYouDialogueUntilLookBackRoutine(junk));

        // 18. Player still frozen and must look at the back -> triggers low-quality jumpscare camera shake & returns to normal state
        currentStage = StoryStage.JunkWaitingForLookBack;
        if (junk != null)
        {
            yield return StartCoroutine(junk.WaitForPlayerToLookBackAndJumpscareRoutine());
        }

        // 19. Post-jumpscare dialogue sequence (0.5s cooldown -> You: YOU SCARED ME, WHAT DO YOU WANT? -> 1s -> You: WHO ARE YOU? -> 1s -> Junk: My name is Junk, I just want to buy wet socks. -> 1.5s -> You: You're weird, we don't sell those! -> Junk: Oh ok. -> 1s)
        currentStage = StoryStage.JunkConversationActive;
        if (junk != null)
        {
            yield return StartCoroutine(junk.RunPostJumpscareDialogueRoutine());
        }

        // 20. Unfreeze player movement & JunkNPC walks toward SpawnerNPC to disappear (pushes player aside with "Junk: Move please." if blocked)
        isMovementLockedForSequence = false;
        if (junk != null)
        {
            junk.StartWalkToSpawnerAndDisappear();
        }

        // 21. Wait 3 seconds cooldown, then "You: That was weird.."
        currentStage = StoryStage.Waiting3sAfterJunkWalks;
        yield return new WaitForSeconds(3.0f);
        while (isDialogueVisible)
        {
            yield return null;
        }

        currentStage = StoryStage.Dialogue_ThatWasWeird;
        yield return StartCoroutine(PlayDialogueRoutine("You: That was weird.."));

        // 22. Wait 1 second cooldown, then "You: I should get back to my counter."
        currentStage = StoryStage.Waiting1sBeforeBackToCounter;
        yield return new WaitForSeconds(1.0f);
        while (isDialogueVisible)
        {
            yield return null;
        }

        currentStage = StoryStage.Dialogue_GetBackToCounter;
        yield return StartCoroutine(PlayDialogueRoutine("You: I should get back to my counter."));

        // 23. Activate Task pointing to the counter with world UI indicator ("[ COUNTER ]")
        ResolveCounterReturnSpot();
        HasReturnedToCounter = false;
        IsReturnToCounterTaskActive = true;
        currentStage = StoryStage.ReturnToCounterTaskActive;
        PlayTaskChime();

        while (!IsPlayerAtCounterSpot())
        {
            yield return null;
        }

        // 24. Player reached the counter -> complete task & play "You: Time to wait it out again."
        HasReturnedToCounter = true;
        PlayTaskChime();
        isMovementLockedForSequence = true;

        while (isDialogueVisible)
        {
            yield return null;
        }

        currentStage = StoryStage.Dialogue_TimeToWaitItOutAgain;
        yield return StartCoroutine(PlayDialogueRoutine("You: Time to wait it out again."));

        IsReturnToCounterTaskActive = false;

        // 25. Wait 2 seconds cooldown, then slowly fade screen all to black until fully black
        currentStage = StoryStage.Waiting2sBeforeEndingFade;
        yield return new WaitForSeconds(2.0f);

        currentStage = StoryStage.EndingFadeToBlack;
        yield return StartCoroutine(PlayEndingFadeToBlackRoutine());

        // 26. Ending epilogue text sequence on black screen:
        // "For few hours.. the shift ended..." -> 2s cooldown -> "After you left, you went home and sleep for a whole day..." (2s typing out)
        yield return StartCoroutine(PlayEndingEpilogueRoutine());

        currentStage = StoryStage.ShiftEndedBlackScreen;
        IsStockingTaskActive = false;

        // 27. Transition back to HouseScene for Act 3 (Return Home)
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.currentStoryAct = 3;
            PlayerManager.Instance.TransitionToScene("HouseScene", "Default");
        }
        else
        {
            PlayerPrefs.SetInt("HouseScene_ForceAct3", 1);
            UnityEngine.SceneManagement.SceneManager.LoadScene("HouseScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    public void NotifyEmptyShelfInspected()
    {
        if (!IsInspectShelfTaskActive || HasInspectedEmptyShelf) return;
        HasInspectedEmptyShelf = true;
        PlayTaskChime();
    }

    public void NotifyTrashPickedUp()
    {
        if (!IsTrashTaskActive) return;
        PlayTaskChime();
    }

    public void NotifyDumpsterOpened()
    {
        if (!IsTrashTaskActive) return;
        PlayTaskChime();
    }

    public void NotifyTrashDisposed()
    {
        if (!IsTrashTaskActive || HasDisposedTrash) return;
        HasDisposedTrash = true;
    }

    public IEnumerator PlayCustomDialogueRoutine(string fullText)
    {
        yield return StartCoroutine(PlayDialogueRoutine(fullText));
    }

    public void PlayNonBlockingSideDialogue(string fullText)
    {
        if (isDialogueVisible) return;
        StartCoroutine(PlayDialogueRoutine(fullText));
    }

    public void NotifyCustomerTaskUpdated()
    {
        PlayTaskChime();
    }

    private IEnumerator PlayBehindYouDialogueUntilLookBackRoutine(StrangeNpcJunk junk)
    {
        const string fullText = "???: Hi, behind you";
        skipCurrentDialogueRequested = false;
        targetDialogueText = fullText;
        displayedDialogueText = "";
        dialogueAlpha = 1.0f;
        isTyping = true;
        isDialogueVisible = true;

        for (int i = 0; i < fullText.Length; i++)
        {
            if (skipCurrentDialogueRequested) break;

            displayedDialogueText = fullText.Substring(0, i + 1);
            char c = fullText[i];
            if (c != ' ' && c != ':')
            {
                PlayTypeClick();
            }

            float delay = typingCharDelay;
            if (c == '.' || c == '?' || c == '!') delay = 0.14f;
            else if (c == ',') delay = 0.09f;

            yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(delay, () => skipCurrentDialogueRequested));
        }

        isTyping = false;

        if (!skipCurrentDialogueRequested)
        {
            displayedDialogueText = fullText;
            // Hold & fade out while checking if the player turns around early to face JunkNPC
            float stayElapsed = 0f;
            while (!skipCurrentDialogueRequested && stayElapsed < dialogueStayDuration)
            {
                if (IsPlayerLookingAtJunk(junk))
                {
                    break;
                }
                stayElapsed += Time.deltaTime;
                yield return null;
            }

            float fadeElapsed = 0f;
            while (!skipCurrentDialogueRequested && !IsPlayerLookingAtJunk(junk) && fadeElapsed < dialogueFadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                dialogueAlpha = Mathf.Clamp01(1f - (fadeElapsed / dialogueFadeDuration));
                yield return null;
            }
        }

        dialogueAlpha = 0f;
        isDialogueVisible = false;
        displayedDialogueText = "";
        skipCurrentDialogueRequested = false;
    }

    private bool IsPlayerLookingAtJunk(StrangeNpcJunk junk)
    {
        if (junk == null) return false;
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        Vector3 junkPos = junk.transform.position + Vector3.up * 1.45f;
        Vector3 camToJunkFlat = new Vector3(junkPos.x - cam.transform.position.x, 0f, junkPos.z - cam.transform.position.z).normalized;
        Vector3 camFwdFlat = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z).normalized;
        return Vector3.Dot(camFwdFlat, camToJunkFlat) >= 0.56f;
    }

    private IEnumerator PlayEndingFadeToBlackRoutine()
    {
        isIntroOverlayActive = true;
        introBlackAlpha = 0.0f;
        introTextAlpha = 0.0f;
        introDisplayedText = "";
        isIntroTyping = false;

        float fadeDuration = 3.25f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / fadeDuration);
            introBlackAlpha = Mathf.SmoothStep(0f, 1f, u);
            yield return null;
        }

        introBlackAlpha = 1.0f;
    }

    private IEnumerator PlayEndingEpilogueRoutine()
    {
        // Screen is guaranteed to be 100% black
        isIntroOverlayActive = true;
        introBlackAlpha = 1.0f;
        introTextAlpha = 0.0f;
        introDisplayedText = "";
        isIntroTyping = false;

        // Brief pause in complete darkness before typing begins
        yield return new WaitForSeconds(0.85f);

        // --- Line 1: "For few hours.. the shift ended..." ---
        currentStage = StoryStage.EndingEpilogueLine1;
        const string line1 = "For few hours.. the shift ended...";
        skipCurrentDialogueRequested = false;
        isIntroTyping = true;
        for (int i = 0; i < line1.Length; i++)
        {
            if (skipCurrentDialogueRequested) break;

            introDisplayedText = line1.Substring(0, i + 1);
            introTextAlpha = Mathf.Clamp01((float)(i + 1) / 3f);

            char c = line1[i];
            if (c != ' ')
            {
                PlayTypeClick();
            }

            float delay = (c == '.') ? 0.18f : 0.052f;
            yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(delay, () => skipCurrentDialogueRequested));
        }

        introTextAlpha = 1.0f;
        isIntroTyping = false;
        if (skipCurrentDialogueRequested) introDisplayedText = line1;

        // 2 seconds cooldown
        currentStage = StoryStage.EndingEpilogueCooldown;
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(2.0f, () => skipCurrentDialogueRequested));

        // Smoothly fade out Line 1 before Line 2 starts
        float fadeElapsed = 0f;
        float fadeDuration = 0.65f;
        while (!skipCurrentDialogueRequested && fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            introTextAlpha = Mathf.Clamp01(1f - (fadeElapsed / fadeDuration));
            yield return null;
        }
        introTextAlpha = 0f;
        introDisplayedText = "";
        skipCurrentDialogueRequested = false;

        yield return new WaitForSeconds(0.40f);

        // --- Line 2: "After you left, you went home and sleep for a whole day..." ---
        // 2 seconds typing out (57 chars over ~2.0 seconds)
        currentStage = StoryStage.EndingEpilogueLine2;
        const string line2 = "After you left, you went home and sleep for a whole day...";
        skipCurrentDialogueRequested = false;
        isIntroTyping = true;
        for (int i = 0; i < line2.Length; i++)
        {
            if (skipCurrentDialogueRequested) break;

            introDisplayedText = line2.Substring(0, i + 1);
            introTextAlpha = Mathf.Clamp01((float)(i + 1) / 4f);

            char c = line2[i];
            if (c != ' ')
            {
                PlayTypeClick();
            }

            float delay = (c == '.') ? 0.12f : (c == ',' ? 0.075f : 0.033f);
            yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(delay, () => skipCurrentDialogueRequested));
        }

        introTextAlpha = 1.0f;
        isIntroTyping = false;
        if (skipCurrentDialogueRequested) introDisplayedText = line2;

        // Hold Line 2 for 2.0 seconds cooldown so the player can absorb the message
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(2.0f, () => skipCurrentDialogueRequested));

        // Gently fade out Line 2 into complete black
        fadeElapsed = 0f;
        float finalFadeDuration = 1.25f;
        while (!skipCurrentDialogueRequested && fadeElapsed < finalFadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            introTextAlpha = Mathf.Clamp01(1f - (fadeElapsed / finalFadeDuration));
            yield return null;
        }

        introTextAlpha = 0f;
        introDisplayedText = "";
        introBlackAlpha = 1.0f;
        isIntroOverlayActive = true;
        skipCurrentDialogueRequested = false;
    }

    private void ResolveCounterReturnSpot()
    {
        if (counterReturnSpotTransform != null) return;

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "PlayerNpcInteract", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Player_Npc_Interact", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "PlayerNpcInteractZone", System.StringComparison.OrdinalIgnoreCase))
            {
                counterReturnSpotTransform = t;
                counterReturnSpotCollider = t.GetComponentInChildren<Collider>(true);
                break;
            }
        }

        if (counterReturnSpotTransform == null && CustomerNpcSteve.Instance != null && CustomerNpcSteve.Instance.PlayerCashierSpotTransform != null)
        {
            counterReturnSpotTransform = CustomerNpcSteve.Instance.PlayerCashierSpotTransform;
            counterReturnSpotCollider = counterReturnSpotTransform.GetComponentInChildren<Collider>(true);
        }
    }

    private Vector3 GetCounterSpotWorldPosition()
    {
        if (counterReturnSpotTransform == null)
        {
            ResolveCounterReturnSpot();
        }
        if (counterReturnSpotTransform != null)
        {
            return counterReturnSpotTransform.position;
        }
        return new Vector3(2.49f, 0.17f, -7.57f);
    }

    private bool IsPlayerAtCounterSpot()
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        CharacterController cc = cam.GetComponentInParent<CharacterController>();
        Vector3 pPos = cc != null ? cc.transform.position : cam.transform.position;

        if (counterReturnSpotTransform == null)
        {
            ResolveCounterReturnSpot();
        }

        if (counterReturnSpotCollider != null && counterReturnSpotCollider.enabled)
        {
            Bounds b = counterReturnSpotCollider.bounds;
            float pad = 0.35f;
            if (pPos.x >= b.min.x - pad && pPos.x <= b.max.x + pad &&
                pPos.z >= b.min.z - pad && pPos.z <= b.max.z + pad &&
                Mathf.Abs(pPos.y - b.center.y) <= 2.2f)
            {
                return true;
            }
        }

        Vector3 spotPos = GetCounterSpotWorldPosition();
        float horizDist = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(spotPos.x, spotPos.z));
        return horizDist <= 1.65f && Mathf.Abs(pPos.y - spotPos.y) <= 2.2f;
    }

    private IEnumerator PlayOpeningAtWorkIntroRoutine()
    {
        const string introFullText = "At work...";
        isIntroOverlayActive = true;
        introBlackAlpha = 1.0f;
        introTextAlpha = 0.0f;
        introDisplayedText = "";
        isIntroTyping = false;

        // Initial brief dark pause before typing starts
        yield return new WaitForSeconds(0.45f);

        // Fade in "At work..." text container while typing out character by character
        skipCurrentDialogueRequested = false;
        isIntroTyping = true;
        for (int i = 0; i < introFullText.Length; i++)
        {
            if (skipCurrentDialogueRequested) break;

            introDisplayedText = introFullText.Substring(0, i + 1);
            introTextAlpha = Mathf.Clamp01((float)(i + 1) / 4f);

            char c = introFullText[i];
            if (c != ' ')
            {
                PlayTypeClick();
            }

            float charWait = (c == '.') ? 0.22f : 0.095f;
            yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(charWait, () => skipCurrentDialogueRequested));
        }

        introTextAlpha = 1.0f;
        isIntroTyping = false;
        if (skipCurrentDialogueRequested) introDisplayedText = introFullText;

        // Hold "At work..." on screen
        yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(1.30f, () => skipCurrentDialogueRequested));

        // Fade out the "At work..." text
        float textFadeDuration = 0.85f;
        float elapsedTextFade = 0f;
        while (!skipCurrentDialogueRequested && elapsedTextFade < textFadeDuration)
        {
            elapsedTextFade += Time.deltaTime;
            introTextAlpha = Mathf.Clamp01(1f - (elapsedTextFade / textFadeDuration));
            yield return null;
        }
        introTextAlpha = 0f;
        introDisplayedText = "";
        skipCurrentDialogueRequested = false;

        // Slowly fade out the black screen to reveal the game
        float screenFadeDuration = 2.25f;
        float elapsedScreenFade = 0f;
        while (elapsedScreenFade < screenFadeDuration)
        {
            elapsedScreenFade += Time.deltaTime;
            float u = Mathf.Clamp01(elapsedScreenFade / screenFadeDuration);
            introBlackAlpha = Mathf.SmoothStep(1f, 0f, u);
            yield return null;
        }

        introBlackAlpha = 0f;
        introDisplayedText = "";
        isIntroOverlayActive = false;
    }

    private IEnumerator PlayDialogueRoutine(string fullText)
    {
        skipCurrentDialogueRequested = false;
        targetDialogueText = fullText;
        displayedDialogueText = "";
        dialogueAlpha = 1.0f;
        isTyping = true;
        isDialogueVisible = true;

        // Type out character by character
        for (int i = 0; i < fullText.Length; i++)
        {
            if (skipCurrentDialogueRequested) break;

            displayedDialogueText = fullText.Substring(0, i + 1);

            char c = fullText[i];
            if (c != ' ' && c != ':')
            {
                PlayTypeClick();
            }

            // Natural dramatic pauses on punctuation
            float delay = typingCharDelay;
            if (c == '.' || c == '?' || c == '!')
            {
                delay = 0.16f;
            }
            else if (c == ',')
            {
                delay = 0.10f;
            }

            yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(delay, () => skipCurrentDialogueRequested));
        }

        isTyping = false;

        if (!skipCurrentDialogueRequested)
        {
            displayedDialogueText = fullText;
            // Stay put for the exact requested duration
            yield return StartCoroutine(HorrorHudStyle.WaitSkippableSeconds(dialogueStayDuration, () => skipCurrentDialogueRequested));
        }

        // Dramatically slowly fade out
        float elapsedFade = 0f;
        while (!skipCurrentDialogueRequested && elapsedFade < dialogueFadeDuration)
        {
            elapsedFade += Time.deltaTime;
            dialogueAlpha = Mathf.Clamp01(1f - (elapsedFade / dialogueFadeDuration));
            yield return null;
        }

        dialogueAlpha = 0f;
        isDialogueVisible = false;
        displayedDialogueText = "";
        skipCurrentDialogueRequested = false;
    }

    private bool HasPlayerEnteredStoreAndSeenMess()
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        Vector3 pPos = cam.transform.position;

        // Player must be inside the store building bounds
        bool isInsideStore = (pPos.x >= storeMinX && pPos.x <= storeMaxX &&
                              pPos.z >= storeMinZ && pPos.z <= storeMaxZ &&
                              pPos.y >= storeMinY && pPos.y <= storeMaxY);

        if (!isInsideStore) return false;

        // Check if any floor stain is in player's field of vision
        if (FloorStain.AllStains != null && FloorStain.AllStains.Count > 0)
        {
            for (int i = 0; i < FloorStain.AllStains.Count; i++)
            {
                FloorStain stain = FloorStain.AllStains[i];
                if (stain == null || stain.IsCleaned) continue;

                Vector3 stainPos = stain.transform.position;
                float dist = Vector3.Distance(pPos, stainPos);
                if (dist > 11.5f) continue;

                Vector3 toStain = (stainPos - pPos).normalized;
                float dot = Vector3.Dot(cam.transform.forward, toStain);
                if (dot < 0.35f) continue; // In front cone of vision

                Vector3 vp = cam.WorldToViewportPoint(stainPos);
                if (vp.z > 0.15f && vp.x >= 0.05f && vp.x <= 0.95f && vp.y >= 0.05f && vp.y <= 0.95f)
                {
                    // Direct line of sight
                    if (!Physics.Linecast(pPos, stainPos + Vector3.up * 0.12f, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) ||
                        hit.collider.GetComponentInParent<FloorStain>() != null)
                    {
                        return true;
                    }
                }
            }
        }

        // Fallback once player walks past the front doorway into the store interior
        if (pPos.x > -0.25f)
        {
            return true;
        }

        return false;
    }

    private void InitTextures()
    {
        if (pixelTexture == null)
        {
            pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixelTexture.SetPixel(0, 0, Color.white);
            pixelTexture.Apply();
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

        if (dialogueStyle == null)
        {
            dialogueStyle = HorrorHudStyle.CreateDialogueStyle();
            dialogueShadowStyle = new GUIStyle(dialogueStyle);

            taskHeaderStyle = HorrorHudStyle.CreateTaskHeaderStyle();
            taskHeaderShadowStyle = new GUIStyle(taskHeaderStyle);

            taskSubStyle = HorrorHudStyle.CreateTaskSubStyle(taskHeaderStyle);
            taskSubShadowStyle = new GUIStyle(taskSubStyle);

            indicatorStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            if (retroFont != null) indicatorStyle.font = retroFont;
            indicatorShadowStyle = new GUIStyle(indicatorStyle);

            introTitleStyle = HorrorHudStyle.CreateTitleStyle();
            introTitleShadowStyle = new GUIStyle(introTitleStyle);
        }
    }

    private void OnGUI()
    {
        if (HorrorMainMenuController.IsMainMenuActive) return;
        if (IsPlayerInHouseScene()) return;
        if (Event.current.type != EventType.Repaint) return;
        if (InteractableCashierPC.IsPosScreenOpen) return;

        SetupStyles();
        InitTextures();

        // 1. Draw Minimalist Retro Dialogue (Lower Center)
        if (isDialogueVisible && dialogueAlpha > 0.005f)
        {
            DrawDialogueBox();
        }

        // 2. Draw Top-Left Small Task UI
        if (!string.IsNullOrEmpty(act4TaskHeaderOverride) ||
            IsTalkToBillyTaskActive ||
            IsCleaningTaskActive ||
            IsInspectShelfTaskActive ||
            IsTrashTaskActive ||
            IsReturnToCounterTaskActive ||
            IsStockingTaskActive ||
            ShouldDisplayCustomerTaskHUD() ||
            ShouldDisplayMeliaTaskHUD())
        {
            DrawTopLeftTaskHUD();
        }

        // 3. Draw subtle world indicators (BillyNPC, Cashier Register Spot, Cashier PC, Trashbag in Stockroom, SM_Dumpster, Counter Return Spot, or Act 4 Umbrella Spot)
        if (!isDialogueVisible)
        {
            if (Act4UmbrellaStandSpot.Instance != null && Act4UmbrellaStandSpot.Instance.ShouldShowIndicator)
            {
                DrawWorldTargetIndicator(Act4UmbrellaStandSpot.Instance.IndicatorWorldPosition, "[ UMBRELLA SPOT ]");
            }
            else if (act4ShowCounterStandIndicator && !IsPlayerAtCounterSpot())
            {
                DrawWorldTargetIndicator(GetCounterSpotWorldPosition() + Vector3.up * 0.20f, "[ COUNTER ]");
            }
            else if (Act4JohnNpc.Instance != null &&
                     Act4JohnNpc.Instance.currentState == Act4JohnNpc.JohnState.WaitingForCashierInteract &&
                     !IsPlayerAtCounterSpot())
            {
                DrawWorldTargetIndicator(GetCounterSpotWorldPosition() + Vector3.up * 0.20f, "[ CASHIER AREA ]");
            }
            else if (IsTalkToBillyTaskActive && !HasTalkedToBilly && CoworkerNpcBilly.Instance != null && CoworkerNpcBilly.Instance.ShouldShowBillyIndicator)
            {
                DrawWorldTargetIndicator(CoworkerNpcBilly.Instance.IndicatorWorldPosition, "[ BILLY ]");
            }
            else if (IsReturnToCounterTaskActive && !HasReturnedToCounter)
            {
                DrawWorldTargetIndicator(GetCounterSpotWorldPosition() + Vector3.up * 0.20f, "[ COUNTER ]");
            }
            else if (CustomerNpcSteve.Instance != null && CustomerNpcSteve.Instance.ShouldShowCashierIndicator && CustomerNpcSteve.Instance.PlayerCashierSpotTransform != null)
            {
                DrawWorldTargetIndicator(CustomerNpcSteve.Instance.PlayerCashierSpotTransform.position + Vector3.up * 0.20f, "[ CASHIER AREA ]");
            }
            else if (CustomerNpcMelia.Instance != null && CustomerNpcMelia.Instance.ShouldShowCashierIndicator && CustomerNpcMelia.Instance.PlayerCashierSpotTransform != null)
            {
                DrawWorldTargetIndicator(CustomerNpcMelia.Instance.PlayerCashierSpotTransform.position + Vector3.up * 0.20f, "[ CASHIER AREA ]");
            }
            else if (InteractableCashierPC.Instance != null && InteractableCashierPC.Instance.IsCheckoutReady && InteractableCashierPC.Instance.IsPlayerInsideCashierZone)
            {
                DrawWorldTargetIndicator(InteractableCashierPC.Instance.IndicatorWorldPosition, "[ CASHIER PC ]");
            }
            else if (IsTrashTaskActive && !HasDisposedTrash)
            {
                if (!InteractableTrashbag.IsTrashPickedUp && InteractableTrashbag.Instance != null)
                {
                    DrawWorldTargetIndicator(InteractableTrashbag.Instance.IndicatorWorldPosition, "[ TRASHBAG ]");
                }
                else if (InteractableTrashbag.IsTrashPickedUp && InteractableDumpster.PrimaryInstance != null)
                {
                    DrawWorldTargetIndicator(InteractableDumpster.PrimaryInstance.IndicatorWorldPosition, "[ DUMPSTER ]");
                }
            }
        }

        // 4. Draw Opening "At work..." Full-Screen Black Fade & Typewriter Overlay on top of all UI
        if (isIntroOverlayActive && (introBlackAlpha > 0.001f || introTextAlpha > 0.001f))
        {
            DrawIntroOverlay();
        }
    }

    private void DrawIntroOverlay()
    {
        int prevDepth = GUI.depth;
        GUI.depth = -1000;

        float sw = Screen.width;
        float sh = Screen.height;

        if (introBlackAlpha > 0.001f)
        {
            GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(introBlackAlpha));
            GUI.DrawTexture(new Rect(0f, 0f, sw, sh), pixelTexture);
            GUI.color = Color.white;
        }

        if (introTextAlpha > 0.001f && !string.IsNullOrEmpty(introDisplayedText))
        {
            HorrorHudStyle.DrawCenteredTitle(introDisplayedText, isIntroTyping, introTextAlpha, introTitleStyle, introTitleShadowStyle);
        }

        GUI.depth = prevDepth;
    }

    private bool ShouldDisplayCustomerTaskHUD()
    {
        if (currentStage != StoryStage.CustomerSteveActive || CustomerNpcSteve.Instance == null)
        {
            return false;
        }

        var st = CustomerNpcSteve.Instance.currentState;
        return st == CustomerNpcSteve.NpcState.WaitingForPlayerTalkAtShelf1 ||
               st == CustomerNpcSteve.NpcState.WaitingForPlayerCashierTalk ||
               st == CustomerNpcSteve.NpcState.TalkingAtCounter1 ||
               st == CustomerNpcSteve.NpcState.Waiting2sBeforeScan ||
               st == CustomerNpcSteve.NpcState.WaitingForPlayerScan ||
               st == CustomerNpcSteve.NpcState.PostScanCheckoutSequence ||
               st == CustomerNpcSteve.NpcState.WaitingForPlayerCashierPCCheckout ||
               st == CustomerNpcSteve.NpcState.WalkingBackToSpawn;
    }

    private bool ShouldDisplayMeliaTaskHUD()
    {
        if (currentStage != StoryStage.CustomerMeliaActive || CustomerNpcMelia.Instance == null)
        {
            return false;
        }

        var st = CustomerNpcMelia.Instance.currentState;
        return st == CustomerNpcMelia.NpcState.WaitingForPlayerCashierTalk ||
               st == CustomerNpcMelia.NpcState.TalkingAndPlacingItems ||
               st == CustomerNpcMelia.NpcState.WaitingForPlayerScanTwoItems ||
               st == CustomerNpcMelia.NpcState.PostScanCheckoutSequence ||
               st == CustomerNpcMelia.NpcState.WaitingForPlayerCashierPCCheckout ||
               st == CustomerNpcMelia.NpcState.WalkingBackToSpawn;
    }

    private void DrawDialogueBox()
    {
        HorrorHudStyle.DrawDialogueBox(
            pixelTexture,
            displayedDialogueText,
            isTyping,
            dialogueAlpha,
            dialogueStyle,
            dialogueShadowStyle);
    }

    private void DrawTopLeftTaskHUD()
    {
        float x = HorrorHudStyle.TaskStartX;
        float y = HorrorHudStyle.TaskStartY;

        string headerText = "";
        string subText = "";
        Color headerColor = HorrorHudStyle.ActiveTaskColor;
        Color subColor = HorrorHudStyle.TaskSubColor;

        if (!string.IsNullOrEmpty(act4TaskHeaderOverride))
        {
            headerText = act4TaskHeaderOverride;
            subText = act4TaskSubOverride;
            if (act4TaskCompletedFlag)
            {
                headerColor = HorrorHudStyle.CompletedTaskColor;
                subColor = HorrorHudStyle.CompletedTaskSubColor;
            }
        }
        else if (ShouldDisplayCustomerTaskHUD())
        {
            var steve = CustomerNpcSteve.Instance;
            var st = steve.currentState;

            if (st == CustomerNpcSteve.NpcState.WaitingForPlayerTalkAtShelf1)
            {
                headerText = "Task: Check on the customer";
                subText = "- Observe / talk to the customer";
            }
            else if (st == CustomerNpcSteve.NpcState.WalkingBackToSpawn)
            {
                headerText = "Task: Assist the customer [COMPLETED]";
                subText = "- Customer assisted";
                headerColor = HorrorHudStyle.CompletedTaskColor;
                subColor = HorrorHudStyle.CompletedTaskSubColor;
            }
            else
            {
                headerText = "Task: Assist the customer";
                if (st == CustomerNpcSteve.NpcState.WaitingForPlayerCashierTalk)
                {
                    subText = steve.IsPlayerInsideCashierZone
                        ? "- Talk to the customer"
                        : "- Go to Cashier area first";
                }
                else if (st == CustomerNpcSteve.NpcState.WaitingForPlayerScan)
                {
                    subText = steve.IsPlayerInsideCashierZone
                        ? "- Scan the item on the counter"
                        : "- Go to Cashier area first";
                }
                else if (st == CustomerNpcSteve.NpcState.WaitingForPlayerCashierPCCheckout)
                {
                    subText = steve.IsPlayerInsideCashierZone
                        ? "- Checkout on Cashier PC (Press E)"
                        : "- Go to Cashier area first";
                }
                else
                {
                    subText = "- Complete checkout";
                }
            }
        }
        else if (ShouldDisplayMeliaTaskHUD())
        {
            var melia = CustomerNpcMelia.Instance;
            var st = melia.currentState;

            if (st == CustomerNpcMelia.NpcState.WalkingBackToSpawn)
            {
                headerText = "Task: Assist the Customer [COMPLETED]";
                subText = "- Customer assisted";
                headerColor = HorrorHudStyle.CompletedTaskColor;
                subColor = HorrorHudStyle.CompletedTaskSubColor;
            }
            else if (st == CustomerNpcMelia.NpcState.WaitingForPlayerScanTwoItems)
            {
                headerText = "Task: Scan the two items";
                subText = melia.IsPlayerInsideCashierZone
                    ? $"- Scan the items on the counter ({melia.ScannedItemsCount}/{melia.TotalItemsToScan})"
                    : "- Go to Cashier area first";
            }
            else if (st == CustomerNpcMelia.NpcState.WaitingForPlayerCashierPCCheckout)
            {
                headerText = "Task: Assist the Customer";
                subText = melia.IsPlayerInsideCashierZone
                    ? "- Checkout on Cashier PC (Press E)"
                    : "- Go to Cashier area first";
            }
            else
            {
                headerText = "Task: Assist the Customer";
                if (st == CustomerNpcMelia.NpcState.WaitingForPlayerCashierTalk)
                {
                    subText = melia.IsPlayerInsideCashierZone
                        ? "- Talk to the customer (Press E)"
                        : "- Go to Cashier area first";
                }
                else
                {
                    subText = "- Complete checkout";
                }
            }
        }
        else if (IsTrashTaskActive)
        {
            if (HasDisposedTrash)
            {
                headerText = "Task: Take out the trash [COMPLETED]";
                subText = "- Stockroom trash thrown in dumpster";
                headerColor = HorrorHudStyle.CompletedTaskColor;
                subColor = HorrorHudStyle.CompletedTaskSubColor;
            }
            else if (!InteractableTrashbag.IsTrashPickedUp)
            {
                headerText = "Task: Take out the trash";
                subText = "- Pick up the trashbag in the stockroom";
            }
            else if (!InteractableDumpster.IsAnyDumpsterOpen)
            {
                headerText = "Task: Take out the trash";
                subText = "- Open the dumpster outside (requires empty hands: Press 2/3)";
            }
            else
            {
                headerText = "Task: Take out the trash";
                subText = "- Put the trashbag inside the open dumpster";
            }
        }
        else if (IsReturnToCounterTaskActive)
        {
            if (HasReturnedToCounter)
            {
                headerText = "Task: Get back to the counter [COMPLETED]";
                subText = "- Back at the counter";
                headerColor = HorrorHudStyle.CompletedTaskColor;
                subColor = HorrorHudStyle.CompletedTaskSubColor;
            }
            else
            {
                headerText = "Task: Get back to the counter";
                subText = "- Return to the cashier counter";
            }
        }
        else if (IsTalkToBillyTaskActive)
        {
            headerText = "Task: Talk to Billy";
            subText = "- Talk to Billy";
        }
        else if (IsInspectShelfTaskActive)
        {
            if (HasInspectedEmptyShelf)
            {
                headerText = "Task: Inspect the shelf [COMPLETED]";
                subText = "- Empty shelf inspected";
                headerColor = HorrorHudStyle.CompletedTaskColor;
                subColor = HorrorHudStyle.CompletedTaskSubColor;
            }
            else
            {
                headerText = "Task: Inspect the shelf";
                subText = "- Check the empty spot on the shelf";
            }
        }
        else if (!IsStockingTaskActive)
        {
            // Cleaning Phase
            int total = Mathf.Max(3, FloorStain.TotalStainsCount);
            int cleaned = FloorStain.CleanedStainsCount;

            if (FloorStain.AllStainsCleaned)
            {
                headerText = "Task: Clean the place [COMPLETED]";
                subText = "- All floor stains cleaned";
                headerColor = HorrorHudStyle.CompletedTaskColor;
                subColor = HorrorHudStyle.CompletedTaskSubColor;
            }
            else
            {
                headerText = "Task: Clean the place";
                subText = $"- Clean floor stains ({cleaned}/{total})";
            }
        }
        else
        {
            // Stocking Phase
            ShelfRestockTarget shelf = ShelfRestockTarget.Instance;
            int stocked = shelf != null ? shelf.stockedCount : 0;
            int goal = shelf != null ? shelf.goalCount : 12;

            if (shelf != null && shelf.IsFullyStocked)
            {
                headerText = "Task: Restock shelves [COMPLETED]";
                subText = "- Shelf restock complete!";
                headerColor = HorrorHudStyle.CompletedTaskColor;
                subColor = HorrorHudStyle.CompletedTaskSubColor;
            }
            else
            {
                headerText = "Task: Restock shelves";
                subText = $"- Stock cans on shelf ({stocked}/{goal})";
            }
        }

        // 1. Header (Task: ...)
        HorrorHudStyle.DrawTaskLine(x, y, headerText, headerColor, taskHeaderStyle, taskHeaderShadowStyle);

        // 2. Sub-objective
        HorrorHudStyle.DrawTaskLine(x + 4f, y + 22f, subText, subColor, taskSubStyle, taskSubShadowStyle);
    }

    private void DrawWorldTargetIndicator(Vector3 targetWorld, string label)
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return;

        Vector3 sp = cam.WorldToScreenPoint(targetWorld);

        float sw = Screen.width;
        float sh = Screen.height;
        float pad = 54f;

        float gx;
        float gy;

        if (sp.z > 0.05f)
        {
            gx = Mathf.Clamp(sp.x, pad, sw - pad);
            gy = Mathf.Clamp(sh - sp.y, pad, sh - pad - 40f);
        }
        else
        {
            Vector3 localDir = cam.transform.InverseTransformPoint(targetWorld);
            gx = localDir.x >= 0f ? (sw - pad) : pad;
            gy = sh * 0.52f;
        }

        float pulse = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 4.2f);
        float bob = Mathf.Sin(Time.unscaledTime * 3.2f) * 3.5f;
        gy += bob;

        Color markerCol = new Color(0.98f, 0.88f, 0.62f, pulse);
        Color shadowCol = new Color(0.02f, 0.02f, 0.02f, 0.92f * pulse);

        float sz = 10f;
        GUI.color = shadowCol;
        GUI.DrawTexture(new Rect(gx - sz * 0.5f + 1f, gy - sz * 0.5f + 1f, sz, sz), pixelTexture);
        GUI.color = markerCol;
        GUI.DrawTexture(new Rect(gx - sz * 0.5f, gy - sz * 0.5f, sz, 2f), pixelTexture);
        GUI.DrawTexture(new Rect(gx - sz * 0.5f, gy + sz * 0.5f - 2f, sz, 2f), pixelTexture);
        GUI.DrawTexture(new Rect(gx - sz * 0.5f, gy - sz * 0.5f, 2f, sz), pixelTexture);
        GUI.DrawTexture(new Rect(gx + sz * 0.5f - 2f, gy - sz * 0.5f, 2f, sz), pixelTexture);
        GUI.color = Color.white;

        Rect lRect = new Rect(gx - 110f, gy - 24f, 220f, 18f);
        indicatorShadowStyle.normal.textColor = shadowCol;
        indicatorStyle.normal.textColor = markerCol;

        GUI.Label(new Rect(lRect.x + 1f, lRect.y + 1f, lRect.width, lRect.height), label, indicatorShadowStyle);
        GUI.Label(lRect, label, indicatorStyle);
    }

    private void PlayTypeClick()
    {
        if (audioSource != null && typeClickClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.15f);
            audioSource.PlayOneShot(typeClickClip, 0.16f);
        }
    }

    private void PlayTaskChime()
    {
        if (audioSource != null && taskChimeClip != null)
        {
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(taskChimeClip, 0.65f);
        }
    }

    private void GenerateProceduralAudio()
    {
        // 1. Crisp retro typewriter key click
        int sampleRate = 44100;
        int clickCount = (int)(sampleRate * 0.024f);
        float[] clickSamples = new float[clickCount];

        for (int i = 0; i < clickCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 220f);
            float noise = (Random.value * 2f - 1f) * 0.6f;
            float tone = Mathf.Sin(2f * Mathf.PI * 1850f * t) * 0.4f;
            clickSamples[i] = (noise + tone) * env;
        }
        typeClickClip = AudioClip.Create("TypewriterTick", clickCount, 1, sampleRate, false);
        typeClickClip.SetData(clickSamples, 0);

        // 2. Atmospheric subtle horror task update chime
        int chimeCount = (int)(sampleRate * 0.55f);
        float[] chimeSamples = new float[chimeCount];

        for (int i = 0; i < chimeCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 5.5f);
            float f1 = 587.33f; // D5
            float f2 = 880.00f; // A5
            float tone = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.55f + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.35f;
            chimeSamples[i] = tone * env * 0.4f;
        }
        taskChimeClip = AudioClip.Create("HorrorTaskChime", chimeCount, 1, sampleRate, false);
        taskChimeClip.SetData(chimeSamples, 0);
    }
}

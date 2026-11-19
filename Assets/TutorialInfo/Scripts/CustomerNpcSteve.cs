using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls "Steve" (NPC1): PS1-style procedural walking/browsing/hand animations,
/// obstacle-free navigation into the gas station store, Shelf1 browsing & talk interaction,
/// Double Refrigerator door opening (with anti-bump step-back), Coca-Cola can pickup,
/// counter placement at InvisibleNPCItem, cashier checkout via PlayerNpcInteract, scanning,
/// $2 payment exchange, and departure back to his spawnpoint.
/// </summary>
public class CustomerNpcSteve : MonoBehaviour, IInteractable
{
    public static CustomerNpcSteve Instance { get; private set; }

    public enum NpcState
    {
        HiddenWaiting,
        WalkingToShelf1,
        BrowsingShelf1Initial5s,
        WaitingForPlayerTalkAtShelf1,
        TalkingAtShelf1,
        BrowsingShelf1Post7s,
        WalkingToFridge,
        OpeningFridgeDoor,
        BrowsingFridge7s,
        PickingUpCokeCan,
        ClosingFridgeDoor,
        WalkingToCounter,
        PlacingItemOnCounter,
        Waiting2sBeforeAssistTask,
        WaitingForPlayerCashierTalk,
        TalkingAtCounter1,
        Waiting2sBeforeScan,
        WaitingForPlayerScan,
        PostScanCheckoutSequence,
        WaitingForPlayerCashierPCCheckout,
        WalkingBackToSpawn,
        DepartedHidden
    }

    [Header("State & Settings")]
    public NpcState currentState = NpcState.HiddenWaiting;
    public float slowWalkSpeed = 1.26f;
    public float turnSpeed = 5.5f;
    [Tooltip("PS1 stepped animation frame rate for authentic retro low-poly feel")]
    public float ps1AnimFps = 12f;

    // Public read-only helpers for UI, Sliding Doors, and Reticle
    public bool IsNpcActiveInWorld => isNpcVisible && gameObject.activeInHierarchy;
    public bool IsInteractingWithFridge =>
        currentState == NpcState.OpeningFridgeDoor ||
        currentState == NpcState.BrowsingFridge7s ||
        currentState == NpcState.PickingUpCokeCan ||
        currentState == NpcState.ClosingFridgeDoor;

    public static bool IsNpcUsingFridge(InteractableFridgeDoor door)
    {
        if (door != null && door.isLockedByNpc) return true;
        if (Instance == null || !Instance.IsNpcActiveInWorld) return false;
        if (Instance.IsInteractingWithFridge) return true;

        if (Instance.currentState == NpcState.WalkingToFridge && Instance.targetFridgeTransform != null)
        {
            float dist = Vector3.Distance(Instance.transform.position, Instance.targetFridgeTransform.position);
            if (dist <= 2.25f) return true;
        }
        return false;
    }

    private void SetFridgeDoorsLockedByNpc(bool locked)
    {
        if (targetFridgeDoor != null)
        {
            targetFridgeDoor.isLockedByNpc = locked;
        }

        if (targetFridgeTransform != null)
        {
            InteractableFridgeDoor[] doors = targetFridgeTransform.GetComponentsInChildren<InteractableFridgeDoor>(true);
            if (doors != null)
            {
                for (int i = 0; i < doors.Length; i++)
                {
                    if (doors[i] != null) doors[i].isLockedByNpc = locked;
                }
            }
        }
    }

    public bool ShouldShowCashierIndicator =>
        (currentState == NpcState.WaitingForPlayerCashierTalk ||
         currentState == NpcState.WaitingForPlayerScan ||
         currentState == NpcState.WaitingForPlayerCashierPCCheckout) &&
        !IsPlayerInsideCashierZone;
    public Transform PlayerCashierSpotTransform => playerNpcInteractTransform;

    public bool IsPlayerInsideCashierZone
    {
        get
        {
            if (playerNpcInteractTransform == null) return false;
            Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
            if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
            if (cam == null) return false;

            Vector3 pPos = cam.transform.position;
            CharacterController cc = cam.GetComponentInParent<CharacterController>();
            if (cc != null) pPos = cc.transform.position;

            Collider spotCol = playerNpcInteractTransform.GetComponent<Collider>();
            if (spotCol != null)
            {
                Bounds b = spotCol.bounds;
                float dx = Mathf.Abs(pPos.x - b.center.x);
                float dz = Mathf.Abs(pPos.z - b.center.z);
                if (dx <= b.extents.x + 0.16f && dz <= b.extents.z + 0.16f)
                {
                    return true;
                }
            }

            Vector3 spotPos = playerNpcInteractTransform.position;
            Vector2 flatPlayer = new Vector2(pPos.x, pPos.z);
            Vector2 flatSpot = new Vector2(spotPos.x, spotPos.z);
            return Vector2.Distance(flatPlayer, flatSpot) <= 0.56f;
        }
    }

    // Scene references
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Transform shelf1Transform;
    private Transform targetFridgeTransform;
    private InteractableFridgeDoor targetFridgeDoor;
    private Transform fridgeDoorHandleTransform;
    private AutomaticSlidingDoors slidingDoors;
    private Transform invisibleNpcItemTransform;
    private Transform playerNpcInteractTransform;

    // Coca-Cola can prop & scannable component
    private GameObject cocaColaCanObj;
    private ScannableCounterCan scannableCan;
    private bool isCarryingCan = false;

    // Act 4 (Shift 2) Umbrella & MamaChip support
    public bool isAct4Mode = false;
    public bool HasAct4SteveStartedLeaving { get; private set; } = false;
    private GameObject steveUmbrellaObj;
    private Transform steveUmbrellaCanopyPivot;
    private bool isCarryingUmbrella = false;
    private bool isUmbrellaOpen = false;
    private Transform pillarFrontCenterTransform;
    private Transform chipShelveTransform;
    private GameObject sceneMamaChipTemplate;

    // Renderers & collider on Steve
    private Renderer[] npcRenderers;
    private CapsuleCollider npcCapsule;
    private bool isNpcVisible = false;

    // Rig bones & bind pose
    private Transform pelvis, spine, chest, neck, head;
    private Transform shoulderL, upperArmL, forearmL, handL;
    private Transform shoulderR, upperArmR, forearmR, handR;
    private Transform thighL, shinL, footL, footLEnd;
    private Transform thighR, shinR, footR, footREnd;

    private readonly Dictionary<Transform, Quaternion> bindLocalRots = new Dictionary<Transform, Quaternion>();
    private Vector3 pelvisBindLocalPos;
    private Vector3 modelForwardLocal = Vector3.forward;
    private Vector3 modelRightLocal = Vector3.right;
    private float modelYawCorrectionDeg = 0f;
    private float footLSoleOffset = 0.09f;
    private float footLEndSoleOffset = 0.02f;
    private float footRSoleOffset = 0.09f;
    private float footREndSoleOffset = 0.02f;
    private bool rigInitialized = false;

    // Procedural animation runtime state
    private bool isCurrentlyWalking = false;
    private float walkCycleTimer = 0f;
    private float idleTimer = 0f;
    private float footstepTimer = 0f;

    // Procedural arm reach targets (blended in LateUpdate)
    private float rightArmReachWeight = 0f;
    private Vector3 rightArmWorldTarget = Vector3.zero;
    private float leftArmReachWeight = 0f;
    private Vector3 leftArmWorldTarget = Vector3.zero;
    private bool lookAtPlayerHead = false;

    // Audio
    private AudioSource npcAudioSource;
    private AudioClip footstepClip;
    private AudioClip canPlaceClip;
    private AudioClip scanBeepClip;
    private AudioClip cashPayClip;
    private AudioClip fridgeSealClip;

    // Sequence synchronization flags
    private bool shelfTalkTriggered = false;
    private bool counterTalkTriggered = false;
    private bool itemScannedTriggered = false;

    public static CustomerNpcSteve EnsureAttachedToSceneNpc()
    {
        if (Instance != null) return Instance;

        CustomerNpcSteve existing = Object.FindAnyObjectByType<CustomerNpcSteve>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        // Find NPC1 / Steve placed by user in the scene
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        GameObject npcGo = null;

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "NPC1", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Steve", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "SteveNPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Steve_NPC", System.StringComparison.OrdinalIgnoreCase))
            {
                npcGo = t.gameObject;
                break;
            }
        }

        if (npcGo == null)
        {
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t == null) continue;
                string nm = t.name.Trim();
                if (nm.IndexOf("NPC1", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    nm.IndexOf("Steve", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (t.GetComponentInChildren<SkinnedMeshRenderer>(true) != null ||
                        t.GetComponentInChildren<Animator>(true) != null)
                    {
                        npcGo = t.gameObject;
                        break;
                    }
                }
            }
        }

        if (npcGo != null)
        {
            npcGo.SetActive(true);
            Instance = npcGo.AddComponent<CustomerNpcSteve>();
            return Instance;
        }

        return null;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // Disable idle Animator without controller so procedural PS1 bone animation has full control
        Animator anim = GetComponentInChildren<Animator>(true);
        if (anim != null && anim.runtimeAnimatorController == null)
        {
            anim.enabled = false;
        }

        npcRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < npcRenderers.Length; i++)
        {
            if (npcRenderers[i] is SkinnedMeshRenderer smr)
            {
                smr.updateWhenOffscreen = true;
            }
        }

        EnsureNpcCollider();
        CacheRigAndBindPose();
        SetupProceduralAudio();
        ResolveSceneTargets();

        // Hide Steve at his spawnpoint until the 15-second post-cleaning wait finishes
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        SnapToGroundImmediate();
        SetNpcVisible(false);
        currentState = NpcState.HiddenWaiting;
    }

    private void EnsureNpcCollider()
    {
        npcCapsule = GetComponent<CapsuleCollider>();
        if (npcCapsule == null)
        {
            npcCapsule = gameObject.AddComponent<CapsuleCollider>();
        }
        float sy = Mathf.Max(0.5f, Mathf.Abs(transform.lossyScale.y));
        float sx = Mathf.Max(0.5f, Mathf.Abs(transform.lossyScale.x));
        npcCapsule.height = 1.78f / sy;
        npcCapsule.radius = 0.30f / sx;
        npcCapsule.center = new Vector3(0f, (0.89f / sy), 0f);
        npcCapsule.isTrigger = false;
    }

    private void ResolveSceneTargets()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);

        // 0. Optional NPC Spawner reference
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "NPC Spawner", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "NPCSpawner", System.StringComparison.OrdinalIgnoreCase))
            {
                spawnPosition = new Vector3(t.position.x, transform.position.y, t.position.z);
                break;
            }
        }

        // 1. Active Shelf1 inside store
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            if (string.Equals(t.name.Trim(), "Shelf1", System.StringComparison.OrdinalIgnoreCase))
            {
                // Prefer stacja/Shelf1 near Z = -4.33
                if (shelf1Transform == null || t.position.z < -2.0f)
                {
                    shelf1Transform = t;
                }
            }
        }

        // 2. Sliding Entrance Doors
        slidingDoors = Object.FindAnyObjectByType<AutomaticSlidingDoors>();

        // 3. Double Beverage Refrigerator (prefer Double_Beverage_Fridge (1) at Z = -4.63 which has wide open aisle clearance)
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            if (t.name.StartsWith("Double_Beverage_Fridge", System.StringComparison.OrdinalIgnoreCase))
            {
                if (targetFridgeTransform == null || t.name.Contains("(1)"))
                {
                    InteractableFridgeDoor leftDoor = null;
                    InteractableFridgeDoor anyDoor = null;
                    InteractableFridgeDoor[] doors = t.GetComponentsInChildren<InteractableFridgeDoor>(true);
                    for (int d = 0; d < doors.Length; d++)
                    {
                        if (doors[d] == null) continue;
                        if (anyDoor == null) anyDoor = doors[d];
                        if (doors[d].name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            leftDoor = doors[d];
                        }
                    }

                    InteractableFridgeDoor chosenDoor = leftDoor != null ? leftDoor : anyDoor;
                    if (chosenDoor != null)
                    {
                        targetFridgeTransform = t;
                        targetFridgeDoor = chosenDoor;
                    }
                }
            }
        }

        if (targetFridgeDoor != null)
        {
            Transform[] doorChildren = targetFridgeDoor.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < doorChildren.Length; i++)
            {
                if (doorChildren[i].name.IndexOf("Handle_Outer", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    doorChildren[i].name.IndexOf("Handle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fridgeDoorHandleTransform = doorChildren[i];
                    break;
                }
            }
        }

        // 4. InvisibleNPCItem (or InvisibleNPCItemPutScene) on checkout counter
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "InvisibleNPCItem", System.StringComparison.OrdinalIgnoreCase))
            {
                invisibleNpcItemTransform = t;
                break;
            }
            if (nm.IndexOf("InvisibleNPCItem", System.StringComparison.OrdinalIgnoreCase) >= 0 && invisibleNpcItemTransform == null)
            {
                invisibleNpcItemTransform = t;
            }
        }

        if (invisibleNpcItemTransform != null)
        {
            // Ensure marker mesh is invisible and non-blocking
            Renderer[] rends = invisibleNpcItemTransform.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++) rends[i].enabled = false;

            Collider[] cols = invisibleNpcItemTransform.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) cols[i].isTrigger = true;
        }

        // 5. PlayerNpcInteract (invisible cashier stand marker behind counter)
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "PlayerNpcInteract", System.StringComparison.OrdinalIgnoreCase))
            {
                playerNpcInteractTransform = t;
                break;
            }
            if (nm.IndexOf("PlayerNpcInteract", System.StringComparison.OrdinalIgnoreCase) >= 0 && playerNpcInteractTransform == null)
            {
                playerNpcInteractTransform = t;
            }
        }

        if (playerNpcInteractTransform != null)
        {
            playerNpcInteractTransform.gameObject.SetActive(true);
            Renderer[] rends = playerNpcInteractTransform.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++) rends[i].enabled = false;

            Collider[] cols = playerNpcInteractTransform.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) cols[i].isTrigger = true;
        }
        else
        {
            // Automatic invisible fallback marker behind the checkout register counter
            Vector3 fallbackPos = invisibleNpcItemTransform != null
                ? new Vector3(invisibleNpcItemTransform.position.x - 0.15f, 0.17f, invisibleNpcItemTransform.position.z - 0.92f)
                : new Vector3(2.49f, 0.17f, -7.57f);

            GameObject fallbackMarker = new GameObject("PlayerNpcInteract");
            fallbackMarker.transform.position = fallbackPos;
            BoxCollider trig = fallbackMarker.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.size = new Vector3(1.1f, 2.0f, 1.1f);
            trig.center = new Vector3(0f, 1.0f, 0f);
            playerNpcInteractTransform = fallbackMarker.transform;
        }
    }

    public void SetNpcVisible(bool visible)
    {
        isNpcVisible = visible;
        if (npcRenderers != null)
        {
            for (int i = 0; i < npcRenderers.Length; i++)
            {
                if (npcRenderers[i] != null) npcRenderers[i].enabled = visible;
            }
        }
        if (npcCapsule != null)
        {
            npcCapsule.enabled = visible;
        }
    }

    // =========================================================================
    // MAIN STORY SEQUENCE COROUTINE
    // =========================================================================

    public Coroutine StartCustomerVisit()
    {
        return StartCoroutine(CustomerVisitRoutine());
    }

    private IEnumerator CustomerVisitRoutine()
    {
        ResolveSceneTargets();

        // Spawn Steve at his initial spawnpoint and make him visible
        transform.position = spawnPosition;
        SnapToGroundImmediate();
        SetNpcVisible(true);

        // ---------------------------------------------------------------------
        // 1. Walk from Spawnpoint -> Gas Station Entrance -> Shelf1
        // ---------------------------------------------------------------------
        currentState = NpcState.WalkingToShelf1;

        Vector3 shelfStandPos = ComputeShelf1StandPosition(out Vector3 shelfLookTarget);
        List<Vector3> pathToShelf1 = BuildObstacleFreePathToShelf1(spawnPosition, shelfStandPos);

        yield return StartCoroutine(FollowWaypointsRoutine(pathToShelf1, checkEntranceDoorway: true));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(shelfLookTarget, 0.45f));

        // ---------------------------------------------------------------------
        // 2. Browse Shelf1: Wait 5s -> Show Task & Allow E to Talk -> Dialogue -> Browse 7s
        // ---------------------------------------------------------------------
        currentState = NpcState.BrowsingShelf1Initial5s;
        yield return new WaitForSeconds(5.0f);

        shelfTalkTriggered = false;
        currentState = NpcState.WaitingForPlayerTalkAtShelf1;
        if (NightShiftStoryController.Instance != null)
        {
            NightShiftStoryController.Instance.NotifyCustomerTaskUpdated();
        }

        while (!shelfTalkTriggered)
        {
            yield return null;
        }

        // Play "Steve: I'm still browsing thank you..." (player movement locked while active!)
        currentState = NpcState.TalkingAtShelf1;
        lookAtPlayerHead = true;
        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Steve: I'm still browsing thank you..."));
        }
        lookAtPlayerHead = false;

        // Give Steve 7 seconds to continue browsing Shelf1
        currentState = NpcState.BrowsingShelf1Post7s;
        yield return new WaitForSeconds(7.0f);

        // ---------------------------------------------------------------------
        // 3. Walk to Double Refrigerator, Open Door (Zero Bump), Browse 7s, Pick Coca-Cola, Close Door
        // ---------------------------------------------------------------------
        currentState = NpcState.WalkingToFridge;
        ComputeFridgeChoreographyPoints(
            out Vector3 fridgeApproachPos,
            out Vector3 fridgeStepBackSafePos,
            out Vector3 fridgeBrowsePos,
            out Vector3 fridgeLookTarget,
            out Vector3 cokeShelfReachPos);

        List<Vector3> pathToFridge = new List<Vector3>
        {
            new Vector3(3.38f, transform.position.y, -5.22f),
            fridgeApproachPos
        };
        yield return StartCoroutine(FollowWaypointsRoutine(pathToFridge, checkEntranceDoorway: false));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(fridgeLookTarget, 0.40f));

        // Reach right hand to fridge handle & step back smoothly as door opens so door NEVER hits Steve!
        SetFridgeDoorsLockedByNpc(true);
        currentState = NpcState.OpeningFridgeDoor;
        yield return StartCoroutine(OpenFridgeWithStepBackRoutine(fridgeStepBackSafePos, fridgeLookTarget));

        // Step slightly into clear open bay spot (outside open glass door) and browse for 7 seconds
        yield return StartCoroutine(MoveDirectlyToSpotRoutine(fridgeBrowsePos, fridgeLookTarget, slowWalkSpeed * 0.80f));
        currentState = NpcState.BrowsingFridge7s;
        yield return new WaitForSeconds(7.0f);

        // Reach inside refrigerator and pick up Coca-Cola can
        currentState = NpcState.PickingUpCokeCan;
        yield return StartCoroutine(PickUpCocaColaFromFridgeRoutine(cokeShelfReachPos));

        // Step back clear of door swing and close refrigerator door with left hand
        currentState = NpcState.ClosingFridgeDoor;
        yield return StartCoroutine(CloseFridgeDoorCleanlyRoutine(fridgeStepBackSafePos, fridgeLookTarget));
        SetFridgeDoorsLockedByNpc(false);

        // ---------------------------------------------------------------------
        // 4. Slowly Walk to InvisibleNPCItem & Place Coca-Cola Can
        // ---------------------------------------------------------------------
        currentState = NpcState.WalkingToCounter;
        ComputeCheckoutPositions(out Vector3 counterStandPos, out Vector3 counterItemSurfacePos, out Vector3 cashierLookPos);

        List<Vector3> pathToCounter = new List<Vector3>
        {
            new Vector3(3.38f, transform.position.y, -5.32f),
            new Vector3(counterStandPos.x, transform.position.y, Mathf.Min(-5.32f, counterStandPos.z + 0.35f)),
            counterStandPos
        };
        yield return StartCoroutine(FollowWaypointsRoutine(pathToCounter, checkEntranceDoorway: false, speedMultiplier: 0.88f));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(cashierLookPos, 0.42f));

        // Hand animation placing Coca-Cola can onto InvisibleNPCItem
        currentState = NpcState.PlacingItemOnCounter;
        yield return StartCoroutine(PlaceCanOnCounterRoutine(counterItemSurfacePos));

        // ---------------------------------------------------------------------
        // 5. Wait 2s -> Show "Assist the customer" & PlayerNpcInteract Indicator -> Talk -> Scan -> Pay
        // ---------------------------------------------------------------------
        currentState = NpcState.Waiting2sBeforeAssistTask;
        yield return new WaitForSeconds(2.0f);

        counterTalkTriggered = false;
        currentState = NpcState.WaitingForPlayerCashierTalk;
        if (NightShiftStoryController.Instance != null)
        {
            NightShiftStoryController.Instance.NotifyCustomerTaskUpdated();
        }

        while (!counterTalkTriggered)
        {
            yield return null;
        }

        // Steve: "Just this one, thanks."
        currentState = NpcState.TalkingAtCounter1;
        lookAtPlayerHead = true;
        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Steve: Just this one, thanks."));
        }

        // Wait 2 seconds, then make Coca-Cola can interactable ("E to Scan")
        currentState = NpcState.Waiting2sBeforeScan;
        yield return new WaitForSeconds(2.0f);

        itemScannedTriggered = false;
        currentState = NpcState.WaitingForPlayerScan;
        InteractableCashierPC cashierPc = InteractableCashierPC.EnsureAttachedToScenePC();
        if (cashierPc != null)
        {
            cashierPc.BeginCustomerTransaction("Steve");
        }
        if (scannableCan != null)
        {
            scannableCan.SetScannable(true);
        }

        while (!itemScannedTriggered)
        {
            yield return null;
        }

        // Wait 1 second after scan -> "You: That will be 2$" -> "Steve: Alright here's 2$" -> E to Checkout on CashierPC
        currentState = NpcState.PostScanCheckoutSequence;
        yield return new WaitForSeconds(1.0f);

        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: That will be 2$"));
            PlayOneShotClip(cashPayClip, 0.55f, 1.0f);
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Steve: Alright here's 2$"));
        }

        if (cashierPc == null) cashierPc = InteractableCashierPC.EnsureAttachedToScenePC();
        if (cashierPc != null)
        {
            cashierPc.SetReadyForCheckout("Steve", 2.00f);
            currentState = NpcState.WaitingForPlayerCashierPCCheckout;
            while (!cashierPc.IsCheckoutCompleted)
            {
                yield return null;
            }
            cashierPc.ResetToIdleScreen();
        }
        lookAtPlayerHead = false;

        // Item Coca-Cola can disappears after CashierPC checkout completes
        if (cocaColaCanObj != null)
        {
            Destroy(cocaColaCanObj);
            cocaColaCanObj = null;
            scannableCan = null;
        }

        // ---------------------------------------------------------------------
        // 6. Leave the Store Walking Towards Spawnpoint & Disappear
        // ---------------------------------------------------------------------
        currentState = NpcState.WalkingBackToSpawn;
        if (NightShiftStoryController.Instance != null)
        {
            NightShiftStoryController.Instance.NotifyCustomerTaskUpdated();
        }

        List<Vector3> exitPath = BuildObstacleFreeExitPathToSpawn(transform.position, spawnPosition);
        yield return StartCoroutine(FollowWaypointsRoutine(exitPath, checkEntranceDoorway: true));

        // Disappear at spawnpoint
        SetNpcVisible(false);
        currentState = NpcState.DepartedHidden;
    }

    // =========================================================================
    // ACT 4 (SHIFT 2) CUSTOMER VISIT: UMBRELLA -> PILLAR -> CHIPSHELVE -> MAMACHIP ($9)
    // =========================================================================

    public Coroutine StartAct4CustomerVisit()
    {
        isAct4Mode = true;
        HasAct4SteveStartedLeaving = false;
        return StartCoroutine(Act4CustomerVisitRoutine());
    }

    private void ResolveAct4Targets()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            string nm = t.name.Trim();

            if (pillarFrontCenterTransform == null &&
                string.Equals(nm, "Pillar_Front_Center", System.StringComparison.OrdinalIgnoreCase))
            {
                pillarFrontCenterTransform = t;
            }

            if (chipShelveTransform == null &&
                (string.Equals(nm, "chipShelve", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(nm, "Chip_Shelve", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(nm, "ChipShelf", System.StringComparison.OrdinalIgnoreCase)))
            {
                chipShelveTransform = t;
            }

            if (sceneMamaChipTemplate == null &&
                (string.Equals(nm, "MamaChip", System.StringComparison.OrdinalIgnoreCase) ||
                 nm.IndexOf("MamaChip", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                sceneMamaChipTemplate = t.gameObject;
            }
        }

        if (chipShelveTransform == null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                string nm = t.name.Trim();
                if (nm.IndexOf("chipShelve", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    nm.IndexOf("chip_display", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    chipShelveTransform = t;
                    break;
                }
            }
        }

        if (chipShelveTransform == null)
        {
            chipShelveTransform = shelf1Transform;
        }
    }

    private IEnumerator Act4CustomerVisitRoutine()
    {
        ResolveSceneTargets();
        ResolveAct4Targets();

        transform.position = spawnPosition;
        SnapToGroundImmediate();
        SetNpcVisible(true);

        // 1. Spawn open 3D Umbrella in Steve's right hand while walking in the rain
        if (steveUmbrellaObj != null) Destroy(steveUmbrellaObj);
        steveUmbrellaObj = Act4UmbrellaVisualBuilder.CreateUmbrellaModel("Steve_Umbrella", out steveUmbrellaCanopyPivot);
        Act4UmbrellaVisualBuilder.SetCanopyOpenImmediate(steveUmbrellaCanopyPivot, true);
        isCarryingUmbrella = true;
        isUmbrellaOpen = true;
        isCarryingCan = false;

        // 2. Walk from spawnpoint toward the store entrance, stopping right in front of Pillar_Front_Center
        currentState = NpcState.WalkingToShelf1;

        float doorZ = slidingDoors != null ? slidingDoors.transform.position.z : -1.60f;
        float doorX = slidingDoors != null ? slidingDoors.transform.position.x : -1.77f;

        Vector3 pillarWorld = pillarFrontCenterTransform != null
            ? new Vector3(pillarFrontCenterTransform.position.x, 0.15f, pillarFrontCenterTransform.position.z)
            : new Vector3(doorX - 0.05f, 0.15f, doorZ - 1.85f);

        Vector3 pillarStandPos = new Vector3(pillarWorld.x - 0.82f, 0.15f, pillarWorld.z);
        Vector3 pillarLeanPos = new Vector3(pillarWorld.x - 0.26f, 0.15f, pillarWorld.z + 0.08f);

        List<Vector3> pathToPillar = new List<Vector3>();
        if (spawnPosition.x < -22f)
        {
            pathToPillar.Add(new Vector3(Mathf.Clamp(spawnPosition.x, -30.8f, -29.8f), spawnPosition.y, -13.20f));
            pathToPillar.Add(new Vector3(-25.50f, 0.02f, -12.80f));
        }
        pathToPillar.Add(new Vector3(-11.50f, 0.08f, -12.00f));
        pathToPillar.Add(new Vector3(-5.60f, 0.14f, -9.80f));
        pathToPillar.Add(new Vector3(-4.80f, 0.14f, -4.80f));
        pathToPillar.Add(pillarStandPos);

        yield return StartCoroutine(FollowWaypointsRoutine(pathToPillar, checkEntranceDoorway: false));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(pillarWorld + Vector3.up * 1.1f, 0.35f));

        // 3. Animation of sheathing (closing) the umbrella and putting it down leaning against Pillar_Front_Center
        float closeDur = 0.45f;
        float elapsed = 0f;
        while (elapsed < closeDur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, elapsed / closeDur);
            Act4UmbrellaVisualBuilder.SetCanopyOpenBlend(steveUmbrellaCanopyPivot, 1f - u);
            yield return null;
        }
        isUmbrellaOpen = false;

        // Reach right arm to lean umbrella against Pillar_Front_Center
        elapsed = 0f;
        Vector3 reachTarget = pillarLeanPos + Vector3.up * 0.55f;
        while (elapsed < 0.45f)
        {
            elapsed += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(0f, 1f, elapsed / 0.45f);
            rightArmWorldTarget = reachTarget;
            yield return null;
        }

        isCarryingUmbrella = false;
        if (steveUmbrellaObj != null)
        {
            steveUmbrellaObj.transform.SetParent(null, true);
            // Lean point-down against the west face of Pillar_Front_Center so ferrule tip rests on floor and J-hook handle leans at waist height
            steveUmbrellaObj.transform.position = new Vector3(pillarWorld.x - 0.25f, 0.15f + 0.58f, pillarWorld.z);
            steveUmbrellaObj.transform.rotation = Quaternion.Euler(0f, 0f, 168f);
            steveUmbrellaObj.transform.localScale = Vector3.one;
        }
        PlayOneShotClip(canPlaceClip, 0.40f, 0.85f);

        elapsed = 0f;
        while (elapsed < 0.38f)
        {
            elapsed += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(1f, 0f, elapsed / 0.38f);
            rightArmWorldTarget = reachTarget;
            yield return null;
        }
        rightArmReachWeight = 0f;

        // 4. Walk inside the store and browse chipShelve for 5 seconds
        Vector3 chipStandPos = ComputeChipShelveStandPosition(out Vector3 chipLookTarget, out Vector3 chipReachPos);
        List<Vector3> pathToChipShelf = new List<Vector3>
        {
            new Vector3(doorX - 1.35f, 0.15f, doorZ),
            new Vector3(-0.72f, 0.17f, doorZ),
            new Vector3(-0.72f, 0.17f, -3.50f),
            new Vector3(-0.48f, 0.17f, -4.85f),
            chipStandPos
        };

        yield return StartCoroutine(FollowWaypointsRoutine(pathToChipShelf, checkEntranceDoorway: true));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(chipLookTarget, 0.42f));

        currentState = NpcState.BrowsingShelf1Initial5s;
        yield return new WaitForSeconds(5.0f);

        // 5. Grab MamaChip from chipShelve
        elapsed = 0f;
        while (elapsed < 0.48f)
        {
            elapsed += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(0f, 1f, elapsed / 0.48f);
            rightArmWorldTarget = chipReachPos;
            yield return null;
        }

        cocaColaCanObj = CreateMamaChipItem();
        if (sceneMamaChipTemplate != null)
        {
            // Deduct MamaChip from chipShelve when Steve grabs it
            sceneMamaChipTemplate.SetActive(false);
        }
        isCarryingCan = true;
        PlayOneShotClip(canPlaceClip, 0.38f, 1.12f);

        yield return new WaitForSeconds(0.15f);

        elapsed = 0f;
        while (elapsed < 0.42f)
        {
            elapsed += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(1f, 0f, elapsed / 0.42f);
            rightArmWorldTarget = chipReachPos;
            yield return null;
        }
        rightArmReachWeight = 0f;

        // 6. Walk straight East along the clear South Aisle to the counter and put MamaChip down
        currentState = NpcState.WalkingToCounter;
        ComputeCheckoutPositions(out Vector3 counterStandPos, out Vector3 counterItemSurfacePos, out Vector3 cashierLookPos);

        List<Vector3> pathToCounter = new List<Vector3>
        {
            new Vector3(0.65f, 0.17f, -5.35f),
            new Vector3(counterStandPos.x, 0.17f, Mathf.Min(-5.35f, counterStandPos.z + 0.35f)),
            counterStandPos
        };
        yield return StartCoroutine(FollowWaypointsRoutine(pathToCounter, checkEntranceDoorway: false, speedMultiplier: 0.90f));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(cashierLookPos, 0.40f));

        currentState = NpcState.PlacingItemOnCounter;
        yield return StartCoroutine(PlaceCanOnCounterRoutine(counterItemSurfacePos));

        // 7. Wait for player interaction from PlayerNpcInteract ("E to Interact")
        counterTalkTriggered = false;
        currentState = NpcState.WaitingForPlayerCashierTalk;
        if (NightShiftStoryController.Instance != null)
        {
            NightShiftStoryController.Instance.NotifyCustomerTaskUpdated();
        }

        while (!counterTalkTriggered)
        {
            yield return null;
        }

        // 8. Act 4 Steve Dialogue Sequence
        currentState = NpcState.TalkingAtCounter1;
        lookAtPlayerHead = true;
        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Steve: Let me have this."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: That will be 9$."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: Do you usually buy here everynight?"));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Steve: Yeah, as usual."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Steve: I've been buying this store for like 3 yrs now."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: That's awesome."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: Alright enough with the chit-chat, let me check you out."));
        }

        // 9. Task: Check out. (Scan MamaChip & Checkout on CashierPC)
        itemScannedTriggered = false;
        currentState = NpcState.WaitingForPlayerScan;
        InteractableCashierPC cashierPc = InteractableCashierPC.EnsureAttachedToScenePC();
        if (cashierPc != null)
        {
            cashierPc.BeginCustomerTransaction("Steve");
            // Pre-populate and ready CashierPC so player can either scan MamaChip first OR click Checkout directly on CashierPC
            cashierPc.RecordScannedItem("4809-MAMA-CHP", "MamaChip Crispy Chips", 9.00f, "Steve");
            cashierPc.SetReadyForCheckout("Steve", 9.00f);
        }
        if (scannableCan != null)
        {
            scannableCan.SetScannable(true);
        }
        if (NightShiftStoryController.Instance != null)
        {
            NightShiftStoryController.Instance.NotifyCustomerTaskUpdated();
        }

        currentState = NpcState.WaitingForPlayerCashierPCCheckout;
        while (cashierPc != null && !cashierPc.IsCheckoutCompleted)
        {
            yield return null;
        }
        if (cashierPc != null)
        {
            cashierPc.ResetToIdleScreen();
        }

        // 10. Steve grabs the MamaChip bag back from the counter and says "Steve: Thanks, have a nice night."
        if (scannableCan != null) scannableCan.SetScannable(false);
        elapsed = 0f;
        Vector3 grabTarget = counterItemSurfacePos + Vector3.up * 0.07f;
        while (elapsed < 0.42f)
        {
            elapsed += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(0f, 1f, elapsed / 0.42f);
            rightArmWorldTarget = grabTarget;
            yield return null;
        }

        isCarryingCan = true;
        PlayOneShotClip(canPlaceClip, 0.40f, 1.08f);

        elapsed = 0f;
        while (elapsed < 0.38f)
        {
            elapsed += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(1f, 0f, elapsed / 0.38f);
            rightArmWorldTarget = grabTarget;
            yield return null;
        }
        rightArmReachWeight = 0f;

        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Steve: Thanks, have a nice night."));
        }
        lookAtPlayerHead = false;

        // 11. Signal NightShiftStoryController immediately so the 12-second cooldown starts while Steve walks out!
        HasAct4SteveStartedLeaving = true;
        currentState = NpcState.WalkingBackToSpawn;
        if (NightShiftStoryController.Instance != null)
        {
            NightShiftStoryController.Instance.NotifyCustomerTaskUpdated();
        }

        // Walk out to Pillar_Front_Center to retrieve umbrella
        List<Vector3> pathToExitPillar = new List<Vector3>
        {
            new Vector3(1.50f, 0.17f, -5.22f),
            new Vector3(0.05f, 0.17f, -5.18f),
            new Vector3(-0.55f, 0.17f, -4.65f),
            new Vector3(-0.72f, 0.17f, -3.50f),
            new Vector3(-0.72f, 0.17f, doorZ),
            new Vector3(doorX - 1.35f, 0.15f, doorZ),
            pillarStandPos
        };
        yield return StartCoroutine(FollowWaypointsRoutine(pathToExitPillar, checkEntranceDoorway: true));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(pillarWorld + Vector3.up * 1.1f, 0.30f));

        // Put MamaChip in pocket/bag and grab Umbrella from Pillar_Front_Center
        if (cocaColaCanObj != null)
        {
            Destroy(cocaColaCanObj);
            cocaColaCanObj = null;
            scannableCan = null;
        }
        isCarryingCan = false;

        elapsed = 0f;
        while (elapsed < 0.38f)
        {
            elapsed += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(0f, 1f, elapsed / 0.38f);
            rightArmWorldTarget = reachTarget;
            yield return null;
        }

        isCarryingUmbrella = true;
        PlayOneShotClip(canPlaceClip, 0.38f, 1.05f);

        elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(1f, 0f, elapsed / 0.35f);
            rightArmWorldTarget = reachTarget;
            yield return null;
        }
        rightArmReachWeight = 0f;

        // Open the umbrella overhead
        elapsed = 0f;
        while (elapsed < 0.42f)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, elapsed / 0.42f);
            Act4UmbrellaVisualBuilder.SetCanopyOpenBlend(steveUmbrellaCanopyPivot, u);
            yield return null;
        }
        isUmbrellaOpen = true;

        // Walk back to spawnpoint in the rain and disappear
        List<Vector3> pathToSpawn = new List<Vector3>
        {
            new Vector3(-4.80f, 0.14f, -4.80f),
            new Vector3(-5.60f, 0.14f, -9.80f),
            new Vector3(-11.50f, 0.08f, -12.00f),
            new Vector3(-25.50f, 0.02f, -12.80f),
            new Vector3(Mathf.Clamp(spawnPosition.x, -30.8f, -29.8f), spawnPosition.y, -13.20f),
            spawnPosition
        };
        yield return StartCoroutine(FollowWaypointsRoutine(pathToSpawn, checkEntranceDoorway: false));

        if (steveUmbrellaObj != null)
        {
            Destroy(steveUmbrellaObj);
            steveUmbrellaObj = null;
        }
        isCarryingUmbrella = false;
        isUmbrellaOpen = false;
        SetNpcVisible(false);
        currentState = NpcState.DepartedHidden;
    }

    private Vector3 ComputeChipShelveStandPosition(out Vector3 lookTarget, out Vector3 reachPos)
    {
        // Note: Some counter props (like PhoneUserHerePut / ID1513 (1)) remain parented under chipShelve in the scene hierarchy
        // despite being positioned on the cashier counter (+X). Filter strictly to renderers actually on the west-wall chipShelve rack (X < -0.25f).
        Bounds rackBounds = new Bounds(new Vector3(-1.20f, 1.10f, -5.48f), new Vector3(0.45f, 1.40f, 0.95f));
        bool hasValidRackBounds = false;

        if (sceneMamaChipTemplate != null)
        {
            Renderer mamaRend = sceneMamaChipTemplate.GetComponentInChildren<Renderer>(true);
            if (mamaRend != null && mamaRend.bounds.size.sqrMagnitude > 0.0001f && mamaRend.bounds.center.x < -0.25f)
            {
                rackBounds = mamaRend.bounds;
                hasValidRackBounds = true;
            }
        }

        if (chipShelveTransform != null)
        {
            Renderer[] rends = chipShelveTransform.GetComponentsInChildren<Renderer>(true);
            if (rends != null)
            {
                for (int i = 0; i < rends.Length; i++)
                {
                    Renderer r = rends[i];
                    if (r == null) continue;
                    string rName = r.gameObject.name;
                    if (rName.IndexOf("PhoneUserHerePut", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        rName.IndexOf("ID1513", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }
                    Bounds rb = r.bounds;
                    if (rb.size.sqrMagnitude < 0.0001f || rb.center.x > -0.25f) continue;

                    if (!hasValidRackBounds)
                    {
                        rackBounds = rb;
                        hasValidRackBounds = true;
                    }
                    else
                    {
                        rackBounds.Encapsulate(rb);
                    }
                }
            }
        }

        Vector3 mamaTargetPos = new Vector3(
            Mathf.Clamp(rackBounds.center.x, -1.45f, -0.95f),
            1.15f,
            Mathf.Clamp(rackBounds.center.z, -5.62f, -5.25f));

        if (sceneMamaChipTemplate != null)
        {
            Renderer mr = sceneMamaChipTemplate.GetComponentInChildren<Renderer>(true);
            if (mr != null && mr.bounds.size.sqrMagnitude > 0.0001f && mr.bounds.center.x < -0.25f)
            {
                mamaTargetPos = new Vector3(
                    mr.bounds.center.x,
                    Mathf.Clamp(mr.bounds.center.y, 0.95f, 1.35f),
                    Mathf.Clamp(mr.bounds.center.z, -5.65f, -5.20f));
            }
        }

        // Stand in the aisle just to the East (+X) of chipShelve and face strictly West (-X) toward chipShelve
        float sx = Mathf.Clamp(rackBounds.max.x + 0.54f, -0.48f, -0.36f);
        float sz = Mathf.Clamp(mamaTargetPos.z, -5.62f, -5.25f);
        lookTarget = new Vector3(Mathf.Min(mamaTargetPos.x, sx - 0.78f), 1.15f, sz);
        reachPos = new Vector3(Mathf.Min(rackBounds.max.x - 0.03f, sx - 0.44f), mamaTargetPos.y, sz);
        return new Vector3(sx, 0.17f, sz);
    }

    private GameObject CreateMamaChipItem()
    {
        if (sceneMamaChipTemplate != null)
        {
            GameObject wrapper = new GameObject("Customer_MamaChip");
            GameObject clone = Instantiate(sceneMamaChipTemplate);
            clone.name = "MamaChip_Mesh";
            clone.SetActive(true);
            Collider[] cols = clone.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

            clone.transform.position = Vector3.zero;
            clone.transform.rotation = sceneMamaChipTemplate.transform.rotation;
            clone.transform.localScale = sceneMamaChipTemplate.transform.lossyScale;

            Renderer r = clone.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                Vector3 centerOffset = r.bounds.center - clone.transform.position;
                float halfH = r.bounds.extents.y;
                clone.transform.position = -centerOffset + new Vector3(0f, halfH, 0f);
            }

            clone.transform.SetParent(wrapper.transform, true);
            return wrapper;
        }

        return CustomerNpcMelia.CreateGoChipsBag("Customer_MamaChip");
    }

    // =========================================================================
    // IINTERACTABLE IMPLEMENTATION (TALK AT SHELF1 & TALK AT COUNTER)
    // =========================================================================

    public void Interact()
    {
        if (NightShiftStoryController.IsDialogueActive) return;

        if (currentState == NpcState.WaitingForPlayerTalkAtShelf1)
        {
            if (!IsPlayerWithinDistance(1.75f)) return;
            shelfTalkTriggered = true;
        }
        else if (currentState == NpcState.WaitingForPlayerCashierTalk)
        {
            if (!IsPlayerInsideCashierZone) return;
            counterTalkTriggered = true;
        }
    }

    public string GetPromptText()
    {
        if (NightShiftStoryController.IsDialogueActive) return string.Empty;

        if (currentState == NpcState.WaitingForPlayerTalkAtShelf1)
        {
            if (!IsPlayerWithinDistance(1.75f)) return string.Empty;
            return "E to talk";
        }

        if (currentState == NpcState.WaitingForPlayerCashierTalk)
        {
            if (!IsPlayerInsideCashierZone) return "Go to Cashier area first.";
            return isAct4Mode ? "E to Interact" : "E to talk";
        }

        return string.Empty;
    }

    public void NotifyCanScanned()
    {
        if (currentState != NpcState.WaitingForPlayerScan && currentState != NpcState.WaitingForPlayerCashierPCCheckout) return;
        PlayOneShotClip(scanBeepClip, 0.72f, 1.0f);

        InteractableCashierPC cashierPc = InteractableCashierPC.EnsureAttachedToScenePC();
        if (cashierPc != null)
        {
            if (isAct4Mode)
            {
                if (cashierPc.GetTotalItemCount() == 0)
                {
                    cashierPc.RecordScannedItem("4809-MAMA-CHP", "MamaChip Crispy Chips", 9.00f, "Steve");
                }
                cashierPc.SetReadyForCheckout("Steve", 9.00f);
            }
            else
            {
                cashierPc.RecordScannedItem("8901-COKE-330", "Coca-Cola Can (330ml)", 2.00f, "Steve");
            }
        }

        itemScannedTriggered = true;
    }

    private bool IsPlayerWithinDistance(float maxDist)
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        Vector3 closest = npcCapsule != null
            ? npcCapsule.ClosestPoint(cam.transform.position)
            : transform.position + Vector3.up * 1.2f;

        return Vector3.Distance(cam.transform.position, closest) <= maxDist;
    }

    // =========================================================================
    // WAYPOINT NAVIGATION, GROUND SNAPPING & ANTI-BUMP AVOIDANCE
    // =========================================================================

    private Vector3 ComputeShelf1StandPosition(out Vector3 lookTarget)
    {
        if (shelf1Transform != null)
        {
            Bounds b = new Bounds(shelf1Transform.position, new Vector3(1.6f, 1.6f, 0.8f));
            Renderer[] rends = shelf1Transform.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            }
            else
            {
                Collider col = shelf1Transform.GetComponentInChildren<Collider>();
                if (col != null) b = col.bounds;
            }

            lookTarget = new Vector3(b.center.x - 0.15f, 1.15f, b.center.z);
            // Stand comfortably in the south aisle facing Shelf1 (-Z face -> looking +Z)
            float standZ = Mathf.Clamp(b.min.z - 0.78f, -5.28f, -5.18f);
            float standX = Mathf.Clamp(b.center.x - 0.15f, 0.75f, 1.35f);
            return new Vector3(standX, 0.17f, standZ);
        }

        lookTarget = new Vector3(1.05f, 1.15f, -4.11f);
        return new Vector3(1.05f, 0.17f, -5.22f);
    }

    private List<Vector3> BuildObstacleFreePathToShelf1(Vector3 start, Vector3 shelfStandPos)
    {
        float doorZ = slidingDoors != null ? slidingDoors.transform.position.z : -1.60f;
        float doorX = slidingDoors != null ? slidingDoors.transform.position.x : -1.77f;

        List<Vector3> pts = new List<Vector3>();

        // 1. Walk along the right edge of the road up to the right-side station driveway (never through the middle of the pumps!)
        if (start.x < -22f)
        {
            pts.Add(new Vector3(Mathf.Clamp(start.x, -30.8f, -29.8f), start.y, -13.20f));
            pts.Add(new Vector3(-25.50f, 0.02f, -12.80f));
        }

        // 2. Walk naturally along the right-hand walkway beside the perimeter wall, completely clear of the middle pump lanes
        pts.Add(new Vector3(-11.50f, 0.08f, -12.00f));
        pts.Add(new Vector3(-5.60f, 0.14f, -9.80f));
        pts.Add(new Vector3(-4.80f, 0.14f, -4.80f));

        // 3. Align cleanly with the automatic sliding entrance doors
        pts.Add(new Vector3(doorX - 1.35f, 0.15f, doorZ));

        // 4. Step cleanly down the center of the west entrance aisle (X = -0.72f, 0.56m west of Shelf3 and 0.83m east of window wall)
        pts.Add(new Vector3(-0.72f, 0.17f, doorZ));
        pts.Add(new Vector3(-0.72f, 0.17f, -3.50f));

        // 5. Curve cleanly around Shelf1's south-west corner while staying 0.85m+ north-east of the left snack racks (group_1 at Z <= -5.005, X <= -0.994)
        pts.Add(new Vector3(-0.55f, 0.17f, -4.65f));
        pts.Add(new Vector3(0.05f, 0.17f, shelfStandPos.z));

        // 6. Step in front of Shelf1
        pts.Add(shelfStandPos);

        return pts;
    }

    private List<Vector3> BuildObstacleFreeExitPathToSpawn(Vector3 currentPos, Vector3 targetSpawn)
    {
        float doorZ = slidingDoors != null ? slidingDoors.transform.position.z : -1.60f;
        float doorX = slidingDoors != null ? slidingDoors.transform.position.x : -1.77f;

        return new List<Vector3>
        {
            new Vector3(1.50f, 0.17f, -5.22f),
            new Vector3(0.05f, 0.17f, -5.18f),
            new Vector3(-0.55f, 0.17f, -4.65f),
            new Vector3(-0.72f, 0.17f, -3.50f),
            new Vector3(-0.72f, 0.17f, doorZ),
            new Vector3(doorX - 1.35f, 0.15f, doorZ),
            new Vector3(-4.80f, 0.14f, -4.80f),
            new Vector3(-5.60f, 0.14f, -9.80f),
            new Vector3(-11.50f, 0.08f, -12.00f),
            new Vector3(-25.50f, 0.02f, -12.80f),
            new Vector3(Mathf.Clamp(targetSpawn.x, -30.8f, -29.8f), targetSpawn.y, -13.20f),
            targetSpawn
        };
    }

    private Vector3 EnforceStoreFurnitureClearance(Vector3 pos)
    {
        // 1. Shelf2 (North Gondola: X = [-0.155 .. +2.955], Z = [-0.184 .. +0.439])
        Bounds b2 = new Bounds();
        b2.SetMinMax(new Vector3(-0.16f, 0.17f, -0.19f), new Vector3(2.96f, 1.58f, 0.44f));
        pos = PushOutOfBox2D(pos, b2, 0.34f);

        // 2. Shelf3 (Middle Gondola: X = [-0.155 .. +2.955], Z = [-2.142 .. -1.519])
        Bounds b3 = new Bounds();
        b3.SetMinMax(new Vector3(-0.16f, 0.17f, -2.15f), new Vector3(2.96f, 1.58f, -1.51f));
        pos = PushOutOfBox2D(pos, b3, 0.34f);

        // 3. Shelf1 (South Gondola: X = [-0.067 .. +2.741], Z = [-4.424 .. -3.801])
        Bounds b1 = new Bounds();
        b1.SetMinMax(new Vector3(-0.07f, 0.17f, -4.43f), new Vector3(2.75f, 1.58f, -3.80f));
        if (shelf1Transform != null)
        {
            Renderer[] rends = shelf1Transform.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                b1 = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b1.Encapsulate(rends[i].bounds);
            }
        }
        pos = PushOutOfBox2D(pos, b1, 0.34f);

        // 4. Left Window Wire Snack Racks (model/group_0 & model/group_1: X = [-1.41 .. -0.99], Z = [-6.28 .. -5.00])
        Bounds leftSnackRacks = new Bounds();
        leftSnackRacks.SetMinMax(new Vector3(-1.45f, 0.0f, -6.32f), new Vector3(-0.98f, 1.55f, -4.99f));
        pos = PushOutOfBox2D(pos, leftSnackRacks, 0.32f);

        return pos;
    }

    private static Vector3 PushOutOfBox2D(Vector3 pos, Bounds box, float margin)
    {
        float minX = box.min.x - margin;
        float maxX = box.max.x + margin;
        float minZ = box.min.z - margin;
        float maxZ = box.max.z + margin;

        if (pos.x > minX && pos.x < maxX && pos.z > minZ && pos.z < maxZ)
        {
            float dWest = pos.x - minX;
            float dEast = maxX - pos.x;
            float dSouth = pos.z - minZ;
            float dNorth = maxZ - pos.z;

            float minDist = Mathf.Min(Mathf.Min(dWest, dEast), Mathf.Min(dSouth, dNorth));
            if (minDist == dWest) pos.x = minX;
            else if (minDist == dEast) pos.x = maxX;
            else if (minDist == dSouth) pos.z = minZ;
            else pos.z = maxZ;
        }
        return pos;
    }

    private void ComputeFridgeChoreographyPoints(
        out Vector3 approachPos,
        out Vector3 stepBackSafePos,
        out Vector3 browsePos,
        out Vector3 lookTarget,
        out Vector3 cokeShelfReachPos)
    {
        if (targetFridgeTransform != null && targetFridgeDoor != null)
        {
            // Fridge forward points outward into store aisle (-X); fridge right points from Left bay to Right bay (+Z)
            Vector3 outward = targetFridgeTransform.forward;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.001f) outward = Vector3.left;
            outward.Normalize();

            Vector3 leftToRight = targetFridgeTransform.right;
            leftToRight.y = 0f;
            if (leftToRight.sqrMagnitude < 0.001f) leftToRight = Vector3.forward;
            leftToRight.Normalize();

            Vector3 pivotPos = targetFridgeDoor.transform.position;
            bool isLeftDoor = targetFridgeDoor.openAngleY < 0f;
            float dirSign = isLeftDoor ? 1f : -1f;

            Vector3 closedHandleWorld = fridgeDoorHandleTransform != null
                ? fridgeDoorHandleTransform.position
                : pivotPos + leftToRight * (0.85f * dirSign);

            // Approach close enough for arm to grasp handle
            approachPos = new Vector3(closedHandleWorld.x, 0.17f, closedHandleWorld.z)
                          + outward * 0.68f
                          + leftToRight * (0.14f * dirSign);

            // Safe step-back position completely outside the 0.99m swinging glass door arc AND well east of Shelf1!
            stepBackSafePos = new Vector3(pivotPos.x, 0.17f, pivotPos.z)
                              + outward * 0.88f
                              + leftToRight * (1.02f * dirSign);

            // Open-bay browsing position (0.70m clear of the open door leaf resting at pivotPos)
            browsePos = new Vector3(pivotPos.x, 0.17f, pivotPos.z)
                        + outward * 0.80f
                        + leftToRight * (0.70f * dirSign);

            lookTarget = new Vector3(pivotPos.x, 1.25f, pivotPos.z)
                         - outward * 0.35f
                         + leftToRight * (0.52f * dirSign);

            cokeShelfReachPos = new Vector3(pivotPos.x, 1.23f, pivotPos.z)
                                - outward * 0.18f
                                + leftToRight * (0.54f * dirSign);
            return;
        }

        // Fallback coordinates for Double_Beverage_Fridge (1)
        approachPos = new Vector3(3.52f, 0.17f, -4.60f);
        stepBackSafePos = new Vector3(3.38f, 0.17f, -4.58f);
        browsePos = new Vector3(3.45f, 0.17f, -4.89f);
        lookTarget = new Vector3(4.55f, 1.25f, -5.08f);
        cokeShelfReachPos = new Vector3(4.38f, 1.23f, -5.08f);
    }

    private void ComputeCheckoutPositions(
        out Vector3 standPos,
        out Vector3 itemSurfacePos,
        out Vector3 cashierLookPos)
    {
        if (invisibleNpcItemTransform != null)
        {
            Vector3 p = invisibleNpcItemTransform.position;
            float halfHeight = Mathf.Abs(invisibleNpcItemTransform.lossyScale.y) * 0.5f;
            itemSurfacePos = new Vector3(p.x, p.y + halfHeight, p.z);

            // Counter runs East-West along X at Z = p.z; cashier stands on -Z side, customer stands squarely on +Z side
            float zSign = 1f;
            if (playerNpcInteractTransform != null)
            {
                zSign = (playerNpcInteractTransform.position.z <= p.z) ? 1f : -1f;
            }

            standPos = new Vector3(p.x - 0.12f, 0.17f, p.z + (0.90f * zSign));
            cashierLookPos = playerNpcInteractTransform != null
                ? new Vector3(playerNpcInteractTransform.position.x, 1.35f, playerNpcInteractTransform.position.z)
                : new Vector3(p.x - 0.25f, 1.35f, p.z - (0.90f * zSign));
            return;
        }

        itemSurfacePos = new Vector3(2.64f, 1.03f, -6.65f);
        standPos = new Vector3(2.64f, 0.17f, -5.78f);
        cashierLookPos = new Vector3(2.64f, 1.35f, -7.50f);
    }

    private IEnumerator FollowWaypointsRoutine(List<Vector3> waypoints, bool checkEntranceDoorway, float speedMultiplier = 1f)
    {
        isCurrentlyWalking = true;

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 target = waypoints[i];

            while (true)
            {
                Vector3 cur = transform.position;
                Vector3 toTarget = new Vector3(target.x - cur.x, 0f, target.z - cur.z);
                float dist = toTarget.magnitude;
                if (dist <= 0.09f)
                {
                    break;
                }

                // If crossing the sliding entrance doors, wait gently until the door panels have opened enough
                if (checkEntranceDoorway && slidingDoors != null)
                {
                    float distToDoor = Vector2.Distance(
                        new Vector2(cur.x, cur.z),
                        new Vector2(slidingDoors.transform.position.x, slidingDoors.transform.position.z));

                    if (distToDoor < 1.35f)
                    {
                        slidingDoors.isOpen = true;
                        if (slidingDoors.leftDoor != null && Mathf.Abs(slidingDoors.leftDoor.localPosition.x) < 1.15f)
                        {
                            isCurrentlyWalking = false;
                            RotateTowardsWorldDir(toTarget.normalized, Time.deltaTime * turnSpeed);
                            yield return null;
                            isCurrentlyWalking = true;
                            continue;
                        }
                    }
                }

                // Polite yield if player stands right in front of Steve
                if (IsPlayerBlockingImmediatePath(toTarget.normalized))
                {
                    isCurrentlyWalking = false;
                    yield return null;
                    isCurrentlyWalking = true;
                    continue;
                }

                Vector3 moveDir = ComputeObstacleFreeMoveDirection(cur, toTarget.normalized);
                RotateTowardsWorldDir(moveDir, Time.deltaTime * turnSpeed);

                float step = slowWalkSpeed * speedMultiplier * Time.deltaTime;
                if (step > dist) step = dist;

                Vector3 nextPos = cur + moveDir * step;
                nextPos = EnforceStoreFurnitureClearance(nextPos);
                nextPos.y = SampleGroundHeight(nextPos, cur.y);
                transform.position = nextPos;

                AdvanceWalkAudioAndTimer(speedMultiplier);
                yield return null;
            }
        }

        isCurrentlyWalking = false;
    }

    private IEnumerator MoveDirectlyToSpotRoutine(Vector3 targetSpot, Vector3 lookAtWorld, float speed)
    {
        isCurrentlyWalking = true;

        while (true)
        {
            Vector3 cur = transform.position;
            Vector3 diff = new Vector3(targetSpot.x - cur.x, 0f, targetSpot.z - cur.z);
            float dist = diff.magnitude;
            if (dist <= 0.04f) break;

            Vector3 lookDir = new Vector3(lookAtWorld.x - cur.x, 0f, lookAtWorld.z - cur.z);
            if (lookDir.sqrMagnitude > 0.001f)
            {
                RotateTowardsWorldDir(lookDir.normalized, Time.deltaTime * turnSpeed);
            }

            float step = Mathf.Min(dist, speed * Time.deltaTime);
            Vector3 nextPos = cur + diff.normalized * step;
            nextPos = EnforceStoreFurnitureClearance(nextPos);
            nextPos.y = SampleGroundHeight(nextPos, cur.y);
            transform.position = nextPos;

            AdvanceWalkAudioAndTimer(0.75f);
            yield return null;
        }

        isCurrentlyWalking = false;
    }

    private IEnumerator TurnSmoothlyTowardsRoutine(Vector3 worldLookTarget, float duration)
    {
        isCurrentlyWalking = false;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector3 cur = transform.position;
            cur.y = SampleGroundHeight(cur, cur.y);
            transform.position = cur;

            Vector3 dir = worldLookTarget - cur;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                RotateTowardsWorldDir(dir.normalized, Time.deltaTime * (turnSpeed * 1.5f));
            }
            yield return null;
        }
        SnapToGroundImmediate();
    }

    private bool IsPlayerBlockingImmediatePath(Vector3 moveDir)
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) return false;

        Vector3 pPos = cam.transform.position;
        Vector3 diff = new Vector3(pPos.x - transform.position.x, 0f, pPos.z - transform.position.z);
        float d = diff.magnitude;
        if (d < 0.82f && d > 0.01f)
        {
            return Vector3.Dot(diff.normalized, moveDir) > 0.45f;
        }
        return false;
    }

    private Vector3 ComputeObstacleFreeMoveDirection(Vector3 currentPos, Vector3 desiredDir)
    {
        Vector3 checkCenter = currentPos + Vector3.up * 0.85f;
        Collider[] hits = Physics.OverlapSphere(checkCenter, 0.34f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return desiredDir;

        Vector3 repulsion = Vector3.zero;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null) continue;
            if (col.transform.IsChildOf(transform)) continue;

            // Ignore floor / road / flat ground slabs
            if (col.bounds.max.y <= currentPos.y + 0.24f) continue;

            // Ignore overhead lintels above head
            if (col.bounds.min.y >= currentPos.y + 1.80f) continue;

            Vector3 closest = col.bounds.ClosestPoint(checkCenter);
            Vector3 push = new Vector3(checkCenter.x - closest.x, 0f, checkCenter.z - closest.z);
            float d = push.magnitude;
            if (d < 0.32f && d > 0.0001f)
            {
                repulsion += push.normalized * ((0.32f - d) / 0.32f);
            }
        }

        if (repulsion.sqrMagnitude < 0.0001f) return desiredDir;

        Vector3 blended = (desiredDir + repulsion * 1.15f);
        blended.y = 0f;
        return blended.sqrMagnitude > 0.0001f ? blended.normalized : desiredDir;
    }

    private static bool IsNonFloorPropCollider(Collider col)
    {
        Transform t = col.transform;
        while (t != null)
        {
            string n = t.name;
            if (n.IndexOf("Shelf", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Fridge", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Counter", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Cashier", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Register", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Cardboard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Putter", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Trashbin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Cube.008", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            t = t.parent;
        }
        return false;
    }

    private float SampleGroundHeight(Vector3 pos, float currentY)
    {
        Vector3 origin = new Vector3(pos.x, currentY + 0.65f, pos.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 2.2f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return currentY;

        // Pick the HIGHEST walkable floor surface below step-up height so overlapping sub-layers
        // (Terrain at -0.05m / Ground at 0.00m beneath Store_Tiled_Floor at 0.17m-0.18m) never pull Steve underground!
        float highestFloorY = float.MinValue;
        float maxStepUpY = currentY + 0.48f;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.normal.y < 0.70f) continue; // Only walkable horizontal surfaces
            if (h.point.y > maxStepUpY) continue;
            if (IsNonFloorPropCollider(h.collider)) continue;

            if (h.point.y > highestFloorY)
            {
                highestFloorY = h.point.y;
            }
        }

        if (highestFloorY == float.MinValue) return currentY;
        return Mathf.Lerp(currentY, highestFloorY, Mathf.Clamp01(Time.deltaTime * 22f));
    }

    private void SnapToGroundImmediate()
    {
        Vector3 origin = transform.position + Vector3.up * 0.65f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore);
        float highestFloorY = float.MinValue;
        float maxStepUpY = transform.position.y + 0.55f;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
            if (h.normal.y < 0.70f) continue;
            if (h.point.y > maxStepUpY) continue;
            if (IsNonFloorPropCollider(h.collider)) continue;

            if (h.point.y > highestFloorY)
            {
                highestFloorY = h.point.y;
            }
        }

        if (highestFloorY > float.MinValue)
        {
            transform.position = new Vector3(transform.position.x, highestFloorY, transform.position.z);
        }
    }

    private void RotateTowardsWorldDir(Vector3 worldForwardDir, float maxRadiansDelta)
    {
        if (worldForwardDir.sqrMagnitude < 0.0001f) return;
        Quaternion lookRot = Quaternion.LookRotation(worldForwardDir.normalized, Vector3.up);
        Quaternion correctedTarget = lookRot * Quaternion.Euler(0f, modelYawCorrectionDeg, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, correctedTarget, Mathf.Clamp01(maxRadiansDelta));
    }

    private void AdvanceWalkAudioAndTimer(float speedMult)
    {
        walkCycleTimer += Time.deltaTime * 5.2f * speedMult;
        footstepTimer += Time.deltaTime * speedMult;
        if (footstepTimer >= 0.58f)
        {
            footstepTimer = 0f;
            PlayOneShotClip(footstepClip, 0.24f, Random.Range(0.88f, 1.06f));
        }
    }

    // =========================================================================
    // FRIDGE INTERACTION, COCA-COLA PICKUP & COUNTER PLACEMENT ROUTINES
    // =========================================================================

    private IEnumerator OpenFridgeWithStepBackRoutine(Vector3 stepBackSafePos, Vector3 fridgeLookTarget)
    {
        // Phase A: Reach right hand toward fridge outer handle (0.38s)
        float t = 0f;
        while (t < 0.38f)
        {
            t += Time.deltaTime;
            rightArmReachWeight = Mathf.Clamp01(t / 0.38f);
            rightArmWorldTarget = GetCurrentFridgeHandleWorldPos();
            yield return null;
        }

        // Trigger fridge door open & play subtle suction seal sound
        if (targetFridgeDoor != null)
        {
            targetFridgeDoor.isOpen = true;
        }
        PlayOneShotClip(fridgeSealClip, 0.42f, 1.0f);

        // Phase B: Smoothly step back & adjust outside the swinging glass door arc so Steve NEVER gets hit!
        isCurrentlyWalking = true;
        Vector3 startPos = transform.position;
        float duration = 0.85f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            Vector3 nextPos = Vector3.Lerp(startPos, stepBackSafePos, u);
            nextPos = EnforceStoreFurnitureClearance(nextPos);
            nextPos.y = SampleGroundHeight(nextPos, transform.position.y);
            transform.position = nextPos;

            Vector3 lookDir = fridgeLookTarget - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                RotateTowardsWorldDir(lookDir.normalized, Time.deltaTime * turnSpeed);
            }

            // Keep hand tracking the door handle for first half of swing, then release smoothly
            rightArmWorldTarget = GetCurrentFridgeHandleWorldPos();
            rightArmReachWeight = u < 0.5f ? 1f : Mathf.Clamp01(1f - ((u - 0.5f) / 0.5f));

            AdvanceWalkAudioAndTimer(0.75f);
            yield return null;
        }

        rightArmReachWeight = 0f;
        isCurrentlyWalking = false;
    }

    private IEnumerator PickUpCocaColaFromFridgeRoutine(Vector3 cokeShelfReachPos)
    {
        // Reach right hand into open Coca-Cola bay
        float t = 0f;
        while (t < 0.50f)
        {
            t += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.50f));
            rightArmWorldTarget = cokeShelfReachPos;
            yield return null;
        }

        // Spawn authentic 3D Coca-Cola can in Steve's right hand
        cocaColaCanObj = CreateAuthenticCocaColaCan();
        isCarryingCan = true;
        PlayOneShotClip(canPlaceClip, 0.35f, 1.18f);

        yield return new WaitForSeconds(0.16f);

        // Retract right arm to carry pose
        t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / 0.45f));
            rightArmWorldTarget = cokeShelfReachPos;
            yield return null;
        }

        rightArmReachWeight = 0f;
    }

    private IEnumerator CloseFridgeDoorCleanlyRoutine(Vector3 stepBackSafePos, Vector3 fridgeLookTarget)
    {
        // Step back outside the door closing arc first
        yield return StartCoroutine(MoveDirectlyToSpotRoutine(stepBackSafePos, fridgeLookTarget, slowWalkSpeed * 0.80f));

        // Extend left hand briefly toward open door edge & trigger close
        float t = 0f;
        while (t < 0.32f)
        {
            t += Time.deltaTime;
            leftArmReachWeight = Mathf.Clamp01(t / 0.32f);
            leftArmWorldTarget = GetCurrentFridgeHandleWorldPos();
            yield return null;
        }

        if (targetFridgeDoor != null)
        {
            targetFridgeDoor.isOpen = false;
        }
        PlayOneShotClip(fridgeSealClip, 0.38f, 0.90f);

        t = 0f;
        while (t < 0.55f)
        {
            t += Time.deltaTime;
            leftArmReachWeight = Mathf.Clamp01(1f - (t / 0.38f));
            leftArmWorldTarget = GetCurrentFridgeHandleWorldPos();
            yield return null;
        }
        leftArmReachWeight = 0f;
    }

    private IEnumerator PlaceCanOnCounterRoutine(Vector3 counterSurfacePos)
    {
        Vector3 handPlaceTarget = counterSurfacePos + Vector3.up * 0.07f;

        // Extend right hand carrying the Coca-Cola can over InvisibleNPCItem
        float t = 0f;
        while (t < 0.60f)
        {
            t += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.60f));
            rightArmWorldTarget = handPlaceTarget;
            yield return null;
        }

        // Release Coca-Cola can upright onto InvisibleNPCItem
        isCarryingCan = false;
        if (cocaColaCanObj != null)
        {
            cocaColaCanObj.transform.position = counterSurfacePos;
            cocaColaCanObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            cocaColaCanObj.transform.localScale = Vector3.one;

            scannableCan = cocaColaCanObj.GetComponent<ScannableCounterCan>();
            if (scannableCan == null)
            {
                scannableCan = cocaColaCanObj.AddComponent<ScannableCounterCan>();
            }
            scannableCan.Initialize(this);
        }

        PlayOneShotClip(canPlaceClip, 0.52f, 0.96f);
        yield return new WaitForSeconds(0.18f);

        // Retract right hand smoothly to relaxed standing pose
        t = 0f;
        while (t < 0.48f)
        {
            t += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / 0.48f));
            rightArmWorldTarget = handPlaceTarget;
            yield return null;
        }

        rightArmReachWeight = 0f;
    }

    private Vector3 GetCurrentFridgeHandleWorldPos()
    {
        if (fridgeDoorHandleTransform != null)
        {
            return fridgeDoorHandleTransform.position;
        }
        if (targetFridgeDoor != null)
        {
            return targetFridgeDoor.transform.position + Vector3.up * 0.05f;
        }
        return transform.position + CharacterForwardWorld * 0.55f + Vector3.up * 1.15f;
    }

    // =========================================================================
    // AUTHENTIC 3D COCA-COLA CAN BUILDER
    // =========================================================================

    private GameObject CreateAuthenticCocaColaCan()
    {
        // 1. Try loading prebuilt Resources prefab if present
        GameObject prefab = Resources.Load<GameObject>("CustomerCocaColaCan");
        if (prefab != null)
        {
            GameObject inst = Instantiate(prefab);
            inst.name = "Customer_CocaCola_Can";
            inst.transform.localScale = Vector3.one;
            return inst;
        }

        // 2. Extract genuine single Coca-Cola mesh & Coca-Cola_BaseColor material directly from stocked fridge!
        Mesh cokeMesh = null;
        Material cokeMat = null;

        if (targetFridgeTransform != null)
        {
            Transform cansCoke = targetFridgeTransform.Find("Drink_Cans/Cans_CocaCola");
            if (cansCoke != null)
            {
                MeshRenderer mr = cansCoke.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    cokeMat = mr.sharedMaterial;
                }

                MeshFilter mf = cansCoke.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
                {
                    cokeMesh = ExtractSingleCanFromCombinedFridgeMesh(mf.sharedMesh, 24);
                }
            }
        }

        // 3. Clean procedural cylinder fallback if mesh wasn't readable
        if (cokeMesh == null)
        {
            cokeMesh = RestockCardboardBox.CreateSharedCanMesh();
        }

        if (cokeMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            cokeMat = new Material(sh);
            cokeMat.color = new Color(0.82f, 0.08f, 0.10f);
        }

        GameObject canGo = new GameObject("Customer_CocaCola_Can");
        MeshFilter canMf = canGo.AddComponent<MeshFilter>();
        canMf.sharedMesh = cokeMesh;
        MeshRenderer canMr = canGo.AddComponent<MeshRenderer>();
        canMr.sharedMaterial = cokeMat;

        return canGo;
    }

    private static Mesh ExtractSingleCanFromCombinedFridgeMesh(Mesh combined, int instanceCount)
    {
        int totalVerts = combined.vertexCount;
        int[] allTris = combined.triangles;
        if (totalVerts < instanceCount * 12 || totalVerts % instanceCount != 0 || allTris.Length % instanceCount != 0)
        {
            return null;
        }

        int vCount = totalVerts / instanceCount;
        int tCount = allTris.Length / instanceCount;

        Vector3[] srcVerts = combined.vertices;
        Vector3[] srcNorms = combined.normals;
        Vector2[] srcUvs = combined.uv;

        Vector3[] dstVerts = new Vector3[vCount];
        Vector3[] dstNorms = (srcNorms != null && srcNorms.Length == totalVerts) ? new Vector3[vCount] : null;
        Vector2[] dstUvs = (srcUvs != null && srcUvs.Length == totalVerts) ? new Vector2[vCount] : null;

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int i = 0; i < vCount; i++)
        {
            Vector3 v = srcVerts[i];
            if (v.x < min.x) min.x = v.x;
            if (v.y < min.y) min.y = v.y;
            if (v.z < min.z) min.z = v.z;
            if (v.x > max.x) max.x = v.x;
            if (v.y > max.y) max.y = v.y;
            if (v.z > max.z) max.z = v.z;
        }

        // Recenter XZ to 0 and place bottom base at Y = 0
        Vector3 pivotOffset = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);

        for (int i = 0; i < vCount; i++)
        {
            dstVerts[i] = srcVerts[i] - pivotOffset;
            if (dstNorms != null) dstNorms[i] = srcNorms[i];
            if (dstUvs != null) dstUvs[i] = srcUvs[i];
        }

        int[] dstTris = new int[tCount];
        for (int i = 0; i < tCount; i++)
        {
            if (allTris[i] >= vCount) return null;
            dstTris[i] = allTris[i];
        }

        Mesh single = new Mesh();
        single.name = "RuntimeSingleCocaColaCan";
        single.vertices = dstVerts;
        if (dstNorms != null) single.normals = dstNorms;
        if (dstUvs != null) single.uv = dstUvs;
        single.triangles = dstTris;
        single.RecalculateBounds();
        if (dstNorms == null) single.RecalculateNormals();
        return single;
    }

    // =========================================================================
    // PROCEDURAL PS1 BONE RIGGING & ANIMATION (IMMUNE TO BONE ROLL)
    // =========================================================================

    private Vector3 CharacterRightWorld => transform.TransformDirection(modelRightLocal).normalized;
    private Vector3 CharacterForwardWorld => transform.TransformDirection(modelForwardLocal).normalized;

    private void CacheRigAndBindPose()
    {
        Quaternion savedRot = transform.rotation;
        transform.rotation = Quaternion.identity;

        Transform[] allBones = GetComponentsInChildren<Transform>(true);
        bindLocalRots.Clear();

        for (int i = 0; i < allBones.Length; i++)
        {
            Transform b = allBones[i];
            if (b == transform) continue;
            bindLocalRots[b] = b.localRotation;

            string n = b.name;
            if (n == "pelvis") pelvis = b;
            else if (n == "spine") spine = b;
            else if (n == "chest") chest = b;
            else if (n == "neck") neck = b;
            else if (n == "head") head = b;
            else if (n == "shoulder_left") shoulderL = b;
            else if (n == "upper_arm_left") upperArmL = b;
            else if (n == "forearm_left") forearmL = b;
            else if (n == "hand_left") handL = b;
            else if (n == "shoulder_right") shoulderR = b;
            else if (n == "upper_arm_right") upperArmR = b;
            else if (n == "forearm_right") forearmR = b;
            else if (n == "hand_right") handR = b;
            else if (n == "thigh_left") thighL = b;
            else if (n == "shin.L" || n == "shin_left") shinL = b;
            else if (n == "foot_left") footL = b;
            else if (n == "foot_left_end") footLEnd = b;
            else if (n == "thigh_right") thighR = b;
            else if (n == "shin.R" || n == "shin_right") shinR = b;
            else if (n == "foot_right") footR = b;
            else if (n == "foot_right_end") footREnd = b;
        }

        if (pelvis != null) pelvisBindLocalPos = pelvis.localPosition;

        if (footL != null) footLSoleOffset = Mathf.Max(0.01f, footL.position.y - transform.position.y);
        if (footLEnd != null) footLEndSoleOffset = Mathf.Max(0.005f, footLEnd.position.y - transform.position.y);
        if (footR != null) footRSoleOffset = Mathf.Max(0.01f, footR.position.y - transform.position.y);
        if (footREnd != null) footREndSoleOffset = Mathf.Max(0.005f, footREnd.position.y - transform.position.y);

        if (upperArmL != null && upperArmR != null)
        {
            Vector3 rVec = upperArmR.position - upperArmL.position;
            rVec.y = 0f;
            if (rVec.sqrMagnitude > 0.0001f)
            {
                modelRightLocal = rVec.normalized;
                modelForwardLocal = Vector3.Cross(modelRightLocal, Vector3.up).normalized;
            }

            // Verify forward against foot -> toe direction so character never walks backward
            if (footL != null && footLEnd != null)
            {
                Vector3 toe = footLEnd.position - footL.position;
                toe.y = 0f;
                if (toe.sqrMagnitude > 0.0001f && Vector3.Dot(toe.normalized, modelForwardLocal) < 0f)
                {
                    modelForwardLocal = -modelForwardLocal;
                    modelRightLocal = -modelRightLocal;
                }
            }

            float angleFromPlusZ = Vector3.SignedAngle(Vector3.forward, modelForwardLocal, Vector3.up);
            modelYawCorrectionDeg = -angleFromPlusZ;
            rigInitialized = true;
        }

        transform.rotation = savedRot;
    }

    private void LateUpdate()
    {
        if (!isNpcVisible) return;

        idleTimer += Time.deltaTime;

        if (rigInitialized)
        {
            ApplyProceduralPs1Animation();
        }

        // Keep carried Coca-Cola can / MamaChip locked to Steve's right palm at exact 1:1 world scale
        if (isCarryingCan && cocaColaCanObj != null && handR != null)
        {
            Vector3 cRight = CharacterRightWorld;
            Vector3 cFwd = CharacterForwardWorld;
            Vector3 palmWorld = handR.position + cFwd * 0.055f - cRight * 0.03f - Vector3.up * 0.058f;
            Quaternion canRot = Quaternion.LookRotation(cFwd, Vector3.up);
            cocaColaCanObj.transform.SetPositionAndRotation(palmWorld, canRot);
            cocaColaCanObj.transform.localScale = Vector3.one;
        }

        // Keep carried Umbrella locked to Steve's right hand
        if (isCarryingUmbrella && steveUmbrellaObj != null && handR != null)
        {
            Vector3 cRight = CharacterRightWorld;
            Vector3 cFwd = CharacterForwardWorld;
            Vector3 gripPos = handR.position + cFwd * 0.06f - cRight * 0.02f - Vector3.up * 0.08f;
            Quaternion umbRot = isUmbrellaOpen
                ? Quaternion.LookRotation(cFwd, (Vector3.up + cFwd * 0.08f).normalized)
                : Quaternion.LookRotation(cFwd, (Vector3.up + cFwd * 0.45f).normalized);
            steveUmbrellaObj.transform.SetPositionAndRotation(gripPos, umbRot);
            steveUmbrellaObj.transform.localScale = Vector3.one;
        }
    }

    private void ApplyProceduralPs1Animation()
    {
        // 1. Restore bind pose first
        foreach (var kvp in bindLocalRots)
        {
            if (kvp.Key != null) kvp.Key.localRotation = kvp.Value;
        }
        if (pelvis != null) pelvis.localPosition = pelvisBindLocalPos;

        Vector3 wRight = CharacterRightWorld;
        Vector3 wFwd = CharacterForwardWorld;

        // 2. Bring T-pose/A-pose arms down naturally beside Steve's torso
        if (upperArmL != null && handL != null)
        {
            Vector3 curDirL = (handL.position - upperArmL.position).normalized;
            Vector3 relaxDirL = (Vector3.down - wRight * 0.14f + wFwd * 0.05f).normalized;
            upperArmL.rotation = Quaternion.FromToRotation(curDirL, relaxDirL) * upperArmL.rotation;
        }
        if (upperArmR != null && handR != null)
        {
            Vector3 curDirR = (handR.position - upperArmR.position).normalized;
            Vector3 relaxDirR = (Vector3.down + wRight * 0.14f + wFwd * 0.05f).normalized;
            upperArmR.rotation = Quaternion.FromToRotation(curDirR, relaxDirR) * upperArmR.rotation;
        }
        if (forearmL != null) RotateBoneWorld(forearmL, wRight, -10f);
        if (forearmR != null) RotateBoneWorld(forearmR, wRight, -10f);

        // Quantize animation clock to 12 FPS for authentic PS1 low-quality retro motion
        float qWalk = Mathf.Floor(walkCycleTimer * (ps1AnimFps / 5.2f)) / (ps1AnimFps / 5.2f);
        float qIdle = Mathf.Floor(idleTimer * ps1AnimFps) / ps1AnimFps;

        if (isCurrentlyWalking)
        {
            float stride = Mathf.Sin(qWalk);
            float swingPass = Mathf.Cos(qWalk);
            float legAngle = stride * 25f;

            // Note: around +wRight, negative angle swings a downward limb FORWARD, positive swings BACKWARD
            if (thighL != null) RotateBoneWorld(thighL, wRight, -legAngle);
            if (thighR != null) RotateBoneWorld(thighR, wRight, legAngle);

            // Lift the swinging knee specifically at mid-swing (Cos) while keeping the planted stance leg straight
            float kneeBendL = Mathf.Max(0f, swingPass) * 32f;
            float kneeBendR = Mathf.Max(0f, -swingPass) * 32f;
            if (shinL != null) RotateBoneWorld(shinL, wRight, kneeBendL);
            if (shinR != null) RotateBoneWorld(shinR, wRight, kneeBendR);

            // Dorsiflex foot slightly upward when swinging forward so toes never scrape the floor
            if (footL != null) RotateBoneWorld(footL, wRight, -Mathf.Max(0f, swingPass) * 10f);
            if (footR != null) RotateBoneWorld(footR, wRight, -Mathf.Max(0f, -swingPass) * 10f);

            // Subtle PS1 torso bob & sway
            if (spine != null)
            {
                RotateBoneWorld(spine, wRight, -3.5f); // Slight forward walk lean
                RotateBoneWorld(spine, Vector3.up, stride * 4.0f);
            }

            // Opposite arm swing
            if (upperArmL != null) RotateBoneWorld(upperArmL, wRight, legAngle * 0.65f);
            if (forearmL != null) RotateBoneWorld(forearmL, wRight, -Mathf.Max(0f, -legAngle) * 0.45f);

            if (!isCarryingCan && !isCarryingUmbrella)
            {
                if (upperArmR != null) RotateBoneWorld(upperArmR, wRight, -legAngle * 0.65f);
                if (forearmR != null) RotateBoneWorld(forearmR, wRight, -Mathf.Max(0f, legAngle) * 0.45f);
            }
        }
        else
        {
            // Standing / Browsing idle animation
            float breath = Mathf.Sin(qIdle * 1.9f) * 1.8f;
            if (chest != null) RotateBoneWorld(chest, wRight, breath);

            bool isBrowsing = (currentState == NpcState.BrowsingShelf1Initial5s ||
                               currentState == NpcState.WaitingForPlayerTalkAtShelf1 ||
                               currentState == NpcState.BrowsingShelf1Post7s ||
                               currentState == NpcState.BrowsingFridge7s);

            if (isBrowsing && !lookAtPlayerHead && head != null)
            {
                float scanYaw = Mathf.Sin(qIdle * 1.15f) * 16f;
                float scanPitch = Mathf.Sin(qIdle * 0.75f) * 8f + 4f;
                RotateBoneWorld(head, Vector3.up, scanYaw);
                RotateBoneWorld(head, wRight, scanPitch);
            }
        }

        // Carry pose for right arm while holding Coca-Cola can / MamaChip
        if (isCarryingCan && rightArmReachWeight < 0.99f && upperArmR != null && forearmR != null)
        {
            RotateBoneWorld(upperArmR, wRight, -18f);
            RotateBoneWorld(forearmR, wRight, -64f);
            RotateBoneWorld(forearmR, Vector3.up, -14f);
        }

        // Carry pose for right arm while holding Umbrella overhead
        if (isCarryingUmbrella && rightArmReachWeight < 0.99f && upperArmR != null && forearmR != null)
        {
            RotateBoneWorld(upperArmR, wRight, -44f);
            RotateBoneWorld(forearmR, wRight, -62f);
            RotateBoneWorld(forearmR, Vector3.up, -18f);
        }

        // Head look-at player during conversations
        if (lookAtPlayerHead && head != null)
        {
            Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - head.position;
                Vector3 flatToCam = new Vector3(toCam.x, 0f, toCam.z);
                if (flatToCam.sqrMagnitude > 0.01f)
                {
                    float yaw = Mathf.Clamp(Vector3.SignedAngle(wFwd, flatToCam.normalized, Vector3.up), -52f, 52f);
                    RotateBoneWorld(head, Vector3.up, yaw);
                }
            }
        }

        // Procedural 2-bone arm reach towards target (fridge handle, Coca-Cola shelf, counter placement)
        if (rightArmReachWeight > 0.001f && upperArmR != null && forearmR != null && handR != null)
        {
            ApplyArmReach(upperArmR, forearmR, handR, rightArmWorldTarget, wRight, rightArmReachWeight);
        }
        if (leftArmReachWeight > 0.001f && upperArmL != null && forearmL != null && handL != null)
        {
            ApplyArmReach(upperArmL, forearmL, handL, leftArmWorldTarget, wRight, leftArmReachWeight);
        }

        // Prevent heels/toes from ever dipping below the floor plane during stride rotations
        if (pelvis != null)
        {
            float floorY = transform.position.y;
            float minSoleY = floorY;
            if (footL != null) minSoleY = Mathf.Min(minSoleY, footL.position.y - footLSoleOffset);
            if (footLEnd != null) minSoleY = Mathf.Min(minSoleY, footLEnd.position.y - footLEndSoleOffset);
            if (footR != null) minSoleY = Mathf.Min(minSoleY, footR.position.y - footRSoleOffset);
            if (footREnd != null) minSoleY = Mathf.Min(minSoleY, footREnd.position.y - footREndSoleOffset);

            if (minSoleY < floorY)
            {
                pelvis.position += Vector3.up * (floorY - minSoleY);
            }
        }
    }

    private static void RotateBoneWorld(Transform bone, Vector3 worldAxis, float angleDeg)
    {
        bone.rotation = Quaternion.AngleAxis(angleDeg, worldAxis) * bone.rotation;
    }

    private static void ApplyArmReach(
        Transform upperArm,
        Transform forearm,
        Transform hand,
        Vector3 worldTarget,
        Vector3 charRightWorld,
        float weight)
    {
        Quaternion baseUpper = upperArm.rotation;
        Quaternion baseFore = forearm.rotation;

        float upperLen = Vector3.Distance(upperArm.position, forearm.position);
        float foreLen = Vector3.Distance(forearm.position, hand.position);
        float maxReach = Mathf.Max(0.2f, upperLen + foreLen);

        Vector3 toTarget = worldTarget - upperArm.position;
        float dist = Mathf.Clamp(toTarget.magnitude, 0.12f, maxReach * 0.97f);
        Vector3 targetDir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.forward;

        // First align entire arm toward target
        Vector3 currentArmDir = (hand.position - upperArm.position).normalized;
        upperArm.rotation = Quaternion.FromToRotation(currentArmDir, targetDir) * upperArm.rotation;

        // Apply natural human elbow bend using law of cosines when target is closer than full arm extension
        if (upperLen > 0.02f && foreLen > 0.02f && dist < (upperLen + foreLen) * 0.96f)
        {
            float cosShoulder = Mathf.Clamp((upperLen * upperLen + dist * dist - foreLen * foreLen) / (2f * upperLen * dist), -1f, 1f);
            float shoulderPitchDown = Mathf.Acos(cosShoulder) * Mathf.Rad2Deg;

            float cosElbow = Mathf.Clamp((upperLen * upperLen + foreLen * foreLen - dist * dist) / (2f * upperLen * foreLen), -1f, 1f);
            float elbowFlex = 180f - (Mathf.Acos(cosElbow) * Mathf.Rad2Deg);

            // Drop elbow downward (+charRightWorld) and flex forearm upward (-charRightWorld) so hand meets worldTarget
            upperArm.rotation = Quaternion.AngleAxis(shoulderPitchDown, charRightWorld) * upperArm.rotation;
            forearm.rotation = Quaternion.AngleAxis(-elbowFlex, charRightWorld) * forearm.rotation;
        }

        upperArm.rotation = Quaternion.Slerp(baseUpper, upperArm.rotation, weight);
        forearm.rotation = Quaternion.Slerp(baseFore, forearm.rotation, weight);
    }

    // =========================================================================
    // PROCEDURAL RETRO HORROR SFX
    // =========================================================================

    private void SetupProceduralAudio()
    {
        npcAudioSource = GetComponent<AudioSource>();
        if (npcAudioSource == null)
        {
            npcAudioSource = gameObject.AddComponent<AudioSource>();
        }
        npcAudioSource.playOnAwake = false;
        npcAudioSource.spatialBlend = 0.82f;
        npcAudioSource.minDistance = 1.5f;
        npcAudioSource.maxDistance = 14f;

        int sr = 44100;

        // 1. Subtle low-fi PS1 footstep thud
        int stepSamples = (int)(sr * 0.075f);
        float[] stepBuf = new float[stepSamples];
        for (int i = 0; i < stepSamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 58f);
            float low = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.65f;
            float grit = (Random.value * 2f - 1f) * 0.35f;
            stepBuf[i] = (low + grit) * env * 0.6f;
        }
        footstepClip = AudioClip.Create("SteveFootstep", stepSamples, 1, sr, false);
        footstepClip.SetData(stepBuf, 0);

        // 2. Aluminum soda can set-down clink
        int canSamples = (int)(sr * 0.09f);
        float[] canBuf = new float[canSamples];
        for (int i = 0; i < canSamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 65f);
            float ring = Mathf.Sin(2f * Mathf.PI * 1420f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 410f * t) * 0.45f;
            float tap = (Random.value * 2f - 1f) * Mathf.Exp(-t * 140f) * 0.4f;
            canBuf[i] = (ring + tap) * env * 0.7f;
        }
        canPlaceClip = AudioClip.Create("CokeCanPlace", canSamples, 1, sr, false);
        canPlaceClip.SetData(canBuf, 0);

        // 3. Crisp supermarket barcode scanner BEEP (2450 Hz)
        int beepSamples = (int)(sr * 0.11f);
        float[] beepBuf = new float[beepSamples];
        for (int i = 0; i < beepSamples; i++)
        {
            float t = (float)i / sr;
            float env = i < sr * 0.09f ? 1f : Mathf.Clamp01(1f - (t - 0.09f) / 0.02f);
            beepBuf[i] = Mathf.Sin(2f * Mathf.PI * 2480f * t) * env * 0.45f;
        }
        scanBeepClip = AudioClip.Create("RegisterScanBeep", beepSamples, 1, sr, false);
        scanBeepClip.SetData(beepBuf, 0);

        // 4. Subtle cash bill / coin chime
        int paySamples = (int)(sr * 0.16f);
        float[] payBuf = new float[paySamples];
        for (int i = 0; i < paySamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 28f);
            float rustle = (Random.value * 2f - 1f) * 0.28f;
            float chime = Mathf.Sin(2f * Mathf.PI * 1960f * t) * 0.25f;
            payBuf[i] = (rustle + chime) * env * 0.65f;
        }
        cashPayClip = AudioClip.Create("CashPayment", paySamples, 1, sr, false);
        cashPayClip.SetData(payBuf, 0);

        // 5. Fridge rubber door seal whoosh
        int sealSamples = (int)(sr * 0.12f);
        float[] sealBuf = new float[sealSamples];
        for (int i = 0; i < sealSamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 36f);
            sealBuf[i] = ((Random.value * 2f - 1f) * 0.45f + Mathf.Sin(2f * Mathf.PI * 160f * t) * 0.35f) * env * 0.5f;
        }
        fridgeSealClip = AudioClip.Create("FridgeSeal", sealSamples, 1, sr, false);
        fridgeSealClip.SetData(sealBuf, 0);
    }

    private void PlayOneShotClip(AudioClip clip, float volume, float pitch)
    {
        if (npcAudioSource == null || clip == null) return;
        npcAudioSource.pitch = pitch;
        npcAudioSource.PlayOneShot(clip, volume);
    }
}

/// <summary>
/// Attached to the Coca-Cola can placed by Steve onto InvisibleNPCItem so the player can press E to Scan.
/// </summary>
public class ScannableCounterCan : MonoBehaviour, IInteractable
{
    private CustomerNpcSteve ownerNpc;
    private BoxCollider scanCollider;
    private bool canBeScanned = false;

    public bool IsScannable => canBeScanned;

    public void Initialize(CustomerNpcSteve owner)
    {
        ownerNpc = owner;
        scanCollider = GetComponent<BoxCollider>();
        if (scanCollider == null)
        {
            scanCollider = gameObject.AddComponent<BoxCollider>();
        }
        // Slightly generous collider around the 355ml can so aiming at it on the counter feels crisp and responsive
        scanCollider.center = new Vector3(0f, 0.08f, 0f);
        scanCollider.size = new Vector3(0.22f, 0.24f, 0.22f);
        scanCollider.isTrigger = true;
        canBeScanned = false;
    }

    public void SetScannable(bool scannable)
    {
        canBeScanned = scannable;
    }

    public void Interact()
    {
        if (!canBeScanned || NightShiftStoryController.IsDialogueActive) return;
        if (ownerNpc != null && !ownerNpc.IsPlayerInsideCashierZone) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > 2.65f)
        {
            return;
        }

        canBeScanned = false;
        if (ownerNpc != null)
        {
            ownerNpc.NotifyCanScanned();
        }
    }

    public string GetPromptText()
    {
        if (!canBeScanned || NightShiftStoryController.IsDialogueActive) return string.Empty;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > 2.65f)
        {
            return string.Empty;
        }

        if (ownerNpc != null && !ownerNpc.IsPlayerInsideCashierZone)
        {
            return "Go to Cashier area first.";
        }

        return "E to Scan";
    }
}


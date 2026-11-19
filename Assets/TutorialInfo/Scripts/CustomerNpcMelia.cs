using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls "Melia" (MeliaNPC): PS1-style procedural walking/browsing/hand animations,
/// obstacle-free navigation into the gas station store, grabbing one GoChips Banana Chips bag
/// from the other shelf (Shelf2), opening the other Double Refrigerator (zero door bump &
/// blocking player fridge interaction), picking up a Sprite can, walking to the counter,
/// placing both items on the counter, full dialogue sequence with the player, scanning
/// both items, $12 payment exchange, and departure back to her spawnpoint.
/// </summary>
public class CustomerNpcMelia : MonoBehaviour, IInteractable
{
    public static CustomerNpcMelia Instance { get; private set; }

    public enum NpcState
    {
        HiddenWaiting,
        WalkingToOtherShelf,
        BrowsingOtherShelf,
        PickingUpGoChips,
        WalkingToOtherFridge,
        OpeningFridgeDoor,
        BrowsingFridge,
        PickingUpSpriteCan,
        ClosingFridgeDoor,
        WalkingToCounter,
        Waiting1sBeforeAssistTask,
        WaitingForPlayerCashierTalk,
        TalkingAndPlacingItems,
        WaitingForPlayerScanTwoItems,
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

    public bool IsNpcActiveInWorld => isNpcVisible && gameObject.activeInHierarchy;

    public bool IsInteractingWithFridge =>
        currentState == NpcState.OpeningFridgeDoor ||
        currentState == NpcState.BrowsingFridge ||
        currentState == NpcState.PickingUpSpriteCan ||
        currentState == NpcState.ClosingFridgeDoor;

    public static bool IsNpcUsingFridge(InteractableFridgeDoor door)
    {
        if (door != null && door.isLockedByNpc) return true;
        if (Instance == null || !Instance.IsNpcActiveInWorld) return false;
        if (Instance.IsInteractingWithFridge) return true;

        if (Instance.currentState == NpcState.WalkingToOtherFridge && Instance.targetFridgeTransform != null)
        {
            float dist = Vector3.Distance(Instance.transform.position, Instance.targetFridgeTransform.position);
            if (dist <= 2.25f) return true;
        }
        return false;
    }

    public bool ShouldShowCashierIndicator =>
        (currentState == NpcState.WaitingForPlayerCashierTalk ||
         currentState == NpcState.WaitingForPlayerScanTwoItems ||
         currentState == NpcState.WaitingForPlayerCashierPCCheckout) &&
        !IsPlayerInsideCashierZone;

    public Transform PlayerCashierSpotTransform => playerNpcInteractTransform;

    public int ScannedItemsCount => scannedItemsCount;
    public int TotalItemsToScan => isAct4Mode ? 1 : 2;

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
    private Transform otherShelfTransform;
    private Transform shelf3Transform;
    private Transform shelf1Transform;
    private Transform targetFridgeTransform;
    private InteractableFridgeDoor targetFridgeDoor;
    private Transform fridgeDoorHandleTransform;
    private AutomaticSlidingDoors slidingDoors;
    private Transform invisibleNpcItemTransform;
    private Transform playerNpcInteractTransform;

    // Colliders & Renderers
    private CapsuleCollider npcCapsule;
    private Renderer[] npcRenderers;
    private bool isNpcVisible = false;

    // Carried & Counter Props (GoChips pouch & Sprite can)
    private GameObject shelfDisplayGoChipsObj;
    private GameObject goChipsBagObj;
    private GameObject spriteCanObj;
    private ScannableMeliaCounterItem scannableChips;
    private ScannableMeliaCounterItem scannableSprite;
    private bool isCarryingChips = false;
    private bool isCarryingSprite = false;

    // Act 4 (Shift 2) HersheyShelve & Hershey ($5) support
    public bool isAct4Mode = false;
    public bool IsAct4CheckoutCompleted { get; private set; } = false;
    private Transform hersheyShelveTransform;
    private GameObject sceneHersheyTemplate;

    private static Mesh cachedGoChipsMesh;
    private static Material cachedGoChipsMaterial;

    // Rigging & Bone Transforms
    private bool rigInitialized = false;
    private Transform pelvis;
    private Transform spine;
    private Transform chest;
    private Transform neck;
    private Transform head;
    private Transform shoulderL;
    private Transform upperArmL;
    private Transform forearmL;
    private Transform handL;
    private Transform shoulderR;
    private Transform upperArmR;
    private Transform forearmR;
    private Transform handR;
    private Transform thighL;
    private Transform shinL;
    private Transform footL;
    private Transform footLEnd;
    private Transform thighR;
    private Transform shinR;
    private Transform footR;
    private Transform footREnd;

    private readonly Dictionary<Transform, Quaternion> bindLocalRots = new Dictionary<Transform, Quaternion>();
    private Vector3 pelvisBindLocalPos;
    private float footLSoleOffset = 0.09f;
    private float footLEndSoleOffset = 0.03f;
    private float footRSoleOffset = 0.09f;
    private float footREndSoleOffset = 0.03f;

    // Model-space axis calibration
    private Vector3 modelRightLocal = Vector3.right;
    private Vector3 modelForwardLocal = Vector3.forward;
    private float modelYawCorrectionDeg = 0f;

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
    private AudioClip itemPlaceClip;
    private AudioClip bagRustleClip;
    private AudioClip scanBeepClip;
    private AudioClip cashPayClip;
    private AudioClip fridgeSealClip;

    // Sequence synchronization flags
    private bool counterTalkTriggered = false;
    private int scannedItemsCount = 0;

    public static CustomerNpcMelia EnsureAttachedToSceneNpc()
    {
        if (Instance != null) return Instance;

        CustomerNpcMelia existing = Object.FindAnyObjectByType<CustomerNpcMelia>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        GameObject npcGo = null;

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t != null && string.Equals(t.name.Trim(), "MeliaNPC", System.StringComparison.OrdinalIgnoreCase))
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
                if (t != null && t.name.IndexOf("Melia", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    npcGo = t.gameObject;
                    break;
                }
            }
        }

        if (npcGo != null)
        {
            npcGo.SetActive(true);
            Instance = npcGo.AddComponent<CustomerNpcMelia>();
            return Instance;
        }

        return null;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;

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
        SpawnShelfDisplayGoChips();

        // Hide Melia at her spawnpoint until triggered 4 seconds after the shelf inspection
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
        npcCapsule.height = 1.72f / sy;
        npcCapsule.radius = 0.28f / sx;
        npcCapsule.center = new Vector3(0f, (0.86f / sy), 0f);
        npcCapsule.isTrigger = false;
    }

    private void ResolveSceneTargets()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);

        // 1. Find Shelf2 (the other shelf at Z = -0.09), Shelf3 (middle shelf at Z = -2.05), and Shelf1 (south shelf at Z = -4.33)
        Transform shelf1Fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "Shelf2", System.StringComparison.OrdinalIgnoreCase))
            {
                otherShelfTransform = t;
            }
            else if (string.Equals(nm, "Shelf3", System.StringComparison.OrdinalIgnoreCase))
            {
                shelf3Transform = t;
            }
            else if (string.Equals(nm, "Shelf1", System.StringComparison.OrdinalIgnoreCase))
            {
                if (shelf1Transform == null || t.position.z < -2.0f)
                {
                    shelf1Transform = t;
                }
                shelf1Fallback = t;
            }
        }
        if (otherShelfTransform == null)
        {
            otherShelfTransform = shelf1Fallback;
        }

        // 2. Sliding Entrance Doors
        slidingDoors = Object.FindAnyObjectByType<AutomaticSlidingDoors>();

        // 3. The OTHER Double Beverage Refrigerator (prefer "Double_Beverage_Fridge" at Z = -2.435 without "(1)")
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            if (t.name.StartsWith("Double_Beverage_Fridge", System.StringComparison.OrdinalIgnoreCase))
            {
                bool isOtherFridge = !t.name.Contains("(1)");
                if (targetFridgeTransform == null || isOtherFridge)
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
                        if (isOtherFridge) break;
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
    }

    private void SpawnShelfDisplayGoChips()
    {
        if (shelfDisplayGoChipsObj != null) return;
        ComputeOtherShelfPositions(out _, out _, out Vector3 chipsReachPos);
        shelfDisplayGoChipsObj = CreateGoChipsBag("Shelf_GoChips_Display");
        shelfDisplayGoChipsObj.transform.position = chipsReachPos;
        shelfDisplayGoChipsObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        shelfDisplayGoChipsObj.transform.localScale = Vector3.one;
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

    private void SetNpcVisible(bool visible)
    {
        isNpcVisible = visible;
        if (npcRenderers == null) npcRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < npcRenderers.Length; i++)
        {
            if (npcRenderers[i] != null)
            {
                npcRenderers[i].enabled = visible;
            }
        }
        if (npcCapsule != null)
        {
            npcCapsule.enabled = visible;
        }
    }

    public Coroutine StartCustomerVisit()
    {
        return StartCoroutine(CustomerSequenceRoutine());
    }

    private IEnumerator CustomerSequenceRoutine()
    {
        // 1. Spawn at Melia's spawnpoint and walk into the gas station store
        transform.position = spawnPosition;
        SnapToGroundImmediate();
        SetNpcVisible(true);
        isCarryingChips = false;
        isCarryingSprite = false;
        scannedItemsCount = 0;

        currentState = NpcState.WalkingToOtherShelf;
        ComputeOtherShelfPositions(out Vector3 shelfStandPos, out Vector3 shelfLookTarget, out Vector3 chipsReachPos);

        List<Vector3> pathToShelf = BuildObstacleFreePathToOtherShelf(transform.position, shelfStandPos);
        yield return StartCoroutine(FollowWaypointsRoutine(pathToShelf, checkEntranceDoorway: true));

        // 2. Face the other shelf, browse briefly, and grab one GoChips bag
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(shelfLookTarget, 0.42f));
        currentState = NpcState.BrowsingOtherShelf;
        yield return new WaitForSeconds(2.6f);

        currentState = NpcState.PickingUpGoChips;
        yield return StartCoroutine(PickUpGoChipsFromShelfRoutine(chipsReachPos));

        // 3. Walk to the other Double Refrigerator, open door (zero bump & lock player interaction), pick up Sprite can, close door
        currentState = NpcState.WalkingToOtherFridge;
        ComputeFridgeChoreographyPoints(
            out Vector3 fridgeApproachPos,
            out Vector3 fridgeStepBackSafePos,
            out Vector3 fridgeBrowsePos,
            out Vector3 fridgeLookTarget,
            out Vector3 spriteShelfReachPos);

        // Route cleanly down the center of the wide east aisle (X = 3.58f, 0.63m east of Shelf3's +2.955m edge and 0.67m west of the fridge/wall shelves)
        List<Vector3> pathToFridge = new List<Vector3>
        {
            new Vector3(3.58f, transform.position.y, shelfStandPos.z),
            new Vector3(3.58f, transform.position.y, fridgeApproachPos.z),
            fridgeApproachPos
        };
        yield return StartCoroutine(FollowWaypointsRoutine(pathToFridge, checkEntranceDoorway: false));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(fridgeLookTarget, 0.40f));

        SetFridgeDoorsLockedByNpc(true);
        currentState = NpcState.OpeningFridgeDoor;
        yield return StartCoroutine(OpenFridgeWithStepBackRoutine(fridgeStepBackSafePos, fridgeLookTarget));

        yield return StartCoroutine(MoveDirectlyToSpotRoutine(fridgeBrowsePos, fridgeLookTarget, slowWalkSpeed * 0.80f));
        currentState = NpcState.BrowsingFridge;
        yield return new WaitForSeconds(2.8f);

        currentState = NpcState.PickingUpSpriteCan;
        yield return StartCoroutine(PickUpSpriteFromFridgeRoutine(spriteShelfReachPos));

        currentState = NpcState.ClosingFridgeDoor;
        yield return StartCoroutine(CloseFridgeDoorCleanlyRoutine(fridgeStepBackSafePos, fridgeLookTarget));
        SetFridgeDoorsLockedByNpc(false);

        // 4. Walk to the checkout counter along the center of the east aisle (X = 3.55f) so she never clips through Shelf3 or Shelf1
        currentState = NpcState.WalkingToCounter;
        ComputeCheckoutPositions(out Vector3 counterStandPos, out Vector3 counterItemSurfacePos, out Vector3 cashierLookPos);

        List<Vector3> pathToCounter = new List<Vector3>
        {
            new Vector3(3.55f, transform.position.y, transform.position.z),
            new Vector3(3.55f, transform.position.y, -5.25f),
            new Vector3(counterStandPos.x, transform.position.y, Mathf.Min(-5.25f, counterStandPos.z + 0.35f)),
            counterStandPos
        };
        yield return StartCoroutine(FollowWaypointsRoutine(pathToCounter, checkEntranceDoorway: false, speedMultiplier: 0.92f));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(cashierLookPos, 0.42f));

        // 5. 1-second cooldown, then show "Assist the Customer" and allow E to interact
        currentState = NpcState.Waiting1sBeforeAssistTask;
        yield return new WaitForSeconds(1.0f);

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

        // 6. Dialogue & placing both items on the counter
        currentState = NpcState.TalkingAndPlacingItems;
        lookAtPlayerHead = true;

        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Melia: Just these two, please."));
        }

        // Melia places the two selected items (GoChips & Sprite can) on the counter
        yield return StartCoroutine(PlaceTwoItemsOnCounterRoutine(counterItemSurfacePos));

        // [1-second cooldown]
        yield return new WaitForSeconds(1.0f);

        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Melia: Oh, and you must be the new employee, right?"));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Melia: I heard the previous employee disappeared without a trace."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Melia: Nobody really knows what happened One day, he was working here, and the next... he was gone."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: I don't know much about it, It's still my second time working here."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Melia: Anyway, I probably shouldn't be telling you this on your second night."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: No, it's actually a head start, thank you."));
            yield return new WaitForSeconds(2.0f);
        }

        // 7. Task to Scan the Two Items
        scannedItemsCount = 0;
        currentState = NpcState.WaitingForPlayerScanTwoItems;
        InteractableCashierPC cashierPc = InteractableCashierPC.EnsureAttachedToScenePC();
        if (cashierPc != null)
        {
            cashierPc.BeginCustomerTransaction("Melia");
        }
        if (scannableChips != null) scannableChips.SetScannable(true);
        if (scannableSprite != null) scannableSprite.SetScannable(true);

        if (NightShiftStoryController.Instance != null)
        {
            NightShiftStoryController.Instance.NotifyCustomerTaskUpdated();
        }

        while (scannedItemsCount < 2)
        {
            yield return null;
        }

        // 8. Post-scan checkout dialogues, $12 payment, and CashierPC Checkout ("E to Checkout")
        currentState = NpcState.PostScanCheckoutSequence;
        yield return new WaitForSeconds(0.65f);

        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: Alright, that would be 12$ please."));
            PlayOneShotClip(cashPayClip, 0.58f, 1.0f);
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Melia:Oh Here's 12$."));
        }

        if (cashierPc == null) cashierPc = InteractableCashierPC.EnsureAttachedToScenePC();
        if (cashierPc != null)
        {
            cashierPc.SetReadyForCheckout("Melia", 12.00f);
            currentState = NpcState.WaitingForPlayerCashierPCCheckout;
            while (!cashierPc.IsCheckoutCompleted)
            {
                yield return null;
            }
            cashierPc.ResetToIdleScreen();
        }

        // Both scanned items disappear from the counter once CashierPC checkout completes
        if (goChipsBagObj != null)
        {
            Destroy(goChipsBagObj);
            goChipsBagObj = null;
            scannableChips = null;
        }
        if (spriteCanObj != null)
        {
            Destroy(spriteCanObj);
            spriteCanObj = null;
            scannableSprite = null;
        }

        // [2-second cooldown]
        yield return new WaitForSeconds(2.0f);

        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Melia: Just be careful, okay? Strange things have been happening around here lately."));
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: Thanks again.. I guess..?"));
        }
        lookAtPlayerHead = false;

        // 9. Customer Melia leaves the store walking back towards her spawnpoint
        currentState = NpcState.WalkingBackToSpawn;
        if (NightShiftStoryController.Instance != null)
        {
            NightShiftStoryController.Instance.NotifyCustomerTaskUpdated();
        }

        List<Vector3> exitPath = BuildObstacleFreeExitPathToSpawn(transform.position, spawnPosition);
        yield return StartCoroutine(FollowWaypointsRoutine(exitPath, checkEntranceDoorway: true));

        SetNpcVisible(false);
        currentState = NpcState.DepartedHidden;
    }

    // =========================================================================
    // ACT 4 (SHIFT 2) CUSTOMER VISIT: HERSHEYSHELVE -> HERSHEY ($5) -> MORTUUS ES
    // =========================================================================

    public Coroutine StartAct4CustomerVisit()
    {
        isAct4Mode = true;
        IsAct4CheckoutCompleted = false;
        return StartCoroutine(Act4CustomerSequenceRoutine());
    }

    private void ResolveAct4Targets()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            string nm = t.name.Trim();

            if (hersheyShelveTransform == null &&
                (string.Equals(nm, "HersheyShelve", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(nm, "Hershey_Shelve", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(nm, "HersheyShelf", System.StringComparison.OrdinalIgnoreCase)))
            {
                hersheyShelveTransform = t;
            }

            if (sceneHersheyTemplate == null &&
                (string.Equals(nm, "Hershey", System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(nm, "Hersheys", System.StringComparison.OrdinalIgnoreCase) ||
                 (nm.IndexOf("Hershey", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                  nm.IndexOf("Shelv", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                  nm.IndexOf("Shelf", System.StringComparison.OrdinalIgnoreCase) < 0)))
            {
                sceneHersheyTemplate = t.gameObject;
            }
        }

        if (hersheyShelveTransform == null)
        {
            hersheyShelveTransform = otherShelfTransform != null ? otherShelfTransform : shelf3Transform;
        }
    }

    private IEnumerator Act4CustomerSequenceRoutine()
    {
        ResolveSceneTargets();
        ResolveAct4Targets();

        transform.position = spawnPosition;
        SnapToGroundImmediate();
        SetNpcVisible(true);
        isCarryingChips = false;
        isCarryingSprite = false;
        scannedItemsCount = 0;
        IsAct4CheckoutCompleted = false;

        // 1. Walk into the store and go accurately to HersheyShelve via the wide-open West, North, and East aisles
        currentState = NpcState.WalkingToOtherShelf;
        ComputeHersheyShelfPositions(out Vector3 shelfStandPos, out Vector3 shelfLookTarget, out Vector3 hersheyReachPos);

        float doorZ = slidingDoors != null ? slidingDoors.transform.position.z : -1.60f;
        float doorX = slidingDoors != null ? slidingDoors.transform.position.x : -1.77f;

        List<Vector3> pathToShelf = new List<Vector3>();
        if (transform.position.x < -22f)
        {
            pathToShelf.Add(new Vector3(Mathf.Clamp(transform.position.x, -30.8f, -29.8f), transform.position.y, -13.20f));
            pathToShelf.Add(new Vector3(-25.50f, 0.02f, -12.80f));
        }
        pathToShelf.Add(new Vector3(-11.50f, 0.08f, -12.00f));
        pathToShelf.Add(new Vector3(-5.60f, 0.14f, -9.80f));
        pathToShelf.Add(new Vector3(-4.80f, 0.14f, -4.80f));
        pathToShelf.Add(new Vector3(doorX - 1.35f, 0.15f, doorZ));
        pathToShelf.Add(new Vector3(-0.72f, 0.17f, doorZ));
        if (shelfStandPos.x > 3.0f)
        {
            // Route cleanly up the West Aisle (-0.72) and across the North Aisle (Z = +1.02) into the East Aisle (X = 3.48)
            pathToShelf.Add(new Vector3(-0.72f, 0.17f, 1.02f));
            pathToShelf.Add(new Vector3(shelfStandPos.x, 0.17f, 1.02f));
        }
        else
        {
            pathToShelf.Add(new Vector3(-0.72f, 0.17f, shelfStandPos.z));
        }
        pathToShelf.Add(shelfStandPos);

        yield return StartCoroutine(FollowWaypointsRoutine(pathToShelf, checkEntranceDoorway: true));

        // 2. Browse HersheyShelve for 5 seconds, then grab Hershey chocolate
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(shelfLookTarget, 0.42f));
        currentState = NpcState.BrowsingOtherShelf;
        yield return new WaitForSeconds(5.0f);

        currentState = NpcState.PickingUpGoChips;
        float t = 0f;
        while (t < 0.48f)
        {
            t += Time.deltaTime;
            leftArmReachWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.48f));
            leftArmWorldTarget = hersheyReachPos;
            yield return null;
        }

        if (shelfDisplayGoChipsObj != null)
        {
            shelfDisplayGoChipsObj.SetActive(false);
        }
        goChipsBagObj = CreateHersheyChocolateItem();
        if (sceneHersheyTemplate != null)
        {
            sceneHersheyTemplate.SetActive(false);
        }
        isCarryingChips = true;
        PlayOneShotClip(bagRustleClip, 0.42f, 1.04f);

        yield return new WaitForSeconds(0.16f);

        t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            leftArmReachWeight = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / 0.45f));
            leftArmWorldTarget = hersheyReachPos;
            yield return null;
        }
        leftArmReachWeight = 0f;

        // 3. Walk South down the East Aisle to the counter and place the Hershey chocolate bar on the counter
        currentState = NpcState.WalkingToCounter;
        ComputeCheckoutPositions(out Vector3 counterStandPos, out Vector3 counterItemSurfacePos, out Vector3 cashierLookPos);

        float eastAisleX = Mathf.Clamp(shelfStandPos.x, 3.45f, 3.55f);
        List<Vector3> pathToCounter = new List<Vector3>
        {
            new Vector3(eastAisleX, 0.17f, -2.50f),
            new Vector3(eastAisleX, 0.17f, -5.20f),
            new Vector3(counterStandPos.x, 0.17f, Mathf.Min(-5.20f, counterStandPos.z + 0.35f)),
            counterStandPos
        };
        yield return StartCoroutine(FollowWaypointsRoutine(pathToCounter, checkEntranceDoorway: false, speedMultiplier: 0.92f));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(cashierLookPos, 0.42f));

        // Place Hershey chocolate on the counter immediately upon arriving
        Vector3 leftHandTarget = counterItemSurfacePos + Vector3.up * 0.08f;
        t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            leftArmReachWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.45f));
            leftArmWorldTarget = leftHandTarget;
            yield return null;
        }

        isCarryingChips = false;
        if (goChipsBagObj != null)
        {
            goChipsBagObj.transform.position = counterItemSurfacePos;
            goChipsBagObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            goChipsBagObj.transform.localScale = Vector3.one;

            scannableChips = goChipsBagObj.GetComponent<ScannableMeliaCounterItem>();
            if (scannableChips == null)
            {
                scannableChips = goChipsBagObj.AddComponent<ScannableMeliaCounterItem>();
            }
            scannableChips.Initialize(this, "E to Scan (Hershey)");
        }
        PlayOneShotClip(bagRustleClip, 0.48f, 0.98f);

        t = 0f;
        while (t < 0.42f)
        {
            t += Time.deltaTime;
            leftArmReachWeight = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / 0.42f));
            leftArmWorldTarget = leftHandTarget;
            yield return null;
        }
        leftArmReachWeight = 0f;

        // 4. Task: Check out Melia (wait for player interaction while standing on PlayerNpcInteract)
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

        // 5. Act 4 Melia Dialogue Sequence
        currentState = NpcState.TalkingAndPlacingItems;
        lookAtPlayerHead = true;

        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Melia: Hello again, let me buy this."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: Oh it's you again, how have you been?"));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Melia: ..."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: Umm.. alright that will be 5$.."));
            yield return new WaitForSeconds(2.0f);
        }

        // 6. Scan item & checkout on CashierPC ($5)
        scannedItemsCount = 0;
        currentState = NpcState.WaitingForPlayerScanTwoItems;
        InteractableCashierPC cashierPc = InteractableCashierPC.EnsureAttachedToScenePC();
        if (cashierPc != null)
        {
            cashierPc.BeginCustomerTransaction("Melia");
            cashierPc.RecordScannedItem("4815-HERS-BAR", "Hershey Chocolate Bar", 5.00f, "Melia");
            cashierPc.SetReadyForCheckout("Melia", 5.00f);
        }
        if (scannableChips != null) scannableChips.SetScannable(true);

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

        IsAct4CheckoutCompleted = true;

        if (goChipsBagObj != null)
        {
            Destroy(goChipsBagObj);
            goChipsBagObj = null;
            scannableChips = null;
        }

        // 7. Post-checkout Latin line ("Melia: Mortuus es.")
        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("Melia: Mortuus es."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: Huh..? I don't understand sorry.."));
            yield return new WaitForSeconds(2.0f);
        }
        lookAtPlayerHead = false;

        // 8. Melia leaves the store as usual and disappears
        currentState = NpcState.WalkingBackToSpawn;
        if (NightShiftStoryController.Instance != null)
        {
            NightShiftStoryController.Instance.NotifyCustomerTaskUpdated();
        }

        List<Vector3> exitPath = BuildObstacleFreeExitPathToSpawn(transform.position, spawnPosition);
        yield return StartCoroutine(FollowWaypointsRoutine(exitPath, checkEntranceDoorway: true));

        SetNpcVisible(false);
        currentState = NpcState.DepartedHidden;
    }

    private void ComputeHersheyShelfPositions(out Vector3 standPos, out Vector3 lookTarget, out Vector3 reachPos)
    {
        if (hersheyShelveTransform != null && hersheyShelveTransform != otherShelfTransform)
        {
            Bounds b = GetShelfBounds(
                hersheyShelveTransform,
                new Vector3(4.10f, 0.15f, -0.35f),
                new Vector3(4.60f, 1.50f, 0.45f));

            // HersheyShelve is on the East Wall (X ~ 4.10 .. 4.60, Z ~ -0.35 .. +0.45), facing West (-X) into the East Aisle (X = 3.48)
            if (b.center.x > 3.0f)
            {
                float standZ = Mathf.Clamp(b.center.z, -0.45f, 1.85f);
                float standX = Mathf.Clamp(b.min.x - 0.60f, 3.44f, 3.55f);
                standPos = new Vector3(standX, 0.17f, standZ);
                lookTarget = new Vector3(b.center.x, 1.15f, standZ);
                reachPos = new Vector3(Mathf.Max(standX + 0.45f, b.min.x - 0.05f), 1.12f, standZ);
                return;
            }

            lookTarget = new Vector3(b.center.x, 1.15f, b.center.z);
            float fallbackStandX = Mathf.Clamp(b.center.x, 0.55f, 2.35f);
            float fallbackStandZ = Mathf.Clamp(b.min.z - 0.65f, -5.20f, 1.10f);
            standPos = new Vector3(fallbackStandX, 0.17f, fallbackStandZ);
            reachPos = new Vector3(fallbackStandX, 1.12f, b.min.z + 0.08f);
            return;
        }

        ComputeOtherShelfPositions(out standPos, out lookTarget, out reachPos);
    }

    private GameObject CreateHersheyChocolateItem()
    {
        if (sceneHersheyTemplate != null)
        {
            GameObject wrapper = new GameObject("Customer_Hershey_Chocolate");
            GameObject clone = Instantiate(sceneHersheyTemplate);
            clone.name = "Hershey_Mesh";
            clone.SetActive(true);
            Collider[] cols = clone.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

            clone.transform.position = Vector3.zero;
            clone.transform.rotation = sceneHersheyTemplate.transform.rotation;
            clone.transform.localScale = sceneHersheyTemplate.transform.lossyScale;

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

        GameObject barGo = new GameObject("Customer_Hershey_Chocolate");
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material wrapMat = new Material(sh) { color = new Color(0.24f, 0.11f, 0.07f) };
        Material foilMat = new Material(sh) { color = new Color(0.82f, 0.84f, 0.86f) };

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "HersheyWrapper";
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(barGo.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.012f, 0f);
        body.transform.localScale = new Vector3(0.085f, 0.018f, 0.165f);
        body.GetComponent<Renderer>().sharedMaterial = wrapMat;

        GameObject foil = GameObject.CreatePrimitive(PrimitiveType.Cube);
        foil.name = "HersheySilverFoil";
        Destroy(foil.GetComponent<Collider>());
        foil.transform.SetParent(barGo.transform, false);
        foil.transform.localPosition = new Vector3(0f, 0.013f, 0.065f);
        foil.transform.localScale = new Vector3(0.080f, 0.019f, 0.036f);
        foil.GetComponent<Renderer>().sharedMaterial = foilMat;

        return barGo;
    }

    // =========================================================================
    // IINTERACTABLE IMPLEMENTATION (ASSIST CUSTOMER AT COUNTER)
    // =========================================================================

    public void Interact()
    {
        if (NightShiftStoryController.IsDialogueActive) return;

        if (currentState == NpcState.WaitingForPlayerCashierTalk)
        {
            if (!IsPlayerInsideCashierZone) return;
            counterTalkTriggered = true;
        }
    }

    public string GetPromptText()
    {
        if (NightShiftStoryController.IsDialogueActive) return string.Empty;

        if (currentState == NpcState.WaitingForPlayerCashierTalk)
        {
            if (!IsPlayerInsideCashierZone) return "Go to Cashier area first.";
            return "E to interact";
        }

        return string.Empty;
    }

    public void NotifyCounterItemScanned(ScannableMeliaCounterItem scannedItem)
    {
        if (currentState != NpcState.WaitingForPlayerScanTwoItems && currentState != NpcState.WaitingForPlayerCashierPCCheckout) return;
        PlayOneShotClip(scanBeepClip, 0.74f, 1.0f);
        scannedItemsCount = Mathf.Clamp(scannedItemsCount + 1, 0, TotalItemsToScan);

        InteractableCashierPC cashierPc = InteractableCashierPC.EnsureAttachedToScenePC();
        if (cashierPc != null)
        {
            if (isAct4Mode)
            {
                if (cashierPc.GetTotalItemCount() == 0)
                {
                    cashierPc.RecordScannedItem("4815-HERS-BAR", "Hershey Chocolate Bar", 5.00f, "Melia");
                }
                cashierPc.SetReadyForCheckout("Melia", 5.00f);
            }
            else if (scannedItem != null && scannedItem == scannableChips)
            {
                cashierPc.RecordScannedItem("4802-GOCH-100", "GoChips Banana Chips", 6.00f, "Melia");
            }
            else
            {
                cashierPc.RecordScannedItem("8904-SPRT-330", "Sprite Lemon-Lime Can", 6.00f, "Melia");
            }
        }

        if (NightShiftStoryController.Instance != null && scannedItemsCount < TotalItemsToScan)
        {
            NightShiftStoryController.Instance.NotifyCustomerTaskUpdated();
        }
    }

    // =========================================================================
    // WAYPOINT NAVIGATION & CHOREOGRAPHY POSITIONS
    // =========================================================================

    private static Bounds GetShelfBounds(Transform shelfTransform, Vector3 fallbackMin, Vector3 fallbackMax)
    {
        if (shelfTransform != null)
        {
            Renderer[] rends = shelfTransform.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++)
                {
                    // Ignore our own spawned GoChips display bag when measuring shelf bounds
                    if (rends[i].name.IndexOf("GoChips", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    b.Encapsulate(rends[i].bounds);
                }
                return b;
            }

            Collider col = shelfTransform.GetComponentInChildren<Collider>();
            if (col != null)
            {
                return col.bounds;
            }
        }

        Bounds fallback = new Bounds();
        fallback.SetMinMax(fallbackMin, fallbackMax);
        return fallback;
    }

    private void ComputeOtherShelfPositions(
        out Vector3 standPos,
        out Vector3 lookTarget,
        out Vector3 chipsReachPos)
    {
        Bounds b2 = GetShelfBounds(
            otherShelfTransform,
            new Vector3(-0.16f, 0.17f, -0.19f),
            new Vector3(2.96f, 1.58f, 0.44f));

        Bounds b3 = GetShelfBounds(
            shelf3Transform,
            new Vector3(-0.16f, 0.17f, -2.15f),
            new Vector3(2.96f, 1.58f, -1.51f));

        float standX = Mathf.Clamp(b2.center.x, 1.15f, 1.55f);
        // Center of the 1.34m aisle between Shelf3 (maxZ = -1.52) and Shelf2 (minZ = -0.18) -> Z = -0.85
        float aisleMidZ = (b2.min.z + b3.max.z) * 0.5f;
        float standZ = Mathf.Clamp(aisleMidZ, b3.max.z + 0.56f, b2.min.z - 0.56f);

        standPos = new Vector3(standX, 0.17f, standZ);
        lookTarget = new Vector3(standX, 1.16f, b2.center.z);
        chipsReachPos = new Vector3(standX - 0.10f, 1.15f, b2.min.z + 0.08f);
    }

    private List<Vector3> BuildObstacleFreePathToOtherShelf(Vector3 start, Vector3 shelfStandPos)
    {
        float doorZ = slidingDoors != null ? slidingDoors.transform.position.z : -1.60f;
        float doorX = slidingDoors != null ? slidingDoors.transform.position.x : -1.77f;

        // Center of the west entrance aisle (X = -0.72f: 0.565m west of Shelf3/Shelf2 at -0.155m, 0.83m east of front window wall)
        float clearWestAisleX = -0.72f;

        List<Vector3> pts = new List<Vector3>();

        if (start.x < -22f)
        {
            pts.Add(new Vector3(Mathf.Clamp(start.x, -30.8f, -29.8f), start.y, -13.20f));
            pts.Add(new Vector3(-25.50f, 0.02f, -12.80f));
        }

        pts.Add(new Vector3(-11.50f, 0.08f, -12.00f));
        pts.Add(new Vector3(-5.60f, 0.14f, -9.80f));
        pts.Add(new Vector3(-4.80f, 0.14f, -4.80f));
        pts.Add(new Vector3(doorX - 1.35f, 0.15f, doorZ));
        pts.Add(new Vector3(clearWestAisleX, 0.17f, doorZ));
        pts.Add(new Vector3(clearWestAisleX, 0.17f, shelfStandPos.z));
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

    private Vector3 EnforceShelfClearance(Vector3 pos)
    {
        pos = PushOutOfShelfBox(pos, GetShelfBounds(
            shelf3Transform,
            new Vector3(-0.16f, 0.17f, -2.15f),
            new Vector3(2.96f, 1.58f, -1.51f)), 0.30f);

        pos = PushOutOfShelfBox(pos, GetShelfBounds(
            otherShelfTransform,
            new Vector3(-0.16f, 0.17f, -0.19f),
            new Vector3(2.96f, 1.58f, 0.44f)), 0.30f);

        pos = PushOutOfShelfBox(pos, GetShelfBounds(
            shelf1Transform,
            new Vector3(-0.07f, 0.17f, -4.43f),
            new Vector3(2.75f, 1.58f, -3.80f)), 0.30f);

        // Left Window Wire Snack Racks (model/group_0 & chipShelve: X = [-1.45 .. -0.98], Z = [-6.32 .. -4.99])
        Bounds leftSnackRacks = new Bounds();
        leftSnackRacks.SetMinMax(new Vector3(-1.45f, 0.0f, -6.32f), new Vector3(-0.98f, 1.55f, -4.99f));
        pos = PushOutOfShelfBox(pos, leftSnackRacks, 0.28f);

        // East Wall Wire Snack Racks (HersheyShelve & neighbor: X = [4.08 .. 4.65], Z = [-0.55 .. 2.10])
        Bounds eastSnackRacks = new Bounds();
        eastSnackRacks.SetMinMax(new Vector3(4.08f, 0.0f, -0.55f), new Vector3(4.65f, 1.55f, 2.10f));
        pos = PushOutOfShelfBox(pos, eastSnackRacks, 0.26f);

        return pos;
    }

    private static Vector3 PushOutOfShelfBox(Vector3 pos, Bounds shelfBounds, float margin)
    {
        float minX = shelfBounds.min.x - margin;
        float maxX = shelfBounds.max.x + margin;
        float minZ = shelfBounds.min.z - margin;
        float maxZ = shelfBounds.max.z + margin;

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
        out Vector3 spriteShelfReachPos)
    {
        if (targetFridgeTransform != null && targetFridgeDoor != null)
        {
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

            approachPos = new Vector3(closedHandleWorld.x, 0.17f, closedHandleWorld.z)
                          + outward * 0.68f
                          + leftToRight * (0.14f * dirSign);

            // Safe step-back position completely outside the 0.99m swinging glass door arc AND well east of Shelf3 (+2.955m)!
            stepBackSafePos = new Vector3(pivotPos.x, 0.17f, pivotPos.z)
                              + outward * 0.82f
                              + leftToRight * (1.08f * dirSign);

            browsePos = new Vector3(pivotPos.x, 0.17f, pivotPos.z)
                        + outward * 0.78f
                        + leftToRight * (0.72f * dirSign);

            lookTarget = new Vector3(pivotPos.x, 1.25f, pivotPos.z)
                         - outward * 0.35f
                         + leftToRight * (0.52f * dirSign);

            spriteShelfReachPos = new Vector3(pivotPos.x, 1.23f, pivotPos.z)
                                  - outward * 0.18f
                                  + leftToRight * (0.54f * dirSign);
            return;
        }

        // Fallback coordinates for Double_Beverage_Fridge at Z = -2.435
        approachPos = new Vector3(3.56f, 0.17f, -2.40f);
        stepBackSafePos = new Vector3(3.43f, 0.17f, -2.35f);
        browsePos = new Vector3(3.47f, 0.17f, -2.71f);
        lookTarget = new Vector3(4.55f, 1.25f, -2.88f);
        spriteShelfReachPos = new Vector3(4.38f, 1.23f, -2.88f);
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

    // =========================================================================
    // MOVEMENT, GROUND SNAPPING & OBSTACLE AVOIDANCE
    // =========================================================================

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
                nextPos = EnforceShelfClearance(nextPos);
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

            float step = speed * Time.deltaTime;
            if (step > dist) step = dist;

            Vector3 nextPos = cur + diff.normalized * step;
            nextPos = EnforceShelfClearance(nextPos);
            nextPos.y = SampleGroundHeight(nextPos, cur.y);
            transform.position = nextPos;

            AdvanceWalkAudioAndTimer(0.8f);
            yield return null;
        }

        isCurrentlyWalking = false;
    }

    private IEnumerator TurnSmoothlyTowardsRoutine(Vector3 worldLookPoint, float duration)
    {
        isCurrentlyWalking = false;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector3 dir = worldLookPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                RotateTowardsWorldDir(dir.normalized, Time.deltaTime * (turnSpeed * 1.45f));
            }
            yield return null;
        }
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
            if (col.bounds.max.y <= currentPos.y + 0.24f) continue;
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

        Vector3 blended = desiredDir + repulsion * 1.15f;
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
                n.IndexOf("Trash", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Dumpster", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
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

        float highestFloorY = float.MinValue;
        float maxStepUpY = currentY + 0.48f;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.normal.y < 0.70f) continue;
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
        if (footstepTimer >= 0.56f)
        {
            footstepTimer = 0f;
            PlayOneShotClip(footstepClip, 0.24f, Random.Range(0.95f, 1.12f));
        }
    }

    // =========================================================================
    // GOCHIPS PICKUP, FRIDGE DOOR ANTI-BUMP, SPRITE PICKUP & COUNTER PLACEMENT
    // =========================================================================

    private IEnumerator PickUpGoChipsFromShelfRoutine(Vector3 chipsReachPos)
    {
        float t = 0f;
        while (t < 0.48f)
        {
            t += Time.deltaTime;
            leftArmReachWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.48f));
            leftArmWorldTarget = chipsReachPos;
            yield return null;
        }

        // Take the shelf display GoChips bag or create a new one in Melia's left hand
        if (shelfDisplayGoChipsObj != null)
        {
            goChipsBagObj = shelfDisplayGoChipsObj;
            shelfDisplayGoChipsObj = null;
            goChipsBagObj.name = "Customer_GoChips_Bag";
        }
        else
        {
            goChipsBagObj = CreateGoChipsBag("Customer_GoChips_Bag");
        }

        isCarryingChips = true;
        PlayOneShotClip(bagRustleClip, 0.42f, 1.04f);

        yield return new WaitForSeconds(0.16f);

        t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            leftArmReachWeight = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / 0.45f));
            leftArmWorldTarget = chipsReachPos;
            yield return null;
        }

        leftArmReachWeight = 0f;
    }

    private IEnumerator OpenFridgeWithStepBackRoutine(Vector3 stepBackSafePos, Vector3 fridgeLookTarget)
    {
        float t = 0f;
        while (t < 0.38f)
        {
            t += Time.deltaTime;
            rightArmReachWeight = Mathf.Clamp01(t / 0.38f);
            rightArmWorldTarget = GetCurrentFridgeHandleWorldPos();
            yield return null;
        }

        if (targetFridgeDoor != null)
        {
            targetFridgeDoor.isOpen = true;
        }
        PlayOneShotClip(fridgeSealClip, 0.42f, 1.0f);

        // Smoothly step back outside the swinging glass door arc so Melia NEVER gets hit by the door
        isCurrentlyWalking = true;
        Vector3 startPos = transform.position;
        float duration = 0.85f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            Vector3 nextPos = Vector3.Lerp(startPos, stepBackSafePos, u);
            nextPos = EnforceShelfClearance(nextPos);
            nextPos.y = SampleGroundHeight(nextPos, transform.position.y);
            transform.position = nextPos;

            Vector3 lookDir = fridgeLookTarget - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                RotateTowardsWorldDir(lookDir.normalized, Time.deltaTime * turnSpeed);
            }

            rightArmWorldTarget = GetCurrentFridgeHandleWorldPos();
            rightArmReachWeight = u < 0.5f ? 1f : Mathf.Clamp01(1f - ((u - 0.5f) / 0.5f));

            AdvanceWalkAudioAndTimer(0.75f);
            yield return null;
        }

        rightArmReachWeight = 0f;
        isCurrentlyWalking = false;
    }

    private IEnumerator PickUpSpriteFromFridgeRoutine(Vector3 spriteShelfReachPos)
    {
        float t = 0f;
        while (t < 0.50f)
        {
            t += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.50f));
            rightArmWorldTarget = spriteShelfReachPos;
            yield return null;
        }

        spriteCanObj = CreateAuthenticSpriteCan();
        isCarryingSprite = true;
        PlayOneShotClip(itemPlaceClip, 0.35f, 1.18f);

        yield return new WaitForSeconds(0.16f);

        t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / 0.45f));
            rightArmWorldTarget = spriteShelfReachPos;
            yield return null;
        }

        rightArmReachWeight = 0f;
    }

    private IEnumerator CloseFridgeDoorCleanlyRoutine(Vector3 stepBackSafePos, Vector3 fridgeLookTarget)
    {
        yield return StartCoroutine(MoveDirectlyToSpotRoutine(stepBackSafePos, fridgeLookTarget, slowWalkSpeed * 0.80f));

        float t = 0f;
        while (t < 0.32f)
        {
            t += Time.deltaTime;
            rightArmReachWeight = Mathf.Clamp01(t / 0.32f);
            rightArmWorldTarget = GetCurrentFridgeHandleWorldPos();
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
            rightArmReachWeight = Mathf.Clamp01(1f - (t / 0.38f));
            rightArmWorldTarget = GetCurrentFridgeHandleWorldPos();
            yield return null;
        }
        rightArmReachWeight = 0f;
    }

    private IEnumerator PlaceTwoItemsOnCounterRoutine(Vector3 counterSurfacePos)
    {
        Vector3 chipsCounterPos = counterSurfacePos + new Vector3(-0.11f, 0.0f, -0.01f);
        Vector3 spriteCounterPos = counterSurfacePos + new Vector3(0.11f, 0.0f, 0.01f);

        Vector3 leftHandTarget = chipsCounterPos + Vector3.up * 0.08f;
        Vector3 rightHandTarget = spriteCounterPos + Vector3.up * 0.07f;

        // 1. Place GoChips bag with left hand
        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            leftArmReachWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.45f));
            leftArmWorldTarget = leftHandTarget;
            yield return null;
        }

        isCarryingChips = false;
        if (goChipsBagObj != null)
        {
            goChipsBagObj.transform.position = chipsCounterPos;
            goChipsBagObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            goChipsBagObj.transform.localScale = Vector3.one;

            scannableChips = goChipsBagObj.GetComponent<ScannableMeliaCounterItem>();
            if (scannableChips == null)
            {
                scannableChips = goChipsBagObj.AddComponent<ScannableMeliaCounterItem>();
            }
            scannableChips.Initialize(this, "E to Scan (GoChips)");
        }
        PlayOneShotClip(bagRustleClip, 0.48f, 0.98f);

        // 2. Simultaneously retract left hand while right hand places Sprite can
        t = 0f;
        while (t < 0.48f)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / 0.48f);
            leftArmReachWeight = Mathf.SmoothStep(1f, 0f, u);
            leftArmWorldTarget = leftHandTarget;

            rightArmReachWeight = Mathf.SmoothStep(0f, 1f, u);
            rightArmWorldTarget = rightHandTarget;
            yield return null;
        }
        leftArmReachWeight = 0f;

        isCarryingSprite = false;
        if (spriteCanObj != null)
        {
            spriteCanObj.transform.position = spriteCounterPos;
            spriteCanObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            spriteCanObj.transform.localScale = Vector3.one;

            scannableSprite = spriteCanObj.GetComponent<ScannableMeliaCounterItem>();
            if (scannableSprite == null)
            {
                scannableSprite = spriteCanObj.AddComponent<ScannableMeliaCounterItem>();
            }
            scannableSprite.Initialize(this, "E to Scan (Sprite)");
        }
        PlayOneShotClip(itemPlaceClip, 0.52f, 0.96f);

        yield return new WaitForSeconds(0.14f);

        t = 0f;
        while (t < 0.42f)
        {
            t += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / 0.42f));
            rightArmWorldTarget = rightHandTarget;
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
    // PROCEDURAL 3D GOCHIPS BANANA CHIPS POUCH & AUTHENTIC SPRITE CAN BUILDERS
    // =========================================================================

    public static GameObject CreateGoChipsBag(string objName = "GoChips_BananaChips_Bag")
    {
        if (cachedGoChipsMesh == null)
        {
            cachedGoChipsMesh = BuildStandUpPouchMesh();
        }
        if (cachedGoChipsMaterial == null)
        {
            cachedGoChipsMaterial = BuildGoChipsMaterial();
        }

        GameObject bagGo = new GameObject(objName);
        MeshFilter mf = bagGo.AddComponent<MeshFilter>();
        mf.sharedMesh = cachedGoChipsMesh;
        MeshRenderer mr = bagGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = cachedGoChipsMaterial;
        return bagGo;
    }

    private static Mesh BuildStandUpPouchMesh()
    {
        // Stand-up resealable snack pouch: 13.5cm wide, 19.5cm tall, 5.2cm bottom gusset tapering to flat top seal
        int cols = 10;
        int rows = 14;
        float halfW = 0.068f;
        float height = 0.195f;
        float maxHalfDepth = 0.027f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        // Front face (+Z) and Back face (-Z) grids
        for (int side = 0; side < 2; side++)
        {
            float zSign = (side == 0) ? 1f : -1f;
            int baseIdx = verts.Count;

            for (int r = 0; r <= rows; r++)
            {
                float v = (float)r / rows;
                float y = v * height;

                // Pouch depth profile: rounded bottom gusset, full belly in lower/mid section, flat heat-sealed top strip
                float belly = Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01(v * 1.12f) * Mathf.PI));
                if (v > 0.84f)
                {
                    belly *= Mathf.Clamp01((1f - v) / 0.16f);
                }
                else if (v < 0.08f)
                {
                    belly = Mathf.Lerp(0.55f, belly, v / 0.08f);
                }

                // Slight hourglass/crinkle contour along width
                float rowHalfW = halfW * (v > 0.85f ? 1.02f : (0.96f + 0.05f * Mathf.Clamp01(Mathf.Sin(v * Mathf.PI))));

                for (int c = 0; c <= cols; c++)
                {
                    float u = (float)c / cols;
                    float x = Mathf.Lerp(-rowHalfW, rowHalfW, u);

                    // Side edge seam pinch (u near 0 or 1 flattens depth to 0; clamp >= 0 so Mathf.Sin(PI) = -8.7e-8 never produces NaN in Mathf.Pow)
                    float sinU = Mathf.Clamp01(Mathf.Sin(u * Mathf.PI));
                    float edgeFactor = sinU > 0.0001f ? Mathf.Pow(sinU, 0.58f) : 0f;

                    float z = zSign * maxHalfDepth * belly * edgeFactor;
                    verts.Add(new Vector3(x, y, z));

                    // Front uses U=[0..1], Back mirrors U
                    float texU = (side == 0) ? u : (1f - u);
                    uvs.Add(new Vector2(texU, v));
                }
            }

            int stride = cols + 1;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int i00 = baseIdx + r * stride + c;
                    int i10 = i00 + 1;
                    int i01 = i00 + stride;
                    int i11 = i01 + 1;

                    if (side == 0)
                    {
                        tris.Add(i00); tris.Add(i10); tris.Add(i11);
                        tris.Add(i00); tris.Add(i11); tris.Add(i01);
                    }
                    else
                    {
                        tris.Add(i00); tris.Add(i11); tris.Add(i10);
                        tris.Add(i00); tris.Add(i01); tris.Add(i11);
                    }
                }
            }
        }

        // Bottom gusset cap connecting front bottom row (r=0) and back bottom row (r=0)
        int strideCols = cols + 1;
        int frontBottomStart = 0;
        int backBottomStart = (rows + 1) * strideCols;
        for (int c = 0; c < cols; c++)
        {
            int f0 = frontBottomStart + c;
            int f1 = f0 + 1;
            int b0 = backBottomStart + c;
            int b1 = b0 + 1;

            tris.Add(f0); tris.Add(b1); tris.Add(f1);
            tris.Add(f0); tris.Add(b0); tris.Add(b1);
        }

        Mesh m = new Mesh();
        m.name = "ProceduralGoChipsStandUpPouch";
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static Material BuildGoChipsMaterial()
    {
        int w = 256;
        int h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point; // Crisp PS1 retro texture filtering
        tex.wrapMode = TextureWrapMode.Clamp;

        Color foilBase = new Color(0.76f, 0.78f, 0.81f, 1f);
        Color foilDark = new Color(0.52f, 0.54f, 0.58f, 1f);
        Color foilHighlight = new Color(0.90f, 0.92f, 0.95f, 1f);

        // Precompute procedural crispy golden-brown banana chips inside the clear pouch window
        Vector2[] chipCenters = new Vector2[42];
        float[] chipRadiiX = new float[42];
        float[] chipRadiiY = new float[42];
        float[] chipAngles = new float[42];
        Color[] chipColors = new Color[42];

        Random.State oldState = Random.state;
        Random.InitState(1337);
        for (int i = 0; i < chipCenters.Length; i++)
        {
            chipCenters[i] = new Vector2(Random.Range(0.14f, 0.86f), Random.Range(0.10f, 0.72f));
            chipRadiiX[i] = Random.Range(0.065f, 0.125f);
            chipRadiiY[i] = Random.Range(0.045f, 0.095f);
            chipAngles[i] = Random.Range(-1.2f, 1.2f);
            float shade = Random.Range(0.75f, 1.08f);
            chipColors[i] = new Color(
                Mathf.Clamp01(0.74f * shade),
                Mathf.Clamp01(0.52f * shade),
                Mathf.Clamp01(0.24f * shade),
                1f);
        }
        Random.state = oldState;

        for (int py = 0; py < h; py++)
        {
            float v = (float)py / (h - 1);
            for (int px = 0; px < w; px++)
            {
                float u = (float)px / (w - 1);

                // 1. Metallic silver foil border, top resealable header (v > 0.78), and bottom gusset (v < 0.07)
                bool isFoilMargin = (u < 0.08f || u > 0.92f || v > 0.78f || v < 0.07f);
                Color col;

                if (isFoilMargin)
                {
                    float sheen = Mathf.Sin(u * 14f + v * 6f) * 0.5f + 0.5f;
                    col = Color.Lerp(foilDark, foilHighlight, sheen * 0.65f + 0.2f);

                    // Resealable zip-lock ridge lines at v = 0.84..0.86
                    if (v >= 0.835f && v <= 0.865f)
                    {
                        col = (py % 3 == 0) ? foilDark : foilHighlight;
                    }
                    // Tear notches on top sides
                    if (v > 0.90f && (u < 0.04f || u > 0.96f))
                    {
                        col = foilDark * 0.8f;
                    }
                }
                else
                {
                    // 2. Clear window showing golden-brown banana chips inside
                    col = new Color(0.28f, 0.19f, 0.10f, 1f); // Deep shadow cavity behind chips

                    for (int c = 0; c < chipCenters.Length; c++)
                    {
                        float dx = u - chipCenters[c].x;
                        float dy = v - chipCenters[c].y;
                        float ca = Mathf.Cos(chipAngles[c]);
                        float sa = Mathf.Sin(chipAngles[c]);
                        float rx = (dx * ca - dy * sa) / chipRadiiX[c];
                        float ry = (dx * sa + dy * ca) / chipRadiiY[c];
                        float d2 = rx * rx + ry * ry;

                        if (d2 <= 1.0f)
                        {
                            Color cc = chipColors[c];
                            // Darker toasted rim and lighter crispy center with BBQ spice speckles
                            if (d2 > 0.76f)
                            {
                                cc *= 0.74f;
                            }
                            else if (d2 < 0.18f)
                            {
                                cc *= 0.88f; // Banana slice core ring
                            }

                            float spice = Mathf.Sin((px * 17 + py * 31 + c * 7) * 0.7f);
                            if (spice > 0.65f)
                            {
                                cc = Color.Lerp(cc, new Color(0.48f, 0.20f, 0.08f, 1f), 0.45f);
                            }

                            col = cc;
                        }
                    }

                    // Subtle plastic film glossy diagonal reflection
                    float gloss = Mathf.Sin((u * 1.4f - v * 0.9f) * 10f);
                    if (gloss > 0.82f)
                    {
                        col = Color.Lerp(col, foilHighlight, (gloss - 0.82f) * 1.4f);
                    }
                }

                // 3. Circular "GoChips Banana Chips" Sticker Badge at center-top (u=0.50, v=0.60, radius=0.22)
                float bx = (u - 0.50f) / 0.22f;
                float by = (v - 0.59f) / 0.20f;
                float bDist = Mathf.Sqrt(bx * bx + by * by);

                if (bDist <= 1.05f)
                {
                    if (bDist > 0.91f)
                    {
                        // Crisp white sticker die-cut outline
                        col = new Color(0.96f, 0.95f, 0.92f, 1f);
                    }
                    else
                    {
                        // Warm crimson-red & dark roasted brown circular badge background
                        Color badgeBg = (by > -0.25f)
                            ? new Color(0.76f, 0.16f, 0.14f, 1f)
                            : new Color(0.28f, 0.12f, 0.09f, 1f);
                        col = badgeBg;

                        // Golden inner ring
                        if (bDist > 0.82f && bDist <= 0.88f)
                        {
                            col = new Color(0.95f, 0.76f, 0.20f, 1f);
                        }

                        // Cartoon Yellow Smiling Banana Crescent in center of sticker
                        float banX = bx + 0.04f;
                        float banY = by - 0.02f;
                        float curveY = banY + 0.28f * (banX * banX) - 0.08f * banX;
                        if (Mathf.Abs(banX) < 0.58f && Mathf.Abs(curveY) < 0.22f * (1f - 0.45f * banX * banX))
                        {
                            col = new Color(0.98f, 0.84f, 0.16f, 1f);
                            // Banana cute eyes & smile
                            if (Mathf.Abs(banX - 0.08f) < 0.045f && Mathf.Abs(banY - 0.06f) < 0.055f)
                                col = new Color(0.12f, 0.08f, 0.06f, 1f);
                            if (Mathf.Abs(banX - 0.22f) < 0.045f && Mathf.Abs(banY - 0.08f) < 0.055f)
                                col = new Color(0.12f, 0.08f, 0.06f, 1f);
                        }

                        // Green leaf tuft at top-right of banana (bx ~ 0.35, by ~ 0.48)
                        float lx = bx - 0.32f;
                        float ly = by - 0.46f;
                        if (lx * lx + ly * ly < 0.045f)
                        {
                            col = new Color(0.22f, 0.68f, 0.24f, 1f);
                        }

                        // Bold white/gold header text band ("GoChips") across upper sticker
                        if (by > 0.34f && by < 0.64f && Mathf.Abs(bx) < 0.65f)
                        {
                            bool charStroke = ((px / 3 + py / 3) % 2 == 0) && (py % 4 != 0);
                            if (charStroke)
                            {
                                col = new Color(0.98f, 0.94f, 0.78f, 1f);
                            }
                        }

                        // Bottom white flavor pill ("BBQ") at by in [-0.82, -0.48]
                        if (by >= -0.82f && by <= -0.48f && Mathf.Abs(bx) <= 0.52f)
                        {
                            col = new Color(0.95f, 0.94f, 0.90f, 1f);
                            if (by >= -0.72f && by <= -0.56f && Mathf.Abs(bx) <= 0.34f && (px % 4 < 2))
                            {
                                col = new Color(0.16f, 0.08f, 0.06f, 1f);
                            }
                        }
                    }
                }

                col.a = 1f;
                tex.SetPixel(px, py, col);
            }
        }

        tex.Apply();

        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        Material mat = new Material(sh);
        mat.name = "Procedural_GoChips_Mat";
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.38f);
        return mat;
    }

    private GameObject CreateAuthenticSpriteCan()
    {
        Mesh spriteMesh = null;
        Material spriteMat = null;

        if (targetFridgeTransform != null)
        {
            Transform cansSprite = targetFridgeTransform.Find("Drink_Cans/Cans_Sprite");
            if (cansSprite != null)
            {
                MeshRenderer mr = cansSprite.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    spriteMat = mr.sharedMaterial;
                }

                MeshFilter mf = cansSprite.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
                {
                    spriteMesh = ExtractSingleCanFromCombinedFridgeMesh(mf.sharedMesh, 24);
                }
            }
        }

        if (spriteMesh == null)
        {
            spriteMesh = RestockCardboardBox.CreateSharedCanMesh();
        }

        if (spriteMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            spriteMat = new Material(sh);
            spriteMat.color = new Color(0.08f, 0.68f, 0.26f); // Classic Sprite emerald green
        }

        GameObject canGo = new GameObject("Customer_Sprite_Can");
        MeshFilter canMf = canGo.AddComponent<MeshFilter>();
        canMf.sharedMesh = spriteMesh;
        MeshRenderer canMr = canGo.AddComponent<MeshRenderer>();
        canMr.sharedMaterial = spriteMat;

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
        single.name = "RuntimeSingleSpriteCan";
        single.vertices = dstVerts;
        if (dstNorms != null) single.normals = dstNorms;
        if (dstUvs != null) single.uv = dstUvs;
        single.triangles = dstTris;
        single.RecalculateBounds();
        if (dstNorms == null) single.RecalculateNormals();
        return single;
    }

    // =========================================================================
    // PROCEDURAL PS1 BONE RIGGING & ANIMATION (SUPPORTS MELIANPC & NPC1 BONES)
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
            if (n == "hips" || n == "pelvis") pelvis = b;
            else if (n == "spine") spine = b;
            else if (n == "chest") chest = b;
            else if (n == "neck") neck = b;
            else if (n == "head") head = b;
            else if (n == "shoulder.L" || n == "shoulder_left") shoulderL = b;
            else if (n == "upper_arm.L" || n == "upper_arm_left") upperArmL = b;
            else if (n == "forearm.L" || n == "forearm_left") forearmL = b;
            else if (n == "hand.L" || n == "hand_left") handL = b;
            else if (n == "shoulder.R" || n == "shoulder_right") shoulderR = b;
            else if (n == "upper_arm.R" || n == "upper_arm_right") upperArmR = b;
            else if (n == "forearm.R" || n == "forearm_right") forearmR = b;
            else if (n == "hand.R" || n == "hand_right") handR = b;
            else if (n == "thigh.L" || n == "thigh_left") thighL = b;
            else if (n == "shin.L" || n == "shin_left") shinL = b;
            else if (n == "foot.L" || n == "foot_left") footL = b;
            else if (n == "toe.L" || n == "foot_left_end") footLEnd = b;
            else if (n == "thigh.R" || n == "thigh_right") thighR = b;
            else if (n == "shin.R" || n == "shin_right") shinR = b;
            else if (n == "foot.R" || n == "foot_right") footR = b;
            else if (n == "toe.R" || n == "foot_right_end") footREnd = b;
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

        // Keep carried GoChips bag locked to Melia's left hand
        if (isCarryingChips && goChipsBagObj != null && handL != null)
        {
            Vector3 cRight = CharacterRightWorld;
            Vector3 cFwd = CharacterForwardWorld;
            Vector3 palmWorld = handL.position + cFwd * 0.065f + cRight * 0.03f - Vector3.up * 0.095f;
            Quaternion bagRot = Quaternion.LookRotation(cFwd, Vector3.up);
            goChipsBagObj.transform.SetPositionAndRotation(palmWorld, bagRot);
            goChipsBagObj.transform.localScale = Vector3.one;
        }

        // Keep carried Sprite can locked to Melia's right hand
        if (isCarryingSprite && spriteCanObj != null && handR != null)
        {
            Vector3 cRight = CharacterRightWorld;
            Vector3 cFwd = CharacterForwardWorld;
            Vector3 palmWorld = handR.position + cFwd * 0.055f - cRight * 0.03f - Vector3.up * 0.058f;
            Quaternion canRot = Quaternion.LookRotation(cFwd, Vector3.up);
            spriteCanObj.transform.SetPositionAndRotation(palmWorld, canRot);
            spriteCanObj.transform.localScale = Vector3.one;
        }
    }

    private void ApplyProceduralPs1Animation()
    {
        foreach (var kvp in bindLocalRots)
        {
            if (kvp.Key != null) kvp.Key.localRotation = kvp.Value;
        }
        if (pelvis != null) pelvis.localPosition = pelvisBindLocalPos;

        Vector3 wRight = CharacterRightWorld;
        Vector3 wFwd = CharacterForwardWorld;

        // Relax arms down naturally beside torso
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

        float qWalk = Mathf.Floor(walkCycleTimer * (ps1AnimFps / 5.2f)) / (ps1AnimFps / 5.2f);
        float qIdle = Mathf.Floor(idleTimer * ps1AnimFps) / ps1AnimFps;

        if (isCurrentlyWalking)
        {
            float stride = Mathf.Sin(qWalk);
            float swingPass = Mathf.Cos(qWalk);
            float legAngle = stride * 24f;

            if (thighL != null) RotateBoneWorld(thighL, wRight, -legAngle);
            if (thighR != null) RotateBoneWorld(thighR, wRight, legAngle);

            float kneeBendL = Mathf.Max(0f, swingPass) * 30f;
            float kneeBendR = Mathf.Max(0f, -swingPass) * 30f;
            if (shinL != null) RotateBoneWorld(shinL, wRight, kneeBendL);
            if (shinR != null) RotateBoneWorld(shinR, wRight, kneeBendR);

            if (footL != null) RotateBoneWorld(footL, wRight, -Mathf.Max(0f, swingPass) * 10f);
            if (footR != null) RotateBoneWorld(footR, wRight, -Mathf.Max(0f, -swingPass) * 10f);

            if (spine != null)
            {
                RotateBoneWorld(spine, wRight, -3.2f);
                RotateBoneWorld(spine, Vector3.up, stride * 4.0f);
            }

            if (!isCarryingChips)
            {
                if (upperArmL != null) RotateBoneWorld(upperArmL, wRight, legAngle * 0.62f);
                if (forearmL != null) RotateBoneWorld(forearmL, wRight, -Mathf.Max(0f, -legAngle) * 0.42f);
            }

            if (!isCarryingSprite)
            {
                if (upperArmR != null) RotateBoneWorld(upperArmR, wRight, -legAngle * 0.62f);
                if (forearmR != null) RotateBoneWorld(forearmR, wRight, -Mathf.Max(0f, legAngle) * 0.42f);
            }
        }
        else
        {
            float breath = Mathf.Sin(qIdle * 1.9f) * 1.8f;
            if (chest != null) RotateBoneWorld(chest, wRight, breath);

            bool isBrowsing = (currentState == NpcState.BrowsingOtherShelf ||
                               currentState == NpcState.BrowsingFridge);

            if (isBrowsing && !lookAtPlayerHead && head != null)
            {
                float scanYaw = Mathf.Sin(qIdle * 1.15f) * 15f;
                float scanPitch = Mathf.Sin(qIdle * 0.75f) * 7f + 3f;
                RotateBoneWorld(head, Vector3.up, scanYaw);
                RotateBoneWorld(head, wRight, scanPitch);
            }
        }

        // Carry pose for left arm while holding GoChips bag
        if (isCarryingChips && leftArmReachWeight < 0.99f && upperArmL != null && forearmL != null)
        {
            RotateBoneWorld(upperArmL, wRight, -16f);
            RotateBoneWorld(forearmL, wRight, -58f);
            RotateBoneWorld(forearmL, Vector3.up, 12f);
        }

        // Carry pose for right arm while holding Sprite can
        if (isCarryingSprite && rightArmReachWeight < 0.99f && upperArmR != null && forearmR != null)
        {
            RotateBoneWorld(upperArmR, wRight, -18f);
            RotateBoneWorld(forearmR, wRight, -64f);
            RotateBoneWorld(forearmR, Vector3.up, -14f);
        }

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

        if (rightArmReachWeight > 0.001f && upperArmR != null && forearmR != null && handR != null)
        {
            ApplyArmReach(upperArmR, forearmR, handR, rightArmWorldTarget, wRight, rightArmReachWeight);
        }
        if (leftArmReachWeight > 0.001f && upperArmL != null && forearmL != null && handL != null)
        {
            ApplyArmReach(upperArmL, forearmL, handL, leftArmWorldTarget, wRight, leftArmReachWeight);
        }

        // Prevent heels/toes from dipping below floor plane during stride rotations
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

        Vector3 currentArmDir = (hand.position - upperArm.position).normalized;
        upperArm.rotation = Quaternion.FromToRotation(currentArmDir, targetDir) * upperArm.rotation;

        if (upperLen > 0.02f && foreLen > 0.02f && dist < (upperLen + foreLen) * 0.96f)
        {
            float cosShoulder = Mathf.Clamp((upperLen * upperLen + dist * dist - foreLen * foreLen) / (2f * upperLen * dist), -1f, 1f);
            float shoulderPitchDown = Mathf.Acos(cosShoulder) * Mathf.Rad2Deg;

            float cosElbow = Mathf.Clamp((upperLen * upperLen + foreLen * foreLen - dist * dist) / (2f * upperLen * foreLen), -1f, 1f);
            float elbowFlex = 180f - (Mathf.Acos(cosElbow) * Mathf.Rad2Deg);

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

        int stepSamples = (int)(sr * 0.075f);
        float[] stepBuf = new float[stepSamples];
        for (int i = 0; i < stepSamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 62f);
            float low = Mathf.Sin(2f * Mathf.PI * 115f * t) * 0.62f;
            float grit = (Random.value * 2f - 1f) * 0.35f;
            stepBuf[i] = (low + grit) * env * 0.6f;
        }
        footstepClip = AudioClip.Create("MeliaFootstep", stepSamples, 1, sr, false);
        footstepClip.SetData(stepBuf, 0);

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
        itemPlaceClip = AudioClip.Create("SpriteCanPlace", canSamples, 1, sr, false);
        itemPlaceClip.SetData(canBuf, 0);

        int bagSamples = (int)(sr * 0.11f);
        float[] bagBuf = new float[bagSamples];
        for (int i = 0; i < bagSamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Sin(Mathf.Clamp01(t / 0.11f) * Mathf.PI);
            float crinkle = (Random.value * 2f - 1f) * 0.55f;
            bagBuf[i] = crinkle * env * 0.45f;
        }
        bagRustleClip = AudioClip.Create("GoChipsBagRustle", bagSamples, 1, sr, false);
        bagRustleClip.SetData(bagBuf, 0);

        int beepSamples = (int)(sr * 0.11f);
        float[] beepBuf = new float[beepSamples];
        for (int i = 0; i < beepSamples; i++)
        {
            float t = (float)i / sr;
            float env = i < sr * 0.09f ? 1f : Mathf.Clamp01(1f - (t - 0.09f) / 0.02f);
            beepBuf[i] = Mathf.Sin(2f * Mathf.PI * 2480f * t) * env * 0.45f;
        }
        scanBeepClip = AudioClip.Create("MeliaScanBeep", beepSamples, 1, sr, false);
        scanBeepClip.SetData(beepBuf, 0);

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
        cashPayClip = AudioClip.Create("MeliaCashPayment", paySamples, 1, sr, false);
        cashPayClip.SetData(payBuf, 0);

        int sealSamples = (int)(sr * 0.12f);
        float[] sealBuf = new float[sealSamples];
        for (int i = 0; i < sealSamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 36f);
            sealBuf[i] = ((Random.value * 2f - 1f) * 0.45f + Mathf.Sin(2f * Mathf.PI * 160f * t) * 0.35f) * env * 0.5f;
        }
        fridgeSealClip = AudioClip.Create("MeliaFridgeSeal", sealSamples, 1, sr, false);
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
/// Attached to each of the two items (GoChips bag & Sprite can) placed by Melia on the counter.
/// </summary>
public class ScannableMeliaCounterItem : MonoBehaviour, IInteractable
{
    private CustomerNpcMelia ownerNpc;
    private BoxCollider scanCollider;
    private bool canBeScanned = false;
    private bool isScanned = false;
    private string customPrompt = "E to Scan";

    public bool IsScannable => canBeScanned && !isScanned;

    public void Initialize(CustomerNpcMelia owner, string promptText = "E to Scan")
    {
        ownerNpc = owner;
        customPrompt = promptText;
        scanCollider = GetComponent<BoxCollider>();
        if (scanCollider == null)
        {
            scanCollider = gameObject.AddComponent<BoxCollider>();
        }
        scanCollider.center = new Vector3(0f, 0.09f, 0f);
        scanCollider.size = new Vector3(0.20f, 0.24f, 0.20f);
        scanCollider.isTrigger = true;
        canBeScanned = false;
        isScanned = false;
    }

    public void SetScannable(bool scannable)
    {
        canBeScanned = scannable;
    }

    public void Interact()
    {
        if (!canBeScanned || isScanned || NightShiftStoryController.IsDialogueActive) return;
        if (ownerNpc != null && !ownerNpc.IsPlayerInsideCashierZone) return;

        isScanned = true;
        canBeScanned = false;
        if (ownerNpc != null)
        {
            ownerNpc.NotifyCounterItemScanned(this);
        }
    }

    public string GetPromptText()
    {
        if (!canBeScanned || isScanned || NightShiftStoryController.IsDialogueActive) return string.Empty;

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

        return customPrompt;
    }
}


using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class HorrorInteractionReticle : MonoBehaviour
{
    public static HorrorInteractionReticle Instance { get; private set; }

    [Header("Camera & Detection")]
    public Camera playerCamera;
    public float maxInteractionDistance = 2.2f;
    public LayerMask hitLayers = ~0; // All layers by default

    [Header("Reticle Styling")]
    [Tooltip("Diameter of the circular reticle dot in screen pixels")]
    [Range(4f, 24f)]
    public float circleDiameter = 8f;
    public Color normalDotColor = new Color(0.88f, 0.88f, 0.82f, 0.85f);
    public Color hoverDotColor = new Color(1.0f, 0.95f, 0.65f, 1.0f);
    public Color dotBorderColor = new Color(0f, 0f, 0f, 0.85f);

    [Header("Horror Text Styling")]
    public int fontSize = 16;
    public Color textColor = new Color(0.92f, 0.90f, 0.80f, 1.0f);
    public Color textShadowColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);

    private IInteractable currentInteractable;
    private Texture2D circleTexture;
    private GUIStyle horrorTextStyle;
    private GUIStyle horrorShadowStyle;
    private Font retroFont;

    private static int lastInteractFrame = -1;
    private static float lastInteractTime = -1f;
    private static int lastQFrame = -1;
    private static float lastQTime = -1f;

    public static IInteractable CurrentInteractable { get; private set; }

    public static Camera CurrentPlayerCamera
    {
        get
        {
            if (Instance != null && Instance.playerCamera != null && Instance.playerCamera.enabled && Instance.playerCamera.gameObject.activeInHierarchy)
            {
                return Instance.playerCamera;
            }
            return GetPlayerCamera();
        }
    }

    public static void EnsureActiveInstance(GameObject playerRoot)
    {
        if (playerRoot == null) return;

        HorrorInteractionReticle reticle = playerRoot.GetComponent<HorrorInteractionReticle>();
        if (reticle == null)
        {
            reticle = playerRoot.AddComponent<HorrorInteractionReticle>();
        }

        Instance = reticle;
        reticle.enabled = true;
        reticle.EnsureCamera();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        // Enforce comfortable interaction reach across rooms (switches, doors, counters)
        maxInteractionDistance = Mathf.Clamp(maxInteractionDistance, 0.5f, 3.2f);

        // Enforce 100% consistent HUD/reticle colors & font sizing across all scenes
        circleDiameter = HorrorHudStyle.ReticleDotDiameter;
        normalDotColor = HorrorHudStyle.ReticleNormalDotColor;
        hoverDotColor = HorrorHudStyle.ReticleHoverDotColor;
        dotBorderColor = HorrorHudStyle.ReticleDotBorderColor;
        fontSize = HorrorHudStyle.PromptFontSize;
        textColor = HorrorHudStyle.PromptTextColor;
        textShadowColor = HorrorHudStyle.PromptShadowColor;

        EnsureCamera();
        InitTextures();
        InitFont();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            CurrentInteractable = null;
        }
    }

    private void Start()
    {
        EnsureCamera();
    }

    public void EnsureCamera()
    {
        if (playerCamera != null && playerCamera.enabled && playerCamera.gameObject.activeInHierarchy) return;

        Camera childCam = GetComponentInChildren<Camera>();
        if (childCam != null && childCam.enabled && childCam.gameObject.activeInHierarchy)
        {
            playerCamera = childCam;
            return;
        }

        playerCamera = GetPlayerCamera();
    }

    public static Camera GetPlayerCamera()
    {
        if (Instance != null && Instance.playerCamera != null && Instance.playerCamera.enabled && Instance.playerCamera.gameObject.activeInHierarchy)
        {
            return Instance.playerCamera;
        }

        var playerObj = GameObject.Find("Test_FPS_Player");
        if (playerObj != null)
        {
            var cam = playerObj.GetComponentInChildren<Camera>(true);
            if (cam != null && cam.enabled) return cam;
        }

        var allCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
        for (int i = 0; i < allCams.Length; i++)
        {
            Camera c = allCams[i];
            if (c.enabled && c.gameObject.activeInHierarchy && c.name != "Main Camera")
            {
                return c;
            }
        }

        if (Camera.main != null && Camera.main.enabled && Camera.main.gameObject.activeInHierarchy)
        {
            return Camera.main;
        }

        return allCams.Length > 0 ? allCams[0] : null;
    }

    private void InitTextures()
    {
        if (circleTexture == null)
        {
            int size = 64;
            circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            circleTexture.filterMode = FilterMode.Bilinear;
            circleTexture.wrapMode = TextureWrapMode.Clamp;

            float center = (size - 1) * 0.5f;
            float radius = size * 0.46f;
            float borderThickness = size * 0.12f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > radius)
                    {
                        circleTexture.SetPixel(x, y, Color.clear);
                    }
                    else if (dist > radius - 1.5f)
                    {
                        float alpha = Mathf.Clamp01(radius - dist);
                        circleTexture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha * 0.85f));
                    }
                    else if (dist > radius - borderThickness)
                    {
                        circleTexture.SetPixel(x, y, new Color(0f, 0f, 0f, 0.85f));
                    }
                    else if (dist > radius - borderThickness - 1.5f)
                    {
                        float t = Mathf.Clamp01((radius - borderThickness) - dist);
                        Color fill = Color.white;
                        Color border = new Color(0f, 0f, 0f, 0.85f);
                        circleTexture.SetPixel(x, y, Color.Lerp(border, fill, t));
                    }
                    else
                    {
                        circleTexture.SetPixel(x, y, Color.white);
                    }
                }
            }
            circleTexture.Apply();
        }
    }

    private void InitFont()
    {
        if (retroFont == null)
        {
            retroFont = HorrorHudStyle.GetRetroFont();
        }
    }

    private void Update()
    {
        if (Instance != null && Instance != this) return;

        EnsureCamera();
        if (playerCamera == null || !playerCamera.enabled || !playerCamera.gameObject.activeInHierarchy) return;

        // Suppress interaction prompts & triggers while story/NPC dialogue, camera cinematics, or POS UI is active
        // (Except while waiting for the player to return the empty restock box to its origin spot)
        if ((NightShiftStoryController.IsDialogueActive && !NightShiftStoryController.IsWaitingForBoxReturnToOrigin) ||
            HouseSceneStoryController.IsDialogueActive ||
            HouseSceneStoryController.IsCrosshairHidden ||
            SimpleFPSController.IsExternalCameraAnimationActive ||
            InteractableCashierPC.IsPosScreenOpen ||
            Act4CounterPhoneAndDialpad.IsDialpadOpen)
        {
            currentInteractable = null;
            CurrentInteractable = null;
            return;
        }

        // If sitting in chair, allow interacting with plated food on the dining table OR pressing E to unsit
        if (CabinChairInteractable.IsPlayerSitting)
        {
            currentInteractable = null;

            Ray sitRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] sitHits = Physics.SphereCastAll(sitRay, 0.22f, maxInteractionDistance, hitLayers, QueryTriggerInteraction.Collide);
            if (sitHits != null && sitHits.Length > 0)
            {
                System.Array.Sort(sitHits, (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < sitHits.Length; i++)
                {
                    if (sitHits[i].collider == null) continue;
                    CabinDiningTablePlaceSpot mealSpot = sitHits[i].collider.GetComponentInParent<CabinDiningTablePlaceSpot>();
                    if (mealSpot != null && !string.IsNullOrEmpty(mealSpot.GetPromptText()))
                    {
                        currentInteractable = mealSpot;
                        break;
                    }
                }
            }

            CurrentInteractable = currentInteractable;

            if (currentInteractable != null)
            {
                if (WasInteractPressed())
                {
                    currentInteractable.Interact();
                }
                return;
            }

            if (CabinChairInteractable.IsPlayerEating)
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

            if (unsitRequested && CabinChairInteractable.CurrentChair != null)
            {
                CabinChairInteractable.CurrentChair.UnsitPlayer();
            }

            return;
        }

        // Perform raycast directly through the center of the screen (the POV dot)
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        currentInteractable = null;

        RaycastHit[] hits = Physics.RaycastAll(ray, maxInteractionDistance, hitLayers, QueryTriggerInteraction.Collide);
        currentInteractable = FindBestInteractable(hits);

        // Generous SphereCast fallback for small wall objects (light switches, keys, door latches, doorways)
        if (currentInteractable == null)
        {
            RaycastHit[] sphereHits = Physics.SphereCastAll(ray, 0.18f, maxInteractionDistance, hitLayers, QueryTriggerInteraction.Collide);
            currentInteractable = FindBestInteractable(sphereHits);
        }

        // Close-range BillyNPC detection during "Talk to Billy" task (never through walls)
        if (currentInteractable == null &&
            NightShiftStoryController.IsTalkToBillyTaskActive &&
            !NightShiftStoryController.HasTalkedToBilly &&
            CoworkerNpcBilly.Instance != null &&
            CoworkerNpcBilly.Instance.IsNpcActiveInWorld)
        {
            RaycastHit[] billyHits = Physics.RaycastAll(ray, 2.35f, hitLayers, QueryTriggerInteraction.Collide);
            if (billyHits != null && billyHits.Length > 0)
            {
                System.Array.Sort(billyHits, (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < billyHits.Length; i++)
                {
                    Collider col = billyHits[i].collider;
                    if (col == null) continue;
                    if (col.GetComponentInParent<CharacterController>() != null) continue;

                    CoworkerNpcBilly billy = col.GetComponentInParent<CoworkerNpcBilly>();
                    if (billy != null && !string.IsNullOrEmpty(billy.GetPromptText()))
                    {
                        currentInteractable = billy;
                        break;
                    }

                    if (!col.isTrigger)
                    {
                        break;
                    }
                }
            }
        }

        // Across-the-counter cashier detection (shows "Go to Cashier area first." when outside PlayerNpcInteract, and allows E when inside)
        bool isCustomerAtCounter =
            (CustomerNpcSteve.Instance != null && CustomerNpcSteve.Instance.IsNpcActiveInWorld) ||
            (CustomerNpcMelia.Instance != null && CustomerNpcMelia.Instance.IsNpcActiveInWorld);

        if (currentInteractable == null && isCustomerAtCounter)
        {
            RaycastHit[] counterHits = Physics.RaycastAll(ray, 3.15f, hitLayers, QueryTriggerInteraction.Collide);
            if (counterHits != null && counterHits.Length > 0)
            {
                System.Array.Sort(counterHits, (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < counterHits.Length; i++)
                {
                    Collider col = counterHits[i].collider;
                    if (col == null) continue;

                    InteractableCashierPC cashierPc = col.GetComponentInParent<InteractableCashierPC>();
                    if (cashierPc != null && !string.IsNullOrEmpty(cashierPc.GetPromptText()))
                    {
                        currentInteractable = cashierPc;
                        break;
                    }

                    ScannableCounterCan scanCan = col.GetComponentInParent<ScannableCounterCan>();
                    if (scanCan != null && !string.IsNullOrEmpty(scanCan.GetPromptText()))
                    {
                        currentInteractable = scanCan;
                        break;
                    }

                    ScannableMeliaCounterItem scanMeliaItem = col.GetComponentInParent<ScannableMeliaCounterItem>();
                    if (scanMeliaItem != null && !string.IsNullOrEmpty(scanMeliaItem.GetPromptText()))
                    {
                        currentInteractable = scanMeliaItem;
                        break;
                    }

                    CustomerNpcSteve steve = col.GetComponentInParent<CustomerNpcSteve>();
                    if (steve != null && !string.IsNullOrEmpty(steve.GetPromptText()))
                    {
                        currentInteractable = steve;
                        break;
                    }

                    CustomerNpcMelia melia = col.GetComponentInParent<CustomerNpcMelia>();
                    if (melia != null && !string.IsNullOrEmpty(melia.GetPromptText()))
                    {
                        currentInteractable = melia;
                        break;
                    }
                }
            }
        }

        // If carrying the Cardboard Box near UserBoxPutter or its Origin Return Spot, assist detection smoothly at close range
        if (currentInteractable == null && RestockCardboardBox.Instance != null && RestockCardboardBox.Instance.IsInInventory)
        {
            if (UserBoxPutter.Instance != null && UserBoxPutter.Instance.IsPlayerNearPutter(1.45f))
            {
                Vector3 toPutter = (UserBoxPutter.Instance.transform.position - playerCamera.transform.position).normalized;
                if (Vector3.Dot(playerCamera.transform.forward, toPutter) > 0.45f)
                {
                    string prompt = UserBoxPutter.Instance.GetPromptText();
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        currentInteractable = UserBoxPutter.Instance;
                    }
                }
            }

            if (currentInteractable == null && RestockCardboardBox.Instance.OriginReturnSpot != null && RestockCardboardBox.Instance.IsPlayerNearOriginSpot(1.45f))
            {
                Vector3 toOrigin = (RestockCardboardBox.Instance.OriginalWorldPos - playerCamera.transform.position).normalized;
                if (Vector3.Dot(playerCamera.transform.forward, toOrigin) > 0.40f)
                {
                    string prompt = RestockCardboardBox.Instance.OriginReturnSpot.GetPromptText();
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        currentInteractable = RestockCardboardBox.Instance.OriginReturnSpot;
                    }
                }
            }
        }

        // If player is inside the Stockroom return zone with the mop equipped, show "Q to return"
        if (currentInteractable == null && EquippableMop.Instance != null && EquippableMop.Instance.IsEquipped)
        {
            if (MopReturnSpot.Instance != null && MopReturnSpot.Instance.IsPlayerInsideZone)
            {
                string prompt = MopReturnSpot.Instance.GetPromptText();
                if (!string.IsNullOrEmpty(prompt))
                {
                    currentInteractable = MopReturnSpot.Instance;
                }
            }
            else if (EquippableMop.Instance.IsNearOriginalSpot())
            {
                currentInteractable = EquippableMop.Instance;
            }
        }

        CurrentInteractable = currentInteractable;

        if (currentInteractable != null && !CabinKitchenSinkStation.IsPlayerWashingPlate)
        {
            bool isReturnAction = (currentInteractable is MopReturnSpot) ||
                                  (currentInteractable is EquippableMop mop && mop.IsEquipped);

            if (isReturnAction)
            {
                if (WasQPressed())
                {
                    currentInteractable.Interact();
                }
            }
            else
            {
                if (WasInteractPressed())
                {
                    currentInteractable.Interact();
                }
            }
        }
    }

    private static IInteractable FindBestInteractable(RaycastHit[] hits)
    {
        if (hits == null || hits.Length == 0) return null;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        if (CabinKitchenSinkStation.IsPlayerWashingPlate)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                CabinKitchenSinkStation sink = col.GetComponentInParent<CabinKitchenSinkStation>();
                if (sink != null && !string.IsNullOrEmpty(sink.GetPromptText()))
                {
                    return sink;
                }
            }
            return null;
        }

        // Priority pass: when aiming at pickable items inside open cabinets/drawers/fridge or cooking stations,
        // always prioritize the item/station over closing the parent door/drawer.
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;

            InteractableFoodItem food = col.GetComponentInParent<InteractableFoodItem>();
            if (food != null && !string.IsNullOrEmpty(food.GetPromptText()))
            {
                return food;
            }

            CabinHeldCookingItem cookingItem = col.GetComponentInParent<CabinHeldCookingItem>();
            if (cookingItem != null && !string.IsNullOrEmpty(cookingItem.GetPromptText()))
            {
                return cookingItem;
            }

            CabinPlateStackInteractable plateStack = col.GetComponentInParent<CabinPlateStackInteractable>();
            if (plateStack != null && !string.IsNullOrEmpty(plateStack.GetPromptText()))
            {
                return plateStack;
            }

            CabinDrawerForkInteractable forkItem = col.GetComponentInParent<CabinDrawerForkInteractable>();
            if (forkItem != null && !string.IsNullOrEmpty(forkItem.GetPromptText()))
            {
                return forkItem;
            }

            CabinStoveStation stoveStation = col.GetComponentInParent<CabinStoveStation>();
            if (stoveStation != null && !string.IsNullOrEmpty(stoveStation.GetPromptText()))
            {
                return stoveStation;
            }

            CabinDiningTablePlaceSpot tableSpot = col.GetComponentInParent<CabinDiningTablePlaceSpot>();
            if (tableSpot != null && !string.IsNullOrEmpty(tableSpot.GetPromptText()))
            {
                return tableSpot;
            }

            CabinKitchenSinkStation sinkStation = col.GetComponentInParent<CabinKitchenSinkStation>();
            if (sinkStation != null && !string.IsNullOrEmpty(sinkStation.GetPromptText()))
            {
                return sinkStation;
            }

            CabinKitchenCounterSurface counterSurface = col.GetComponentInParent<CabinKitchenCounterSurface>();
            if (counterSurface != null && !string.IsNullOrEmpty(counterSurface.GetPromptText()))
            {
                return counterSurface;
            }

            CabinFridgeReturnSpot fridgeReturnSpot = col.GetComponentInParent<CabinFridgeReturnSpot>();
            if (fridgeReturnSpot != null && !string.IsNullOrEmpty(fridgeReturnSpot.GetPromptText()))
            {
                return fridgeReturnSpot;
            }

            CabinTvRemoteSpotInteractable remoteSpot = col.GetComponentInParent<CabinTvRemoteSpotInteractable>();
            if (remoteSpot != null && !string.IsNullOrEmpty(remoteSpot.GetPromptText()))
            {
                return remoteSpot;
            }
        }

        float firstSolidDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            if (col.GetComponentInParent<CharacterController>() != null) continue;

            IInteractable interactable = ResolveInteractable(col);
            if (interactable != null && !string.IsNullOrEmpty(interactable.GetPromptText()))
            {
                bool isRoomDoor = interactable is InteractableRestroomDoor ||
                    (interactable is CabinDoorInteractable cdi && cdi.targetAnimator != null &&
                     (cdi.targetAnimator.name.IndexOf("DoorSingle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                      cdi.targetAnimator.name.IndexOf("DoorDouble", System.StringComparison.OrdinalIgnoreCase) >= 0));

                float occlusionTolerance = isRoomDoor ? 0.35f : 0.025f;
                if (firstSolidDist == float.MaxValue || hits[i].distance <= firstSolidDist + occlusionTolerance)
                {
                    return interactable;
                }
            }

            if (!col.isTrigger && firstSolidDist == float.MaxValue)
            {
                firstSolidDist = hits[i].distance;
            }
        }

        return null;
    }

    private static IInteractable ResolveInteractable(Collider col)
    {
        IInteractable direct = col.GetComponentInParent<IInteractable>();
        if (direct is MopReturnSpot)
        {
            // MopReturnSpot is handled strictly by the Stockroom interior zone check
            return null;
        }
        if (direct != null)
        {
            return direct;
        }

        // If looking at the door frame (DoorFrame_South / DoorFrame_North / DoorFrame_Top),
        // resolve the sibling InteractableRestroomDoor so open doors can always be closed easily
        if (col.name.StartsWith("DoorFrame_") && col.transform.parent != null)
        {
            InteractableRestroomDoor door = col.transform.parent.GetComponentInChildren<InteractableRestroomDoor>();
            if (door != null)
            {
                return door;
            }
        }

        // If looking at entrance DoorDouble frame or parent, resolve CabinDoorInteractable on leaves
        if (col.name.IndexOf("DoorDouble", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            CabinDoorInteractable cdi = col.GetComponentInChildren<CabinDoorInteractable>();
            if (cdi != null) return cdi;
            cdi = col.GetComponentInParent<CabinDoorInteractable>();
            if (cdi != null) return cdi;
        }

        return null;
    }

    /// <summary>
    /// Checks distance from the active player camera to the closest point across a target's colliders,
    /// avoiding floor-level or corner-hinge distance bias on doors and furniture.
    /// </summary>
    public static bool IsCameraWithinColliderReach(Collider[] colliders, Vector3 fallbackPosition, float maxReach, bool horizontalFallback = false)
    {
        Camera cam = CurrentPlayerCamera;
        if (cam == null) cam = GetPlayerCamera();
        if (cam == null) return true;

        Vector3 camPos = cam.transform.position;
        if (colliders != null && colliders.Length > 0)
        {
            float minSqDist = float.MaxValue;
            float maxSq = maxReach * maxReach;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null || !col.enabled) continue;
                Vector3 closest = (col is MeshCollider mc && !mc.convex)
                    ? col.bounds.ClosestPoint(camPos)
                    : col.ClosestPoint(camPos);
                float sq = (closest - camPos).sqrMagnitude;
                if (sq < minSqDist) minSqDist = sq;
            }
            if (minSqDist < float.MaxValue)
            {
                return minSqDist <= maxSq;
            }
        }

        Vector3 delta = camPos - fallbackPosition;
        if (horizontalFallback) delta.y = 0f;
        return delta.magnitude <= maxReach;
    }

    public static bool WasInteractPressed()
    {
        if (lastInteractFrame == Time.frameCount) return true;
        if (Time.unscaledTime - lastInteractTime < 0.12f) return false;

        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        foreach (var device in InputSystem.devices)
        {
            if (device is Keyboard kb && kb.eKey.wasPressedThisFrame)
            {
                pressed = true;
                break;
            }
        }

        if (!pressed && Gamepad.current != null &&
            (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonWest.wasPressedThisFrame))
        {
            pressed = true;
        }
#else
        pressed = Input.GetKeyDown(KeyCode.E);
#endif
        if (pressed)
        {
            lastInteractFrame = Time.frameCount;
            lastInteractTime = Time.unscaledTime;
        }
        return pressed;
    }

    public static bool WasQPressed()
    {
        if (lastQFrame == Time.frameCount) return true;
        if (Time.unscaledTime - lastQTime < 0.12f) return false;

        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        foreach (var device in InputSystem.devices)
        {
            if (device is Keyboard kb && (kb.qKey.wasPressedThisFrame || kb.gKey.wasPressedThisFrame))
            {
                pressed = true;
                break;
            }
        }
#else
        pressed = Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.G);
#endif
        if (pressed)
        {
            lastQFrame = Time.frameCount;
            lastQTime = Time.unscaledTime;
        }
        return pressed;
    }

    public static bool WasMouseClicked()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private void OnGUI()
    {
        if (Instance != null && Instance != this) return;
        if (Event.current.type != EventType.Repaint) return;
        if (InteractableCashierPC.IsPosScreenOpen) return;
        if (NightShiftStoryController.IsIntroOverlayBlockingUI) return;
        if (HouseSceneStoryController.IsCrosshairHidden) return;
        if (SimpleFPSController.IsExternalCameraAnimationActive) return;

        InitTextures();
        InitFont();

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        bool isHovering = currentInteractable != null;

        // 1. Draw Small Center Circular Dot on POV (stays 100% consistent whether standing or sitting)
        float currentDiameter = isHovering ? (circleDiameter + 3f) : circleDiameter;
        float halfSize = currentDiameter * 0.5f;

        GUI.color = isHovering ? hoverDotColor : normalDotColor;
        GUI.DrawTexture(new Rect(cx - halfSize, cy - halfSize, currentDiameter, currentDiameter), circleTexture);

        GUI.color = Color.white;

        // If sitting in chair, draw the top-right "Press E to Unsit" prompt (and still allow center hover prompt for eating)
        if (CabinChairInteractable.IsPlayerSitting)
        {
            DrawSittingTopRightPrompt();
            if (!isHovering)
            {
                return;
            }
        }

        // 2. If hovering over an interactable, display horror interaction prompt
        if (isHovering)
        {
            SetupStyles();

            string prompt = currentInteractable.GetPromptText();
            if (string.IsNullOrEmpty(prompt))
            {
                prompt = "E to Interact";
            }

            GUIContent content = new GUIContent(prompt);
            Vector2 size = horrorTextStyle.CalcSize(content);

            // Generous width so prompts fit cleanly on 1 line without wrapping or cutting
            float boxWidth = Mathf.Max(size.x + 40f, 420f);
            float maxAllowedWidth = Screen.width - 32f;
            float boxHeight = Mathf.Max(size.y + 18f, 38f);

            if (boxWidth > maxAllowedWidth)
            {
                boxWidth = maxAllowedWidth;
                horrorTextStyle.wordWrap = true;
                horrorShadowStyle.wordWrap = true;
                boxHeight = horrorTextStyle.CalcHeight(content, boxWidth) + 18f;
            }
            else
            {
                horrorTextStyle.wordWrap = false;
                horrorShadowStyle.wordWrap = false;
            }

            float textX = cx - boxWidth * 0.5f;
            float textY = cy + 14f;

            Rect rect = new Rect(textX, textY, boxWidth, boxHeight);

            HorrorHudStyle.DrawOutlinedLabel(
                rect,
                prompt,
                horrorShadowStyle,
                textShadowColor,
                horrorTextStyle,
                textColor);
        }
    }

    private void DrawSittingTopRightPrompt()
    {
        InitTextures();
        InitFont();
        SetupStyles();

        string prompt = CabinChairInteractable.IsPlayerEating
            ? "Eating Breakfast..."
            : (HouseSceneStoryController.IsSitLockedByTv ? "Watching TV..." : "Press E to Stand");
        float boxWidth = 240f;
        float boxHeight = 44f;
        float rightMargin = 32f;
        float topMargin = 32f;

        float x = Screen.width - boxWidth - rightMargin;
        float y = topMargin;

        Rect boxRect = new Rect(x, y, boxWidth, boxHeight);

        // Dark atmospheric backing pill
        if (circleTexture != null)
        {
            GUI.color = new Color(0.06f, 0.06f, 0.08f, 0.88f);
            GUI.DrawTexture(boxRect, Texture2D.whiteTexture);

            // Subtle warm horror border
            GUI.color = new Color(0.60f, 0.55f, 0.38f, 0.75f);
            GUI.DrawTexture(new Rect(boxRect.x, boxRect.y, boxRect.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxRect.x, boxRect.yMax - 1, boxRect.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxRect.x, boxRect.y, 1, boxRect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxRect.xMax - 1, boxRect.y, 1, boxRect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        HorrorHudStyle.DrawOutlinedLabel(
            boxRect,
            prompt,
            horrorShadowStyle,
            textShadowColor,
            horrorTextStyle,
            textColor);
    }

    private void SetupStyles()
    {
        InitFont();
        if (horrorTextStyle == null)
        {
            horrorTextStyle = HorrorHudStyle.CreatePromptStyle();
            horrorShadowStyle = new GUIStyle(horrorTextStyle);
            horrorShadowStyle.normal.textColor = textShadowColor;
        }
    }
}

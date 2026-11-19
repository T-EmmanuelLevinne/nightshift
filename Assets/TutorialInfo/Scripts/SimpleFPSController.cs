using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class SimpleFPSController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 4.5f;
    public float sprintSpeed = 7.5f;
    public float gravity = 20.0f;
    [Tooltip("Whether jumping is allowed. Set to false to disable player jumping.")]
        public bool canJump = false;
        public float jumpHeight = 1.2f;

        [Header("Look & POV Settings")]
        public Camera playerCamera;
        public float mouseSensitivity = 2.0f;
        public float maxLookAngle = 80.0f;
        [Range(1.4f, 2.2f)]
        public float eyeHeight = 1.75f;
        [Range(50f, 90f)]
        public float fieldOfView = 60.0f;

        private CharacterController controller;
        private float verticalRotation = 0f;
        private float verticalVelocity = 0f;
        public static bool IsExternalCameraAnimationActive = false;
        public static float ExternalMovementSpeedMultiplier = 1.0f;

    private void Awake()
    {
        ExternalMovementSpeedMultiplier = 1.0f;
        // Enforce uniform scale on CharacterController to prevent PhysX penetration resolution glitches
        transform.localScale = Vector3.one;
        EnsurePlayerCameraActive();
    }

    private void Start()
    {
        transform.localScale = Vector3.one;
        controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        EnsurePlayerCameraActive();

        if (GetComponent<HorrorInteractionReticle>() == null)
        {
            gameObject.AddComponent<HorrorInteractionReticle>();
        }

        if (GetComponent<RetroHorrorPOV>() == null)
        {
            gameObject.AddComponent<RetroHorrorPOV>();
        }

        if (GetComponent<HorrorAmbience>() == null)
        {
            gameObject.AddComponent<HorrorAmbience>();
        }
    }

    public void EnsurePlayerCameraActive()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            playerCamera.enabled = true;
            playerCamera.depth = 10; // Highest priority so Game view always renders through player's eyes
            playerCamera.tag = "MainCamera";
            playerCamera.fieldOfView = fieldOfView;
            playerCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);

            AudioListener listener = playerCamera.GetComponent<AudioListener>();
            if (listener == null)
            {
                listener = playerCamera.gameObject.AddComponent<AudioListener>();
            }
            listener.enabled = true;

            // Ensure URP post-processing stack (film grain, chromatic aberration, vignette) is active on POV
            var camData = playerCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData == null)
            {
                camData = playerCamera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }
            camData.renderPostProcessing = true;
        }

        // Disable any other camera in the scene (such as imported SketchUp cameras or background cameras)
        Camera[] allCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        for (int i = 0; i < allCams.Length; i++)
        {
            Camera c = allCams[i];
            if (c == null || c == playerCamera) continue;

            // Don't disable child cameras of the player
            if (c.transform.IsChildOf(transform)) continue;

            c.enabled = false;
            AudioListener al = c.GetComponent<AudioListener>();
            if (al != null) al.enabled = false;

            if (c.gameObject.name.Contains("skp_camera") || c.gameObject.name.Contains("SketchUp"))
            {
                c.gameObject.SetActive(false);
            }
        }
    }

    private void OnValidate()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (playerCamera != null && playerCamera.transform.parent == transform)
        {
            playerCamera.fieldOfView = fieldOfView;
            playerCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
        }
    }

    private void Update()
    {
        if (HorrorMainMenuController.IsMainMenuActive)
        {
            return;
        }

        if (InteractableCashierPC.IsPosScreenOpen)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
#else
            if (Input.GetKeyDown(KeyCode.Escape))
#endif
            {
                if (InteractableCashierPC.Instance != null)
                {
                    InteractableCashierPC.Instance.ClosePosUI();
                }
                return;
            }

            HandleMovement();
            return;
        }

        if (HouseSceneStoryController.IsSleepingSequenceActive)
        {
            return;
        }

        HandleMouseLook();
        HandleMovement();

        // Toggle cursor lock with Escape
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
#else
        if (Input.GetKeyDown(KeyCode.Escape))
#endif
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void HandleMouseLook()
    {
        if (InteractableCashierPC.IsPosScreenOpen ||
            Act4CounterPhoneAndDialpad.IsDialpadOpen ||
            HouseSceneStoryController.IsIntroActive ||
            HouseSceneStoryController.IsSleepingSequenceActive ||
            IsExternalCameraAnimationActive ||
            CabinTvController.IsRemoteCursorActive) return;

        float mouseX = 0f;
        float mouseY = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue() * 0.1f * mouseSensitivity;
            mouseX = delta.x;
            mouseY = delta.y;
        }
#else
        mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
#endif

        // If sitting in chair, allow natural seated head look (clamped yaw & pitch)
        if (CabinChairInteractable.IsPlayerSitting && CabinChairInteractable.CurrentChair != null)
        {
            float targetYaw = transform.eulerAngles.y + mouseX;
            float chairYaw = CabinChairInteractable.CurrentChair.transform.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(chairYaw, targetYaw);
            deltaYaw = Mathf.Clamp(deltaYaw, -85f, 85f);
            transform.rotation = Quaternion.Euler(0f, chairYaw + deltaYaw, 0f);

            verticalRotation -= mouseY;
            verticalRotation = Mathf.Clamp(verticalRotation, -60f, 60f);

            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
            }
            return;
        }

        transform.Rotate(Vector3.up * mouseX);

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        float moveX = 0f;
        float moveZ = 0f;
        bool isSprinting = false;
        bool jumpPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveZ += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveZ -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX -= 1f;
            isSprinting = Keyboard.current.leftShiftKey.isPressed;
            jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
        }
#else
        moveX = Input.GetAxisRaw("Horizontal");
        moveZ = Input.GetAxisRaw("Vertical");
        isSprinting = Input.GetKey(KeyCode.LeftShift);
        jumpPressed = Input.GetKeyDown(KeyCode.Space);
#endif

        bool isPostCleaningTransitionLocked =
            NightShiftStoryController.Instance != null &&
            NightShiftStoryController.Instance.currentStage == NightShiftStoryController.StoryStage.CleaningTaskCompleted;

        if (NightShiftStoryController.IsDialogueActive ||
            HouseSceneStoryController.IsDialogueActive ||
            IsExternalCameraAnimationActive ||
            InteractableCashierPC.IsPosScreenOpen ||
            EquippableMop.IsPlayerMopping ||
            FloorStain.IsAnyStainBeingMopped ||
            isPostCleaningTransitionLocked ||
            CabinChairInteractable.IsPlayerSitting ||
            CabinKitchenSinkStation.IsPlayerWashingPlate)
        {
            moveX = 0f;
            moveZ = 0f;
            isSprinting = false;
            jumpPressed = false;
        }

        if (controller == null || !controller.enabled) return;

        Vector3 move = (transform.right * moveX + transform.forward * moveZ).normalized;
        float currentSpeed = (isSprinting ? sprintSpeed : walkSpeed) * Mathf.Clamp01(ExternalMovementSpeedMultiplier);

        if (controller.isGrounded)
        {
            verticalVelocity = -4f; // Solid downward stick to avoid bouncing or popping off slopes
            if (canJump && jumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * gravity);
            }
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, -30f); // Terminal fall velocity clamp
            if (!canJump && verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }
        }

        // Only allow stepping up obstacles when actively moving
        controller.stepOffset = (move.sqrMagnitude > 0.001f) ? 0.3f : 0f;

        Vector3 motion = move * currentSpeed;
        motion.y = verticalVelocity;

        controller.Move(motion * Time.deltaTime);
    }

    public void ResetAfterExternalPose()
    {
        verticalRotation = 0f;
        verticalVelocity = 0f;

        if (controller != null)
        {
            controller.enabled = true;
        }

        if (playerCamera != null && playerCamera.transform.parent == transform)
        {
            playerCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
            playerCamera.transform.localRotation = Quaternion.identity;
            playerCamera.fieldOfView = fieldOfView;
        }
    }

    public void SyncLookAnglesFromCamera()
    {
        verticalVelocity = 0f;

        if (controller != null)
        {
            controller.enabled = true;
        }

        if (playerCamera != null && playerCamera.transform.parent == transform)
        {
            Quaternion worldCamRot = playerCamera.transform.rotation;
            Vector3 forward = worldCamRot * Vector3.forward;
            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            }

            playerCamera.transform.rotation = worldCamRot;
            float pitch = playerCamera.transform.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            verticalRotation = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
            playerCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
            playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
            playerCamera.fieldOfView = fieldOfView;
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null) return;

        InvisibleWorldBorder border = hit.collider.GetComponentInParent<InvisibleWorldBorder>();
        if (border != null)
        {
            border.TriggerBoundaryDialogue();
        }
        else if (hit.collider.name.IndexOf("InvisWall", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            InvisibleWorldBorder.TriggerGlobalBoundaryDialogue();
        }
    }
}


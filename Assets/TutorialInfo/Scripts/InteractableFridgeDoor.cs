using UnityEngine;

public class InteractableFridgeDoor : MonoBehaviour, IInteractable
{
    [Header("Door Identification")]
    public string doorLabel = "Fridge Door";

    [Header("Door Animation")]
    public bool isOpen = false;
    public float closedAngleY = 0f;
    public float openAngleY = -85f; // Negative swings outward-left, Positive swings outward-right
    public float swingSpeed = 6.0f;

    [HideInInspector]
    public bool isLockedByNpc = false;

    private Collider[] doorColliders;
    private int lastToggleFrame = -1;
    private float nextToggleAllowedTime = 0f;

    private void Awake()
    {
        doorColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        float targetY = isOpen ? openAngleY : closedAngleY;
        transform.localRotation = Quaternion.Euler(0f, targetY, 0f);
    }

    private void Update()
    {
        // Smoothly swing door hinge pivot toward target open/closed angle
        float targetY = isOpen ? openAngleY : closedAngleY;
        Quaternion targetRot = Quaternion.Euler(0f, targetY, 0f);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * swingSpeed);
    }

    public void ToggleDoor()
    {
        if (lastToggleFrame == Time.frameCount || Time.unscaledTime < nextToggleAllowedTime)
        {
            return;
        }

        lastToggleFrame = Time.frameCount;
        nextToggleAllowedTime = Time.unscaledTime + 0.12f;
        isOpen = !isOpen;
    }

    // IInteractable implementation
    public void Interact()
    {
        if (isLockedByNpc ||
            CustomerNpcSteve.IsNpcUsingFridge(this) ||
            CustomerNpcMelia.IsNpcUsingFridge(this) ||
            NightShiftStoryController.IsDialogueActive ||
            !HorrorInteractionReticle.IsCameraWithinColliderReach(doorColliders, transform.position, 2.0f))
        {
            return;
        }

        ToggleDoor();
    }

    public string GetPromptText()
    {
        if (isLockedByNpc ||
            CustomerNpcSteve.IsNpcUsingFridge(this) ||
            CustomerNpcMelia.IsNpcUsingFridge(this) ||
            NightShiftStoryController.IsDialogueActive ||
            !HorrorInteractionReticle.IsCameraWithinColliderReach(doorColliders, transform.position, 2.0f))
        {
            return string.Empty;
        }

        return isOpen ? "E to Close" : "E to Open";
    }
}

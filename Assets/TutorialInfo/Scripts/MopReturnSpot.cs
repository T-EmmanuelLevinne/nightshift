using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MopReturnSpot : MonoBehaviour, IInteractable
{
    public static MopReturnSpot Instance { get; private set; }

    [Header("Mop Reference")]
    public EquippableMop mop;

    [Header("Prompt Settings")]
    [SerializeField] private string returnPrompt = "Q to return";

    private void Awake()
    {
        Instance = this;

        // Place on Ignore Raycast layer so this trigger never blocks door or item raycasts
        gameObject.layer = 2;

        // Keep trigger strictly inside the Stockroom corner
        BoxCollider boxCol = GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(1.1f, 2.0f, 1.1f);
            boxCol.center = new Vector3(0.35f, 0f, 0.35f);
        }

        // Kinematic Rigidbody enables CharacterController trigger events in Unity
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;

        if (mop == null)
        {
            mop = FindAnyObjectByType<EquippableMop>();
        }
    }

    private void Start()
    {
        if (mop == null) mop = FindAnyObjectByType<EquippableMop>();
        if (mop != null)
        {
            // Sync spot directly to where the mop is placed
            transform.position = mop.OriginalWorldPos;
            transform.rotation = Quaternion.identity;
        }
    }

    public bool IsPlayerInsideZone
    {
        get
        {
            if (mop != null && mop.IsNearOriginalSpot()) return true;
            return IsPlayerNearSpot();
        }
    }

    private bool IsPlayerNearSpot(float maxDist = 1.35f)
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        Vector3 pPos = cam.transform.position;

        // Strictly require player to be physically inside the Stockroom walls:
        // Dividing wall between Male Restroom & Stockroom is at worldX = [1.63, 1.79]
        // Partition wall between Main Store & Stockroom is at worldZ = [2.00, 2.16]
        if (pPos.x < 1.78f || pPos.x > 5.05f || pPos.z < 2.15f || pPos.z > 6.88f)
        {
            return false;
        }

        float hDist = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(transform.position.x, transform.position.z));
        float vDist = Mathf.Abs(pPos.y - transform.position.y);

        return hDist <= maxDist && vDist <= 2.2f;
    }

    private void Update()
    {
        if (mop == null) mop = FindAnyObjectByType<EquippableMop>();
        if (mop == null || !mop.IsEquipped) return;

        if (IsPlayerInsideZone && HorrorInteractionReticle.WasQPressed())
        {
            Interact();
        }
    }

    public string GetPromptText()
    {
        if (mop == null) mop = FindAnyObjectByType<EquippableMop>();

        // Only show prompt if player is holding the mop and in the return zone
        if (mop != null && mop.IsEquipped && IsPlayerInsideZone)
        {
            return returnPrompt;
        }
        return string.Empty;
    }

    public void Interact()
    {
        if (mop == null) mop = FindAnyObjectByType<EquippableMop>();

        if (mop != null && mop.IsEquipped)
        {
            mop.PutBack();
        }
    }
}

using UnityEngine;

[ExecuteAlways]
public class AutomaticSlidingDoors : MonoBehaviour
{
    [Header("Door References")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("Settings")]
    public float openDistance = 1.22f;
    public float speed = 4.2f;

    [Header("Motion Sensor")]
    public bool autoTrigger = true;
    public float sensorRadius = 2.6f;

    [Header("State")]
    public bool isOpen = false;

    [SerializeField]
    private Vector3 leftClosedPos = new Vector3(-0.66f, 0f, 0f);
    [SerializeField]
    private Vector3 rightClosedPos = new Vector3(0.66f, 0f, 0f);

    private Transform playerTransform;

    private void Awake()
    {
        EnsureFlushClosedPositions();
    }

    private void Start()
    {
        EnsureFlushClosedPositions();
    }

    private void OnValidate()
    {
        EnsureFlushClosedPositions();
    }

    public void EnsureFlushClosedPositions()
    {
        if (leftDoor == null)
        {
            Transform foundLeft = transform.Find("Left_Sliding_Door");
            if (foundLeft != null) leftDoor = foundLeft;
        }
        if (rightDoor == null)
        {
            Transform foundRight = transform.Find("Right_Sliding_Door");
            if (foundRight != null) rightDoor = foundRight;
        }

        // Each door panel base mesh is 1.30m wide (half-width 0.65m).
        // Scaling X by 1.0154f makes each panel 1.32m wide (half-width 0.66m).
        // Placing closed centers at -0.66m and +0.66m makes the inner stiles meet
        // 100% flush at x = 0.00m with zero gap, while outer edges reach +/- 1.32m
        // to seal completely against the side jambs (+/- 1.31m).
        leftClosedPos = new Vector3(-0.66f, 0f, 0f);
        rightClosedPos = new Vector3(0.66f, 0f, 0f);

        Vector3 targetScale = new Vector3(1.0154f, 1f, 1f);
        if (leftDoor != null && Vector3.Distance(leftDoor.localScale, targetScale) > 0.001f)
        {
            leftDoor.localScale = targetScale;
        }
        if (rightDoor != null && Vector3.Distance(rightDoor.localScale, targetScale) > 0.001f)
        {
            rightDoor.localScale = targetScale;
        }
    }

    private void Update()
    {
        if (leftDoor == null || rightDoor == null)
        {
            EnsureFlushClosedPositions();
            if (leftDoor == null || rightDoor == null) return;
        }

        // Keep closed positions locked to flush center alignment even if old scene values were serialized
        if (Mathf.Abs(leftClosedPos.x - (-0.66f)) > 0.001f || Mathf.Abs(rightClosedPos.x - 0.66f) > 0.001f)
        {
            EnsureFlushClosedPositions();
        }

        // Auto motion sensor detection at runtime
        if (Application.isPlaying && autoTrigger)
        {
            CheckPlayerProximity();
        }

        Vector3 targetLeft = isOpen ? leftClosedPos - Vector3.right * openDistance : leftClosedPos;
        Vector3 targetRight = isOpen ? rightClosedPos + Vector3.right * openDistance : rightClosedPos;

        if (Application.isPlaying)
        {
            // Smoothly animate the sliding doors and snap tight over the final millimeter so no gap remains
            Vector3 nextLeft = Vector3.Lerp(leftDoor.localPosition, targetLeft, Time.deltaTime * speed);
            Vector3 nextRight = Vector3.Lerp(rightDoor.localPosition, targetRight, Time.deltaTime * speed);

            nextLeft = Vector3.MoveTowards(nextLeft, targetLeft, Time.deltaTime * 0.35f);
            nextRight = Vector3.MoveTowards(nextRight, targetRight, Time.deltaTime * 0.35f);

            if (Vector3.Distance(nextLeft, targetLeft) < 0.0015f) nextLeft = targetLeft;
            if (Vector3.Distance(nextRight, targetRight) < 0.0015f) nextRight = targetRight;

            leftDoor.localPosition = nextLeft;
            rightDoor.localPosition = nextRight;
        }
        else
        {
            // Instantly snap to target state in the Editor view
            leftDoor.localPosition = targetLeft;
            rightDoor.localPosition = targetRight;
        }
    }

    private void CheckPlayerProximity()
    {
        if (playerTransform == null)
        {
            var fps = Object.FindAnyObjectByType<SimpleFPSController>();
            if (fps != null)
            {
                playerTransform = fps.transform;
            }
            else if (HorrorInteractionReticle.CurrentPlayerCamera != null)
            {
                playerTransform = HorrorInteractionReticle.CurrentPlayerCamera.transform;
            }
            else
            {
                GameObject playerObj = GameObject.Find("Test_FPS_Player");
                if (playerObj != null) playerTransform = playerObj.transform;
            }
        }

        float minActorDist = float.MaxValue;

        if (playerTransform != null)
        {
            // Measure horizontal distance from player to the entrance doorway center
            Vector3 diff = playerTransform.position - transform.position;
            diff.y = 0f;
            minActorDist = Mathf.Min(minActorDist, diff.magnitude);
        }

        if (CustomerNpcSteve.Instance != null && CustomerNpcSteve.Instance.IsNpcActiveInWorld)
        {
            Vector3 npcDiff = CustomerNpcSteve.Instance.transform.position - transform.position;
            npcDiff.y = 0f;
            minActorDist = Mathf.Min(minActorDist, npcDiff.magnitude);
        }

        if (CustomerNpcMelia.Instance != null && CustomerNpcMelia.Instance.IsNpcActiveInWorld)
        {
            Vector3 meliaDiff = CustomerNpcMelia.Instance.transform.position - transform.position;
            meliaDiff.y = 0f;
            minActorDist = Mathf.Min(minActorDist, meliaDiff.magnitude);
        }

        if (CoworkerNpcBilly.Instance != null && CoworkerNpcBilly.Instance.IsNpcActiveInWorld)
        {
            Vector3 billyDiff = CoworkerNpcBilly.Instance.transform.position - transform.position;
            billyDiff.y = 0f;
            minActorDist = Mathf.Min(minActorDist, billyDiff.magnitude);
        }

        if (StrangeNpcJunk.Instance != null && StrangeNpcJunk.Instance.IsNpcActiveInWorld)
        {
            Vector3 junkDiff = StrangeNpcJunk.Instance.transform.position - transform.position;
            junkDiff.y = 0f;
            minActorDist = Mathf.Min(minActorDist, junkDiff.magnitude);
        }

        if (Act4JohnNpc.Instance != null && Act4JohnNpc.Instance.IsNpcActiveInWorld)
        {
            Vector3 johnDiff = Act4JohnNpc.Instance.transform.position - transform.position;
            johnDiff.y = 0f;
            minActorDist = Mathf.Min(minActorDist, johnDiff.magnitude);
        }

        if (minActorDist < float.MaxValue)
        {
            if (minActorDist <= sensorRadius)
            {
                isOpen = true;
            }
            else if (minActorDist > sensorRadius + 0.35f)
            {
                isOpen = false;
            }
        }
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null) return false;
        if (other.GetComponentInParent<SimpleFPSController>() != null) return true;
        if (other.GetComponentInParent<CharacterController>() != null) return true;
        if (other.GetComponentInParent<CustomerNpcSteve>() != null) return true;
        if (other.GetComponentInParent<CustomerNpcMelia>() != null) return true;
        if (other.GetComponentInParent<CoworkerNpcBilly>() != null) return true;
        if (other.GetComponentInParent<StrangeNpcJunk>() != null) return true;
        if (other.GetComponentInParent<Act4JohnNpc>() != null) return true;
        return other.gameObject.name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (autoTrigger && IsPlayerCollider(other))
        {
            isOpen = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (autoTrigger && IsPlayerCollider(other))
        {
            // Re-evaluate all nearby actors before closing so one actor exiting never snaps the door shut on another
            CheckPlayerProximity();
        }
    }
}

using UnityEngine;

/// <summary>
/// Attached directly to user-placed invisible wall GameObjects (e.g. InvisWall, InvisWall2, InvisWall3, InvisWall4).
/// Ensures the wall is 100% invisible (no visible mesh/renderer) while maintaining a solid physical BoxCollider
/// that prevents the player from leaving the designated area.
/// Plays player thought dialogue when bumped into: "You: I can't leave yet, I still have to work."
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class InvisibleWorldBorder : MonoBehaviour
{
    [Header("Player Feedback")]
    [Tooltip("Show warning dialogue when player bumps into the invisible wall.")]
    public bool showWarningDialogue = true;

    [Tooltip("Warning dialogue text when the player attempts to walk too far away.")]
    public string boundaryWarningText = "You: I can't leave yet, I still have to work.";

    private static float lastWarningTime = -10f;
    private BoxCollider boxCollider;

    private void Awake()
    {
        ApplyInvisibleWallSettings();
    }

    private void OnValidate()
    {
        ApplyInvisibleWallSettings();
    }

    private void Update()
    {
        if (!Application.isPlaying || !showWarningDialogue) return;
        if (Time.time - lastWarningTime < 3.0f) return;

        if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return;

        CharacterController cc = cam.GetComponentInParent<CharacterController>();
        Vector3 playerPos = cc != null ? cc.transform.position : cam.transform.position;

        // Proximity detection: if player touches or presses against this wall
        Vector3 closest = boxCollider.ClosestPoint(playerPos);
        float distSq = (closest - playerPos).sqrMagnitude;
        if (distSq < 0.85f * 0.85f)
        {
            TriggerBoundaryDialogue();
        }
    }

    public void ApplyInvisibleWallSettings()
    {
        // 1. Ensure solid BoxCollider
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null) boxCollider = gameObject.AddComponent<BoxCollider>();

        boxCollider.isTrigger = false;
        boxCollider.enabled = true;

        // 2. Ensure completely invisible: disable/destroy MeshRenderer and MeshFilter
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.enabled = false;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(mr);
            }
#endif
        }

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(mf);
            }
#endif
        }

        // 3. Clean up any accidental legacy child walls from earlier versions
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && (child.name.StartsWith("Wall_") || child.name.Contains("Wall")))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(child.gameObject);
                }
                else
                {
                    Destroy(child.gameObject);
                }
#else
                Destroy(child.gameObject);
#endif
            }
        }
    }

    public void TriggerBoundaryDialogue()
    {
        if (!showWarningDialogue) return;
        TriggerGlobalBoundaryDialogue(boundaryWarningText);
    }

    public static void TriggerGlobalBoundaryDialogue(string message = null)
    {
        if (Time.time - lastWarningTime < 3.0f) return;
        lastWarningTime = Time.time;

        string textToDisplay = !string.IsNullOrEmpty(message)
            ? message
            : "You: I can't leave yet, I still have to work.";

        if (NightShiftStoryController.Instance != null)
        {
            NightShiftStoryController.Instance.PlayNonBlockingSideDialogue(textToDisplay);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckPlayerCollision(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        CheckPlayerCollision(collision.gameObject);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        CheckPlayerCollision(hit.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckPlayerCollision(other.gameObject);
    }

    private void CheckPlayerCollision(GameObject other)
    {
        if (!showWarningDialogue) return;

        bool isPlayer = other.CompareTag("Player") ||
                        other.name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        other.GetComponent<CharacterController>() != null ||
                        other.GetComponentInParent<CharacterController>() != null;

        if (isPlayer)
        {
            TriggerBoundaryDialogue();
        }
    }

    private void OnDrawGizmos()
    {
        // Draw subtle wireframe in Scene view so the developer can see where the invisible wall is placed
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null) return;

        Gizmos.color = new Color(0.2f, 0.85f, 1.0f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
    }
}

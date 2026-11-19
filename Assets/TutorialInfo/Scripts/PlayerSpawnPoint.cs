using UnityEngine;

/// <summary>
/// Defines a player spawn or teleport location in a scene.
/// Includes visual editor gizmos for exact positioning and rotation.
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    [Tooltip("Identifier for this spawn point (e.g., 'Default', 'FromGasStation', 'FromHouse')")]
    public string spawnId = "Default";

    [Tooltip("Scene name this spawn point connects from, or leave empty for default")]
    public string incomingFromScene = "";

    private void OnDrawGizmos()
    {
        // Visual indicator in Scene view: Cyan wire cylinder and forward arrow
        Gizmos.color = new Color(0f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.8f);

        // Forward looking direction arrow
        Gizmos.color = Color.yellow;
        Vector3 eyePos = transform.position + Vector3.up * 1.6f;
        Gizmos.DrawRay(eyePos, transform.forward * 1.5f);
        Gizmos.DrawLine(eyePos + transform.forward * 1.5f, eyePos + transform.forward * 1.1f + transform.right * 0.25f);
        Gizmos.DrawLine(eyePos + transform.forward * 1.5f, eyePos + transform.forward * 1.1f - transform.right * 0.25f);

        // Ground disc
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.45f);
    }
}

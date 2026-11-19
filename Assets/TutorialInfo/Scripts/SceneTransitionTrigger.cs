using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Triggers a clean single-scene transition to another scene (e.g. between GasScene2 and HouseScene).
/// Supports both direct walk-in triggers and reticle interaction ([E]).
/// </summary>
public class SceneTransitionTrigger : MonoBehaviour, IInteractable
{
    [Header("Destination Settings")]
    [Tooltip("Target scene name to load (must be in Build Settings)")]
    public string targetSceneName = "HouseScene";

    [Tooltip("Matching spawn point ID in the target scene")]
    public string targetSpawnId = "Default";

    [Header("Interaction Settings")]
    [Tooltip("Whether to trigger automatically when player walks into the collider")]
    public bool triggerOnWalkIn = true;

    [Tooltip("Whether to allow [E] interaction when aiming at this object")]
    public bool allowInteractPrompt = true;

    [Tooltip("Reticle interaction text displayed to player")]
    public string promptText = "Travel to Cabin [E]";

    private bool isTransitioning = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnWalkIn || isTransitioning) return;

        if (other.CompareTag("Player") || other.name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            PerformTransition();
        }
    }

    public void Interact()
    {
        if (isTransitioning) return;
        PerformTransition();
    }

    public string GetPromptText()
    {
        return allowInteractPrompt ? promptText : "";
    }

    public void PerformTransition()
    {
        if (isTransitioning) return;
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("[SceneTransitionTrigger] Target scene name is not set!");
            return;
        }

        isTransitioning = true;
        Debug.Log($"[SceneTransitionTrigger] Transitioning to scene '{targetSceneName}' with spawn ID '{targetSpawnId}'...");

        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.TransitionToScene(targetSceneName, targetSpawnId);
        }
        else
        {
            SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
        }
    }
}

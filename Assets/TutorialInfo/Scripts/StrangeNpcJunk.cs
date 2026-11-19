using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls "Junk" (JunkNPC):
/// - Stays hidden until the stockroom trashbag is thrown into the dumpster
/// - Appears directly behind the frozen player ("???: Hi, behind you")
/// - Waits for the player to turn around and look at him, triggering a low-quality startle jumpscare camera shake
/// - Runs the wet-socks dialogue sequence with exact cooldowns
/// - Walks back toward SpawnerNPC (spawn) and disappears; if the player blocks his path, forces the player
///   to move aside and says "Junk: Move please."
/// Supports both flat-sibling segmented PS1 meshes (LowPolyPass.fbx) and hierarchical humanoid bone rigs.
/// </summary>
public class StrangeNpcJunk : MonoBehaviour
{
    public static StrangeNpcJunk Instance { get; private set; }

    public enum NpcState
    {
        HiddenWaiting,
        StandingBehindPlayer,
        JumpscareReaction,
        TalkingWithPlayer,
        WalkingToSpawner,
        DepartedHidden
    }

    [Header("State & Settings")]
    public NpcState currentState = NpcState.HiddenWaiting;
    public float walkSpeed = 1.38f;
    public float turnSpeed = 6.0f;
    public float ps1AnimFps = 12f;

    public bool IsNpcActiveInWorld => isNpcVisible && gameObject.activeInHierarchy;

    // Scene references
    private Transform spawnerNpcTransform;
    private Vector3 spawnerTargetPosition = new Vector3(-30.30f, -0.04f, -13.20f);
    private AutomaticSlidingDoors slidingDoors;

    // Renderers & collider on JunkNPC
    private Renderer[] npcRenderers;
    private CapsuleCollider npcCapsule;
    private bool isNpcVisible = false;

    // Rig parts & bind pose (supports flat-sibling LowPolyPass.fbx & hierarchical rigs)
    private Transform pelvis, spine, spine1, chest, neck, head;
    private Transform clothPlane, beltTorus1, beltTorus2;
    private Transform shoulderL, upperArmL, forearmL, handL, fingerL, thumbL;
    private Transform shoulderR, upperArmR, forearmR, handR, fingerR, thumbR;
    private Transform thighL, shinL, footL, footLEnd;
    private Transform thighR, shinR, footR, footREnd;

    private readonly Dictionary<Transform, Vector3> bindLocalPos = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Quaternion> bindLocalRots = new Dictionary<Transform, Quaternion>();
    private Vector3 modelForwardLocal = Vector3.forward;
    private Vector3 modelRightLocal = Vector3.right;
    private Quaternion modelUprightRotation = Quaternion.identity;
    private bool isZUpModel = false;
    private float modelYawCorrectionDeg = 0f;
    private float footLSoleOffset = 0.08f;
    private float footRSoleOffset = 0.08f;
    private bool rigInitialized = false;

    // Procedural animation runtime state
    private bool isCurrentlyWalking = false;
    private bool isJunkSpeaking = false;
    private bool isJumpscareTwitching = false;
    private bool lookAtPlayerHead = false;
    private float walkCycleTimer = 0f;
    private float idleTimer = 0f;
    private float footstepTimer = 0f;
    private float lastMovePleaseTime = -10f;

    // Audio
    private AudioSource npcAudioSource;
    private AudioClip footstepClip;
    private AudioClip jumpscareStingClip;

    public static StrangeNpcJunk EnsureAttachedToSceneNpc()
    {
        if (Instance != null) return Instance;

        StrangeNpcJunk existing = Object.FindAnyObjectByType<StrangeNpcJunk>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        GameObject npcGo = null;

        // Pass 1: Exact name match ("JunkNPC", "Junk_NPC", "Junk NPC", "Junk", "LowPolyPass")
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "JunkNPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Junk_NPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Junk NPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Junk", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "LowPolyPass", System.StringComparison.OrdinalIgnoreCase))
            {
                npcGo = ResolveCharacterRoot(t).gameObject;
                break;
            }
        }

        // Pass 2: Substring match containing "Junk" or "LowPolyPass"
        if (npcGo == null)
        {
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t == null) continue;
                if (t.name.IndexOf("Junk", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.name.IndexOf("LowPolyPass", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    npcGo = ResolveCharacterRoot(t).gameObject;
                    break;
                }
            }
        }

        if (npcGo != null)
        {
            npcGo.SetActive(true);
            Instance = npcGo.AddComponent<StrangeNpcJunk>();
            return Instance;
        }

        return null;
    }

    private static Transform ResolveCharacterRoot(Transform candidate)
    {
        Transform cur = candidate;
        while (cur.parent != null)
        {
            if (cur.parent.GetComponent<Animator>() != null ||
                cur.parent.name.IndexOf("Junk", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                cur.parent.name.IndexOf("LowPolyPass", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cur = cur.parent;
            }
            else
            {
                break;
            }
        }
        return cur;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;

        Animator anim = GetComponentInChildren<Animator>(true);

        npcRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < npcRenderers.Length; i++)
        {
            if (npcRenderers[i] is SkinnedMeshRenderer smr)
            {
                smr.updateWhenOffscreen = true;
            }
        }

        CacheRigAndBindPose(anim);
        EnsureNpcCollider();

        if (anim != null)
        {
            anim.enabled = false;
        }

        SetupProceduralAudio();
        ResolveSceneTargets();

        // JunkNPC stays hidden until the player throws the trash into the dumpster
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

        if (isZUpModel)
        {
            float sz = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.z));
            float sx = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
            npcCapsule.direction = 2; // Z-axis is vertical height in LowPolyPass.fbx
            npcCapsule.height = 1.65f / sz;
            npcCapsule.radius = 0.30f / sx;
            npcCapsule.center = new Vector3(0f, 0f, (0.825f / sz));
        }
        else
        {
            float sy = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
            float sx = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
            npcCapsule.direction = 1; // Y-axis
            npcCapsule.height = 1.78f / sy;
            npcCapsule.radius = 0.32f / sx;
            npcCapsule.center = new Vector3(0f, (0.89f / sy), 0f);
        }
        npcCapsule.isTrigger = false;
    }

    private void ResolveSceneTargets()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == transform || t.IsChildOf(transform)) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "SpawnerNPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Spawner_NPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Spawner NPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "NPCSpawner", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "NPC Spawner", System.StringComparison.OrdinalIgnoreCase))
            {
                spawnerNpcTransform = t;
                break;
            }
        }

        if (spawnerNpcTransform == null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t == transform || t.IsChildOf(transform)) continue;
                if (t.name.IndexOf("Spawner", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    spawnerNpcTransform = t;
                    break;
                }
            }
        }

        if (spawnerNpcTransform != null)
        {
            spawnerTargetPosition = spawnerNpcTransform.position;

            Renderer[] rends = spawnerNpcTransform.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] != null) rends[i].enabled = false;
            }

            Collider[] cols = spawnerNpcTransform.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].isTrigger = true;
            }
        }

        slidingDoors = Object.FindAnyObjectByType<AutomaticSlidingDoors>();
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
    // APPEAR BEHIND PLAYER, JUMPSCARE & DIALOGUE SEQUENCE
    // =========================================================================

    public void AppearBehindPlayer()
    {
        ResolveSceneTargets();

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();

        Vector3 playerPos = transform.position;
        Vector3 flatCamForward = Vector3.forward;

        if (cam != null)
        {
            CharacterController cc = cam.GetComponentInParent<CharacterController>();
            playerPos = cc != null ? cc.transform.position : (cam.transform.position - Vector3.up * 1.45f);

            flatCamForward = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z);
            if (flatCamForward.sqrMagnitude < 0.001f)
            {
                flatCamForward = Vector3.forward;
            }
            else
            {
                flatCamForward.Normalize();
            }
        }

        Vector3 behindDir = -flatCamForward;
        float desiredDist = 1.48f;
        Vector3 chosenSpot = playerPos + behindDir * desiredDist;

        // Pick the clearest spot directly behind the player (avoiding walls or dumpsters)
        float[] candidateAngles = new float[] { 0f, 22f, -22f, 42f, -42f, 65f, -65f, 88f, -88f };
        for (int i = 0; i < candidateAngles.Length; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(candidateAngles[i], Vector3.up) * behindDir;
            Vector3 candidate = playerPos + dir * desiredDist;
            candidate.y = SampleGroundHeightImmediate(candidate, playerPos.y);

            Vector3 checkCenter = candidate + Vector3.up * 0.90f;
            Collider[] overlaps = Physics.OverlapSphere(checkCenter, 0.30f, ~0, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            if (overlaps != null)
            {
                for (int c = 0; c < overlaps.Length; c++)
                {
                    Collider col = overlaps[c];
                    if (col == null || col.transform.IsChildOf(transform)) continue;
                    if (col.GetComponentInParent<CharacterController>() != null) continue;
                    if (col.bounds.max.y <= candidate.y + 0.24f) continue;
                    blocked = true;
                    break;
                }
            }

            if (!blocked)
            {
                chosenSpot = candidate;
                break;
            }
        }

        chosenSpot.y = SampleGroundHeightImmediate(chosenSpot, playerPos.y);
        transform.position = chosenSpot;

        // Face directly toward the player while standing upright
        Vector3 toPlayer = new Vector3(playerPos.x - chosenSpot.x, 0f, playerPos.z - chosenSpot.z);
        if (toPlayer.sqrMagnitude > 0.001f)
        {
            transform.rotation = GetTargetWorldRotation(toPlayer.normalized);
        }
        else
        {
            transform.rotation = GetTargetWorldRotation(flatCamForward);
        }

        lookAtPlayerHead = true;
        isCurrentlyWalking = false;
        isJunkSpeaking = false;
        isJumpscareTwitching = false;
        currentState = NpcState.StandingBehindPlayer;
        SetNpcVisible(true);
    }

    public IEnumerator WaitForPlayerToLookBackAndJumpscareRoutine()
    {
        currentState = NpcState.StandingBehindPlayer;
        lookAtPlayerHead = true;

        // Wait until the player turns around to look at JunkNPC behind them
        while (true)
        {
            Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
            if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
            if (cam != null)
            {
                // Keep facing the player
                Vector3 toCamFlat = new Vector3(cam.transform.position.x - transform.position.x, 0f, cam.transform.position.z - transform.position.z);
                if (toCamFlat.sqrMagnitude > 0.001f)
                {
                    RotateTowardsWorldDir(toCamFlat.normalized, Time.deltaTime * turnSpeed * 2.0f);
                }

                Vector3 junkTarget = (head != null) ? head.position : (transform.position + Vector3.up * 1.45f);
                Vector3 camToJunkFlat = new Vector3(junkTarget.x - cam.transform.position.x, 0f, junkTarget.z - cam.transform.position.z).normalized;
                Vector3 camFwdFlat = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z).normalized;

                if (Vector3.Dot(camFwdFlat, camToJunkFlat) >= 0.56f)
                {
                    break;
                }
            }
            yield return null;
        }

        // Trigger low-quality jumpscare (human startle camera flinch & shake + startle sting + brief NPC twitch)
        currentState = NpcState.JumpscareReaction;
        isJumpscareTwitching = true;
        PlayOneShotClip(jumpscareStingClip, 0.90f, 1.0f);

        if (RetroHorrorPOV.Instance != null)
        {
            RetroHorrorPOV.Instance.TriggerStartleJumpscareShake(0.68f);
        }

        yield return new WaitForSeconds(0.68f);
        isJumpscareTwitching = false;
        currentState = NpcState.TalkingWithPlayer;
    }

    public IEnumerator RunPostJumpscareDialogueRoutine()
    {
        currentState = NpcState.TalkingWithPlayer;
        lookAtPlayerHead = true;

        // 0.5 seconds cooldown after camera returns to normal state
        yield return new WaitForSeconds(0.5f);

        NightShiftStoryController story = NightShiftStoryController.Instance;
        if (story != null)
        {
            // You: YOU SCARED ME, WHAT DO YOU WANT?
            yield return StartCoroutine(story.PlayCustomDialogueRoutine("You: YOU SCARED ME, WHAT DO YOU WANT?"));

            // 1 second cooldown
            yield return new WaitForSeconds(1.0f);

            // You: WHO ARE YOU?
            yield return StartCoroutine(story.PlayCustomDialogueRoutine("You: WHO ARE YOU?"));

            // 1 second cooldown
            yield return new WaitForSeconds(1.0f);

            // Junk: My name is Junk, I just want to buy wet socks.
            isJunkSpeaking = true;
            yield return StartCoroutine(story.PlayCustomDialogueRoutine("Junk: My name is Junk, I just want to buy wet socks."));
            isJunkSpeaking = false;

            // 1.5 seconds cooldown
            yield return new WaitForSeconds(1.5f);

            // You: You're weird, we don't sell those!
            yield return StartCoroutine(story.PlayCustomDialogueRoutine("You: You're weird, we don't sell those!"));

            // Junk: Oh ok.
            isJunkSpeaking = true;
            yield return StartCoroutine(story.PlayCustomDialogueRoutine("Junk: Oh ok."));
            isJunkSpeaking = false;

            // 1 second cooldown before Junk walks toward spawn to disappear
            yield return new WaitForSeconds(1.0f);
        }

        isJunkSpeaking = false;
        lookAtPlayerHead = false;
    }

    public Coroutine StartWalkToSpawnerAndDisappear()
    {
        return StartCoroutine(WalkToSpawnerAndDisappearRoutine());
    }

    private IEnumerator WalkToSpawnerAndDisappearRoutine()
    {
        ResolveSceneTargets();
        lookAtPlayerHead = false;
        isJunkSpeaking = false;
        currentState = NpcState.WalkingToSpawner;

        Vector3 targetPos = spawnerNpcTransform != null ? spawnerNpcTransform.position : spawnerTargetPosition;
        List<Vector3> path = BuildObstacleFreePathToSpawner(transform.position, targetPos);

        yield return StartCoroutine(FollowWaypointsRoutine(path));

        SetNpcVisible(false);
        currentState = NpcState.DepartedHidden;
    }

    // =========================================================================
    // OBSTACLE-FREE NAVIGATION & PLAYER BLOCKING PUSH ("Junk: Move please.")
    // =========================================================================

    private List<Vector3> BuildObstacleFreePathToSpawner(Vector3 start, Vector3 targetSpawner)
    {
        List<Vector3> pts = new List<Vector3>();

        // Dumpsters are outside the east wall of the store (X ~ 5.7, Z ~ -5.1).
        // Route cleanly south around the south-east exterior corner of the building (X = 6.2, Z = -10.8),
        // then west along the south driveway (-5.6, -10.8) -> (-11.5, -12.0) -> (-25.5, -12.8) -> SpawnerNPC!
        if (start.x > -1.80f && start.z > -9.60f)
        {
            pts.Add(new Vector3(Mathf.Max(6.15f, start.x + 0.35f), start.y, -10.80f));
        }

        if (start.x > -5.20f)
        {
            pts.Add(new Vector3(-5.60f, 0.14f, -10.80f));
        }

        if (start.x > -11.00f && targetSpawner.x < -13.00f)
        {
            pts.Add(new Vector3(-11.50f, 0.08f, -12.00f));
        }

        if (start.x > -24.00f && targetSpawner.x < -25.00f)
        {
            pts.Add(new Vector3(-25.50f, 0.02f, Mathf.Lerp(-12.20f, targetSpawner.z, 0.65f)));
        }

        pts.Add(targetSpawner);
        return pts;
    }

    private IEnumerator FollowWaypointsRoutine(List<Vector3> waypoints)
    {
        isCurrentlyWalking = true;

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 target = waypoints[i];
            bool isFinalWaypoint = (i == waypoints.Count - 1);
            float arriveThreshold = isFinalWaypoint ? 0.35f : 0.22f;
            float waypointElapsed = 0f;

            while (true)
            {
                waypointElapsed += Time.deltaTime;
                if (waypointElapsed > 25f)
                {
                    break;
                }

                if (isFinalWaypoint && spawnerNpcTransform != null)
                {
                    target = spawnerNpcTransform.position;
                }

                Vector3 cur = transform.position;
                Vector3 toTarget = new Vector3(target.x - cur.x, 0f, target.z - cur.z);
                float dist = toTarget.magnitude;
                if (dist <= arriveThreshold)
                {
                    break;
                }

                Vector3 desiredDir = toTarget.normalized;

                // If player blocks JunkNPC, force the player to move aside and say "Junk: Move please."
                if (CheckAndHandlePlayerBlockingPath(desiredDir))
                {
                    // Player is actively being pushed out of the way; keep walking smoothly
                }

                Vector3 moveDir = ComputeObstacleFreeMoveDirection(cur, desiredDir);
                RotateTowardsWorldDir(moveDir, Time.deltaTime * turnSpeed);

                float step = walkSpeed * Time.deltaTime;
                if (step > dist) step = dist;

                Vector3 nextPos = cur + moveDir * step;
                nextPos.y = SampleGroundHeight(nextPos, cur.y);
                transform.position = nextPos;

                AdvanceWalkAudioAndTimer(1.0f);
                yield return null;
            }
        }

        isCurrentlyWalking = false;
    }

    private bool CheckAndHandlePlayerBlockingPath(Vector3 moveDir)
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        CharacterController playerCC = cam.GetComponentInParent<CharacterController>();
        Transform playerRoot = playerCC != null ? playerCC.transform : cam.transform;

        Vector3 pPos = playerRoot.position;
        Vector3 toPlayer = new Vector3(pPos.x - transform.position.x, 0f, pPos.z - transform.position.z);
        float d = toPlayer.magnitude;

        if (d < 1.15f && d > 0.001f)
        {
            float forwardDot = Vector3.Dot(toPlayer.normalized, moveDir);
            if (forwardDot > 0.22f || d < 0.72f)
            {
                // Compute sideways + outward push vector so player is forced out of JunkNPC's path
                Vector3 rightPerp = new Vector3(moveDir.z, 0f, -moveDir.x).normalized;
                float sideSign = Vector3.Dot(toPlayer, rightPerp) >= 0f ? 1f : -1f;
                Vector3 pushDir = (rightPerp * sideSign * 0.85f + toPlayer.normalized * 0.45f).normalized;

                if (playerCC != null && playerCC.enabled)
                {
                    playerCC.Move(pushDir * (4.2f * Time.deltaTime));
                }
                else
                {
                    playerRoot.position += pushDir * (4.2f * Time.deltaTime);
                }

                // Trigger "Junk: Move please." with cooldown
                if (Time.time - lastMovePleaseTime >= 3.4f)
                {
                    lastMovePleaseTime = Time.time;
                    if (NightShiftStoryController.Instance != null)
                    {
                        NightShiftStoryController.Instance.PlayNonBlockingSideDialogue("Junk: Move please.");
                    }
                }

                return true;
            }
        }

        return false;
    }

    private Vector3 ComputeObstacleFreeMoveDirection(Vector3 currentPos, Vector3 desiredDir)
    {
        Vector3 checkCenter = currentPos + Vector3.up * 0.85f;
        Collider[] hits = Physics.OverlapSphere(checkCenter, 0.52f, ~0, QueryTriggerInteraction.Ignore);
        Vector3 repulsion = Vector3.zero;

        if (hits != null && hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i];
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue;
                if (col.GetComponentInParent<CharacterController>() != null) continue;
                if (spawnerNpcTransform != null && col.transform.IsChildOf(spawnerNpcTransform)) continue;

                if (col.bounds.max.y <= currentPos.y + 0.24f) continue;
                if (col.bounds.min.y >= currentPos.y + 1.80f) continue;

                Vector3 closest = col.ClosestPoint(checkCenter);
                Vector3 push = new Vector3(checkCenter.x - closest.x, 0f, checkCenter.z - closest.z);
                float d = push.magnitude;
                if (d < 0.50f && d > 0.0001f)
                {
                    repulsion += push.normalized * ((0.50f - d) / 0.50f);
                }
            }
        }

        if (Physics.SphereCast(checkCenter, 0.26f, desiredDir, out RaycastHit forwardHit, 0.75f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (forwardHit.collider != null &&
                !forwardHit.collider.transform.IsChildOf(transform) &&
                forwardHit.collider.GetComponentInParent<CharacterController>() == null &&
                (spawnerNpcTransform == null || !forwardHit.collider.transform.IsChildOf(spawnerNpcTransform)) &&
                forwardHit.collider.bounds.max.y > currentPos.y + 0.26f &&
                Mathf.Abs(forwardHit.normal.y) < 0.55f)
            {
                Vector3 n = new Vector3(forwardHit.normal.x, 0f, forwardHit.normal.z).normalized;
                Vector3 slide = Vector3.Cross(Vector3.up, n);
                if (Vector3.Dot(slide, desiredDir) < 0f) slide = -slide;
                repulsion += (n * 0.65f + slide * 0.95f);
            }
        }

        if (repulsion.sqrMagnitude < 0.0001f) return desiredDir;

        Vector3 blended = (desiredDir + repulsion * 1.45f);
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
                n.IndexOf("Dumpster", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Trashbag", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Spawner", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
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
        float targetY = SampleGroundHeightImmediate(pos, currentY);
        return Mathf.Lerp(currentY, targetY, Mathf.Clamp01(Time.deltaTime * 22f));
    }

    private float SampleGroundHeightImmediate(Vector3 pos, float referenceY)
    {
        Vector3 origin = new Vector3(pos.x, referenceY + 1.0f, pos.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 4.0f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return referenceY;

        float highestFloorY = float.MinValue;
        float maxStepUpY = referenceY + 0.55f;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (h.normal.y < 0.65f) continue;
            if (h.point.y > maxStepUpY) continue;
            if (IsNonFloorPropCollider(h.collider)) continue;

            if (h.point.y > highestFloorY)
            {
                highestFloorY = h.point.y;
            }
        }

        return highestFloorY > float.MinValue ? highestFloorY : referenceY;
    }

    private Quaternion GetTargetWorldRotation(Vector3 worldForwardFlat)
    {
        if (worldForwardFlat.sqrMagnitude < 0.0001f)
        {
            worldForwardFlat = Vector3.forward;
        }
        Quaternion lookRot = Quaternion.LookRotation(worldForwardFlat.normalized, Vector3.up);
        return lookRot * Quaternion.Euler(0f, modelYawCorrectionDeg, 0f) * modelUprightRotation;
    }

    private void RotateTowardsWorldDir(Vector3 worldForwardDir, float maxRadiansDelta)
    {
        if (worldForwardDir.sqrMagnitude < 0.0001f) return;
        Quaternion correctedTarget = GetTargetWorldRotation(worldForwardDir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, correctedTarget, Mathf.Clamp01(maxRadiansDelta));
    }

    private void AdvanceWalkAudioAndTimer(float speedMult)
    {
        walkCycleTimer += Time.deltaTime * 5.2f * speedMult;
        footstepTimer += Time.deltaTime * speedMult;
        if (footstepTimer >= 0.55f)
        {
            footstepTimer = 0f;
            PlayOneShotClip(footstepClip, 0.25f, Random.Range(0.88f, 1.06f));
        }
    }

    // =========================================================================
    // PROCEDURAL PS1 BONE RIGGING & ANIMATION (SUPPORTS LOWPOLYPASS & HUMANOID)
    // =========================================================================

    private Vector3 CharacterRightWorld => transform.TransformDirection(modelRightLocal).normalized;
    private Vector3 CharacterForwardWorld => transform.TransformDirection(modelForwardLocal).normalized;

    private void CacheRigAndBindPose(Animator anim)
    {
        Quaternion savedRot = transform.rotation;
        transform.rotation = Quaternion.identity;

        if (anim != null && anim.avatar != null && anim.isHuman)
        {
            pelvis = anim.GetBoneTransform(HumanBodyBones.Hips);
            spine = anim.GetBoneTransform(HumanBodyBones.Spine);
            chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
            neck = anim.GetBoneTransform(HumanBodyBones.Neck);
            head = anim.GetBoneTransform(HumanBodyBones.Head);

            shoulderL = anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
            upperArmL = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            forearmL = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            handL = anim.GetBoneTransform(HumanBodyBones.LeftHand);

            shoulderR = anim.GetBoneTransform(HumanBodyBones.RightShoulder);
            upperArmR = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            forearmR = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            handR = anim.GetBoneTransform(HumanBodyBones.RightHand);

            thighL = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            shinL = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            footL = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            footLEnd = anim.GetBoneTransform(HumanBodyBones.LeftToes);

            thighR = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            shinR = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            footR = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            footREnd = anim.GetBoneTransform(HumanBodyBones.RightToes);
        }

        Transform[] allBones = GetComponentsInChildren<Transform>(true);
        bindLocalPos.Clear();
        bindLocalRots.Clear();

        for (int i = 0; i < allBones.Length; i++)
        {
            Transform b = allBones[i];
            if (b == transform) continue;
            bindLocalPos[b] = b.localPosition;
            bindLocalRots[b] = b.localRotation;

            string rawName = b.name;
            string n = rawName;
            int colonIdx = n.IndexOf(':');
            if (colonIdx >= 0 && colonIdx + 1 < n.Length)
            {
                n = n.Substring(colonIdx + 1);
            }

            if (pelvis == null && (n == "Hips" || n == "hips" || n == "pelvis")) pelvis = b;
            else if (spine == null && (n == "Spine" || n == "spine")) spine = b;
            else if (spine1 == null && n == "Spine1") spine1 = b;
            else if (chest == null && (n == "Spine2" || n == "chest" || n == "Chest")) chest = b;
            else if (neck == null && (n == "Neck" || n == "neck")) neck = b;
            else if (head == null && (n == "Head" || n == "head")) head = b;
            else if (clothPlane == null && n == "Plane") clothPlane = b;
            else if (beltTorus1 == null && n == "Torus.001") beltTorus1 = b;
            else if (beltTorus2 == null && n == "Torus.002") beltTorus2 = b;
            else if (shoulderL == null && (n == "LeftShoulder" || n == "shoulder.L" || n == "shoulder_left")) shoulderL = b;
            else if (upperArmL == null && (n == "LArm" || n == "LeftArm" || n == "upper_arm.L" || n == "upper_arm_left")) upperArmL = b;
            else if (forearmL == null && (n == "LForeArm" || n == "LeftForeArm" || n == "forearm.L" || n == "forearm_left")) forearmL = b;
            else if (handL == null && (n == "LHand" || n == "LeftHand" || n == "hand.L" || n == "hand_left")) handL = b;
            else if (fingerL == null && n == "LFinger") fingerL = b;
            else if (thumbL == null && n == "LThumb") thumbL = b;
            else if (shoulderR == null && (n == "RightShoulder" || n == "shoulder.R" || n == "shoulder_right")) shoulderR = b;
            else if (upperArmR == null && (n == "RArm" || n == "RightArm" || n == "upper_arm.R" || n == "upper_arm_right")) upperArmR = b;
            else if (forearmR == null && (n == "RArm.001" || n == "RForeArm" || n == "RightForeArm" || n == "forearm.R" || n == "forearm_right")) forearmR = b;
            else if (handR == null && (n == "RHand" || n == "RightHand" || n == "hand.R" || n == "hand_right")) handR = b;
            else if (fingerR == null && n == "RFinger") fingerR = b;
            else if (thumbR == null && n == "RThumb") thumbR = b;
            else if (thighL == null && (n == "LUpLeg" || n == "LeftUpLeg" || n == "thigh.L" || n == "thigh_left")) thighL = b;
            else if (shinL == null && (n == "LLeg" || n == "LeftLeg" || n == "shin.L" || n == "shin_left")) shinL = b;
            else if (footL == null && (n == "LFoot" || n == "LeftFoot" || n == "foot.L" || n == "foot_left")) footL = b;
            else if (footLEnd == null && (n == "LeftToeBase" || n == "LeftToe_End" || n == "toe.L" || n == "foot_left_end")) footLEnd = b;
            else if (thighR == null && (n == "RUpLeg" || n == "RightUpLeg" || n == "thigh.R" || n == "thigh_right")) thighR = b;
            else if (shinR == null && (n == "RLeg" || n == "RightLeg" || n == "shin.R" || n == "shin_right")) shinR = b;
            else if (footR == null && (n == "RFoot" || n == "RightFoot" || n == "foot.R" || n == "foot_right")) footR = b;
            else if (footREnd == null && (n == "RightToeBase" || n == "RightToe_End" || n == "toe.R" || n == "foot_right_end")) footREnd = b;
        }

        if (chest == null && spine1 != null) chest = spine1;

        // Detect if the FBX root is Z-up (e.g. LowPolyPass.fbx exported from Blender where feet->head is along local +Z)
        isZUpModel = false;
        modelUprightRotation = Quaternion.identity;
        if (head != null && (footL != null || pelvis != null))
        {
            Transform lowerRef = footL != null ? footL : pelvis;
            Vector3 spanLocal = transform.InverseTransformPoint(head.position) - transform.InverseTransformPoint(lowerRef.position);
            if (Mathf.Abs(spanLocal.z) > Mathf.Abs(spanLocal.y))
            {
                isZUpModel = true;
                modelUprightRotation = (spanLocal.z >= 0f)
                    ? Quaternion.Euler(-90f, 0f, 0f)
                    : Quaternion.Euler(90f, 0f, 0f);
            }
        }

        // Normalize non-uniform root scale (e.g. X=0.397, Z=0.222) to uniform vertical scale so rotating
        // horizontal T-pose arms/hands downward does not stretch them to ankle length, squash meshes,
        // or open air gaps between flat-sibling limb segments.
        Vector3 rootScale = transform.localScale;
        float absSx = Mathf.Abs(rootScale.x);
        float absSy = Mathf.Abs(rootScale.y);
        float absSz = Mathf.Abs(rootScale.z);
        float minScale = Mathf.Min(absSx, Mathf.Min(absSy, absSz));
        float maxScale = Mathf.Max(absSx, Mathf.Max(absSy, absSz));
        if (minScale > 0.0001f && (maxScale / minScale) > 1.05f)
        {
            float uniformScale = isZUpModel ? absSz : absSy;
            if (uniformScale < 0.05f) uniformScale = minScale;
            transform.localScale = new Vector3(
                rootScale.x < 0f ? -uniformScale : uniformScale,
                rootScale.y < 0f ? -uniformScale : uniformScale,
                rootScale.z < 0f ? -uniformScale : uniformScale);
        }

        // Stand the character upright in world space before measuring forward/right axes and sole offsets
        transform.rotation = modelUprightRotation;

        if (footL != null) footLSoleOffset = Mathf.Max(0.01f, footL.position.y - transform.position.y);
        if (footR != null) footRSoleOffset = Mathf.Max(0.01f, footR.position.y - transform.position.y);

        if (upperArmL != null && upperArmR != null)
        {
            Vector3 rVec = upperArmR.position - upperArmL.position;
            rVec.y = 0f;
            Vector3 uprightWorldRight = rVec.sqrMagnitude > 0.0001f ? rVec.normalized : Vector3.right;
            Vector3 uprightWorldFwd = Vector3.Cross(uprightWorldRight, Vector3.up).normalized;

            // Verify anatomical forward direction using landmarks that genuinely point forward:
            // toes vs foot (Humanoid), foot vs shin, thumb vs hand, head vs neck, or pelvis vs back cloth ("Plane").
            // Note: In LowPolyPass.fbx, "Plane" sits on the back of Hips (+Y in Blender) and Spine2 hunches back,
            // so treating clothPlane - pelvis or chest - pelvis as forward flips the character 180° backwards.
            Vector3 forwardHint = Vector3.zero;
            if (footL != null && footLEnd != null)
            {
                forwardHint = footLEnd.position - footL.position;
            }
            else if (footL != null && shinL != null)
            {
                forwardHint = footL.position - shinL.position;
            }
            else if (footR != null && shinR != null)
            {
                forwardHint = footR.position - shinR.position;
            }
            else if (thumbL != null && handL != null)
            {
                forwardHint = thumbL.position - handL.position;
            }
            else if (head != null && neck != null)
            {
                forwardHint = head.position - neck.position;
            }
            else if (pelvis != null && clothPlane != null)
            {
                forwardHint = pelvis.position - clothPlane.position;
            }

            forwardHint.y = 0f;
            if (forwardHint.sqrMagnitude > 0.0001f && Vector3.Dot(forwardHint.normalized, uprightWorldFwd) < 0f)
            {
                uprightWorldFwd = -uprightWorldFwd;
                uprightWorldRight = -uprightWorldRight;
            }

            float angleFromPlusZ = Vector3.SignedAngle(Vector3.forward, uprightWorldFwd, Vector3.up);
            modelYawCorrectionDeg = -angleFromPlusZ;

            modelForwardLocal = transform.InverseTransformDirection(uprightWorldFwd).normalized;
            modelRightLocal = transform.InverseTransformDirection(uprightWorldRight).normalized;
            rigInitialized = true;
        }

        transform.rotation = GetTargetWorldRotation(Vector3.forward);
    }

    private void LateUpdate()
    {
        if (!isNpcVisible) return;

        idleTimer += Time.deltaTime;

        if (rigInitialized)
        {
            ApplyProceduralPs1Animation();
        }
    }

    private void ApplyProceduralPs1Animation()
    {
        // 1. Restore bind local positions and rotations
        foreach (var kvp in bindLocalPos)
        {
            if (kvp.Key != null) kvp.Key.localPosition = kvp.Value;
        }
        foreach (var kvp in bindLocalRots)
        {
            if (kvp.Key != null) kvp.Key.localRotation = kvp.Value;
        }

        Vector3 wRight = CharacterRightWorld;
        Vector3 wFwd = CharacterForwardWorld;

        // 2. Relax T-pose/A-pose arms down naturally beside JunkNPC's torso using virtual shoulder & elbow pivots
        Vector3 shoulderPivotL = upperArmL != null ? upperArmL.position : transform.position;
        Vector3 shoulderPivotR = upperArmR != null ? upperArmR.position : transform.position;

        if (upperArmL != null && handL != null)
        {
            Vector3 curDirL = (handL.position - upperArmL.position).normalized;
            float segLenL = forearmL != null ? Vector3.Distance(upperArmL.position, forearmL.position) : 0.22f;
            shoulderPivotL = upperArmL.position - curDirL * (segLenL * 0.48f);

            Vector3 relaxDirL = (Vector3.down + curDirL * 0.18f + wFwd * 0.05f).normalized;
            Quaternion qRelaxL = Quaternion.FromToRotation(curDirL, relaxDirL);
            RotateGroupAroundPivot(qRelaxL, shoulderPivotL, upperArmL, forearmL, handL, fingerL, thumbL);
        }

        if (upperArmR != null && handR != null)
        {
            Vector3 curDirR = (handR.position - upperArmR.position).normalized;
            float segLenR = forearmR != null ? Vector3.Distance(upperArmR.position, forearmR.position) : 0.22f;
            shoulderPivotR = upperArmR.position - curDirR * (segLenR * 0.48f);

            Vector3 relaxDirR = (Vector3.down + curDirR * 0.18f + wFwd * 0.05f).normalized;
            Quaternion qRelaxR = Quaternion.FromToRotation(curDirR, relaxDirR);
            RotateGroupAroundPivot(qRelaxR, shoulderPivotR, upperArmR, forearmR, handR, fingerR, thumbR);
        }

        if (upperArmL != null && forearmL != null)
        {
            Vector3 elbowPivotL = Vector3.Lerp(upperArmL.position, forearmL.position, 0.5f);
            RotateGroupAroundPivot(Quaternion.AngleAxis(-12f, wRight), elbowPivotL, forearmL, handL, fingerL, thumbL);
        }
        if (upperArmR != null && forearmR != null)
        {
            Vector3 elbowPivotR = Vector3.Lerp(upperArmR.position, forearmR.position, 0.5f);
            RotateGroupAroundPivot(Quaternion.AngleAxis(-12f, wRight), elbowPivotR, forearmR, handR, fingerR, thumbR);
        }

        float qWalk = Mathf.Floor(walkCycleTimer * (ps1AnimFps / 5.2f)) / (ps1AnimFps / 5.2f);
        float qIdle = Mathf.Floor(idleTimer * ps1AnimFps) / ps1AnimFps;

        if (isCurrentlyWalking)
        {
            float stride = Mathf.Sin(qWalk);
            float swingPass = Mathf.Cos(qWalk);
            float legAngle = stride * 24f;

            // Left Leg Swing (Hip & Knee)
            if (thighL != null)
            {
                Vector3 hipPivotL = new Vector3(
                    thighL.position.x,
                    pelvis != null ? pelvis.position.y : thighL.position.y,
                    thighL.position.z);
                RotateGroupAroundPivot(Quaternion.AngleAxis(-legAngle, wRight), hipPivotL, thighL, shinL, footL, footLEnd);

                if (shinL != null)
                {
                    float kneeBendL = Mathf.Max(0f, swingPass) * 30f;
                    Vector3 kneePivotL = Vector3.Lerp(thighL.position, shinL.position, 0.5f);
                    RotateGroupAroundPivot(Quaternion.AngleAxis(kneeBendL, wRight), kneePivotL, shinL, footL, footLEnd);
                }
            }

            // Right Leg Swing (Hip & Knee)
            if (thighR != null)
            {
                Vector3 hipPivotR = new Vector3(
                    thighR.position.x,
                    pelvis != null ? pelvis.position.y : thighR.position.y,
                    thighR.position.z);
                RotateGroupAroundPivot(Quaternion.AngleAxis(legAngle, wRight), hipPivotR, thighR, shinR, footR, footREnd);

                if (shinR != null)
                {
                    float kneeBendR = Mathf.Max(0f, -swingPass) * 30f;
                    Vector3 kneePivotR = Vector3.Lerp(thighR.position, shinR.position, 0.5f);
                    RotateGroupAroundPivot(Quaternion.AngleAxis(kneeBendR, wRight), kneePivotR, shinR, footR, footREnd);
                }
            }

            //Opposite Arm Swing
            if (upperArmL != null)
            {
                RotateGroupAroundPivot(Quaternion.AngleAxis(legAngle * 0.62f, wRight), shoulderPivotL, upperArmL, forearmL, handL, fingerL, thumbL);
            }
            if (upperArmR != null)
            {
                RotateGroupAroundPivot(Quaternion.AngleAxis(-legAngle * 0.62f, wRight), shoulderPivotR, upperArmR, forearmR, handR, fingerR, thumbR);
            }
        }
        else
        {
            // Eerie idle breathing & head tilt
            float breath = Mathf.Sin(qIdle * 2.1f) * 1.8f;
            if (chest != null)
            {
                RotateGroupAroundPivot(Quaternion.AngleAxis(breath, wRight), chest.position, chest, neck, head);
            }

            if (isJumpscareTwitching && head != null)
            {
                Vector3 neckPivot = neck != null ? neck.position : head.position;
                float twitchRoll = Mathf.Sin(Time.unscaledTime * 55f) * 14f;
                float twitchPitch = -8f + Mathf.Cos(Time.unscaledTime * 42f) * 6f;
                RotateGroupAroundPivot(Quaternion.AngleAxis(twitchRoll, wFwd) * Quaternion.AngleAxis(twitchPitch, wRight), neckPivot, neck, head);
            }
            else if (isJunkSpeaking && head != null)
            {
                Vector3 neckPivot = neck != null ? neck.position : head.position;
                float nod = Mathf.Sin(qIdle * 6.5f) * 3.2f;
                RotateGroupAroundPivot(Quaternion.AngleAxis(nod, wRight), neckPivot, neck, head);
            }
        }

        if (lookAtPlayerHead && head != null)
        {
            Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
            if (cam != null)
            {
                Vector3 neckPivot = neck != null ? neck.position : head.position;
                Vector3 toCam = cam.transform.position - head.position;
                Vector3 flatToCam = new Vector3(toCam.x, 0f, toCam.z);
                if (flatToCam.sqrMagnitude > 0.01f)
                {
                    float yaw = Mathf.Clamp(Vector3.SignedAngle(wFwd, flatToCam.normalized, Vector3.up), -52f, 52f);
                    RotateGroupAroundPivot(Quaternion.AngleAxis(yaw, Vector3.up), neckPivot, neck, head);
                }
            }
        }
    }

    private static void RotateGroupAroundPivot(Quaternion worldRot, Vector3 pivotWorld, params Transform[] parts)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            Transform t = parts[i];
            if (t == null) continue;

            bool hasAncestorInGroup = false;
            for (int j = 0; j < parts.Length; j++)
            {
                if (i == j || parts[j] == null) continue;
                if (t.IsChildOf(parts[j]))
                {
                    hasAncestorInGroup = true;
                    break;
                }
            }
            if (hasAncestorInGroup) continue;

            t.position = pivotWorld + worldRot * (t.position - pivotWorld);
            t.rotation = worldRot * t.rotation;
        }
    }

    // =========================================================================
    // PROCEDURAL RETRO HORROR SFX (JUMPSCARE STING & FOOTSTEPS)
    // =========================================================================

    private void SetupProceduralAudio()
    {
        npcAudioSource = GetComponent<AudioSource>();
        if (npcAudioSource == null)
        {
            npcAudioSource = gameObject.AddComponent<AudioSource>();
        }
        npcAudioSource.playOnAwake = false;
        npcAudioSource.spatialBlend = 0.75f;
        npcAudioSource.minDistance = 1.5f;
        npcAudioSource.maxDistance = 16f;

        int sr = 44100;

        // 1. Footstep thud
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
        footstepClip = AudioClip.Create("JunkFootstep", stepSamples, 1, sr, false);
        footstepClip.SetData(stepBuf, 0);

        // 2. Low-quality PS1 horror startle jumpscare sting (sharp dissonant brass/string stab + sub-bass thump)
        int scareSamples = (int)(sr * 0.58f);
        float[] scareBuf = new float[scareSamples];
        for (int i = 0; i < scareSamples; i++)
        {
            float t = (float)i / sr;
            float attack = Mathf.Clamp01(t / 0.008f);
            float decay = Mathf.Exp(-t * 6.2f);
            float env = attack * decay;

            // Dissonant tritone + minor second cluster + sub impact
            float sub = Mathf.Sin(2f * Mathf.PI * (115f * Mathf.Exp(-t * 8f) + 52f) * t) * 0.55f;
            float chord =
                Mathf.Sin(2f * Mathf.PI * 311.1f * t) * 0.28f +
                Mathf.Sin(2f * Mathf.PI * 329.6f * t) * 0.28f +
                Mathf.Sin(2f * Mathf.PI * 440.0f * t) * 0.25f +
                Mathf.Sin(2f * Mathf.PI * 622.3f * t) * 0.22f;
            float crunch = (Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * 0.35f;

            // PS1 8-bit quantization grit
            float raw = Mathf.Clamp((sub + chord + crunch) * env, -1f, 1f);
            scareBuf[i] = Mathf.Round(raw * 32f) / 32f;
        }
        jumpscareStingClip = AudioClip.Create("LowQualityJumpscareSting", scareSamples, 1, sr, false);
        jumpscareStingClip.SetData(scareBuf, 0);
    }

    private void PlayOneShotClip(AudioClip clip, float volume, float pitch)
    {
        if (npcAudioSource == null || clip == null) return;
        npcAudioSource.pitch = pitch;
        npcAudioSource.PlayOneShot(clip, volume);
    }
}


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls "Billy" (BillyNPC): the night-shift coworker handing over the shift at the start of the game.
/// Supports Mixamo (Character_Male_01 / mixamorig:*), Unity Humanoid Avatar mapping, and Blender/PSX rigs.
/// Handles:
/// - Standing idle at his user-placed scene position with "[ BILLY ]" world indicator
/// - "E to Interact" close-range interaction
/// - Full shift-handover conversation with exact cooldowns
/// - Walking toward SpawnerNPC after the dialogue ends and disappearing upon arrival
/// </summary>
public class CoworkerNpcBilly : MonoBehaviour, IInteractable
{
    public static CoworkerNpcBilly Instance { get; private set; }

    public enum NpcState
    {
        StandingIdle,
        WaitingForPlayerTalk,
        TalkingWithPlayer,
        WalkingToSpawner,
        DepartedHidden
    }

    [Header("State & Settings")]
    public NpcState currentState = NpcState.StandingIdle;
    public float walkSpeed = 1.32f;
    public float turnSpeed = 5.5f;
    public float ps1AnimFps = 12f;

    public bool IsNpcActiveInWorld => isNpcVisible && gameObject.activeInHierarchy;

    public bool ShouldShowBillyIndicator =>
        !NightShiftStoryController.IsPlayerInHouseScene() &&
        string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, "GasScene2", System.StringComparison.OrdinalIgnoreCase) &&
        NightShiftStoryController.IsTalkToBillyTaskActive &&
        !NightShiftStoryController.HasTalkedToBilly &&
        currentState == NpcState.WaitingForPlayerTalk &&
        IsNpcActiveInWorld;

    public Vector3 IndicatorWorldPosition
    {
        get
        {
            if (head != null)
            {
                return head.position + Vector3.up * 0.36f;
            }
            return transform.position + Vector3.up * 1.95f;
        }
    }

    // Scene references
    private Transform spawnerNpcTransform;
    private Vector3 spawnerTargetPosition = new Vector3(-30.30f, -0.04f, -13.20f);
    private AutomaticSlidingDoors slidingDoors;
    private Transform shelf1Transform;

    // Renderers & collider on Billy
    private Renderer[] npcRenderers;
    private CapsuleCollider npcCapsule;
    private bool isNpcVisible = true;

    // Rig bones & bind pose
    private Transform pelvis, spine, chest, neck, head;
    private Transform shoulderL, upperArmL, forearmL, handL;
    private Transform shoulderR, upperArmR, forearmR, handR;
    private Transform thighL, shinL, footL, footLEnd;
    private Transform thighR, shinR, footR, footREnd;

    private readonly Dictionary<Transform, Quaternion> bindLocalRots = new Dictionary<Transform, Quaternion>();
    private Vector3 pelvisBindLocalPos;
    private Vector3 modelForwardLocal = Vector3.forward;
    private Vector3 modelRightLocal = Vector3.right;
    private float modelYawCorrectionDeg = 0f;
    private float footLSoleOffset = 0.09f;
    private float footLEndSoleOffset = 0.02f;
    private float footRSoleOffset = 0.09f;
    private float footREndSoleOffset = 0.02f;
    private bool rigInitialized = false;

    // Procedural animation runtime state
    private bool isCurrentlyWalking = false;
    private bool isBillySpeaking = false;
    private bool lookAtPlayerHead = false;
    private float walkCycleTimer = 0f;
    private float idleTimer = 0f;
    private float footstepTimer = 0f;

    // Audio
    private AudioSource npcAudioSource;
    private AudioClip footstepClip;

    // Sequence synchronization flags
    private bool talkTriggered = false;
    public bool HasPlayerTriggeredTalk => talkTriggered;

    public static CoworkerNpcBilly EnsureAttachedToSceneNpc()
    {
        if (Instance != null) return Instance;

        CoworkerNpcBilly existing = Object.FindAnyObjectByType<CoworkerNpcBilly>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        GameObject npcGo = null;

        // Pass 1: Exact name match ("BillyNPC", "Billy_NPC", "Billy NPC", "Billy")
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "BillyNPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Billy_NPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Billy NPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "Billy", System.StringComparison.OrdinalIgnoreCase))
            {
                npcGo = ResolveCharacterRoot(t).gameObject;
                break;
            }
        }

        // Pass 2: Substring match containing "Billy"
        if (npcGo == null)
        {
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t == null) continue;
                if (t.name.IndexOf("Billy", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    npcGo = ResolveCharacterRoot(t).gameObject;
                    break;
                }
            }
        }

        if (npcGo != null)
        {
            npcGo.SetActive(true);
            Instance = npcGo.AddComponent<CoworkerNpcBilly>();
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
                cur.parent.name.IndexOf("Billy", System.StringComparison.OrdinalIgnoreCase) >= 0)
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

        EnsureNpcCollider();
        CacheRigAndBindPose(anim);

        // Disable Animator so our procedural PS1 bone animation in LateUpdate has full uncontested control
        if (anim != null)
        {
            anim.enabled = false;
        }

        SetupProceduralAudio();
        ResolveSceneTargets();

        // Billy starts visible and standing at his user-placed position in the scene
        SetNpcVisible(true);
        SnapToGroundImmediate();
        currentState = NpcState.StandingIdle;
    }

    private void EnsureNpcCollider()
    {
        npcCapsule = GetComponent<CapsuleCollider>();
        if (npcCapsule == null)
        {
            npcCapsule = gameObject.AddComponent<CapsuleCollider>();
        }
        float sy = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        float sx = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
        npcCapsule.height = 1.78f / sy;
        npcCapsule.radius = 0.34f / sx;
        npcCapsule.center = new Vector3(0f, (0.89f / sy), 0f);
        npcCapsule.isTrigger = false;
    }

    private void ResolveSceneTargets()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);

        // 1. Find SpawnerNPC placed by the user (exact match first, then substring fallback)
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

            // Hide any marker mesh on SpawnerNPC and make any marker collider a non-blocking trigger
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
        else if (CustomerNpcSteve.Instance != null)
        {
            spawnerTargetPosition = new Vector3(-30.30f, -0.04f, -13.20f);
        }

        // 2. Sliding Entrance Doors & Shelf1 (in case Billy walks from near/inside store)
        slidingDoors = Object.FindAnyObjectByType<AutomaticSlidingDoors>();

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            if (string.Equals(t.name.Trim(), "Shelf1", System.StringComparison.OrdinalIgnoreCase))
            {
                if (shelf1Transform == null || t.position.z < -2.0f)
                {
                    shelf1Transform = t;
                }
            }
        }
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

    public void BeginWaitingForPlayerTalk()
    {
        ResolveSceneTargets();
        SetNpcVisible(true);
        SnapToGroundImmediate();
        talkTriggered = false;
        currentState = NpcState.WaitingForPlayerTalk;
    }

    // =========================================================================
    // HANDOVER CONVERSATION & WALK TO SPAWNERNPC ROUTINES
    // =========================================================================

    public IEnumerator WaitAndRunHandoverDialogueRoutine()
    {
        BeginWaitingForPlayerTalk();

        while (!talkTriggered)
        {
            yield return null;
        }

        yield return StartCoroutine(RunHandoverDialogueRoutine());
    }

    public IEnumerator RunHandoverDialogueRoutine()
    {
        currentState = NpcState.TalkingWithPlayer;
        lookAtPlayerHead = true;

        // Smoothly turn Billy towards the player during the 0.5-second cooldown before his first line
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        Vector3 playerLookTarget = cam != null ? cam.transform.position : (transform.position + CharacterForwardWorld);

        // [0.5-second cooldown]
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(playerLookTarget, 0.5f));

        NightShiftStoryController story = NightShiftStoryController.Instance;
        if (story != null)
        {
            // Billy: I guess it's your turn for the night again...
            isBillySpeaking = true;
            yield return StartCoroutine(story.PlayCustomDialogueRoutine("Billy: I guess it's your turn for the night again..."));
            isBillySpeaking = false;

            // [2-second cooldown]
            yield return new WaitForSeconds(2.0f);

            // You: Yeah. Looks like it.
            yield return StartCoroutine(story.PlayCustomDialogueRoutine("You: Yeah. Looks like it."));

            // [1-second cooldown]
            yield return new WaitForSeconds(1.0f);

            // Billy: Getting used to the night shift yet?
            isBillySpeaking = true;
            yield return StartCoroutine(story.PlayCustomDialogueRoutine("Billy: Getting used to the night shift yet?"));
            isBillySpeaking = false;

            // [2-second cooldown]
            yield return new WaitForSeconds(2.0f);

            // You: Not really. It's still pretty quiet around here.
            yield return StartCoroutine(story.PlayCustomDialogueRoutine("You: Not really. It's still pretty quiet around here."));

            // [2-second cooldown]
            yield return new WaitForSeconds(2.0f);

            // Billy: You'll get used to it. Just takes time.
            isBillySpeaking = true;
            yield return StartCoroutine(story.PlayCustomDialogueRoutine("Billy: You'll get used to it. Just takes time."));
            isBillySpeaking = false;

            // [1-second cooldown]
            yield return new WaitForSeconds(1.0f);

            // Billy: I better get going. Good luck, buddy.
            isBillySpeaking = true;
            yield return StartCoroutine(story.PlayCustomDialogueRoutine("Billy: I better get going. Good luck, buddy."));
            isBillySpeaking = false;

            // [1-second cooldown]
            yield return new WaitForSeconds(1.0f);

            // You: Thanks.
            yield return StartCoroutine(story.PlayCustomDialogueRoutine("You: Thanks."));
        }

        isBillySpeaking = false;
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
        isBillySpeaking = false;
        currentState = NpcState.WalkingToSpawner;

        Vector3 targetPos = spawnerNpcTransform != null ? spawnerNpcTransform.position : spawnerTargetPosition;
        List<Vector3> path = BuildObstacleFreePathToSpawner(transform.position, targetPos);

        yield return StartCoroutine(FollowWaypointsRoutine(path, checkEntranceDoorway: true));

        // Disappear upon reaching SpawnerNPC
        SetNpcVisible(false);
        currentState = NpcState.DepartedHidden;
    }

    // =========================================================================
    // IINTERACTABLE IMPLEMENTATION ("E to Interact")
    // =========================================================================

    public void Interact()
    {
        if (NightShiftStoryController.IsDialogueActive) return;
        if (currentState != NpcState.WaitingForPlayerTalk) return;
        if (!IsPlayerWithinDistance(2.35f)) return;

        talkTriggered = true;
    }

    public string GetPromptText()
    {
        if (NightShiftStoryController.IsDialogueActive) return string.Empty;
        if (currentState != NpcState.WaitingForPlayerTalk) return string.Empty;
        if (!IsPlayerWithinDistance(2.35f)) return string.Empty;

        return "E to Interact";
    }

    public bool IsPlayerWithinDistance(float maxDist)
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        Vector3 closest = npcCapsule != null
            ? npcCapsule.ClosestPoint(cam.transform.position)
            : transform.position + Vector3.up * 1.2f;

        return Vector3.Distance(cam.transform.position, closest) <= maxDist;
    }

    // =========================================================================
    // OBSTACLE-FREE NAVIGATION & GROUND SNAPPING TO SPAWNERNPC
    // =========================================================================

    private List<Vector3> BuildObstacleFreePathToSpawner(Vector3 start, Vector3 targetSpawner)
    {
        List<Vector3> pts = new List<Vector3>();

        float doorZ = slidingDoors != null ? slidingDoors.transform.position.z : -1.60f;
        float doorX = slidingDoors != null ? slidingDoors.transform.position.x : -1.77f;

        bool startsInsideStore = (start.x >= -1.70f && start.x <= 5.20f &&
                                  start.z >= -9.00f && start.z <= 6.85f);
        bool targetOutsideStore = (targetSpawner.x < -1.80f || targetSpawner.z < -9.05f || targetSpawner.z > 6.90f);

        Vector3 cur = start;

        // 1. If Billy was placed inside the gas station store and SpawnerNPC is outside, exit cleanly via the sliding doors first
        if (startsInsideStore && targetOutsideStore)
        {
            if (cur.x > -0.20f && cur.z < -3.60f)
            {
                pts.Add(new Vector3(0.05f, 0.17f, -5.18f));
                pts.Add(new Vector3(-0.55f, 0.17f, -4.65f));
            }
            pts.Add(new Vector3(-0.72f, 0.17f, Mathf.Clamp(cur.z, -3.50f, doorZ)));
            pts.Add(new Vector3(-0.72f, 0.17f, doorZ));
            pts.Add(new Vector3(doorX - 1.35f, 0.15f, doorZ));
            cur = new Vector3(doorX - 1.35f, 0.15f, doorZ);
        }

        // 2. If walking from near the store entrance / forecourt out toward the road SpawnerNPC (X < -14),
        // route cleanly around the central fuel pump islands along the south-west walkway
        if (targetSpawner.x < -14.0f && cur.x > -12.0f)
        {
            if (cur.x > -4.6f && cur.z > -5.2f)
            {
                pts.Add(new Vector3(-4.80f, 0.14f, -4.80f));
            }
            if (cur.x > -5.5f && cur.z > -9.2f)
            {
                pts.Add(new Vector3(-5.60f, 0.14f, -9.80f));
            }
            if (cur.x > -11.0f && targetSpawner.x < -13.0f)
            {
                pts.Add(new Vector3(-11.50f, 0.08f, -12.00f));
            }
            if (cur.x > -24.0f && targetSpawner.x < -25.0f)
            {
                pts.Add(new Vector3(-25.50f, 0.02f, Mathf.Lerp(-12.20f, targetSpawner.z, 0.65f)));
            }
        }

        // 3. Final destination: exact SpawnerNPC position
        pts.Add(targetSpawner);
        return pts;
    }

    private IEnumerator FollowWaypointsRoutine(List<Vector3> waypoints, bool checkEntranceDoorway)
    {
        isCurrentlyWalking = true;

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 target = waypoints[i];
            bool isFinalWaypoint = (i == waypoints.Count - 1);
            float arriveThreshold = isFinalWaypoint ? 0.32f : 0.18f;
            float waypointElapsed = 0f;

            while (true)
            {
                waypointElapsed += Time.deltaTime;
                if (waypointElapsed > 22f)
                {
                    break;
                }

                // Refresh target position if final waypoint is the live SpawnerNPC transform
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

                // If crossing the sliding entrance doors, open them smoothly
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

                // Polite yield if player stands directly in Billy's path
                if (IsPlayerBlockingImmediatePath(toTarget.normalized))
                {
                    isCurrentlyWalking = false;
                    yield return null;
                    isCurrentlyWalking = true;
                    continue;
                }

                Vector3 moveDir = ComputeObstacleFreeMoveDirection(cur, toTarget.normalized);
                RotateTowardsWorldDir(moveDir, Time.deltaTime * turnSpeed);

                float step = walkSpeed * Time.deltaTime;
                if (step > dist) step = dist;

                Vector3 nextPos = cur + moveDir * step;
                nextPos = EnforceStoreFurnitureClearance(nextPos);
                nextPos.y = SampleGroundHeight(nextPos, cur.y);
                transform.position = nextPos;

                AdvanceWalkAudioAndTimer(1.0f);
                yield return null;
            }
        }

        isCurrentlyWalking = false;
    }

    private IEnumerator TurnSmoothlyTowardsRoutine(Vector3 worldLookTarget, float duration)
    {
        isCurrentlyWalking = false;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector3 cur = transform.position;
            cur.y = SampleGroundHeight(cur, cur.y);
            transform.position = cur;

            Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
            if (cam != null)
            {
                worldLookTarget = cam.transform.position;
            }

            Vector3 dir = worldLookTarget - cur;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                RotateTowardsWorldDir(dir.normalized, Time.deltaTime * (turnSpeed * 1.6f));
            }
            yield return null;
        }
        SnapToGroundImmediate();
    }

    private bool IsPlayerBlockingImmediatePath(Vector3 moveDir)
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) return false;

        Vector3 pPos = cam.transform.position;
        Vector3 diff = new Vector3(pPos.x - transform.position.x, 0f, pPos.z - transform.position.z);
        float d = diff.magnitude;
        if (d < 0.78f && d > 0.01f)
        {
            return Vector3.Dot(diff.normalized, moveDir) > 0.48f;
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
                if (spawnerNpcTransform != null && col.transform.IsChildOf(spawnerNpcTransform)) continue;

                // Ignore floor / road / flat ground slabs
                if (col.bounds.max.y <= currentPos.y + 0.24f) continue;

                // Ignore overhead lintels above head
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

        // Short forward feeler raycast to steer around pillars, walls, or fuel pumps smoothly
        if (Physics.SphereCast(checkCenter, 0.26f, desiredDir, out RaycastHit forwardHit, 0.75f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (forwardHit.collider != null &&
                !forwardHit.collider.transform.IsChildOf(transform) &&
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

    private Vector3 EnforceStoreFurnitureClearance(Vector3 pos)
    {
        // Only apply store interior shelf clearance when inside store bounds
        if (pos.x < -1.80f || pos.x > 5.20f || pos.z < -9.00f || pos.z > 6.85f)
        {
            return pos;
        }

        // 1. Shelf2
        Bounds b2 = new Bounds();
        b2.SetMinMax(new Vector3(-0.16f, 0.17f, -0.19f), new Vector3(2.96f, 1.58f, 0.44f));
        pos = PushOutOfBox2D(pos, b2, 0.34f);

        // 2. Shelf3
        Bounds b3 = new Bounds();
        b3.SetMinMax(new Vector3(-0.16f, 0.17f, -2.15f), new Vector3(2.96f, 1.58f, -1.51f));
        pos = PushOutOfBox2D(pos, b3, 0.34f);

        // 3. Shelf1
        Bounds b1 = new Bounds();
        b1.SetMinMax(new Vector3(-0.07f, 0.17f, -4.43f), new Vector3(2.75f, 1.58f, -3.80f));
        if (shelf1Transform != null)
        {
            Renderer[] rends = shelf1Transform.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                b1 = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b1.Encapsulate(rends[i].bounds);
            }
        }
        pos = PushOutOfBox2D(pos, b1, 0.34f);

        // 4. Left Window Wire Snack Racks (model/group_0 & model/group_1)
        Bounds leftSnackRacks = new Bounds();
        leftSnackRacks.SetMinMax(new Vector3(-1.45f, 0.0f, -6.32f), new Vector3(-0.98f, 1.55f, -4.99f));
        pos = PushOutOfBox2D(pos, leftSnackRacks, 0.32f);

        return pos;
    }

    private static Vector3 PushOutOfBox2D(Vector3 pos, Bounds box, float margin)
    {
        float minX = box.min.x - margin;
        float maxX = box.max.x + margin;
        float minZ = box.min.z - margin;
        float maxZ = box.max.z + margin;

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
        Vector3 origin = new Vector3(pos.x, currentY + 0.85f, pos.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 3.5f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return currentY;

        float highestFloorY = float.MinValue;
        float maxStepUpY = currentY + 0.48f;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.normal.y < 0.65f) continue;
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
        Vector3 origin = transform.position + Vector3.up * 0.85f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 3.5f, ~0, QueryTriggerInteraction.Ignore);
        float highestFloorY = float.MinValue;
        float maxStepUpY = transform.position.y + 0.55f;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
            if (h.normal.y < 0.65f) continue;
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
            PlayOneShotClip(footstepClip, 0.25f, Random.Range(0.88f, 1.06f));
        }
    }

    // =========================================================================
    // PROCEDURAL PS1 BONE RIGGING & ANIMATION (SUPPORTS MIXAMO, HUMANOID & PSX)
    // =========================================================================

    private Vector3 CharacterRightWorld => transform.TransformDirection(modelRightLocal).normalized;
    private Vector3 CharacterForwardWorld => transform.TransformDirection(modelForwardLocal).normalized;

    private void CacheRigAndBindPose(Animator anim)
    {
        Quaternion savedRot = transform.rotation;
        transform.rotation = Quaternion.identity;

        // 1. If Animator has a valid Humanoid Avatar, query Unity's exact bone mapping first
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

        // 2. Cache bind rotations and match bone names (supporting Mixamo mixamorig:*, Blender, and PSX rigs)
        Transform[] allBones = GetComponentsInChildren<Transform>(true);
        bindLocalRots.Clear();

        for (int i = 0; i < allBones.Length; i++)
        {
            Transform b = allBones[i];
            if (b == transform) continue;
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
            else if (chest == null && (n == "Spine1" || n == "Spine2" || n == "chest" || n == "Chest")) chest = b;
            else if (neck == null && (n == "Neck" || n == "neck")) neck = b;
            else if (head == null && (n == "Head" || n == "head")) head = b;
            else if (shoulderL == null && (n == "LeftShoulder" || n == "shoulder.L" || n == "shoulder_left")) shoulderL = b;
            else if (upperArmL == null && (n == "LeftArm" || n == "upper_arm.L" || n == "upper_arm_left")) upperArmL = b;
            else if (forearmL == null && (n == "LeftForeArm" || n == "forearm.L" || n == "forearm_left")) forearmL = b;
            else if (handL == null && (n == "LeftHand" || n == "hand.L" || n == "hand_left")) handL = b;
            else if (shoulderR == null && (n == "RightShoulder" || n == "shoulder.R" || n == "shoulder_right")) shoulderR = b;
            else if (upperArmR == null && (n == "RightArm" || n == "upper_arm.R" || n == "upper_arm_right")) upperArmR = b;
            else if (forearmR == null && (n == "RightForeArm" || n == "forearm.R" || n == "forearm_right")) forearmR = b;
            else if (handR == null && (n == "RightHand" || n == "hand.R" || n == "hand_right")) handR = b;
            else if (thighL == null && (n == "LeftUpLeg" || n == "thigh.L" || n == "thigh_left")) thighL = b;
            else if (shinL == null && (n == "LeftLeg" || n == "shin.L" || n == "shin_left")) shinL = b;
            else if (footL == null && (n == "LeftFoot" || n == "foot.L" || n == "foot_left")) footL = b;
            else if (footLEnd == null && (n == "LeftToeBase" || n == "LeftToe_End" || n == "toe.L" || n == "foot_left_end")) footLEnd = b;
            else if (thighR == null && (n == "RightUpLeg" || n == "thigh.R" || n == "thigh_right")) thighR = b;
            else if (shinR == null && (n == "RightLeg" || n == "shin.R" || n == "shin_right")) shinR = b;
            else if (footR == null && (n == "RightFoot" || n == "foot.R" || n == "foot_right")) footR = b;
            else if (footREnd == null && (n == "RightToeBase" || n == "RightToe_End" || n == "toe.R" || n == "foot_right_end")) footREnd = b;
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
    }

    private void ApplyProceduralPs1Animation()
    {
        // 1. Restore bind pose first
        foreach (var kvp in bindLocalRots)
        {
            if (kvp.Key != null) kvp.Key.localRotation = kvp.Value;
        }
        if (pelvis != null) pelvis.localPosition = pelvisBindLocalPos;

        Vector3 wRight = CharacterRightWorld;
        Vector3 wFwd = CharacterForwardWorld;

        // 2. Relax T-pose/A-pose arms down naturally beside Billy's torso
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
            float legAngle = stride * 25f;

            if (thighL != null) RotateBoneWorld(thighL, wRight, -legAngle);
            if (thighR != null) RotateBoneWorld(thighR, wRight, legAngle);

            float kneeBendL = Mathf.Max(0f, swingPass) * 32f;
            float kneeBendR = Mathf.Max(0f, -swingPass) * 32f;
            if (shinL != null) RotateBoneWorld(shinL, wRight, kneeBendL);
            if (shinR != null) RotateBoneWorld(shinR, wRight, kneeBendR);

            if (footL != null) RotateBoneWorld(footL, wRight, -Mathf.Max(0f, swingPass) * 10f);
            if (footR != null) RotateBoneWorld(footR, wRight, -Mathf.Max(0f, -swingPass) * 10f);

            if (spine != null)
            {
                RotateBoneWorld(spine, wRight, -3.5f);
                RotateBoneWorld(spine, Vector3.up, stride * 4.0f);
            }

            if (upperArmL != null) RotateBoneWorld(upperArmL, wRight, legAngle * 0.65f);
            if (forearmL != null) RotateBoneWorld(forearmL, wRight, -Mathf.Max(0f, -legAngle) * 0.45f);

            if (upperArmR != null) RotateBoneWorld(upperArmR, wRight, -legAngle * 0.65f);
            if (forearmR != null) RotateBoneWorld(forearmR, wRight, -Mathf.Max(0f, legAngle) * 0.45f);
        }
        else
        {
            // Subtle standing breathing & tired night-shift posture
            float breath = Mathf.Sin(qIdle * 1.85f) * 1.9f;
            if (chest != null) RotateBoneWorld(chest, wRight, breath);
            if (spine != null) RotateBoneWorld(spine, Vector3.up, Mathf.Sin(qIdle * 0.85f) * 1.4f);

            // Subtle conversational gesture & nod when Billy is speaking
            if (isBillySpeaking)
            {
                float talkPulse = Mathf.Sin(qIdle * 6.2f);
                if (head != null) RotateBoneWorld(head, wRight, talkPulse * 2.6f);
                if (forearmR != null) RotateBoneWorld(forearmR, wRight, -12f + talkPulse * 5.5f);
            }
        }

        // Head look-at player during handover conversation
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
            float env = Mathf.Exp(-t * 58f);
            float low = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.65f;
            float grit = (Random.value * 2f - 1f) * 0.35f;
            stepBuf[i] = (low + grit) * env * 0.6f;
        }
        footstepClip = AudioClip.Create("BillyFootstep", stepSamples, 1, sr, false);
        footstepClip.SetData(stepBuf, 0);
    }

    private void PlayOneShotClip(AudioClip clip, float volume, float pitch)
    {
        if (npcAudioSource == null || clip == null) return;
        npcAudioSource.pitch = pitch;
        npcAudioSource.PlayOneShot(clip, volume);
    }
}

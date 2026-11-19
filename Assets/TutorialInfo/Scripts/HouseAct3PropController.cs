using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all Act 3 (HouseScene Return Home) 3D props, weather (heavy realistic low-horror rain & thunder),
/// monster_low / monster_lowqual normalization, visibility & standing animation, bathroom/kitchen sink face-washing,
/// living room TV-stand candle (left hand), bedroom nightstand phone (right hand), broken left living room window,
/// and the 3D dead pigeon model.
/// </summary>
public class HouseAct3PropController : MonoBehaviour
{
    public static HouseAct3PropController Instance { get; private set; }

    [Header("Monster Reference (monster_low / monster_lowqual)")]
    public GameObject monsterInstance;
    public Transform monsterHeadLookTarget;
    public float targetMonsterHeight = 1.74f;

    [Header("Act 3 Props")]
    public CabinCandleInteractable candleInteractable;
    public CabinPhoneInteractable phoneInteractable;
    public CabinBathroomSinkFaceWash bathroomSinkFaceWash;
    public Transform leftLivingRoomWindowTransform;
    public Transform frontDoubleDoorTransform;
    public GameObject brokenWindowVisualRoot;
    public GameObject deadPigeonRoot;

    [Header("Weather & Audio")]
    public Act3RainStormController rainStormController;
    private AudioSource doorAudioSource;
    private AudioSource windowAudioSource;
    private AudioSource powerOutageAudioSource;

    private AudioClip doorKnockClip;
    private AudioClip powerOutageClip;
    private AudioClip glassBreakClip;
    private AudioClip monsterGlitchClip;
    private Vector3 monsterSavedBasePos;
    private Quaternion monsterSavedBaseRot;
    private bool hasSavedMonsterBasePose = false;

    private void Awake()
    {
        Instance = this;
        SetupAudioSources();
        GenerateStoryAudioClips();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static HouseAct3PropController EnsureExists(Transform buildingRoot)
    {
        if (Instance != null) return Instance;

        HouseAct3PropController existing = Object.FindAnyObjectByType<HouseAct3PropController>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("HouseAct3_PropController");
        Instance = go.AddComponent<HouseAct3PropController>();
        Instance.InitializeSceneReferences(buildingRoot);
        return Instance;
    }

    public static void ResetStaticAct3State()
    {
        CabinCandleInteractable.HeldCandle = null;
        CabinPhoneInteractable.HeldPhone = null;
    }

    /// <summary>
    /// Ensures monster_low / monster_lowqual is normalized to ~1.74m height, has a standing animation,
    /// and is 100% invisible unless explicitly shown during the Act 3 broken-window sequence.
    /// Safe to call in both Act 1 and Act 3.
    /// </summary>
    public void InitializeSceneReferences(Transform buildingRoot)
    {
        FindKeyCabinTransforms(buildingRoot);
        EnsureMonsterNormalizedAndHidden(buildingRoot);
    }

    public void SetupAct3PropsAndWeather(Transform buildingRoot)
    {
        InitializeSceneReferences(buildingRoot);

        // 1. Heavy realistic low-horror rain with thunder
        rainStormController = Act3RainStormController.EnsureActive(buildingRoot);

        // 2. Bathroom sink face-wash interactable
        EnsureBathroomSinkFaceWash(buildingRoot);

        // 3. Candle laying down between grp_doorsL and grp_doorsR of PFB_TVStand
        EnsureCandleInLivingRoom(buildingRoot);

        // 4. Phone already existing on PFB_BedsideTable in the bedroom
        EnsurePhoneInBedroom(buildingRoot);
    }

    private void FindKeyCabinTransforms(Transform buildingRoot)
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.gameObject.scene != gameObject.scene) continue;

            if (frontDoubleDoorTransform == null &&
                string.Equals(t.name, "PFB_DoorDouble", System.StringComparison.OrdinalIgnoreCase))
            {
                frontDoubleDoorTransform = t;
            }

            // Left living room window is PFB_Curtain (local ~(1.309, 1.902, 5.244)), whereas right window is PFB_Curtain (1)
            if (leftLivingRoomWindowTransform == null &&
                string.Equals(t.name, "PFB_Curtain", System.StringComparison.OrdinalIgnoreCase))
            {
                leftLivingRoomWindowTransform = t;
            }
        }
    }

    public void EnsureMonsterNormalizedAndHidden(Transform buildingRoot)
    {
        if (monsterInstance == null)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.gameObject.scene != gameObject.scene) continue;
                string n = t.name;
                if (n.IndexOf("monster_low", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("psx_horror_creature", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Pick the root-most matching transform of the creature
                    Transform rootMatch = t;
                    while (rootMatch.parent != null &&
                           (rootMatch.parent.name.IndexOf("monster_low", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            rootMatch.parent.name.IndexOf("psx_horror_creature", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        rootMatch = rootMatch.parent;
                    }
                    monsterInstance = rootMatch.gameObject;
                    break;
                }
            }
        }

        // Fallback if scene reloaded without unsaved hierarchy addition: instantiate from Resources
        if (monsterInstance == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Act3_Monster_LowQual");
            if (prefab != null)
            {
                monsterInstance = Instantiate(prefab);
                monsterInstance.name = "monster_lowqual";
            }
        }

        if (monsterInstance == null) return;

        monsterInstance.SetActive(true);

        // Temporarily enable renderers for accurate bounds calculation, remove bright white specular/glow, then hide
        Renderer[] renderers = monsterInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
                if (renderers[i] is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                }
            }
        }
        PrepareMonsterDarkMaterials(renderers);

        // Normalize height to targetMonsterHeight (~1.80m, human/NPC height)
        Bounds combinedBounds = CalculateCombinedBounds(renderers, monsterInstance.transform.position);
        if (combinedBounds.size.y > 0.05f)
        {
            float scaleRatio = targetMonsterHeight / combinedBounds.size.y;
            if (Mathf.Abs(scaleRatio - 1f) > 0.02f)
            {
                monsterInstance.transform.localScale *= scaleRatio;
            }
        }

        // Attach standing animation controller
        Act3MonsterStandingAnimator standingAnim = monsterInstance.GetComponent<Act3MonsterStandingAnimator>();
        if (standingAnim == null)
        {
            standingAnim = monsterInstance.AddComponent<Act3MonsterStandingAnimator>();
        }
        standingAnim.InitializeBones();

        // Position monster outside the left living room window so it is clearly visible standing outside when window breaks
        PositionMonsterOutsideBrokenWindow(buildingRoot, renderers);

        // Find or create head look target at upper chest/head height
        if (monsterHeadLookTarget == null)
        {
            Transform existingTarget = monsterInstance.transform.Find("MonsterLookTarget");
            if (existingTarget == null)
            {
                GameObject targetGo = new GameObject("MonsterLookTarget");
                targetGo.transform.SetParent(monsterInstance.transform, false);
                existingTarget = targetGo.transform;
            }
            monsterHeadLookTarget = existingTarget;
        }
        Bounds finalBounds = CalculateCombinedBounds(renderers, monsterInstance.transform.position);
        monsterHeadLookTarget.position = new Vector3(
            finalBounds.center.x,
            finalBounds.min.y + targetMonsterHeight * 0.80f,
            finalBounds.center.z);

        // Keep 100% invisible until the window breaks in Act 3
        SetMonsterVisible(false);
    }

    private void PositionMonsterOutsideBrokenWindow(Transform buildingRoot, Renderer[] renderers)
    {
        if (monsterInstance == null) return;

        Vector3 windowCenter;
        Vector3 outwardDir;
        float cabinFloorY;

        GetLeftLivingRoomWindowFrame(buildingRoot, out windowCenter, out outwardDir, out cabinFloorY);

        Vector3 offsetFromWin = monsterInstance.transform.position - windowCenter;
        float outwardDist = Vector3.Dot(offsetFromWin, outwardDir);
        Vector3 rightDir = Vector3.Cross(Vector3.up, outwardDir).normalized;
        float lateralDist = Mathf.Abs(Vector3.Dot(offsetFromWin, rightDir));

        Vector3 targetPos;
        if (outwardDist >= 4.0f && outwardDist <= 20.0f && lateralDist <= 2.40f)
        {
            // Keep user's far exterior placement outside the window (e.g. (-12.01, y, 322.87))
            targetPos = monsterInstance.transform.position;
        }
        else
        {
            // Place far outside the shattered living room window so it looms in the distant dark rain
            targetPos = windowCenter + outwardDir * 8.50f;
        }

        // psx_horror_creature.fbx was authored with its chest and face pointing along local -X (Vector3.left), NOT +Z.
        // Rotating LookRotation(lookDir) by Euler(0, +90, 0) turns local -X straight toward the broken window.
        Vector3 lookDir = windowCenter - targetPos;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            monsterInstance.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
        }

        // Plant feet solidly on exterior ground height at targetPos (Raycast down onto terrain/ground)
        float desiredFeetY = cabinFloorY;
        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(targetPos.x, cabinFloorY + 6.0f, targetPos.z),
            Vector3.down,
            16.0f,
            ~0,
            QueryTriggerInteraction.Ignore);
        float bestGroundY = float.MinValue;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hc = hits[i].collider;
            if (hc == null || hc.transform.IsChildOf(monsterInstance.transform)) continue;
            if (hits[i].point.y > bestGroundY && hits[i].point.y <= cabinFloorY + 2.5f)
            {
                bestGroundY = hits[i].point.y;
            }
        }
        if (bestGroundY > float.MinValue)
        {
            desiredFeetY = bestGroundY;
        }

        monsterInstance.transform.position = targetPos;
        Bounds b = CalculateCombinedBounds(renderers, targetPos);
        float deltaY = desiredFeetY - b.min.y;
        monsterInstance.transform.position = new Vector3(targetPos.x, monsterInstance.transform.position.y + deltaY, targetPos.z);
    }

    private static void PrepareMonsterDarkMaterials(Renderer[] renderers)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            Material[] mats = r.materials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null) continue;

                // Strip glossy white specular highlights, environment reflections, and emission so the monster never glows white
                mat.SetFloat("_BlendModePreserveSpecular", 0f);
                mat.SetFloat("_SpecularHighlights", 0f);
                mat.SetFloat("_EnvironmentReflections", 0f);
                mat.SetFloat("_Smoothness", 0.08f);
                mat.SetFloat("_Metallic", 0f);
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                mat.DisableKeyword("_EMISSION");

                Color mutedNightTint = new Color(0.62f, 0.66f, 0.72f, 1f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mutedNightTint);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", mutedNightTint);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
                if (mat.HasProperty("_SpecColor")) mat.SetColor("_SpecColor", Color.black);
            }
        }
    }

    public void SetMonsterVisible(bool visible)
    {
        if (monsterInstance == null) return;

        Renderer[] renderers = monsterInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }

        // Ensure no white point lights glow around the monster; lightning sky flash alone illuminates it
        Transform rimLightTr = monsterInstance.transform.Find("MonsterWindowRimLight");
        if (rimLightTr != null) rimLightTr.gameObject.SetActive(false);

        Transform backLightTr = monsterInstance.transform.Find("MonsterSilhouetteBackLight");
        if (backLightTr != null) backLightTr.gameObject.SetActive(false);
    }

    public void GetLeftLivingRoomWindowFrame(Transform buildingRoot, out Vector3 windowCenter, out Vector3 outwardDir, out float cabinFloorY)
    {
        cabinFloorY = buildingRoot != null ? buildingRoot.position.y + 0.112f : 11.71f;

        if (leftLivingRoomWindowTransform != null)
        {
            // PFB_Curtain is mounted on the inner face of the +Z living room wall at local (1.309, 1.902, 5.244)
            if (buildingRoot != null)
            {
                Vector3 localCurtain = buildingRoot.InverseTransformPoint(leftLivingRoomWindowTransform.position);
                windowCenter = buildingRoot.TransformPoint(new Vector3(localCurtain.x, 1.82f, localCurtain.z + 0.12f));
                outwardDir = buildingRoot.TransformDirection(Vector3.forward).normalized;
            }
            else
            {
                windowCenter = leftLivingRoomWindowTransform.position + new Vector3(0f, -0.08f, 0f);
                outwardDir = -leftLivingRoomWindowTransform.forward;
                outwardDir.y = 0f;
                if (outwardDir.sqrMagnitude < 0.001f) outwardDir = Vector3.forward;
                outwardDir.Normalize();
            }
            return;
        }

        if (buildingRoot != null)
        {
            windowCenter = buildingRoot.TransformPoint(new Vector3(1.309f, 1.82f, 5.36f));
            outwardDir = buildingRoot.TransformDirection(Vector3.forward).normalized;
        }
        else
        {
            windowCenter = new Vector3(-9.57f, 13.42f, 307.98f);
            outwardDir = Vector3.left;
        }
    }

    public Vector3 GetFrontDoorWorldPosition(Transform buildingRoot)
    {
        if (frontDoubleDoorTransform != null)
        {
            return frontDoubleDoorTransform.position;
        }
        if (buildingRoot != null)
        {
            return buildingRoot.TransformPoint(new Vector3(-1.861f, 1.112f, 5.344f));
        }
        return new Vector3(-9.56f, 12.71f, 311.15f);
    }

    public void PlayDoorKnockSound(Transform buildingRoot)
    {
        if (doorAudioSource == null || doorKnockClip == null) return;
        doorAudioSource.transform.position = GetFrontDoorWorldPosition(buildingRoot);
        doorAudioSource.pitch = Random.Range(0.95f, 1.03f);
        doorAudioSource.PlayOneShot(doorKnockClip, 1.0f);
    }

    public void PlayPowerOutageSound(Vector3 position)
    {
        if (powerOutageAudioSource == null || powerOutageClip == null) return;
        powerOutageAudioSource.transform.position = position;
        powerOutageAudioSource.PlayOneShot(powerOutageClip, 1.0f);
    }

    /// <summary>
    /// Breaks the left living room window with glass-shattering audio and spawns broken glass shards & dead pigeon on the floor,
    /// keeping monster_lowqual hidden outside until the player inspects the broken window.
    /// </summary>
    public void TriggerLivingRoomWindowBreak(Transform buildingRoot)
    {
        Vector3 windowCenter;
        Vector3 outwardDir;
        float cabinFloorY;
        GetLeftLivingRoomWindowFrame(buildingRoot, out windowCenter, out outwardDir, out cabinFloorY);

        // 1. Play loud glass-shatter sound at the window
        if (windowAudioSource != null && glassBreakClip != null)
        {
            windowAudioSource.transform.position = windowCenter;
            windowAudioSource.PlayOneShot(glassBreakClip, 1.0f);
        }

        // Trigger a simultaneous thunder rumble for dramatic punctuation
        if (rainStormController != null)
        {
            rainStormController.TriggerImmediateLightningAndThunder();
        }

        // 2. Build broken window shards + floor shards + 3D Dead Pigeon + "[E] Inspect" trigger on the window
        BuildBrokenWindowAndDeadPigeon(buildingRoot, windowCenter, outwardDir, cabinFloorY);

        // 3. Prepare monster_lowqual outside the window, kept hidden until player presses E to inspect the broken window
        EnsureMonsterNormalizedAndHidden(buildingRoot);
        SetMonsterVisible(false);
    }

    public void PlayMonsterGlitchSound(Vector3 worldPos)
    {
        if (windowAudioSource != null && monsterGlitchClip != null)
        {
            windowAudioSource.transform.position = worldPos;
            windowAudioSource.PlayOneShot(monsterGlitchClip, 0.92f);
        }
    }

    public void SetMonsterGlitchState(bool active, bool strobeVisible, float jitterRadius = 0.14f)
    {
        if (monsterInstance == null) return;

        if (!hasSavedMonsterBasePose)
        {
            monsterSavedBasePos = monsterInstance.transform.position;
            monsterSavedBaseRot = monsterInstance.transform.rotation;
            hasSavedMonsterBasePose = true;
        }

        if (active)
        {
            SetMonsterVisible(strobeVisible);
            Vector3 lateral = Random.insideUnitSphere * jitterRadius;
            lateral.y *= 0.25f;
            monsterInstance.transform.position = monsterSavedBasePos + lateral;
            monsterInstance.transform.rotation = monsterSavedBaseRot * Quaternion.Euler(0f, Random.Range(-7f, 7f), 0f);
        }
        else
        {
            monsterInstance.transform.position = monsterSavedBasePos;
            monsterInstance.transform.rotation = monsterSavedBaseRot;
            hasSavedMonsterBasePose = false;
            SetMonsterVisible(false);
        }
    }

    public Vector3 GetDeadPigeonWorldPosition(Transform buildingRoot)
    {
        if (deadPigeonRoot != null)
        {
            return deadPigeonRoot.transform.position;
        }

        Vector3 windowCenter;
        Vector3 outwardDir;
        float cabinFloorY;
        GetLeftLivingRoomWindowFrame(buildingRoot, out windowCenter, out outwardDir, out cabinFloorY);
        Vector3 inwardDir = -outwardDir;
        return new Vector3(windowCenter.x, cabinFloorY + 0.04f, windowCenter.z) + inwardDir * 0.62f;
    }

    // Jagged silhouette multipliers around the hole perimeter: natural plate-glass teeth & open center bays
    private static readonly float[] BrokenGlassJagPattern =
    {
        1.02f, 0.80f, 1.12f, 0.90f, 1.06f, 0.78f, 0.98f, 1.14f,
        0.84f, 1.05f, 0.79f, 1.11f, 0.92f, 0.81f, 1.01f, 1.15f,
        0.83f, 1.04f, 0.80f, 1.09f, 0.89f, 0.77f, 0.99f, 1.13f,
        0.85f, 1.07f, 0.79f, 1.12f, 0.91f, 0.82f, 1.03f, 1.14f,
        0.82f, 1.02f, 0.78f, 1.08f, 0.92f, 0.83f, 1.10f, 0.86f
    };

    private void BuildBrokenWindowAndDeadPigeon(Transform buildingRoot, Vector3 windowCenter, Vector3 outwardDir, float cabinFloorY)
    {
        if (brokenWindowVisualRoot != null)
        {
            Destroy(brokenWindowVisualRoot);
        }

        Vector3 inwardDir = -outwardDir;
        Vector3 rightDir = Vector3.Cross(Vector3.up, outwardDir).normalized;

        brokenWindowVisualRoot = new GameObject("Act3_BrokenWindowAndPigeon");
        if (buildingRoot != null)
        {
            brokenWindowVisualRoot.transform.SetParent(buildingRoot, true);
        }

        // 1. Punch out ONLY a single glass pane box (Center-Right Bottom Box: 0.536m x 0.592m) in Cabin_WindowGlass & Cabin_WindowRainRivulets,
        // leaving the wooden muntin crossbars and the other 7 window boxes completely intact!
        Bounds singlePaneBoxRect = PunchWindowHoleInCabinGlass(buildingRoot, windowCenter, rightDir, outwardDir);
        Vector3 holeCenter = singlePaneBoxRect.center;
        float holeHalfW = 0.165f;
        float holeHalfH = 0.185f;

        // 2. Configure realistic URP light-reactive glass materials (clear edge speculars + crisp frosted crack relief)
        Material fractureRimMat = CreateBrokenGlassMaterial(
            "Act3_GlassFractureRim_MAT",
            null,
            new Color(0.90f, 0.96f, 1.0f, 0.88f),
            new Color(0.18f, 0.23f, 0.28f),
            0.96f,
            3026);
        Material brokenEdgeGlassMat = CreateBrokenGlassMaterial(
            "Act3_BrokenGlassTooth_MAT",
            null,
            new Color(0.84f, 0.92f, 0.98f, 0.62f),
            new Color(0.09f, 0.12f, 0.16f),
            0.96f,
            3026);
        Material frostedToothMat = CreateBrokenGlassMaterial(
            "Act3_FrostedGlassTooth_MAT",
            null,
            new Color(0.90f, 0.95f, 0.99f, 0.78f),
            new Color(0.13f, 0.17f, 0.22f),
            0.86f,
            3026);

        Shader opaqueShardShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material floorShardMat = new Material(opaqueShardShader);
        floorShardMat.name = "Act3_FloorGlassShard_MAT";
        Color floorGlassCol = new Color(0.82f, 0.91f, 0.97f, 1.0f);
        floorShardMat.color = floorGlassCol;
        if (floorShardMat.HasProperty("_BaseColor")) floorShardMat.SetColor("_BaseColor", floorGlassCol);
        if (floorShardMat.HasProperty("_Smoothness")) floorShardMat.SetFloat("_Smoothness", 0.96f);
        if (floorShardMat.HasProperty("_EmissionColor"))
        {
            floorShardMat.EnableKeyword("_EMISSION");
            floorShardMat.SetColor("_EmissionColor", new Color(0.06f, 0.08f, 0.11f));
        }

        // 3. Build custom high-detail cracked glass pane + 3D fractured cut-glass edge rim strictly inside the single broken box
        Vector3[] jaggedHolePointsWorld = BuildShatteredWindowGlassRing(
            brokenWindowVisualRoot.transform,
            singlePaneBoxRect,
            holeCenter,
            rightDir,
            outwardDir,
            holeHalfW,
            holeHalfH);
        BuildHoleFractureRimRibbon(brokenWindowVisualRoot.transform, jaggedHolePointsWorld, holeCenter, outwardDir, fractureRimMat);

        // 4. Attach varied asymmetric 3D glass shards clinging around the jagged perimeter inside the single box
        Mesh[] shardVariants = CreateGlassShardVariantMeshes();
        Random.State oldState = Random.state;
        Random.InitState(30319);

        for (int i = 0; i < jaggedHolePointsWorld.Length; i++)
        {
            float jag = BrokenGlassJagPattern[i % BrokenGlassJagPattern.Length];
            if (jag > 1.02f) continue;

            Vector3 p = jaggedHolePointsWorld[i];
            Vector3 toHole = (holeCenter - p).normalized;
            bool isLongFang = jag < 0.84f;

            Material toothMat = (i % 3 == 0) ? frostedToothMat : brokenEdgeGlassMat;
            Mesh toothMesh = shardVariants[isLongFang ? (i % 2 == 0 ? 0 : 2) : (i % shardVariants.Length)];
            SpawnClingingGlassTooth(
                brokenWindowVisualRoot.transform,
                "WindowGlassTooth_" + i,
                toothMesh,
                toothMat,
                p,
                toHole,
                outwardDir,
                inwardDir,
                isLongFang ? Random.Range(0.018f, 0.032f) : Random.Range(0.022f, 0.038f),
                isLongFang ? Random.Range(0.032f, 0.058f) : Random.Range(0.018f, 0.036f),
                Random.Range(0.005f, 0.008f));

            if (isLongFang && (i % 3 == 0))
            {
                Vector3 tangent = Vector3.Cross(outwardDir, toHole).normalized;
                Vector3 microDir = (toHole + tangent * Random.Range(-0.35f, 0.35f)).normalized;
                SpawnClingingGlassTooth(
                    brokenWindowVisualRoot.transform,
                    "WindowGlassSplinter_" + i,
                    shardVariants[(i + 1) % shardVariants.Length],
                    frostedToothMat,
                    p + tangent * Random.Range(-0.010f, 0.010f),
                    microDir,
                    outwardDir,
                    inwardDir,
                    Random.Range(0.012f, 0.022f),
                    Random.Range(0.018f, 0.034f),
                    0.005f);
            }
        }

        // 5. Spawn an instant burst of shattering glass splinters spraying into the room upon impact
        SpawnGlassShatterBurst(brokenWindowVisualRoot.transform, holeCenter, inwardDir, brokenEdgeGlassMat);

        // 6. Scatter tiny glass shards along the window bottom wooden sill and sofa top backrest right under the break
        ScatterSillAndSofaGlassShards(
            brokenWindowVisualRoot.transform,
            singlePaneBoxRect,
            holeCenter,
            rightDir,
            inwardDir,
            shardVariants,
            floorShardMat,
            brokenEdgeGlassMat);

        // 9. Find unobstructed open living room floor spot near the sofa edge so the Dead Pigeon is clearly visible
        Vector3 floorCenter = ResolveOpenFloorSpotBelowWindow(buildingRoot, windowCenter, rightDir, inwardDir, cabinFloorY);

        // 10. Scatter varied sharp glass shards & glinting glass crumbs across the floor around the Dead Pigeon
        for (int i = 0; i < 28; i++)
        {
            GameObject floorShard = new GameObject("FloorGlassShard_" + i);
            floorShard.transform.SetParent(brokenWindowVisualRoot.transform, true);
            floorShard.AddComponent<MeshFilter>().sharedMesh = shardVariants[i % shardVariants.Length];
            MeshRenderer fmr = floorShard.AddComponent<MeshRenderer>();
            fmr.sharedMaterial = (i % 4 == 0) ? brokenEdgeGlassMat : floorShardMat;

            float rx = Random.Range(-0.68f, 0.68f);
            float rz = Random.Range(-0.46f, 0.48f);
            floorShard.transform.position = floorCenter + rightDir * rx + inwardDir * rz;
            floorShard.transform.rotation = Quaternion.Euler(90f + Random.Range(-4f, 4f), Random.Range(0f, 360f), 0f);
            floorShard.transform.localScale = new Vector3(
                Random.Range(0.024f, 0.072f),
                Random.Range(0.042f, 0.115f),
                Random.Range(0.005f, 0.009f));
        }

        for (int i = 0; i < 18; i++)
        {
            GameObject crumb = new GameObject("FloorGlassCrumb_" + i);
            crumb.transform.SetParent(brokenWindowVisualRoot.transform, true);
            crumb.AddComponent<MeshFilter>().sharedMesh = shardVariants[(i + 1) % shardVariants.Length];
            MeshRenderer cmr = crumb.AddComponent<MeshRenderer>();
            cmr.sharedMaterial = fractureRimMat;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(0.14f, 0.78f);
            crumb.transform.position = floorCenter + rightDir * (Mathf.Cos(angle) * dist) + inwardDir * (Mathf.Sin(angle) * dist * 0.75f);
            crumb.transform.rotation = Quaternion.Euler(Random.Range(72f, 90f), Random.Range(0f, 360f), 0f);
            crumb.transform.localScale = new Vector3(
                Random.Range(0.010f, 0.024f),
                Random.Range(0.014f, 0.032f),
                0.005f);
        }

        Random.state = oldState;

        // 11. Build the 3D Dead Pigeon model lying on the clear floor amidst the shattered glass
        deadPigeonRoot = BuildDeadPigeonModel(brokenWindowVisualRoot.transform, floorCenter, rightDir, inwardDir);

        // 12. Spawn generous trigger collider in front of the broken window for "[E] Inspect"
        GameObject inspectGo = new GameObject("Act3_BrokenWindowInspectTrigger");
        inspectGo.transform.SetParent(brokenWindowVisualRoot.transform, true);
        inspectGo.transform.position = windowCenter + inwardDir * 0.60f;
        inspectGo.transform.rotation = Quaternion.LookRotation(outwardDir, Vector3.up);
        BoxCollider inspectBox = inspectGo.AddComponent<BoxCollider>();
        inspectBox.isTrigger = true;
        inspectBox.center = Vector3.zero;
        inspectBox.size = new Vector3(2.45f, 1.75f, 1.45f);
        inspectGo.AddComponent<CabinBrokenWindowInteractable>();
    }

    private static Material CreateBrokenGlassMaterial(
        string matName,
        Texture2D texture,
        Color baseTint,
        Color emissionTint,
        float smoothness,
        int renderQueue)
    {
        Shader glassShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(glassShader);
        mat.name = matName;

        mat.color = baseTint;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseTint);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseTint);
        if (texture != null)
        {
            mat.mainTexture = texture;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        }

        bool isTransparent = baseTint.a < 0.99f || texture != null;
        if (isTransparent)
        {
            if (mat.HasProperty("_WorkflowMode")) mat.SetFloat("_WorkflowMode", 1f);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_BlendModePreserveSpecular")) mat.SetFloat("_BlendModePreserveSpecular", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetShaderPassEnabled("ShadowCaster", false);
            mat.SetShaderPassEnabled("DepthOnly", false);
        }

        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.08f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 1f);
        mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0f);
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

        if (emissionTint.maxColorComponent > 0.001f)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionTint);
            if (texture != null && mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", texture);
        }

        mat.renderQueue = renderQueue;
        return mat;
    }

    private static void SpawnClingingGlassTooth(
        Transform parent,
        string name,
        Mesh shardMesh,
        Material mat,
        Vector3 rimWorldPos,
        Vector3 toHoleDir,
        Vector3 outwardDir,
        Vector3 inwardDir,
        float scaleW,
        float scaleLen,
        float scaleThickness)
    {
        GameObject edgeTooth = new GameObject(name);
        edgeTooth.transform.SetParent(parent, true);
        edgeTooth.AddComponent<MeshFilter>().sharedMesh = shardMesh;
        MeshRenderer emr = edgeTooth.AddComponent<MeshRenderer>();
        emr.sharedMaterial = mat;
        emr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Slight inward impact deflection with slight twist so teeth catch oblique candlelight from inside the room
        Vector3 bentDir = (toHoleDir + inwardDir * Random.Range(0.02f, 0.32f)).normalized;
        Vector3 sideVec = Vector3.Cross(outwardDir, bentDir).normalized;
        if (sideVec.sqrMagnitude < 0.001f) sideVec = Vector3.right;
        Vector3 planeNormal = Vector3.Cross(sideVec, bentDir).normalized;

        edgeTooth.transform.position = rimWorldPos - bentDir * (scaleLen * 0.14f);
        edgeTooth.transform.rotation = Quaternion.LookRotation(planeNormal, bentDir) * Quaternion.Euler(0f, 0f, Random.Range(-5f, 5f));
        edgeTooth.transform.localScale = new Vector3(scaleW, scaleLen, scaleThickness);
    }

    private static void ScatterSillAndSofaGlassShards(
        Transform parent,
        Bounds windowRectWorld,
        Vector3 holeCenter,
        Vector3 rightDir,
        Vector3 inwardDir,
        Mesh[] shardVariants,
        Material floorShardMat,
        Material edgeGlassMat)
    {
        Vector3 winCenter = windowRectWorld.center;
        float sillY = windowRectWorld.min.y + 0.045f;

        // Small glittering crumbs & shards resting visibly along the inner wooden bottom sill
        for (int i = 0; i < 12; i++)
        {
            float alongX = Random.Range(-0.82f, 0.82f);
            Vector3 sillPos = new Vector3(holeCenter.x, sillY, winCenter.z)
                              + rightDir * alongX
                              + inwardDir * Random.Range(0.03f, 0.09f);

            GameObject sillShard = new GameObject("WindowSillShard_" + i);
            sillShard.transform.SetParent(parent, true);
            sillShard.AddComponent<MeshFilter>().sharedMesh = shardVariants[i % shardVariants.Length];
            MeshRenderer smr = sillShard.AddComponent<MeshRenderer>();
            smr.sharedMaterial = (i % 2 == 0) ? floorShardMat : edgeGlassMat;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            sillShard.transform.position = sillPos;
            sillShard.transform.rotation = Quaternion.Euler(Random.Range(68f, 90f), Random.Range(0f, 360f), 0f);
            sillShard.transform.localScale = new Vector3(
                Random.Range(0.015f, 0.036f),
                Random.Range(0.022f, 0.055f),
                0.006f);
        }

        // Raycast downward onto PFB_Sofa backrest / cushions just inside the broken window so spilled glass sits directly on the sofa
        for (int i = 0; i < 10; i++)
        {
            Vector3 probeOrigin = new Vector3(holeCenter.x, windowRectWorld.min.y + 0.25f, holeCenter.z)
                                  + rightDir * Random.Range(-0.72f, 0.72f)
                                  + inwardDir * Random.Range(0.28f, 1.05f);
            if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, 1.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == null) continue;
                GameObject sofaShard = new GameObject("SofaGlassShard_" + i);
                sofaShard.transform.SetParent(parent, true);
                sofaShard.AddComponent<MeshFilter>().sharedMesh = shardVariants[(i + 2) % shardVariants.Length];
                MeshRenderer smr = sofaShard.AddComponent<MeshRenderer>();
                smr.sharedMaterial = (i % 3 == 0) ? edgeGlassMat : floorShardMat;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                sofaShard.transform.position = hit.point + hit.normal * 0.006f;
                sofaShard.transform.rotation = Quaternion.FromToRotation(Vector3.forward, hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                sofaShard.transform.localScale = new Vector3(
                    Random.Range(0.016f, 0.042f),
                    Random.Range(0.026f, 0.068f),
                    0.006f);
            }
        }
    }

    private const string BrokenMullionOverrideChildName = "Act3_BuildingBase_BrokenMullionOverride";
    private const string BrokenGlassOverrideChildName = "Act3_CabinWindowGlass_RuntimeOverride";

    internal static Mesh GetReadableSourceMesh(GameObject targetObj)
    {
        if (targetObj == null) return null;

        MeshFilter mf = targetObj.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
        {
            return mf.sharedMesh;
        }

        // Unity Static Batching replaces MeshFilter.sharedMesh with an unreadable 'Combined Mesh (root: scene)';
        // fall back cleanly to MeshCollider.sharedMesh, which retains the original readable source asset.
        MeshCollider mc = targetObj.GetComponent<MeshCollider>();
        if (mc != null && mc.sharedMesh != null && mc.sharedMesh.isReadable)
        {
            return mc.sharedMesh;
        }

        return null;
    }

    private static void ApplyRuntimeMeshPreservingStaticBatch(GameObject targetObj, Mesh modifiedMesh, string overrideChildName)
    {
        if (targetObj == null || modifiedMesh == null) return;

        MeshFilter mf = targetObj.GetComponent<MeshFilter>();
        MeshRenderer mr = targetObj.GetComponent<MeshRenderer>();

        if (mr != null && mr.isPartOfStaticBatch)
        {
            mr.enabled = false;

            Transform existing = targetObj.transform.Find(overrideChildName);
            GameObject overrideGo = existing != null ? existing.gameObject : new GameObject(overrideChildName);
            if (existing == null)
            {
                overrideGo.transform.SetParent(targetObj.transform, false);
                overrideGo.transform.localPosition = Vector3.zero;
                overrideGo.transform.localRotation = Quaternion.identity;
                overrideGo.transform.localScale = Vector3.one;
            }
            overrideGo.layer = targetObj.layer;

            MeshFilter omf = overrideGo.GetComponent<MeshFilter>() ?? overrideGo.AddComponent<MeshFilter>();
            omf.sharedMesh = modifiedMesh;

            MeshRenderer omr = overrideGo.GetComponent<MeshRenderer>() ?? overrideGo.AddComponent<MeshRenderer>();
            omr.sharedMaterials = mr.sharedMaterials;
            omr.shadowCastingMode = mr.shadowCastingMode;
            omr.receiveShadows = mr.receiveShadows;
            omr.lightProbeUsage = mr.lightProbeUsage;
            omr.reflectionProbeUsage = mr.reflectionProbeUsage;
            omr.lightmapIndex = mr.lightmapIndex;
            omr.lightmapScaleOffset = mr.lightmapScaleOffset;
            omr.realtimeLightmapIndex = mr.realtimeLightmapIndex;
            omr.realtimeLightmapScaleOffset = mr.realtimeLightmapScaleOffset;
            omr.renderingLayerMask = mr.renderingLayerMask;
        }
        else if (mf != null)
        {
            mf.sharedMesh = modifiedMesh;
        }
    }

    private List<Vector3> PunchBrokenWindowMullionsInBuildingMesh(
        Bounds windowRectWorld,
        Vector3 holeCenterWorld,
        Vector3 rightDir,
        Vector3 outwardDir,
        float cutHalfW,
        float cutHalfH)
    {
        List<Vector3> snappedTipPositionsWorld = new List<Vector3>(4);
        GameObject buildingBase = CabinWindowUtility.FindBuildingBaseInScene(gameObject.scene);
        if (buildingBase == null) return snappedTipPositionsWorld;

        Transform prevOverride = buildingBase.transform.Find(BrokenMullionOverrideChildName);
        GameObject meshOwner = prevOverride != null ? prevOverride.gameObject : buildingBase;
        Mesh srcMesh = GetReadableSourceMesh(meshOwner) ?? GetReadableSourceMesh(buildingBase);
        if (srcMesh == null) return snappedTipPositionsWorld;

        Transform bt = buildingBase.transform;

        // Track snapped tips where central mullions break around the hole so wood splinter end-caps align cleanly
        snappedTipPositionsWorld.Add(holeCenterWorld + Vector3.up * cutHalfH);     // top center mullion stub
        snappedTipPositionsWorld.Add(holeCenterWorld - Vector3.up * cutHalfH);     // bottom center mullion stub
        snappedTipPositionsWorld.Add(holeCenterWorld - rightDir * cutHalfW);       // left crossbar stub
        snappedTipPositionsWorld.Add(holeCenterWorld + rightDir * cutHalfW);       // right crossbar stub

        Vector3[] srcVerts = srcMesh.vertices;
        Vector3[] srcNormals = srcMesh.normals;
        Vector4[] srcTangents = srcMesh.tangents;
        Vector2[] srcUv = srcMesh.uv;
        Vector2[] srcUv2 = srcMesh.uv2;
        Color[] srcColors = srcMesh.colors;

        List<Vector3> verts = new List<Vector3>(srcVerts);
        bool hasNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;
        bool hasTangents = srcTangents != null && srcTangents.Length == srcVerts.Length;
        bool hasUv = srcUv != null && srcUv.Length == srcVerts.Length;
        bool hasUv2 = srcUv2 != null && srcUv2.Length == srcVerts.Length;
        bool hasColors = srcColors != null && srcColors.Length == srcVerts.Length;

        List<Vector3> normals = hasNormals ? new List<Vector3>(srcNormals) : null;
        List<Vector4> tangents = hasTangents ? new List<Vector4>(srcTangents) : null;
        List<Vector2> uv = hasUv ? new List<Vector2>(srcUv) : null;
        List<Vector2> uv2 = hasUv2 ? new List<Vector2>(srcUv2) : null;
        List<Color> colors = hasColors ? new List<Color>(srcColors) : null;

        float halfWinW = Mathf.Max(0.95f, Mathf.Abs(Vector3.Dot(windowRectWorld.size, rightDir)) * 0.5f);
        float halfWinH = Mathf.Max(0.55f, windowRectWorld.size.y * 0.5f);
        Vector3 winCenter = windowRectWorld.center;

        // Window #6 internal mullions live strictly inside the outer jambs/sills and within +-7.5cm of the glass plane
        float maxRecessRight = halfWinW - 0.065f;
        float maxRecessUp = halfWinH - 0.065f;
        const float maxRecessOut = 0.075f;

        int subCount = srcMesh.subMeshCount;
        List<int>[] outSubTris = new List<int>[subCount];
        bool anyModified = false;

        for (int sub = 0; sub < subCount; sub++)
        {
            int[] subTris = srcMesh.GetTriangles(sub);
            List<int> kept = new List<int>(subTris.Length);

            for (int t = 0; t + 2 < subTris.Length; t += 3)
            {
                int i0 = subTris[t];
                int i1 = subTris[t + 1];
                int i2 = subTris[t + 2];

                Vector3 w0 = bt.TransformPoint(srcVerts[i0]);
                Vector3 w1 = bt.TransformPoint(srcVerts[i1]);
                Vector3 w2 = bt.TransformPoint(srcVerts[i2]);

                if (IsStrictlyInsideWindowRecess(w0, winCenter, rightDir, outwardDir, maxRecessRight, maxRecessUp, maxRecessOut) &&
                    IsStrictlyInsideWindowRecess(w1, winCenter, rightDir, outwardDir, maxRecessRight, maxRecessUp, maxRecessOut) &&
                    IsStrictlyInsideWindowRecess(w2, winCenter, rightDir, outwardDir, maxRecessRight, maxRecessUp, maxRecessOut))
                {
                    Vector2 c0 = ProjectToHoleLocal2D(w0, holeCenterWorld, rightDir);
                    Vector2 c1 = ProjectToHoleLocal2D(w1, holeCenterWorld, rightDir);
                    Vector2 c2 = ProjectToHoleLocal2D(w2, holeCenterWorld, rightDir);

                    float minX = Mathf.Min(c0.x, Mathf.Min(c1.x, c2.x));
                    float maxX = Mathf.Max(c0.x, Mathf.Max(c1.x, c2.x));
                    float minY = Mathf.Min(c0.y, Mathf.Min(c1.y, c2.y));
                    float maxY = Mathf.Max(c0.y, Mathf.Max(c1.y, c2.y));

                    if (maxX > -cutHalfW && minX < cutHalfW && maxY > -cutHalfH && minY < cutHalfH)
                    {
                        anyModified = true;
                        AppendClippedTriangleOutsideRect(
                            i0, i1, i2, c0, c1, c2, cutHalfW, cutHalfH,
                            verts, normals, tangents, uv, uv2, colors, kept);
                        continue;
                    }
                }

                kept.Add(i0);
                kept.Add(i1);
                kept.Add(i2);
            }

            outSubTris[sub] = kept;
        }

        if (anyModified)
        {
            Mesh brokenMullionMesh = Instantiate(srcMesh);
            brokenMullionMesh.name = srcMesh.name + "_BrokenMullionRuntime";
            if (verts.Count > 65000)
            {
                brokenMullionMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            brokenMullionMesh.SetVertices(verts);
            if (hasNormals) brokenMullionMesh.SetNormals(normals);
            if (hasTangents) brokenMullionMesh.SetTangents(tangents);
            if (hasUv) brokenMullionMesh.SetUVs(0, uv);
            if (hasUv2) brokenMullionMesh.SetUVs(1, uv2);
            if (hasColors) brokenMullionMesh.SetColors(colors);

            brokenMullionMesh.subMeshCount = subCount;
            for (int sub = 0; sub < subCount; sub++)
            {
                brokenMullionMesh.SetTriangles(outSubTris[sub], sub);
            }
            brokenMullionMesh.RecalculateBounds();
            ApplyRuntimeMeshPreservingStaticBatch(buildingBase, brokenMullionMesh, BrokenMullionOverrideChildName);
        }

        return snappedTipPositionsWorld;
    }

    private static bool IsStrictlyInsideWindowRecess(
        Vector3 worldPos,
        Vector3 winCenter,
        Vector3 rightDir,
        Vector3 outwardDir,
        float maxRight,
        float maxUp,
        float maxOut)
    {
        Vector3 rel = worldPos - winCenter;
        return Mathf.Abs(Vector3.Dot(rel, rightDir)) <= maxRight &&
               Mathf.Abs(rel.y) <= maxUp &&
               Mathf.Abs(Vector3.Dot(rel, outwardDir)) <= maxOut;
    }

    private static Vector2 ProjectToHoleLocal2D(Vector3 worldPos, Vector3 holeCenterWorld, Vector3 rightDir)
    {
        Vector3 rel = worldPos - holeCenterWorld;
        return new Vector2(Vector3.Dot(rel, rightDir), rel.y);
    }

    private struct ClippedPolyVertex
    {
        public Vector2 p;
        public Vector3 pos;
        public Vector3 normal;
        public Vector4 tangent;
        public Vector2 uv;
        public Vector2 uv2;
        public Color color;
    }

    private static void AppendClippedTriangleOutsideRect(
        int i0, int i1, int i2,
        Vector2 c0, Vector2 c1, Vector2 c2,
        float cutHalfW, float cutHalfH,
        List<Vector3> verts,
        List<Vector3> normals,
        List<Vector4> tangents,
        List<Vector2> uv,
        List<Vector2> uv2,
        List<Color> colors,
        List<int> outTris)
    {
        ClippedPolyVertex[] tri =
        {
            MakePolyVert(i0, c0, verts, normals, tangents, uv, uv2, colors),
            MakePolyVert(i1, c1, verts, normals, tangents, uv, uv2, colors),
            MakePolyVert(i2, c2, verts, normals, tangents, uv, uv2, colors)
        };

        // Partition the complement of [-cutHalfW..+cutHalfW] x [-cutHalfH..+cutHalfH] into 4 disjoint half-space slabs
        // Slab 1: x <= -cutHalfW
        List<ClippedPolyVertex> poly = new List<ClippedPolyVertex>(tri);
        ClipPolyAgainstLine(poly, true, true, -cutHalfW);
        EmitClippedPolygonFan(poly, verts, normals, tangents, uv, uv2, colors, outTris);

        // Slab 2: x >= +cutHalfW
        poly.Clear();
        poly.AddRange(tri);
        ClipPolyAgainstLine(poly, true, false, cutHalfW);
        EmitClippedPolygonFan(poly, verts, normals, tangents, uv, uv2, colors, outTris);

        // Slab 3: -cutHalfW <= x <= +cutHalfW AND y <= -cutHalfH
        poly.Clear();
        poly.AddRange(tri);
        ClipPolyAgainstLine(poly, true, false, -cutHalfW);
        ClipPolyAgainstLine(poly, true, true, cutHalfW);
        ClipPolyAgainstLine(poly, false, true, -cutHalfH);
        EmitClippedPolygonFan(poly, verts, normals, tangents, uv, uv2, colors, outTris);

        // Slab 4: -cutHalfW <= x <= +cutHalfW AND y >= +cutHalfH
        poly.Clear();
        poly.AddRange(tri);
        ClipPolyAgainstLine(poly, true, false, -cutHalfW);
        ClipPolyAgainstLine(poly, true, true, cutHalfW);
        ClipPolyAgainstLine(poly, false, false, cutHalfH);
        EmitClippedPolygonFan(poly, verts, normals, tangents, uv, uv2, colors, outTris);
    }

    private static ClippedPolyVertex MakePolyVert(
        int idx,
        Vector2 p2,
        List<Vector3> verts,
        List<Vector3> normals,
        List<Vector4> tangents,
        List<Vector2> uv,
        List<Vector2> uv2,
        List<Color> colors)
    {
        return new ClippedPolyVertex
        {
            p = p2,
            pos = verts[idx],
            normal = normals != null ? normals[idx] : Vector3.forward,
            tangent = tangents != null ? tangents[idx] : Vector4.zero,
            uv = uv != null ? uv[idx] : Vector2.zero,
            uv2 = uv2 != null ? uv2[idx] : Vector2.zero,
            color = colors != null ? colors[idx] : Color.white
        };
    }

    private static void ClipPolyAgainstLine(List<ClippedPolyVertex> poly, bool testX, bool keepLessOrEqual, float bound)
    {
        if (poly.Count == 0) return;

        List<ClippedPolyVertex> input = new List<ClippedPolyVertex>(poly);
        poly.Clear();

        for (int i = 0; i < input.Count; i++)
        {
            ClippedPolyVertex a = input[i];
            ClippedPolyVertex b = input[(i + 1) % input.Count];

            float va = testX ? a.p.x : a.p.y;
            float vb = testX ? b.p.x : b.p.y;
            bool inA = keepLessOrEqual ? (va <= bound) : (va >= bound);
            bool inB = keepLessOrEqual ? (vb <= bound) : (vb >= bound);

            if (inA && inB)
            {
                poly.Add(b);
            }
            else if (inA && !inB)
            {
                poly.Add(LerpPolyVert(a, b, bound, va, vb));
            }
            else if (!inA && inB)
            {
                poly.Add(LerpPolyVert(a, b, bound, va, vb));
                poly.Add(b);
            }
        }
    }

    private static ClippedPolyVertex LerpPolyVert(ClippedPolyVertex a, ClippedPolyVertex b, float bound, float va, float vb)
    {
        float denom = vb - va;
        float t = Mathf.Abs(denom) > 1e-6f ? Mathf.Clamp01((bound - va) / denom) : 0f;
        Vector3 norm = Vector3.Lerp(a.normal, b.normal, t);
        if (norm.sqrMagnitude > 1e-5f) norm.Normalize();

        return new ClippedPolyVertex
        {
            p = Vector2.Lerp(a.p, b.p, t),
            pos = Vector3.Lerp(a.pos, b.pos, t),
            normal = norm,
            tangent = Vector4.Lerp(a.tangent, b.tangent, t),
            uv = Vector2.Lerp(a.uv, b.uv, t),
            uv2 = Vector2.Lerp(a.uv2, b.uv2, t),
            color = Color.Lerp(a.color, b.color, t)
        };
    }

    private static void EmitClippedPolygonFan(
        List<ClippedPolyVertex> poly,
        List<Vector3> verts,
        List<Vector3> normals,
        List<Vector4> tangents,
        List<Vector2> uv,
        List<Vector2> uv2,
        List<Color> colors,
        List<int> outTris)
    {
        if (poly.Count < 3) return;

        int baseIdx = verts.Count;
        for (int i = 0; i < poly.Count; i++)
        {
            ClippedPolyVertex cv = poly[i];
            verts.Add(cv.pos);
            if (normals != null) normals.Add(cv.normal);
            if (tangents != null) tangents.Add(cv.tangent);
            if (uv != null) uv.Add(cv.uv);
            if (uv2 != null) uv2.Add(cv.uv2);
            if (colors != null) colors.Add(cv.color);
        }

        for (int i = 1; i + 1 < poly.Count; i++)
        {
            outTris.Add(baseIdx);
            outTris.Add(baseIdx + i);
            outTris.Add(baseIdx + i + 1);
        }
    }

    private static void BuildSnappedMullionStubs(
        Transform parent,
        List<Vector3> snappedTipsWorld,
        Vector3 rightDir,
        Vector3 outwardDir,
        Vector3 inwardDir)
    {
        if (snappedTipsWorld == null || snappedTipsWorld.Count < 4) return;

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material paintedWoodMat = CreateLitMaterial(litShader, new Color(0.76f, 0.77f, 0.79f), 0.25f);
        Material rawSplinterMat = CreateLitMaterial(litShader, new Color(0.62f, 0.50f, 0.36f), 0.12f);

        // Directions pointing away from the open hole back into each solid wooden bar
        Vector3[] awayFromHoleDirs =
        {
            Vector3.up,
            Vector3.down,
            -rightDir,
            rightDir
        };

        for (int i = 0; i < 4; i++)
        {
            Vector3 tipWorld = snappedTipsWorld[i];
            Vector3 backDir = awayFromHoleDirs[i];
            bool isVertical = (i < 2);
            float barThicknessX = isVertical ? 0.088f : 0.048f;
            float barThicknessY = isVertical ? 0.048f : 0.050f;

            // Cap the clipped mullion end with off-white wood plus jagged raw-wood splinters sticking toward the break
            GameObject stubCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stubCap.name = "SnappedMullionCap_" + i;
            Destroy(stubCap.GetComponent<Collider>());
            stubCap.transform.SetParent(parent, true);
            stubCap.transform.position = tipWorld + backDir * 0.012f;
            stubCap.transform.rotation = Quaternion.LookRotation(outwardDir, Vector3.up);
            stubCap.transform.localScale = new Vector3(barThicknessX, barThicknessY, 0.056f);
            stubCap.GetComponent<Renderer>().sharedMaterial = paintedWoodMat;

            for (int s = 0; s < 3; s++)
            {
                GameObject splinter = GameObject.CreatePrimitive(PrimitiveType.Cube);
                splinter.name = "MullionWoodSplinter_" + i + "_" + s;
                Destroy(splinter.GetComponent<Collider>());
                splinter.transform.SetParent(parent, true);

                float offsetAcross = (s - 1) * 0.016f;
                Vector3 crossOffset = isVertical ? rightDir * offsetAcross : Vector3.up * offsetAcross;
                Vector3 splinterDir = (-backDir + inwardDir * Random.Range(0.05f, 0.28f)).normalized;

                splinter.transform.position = tipWorld + crossOffset + splinterDir * Random.Range(0.015f, 0.036f);
                splinter.transform.rotation = Quaternion.LookRotation(outwardDir, splinterDir) * Quaternion.Euler(0f, Random.Range(-8f, 8f), Random.Range(-9f, 9f));
                splinter.transform.localScale = new Vector3(
                    Random.Range(0.009f, 0.016f),
                    Random.Range(0.038f, 0.072f),
                    Random.Range(0.010f, 0.022f));
                splinter.GetComponent<Renderer>().sharedMaterial = (s == 1) ? rawSplinterMat : paintedWoodMat;
            }
        }
    }

    private Bounds PunchWindowHoleInCabinGlass(
        Transform buildingRoot,
        Vector3 windowCenter,
        Vector3 rightDir,
        Vector3 outwardDir)
    {
        const float singlePaneHalfW = 0.268f;
        const float singlePaneHalfH = 0.296f;

        Vector3 fallbackBoxCenter = windowCenter + rightDir * 0.3335f - Vector3.up * 0.18f;
        Bounds fallbackRect = new Bounds(
            fallbackBoxCenter,
            Vector3.Max(Vector3.one * 0.02f, new Vector3(
                Mathf.Abs(rightDir.x) * (singlePaneHalfW * 2f),
                singlePaneHalfH * 2f,
                Mathf.Abs(rightDir.z) * (singlePaneHalfW * 2f))));

        GameObject buildingBase = CabinWindowUtility.FindBuildingBaseInScene(gameObject.scene);
        if (buildingBase == null) return fallbackRect;

        Transform glassChild = buildingBase.transform.Find(CabinWindowUtility.GlassChildObjectName);
        if (glassChild == null) return fallbackRect;

        Transform prevOverride = glassChild.Find(BrokenGlassOverrideChildName);
        GameObject glassMeshOwner = prevOverride != null ? prevOverride.gameObject : glassChild.gameObject;
        Mesh srcMesh = GetReadableSourceMesh(glassMeshOwner) ?? GetReadableSourceMesh(glassChild.gameObject);
        if (srcMesh == null) return fallbackRect;

        Vector3[] srcVerts = srcMesh.vertices;
        Vector3[] srcNormals = srcMesh.normals;
        Vector4[] srcTangents = srcMesh.tangents;
        Vector2[] srcUv = srcMesh.uv;
        Vector2[] srcUv2 = srcMesh.uv2;
        Color[] srcColors = srcMesh.colors;
        int[] tris = srcMesh.triangles;

        Bounds fullWindowWorldBounds = new Bounds(windowCenter, Vector3.zero);
        bool hasExtracted = false;

        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            Vector3 p0 = glassChild.TransformPoint(srcVerts[tris[t]]);
            Vector3 p1 = glassChild.TransformPoint(srcVerts[tris[t + 1]]);
            Vector3 p2 = glassChild.TransformPoint(srcVerts[tris[t + 2]]);
            Vector3 triCenter = (p0 + p1 + p2) / 3f;

            Vector3 rel = triCenter - windowCenter;
            float alongRight = Mathf.Abs(Vector3.Dot(rel, rightDir));
            float alongUp = Mathf.Abs(rel.y);
            float alongOut = Mathf.Abs(Vector3.Dot(rel, outwardDir));

            if (alongRight <= 1.35f && alongUp <= 0.85f && alongOut <= 0.45f)
            {
                if (!hasExtracted)
                {
                    fullWindowWorldBounds = new Bounds(p0, Vector3.zero);
                    hasExtracted = true;
                }
                else
                {
                    fullWindowWorldBounds.Encapsulate(p0);
                }
                fullWindowWorldBounds.Encapsulate(p1);
                fullWindowWorldBounds.Encapsulate(p2);
            }
        }

        if (!hasExtracted) return fallbackRect;

        // Center-Right Bottom Pane Box of Window #6 (local X in [1.426..1.961], Y in [1.341..1.933])
        Vector3 targetPaneCenterWorld = fullWindowWorldBounds.center + rightDir * 0.3335f - Vector3.up * 0.322f;
        Bounds singlePaneWorldBounds = new Bounds(
            targetPaneCenterWorld,
            Vector3.Max(Vector3.one * 0.02f, new Vector3(
                Mathf.Abs(rightDir.x) * (singlePaneHalfW * 2f),
                singlePaneHalfH * 2f,
                Mathf.Abs(rightDir.z) * (singlePaneHalfW * 2f))));

        List<Vector3> verts = new List<Vector3>(srcVerts);
        bool hasNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;
        bool hasTangents = srcTangents != null && srcTangents.Length == srcVerts.Length;
        bool hasUv = srcUv != null && srcUv.Length == srcVerts.Length;
        bool hasUv2 = srcUv2 != null && srcUv2.Length == srcVerts.Length;
        bool hasColors = srcColors != null && srcColors.Length == srcVerts.Length;

        List<Vector3> normals = hasNormals ? new List<Vector3>(srcNormals) : null;
        List<Vector4> tangents = hasTangents ? new List<Vector4>(srcTangents) : null;
        List<Vector2> uv = hasUv ? new List<Vector2>(srcUv) : null;
        List<Vector2> uv2 = hasUv2 ? new List<Vector2>(srcUv2) : null;
        List<Color> colors = hasColors ? new List<Color>(srcColors) : null;
        List<int> keptTris = new List<int>(tris.Length + 64);
        bool anyClipped = false;

        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            int i0 = tris[t];
            int i1 = tris[t + 1];
            int i2 = tris[t + 2];

            Vector3 p0 = glassChild.TransformPoint(srcVerts[i0]);
            Vector3 p1 = glassChild.TransformPoint(srcVerts[i1]);
            Vector3 p2 = glassChild.TransformPoint(srcVerts[i2]);
            Vector3 triCenter = (p0 + p1 + p2) / 3f;

            Vector3 rel = triCenter - windowCenter;
            float alongRight = Mathf.Abs(Vector3.Dot(rel, rightDir));
            float alongUp = Mathf.Abs(rel.y);
            float alongOut = Mathf.Abs(Vector3.Dot(rel, outwardDir));

            if (alongRight <= 1.35f && alongUp <= 0.85f && alongOut <= 0.45f)
            {
                Vector2 c0 = ProjectToHoleLocal2D(p0, targetPaneCenterWorld, rightDir);
                Vector2 c1 = ProjectToHoleLocal2D(p1, targetPaneCenterWorld, rightDir);
                Vector2 c2 = ProjectToHoleLocal2D(p2, targetPaneCenterWorld, rightDir);

                float minX = Mathf.Min(c0.x, Mathf.Min(c1.x, c2.x));
                float maxX = Mathf.Max(c0.x, Mathf.Max(c1.x, c2.x));
                float minY = Mathf.Min(c0.y, Mathf.Min(c1.y, c2.y));
                float maxY = Mathf.Max(c0.y, Mathf.Max(c1.y, c2.y));

                if (maxX > -singlePaneHalfW && minX < singlePaneHalfW && maxY > -singlePaneHalfH && minY < singlePaneHalfH)
                {
                    anyClipped = true;
                    AppendClippedTriangleOutsideRect(
                        i0, i1, i2, c0, c1, c2, singlePaneHalfW, singlePaneHalfH,
                        verts, normals, tangents, uv, uv2, colors, keptTris);
                    continue;
                }
            }

            keptTris.Add(i0);
            keptTris.Add(i1);
            keptTris.Add(i2);
        }

        if (anyClipped)
        {
            Mesh punchedMesh = Instantiate(srcMesh);
            punchedMesh.name = srcMesh.name + "_SingleBoxShatteredRuntime";
            punchedMesh.SetVertices(verts);
            if (hasNormals) punchedMesh.SetNormals(normals);
            if (hasTangents) punchedMesh.SetTangents(tangents);
            if (hasUv) punchedMesh.SetUVs(0, uv);
            if (hasUv2) punchedMesh.SetUVs(1, uv2);
            if (hasColors) punchedMesh.SetColors(colors);
            punchedMesh.SetTriangles(keptTris, 0);
            punchedMesh.RecalculateBounds();
            ApplyRuntimeMeshPreservingStaticBatch(glassChild.gameObject, punchedMesh, BrokenGlassOverrideChildName);

            Transform rivuletsChild = glassChild.Find("Cabin_WindowRainRivulets");
            if (rivuletsChild != null)
            {
                MeshFilter rmf = rivuletsChild.GetComponent<MeshFilter>();
                if (rmf != null)
                {
                    rmf.sharedMesh = Act3RainStormController.BuildVerticalRivuletMesh(punchedMesh);
                }
            }
        }

        return singlePaneWorldBounds;
    }

    private Vector3[] BuildShatteredWindowGlassRing(
        Transform parent,
        Bounds windowRectWorld,
        Vector3 holeCenter,
        Vector3 rightDir,
        Vector3 outwardDir,
        float holeHalfW,
        float holeHalfH)
    {
        float halfWinW = Mathf.Max(0.24f, Mathf.Abs(Vector3.Dot(windowRectWorld.size, rightDir)) * 0.5f);
        float halfWinH = Mathf.Max(0.26f, windowRectWorld.size.y * 0.5f);
        Vector3 winCenter = windowRectWorld.center;

        int segs = BrokenGlassJagPattern.Length;
        Vector3[] outerRing = new Vector3[segs];
        Vector3[] innerHole = new Vector3[segs];
        Vector2[] outerUvs = new Vector2[segs];
        Vector2[] innerUvs = new Vector2[segs];

        for (int i = 0; i < segs; i++)
        {
            float ang = (i / (float)segs) * Mathf.PI * 2f;
            float dx = Mathf.Cos(ang);
            float dy = Mathf.Sin(ang);

            // Project ray onto outer rectangle edges of Window #6
            float scaleX = Mathf.Abs(dx) > 0.0001f ? halfWinW / Mathf.Abs(dx) : float.MaxValue;
            float scaleY = Mathf.Abs(dy) > 0.0001f ? halfWinH / Mathf.Abs(dy) : float.MaxValue;
            float rectT = Mathf.Min(scaleX, scaleY);

            outerRing[i] = winCenter + rightDir * (dx * rectT) + Vector3.up * (dy * rectT);

            float jag = BrokenGlassJagPattern[i];
            float ix = dx * holeHalfW * jag;
            float iy = dy * holeHalfH * jag;
            innerHole[i] = holeCenter + rightDir * ix + Vector3.up * iy;

            // True planar UV projection across Window #6 (2.50m x 1.35m) so cracks map directly onto the jagged 3D rim
            Vector3 outerRel = outerRing[i] - winCenter;
            Vector3 innerRel = innerHole[i] - winCenter;
            outerUvs[i] = new Vector2(
                0.5f + Vector3.Dot(outerRel, rightDir) / (halfWinW * 2f),
                0.5f + outerRel.y / (halfWinH * 2f));
            innerUvs[i] = new Vector2(
                0.5f + Vector3.Dot(innerRel, rightDir) / (halfWinW * 2f),
                0.5f + innerRel.y / (halfWinH * 2f));
        }

        GameObject crackedGlassGo = new GameObject("Act3_ShatteredWindowGlass");
        crackedGlassGo.transform.SetParent(parent, false);
        crackedGlassGo.transform.localPosition = Vector3.zero;
        crackedGlassGo.transform.localRotation = Quaternion.identity;

        List<Vector3> meshVerts = new List<Vector3>(segs * 2);
        List<Vector3> meshNormals = new List<Vector3>(segs * 2);
        List<Vector2> meshUvs = new List<Vector2>(segs * 2);
        List<int> meshTris = new List<int>(segs * 6);

        // Explicit inward-facing unit normal across the flat pane so URP lighting/shading never hits zero-length NaN normals
        Vector3 localGlassNormal = crackedGlassGo.transform.InverseTransformDirection(-outwardDir).normalized;
        if (localGlassNormal.sqrMagnitude < 0.001f) localGlassNormal = Vector3.back;

        for (int i = 0; i < segs; i++)
        {
            meshVerts.Add(crackedGlassGo.transform.InverseTransformPoint(outerRing[i]));
            meshNormals.Add(localGlassNormal);
            meshUvs.Add(outerUvs[i]);
        }
        for (int i = 0; i < segs; i++)
        {
            meshVerts.Add(crackedGlassGo.transform.InverseTransformPoint(innerHole[i]));
            meshNormals.Add(localGlassNormal);
            meshUvs.Add(innerUvs[i]);
        }

        for (int i = 0; i < segs; i++)
        {
            int next = (i + 1) % segs;
            int o0 = i;
            int o1 = next;
            int h0 = segs + i;
            int h1 = segs + next;

            // Single-sided winding with Cull Off shader avoids opposing-triangle normal cancellation (black NaN wedges)
            meshTris.Add(o0); meshTris.Add(h0); meshTris.Add(h1);
            meshTris.Add(o0); meshTris.Add(h1); meshTris.Add(o1);
        }

        Mesh shatteredMesh = new Mesh();
        shatteredMesh.name = "Act3_ShatteredWindowRing_Mesh";
        shatteredMesh.SetVertices(meshVerts);
        shatteredMesh.SetNormals(meshNormals);
        shatteredMesh.SetUVs(0, meshUvs);
        shatteredMesh.SetTriangles(meshTris, 0);
        shatteredMesh.RecalculateBounds();

        crackedGlassGo.AddComponent<MeshFilter>().sharedMesh = shatteredMesh;
        MeshRenderer mr = crackedGlassGo.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Vector3 holeRel = holeCenter - winCenter;
        Vector2 holeUvCenter = new Vector2(
            0.5f + Vector3.Dot(holeRel, rightDir) / (halfWinW * 2f),
            0.5f + holeRel.y / (halfWinH * 2f));
        float holeHalfU = holeHalfW / (halfWinW * 2f);
        float holeHalfV = holeHalfH / (halfWinH * 2f);

        Texture2D crackedTex = GenerateCrackedWindowRingTexture(512, holeUvCenter, holeHalfU, holeHalfV);

        // Use unlit alpha-blended shader with explicit zero exclusion for crystal-clear night transparency + crisp crack lines
        Shader crackShader = Shader.Find("Custom/RealisticHorrorRain") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material crackedMat = new Material(crackShader);
        crackedMat.name = "Act3_CrackedWindowGlass_MAT";
        crackedMat.mainTexture = crackedTex;
        if (crackedMat.HasProperty("_BaseMap")) crackedMat.SetTexture("_BaseMap", crackedTex);
        Color crackTint = new Color(0.86f, 0.91f, 0.97f, 0.85f);
        crackedMat.color = crackTint;
        if (crackedMat.HasProperty("_BaseColor")) crackedMat.SetColor("_BaseColor", crackTint);
        crackedMat.SetVector("_CabinExcludeMin", Vector4.zero);
        crackedMat.SetVector("_CabinLocalMin", Vector4.zero);
        crackedMat.renderQueue = 3015;
        mr.sharedMaterial = crackedMat;

        return innerHole;
    }

    private static void BuildHoleFractureRimRibbon(
        Transform parent,
        Vector3[] innerHoleWorld,
        Vector3 holeCenterWorld,
        Vector3 outwardDir,
        Material rimMat)
    {
        if (innerHoleWorld == null || innerHoleWorld.Length < 3) return;

        GameObject rimGo = new GameObject("Act3_GlassFractureEdgeRim");
        rimGo.transform.SetParent(parent, false);

        int count = innerHoleWorld.Length;
        Vector3 halfDepth = outwardDir * 0.0065f;
        List<Vector3> verts = new List<Vector3>(count * 2);
        List<Vector3> normals = new List<Vector3>(count * 2);
        List<int> tris = new List<int>(count * 6);

        for (int i = 0; i < count; i++)
        {
            Vector3 p = innerHoleWorld[i];
            verts.Add(rimGo.transform.InverseTransformPoint(p - halfDepth));
            verts.Add(rimGo.transform.InverseTransformPoint(p + halfDepth));

            Vector3 radialNormal = rimGo.transform.InverseTransformDirection((holeCenterWorld - p).normalized);
            if (radialNormal.sqrMagnitude < 0.001f) radialNormal = Vector3.up;
            normals.Add(radialNormal);
            normals.Add(radialNormal);
        }

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            int a0 = i * 2;
            int a1 = i * 2 + 1;
            int b0 = next * 2;
            int b1 = next * 2 + 1;

            tris.Add(a0); tris.Add(a1); tris.Add(b1);
            tris.Add(a0); tris.Add(b1); tris.Add(b0);
        }

        Mesh rimMesh = new Mesh();
        rimMesh.name = "Act3_GlassFractureEdgeRim_Mesh";
        rimMesh.SetVertices(verts);
        rimMesh.SetNormals(normals);
        rimMesh.SetTriangles(tris, 0);
        rimMesh.RecalculateBounds();

        rimGo.AddComponent<MeshFilter>().sharedMesh = rimMesh;
        MeshRenderer mr = rimGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = rimMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private static Texture2D GenerateCrackedWindowRingTexture(
        int size,
        Vector2 holeUvCenter,
        float holeHalfU,
        float holeHalfV)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        int segs = BrokenGlassJagPattern.Length;

        // 1. Glass pane base tint: nearly black-clear intact tint so night shows cleanly, with granular crushed-glass frost at the tear edge
        for (int y = 0; y < size; y++)
        {
            float v = y / (float)(size - 1);
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float nu = (u - holeUvCenter.x) / Mathf.Max(0.01f, holeHalfU);
                float nv = (v - holeUvCenter.y) / Mathf.Max(0.01f, holeHalfV);
                float ellipR = Mathf.Sqrt(nu * nu + nv * nv);

                float ang = Mathf.Atan2(nv, nu);
                if (ang < 0f) ang += Mathf.PI * 2f;
                float segFloat = (ang / (Mathf.PI * 2f)) * segs;
                int s0 = Mathf.FloorToInt(segFloat) % segs;
                int s1 = (s0 + 1) % segs;
                float jag = Mathf.Lerp(BrokenGlassJagPattern[s0], BrokenGlassJagPattern[s1], segFloat - Mathf.Floor(segFloat));

                float normFromRim = Mathf.Max(0f, ellipR / jag - 1f);
                float hash = Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f);

                // Crisp crushed-glass micro-shatter frost hugging the jagged hole perimeter, keeping outer panes 96% crystal clear
                float rimCrunch = Mathf.Exp(-normFromRim * 32f) * (0.58f + hash * 0.22f);
                float midFrost = Mathf.Exp(-normFromRim * 6.5f) * (0.11f + hash * 0.05f);
                float alpha = Mathf.Clamp01(0.035f + midFrost + rimCrunch);

                float frostFactor = Mathf.Clamp01(rimCrunch * 1.15f + midFrost * 0.65f);
                Color clearDarkTint = new Color(0.68f, 0.78f, 0.88f, alpha);
                Color crushedFrost = new Color(0.92f, 0.96f, 1.0f, alpha);
                pixels[y * size + x] = Color.Lerp(clearDarkTint, crushedFrost, frostFactor);
            }
        }

        // 2. Plot crisp branching spiderweb radial cracks + concentric stress web connecting neighboring rays
        Random.State oldState = Random.state;
        Random.InitState(8129);

        const int rayCount = 40;
        List<Vector2>[] rayPaths = new List<Vector2>[rayCount];

        for (int c = 0; c < rayCount; c++)
        {
            float baseAng = (c / (float)rayCount) * Mathf.PI * 2f + Random.Range(-0.045f, 0.045f);
            if (baseAng < 0f) baseAng += Mathf.PI * 2f;

            float segFloat = (baseAng / (Mathf.PI * 2f)) * segs;
            int s0 = Mathf.FloorToInt(segFloat) % segs;
            int s1 = (s0 + 1) % segs;
            float startJag = Mathf.Lerp(BrokenGlassJagPattern[s0], BrokenGlassJagPattern[s1], segFloat - Mathf.Floor(segFloat));

            float startR = startJag * 0.99f;
            float reachScale = Random.Range(1.38f, 3.65f);
            float curAng = baseAng;
            float curR = startR;
            int steps = 26;
            float stepR = (reachScale - startR) / steps;

            Vector2 prevUv = holeUvCenter + new Vector2(Mathf.Cos(curAng) * curR * holeHalfU, Mathf.Sin(curAng) * curR * holeHalfV);
            List<Vector2> path = new List<Vector2>(steps + 1) { prevUv };

            bool spawnedFork = false;
            for (int st = 1; st <= steps; st++)
            {
                curR += stepR;
                curAng += Random.Range(-0.06f, 0.06f);

                Vector2 nextUv = holeUvCenter + new Vector2(Mathf.Cos(curAng) * curR * holeHalfU, Mathf.Sin(curAng) * curR * holeHalfV);
                float t = st / (float)steps;
                float lineRadius = Mathf.Lerp(1.65f, 0.72f, t);
                float lineAlpha = Mathf.Lerp(0.95f, 0.56f, t);

                DrawCrackLineUv(pixels, size, prevUv, nextUv, lineRadius, lineAlpha);
                path.Add(nextUv);

                // Fork off sharp secondary Y-fractures partway along major rays
                if (!spawnedFork && st >= 4 && st <= 14 && Random.value < 0.52f)
                {
                    spawnedFork = true;
                    float branchAng = curAng + (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.24f, 0.52f);
                    Vector2 bp = nextUv;
                    float bLen = Random.Range(0.06f, 0.24f);
                    int bSteps = 9;
                    for (int bs = 1; bs <= bSteps; bs++)
                    {
                        branchAng += Random.Range(-0.08f, 0.08f);
                        Vector2 bn = bp + new Vector2(Mathf.Cos(branchAng), Mathf.Sin(branchAng)) * (bLen / bSteps);
                        float bt = bs / (float)bSteps;
                        DrawCrackLineUv(pixels, size, bp, bn, Mathf.Lerp(1.1f, 0.65f, bt), Mathf.Lerp(0.84f, 0.42f, bt));
                        bp = bn;
                    }
                }

                prevUv = nextUv;
            }

            rayPaths[c] = path;
        }

        // 3. Draw 5 broken concentric spiderweb stress rings bridging adjacent fracture rays
        int[] ringStepIndices = { 2, 5, 8, 12, 17 };
        for (int rIdx = 0; rIdx < ringStepIndices.Length; rIdx++)
        {
            int stepIndex = ringStepIndices[rIdx];
            float ringAlpha = Mathf.Lerp(0.86f, 0.48f, rIdx / 4f);
            float ringRadius = Mathf.Lerp(1.15f, 0.72f, rIdx / 4f);

            for (int c = 0; c < rayCount; c++)
            {
                if (Random.value < 0.24f) continue;

                int nextC = (c + 1) % rayCount;
                List<Vector2> pathA = rayPaths[c];
                List<Vector2> pathB = rayPaths[nextC];
                int ia = Mathf.Min(pathA.Count - 1, stepIndex + Random.Range(-1, 2));
                int ib = Mathf.Min(pathB.Count - 1, stepIndex + Random.Range(-1, 2));

                Vector2 pA = pathA[Mathf.Max(0, ia)];
                Vector2 pB = pathB[Mathf.Max(0, ib)];
                Vector2 mid = (pA + pB) * 0.5f + Random.insideUnitCircle * 0.004f;
                DrawCrackLineUv(pixels, size, pA, mid, ringRadius, ringAlpha);
                DrawCrackLineUv(pixels, size, mid, pB, ringRadius, ringAlpha);
            }
        }

        Random.state = oldState;

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static void DrawCrackLineUv(Color[] pixels, int size, Vector2 uvA, Vector2 uvB, float brushRadiusPx, float peakAlpha)
    {
        float ax = uvA.x * (size - 1);
        float ay = uvA.y * (size - 1);
        float bx = uvB.x * (size - 1);
        float by = uvB.y * (size - 1);

        int samples = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(new Vector2(ax, ay), new Vector2(bx, by)) * 1.6f));
        int span = Mathf.CeilToInt(brushRadiusPx + 1.2f);
        Color crackCore = new Color(0.96f, 0.98f, 1.0f, 1.0f);

        for (int s = 0; s <= samples; s++)
        {
            float t = s / (float)samples;
            float cx = Mathf.Lerp(ax, bx, t);
            float cy = Mathf.Lerp(ay, by, t);
            int ixCenter = Mathf.RoundToInt(cx);
            int iyCenter = Mathf.RoundToInt(cy);

            for (int oy = -span; oy <= span; oy++)
            {
                int py = iyCenter + oy;
                if (py < 0 || py >= size) continue;

                for (int ox = -span; ox <= span; ox++)
                {
                    int px = ixCenter + ox;
                    if (px < 0 || px >= size) continue;

                    float dist = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    if (dist > brushRadiusPx + 0.85f) continue;

                    float falloff = Mathf.Clamp01(1f - Mathf.Max(0f, dist - brushRadiusPx * 0.35f) / (brushRadiusPx + 0.65f));
                    float targetAlpha = peakAlpha * falloff;
                    int idx = py * size + px;
                    Color existing = pixels[idx];
                    if (targetAlpha > existing.a * 0.85f)
                    {
                        float blend = Mathf.Clamp01(falloff * 0.95f);
                        pixels[idx] = new Color(
                            Mathf.Lerp(existing.r, crackCore.r, blend),
                            Mathf.Lerp(existing.g, crackCore.g, blend),
                            Mathf.Lerp(existing.b, crackCore.b, blend),
                            Mathf.Max(existing.a, targetAlpha));
                    }
                }
            }
        }
    }

    private static Mesh[] CreateGlassShardVariantMeshes()
    {
        return new Mesh[]
        {
            BuildGlassShardPrismMesh("Act3_ShardVariant_Dagger", new Vector2[]
            {
                new Vector2(-0.46f, 0.0f),
                new Vector2(-0.18f, 0.56f),
                new Vector2( 0.04f, 1.0f),
                new Vector2( 0.24f, 0.44f),
                new Vector2( 0.48f, 0.0f)
            }),
            BuildGlassShardPrismMesh("Act3_ShardVariant_NotchedFang", new Vector2[]
            {
                new Vector2(-0.52f, 0.0f),
                new Vector2(-0.34f, 0.42f),
                new Vector2(-0.08f, 0.52f),
                new Vector2( 0.14f, 1.0f),
                new Vector2( 0.44f, 0.28f),
                new Vector2( 0.50f, 0.0f)
            }),
            BuildGlassShardPrismMesh("Act3_ShardVariant_SplitProng", new Vector2[]
            {
                new Vector2(-0.48f, 0.0f),
                new Vector2(-0.22f, 0.88f),
                new Vector2( 0.02f, 0.52f),
                new Vector2( 0.26f, 1.0f),
                new Vector2( 0.48f, 0.0f)
            }),
            BuildGlassShardPrismMesh("Act3_ShardVariant_TrapezoidBlade", new Vector2[]
            {
                new Vector2(-0.50f, 0.0f),
                new Vector2(-0.32f, 0.82f),
                new Vector2( 0.18f, 0.96f),
                new Vector2( 0.46f, 0.30f),
                new Vector2( 0.52f, 0.0f)
            })
        };
    }

    private static Mesh BuildGlassShardPrismMesh(string meshName, Vector2[] outlinePoints)
    {
        int n = outlinePoints.Length;
        Vector3[] v = new Vector3[n * 2];
        for (int i = 0; i < n; i++)
        {
            v[i] = new Vector3(outlinePoints[i].x, outlinePoints[i].y, 0.5f);
            v[n + i] = new Vector3(outlinePoints[i].x, outlinePoints[i].y, -0.5f);
        }

        List<int> t = new List<int>(n * 12);
        // Front face and back face fan from bottom-center base vertex 0
        for (int i = 1; i + 1 < n; i++)
        {
            t.Add(0); t.Add(i); t.Add(i + 1);
            t.Add(n); t.Add(n + i + 1); t.Add(n + i);
        }

        // 3D beveled perimeter rim linking front & back faces so shard thickness catches specular edge light
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            int f0 = i;
            int f1 = next;
            int b0 = n + i;
            int b1 = n + next;

            t.Add(f0); t.Add(b0); t.Add(b1);
            t.Add(f0); t.Add(b1); t.Add(f1);
        }

        Mesh m = new Mesh();
        m.name = meshName;
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static Mesh CreateTriangularGlassShardMesh()
    {
        return BuildGlassShardPrismMesh("Act3_TriangularGlassShard_Mesh", new Vector2[]
        {
            new Vector2(-0.50f, 0.0f),
            new Vector2( 0.08f, 1.0f),
            new Vector2( 0.50f, 0.0f)
        });
    }

    private static void SpawnGlassShatterBurst(Transform parent, Vector3 origin, Vector3 inwardDir, Material mat)
    {
        GameObject burstGo = new GameObject("Act3_GlassShatterBurst");
        burstGo.transform.SetParent(parent, true);
        burstGo.transform.position = origin;
        burstGo.transform.rotation = Quaternion.LookRotation(inwardDir, Vector3.up);

        ParticleSystem ps = burstGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.duration = 0.8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
        main.startRotation3D = true;
        main.gravityModifier = 1.6f;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 34) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 32f;
        sh.radius = 0.28f;

        ParticleSystemRenderer psr = burstGo.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Mesh;
        psr.mesh = CreateTriangularGlassShardMesh();
        psr.sharedMaterial = mat;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Play();
    }

    private Vector3 ResolveOpenFloorSpotBelowWindow(
        Transform buildingRoot,
        Vector3 windowCenter,
        Vector3 rightDir,
        Vector3 inwardDir,
        float cabinFloorY)
    {
        // Prefer the open living room floor clear in front/beside PFB_Sofa so the dead pigeon & shards are completely unobstructed
        Vector3 candidate = new Vector3(windowCenter.x, cabinFloorY + 0.015f, windowCenter.z) + inwardDir * 0.68f;

        GameObject sofaGo = GameObject.Find("PFB_Sofa");
        if (sofaGo != null)
        {
            Renderer sofaRen = sofaGo.GetComponentInChildren<Renderer>();
            if (sofaRen != null)
            {
                Bounds sb = sofaRen.bounds;
                Vector3 leftClear = new Vector3(sb.min.x - 0.38f, cabinFloorY + 0.015f, windowCenter.z) + inwardDir * 0.95f;
                Vector3 rightClear = new Vector3(sb.max.x + 0.38f, cabinFloorY + 0.015f, windowCenter.z) + inwardDir * 0.95f;
                Vector3 frontClear = new Vector3(windowCenter.x, cabinFloorY + 0.015f, sb.min.z - 0.42f);

                // Pick whichever open floor point is closest to the living room center side and outside the sofa bounds
                Vector3 roomCenter = buildingRoot != null
                    ? buildingRoot.TransformPoint(new Vector3(0.2f, 0.12f, 2.2f))
                    : candidate + inwardDir * 2.0f;

                Vector3[] candidates = { frontClear, leftClear, rightClear };
                float bestDist = float.MaxValue;
                for (int i = 0; i < candidates.Length; i++)
                {
                    Vector3 c = candidates[i];
                    if (c.x >= sb.min.x - 0.12f && c.x <= sb.max.x + 0.12f &&
                        c.z >= sb.min.z - 0.12f && c.z <= sb.max.z + 0.12f)
                    {
                        continue;
                    }
                    float d = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(roomCenter.x, roomCenter.z));
                    if (d < bestDist)
                    {
                        bestDist = d;
                        candidate = c;
                    }
                }
            }
        }

        return candidate;
    }

    private GameObject BuildDeadPigeonModel(Transform parent, Vector3 floorPos, Vector3 rightDir, Vector3 inwardDir)
    {
        GameObject pigeon = new GameObject("DeadPigeon_Model");
        pigeon.transform.SetParent(parent, true);
        pigeon.transform.position = floorPos + new Vector3(0f, 0.038f, 0f);
        pigeon.transform.rotation = Quaternion.LookRotation(rightDir * 0.85f + inwardDir * 0.52f, Vector3.up) * Quaternion.Euler(0f, 0f, 78f);

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material bodyMat = CreateLitMaterial(litShader, new Color(0.42f, 0.45f, 0.50f), 0.18f);
        Material wingMat = CreateLitMaterial(litShader, new Color(0.28f, 0.31f, 0.35f), 0.15f);
        Material wingBarMat = CreateLitMaterial(litShader, new Color(0.14f, 0.15f, 0.18f), 0.12f);
        Material neckMat = CreateLitMaterial(litShader, new Color(0.24f, 0.38f, 0.34f), 0.35f);
        Material beakMat = CreateLitMaterial(litShader, new Color(0.78f, 0.68f, 0.42f), 0.25f);
        Material footMat = CreateLitMaterial(litShader, new Color(0.68f, 0.34f, 0.36f), 0.20f);

        // 1. Plump pigeon torso
        CreatePrimitivePart(pigeon.transform, PrimitiveType.Sphere, "Torso",
            Vector3.zero, Vector3.zero, new Vector3(0.13f, 0.11f, 0.21f), bodyMat);

        // 2. Breast / crop
        CreatePrimitivePart(pigeon.transform, PrimitiveType.Sphere, "Breast",
            new Vector3(0f, 0.015f, 0.075f), new Vector3(12f, 0f, 0f), new Vector3(0.115f, 0.10f, 0.12f), bodyMat);

        // 3. Iridescent neck collar (tilted limply)
        CreatePrimitivePart(pigeon.transform, PrimitiveType.Cylinder, "Neck",
            new Vector3(0.015f, 0.035f, 0.125f), new Vector3(68f, 22f, 15f), new Vector3(0.068f, 0.038f, 0.068f), neckMat);

        // 4. Head (resting sideways on floor)
        CreatePrimitivePart(pigeon.transform, PrimitiveType.Sphere, "Head",
            new Vector3(0.032f, 0.042f, 0.165f), new Vector3(10f, 28f, -35f), new Vector3(0.072f, 0.065f, 0.082f), wingMat);

        // 5. Beak + white cere
        CreatePrimitivePart(pigeon.transform, PrimitiveType.Cube, "Beak",
            new Vector3(0.052f, 0.038f, 0.208f), new Vector3(14f, 28f, -35f), new Vector3(0.018f, 0.016f, 0.042f), beakMat);

        // 6. Left wing (slightly splayed from impact)
        CreatePrimitivePart(pigeon.transform, PrimitiveType.Sphere, "Wing_L",
            new Vector3(-0.068f, 0.018f, -0.01f), new Vector3(8f, -24f, 18f), new Vector3(0.042f, 0.095f, 0.19f), wingMat);
        CreatePrimitivePart(pigeon.transform, PrimitiveType.Cube, "WingBar_L",
            new Vector3(-0.082f, 0.020f, -0.015f), new Vector3(8f, -24f, 18f), new Vector3(0.012f, 0.075f, 0.025f), wingBarMat);

        // 7. Right wing (folded against floor)
        CreatePrimitivePart(pigeon.transform, PrimitiveType.Sphere, "Wing_R",
            new Vector3(0.060f, -0.005f, -0.01f), new Vector3(-4f, 12f, -10f), new Vector3(0.038f, 0.085f, 0.18f), wingMat);

        // 8. Tail fan feathers
        CreatePrimitivePart(pigeon.transform, PrimitiveType.Cube, "Tail",
            new Vector3(0f, -0.008f, -0.145f), new Vector3(-8f, 6f, 4f), new Vector3(0.075f, 0.016f, 0.13f), wingBarMat);

        // 9. Curled lifeless feet
        CreatePrimitivePart(pigeon.transform, PrimitiveType.Cylinder, "Leg_L",
            new Vector3(-0.032f, -0.055f, -0.02f), new Vector3(25f, 0f, -48f), new Vector3(0.014f, 0.035f, 0.014f), footMat);
        CreatePrimitivePart(pigeon.transform, PrimitiveType.Cylinder, "Leg_R",
            new Vector3(0.018f, -0.062f, -0.03f), new Vector3(-18f, 0f, -62f), new Vector3(0.014f, 0.035f, 0.014f), footMat);

        return pigeon;
    }

    private void EnsureBathroomSinkFaceWash(Transform buildingRoot)
    {
        if (bathroomSinkFaceWash != null) return;

        Transform sinkTr = null;
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.scene == gameObject.scene &&
                string.Equals(all[i].name, "PFB_BathroomSink", System.StringComparison.OrdinalIgnoreCase))
            {
                sinkTr = all[i];
                break;
            }
        }

        if (sinkTr != null)
        {
            bathroomSinkFaceWash = sinkTr.GetComponent<CabinBathroomSinkFaceWash>();
            if (bathroomSinkFaceWash == null)
            {
                bathroomSinkFaceWash = sinkTr.gameObject.AddComponent<CabinBathroomSinkFaceWash>();
            }
        }
    }

    private void EnsureCandleInLivingRoom(Transform buildingRoot)
    {
        if (candleInteractable != null) return;

        Transform tvStandTr = null;
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.scene == gameObject.scene &&
                string.Equals(all[i].name, "PFB_TVStand", System.StringComparison.OrdinalIgnoreCase))
            {
                tvStandTr = all[i];
                break;
            }
        }

        GameObject candleGo = new GameObject("Act3_LivingRoom_Candle");
        if (tvStandTr != null)
        {
            candleGo.transform.SetParent(tvStandTr, false);
            // Laying down on the center open shelf between grp_doorsL (x=-0.862) and grp_doorsR (x=+0.887)
            candleGo.transform.localPosition = new Vector3(0.0f, 0.354f, 0.06f);
            candleGo.transform.localRotation = Quaternion.Euler(0f, 24f, 0f);
        }
        else if (buildingRoot != null)
        {
            candleGo.transform.SetParent(buildingRoot, false);
            candleGo.transform.localPosition = new Vector3(3.185f, 0.466f, 1.234f);
            candleGo.transform.localRotation = Quaternion.Euler(0f, 24f, 0f);
        }

        candleInteractable = candleGo.AddComponent<CabinCandleInteractable>();
        candleInteractable.BuildCandleVisuals();
    }

    private void EnsurePhoneInBedroom(Transform buildingRoot)
    {
        if (phoneInteractable != null) return;

        Transform bedsideTr = null;
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.scene == gameObject.scene &&
                string.Equals(all[i].name, "PFB_BedsideTable", System.StringComparison.OrdinalIgnoreCase))
            {
                bedsideTr = all[i];
                break;
            }
        }

        GameObject phoneGo = new GameObject("Act3_Bedroom_Phone");
        if (bedsideTr != null)
        {
            phoneGo.transform.SetParent(bedsideTr, false);
            // Flat on top surface of PFB_BedsideTable (top Y = 0.502)
            phoneGo.transform.localPosition = new Vector3(0.02f, 0.514f, 0.03f);
            phoneGo.transform.localRotation = Quaternion.Euler(0f, -26f, 0f);
        }
        else if (buildingRoot != null)
        {
            phoneGo.transform.SetParent(buildingRoot, false);
            phoneGo.transform.localPosition = new Vector3(4.34f, 0.628f, -9.08f);
            phoneGo.transform.localRotation = Quaternion.Euler(0f, -26f, 0f);
        }

        phoneInteractable = phoneGo.AddComponent<CabinPhoneInteractable>();
        phoneInteractable.BuildPhoneVisuals();
    }

    private void SetupAudioSources()
    {
        GameObject doorGo = new GameObject("Act3_DoorKnockAudio");
        doorGo.transform.SetParent(transform, false);
        doorAudioSource = doorGo.AddComponent<AudioSource>();
        doorAudioSource.spatialBlend = 0.85f;
        doorAudioSource.minDistance = 1.5f;
        doorAudioSource.maxDistance = 22.0f;
        doorAudioSource.rolloffMode = AudioRolloffMode.Linear;
        doorAudioSource.playOnAwake = false;

        GameObject winGo = new GameObject("Act3_WindowBreakAudio");
        winGo.transform.SetParent(transform, false);
        windowAudioSource = winGo.AddComponent<AudioSource>();
        windowAudioSource.spatialBlend = 0.75f;
        windowAudioSource.minDistance = 2.0f;
        windowAudioSource.maxDistance = 25.0f;
        windowAudioSource.rolloffMode = AudioRolloffMode.Linear;
        windowAudioSource.playOnAwake = false;

        GameObject outGo = new GameObject("Act3_PowerOutageAudio");
        outGo.transform.SetParent(transform, false);
        powerOutageAudioSource = outGo.AddComponent<AudioSource>();
        powerOutageAudioSource.spatialBlend = 0.25f;
        powerOutageAudioSource.playOnAwake = false;
    }

    private void GenerateStoryAudioClips()
    {
        int sr = 44100;

        // 1. Heavy wooden front door triple knock (knock... knock... knock...)
        int knockSamples = Mathf.RoundToInt(1.15f * sr);
        float[] knockData = new float[knockSamples];
        float[] hitTimes = { 0.04f, 0.34f, 0.66f };
        for (int i = 0; i < knockSamples; i++)
        {
            float t = (float)i / sr;
            float s = 0f;
            for (int k = 0; k < hitTimes.Length; k++)
            {
                float dt = t - hitTimes[k];
                if (dt >= 0f && dt < 0.25f)
                {
                    float thud = Mathf.Sin(2f * Mathf.PI * (128f - dt * 210f) * dt) * Mathf.Exp(-dt * 28f) * 0.78f;
                    float woodRes = Mathf.Sin(2f * Mathf.PI * 265f * dt) * Mathf.Exp(-dt * 42f) * 0.42f;
                    float knuckle = (Random.value * 2f - 1f) * Mathf.Exp(-dt * 95f) * 0.38f;
                    s += thud + woodRes + knuckle;
                }
            }
            knockData[i] = Mathf.Clamp(s, -1f, 1f);
        }
        doorKnockClip = AudioClip.Create("Act3_DoorKnock", knockSamples, 1, sr, false);
        doorKnockClip.SetData(knockData, 0);

        // 2. Sudden power outage breaker trip + power-down hum
        int outSamples = Mathf.RoundToInt(1.25f * sr);
        float[] outData = new float[outSamples];
        for (int i = 0; i < outSamples; i++)
        {
            float t = (float)i / sr;
            float snap = (Random.value * 2f - 1f) * Mathf.Exp(-t * 48f) * 0.65f;
            float dropFreq = Mathf.Lerp(145f, 32f, Mathf.Clamp01(t / 0.85f));
            float humDrop = Mathf.Sin(2f * Mathf.PI * dropFreq * t) * Mathf.Exp(-t * 3.2f) * 0.58f;
            float relayThud = Mathf.Sin(2f * Mathf.PI * 92f * t) * Mathf.Exp(-t * 22f) * 0.50f;
            outData[i] = Mathf.Clamp(snap + humDrop + relayThud, -1f, 1f);
        }
        powerOutageClip = AudioClip.Create("Act3_PowerOutage", outSamples, 1, sr, false);
        powerOutageClip.SetData(outData, 0);

        // 3. Realistic window glass shatter + falling shards tinkling on wood floor
        int glassSamples = Mathf.RoundToInt(1.65f * sr);
        float[] glassData = new float[glassSamples];
        float[] shardTimes = { 0.0f, 0.06f, 0.14f, 0.24f, 0.33f, 0.45f, 0.58f, 0.72f, 0.89f, 1.08f, 1.26f };
        float[] shardFreqs = { 2150f, 3100f, 2780f, 3850f, 4420f, 3360f, 4890f, 3620f, 5120f, 4180f, 4650f };
        for (int i = 0; i < glassSamples; i++)
        {
            float t = (float)i / sr;
            // Initial impact thump + glass burst
            float impact = Mathf.Sin(2f * Mathf.PI * 155f * t) * Mathf.Exp(-t * 18f) * 0.75f;
            float burst = (Random.value * 2f - 1f) * Mathf.Exp(-t * 9.5f) * 0.82f;
            float rings = 0f;
            for (int s = 0; s < shardTimes.Length; s++)
            {
                float dt = t - shardTimes[s];
                if (dt >= 0f && dt < 0.32f)
                {
                    float f = shardFreqs[s];
                    float chime = (Mathf.Sin(2f * Mathf.PI * f * dt) + 0.5f * Mathf.Sin(2f * Mathf.PI * (f * 1.41f) * dt))
                                  * Mathf.Exp(-dt * 24f) * 0.24f;
                    rings += chime;
                }
            }
            glassData[i] = Mathf.Clamp(impact + burst + rings, -1f, 1f);
        }
        glassBreakClip = AudioClip.Create("Act3_GlassShatter", glassSamples, 1, sr, false);
        glassBreakClip.SetData(glassData, 0);

        // 4. Short eerie digital static / signal glitch burst when the monster vanishes outside the window
        int glitchSamples = Mathf.RoundToInt(0.74f * sr);
        float[] glitchData = new float[glitchSamples];
        float glitchSampleHold = 0f;
        int holdCounter = 0;
        for (int i = 0; i < glitchSamples; i++)
        {
            float t = (float)i / sr;
            if (holdCounter <= 0)
            {
                glitchSampleHold = Random.value * 2f - 1f;
                holdCounter = Random.Range(6, 42);
            }
            holdCounter--;

            // Bursts of harsh digital crackle mixed with unstable low sub-buzz fading cleanly at tail
            float gate = Mathf.Sin(t * 58f) > -0.25f ? 1.0f : 0.22f;
            float env = Mathf.Clamp01(1f - t / 0.74f) * (0.55f + 0.45f * gate);
            float buzz = Mathf.Sin(2f * Mathf.PI * 112f * t + Mathf.Sin(t * 95f) * 3.2f) * 0.28f;
            float highScreech = Mathf.Sin(2f * Mathf.PI * 1680f * t) * (Random.value > 0.7f ? 0.16f : 0f);
            glitchData[i] = Mathf.Clamp((glitchSampleHold * 0.58f + buzz + highScreech) * env, -1f, 1f);
        }
        monsterGlitchClip = AudioClip.Create("Act3_MonsterGlitch", glitchSamples, 1, sr, false);
        monsterGlitchClip.SetData(glitchData, 0);
    }

    private static Bounds CalculateCombinedBounds(Renderer[] renderers, Vector3 fallbackCenter)
    {
        Bounds b = new Bounds(fallbackCenter, Vector3.zero);
        bool initialized = false;
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (!initialized)
                {
                    b = r.bounds;
                    initialized = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }
        }
        return b;
    }

    public static Material CreateLitMaterial(Shader shader, Color color, float smoothness)
    {
        Material m = new Material(shader != null ? shader : Shader.Find("Standard"));
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    public static GameObject CreatePrimitivePart(
        Transform parent,
        PrimitiveType type,
        string partName,
        Vector3 localPos,
        Vector3 localEuler,
        Vector3 localScale,
        Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = partName;
        go.transform.SetParent(parent, false);
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = localScale;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
        return go;
    }
}

/// <summary>
/// Drives a continuous eerie standing idle animation on monster_low / monster_lowqual
/// (psx_horror_creature armature: Bone = pelvis, Bone.001..003 = L arm, Bone.004..006 = R arm, Bone.007..008 = neck/head).
/// </summary>
public class Act3MonsterStandingAnimator : MonoBehaviour
{
    private Transform pelvisBone;
    private Transform neckBone;
    private Transform headBone;
    private Transform leftArmBone;
    private Transform rightArmBone;

    private Quaternion pelvisBaseRot;
    private Quaternion neckBaseRot;
    private Quaternion headBaseRot;
    private Quaternion leftArmBaseRot;
    private Quaternion rightArmBaseRot;
    private Vector3 pelvisBasePos;
    private bool initialized = false;

    public void InitializeBones()
    {
        if (initialized) return;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform t = children[i];
            if (t == null) continue;
            string n = t.name;
            if (n == "Bone") pelvisBone = t;
            else if (n == "Bone.007") neckBone = t;
            else if (n == "Bone.008") headBone = t;
            else if (n == "Bone.001") leftArmBone = t;
            else if (n == "Bone.004") rightArmBone = t;
        }

        if (pelvisBone != null)
        {
            pelvisBaseRot = pelvisBone.localRotation;
            pelvisBasePos = pelvisBone.localPosition;
        }
        if (neckBone != null) neckBaseRot = neckBone.localRotation;
        if (headBone != null) headBaseRot = headBone.localRotation;
        if (leftArmBone != null) leftArmBaseRot = leftArmBone.localRotation;
        if (rightArmBone != null) rightArmBaseRot = rightArmBone.localRotation;

        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized) InitializeBones();

        // Keep all bones strictly motionless in their standing pose (zero movement)
        if (pelvisBone != null)
        {
            pelvisBone.localPosition = pelvisBasePos;
            pelvisBone.localRotation = pelvisBaseRot;
        }
        if (neckBone != null)
        {
            neckBone.localRotation = neckBaseRot;
        }
        if (headBone != null)
        {
            headBone.localRotation = headBaseRot;
        }
        if (leftArmBone != null)
        {
            leftArmBone.localRotation = leftArmBaseRot;
        }
        if (rightArmBone != null)
        {
            rightArmBone.localRotation = rightArmBaseRot;
        }

        // Continuously face/look towards the player
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            Vector3 toPlayer = cam.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                // psx_horror_creature.fbx faces along local -X (Vector3.left), so Euler(0, 90, 0) turns local -X straight toward the player
                transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
            }
        }
    }
}

/// <summary>
/// Interactable trigger at the shattered Act 3 living room window ("E to Inspect").
/// Lets the player walk up and deliberately inspect the broken window before the monster appears and glitches away.
/// </summary>
public class CabinBrokenWindowInteractable : MonoBehaviour, IInteractable
{
    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsAct3InspectWindowTaskActive)
        {
            return "E to Inspect";
        }
        return "";
    }

    public void Interact()
    {
        if (!HouseSceneStoryController.IsAct3InspectWindowTaskActive || HouseSceneStoryController.Instance == null)
        {
            return;
        }
        HouseSceneStoryController.Instance.NotifyAct3BrokenWindowInspected();
    }
}

/// <summary>
/// Heavy realistic pouring rain and rolling thunder system for HouseScene and GasScene2.
/// Uses Custom/RealisticHorrorRain shader with hardware-level interior discard so it pours heavily
/// everywhere across the entire scene and outside every window without a single raindrop ever glitching indoors.
/// </summary>
public class Act3RainStormController : MonoBehaviour
{
    public static Act3RainStormController Instance { get; private set; }

    private ParticleSystem farStormRainPs;
    private ParticleSystem wideSceneRainPs;
    private ParticleSystem nearDenseRainPs;
    private ParticleSystem windGustRainPs;
    private ParticleSystem groundDropletSplatterPs;
    private ParticleSystem groundRippleRingPs;
    private readonly List<ParticleSystem> cabinWallRainSheets = new List<ParticleSystem>();

    private AudioSource rainLoopSource;
    private AudioSource thunderSource;
    private Light lightningLight;

    private AudioClip heavyRainLoopClip;
    private AudioClip[] thunderClips;
    private float nextThunderTime = 0f;

    private Material primaryStreakMat;
    private Material nearStreakMat;
    private Material fineStreakMat;
    private Material windowRivuletMat;
    private Material groundSplatterMat;
    private Material groundRippleMat;

    private Transform buildingBaseTransform;
    private Vector3 cabinWorldCenter = new Vector3(-11.5f, 12.5f, 305.5f);
    private float cabinGroundY = 11.6f;
    private float cabinRoofY = 15.6f;
    private Bounds cabinWorldBounds;
    private bool hasCabinBounds = false;

    private bool hasLocalCabinBounds = false;
    private Bounds cabinLocalBounds;
    private bool hasGasStoreBounds = false;
    private float gasStoreMinX = -2.32f;
    private float gasStoreMaxX = 6.50f;
    private float gasStoreMinZ = -9.80f;
    private float gasStoreMaxZ = 7.60f;
    private float gasStoreMaxY = 25.0f;

    private bool hasCanopyBounds = false;
    private float canopyMinX = -23.30f;
    private float canopyMaxX = -5.20f;
    private float canopyMinZ = -8.65f;
    private float canopyMaxZ = 5.45f;
    private float canopyRoofTopY = 5.50f;

    private struct PovRainDrop
    {
        public float normX;
        public float normY;
        public float baseSizeFraction;
        public float aspectStretch;
        public float age;
        public float lifetime;
        public float slideSpeed;
        public float wanderPhase;
        public float wanderAmp;
        public float maxAlpha;
        public bool leaveTrail;
    }

    private float rainExposureBlend = 0f;
    private float exposureLookUpFactor = 0f;
    private float nextPovDropSpawnTime = 0f;
    private readonly List<PovRainDrop> activePovDrops = new List<PovRainDrop>(16);
    private readonly RaycastHit[] overheadHitsBuffer = new RaycastHit[16];
    private Texture2D povSingleDropTex;
    private Texture2D povDropTrailTex;

    public static Act3RainStormController EnsureActive(Transform buildingRoot)
    {
        if (Instance != null)
        {
            Instance.gameObject.SetActive(true);
            Instance.RefreshCabinExclusionBounds();
            return Instance;
        }

        Act3RainStormController existing = Object.FindAnyObjectByType<Act3RainStormController>();
        if (existing != null)
        {
            Instance = existing;
            existing.gameObject.SetActive(true);
            existing.RefreshCabinExclusionBounds();
            return existing;
        }

        GameObject go = new GameObject("Act3_HeavyRainStorm");
        Instance = go.AddComponent<Act3RainStormController>();
        Instance.SetupStorm(buildingRoot);
        return Instance;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (RetroHorrorPOV.Instance != null) RetroHorrorPOV.Instance.SetRainEyeBlur(0f);
        if (povSingleDropTex != null) Destroy(povSingleDropTex);
        if (povDropTrailTex != null) Destroy(povDropTrailTex);
    }

    public void SetupStorm(Transform buildingRoot)
    {
        CreateRainMaterials();
        EnsurePovRainTextures();
        RefreshCabinExclusionBounds();
        BuildFullSceneRainEmitters();
        AttachWindowPaneRainRivulets();
        SetupStormAudio();
        SetupLightningLight();
        nextThunderTime = Time.time + Random.Range(5.5f, 10.5f);
    }

    private void Update()
    {
        // Keep the wide full-scene rain emitters centered around the player / camera so heavy rain pours everywhere
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            Vector3 p = cam.transform.position;
            bool insideCabinXZ = hasCabinBounds &&
                p.x >= cabinWorldBounds.min.x && p.x <= cabinWorldBounds.max.x &&
                p.z >= cabinWorldBounds.min.z && p.z <= cabinWorldBounds.max.z;

            float groundRefY = cabinGroundY;
            if (!insideCabinXZ)
            {
                groundRefY = p.y - 1.65f;
            }

            if (farStormRainPs != null)
            {
                farStormRainPs.transform.position = new Vector3(p.x, groundRefY + 14.5f, p.z);
            }
            if (wideSceneRainPs != null)
            {
                wideSceneRainPs.transform.position = new Vector3(p.x, groundRefY + 11.2f, p.z);
            }
            if (nearDenseRainPs != null)
            {
                nearDenseRainPs.transform.position = new Vector3(p.x, groundRefY + 9.4f, p.z);
            }
            if (windGustRainPs != null)
            {
                windGustRainPs.transform.position = new Vector3(p.x, groundRefY + 11.8f, p.z);
            }

            float groundSurfaceY = ResolveGroundSurfaceY(p, insideCabinXZ, groundRefY);
            if (groundDropletSplatterPs != null)
            {
                groundDropletSplatterPs.transform.position = new Vector3(p.x, groundSurfaceY + 0.03f, p.z);
            }
            if (groundRippleRingPs != null)
            {
                groundRippleRingPs.transform.position = new Vector3(p.x, groundSurfaceY + 0.015f, p.z);
            }

            // Update smooth first-person wet-eye / random lens raindrop exposure
            float targetExposure = EvaluateTargetRainExposure(cam, out float targetLookUp);
            if (targetExposure > 0.5f)
            {
                float fillRate = 0.42f + targetLookUp * 0.40f;
                rainExposureBlend = Mathf.MoveTowards(rainExposureBlend, 1.0f, Time.deltaTime * fillRate);
                exposureLookUpFactor = Mathf.MoveTowards(exposureLookUpFactor, targetLookUp, Time.deltaTime * 2.4f);
            }
            else
            {
                rainExposureBlend = Mathf.MoveTowards(rainExposureBlend, 0.0f, Time.deltaTime * 0.72f);
                exposureLookUpFactor = Mathf.MoveTowards(exposureLookUpFactor, 0.0f, Time.deltaTime * 2.2f);
            }

            UpdatePovRainDropletPool(targetExposure > 0.5f, targetLookUp);

            if (RetroHorrorPOV.Instance != null)
            {
                float blinkRecover = 0.92f + 0.08f * Mathf.Sin(Time.time * 1.45f);
                float blurLevel = rainExposureBlend > 0.01f
                    ? Mathf.Clamp01(rainExposureBlend * (0.48f + exposureLookUpFactor * 0.34f) * blinkRecover)
                    : 0f;
                RetroHorrorPOV.Instance.SetRainEyeBlur(blurLevel);
            }

            if (rainLoopSource != null)
            {
                float shelterAtten = targetExposure > 0.5f ? 0.78f : 0.48f;
                rainLoopSource.volume = Mathf.MoveTowards(
                    rainLoopSource.volume,
                    Mathf.Lerp(shelterAtten, 0.82f, rainExposureBlend),
                    Time.deltaTime * 0.9f);
            }
        }

        // Scroll delicate wet windowpane rain rivulets downward smoothly
        if (windowRivuletMat != null)
        {
            float scrollY = Time.time * 0.46f;
            float scrollX = Mathf.Sin(Time.time * 0.6f) * 0.015f;
            windowRivuletMat.mainTextureOffset = new Vector2(scrollX, scrollY);
            if (windowRivuletMat.HasProperty("_BaseMap"))
            {
                windowRivuletMat.SetTextureOffset("_BaseMap", new Vector2(scrollX, scrollY));
            }
        }

        if (buildingBaseTransform != null)
        {
            Matrix4x4 w2l = buildingBaseTransform.worldToLocalMatrix;
            ApplyMatrixToRainMaterials(w2l);
        }

        if (Time.time >= nextThunderTime)
        {
            nextThunderTime = Time.time + Random.Range(11.0f, 19.5f);
            StartCoroutine(LightningAndThunderRoutine());
        }
    }

    public void TriggerImmediateLightningAndThunder()
    {
        nextThunderTime = Time.time + Random.Range(12.0f, 20.0f);
        StartCoroutine(LightningAndThunderRoutine());
    }

    public Coroutine TriggerMonsterLightningReveal(HouseAct3PropController propCtrl)
    {
        nextThunderTime = Time.time + Random.Range(14.0f, 22.0f);
        return StartCoroutine(MonsterLightningRevealRoutine(propCtrl));
    }

    private IEnumerator MonsterLightningRevealRoutine(HouseAct3PropController propCtrl)
    {
        // Sync monster silhouette appearance strictly to the duration of a dramatic twin-bolt lightning strike
        Quaternion savedRot = lightningLight != null ? lightningLight.transform.rotation : Quaternion.Euler(38f, -135f, 0f);
        if (lightningLight != null)
        {
            lightningLight.transform.rotation = Quaternion.Euler(34f, -152f, 0f);
            lightningLight.color = new Color(0.76f, 0.86f, 1.00f);
            lightningLight.enabled = true;
            lightningLight.intensity = 3.2f;
        }

        if (propCtrl != null) propCtrl.SetMonsterVisible(true);

        if (thunderSource != null && thunderClips != null && thunderClips.Length > 0)
        {
            AudioClip clip = thunderClips[Random.Range(0, thunderClips.Length)];
            thunderSource.pitch = Random.Range(0.90f, 1.02f);
            thunderSource.PlayOneShot(clip, 0.96f);
        }

        // Bolt 1 peak holding silhouette clearly visible in stormy flash
        yield return new WaitForSeconds(0.28f);

        // Quick storm flicker dip
        if (lightningLight != null) lightningLight.intensity = 0.45f;
        yield return new WaitForSeconds(0.08f);

        // Bolt 2 flash still revealing the monster, then fades back into dark night
        if (lightningLight != null) lightningLight.intensity = 2.9f;
        yield return new WaitForSeconds(0.36f);

        float fadeDur = 0.16f;
        float t = 0f;
        while (t < fadeDur)
        {
            t += Time.deltaTime;
            if (lightningLight != null)
            {
                lightningLight.intensity = Mathf.Lerp(2.9f, 0f, Mathf.Clamp01(t / fadeDur));
            }
            yield return null;
        }

        if (lightningLight != null)
        {
            lightningLight.intensity = 0f;
            lightningLight.enabled = false;
            lightningLight.transform.rotation = savedRot;
        }

        // Monster vanishes immediately as thunder flash fades back to dark
        if (propCtrl != null) propCtrl.SetMonsterVisible(false);
    }

    private IEnumerator LightningAndThunderRoutine()
    {
        if (lightningLight != null)
        {
            lightningLight.enabled = true;
            lightningLight.intensity = 2.4f;
            yield return new WaitForSeconds(0.06f);
            lightningLight.intensity = 0.35f;
            yield return new WaitForSeconds(0.05f);
            lightningLight.intensity = 1.65f;
            yield return new WaitForSeconds(0.09f);
            lightningLight.enabled = false;
        }

        yield return new WaitForSeconds(Random.Range(0.25f, 0.75f));

        if (thunderSource != null && thunderClips != null && thunderClips.Length > 0)
        {
            AudioClip clip = thunderClips[Random.Range(0, thunderClips.Length)];
            thunderSource.pitch = Random.Range(0.86f, 1.06f);
            thunderSource.PlayOneShot(clip, Random.Range(0.78f, 0.98f));
        }
    }

    private void CreateRainMaterials()
    {
        Shader rainShader = Shader.Find("Custom/RealisticHorrorRain")
                            ?? Shader.Find("Particles/Standard Unlit")
                            ?? Shader.Find("Sprites/Default");

        // 1. High-resolution smooth tapered rain streak texture (64x256, Bilinear)
        Texture2D streakTex = GenerateRealisticRainStreakTexture(64, 256);

        primaryStreakMat = new Material(rainShader);
        primaryStreakMat.name = "RealisticRain_PrimaryStreak_MAT";
        primaryStreakMat.mainTexture = streakTex;
        primaryStreakMat.color = new Color(0.84f, 0.89f, 0.95f, 0.38f);
        ConfigureTransparentFallbackIfNeeded(primaryStreakMat);

        nearStreakMat = new Material(rainShader);
        nearStreakMat.name = "RealisticRain_NearStreak_MAT";
        nearStreakMat.mainTexture = streakTex;
        nearStreakMat.color = new Color(0.86f, 0.90f, 0.96f, 0.34f);
        ConfigureTransparentFallbackIfNeeded(nearStreakMat);

        fineStreakMat = new Material(rainShader);
        fineStreakMat.name = "RealisticRain_FineStreak_MAT";
        fineStreakMat.mainTexture = streakTex;
        fineStreakMat.color = new Color(0.78f, 0.84f, 0.92f, 0.28f);
        ConfigureTransparentFallbackIfNeeded(fineStreakMat);

        groundSplatterMat = new Material(rainShader);
        groundSplatterMat.name = "RealisticRain_GroundSplatter_MAT";
        groundSplatterMat.mainTexture = GenerateGroundRainSplatterTexture(64, 64);
        groundSplatterMat.color = new Color(0.86f, 0.92f, 0.98f, 0.52f);
        ConfigureTransparentFallbackIfNeeded(groundSplatterMat);

        groundRippleMat = new Material(rainShader);
        groundRippleMat.name = "RealisticRain_GroundRipple_MAT";
        groundRippleMat.mainTexture = GenerateGroundRippleTexture(64, 64);
        groundRippleMat.color = new Color(0.84f, 0.90f, 0.96f, 0.36f);
        ConfigureTransparentFallbackIfNeeded(groundRippleMat);
    }

    private float ResolveGroundSurfaceY(Vector3 camPos, bool insideCabinXZ, float fallbackGroundY)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(camPos.x, camPos.y + 0.5f, camPos.z),
            Vector3.down,
            8.0f,
            ~0,
            QueryTriggerInteraction.Ignore);

        float bestY = float.MinValue;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || col.GetComponentInParent<CharacterController>() != null) continue;
            float hitY = hits[i].point.y;
            if (hitY <= camPos.y - 0.1f && hitY > bestY)
            {
                bestY = hitY;
            }
        }

        if (bestY > float.MinValue)
        {
            return bestY;
        }

        if (string.Equals(gameObject.scene.name, "GasScene2", System.StringComparison.OrdinalIgnoreCase))
        {
            return cabinGroundY;
        }

        return (hasCabinBounds && insideCabinXZ) ? cabinGroundY : fallbackGroundY;
    }

    private static void ConfigureTransparentFallbackIfNeeded(Material mat)
    {
        if (mat == null) return;
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3020;
        }
    }

    private static Texture2D GenerateRealisticRainStreakTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        float halfW = (width - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            // Vertical profile: bright water-drop leading tip near bottom, long motion-blurred upper trail
            float vy = (float)y / (height - 1);
            float verticalEnv;
            if (vy < 0.14f)
            {
                verticalEnv = Mathf.SmoothStep(0f, 1f, vy / 0.14f);
            }
            else
            {
                float tailT = (vy - 0.14f) / 0.86f;
                verticalEnv = Mathf.Pow(1f - tailT, 1.45f);
            }

            // Slightly fuller needle core so drops remain crisp both through windows and at 15-40m distances
            float widthFactor = Mathf.Lerp(0.28f, 0.14f, vy);

            for (int x = 0; x < width; x++)
            {
                float nx = Mathf.Abs((x - halfW) / halfW);
                float horizEnv = Mathf.Exp(-(nx * nx) / (2f * widthFactor * widthFactor));
                if (nx > 0.72f)
                {
                    horizEnv *= Mathf.Clamp01((1f - nx) / 0.28f);
                }

                float alpha = Mathf.Clamp01(verticalEnv * horizEnv * 1.15f);
                float coreBright = Mathf.Lerp(0.84f, 1.0f, horizEnv * verticalEnv);
                pixels[y * width + x] = new Color(coreBright, coreBright * 0.99f, 1.0f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    public void RefreshCabinExclusionBounds()
    {
        GameObject buildingBase = CabinWindowUtility.FindBuildingBaseInScene(gameObject.scene);
        if (buildingBase != null)
        {
            buildingBaseTransform = buildingBase.transform;
            MeshFilter baseMf = buildingBase.GetComponent<MeshFilter>();
            Renderer baseRen = buildingBase.GetComponent<Renderer>();

            if (baseRen != null)
            {
                Bounds wb = baseRen.bounds;
                cabinWorldBounds = wb;
                hasCabinBounds = true;
                cabinWorldCenter = wb.center;
                cabinGroundY = wb.min.y;
                cabinRoofY = wb.max.y;

                // Exclude the ENTIRE cabin building bounds + 8cm outdoor safety margin from ground to sky
                // so zero rain or mist pixels can ever clip through walls, windows, curtains, or ceiling indoors!
                Vector4 worldMin = new Vector4(wb.min.x - 0.08f, wb.min.y - 5.0f, wb.min.z - 0.08f, 1.0f);
                Vector4 worldMax = new Vector4(wb.max.x + 0.08f, wb.max.y + 40.0f, wb.max.z + 0.08f, 1.0f);

                Vector4 localMin = Vector4.zero;
                Vector4 localMax = Vector4.zero;
                hasLocalCabinBounds = false;
                Mesh boundsMesh = HouseAct3PropController.GetReadableSourceMesh(buildingBaseTransform.gameObject)
                                  ?? (baseMf != null ? baseMf.sharedMesh : null);
                if (boundsMesh != null)
                {
                    Bounds mb = boundsMesh.bounds;
                    float padX = mb.size.x * 0.015f;
                    float padZ = mb.size.z * 0.015f;
                    localMin = new Vector4(mb.min.x - padX, mb.min.y - 5.0f, mb.min.z - padZ, 1.0f);
                    localMax = new Vector4(mb.max.x + padX, mb.max.y + 40.0f, mb.max.z + padZ, 1.0f);
                    hasLocalCabinBounds = true;
                    cabinLocalBounds = new Bounds();
                    cabinLocalBounds.SetMinMax(
                        new Vector3(localMin.x, localMin.y, localMin.z),
                        new Vector3(localMax.x, localMax.y, localMax.z));
                }

                hasGasStoreBounds = false;
                hasCanopyBounds = false;
                ApplyExclusionToRainMaterials(worldMin, worldMax, localMin, localMax, Vector4.zero, Vector4.zero, buildingBaseTransform.worldToLocalMatrix);
                return;
            }
        }

        hasCabinBounds = false;
        hasLocalCabinBounds = false;
        hasGasStoreBounds = false;
        hasCanopyBounds = false;

        // GasScene2 convenience store interior + fuel pump canopy exclusion
        string sceneName = !string.IsNullOrEmpty(gameObject.scene.name)
            ? gameObject.scene.name
            : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (string.Equals(sceneName, "GasScene2", System.StringComparison.OrdinalIgnoreCase))
        {
            cabinWorldCenter = new Vector3(1.75f, 1.5f, -1.0f);
            cabinGroundY = 0.0f;
            cabinRoofY = 4.5f;

            // Cover convenience store interior, stockroom, restroom, and front roof cornice up to -2.30m
            hasGasStoreBounds = true;
            gasStoreMinX = -2.32f;
            gasStoreMaxX = 6.50f;
            gasStoreMinZ = -9.80f;
            gasStoreMaxZ = 7.60f;
            gasStoreMaxY = 25.0f;
            Vector4 gasStoreMin = new Vector4(gasStoreMinX, -2.0f, gasStoreMinZ, 1.0f);
            Vector4 gasStoreMax = new Vector4(gasStoreMaxX, gasStoreMaxY, gasStoreMaxZ, 1.0f);

            // Cover the FULL outdoor fuel pump canopy roof fascia, lights & bay underneath
            // Start with verified GasScene2 canopy envelope, then refine/expand from live geometry, probe & lights
            float cMinX = -23.35f;
            float cMaxX = -5.15f;
            float cMinZ = -8.65f;
            float cMaxZ = 5.45f;
            float canopyCeilingY = 5.55f;

            GameObject stacja = GameObject.Find("stacja");

            // 1. Measure actual physical canopy roof slab via overhead collider raycasts (restricted to fuel bay side X <= -3.5)
            float hitMinX = float.MaxValue, hitMaxX = float.MinValue;
            float hitMinZ = float.MaxValue, hitMaxZ = float.MinValue;
            float hitMaxY = float.MinValue;
            int roofHitCount = 0;

            for (float rx = -25.5f; rx <= -3.6f; rx += 0.70f)
            {
                for (float rz = -10.0f; rz <= 7.0f; rz += 0.70f)
                {
                    if (Physics.Raycast(new Vector3(rx, 14.0f, rz), Vector3.down, out RaycastHit roofHit, 10.6f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Collider col = roofHit.collider;
                        if (col == null || col.GetComponentInParent<CharacterController>() != null) continue;
                        if (col.name.IndexOf("Beverage_Service_Counter", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                        bool isStationCollider = stacja == null || col.transform.IsChildOf(stacja.transform);
                        if (isStationCollider && roofHit.normal.y > 0.65f && roofHit.point.y >= 3.55f && roofHit.point.y <= 7.50f)
                        {
                            hitMinX = Mathf.Min(hitMinX, roofHit.point.x);
                            hitMaxX = Mathf.Max(hitMaxX, roofHit.point.x);
                            hitMinZ = Mathf.Min(hitMinZ, roofHit.point.z);
                            hitMaxZ = Mathf.Max(hitMaxZ, roofHit.point.z);
                            hitMaxY = Mathf.Max(hitMaxY, roofHit.point.y);
                            roofHitCount++;
                        }
                    }
                }
            }

            if (roofHitCount >= 4)
            {
                cMinX = Mathf.Min(cMinX, hitMinX - 0.45f);
                cMaxX = Mathf.Max(cMaxX, Mathf.Min(-3.4f, hitMaxX + 0.45f));
                cMinZ = Mathf.Min(cMinZ, hitMinZ - 0.45f);
                cMaxZ = Mathf.Max(cMaxZ, hitMaxZ + 0.45f);
                canopyCeilingY = Mathf.Max(canopyCeilingY, hitMaxY + 0.35f);
            }

            // 2. Also inspect stacja overhead canopy sub-renderers (strictly outside store wall X < -2.2)
            if (stacja != null)
            {
                Renderer[] stacjaRenderers = stacja.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < stacjaRenderers.Length; i++)
                {
                    Renderer r = stacjaRenderers[i];
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (r.name.IndexOf("Beverage_Service_Counter", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    Bounds rb = r.bounds;
                    if (rb.max.x < -2.2f && rb.min.y >= 2.6f && rb.max.y <= 8.0f && (rb.size.x >= 3.5f || rb.size.z >= 3.5f))
                    {
                        cMinX = Mathf.Min(cMinX, rb.min.x - 0.35f);
                        cMaxX = Mathf.Max(cMaxX, Mathf.Min(-3.4f, rb.max.x + 0.35f));
                        cMinZ = Mathf.Min(cMinZ, rb.min.z - 0.35f);
                        cMaxZ = Mathf.Max(cMaxZ, rb.max.z + 0.35f);
                        canopyCeilingY = Mathf.Max(canopyCeilingY, rb.max.y + 0.35f);
                    }
                }
            }

            // 3. Also factor in Canopy_Exterior_Reflection_Probe & Canopy_Ceiling_Lighting downlights
            GameObject probeGo = GameObject.Find("Canopy_Exterior_Reflection_Probe");
            ReflectionProbe probe = probeGo != null ? probeGo.GetComponent<ReflectionProbe>() : null;
            if (probe != null)
            {
                Bounds pb = probe.bounds;
                cMinX = Mathf.Min(cMinX, pb.min.x - 0.10f);
                cMaxX = Mathf.Max(cMaxX, Mathf.Min(-3.4f, pb.max.x + 0.10f));
                cMinZ = Mathf.Min(cMinZ, pb.min.z - 0.10f);
                cMaxZ = Mathf.Max(cMaxZ, pb.max.z + 0.10f);
                canopyCeilingY = Mathf.Max(canopyCeilingY, pb.max.y + 0.15f);
            }

            float lMinX = float.MaxValue, lMaxX = float.MinValue;
            float lMinZ = float.MaxValue, lMaxZ = float.MinValue;
            float lMaxY = float.MinValue;
            int foundLights = 0;

            GameObject canopyGroup = GameObject.Find("Canopy_Ceiling_Lighting");
            if (canopyGroup != null && canopyGroup.transform.childCount > 0)
            {
                for (int i = 0; i < canopyGroup.transform.childCount; i++)
                {
                    Transform child = canopyGroup.transform.GetChild(i);
                    if (child == null) continue;
                    Vector3 wp = child.position;
                    lMinX = Mathf.Min(lMinX, wp.x);
                    lMaxX = Mathf.Max(lMaxX, wp.x);
                    lMinZ = Mathf.Min(lMinZ, wp.z);
                    lMaxZ = Mathf.Max(lMaxZ, wp.z);
                    lMaxY = Mathf.Max(lMaxY, wp.y);
                    foundLights++;
                }
            }

            if (foundLights == 0 && stacja != null)
            {
                Transform[] stacjaChildren = stacja.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < stacjaChildren.Length; i++)
                {
                    Transform tr = stacjaChildren[i];
                    if (tr == null || tr.name.IndexOf("Beverage_Service_Counter", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (tr.name.StartsWith("Area", System.StringComparison.OrdinalIgnoreCase))
                    {
                        Vector3 wp = tr.position;
                        if (wp.x < -2.2f && wp.y > 3.4f)
                        {
                            lMinX = Mathf.Min(lMinX, wp.x);
                            lMaxX = Mathf.Max(lMaxX, wp.x);
                            lMinZ = Mathf.Min(lMinZ, wp.z);
                            lMaxZ = Mathf.Max(lMaxZ, wp.z);
                            lMaxY = Mathf.Max(lMaxY, wp.y);
                            foundLights++;
                        }
                    }
                }
            }

            if (foundLights > 0)
            {
                cMinX = Mathf.Min(cMinX, lMinX - 4.6f);
                cMaxX = Mathf.Max(cMaxX, Mathf.Min(-3.4f, lMaxX + 4.6f));
                cMinZ = Mathf.Min(cMinZ, lMinZ - 4.6f);
                cMaxZ = Mathf.Max(cMaxZ, lMaxZ + 4.6f);
                canopyCeilingY = Mathf.Max(canopyCeilingY, lMaxY + 1.60f);
            }

            hasCanopyBounds = true;
            canopyMinX = cMinX;
            canopyMaxX = cMaxX;
            canopyMinZ = cMinZ;
            canopyMaxZ = cMaxZ;
            canopyRoofTopY = canopyCeilingY;

            Vector4 canopyMin = new Vector4(cMinX, -2.0f, cMinZ, 1.0f);
            Vector4 canopyMax = new Vector4(cMaxX, canopyCeilingY, cMaxZ, 1.0f);
            ApplyExclusionToRainMaterials(gasStoreMin, gasStoreMax, Vector4.zero, Vector4.zero, canopyMin, canopyMax, Matrix4x4.identity);
        }
    }

    private void ApplyExclusionToRainMaterials(
        Vector4 worldMin,
        Vector4 worldMax,
        Vector4 localMin,
        Vector4 localMax,
        Vector4 canopyMin,
        Vector4 canopyMax,
        Matrix4x4 worldToLocal)
    {
        Material[] mats = { primaryStreakMat, nearStreakMat, fineStreakMat, groundSplatterMat, groundRippleMat };
        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null) continue;
            m.SetVector("_CabinExcludeMin", worldMin);
            m.SetVector("_CabinExcludeMax", worldMax);
            m.SetVector("_CabinLocalMin", localMin);
            m.SetVector("_CabinLocalMax", localMax);
            m.SetVector("_CanopyExcludeMin", canopyMin);
            m.SetVector("_CanopyExcludeMax", canopyMax);
            m.SetMatrix("_WorldToCabinMatrix", worldToLocal);
        }
    }

    private void ApplyMatrixToRainMaterials(Matrix4x4 worldToLocal)
    {
        Material[] mats = { primaryStreakMat, nearStreakMat, fineStreakMat, groundSplatterMat, groundRippleMat };
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] != null)
            {
                mats[i].SetMatrix("_WorldToCabinMatrix", worldToLocal);
            }
        }
    }

    private void BuildFullSceneRainEmitters()
    {
        // Tight ground-timed lifetimes eliminate wasted underground particles so every drop is visibly pouring above ground

        // 1. Near Torrential Pouring Curtain (20m x 20m around player / windows)
        nearDenseRainPs = CreateRainLayerEmitter(
            "RainLayer_NearTorrentialDownpour",
            new Vector3(cabinWorldCenter.x, cabinGroundY + 9.4f, cabinWorldCenter.z),
            new Vector3(20f, 1.2f, 20f),
            rateOverTime: 6500f,
            maxParticles: 3400,
            lifetime: 0.42f,
            minSize: 0.018f,
            maxSize: 0.032f,
            fallVelocity: new Vector3(-1.4f, -25.5f, -0.7f),
            lengthScale: 15.0f,
            velocityScale: 0.035f,
            mat: nearStreakMat);

        // 2. Dense Mid-Range Heavy Downpour (40m x 40m covering yard, porch, driveway, & treeline)
        wideSceneRainPs = CreateRainLayerEmitter(
            "RainLayer_MidHeavyDownpour",
            new Vector3(cabinWorldCenter.x, cabinGroundY + 11.2f, cabinWorldCenter.z),
            new Vector3(40f, 1.6f, 40f),
            rateOverTime: 9000f,
            maxParticles: 5200,
            lifetime: 0.46f,
            minSize: 0.025f,
            maxSize: 0.042f,
            fallVelocity: new Vector3(-1.8f, -27.5f, -0.9f),
            lengthScale: 16.5f,
            velocityScale: 0.038f,
            mat: primaryStreakMat);

        // 3. Fast Wind-Driven Storm Gust Sheets (54m x 54m angled sheets for dynamic downpour gusts)
        windGustRainPs = CreateRainLayerEmitter(
            "RainLayer_WindGustSheets",
            new Vector3(cabinWorldCenter.x, cabinGroundY + 11.8f, cabinWorldCenter.z),
            new Vector3(54f, 1.8f, 54f),
            rateOverTime: 6500f,
            maxParticles: 4000,
            lifetime: 0.48f,
            minSize: 0.020f,
            maxSize: 0.038f,
            fallVelocity: new Vector3(-3.4f, -29.5f, -1.5f),
            lengthScale: 17.5f,
            velocityScale: 0.040f,
            mat: fineStreakMat);

        // 4. Far Forest / Scene-Wide Heavy Storm Canopy (84m x 84m)
        farStormRainPs = CreateRainLayerEmitter(
            "RainLayer_FarStormCanopy",
            new Vector3(cabinWorldCenter.x, cabinGroundY + 14.5f, cabinWorldCenter.z),
            new Vector3(84f, 2.0f, 84f),
            rateOverTime: 6200f,
            maxParticles: 4500,
            lifetime: 0.62f,
            minSize: 0.038f,
            maxSize: 0.062f,
            fallVelocity: new Vector3(-2.1f, -26.5f, -1.0f),
            lengthScale: 18.0f,
            velocityScale: 0.040f,
            mat: fineStreakMat);

        // 5. Dedicated Cabin Exterior Window Walls + Roof-Eave Water Pour Sheets outside all 4 walls
        if (buildingBaseTransform != null)
        {
            Renderer baseRen = buildingBaseTransform.GetComponent<Renderer>();
            if (baseRen != null)
            {
                Bounds wb = baseRen.bounds;
                float topY = wb.max.y + 1.8f;
                float lenX = wb.size.x + 8.0f;
                float lenZ = wb.size.z + 8.0f;

                Vector3[] sheetPositions =
                {
                    new Vector3(wb.center.x, topY, wb.max.z + 1.65f),
                    new Vector3(wb.center.x, topY, wb.min.z - 1.65f),
                    new Vector3(wb.max.x + 1.65f, topY, wb.center.z),
                    new Vector3(wb.min.x - 1.65f, topY, wb.center.z)
                };
                Vector3[] sheetScales =
                {
                    new Vector3(lenX, 0.5f, 3.0f),
                    new Vector3(lenX, 0.5f, 3.0f),
                    new Vector3(3.0f, 0.5f, lenZ),
                    new Vector3(3.0f, 0.5f, lenZ)
                };

                for (int i = 0; i < sheetPositions.Length; i++)
                {
                    ParticleSystem wallSheet = CreateRainLayerEmitter(
                        "RainLayer_CabinExteriorWall_" + i,
                        sheetPositions[i],
                        sheetScales[i],
                        rateOverTime: 4200f,
                        maxParticles: 1600,
                        lifetime: 0.27f,
                        minSize: 0.017f,
                        maxSize: 0.029f,
                        fallVelocity: new Vector3(-0.7f, -24.5f, -0.4f),
                        lengthScale: 14.5f,
                        velocityScale: 0.034f,
                        mat: primaryStreakMat);
                    cabinWallRainSheets.Add(wallSheet);
                }

                // Roofline eave pour curtains falling right past the outer cabin roof edges
                AddPerimeterEaveDripSheets(
                    "RainLayer_CabinRoofEaveDrip_",
                    wb.center,
                    wb.size.x,
                    wb.size.z,
                    wb.max.y - 0.15f,
                    outwardMargin: 0.30f,
                    lifetime: 0.21f,
                    rateOverTime: 1100f);
            }
        }
        else if (hasCanopyBounds)
        {
            // Perimeter water sheets spilling cleanly off the 4 outer edges of the gas station fuel pump canopy roof
            Vector3 canopyCenter = new Vector3((canopyMinX + canopyMaxX) * 0.5f, canopyRoofTopY, (canopyMinZ + canopyMaxZ) * 0.5f);
            float eaveY = canopyRoofTopY - 0.15f;
            float eaveLifetime = Mathf.Clamp((eaveY + 0.30f) / 21.0f, 0.22f, 0.34f);
            AddPerimeterEaveDripSheets(
                "RainLayer_CanopyRoofEaveDrip_",
                canopyCenter,
                canopyMaxX - canopyMinX,
                canopyMaxZ - canopyMinZ,
                eaveY,
                outwardMargin: 0.30f,
                lifetime: eaveLifetime,
                rateOverTime: 1350f);

            // Storefront cornice eave rain sheet just outside the clear front windows & sliding entrance doors
            if (hasGasStoreBounds)
            {
                ParticleSystem storeFrontSheet = CreateRainLayerEmitter(
                    "RainLayer_StorefrontWindowPour",
                    new Vector3(gasStoreMinX - 0.14f, 3.25f, -3.40f),
                    new Vector3(0.24f, 0.05f, 10.2f),
                    rateOverTime: 1650f,
                    maxParticles: 520,
                    lifetime: 0.17f,
                    minSize: 0.015f,
                    maxSize: 0.025f,
                    fallVelocity: new Vector3(-0.25f, -21.0f, -0.15f),
                    lengthScale: 13.5f,
                    velocityScale: 0.032f,
                    mat: nearStreakMat);
                cabinWallRainSheets.Add(storeFrontSheet);
            }
        }

        // 6. Ground-level impact droplets & puddle ripples across wet outdoor surfaces
        CreateGroundRainDropletEmitters();
    }

    private void AddPerimeterEaveDripSheets(
        string namePrefix,
        Vector3 roofCenter,
        float widthX,
        float lengthZ,
        float eaveY,
        float outwardMargin,
        float lifetime,
        float rateOverTime)
    {
        float halfX = widthX * 0.5f + outwardMargin;
        float halfZ = lengthZ * 0.5f + outwardMargin;

        Vector3[] eavePositions =
        {
            new Vector3(roofCenter.x, eaveY, roofCenter.z + halfZ),
            new Vector3(roofCenter.x, eaveY, roofCenter.z - halfZ),
            new Vector3(roofCenter.x + halfX, eaveY, roofCenter.z),
            new Vector3(roofCenter.x - halfX, eaveY, roofCenter.z)
        };
        Vector3[] eaveScales =
        {
            new Vector3(widthX + 0.5f, 0.05f, 0.22f),
            new Vector3(widthX + 0.5f, 0.05f, 0.22f),
            new Vector3(0.22f, 0.05f, lengthZ + 0.5f),
            new Vector3(0.22f, 0.05f, lengthZ + 0.5f)
        };

        for (int i = 0; i < eavePositions.Length; i++)
        {
            ParticleSystem eaveSheet = CreateRainLayerEmitter(
                namePrefix + i,
                eavePositions[i],
                eaveScales[i],
                rateOverTime: rateOverTime,
                maxParticles: 450,
                lifetime: lifetime,
                minSize: 0.014f,
                maxSize: 0.022f,
                fallVelocity: new Vector3(-0.15f, -21.0f, -0.15f),
                lengthScale: 12.5f,
                velocityScale: 0.030f,
                mat: nearStreakMat);
            cabinWallRainSheets.Add(eaveSheet);
        }
    }

    private void CreateGroundRainDropletEmitters()
    {
        // 1. Tiny vertical water-drop splash crowns sitting directly on the wet ground
        GameObject splatterGo = new GameObject("RainLayer_GroundDropletSplatters");
        splatterGo.transform.SetParent(transform, false);
        splatterGo.transform.position = new Vector3(cabinWorldCenter.x, cabinGroundY + 0.03f, cabinWorldCenter.z);
        groundDropletSplatterPs = splatterGo.AddComponent<ParticleSystem>();

        var sMain = groundDropletSplatterPs.main;
        sMain.loop = true;
        sMain.playOnAwake = true;
        sMain.prewarm = true;
        sMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sMain.startSpeed = 0f;
        sMain.gravityModifier = 0f;
        sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.10f, 0.16f);
        sMain.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.085f);
        sMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.78f, 0.85f, 0.94f, 0.65f),
            new Color(0.95f, 0.98f, 1.00f, 1.00f));
        sMain.maxParticles = 900;

        var sEmission = groundDropletSplatterPs.emission;
        sEmission.rateOverTime = 2600f;

        var sShape = groundDropletSplatterPs.shape;
        sShape.shapeType = ParticleSystemShapeType.Box;
        sShape.scale = new Vector3(28f, 0.02f, 28f);
        sShape.rotation = Vector3.zero;

        var sColorOverLife = groundDropletSplatterPs.colorOverLifetime;
        sColorOverLife.enabled = true;
        Gradient splatterGrad = new Gradient();
        splatterGrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.25f),
                new GradientAlphaKey(0.0f, 1.0f)
            });
        sColorOverLife.color = new ParticleSystem.MinMaxGradient(splatterGrad);

        ParticleSystemRenderer sRenderer = splatterGo.GetComponent<ParticleSystemRenderer>();
        sRenderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
        sRenderer.sharedMaterial = groundSplatterMat;
        sRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sRenderer.receiveShadows = false;
        groundDropletSplatterPs.Play();

        // 2. Flat ground puddle ripple rings expanding horizontally along terrain / pavement
        GameObject rippleGo = new GameObject("RainLayer_GroundPuddleRipples");
        rippleGo.transform.SetParent(transform, false);
        rippleGo.transform.position = new Vector3(cabinWorldCenter.x, cabinGroundY + 0.015f, cabinWorldCenter.z);
        groundRippleRingPs = rippleGo.AddComponent<ParticleSystem>();

        var rMain = groundRippleRingPs.main;
        rMain.loop = true;
        rMain.playOnAwake = true;
        rMain.prewarm = true;
        rMain.simulationSpace = ParticleSystemSimulationSpace.World;
        rMain.startSpeed = 0f;
        rMain.gravityModifier = 0f;
        rMain.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.32f);
        rMain.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
        rMain.startColor = new Color(0.84f, 0.91f, 0.98f, 1.0f);
        rMain.maxParticles = 700;

        var rEmission = groundRippleRingPs.emission;
        rEmission.rateOverTime = 1800f;

        var rShape = groundRippleRingPs.shape;
        rShape.shapeType = ParticleSystemShapeType.Box;
        rShape.scale = new Vector3(28f, 0.01f, 28f);
        rShape.rotation = Vector3.zero;

        var rSizeOverLife = groundRippleRingPs.sizeOverLifetime;
        rSizeOverLife.enabled = true;
        rSizeOverLife.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0f, 0.35f, 1f, 1.0f));

        var rColorOverLife = groundRippleRingPs.colorOverLifetime;
        rColorOverLife.enabled = true;
        Gradient rippleGrad = new Gradient();
        rippleGrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.85f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
        rColorOverLife.color = new ParticleSystem.MinMaxGradient(rippleGrad);

        ParticleSystemRenderer rRenderer = rippleGo.GetComponent<ParticleSystemRenderer>();
        rRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        rRenderer.sharedMaterial = groundRippleMat;
        rRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rRenderer.receiveShadows = false;
        groundRippleRingPs.Play();
    }

    private static Texture2D GenerateGroundRainSplatterTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        // Crisp 3-prong splash crown rising from bottom-center plus 4 tiny rebounding micro-droplets
        Vector2 origin = new Vector2(width * 0.5f, height * 0.14f);
        float[] prongAngles = { 62f, 90f, 118f };
        float[] prongLengths = { height * 0.44f, height * 0.56f, height * 0.44f };

        for (int p = 0; p < prongAngles.Length; p++)
        {
            float rad = prongAngles[p] * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float len = prongLengths[p];
            int steps = 28;
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                Vector2 pt = origin + dir * (t * len);
                float radiusPx = Mathf.Lerp(2.1f, 0.85f, t);
                float strength = Mathf.Sin(t * Mathf.PI) * 0.92f + (1f - t) * 0.25f;
                PaintSoftDropletStamp(pixels, width, height, pt.x, pt.y, radiusPx, strength);
            }
        }

        // Tiny rebounding spherical droplets near the crown tips
        Vector2[] microDrops =
        {
            new Vector2(width * 0.28f, height * 0.62f),
            new Vector2(width * 0.72f, height * 0.62f),
            new Vector2(width * 0.39f, height * 0.76f),
            new Vector2(width * 0.61f, height * 0.76f)
        };
        for (int d = 0; d < microDrops.Length; d++)
        {
            PaintSoftDropletStamp(pixels, width, height, microDrops[d].x, microDrops[d].y, 1.9f, 0.95f);
        }

        // Fade cleanly near texture borders to guarantee zero rectangular quad clipping
        for (int y = 0; y < height; y++)
        {
            float vy = y / (float)(height - 1);
            float edgeY = Mathf.Clamp01(Mathf.Min(vy, 1f - vy) / 0.12f);
            for (int x = 0; x < width; x++)
            {
                float vx = x / (float)(width - 1);
                float edgeX = Mathf.Clamp01(Mathf.Min(vx, 1f - vx) / 0.12f);
                int idx = y * width + x;
                Color c = pixels[idx];
                c.a *= edgeX * edgeY;
                pixels[idx] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateGroundRippleTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        float halfW = (width - 1) * 0.5f;
        float halfH = (height - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            float dy = (y - halfH) / halfH;
            for (int x = 0; x < width; x++)
            {
                float dx = (x - halfW) / halfW;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                // Outer water impact ring (r in 0.48..0.82) + tiny central droplet bead (r < 0.14)
                float outerRing = Mathf.Exp(-Mathf.Pow((r - 0.65f) / 0.095f, 2f)) * 0.85f;
                float centerDot = r < 0.18f ? Mathf.Clamp01(1f - r / 0.14f) * 0.72f : 0f;
                float edgeFade = Mathf.Clamp01((0.96f - r) / 0.14f);
                float alpha = Mathf.Clamp01(Mathf.Max(outerRing, centerDot) * edgeFade);

                pixels[y * width + x] = new Color(0.88f, 0.94f, 1.00f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static void PaintSoftDropletStamp(Color[] pixels, int w, int h, float cx, float cy, float radiusPx, float peakAlpha)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radiusPx - 1f));
        int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(cx + radiusPx + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radiusPx - 1f));
        int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(cy + radiusPx + 1f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (dist > radiusPx + 0.8f) continue;

                float a = Mathf.Clamp01(1f - dist / (radiusPx + 0.8f)) * peakAlpha;
                int idx = y * w + x;
                pixels[idx] = new Color(0.90f, 0.95f, 1.00f, Mathf.Max(pixels[idx].a, a));
            }
        }
    }

    private ParticleSystem CreateRainLayerEmitter(
        string emitterName,
        Vector3 worldPos,
        Vector3 boxScale,
        float rateOverTime,
        int maxParticles,
        float lifetime,
        float minSize,
        float maxSize,
        Vector3 fallVelocity,
        float lengthScale,
        float velocityScale,
        Material mat)
    {
        GameObject go = new GameObject(emitterName);
        go.transform.SetParent(transform, false);
        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.identity;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.92f, lifetime * 1.08f);
        main.startSpeed = 0f; // Velocity is controlled strictly in World Space via velocityOverLifetime
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.76f, 0.82f, 0.90f, 0.62f),
            new Color(0.95f, 0.98f, 1.00f, 1.00f));
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = rateOverTime;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxScale;
        shape.rotation = Vector3.zero;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(fallVelocity.x - 0.45f, fallVelocity.x + 0.45f);
        vel.y = new ParticleSystem.MinMaxCurve(fallVelocity.y - 2.4f, fallVelocity.y + 2.4f);
        vel.z = new ParticleSystem.MinMaxCurve(fallVelocity.z - 0.45f, fallVelocity.z + 0.45f);

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Stretch;
        psr.velocityScale = velocityScale;
        psr.lengthScale = lengthScale;
        psr.sharedMaterial = mat;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows = false;

        ps.Play();
        return ps;
    }

    private void AttachWindowPaneRainRivulets()
    {
        if (buildingBaseTransform == null) return;
        Transform glassChild = buildingBaseTransform.Find(CabinWindowUtility.GlassChildObjectName);
        if (glassChild == null) return;

        Mesh srcGlassMesh = HouseAct3PropController.GetReadableSourceMesh(glassChild.gameObject);
        if (srcGlassMesh == null) return;

        Transform existingOverlay = glassChild.Find("Cabin_WindowRainRivulets");
        GameObject overlayGo;
        if (existingOverlay != null)
        {
            overlayGo = existingOverlay.gameObject;
        }
        else
        {
            overlayGo = new GameObject("Cabin_WindowRainRivulets");
            overlayGo.transform.SetParent(glassChild, false);
            overlayGo.transform.localPosition = Vector3.zero;
            overlayGo.transform.localRotation = Quaternion.identity;
            overlayGo.transform.localScale = Vector3.one;
        }

        MeshFilter mf = overlayGo.GetComponent<MeshFilter>();
        if (mf == null) mf = overlayGo.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildVerticalRivuletMesh(srcGlassMesh);

        MeshRenderer mr = overlayGo.GetComponent<MeshRenderer>();
        if (mr == null) mr = overlayGo.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Shader sh = Shader.Find("Custom/RealisticHorrorRain") ?? Shader.Find("Sprites/Default");
        windowRivuletMat = new Material(sh);
        windowRivuletMat.name = "CabinWindowRainRivulets_MAT";
        Texture2D rivuletTex = GenerateWindowRainRivuletTexture(256, 256);
        windowRivuletMat.mainTexture = rivuletTex;
        windowRivuletMat.mainTextureScale = new Vector2(0.85f, 0.72f);
        windowRivuletMat.color = new Color(0.86f, 0.91f, 0.96f, 0.22f);
        windowRivuletMat.SetVector("_CabinExcludeMin", Vector4.zero);
        windowRivuletMat.SetVector("_CabinLocalMin", Vector4.zero);
        windowRivuletMat.SetVector("_CanopyExcludeMin", Vector4.zero);

        mr.sharedMaterial = windowRivuletMat;
    }

    public static Mesh BuildVerticalRivuletMesh(Mesh srcGlassMesh)
    {
        if (srcGlassMesh == null || !srcGlassMesh.isReadable) return srcGlassMesh;

        Mesh m = Instantiate(srcGlassMesh);
        m.name = srcGlassMesh.name + "_VerticalRivuletUVs";

        Vector3[] verts = m.vertices;
        Vector3[] normals = m.normals;
        bool hasNormals = normals != null && normals.Length == verts.Length;

        // Reproject windowpane UVs cleanly into world-vertical planar space (U = horizontal wall span, V = vertical Y)
        // because the cabin's original window mesh has rotated UVs that made rain streaks run sideways.
        Vector2[] verticalUvs = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 p = verts[i];
            float u = (hasNormals && Mathf.Abs(normals[i].x) > Mathf.Abs(normals[i].z)) ? p.z : p.x;
            verticalUvs[i] = new Vector2(u, p.y);
        }

        m.SetUVs(0, verticalUvs);
        return m;
    }

    private static Texture2D GenerateWindowRainRivuletTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;

        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        Random.State oldState = Random.state;
        Random.InitState(44821);

        // Draw 28 vertical meandering water rivulets + 140 tiny beading raindrops on the glass
        for (int s = 0; s < 28; s++)
        {
            float baseX = Random.Range(0f, w);
            float startY = Random.Range(0, h);
            int len = Random.Range(45, 130);
            float phase = Random.Range(0f, 10f);

            for (int j = 0; j < len; j++)
            {
                int py = ((int)startY + j) % h;
                float t = (float)j / len;
                float wiggle = Mathf.Sin(t * 9.0f + phase) * 1.6f;
                int px = ((int)(baseX + wiggle) + w) % w;

                float alpha = Mathf.Sin(t * Mathf.PI) * 0.55f;
                if (j < 6) alpha *= 1.35f; // Brighter bead at tip of rivulet

                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = (px + dx + w) % w;
                    float falloff = dx == 0 ? 1f : 0.35f;
                    int idx = py * w + nx;
                    float a = Mathf.Max(pixels[idx].a, Mathf.Clamp01(alpha * falloff));
                    pixels[idx] = new Color(0.88f, 0.94f, 1.0f, a);
                }
            }
        }

        for (int d = 0; d < 140; d++)
        {
            int cx = Random.Range(0, w);
            int cy = Random.Range(0, h);
            int rad = Random.Range(1, 3);
            for (int dy = -rad; dy <= rad; dy++)
            {
                for (int dx = -rad; dx <= rad; dx++)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= rad + 0.3f)
                    {
                        int nx = (cx + dx + w) % w;
                        int ny = (cy + dy + h) % h;
                        int idx = ny * w + nx;
                        float a = Mathf.Max(pixels[idx].a, Mathf.Clamp01((1f - dist / (rad + 0.6f)) * 0.65f));
                        pixels[idx] = new Color(0.90f, 0.95f, 1.0f, a);
                    }
                }
            }
        }

        Random.state = oldState;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void SetupLightningLight()
    {
        GameObject lightGo = new GameObject("Act3_LightningFlashLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.position = cabinWorldCenter + new Vector3(0f, 12f, 0f);
        lightGo.transform.rotation = Quaternion.Euler(38f, -135f, 0f);

        lightningLight = lightGo.AddComponent<Light>();
        lightningLight.type = LightType.Directional;
        lightningLight.color = new Color(0.72f, 0.84f, 1.0f);
        lightningLight.intensity = 0f;
        lightningLight.enabled = false;
    }

    private void SetupStormAudio()
    {
        int sr = 44100;

        // 1. Heavy realistic rain + rooftop/window patter loop (4.0 seconds seamless)
        int rainSamples = sr * 4;
        float[] rainData = new float[rainSamples];
        float b0 = 0f, b1 = 0f, b2 = 0f;
        Random.State oldState = Random.state;
        Random.InitState(90125);
        for (int i = 0; i < rainSamples; i++)
        {
            float white = Random.value * 2f - 1f;
            b0 = 0.997f * b0 + white * 0.029f;
            b1 = 0.985f * b1 + white * 0.032f;
            b2 = 0.940f * b2 + white * 0.048f;
            float pink = (b0 + b1 + b2) * 0.42f;

            float droplet = 0f;
            if (Random.value < 0.0045f)
            {
                droplet = (Random.value * 2f - 1f) * 0.35f;
            }

            float t = (float)i / sr;
            float windMod = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
            rainData[i] = Mathf.Clamp((pink * windMod + droplet) * 0.68f, -1f, 1f);
        }

        heavyRainLoopClip = AudioClip.Create("Act3_HeavyRainLoop", rainSamples, 1, sr, false);
        heavyRainLoopClip.SetData(rainData, 0);

        // 2. Two rolling thunder variations
        thunderClips = new AudioClip[2];
        for (int c = 0; c < 2; c++)
        {
            int thSamples = Mathf.RoundToInt(3.8f * sr);
            float[] thData = new float[thSamples];
            float lowCarry = 0f;
            for (int i = 0; i < thSamples; i++)
            {
                float t = (float)i / sr;
                float n = Random.value * 2f - 1f;
                lowCarry = Mathf.Lerp(lowCarry, n, 0.035f);

                float env1 = Mathf.Exp(-t * 1.45f) * Mathf.Clamp01(t / 0.05f);
                float env2 = t > 0.55f ? Mathf.Exp(-(t - 0.55f) * 1.15f) * 0.72f : 0f;
                float env3 = t > 1.35f ? Mathf.Exp(-(t - 1.35f) * 0.95f) * 0.48f : 0f;
                float rumble = lowCarry * 2.4f * (env1 + env2 + env3);
                float sub = Mathf.Sin(2f * Mathf.PI * (48f - t * 6f) * t) * (env1 + env2 * 0.8f) * 0.45f;
                thData[i] = Mathf.Clamp(rumble + sub, -1f, 1f);
            }
            thunderClips[c] = AudioClip.Create("Act3_Thunder_" + c, thSamples, 1, sr, false);
            thunderClips[c].SetData(thData, 0);
        }
        Random.state = oldState;

        rainLoopSource = gameObject.AddComponent<AudioSource>();
        rainLoopSource.spatialBlend = 0f;
        rainLoopSource.clip = heavyRainLoopClip;
        rainLoopSource.loop = true;
        rainLoopSource.volume = 0.68f;
        rainLoopSource.Play();

        thunderSource = gameObject.AddComponent<AudioSource>();
        thunderSource.spatialBlend = 0f;
        thunderSource.playOnAwake = false;
        thunderSource.volume = 0.88f;
    }

    private float EvaluateTargetRainExposure(Camera cam, out float lookUpBoost)
    {
        lookUpBoost = 0f;
        if (cam == null) return 0f;

        if (HouseSceneStoryController.IsIntroActive ||
            NightShiftStoryController.IsIntroOverlayBlockingUI)
        {
            return 0f;
        }

        Vector3 camPos = cam.transform.position;

        // 1. Inside HouseScene Cabin footprint (local-space OBB check first, world-space fallback)
        if (hasLocalCabinBounds && buildingBaseTransform != null)
        {
            Vector3 lp = buildingBaseTransform.worldToLocalMatrix.MultiplyPoint3x4(camPos);
            if (lp.x >= cabinLocalBounds.min.x && lp.x <= cabinLocalBounds.max.x &&
                lp.z >= cabinLocalBounds.min.z && lp.z <= cabinLocalBounds.max.z &&
                lp.y <= cabinLocalBounds.max.y + 0.5f)
            {
                return 0f;
            }
        }
        else if (hasCabinBounds)
        {
            if (camPos.x >= cabinWorldBounds.min.x && camPos.x <= cabinWorldBounds.max.x &&
                camPos.z >= cabinWorldBounds.min.z && camPos.z <= cabinWorldBounds.max.z &&
                camPos.y <= cabinRoofY + 0.5f)
            {
                return 0f;
            }
        }

        // 2. Inside GasScene2 convenience store interior / overhang
        if (hasGasStoreBounds &&
            camPos.x >= gasStoreMinX && camPos.x <= gasStoreMaxX &&
            camPos.z >= gasStoreMinZ && camPos.z <= gasStoreMaxZ &&
            camPos.y <= gasStoreMaxY)
        {
            return 0f;
        }

        // 3. Under GasScene2 outdoor fuel pump canopy ceiling
        if (hasCanopyBounds &&
            camPos.x >= canopyMinX && camPos.x <= canopyMaxX &&
            camPos.z >= canopyMinZ && camPos.z <= canopyMaxZ &&
            camPos.y <= canopyRoofTopY + 0.25f)
        {
            return 0f;
        }

        // 4. General overhead shelter check (catches porches, awnings, or parked shelter colliders)
        int roofHits = Physics.SphereCastNonAlloc(
            camPos + Vector3.up * 0.15f,
            0.20f,
            Vector3.up,
            overheadHitsBuffer,
            18.0f,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < roofHits; i++)
        {
            Collider col = overheadHitsBuffer[i].collider;
            if (col == null || col.GetComponentInParent<CharacterController>() != null) continue;
            Bounds cb = col.bounds;
            if (cb.size.x < 0.85f && cb.size.z < 0.85f) continue;
            return 0f;
        }

        lookUpBoost = Mathf.Clamp01((cam.transform.forward.y - 0.05f) * 1.35f);
        return 1.0f;
    }

    private void UpdatePovRainDropletPool(bool isExposedToRain, float lookUpBoost)
    {
        float dt = Time.deltaTime;
        if (isExposedToRain && !HouseSceneStoryController.IsIntroActive && !NightShiftStoryController.IsIntroOverlayBlockingUI)
        {
            int maxAllowedDrops = Mathf.RoundToInt(Mathf.Lerp(10f, 15f, lookUpBoost));
            if (Time.time >= nextPovDropSpawnTime && activePovDrops.Count < maxAllowedDrops)
            {
                float minDelay = Mathf.Lerp(0.24f, 0.11f, lookUpBoost);
                float maxDelay = Mathf.Lerp(0.58f, 0.28f, lookUpBoost);
                nextPovDropSpawnTime = Time.time + Random.Range(minDelay, maxDelay);

                float rx = Random.Range(0.05f, 0.95f);
                float ry = Random.Range(0.05f, 0.86f);
                // Keep the dead-center crosshair reticle clear most of the time
                if (Vector2.Distance(new Vector2(rx, ry), new Vector2(0.5f, 0.5f)) < 0.12f && Random.value < 0.82f)
                {
                    rx = rx < 0.5f ? Random.Range(0.08f, 0.36f) : Random.Range(0.64f, 0.92f);
                }

                bool heavyDrop = Random.value < (0.42f + lookUpBoost * 0.18f);
                PovRainDrop drop = new PovRainDrop
                {
                    normX = rx,
                    normY = ry,
                    baseSizeFraction = heavyDrop ? Random.Range(0.024f, 0.042f) : Random.Range(0.014f, 0.026f),
                    aspectStretch = heavyDrop ? Random.Range(1.22f, 1.48f) : Random.Range(1.04f, 1.18f),
                    age = 0f,
                    lifetime = heavyDrop ? Random.Range(2.1f, 3.6f) : Random.Range(1.4f, 2.7f),
                    slideSpeed = heavyDrop ? Random.Range(0.032f, 0.082f) : Random.Range(0.004f, 0.014f),
                    wanderPhase = Random.Range(0f, 10f),
                    wanderAmp = heavyDrop ? Random.Range(0.008f, 0.022f) : 0.002f,
                    maxAlpha = Random.Range(0.76f, 0.96f),
                    leaveTrail = heavyDrop
                };
                activePovDrops.Add(drop);
            }
        }

        // Advance active random droplets (dry/clear faster once sheltered)
        float ageMultiplier = isExposedToRain ? 1.0f : 2.35f;
        for (int i = activePovDrops.Count - 1; i >= 0; i--)
        {
            PovRainDrop d = activePovDrops[i];
            d.age += dt * ageMultiplier;
            if (d.age > 0.24f)
            {
                d.normY += d.slideSpeed * dt;
                d.normX += Mathf.Sin(d.age * 4.4f + d.wanderPhase) * d.wanderAmp * dt;
            }

            if (d.age >= d.lifetime || d.normY > 1.08f)
            {
                activePovDrops.RemoveAt(i);
            }
            else
            {
                activePovDrops[i] = d;
            }
        }
    }

    private void EnsurePovRainTextures()
    {
        if (povSingleDropTex == null) povSingleDropTex = GeneratePovSingleDropTexture(128, 128);
        if (povDropTrailTex == null) povDropTrailTex = GeneratePovDropTrailTexture(64, 128);
    }

    private void OnGUI()
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        if (activePovDrops.Count == 0) return;
        if (HouseSceneStoryController.IsIntroActive || NightShiftStoryController.IsIntroOverlayBlockingUI) return;

        EnsurePovRainTextures();

        int prevDepth = GUI.depth;
        Color prevColor = GUI.color;
        GUI.depth = -200; // Render over 3D world, strictly behind dialogues/HUD (-900/-1000)

        float sh = Screen.height;
        float sw = Screen.width;

        for (int i = 0; i < activePovDrops.Count; i++)
        {
            PovRainDrop d = activePovDrops[i];

            // Pop-in splash bounce during first 0.12s, gentle evaporation fade during final 0.50s
            float popIn = Mathf.Clamp01(d.age / 0.11f);
            float popScale = popIn < 1f ? Mathf.Lerp(0.55f, 1.12f, Mathf.Sin(popIn * Mathf.PI * 0.5f)) : Mathf.Lerp(1.12f, 1.0f, Mathf.Clamp01((d.age - 0.11f) / 0.12f));
            float fadeOut = Mathf.Clamp01((d.lifetime - d.age) / 0.50f);
            float shrinkOut = Mathf.Lerp(0.75f, 1.0f, fadeOut);
            float alpha = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, popIn) * Mathf.SmoothStep(0f, 1f, fadeOut) * d.maxAlpha);
            if (alpha <= 0.01f) continue;

            float dropW = Mathf.Max(10f, sh * d.baseSizeFraction * popScale * shrinkOut);
            float dropH = dropW * d.aspectStretch;
            float cx = d.normX * sw;
            float cy = d.normY * sh;

            // Optional slender gravity wake above heavy sliding water droplets
            if (d.leaveTrail && povDropTrailTex != null && d.age > 0.28f)
            {
                float slidDist = Mathf.Min(sh * 0.16f, (d.age - 0.24f) * d.slideSpeed * sh * 0.95f);
                if (slidDist > 8f)
                {
                    GUI.color = new Color(1f, 1f, 1f, alpha * 0.48f);
                    Rect trailRect = new Rect(cx - dropW * 0.34f, cy - slidDist - dropH * 0.25f, dropW * 0.68f, slidDist + dropH * 0.35f);
                    GUI.DrawTexture(trailRect, povDropTrailTex, ScaleMode.StretchToFill, true);
                }
            }

            // Main realistic clear liquid water droplet (crisp dark meniscus rim + bottom caustic + specular glint)
            if (povSingleDropTex != null)
            {
                GUI.color = new Color(1f, 1f, 1f, alpha);
                Rect dropRect = new Rect(cx - dropW * 0.5f, cy - dropH * 0.5f, dropW, dropH);
                GUI.DrawTexture(dropRect, povSingleDropTex, ScaleMode.StretchToFill, true);
            }
        }

        GUI.color = prevColor;
        GUI.depth = prevDepth;
    }

    private static Texture2D GeneratePovSingleDropTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            // Unity GUI maps Texture2D y=0 to BOTTOM and y=h-1 to TOP
            float ny = ((float)y / (h - 1)) * 2f - 1f;
            float gravityShapeX = Mathf.Lerp(1.06f, 0.80f, (ny + 1f) * 0.5f);

            for (int x = 0; x < w; x++)
            {
                float nx = ((float)x / (w - 1)) * 2f - 1f;
                float sx = nx / gravityShapeX;
                float sy = ny * 0.98f;
                float r = Mathf.Sqrt(sx * sx + sy * sy);

                if (r > 1.06f)
                {
                    pixels[y * w + x] = Color.clear;
                    continue;
                }

                // 1. Dark total-internal-reflection meniscus rim around outer contour (reads crisply against any background)
                float outerMask = 1f - Mathf.SmoothStep(0.92f, 1.05f, r);
                float darkRim = Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.17f);

                // 2. Curved lower caustic highlight focused by water bead (bottom belly ny < -0.15)
                float bottomCaustic = Mathf.Clamp01((-ny - 0.14f) * 1.65f)
                    * Mathf.SmoothStep(0.28f, 0.72f, r)
                    * (1f - Mathf.SmoothStep(0.76f, 0.96f, r));

                // 3. Soft tight top-left sky glint
                float gDx = (nx + 0.25f) * 1.45f;
                float gDy = (ny - 0.34f) * 1.45f;
                float topGlint = Mathf.Exp(-(gDx * gDx + gDy * gDy) * 18.0f);

                // 4. Transparent inner water bowl (low alpha so background stays cleanly visible inside droplet!)
                float innerBowl = 0.08f + Mathf.SmoothStep(0.0f, 0.85f, r) * 0.10f;

                float rimA = darkRim * 0.54f;
                float causticA = bottomCaustic * 0.78f;
                float glintA = topGlint * 0.84f;
                float totalA = Mathf.Clamp01((innerBowl + rimA + causticA + glintA) * outerMask);

                Color darkWater = new Color(0.04f, 0.07f, 0.12f, 1f);
                Color coolBowl = new Color(0.18f, 0.26f, 0.34f, 1f);
                Color brightCaustic = new Color(0.88f, 0.95f, 1.00f, 1f);
                Color glintCol = new Color(0.96f, 0.99f, 1.00f, 1f);

                float lightMix = Mathf.Clamp01(causticA + glintA);
                float darkMix = Mathf.Clamp01(rimA * 1.25f * (1f - causticA * 0.8f));
                Color rgb = Color.Lerp(coolBowl, darkWater, darkMix);
                rgb = Color.Lerp(rgb, Color.Lerp(brightCaustic, glintCol, glintA), lightMix);
                rgb.a = totalA;

                pixels[y * w + x] = rgb;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D GeneratePovDropTrailTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            // v=0 bottom (touches sliding bead), v=1 top (fades cleanly to dry glass)
            float v = (float)y / (h - 1);
            float vertFade = Mathf.SmoothStep(1.0f, 0.05f, v) * Mathf.SmoothStep(0.0f, 0.12f, v);
            float wiggleCenter = Mathf.Sin(v * 7.2f) * 0.10f;

            // Occasional tiny micro-droplet beads clinging inside the upper wake
            float bead1 = Mathf.Exp(-((v - 0.38f) * (v - 0.38f)) * 420f);
            float bead2 = Mathf.Exp(-((v - 0.72f) * (v - 0.72f)) * 520f) * 0.75f;
            float wakeWidth = Mathf.Lerp(0.30f, 0.14f, v) + (bead1 + bead2) * 0.14f;

            for (int x = 0; x < w; x++)
            {
                float nx = ((float)x / (w - 1)) * 2f - 1f - wiggleCenter;
                float dist = Mathf.Abs(nx) / wakeWidth;
                if (dist > 1.2f)
                {
                    pixels[y * w + x] = Color.clear;
                    continue;
                }

                float edgeRim = Mathf.Clamp01(1f - Mathf.Abs(dist - 0.78f) * 3.2f) * 0.24f;
                float coreGloss = Mathf.Pow(Mathf.Clamp01(1f - dist), 2.0f) * (0.28f + (bead1 + bead2) * 0.45f);
                float alpha = Mathf.Clamp01((coreGloss + edgeRim) * vertFade);

                Color c = Color.Lerp(new Color(0.06f, 0.09f, 0.14f, 1f), new Color(0.86f, 0.94f, 1.00f, 1f), Mathf.Clamp01(coreGloss * 1.9f));
                c.a = alpha;
                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}

/// <summary>
/// Faucet face-washing interactable attached to PFB_BathroomSink for Act 3 ("Task: Wash your face").
/// Shows "E to Wash Your Face", runs faucet water stream & splashing audio for 3 seconds,
/// then triggers the sudden power outage and confused camera look-up animation.
/// </summary>
public class CabinBathroomSinkFaceWash : MonoBehaviour, IInteractable
{
    private GameObject waterStreamGo;
    private GameObject basinRippleGo;
    private readonly List<Transform> splashDroplets = new List<Transform>();
    private AudioSource audioSource;
    private AudioClip faucetTurnClip;
    private AudioClip runningWaterClip;
    private AudioClip scoopWaterClip;
    private AudioClip splashFaceClip;
    private AudioClip electricalFlickerClip;

    private Vector3 spoutWorldPos;
    private Vector3 basinWorldPos;
    private bool isWaterRunning = false;

    private void Awake()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.82f, 0.05f);
            col.size = new Vector3(0.85f, 0.95f, 0.75f);
        }

        BuildWaterVisuals();
        SetupAudio();
    }

    private void Update()
    {
        if (!isWaterRunning) return;

        float t = Time.time;
        if (waterStreamGo != null)
        {
            float pulse = 0.020f + Mathf.Sin(t * 24f) * 0.0025f;
            waterStreamGo.transform.localScale = new Vector3(pulse, waterStreamGo.transform.localScale.y, pulse);
        }
        if (basinRippleGo != null)
        {
            float rScale = 0.21f + Mathf.Sin(t * 14f) * 0.025f;
            basinRippleGo.transform.localScale = new Vector3(rScale, 0.005f, rScale);
        }
        for (int i = 0; i < splashDroplets.Count; i++)
        {
            Transform d = splashDroplets[i];
            if (d == null) continue;
            float phase = (t * 3.4f + i * 0.21f) % 1.0f;
            float ang = i * 1.15f;
            float rad = phase * 0.11f;
            float dy = Mathf.Sin(phase * Mathf.PI) * 0.075f;
            d.position = basinWorldPos + new Vector3(Mathf.Cos(ang) * rad, dy + 0.01f, Mathf.Sin(ang) * rad);
        }
    }

    public Vector3 GetBasinWorldPosition()
    {
        return basinWorldPos != Vector3.zero ? basinWorldPos : transform.position + Vector3.up * 0.86f;
    }

    public Vector3 GetSpoutWorldPosition()
    {
        return spoutWorldPos != Vector3.zero ? spoutWorldPos : transform.position + Vector3.up * 1.08f;
    }

    private void ResolveSinkBasinAndSpoutWorldPositions()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Bounds sinkBounds = new Bounds(transform.position + Vector3.up * 0.5f, new Vector3(0.6f, 1.0f, 0.5f));
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (!hasBounds)
            {
                sinkBounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                sinkBounds.Encapsulate(renderers[i].bounds);
            }
        }

        // Find highest vertices (faucet arch/spout at top of sink)
        Vector3 topSum = Vector3.zero;
        int topCount = 0;
        float topThresholdY = sinkBounds.max.y - 0.09f;

        MeshFilter[] mfs = GetComponentsInChildren<MeshFilter>(true);
        for (int m = 0; m < mfs.Length; m++)
        {
            MeshFilter mf = mfs[m];
            if (mf == null) continue;

            // Static batching replaces MeshFilter.sharedMesh with an unreadable 'Combined Mesh (root: scene)'
            Mesh readableMesh = HouseAct3PropController.GetReadableSourceMesh(mf.gameObject);
            if (readableMesh == null) continue;

            Vector3[] verts = readableMesh.vertices;
            Transform tr = mf.transform;
            for (int v = 0; v < verts.Length; v += 2)
            {
                Vector3 wp = tr.TransformPoint(verts[v]);
                if (wp.y >= topThresholdY)
                {
                    topSum += wp;
                    topCount++;
                }
            }
        }

        Vector3 faucetTopCenter = topCount > 0
            ? (topSum / topCount)
            : new Vector3(sinkBounds.center.x, sinkBounds.max.y - 0.04f, sinkBounds.center.z);

        // Direction from wall/faucet toward center of sink basin
        Vector3 toBasinHoriz = new Vector3(sinkBounds.center.x - faucetTopCenter.x, 0f, sinkBounds.center.z - faucetTopCenter.z);
        if (toBasinHoriz.sqrMagnitude < 0.0004f)
        {
            toBasinHoriz = transform.right;
            toBasinHoriz.y = 0f;
        }
        toBasinHoriz.Normalize();

        spoutWorldPos = new Vector3(faucetTopCenter.x, sinkBounds.max.y - 0.06f, faucetTopCenter.z) + toBasinHoriz * 0.09f;
        float basinY = sinkBounds.min.y + (sinkBounds.max.y - sinkBounds.min.y) * 0.74f;
        basinWorldPos = new Vector3(spoutWorldPos.x, basinY, spoutWorldPos.z) + toBasinHoriz * 0.02f;
    }

    private void BuildWaterVisuals()
    {
        ResolveSinkBasinAndSpoutWorldPositions();

        Shader sh = Shader.Find("Custom/RealisticHorrorRain")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Standard");
        Material waterMat = new Material(sh);
        Color wc = new Color(0.76f, 0.90f, 0.98f, 0.62f);
        waterMat.color = wc;
        if (waterMat.HasProperty("_BaseColor")) waterMat.SetColor("_BaseColor", wc);

        float streamHeight = Mathf.Max(0.10f, spoutWorldPos.y - basinWorldPos.y);
        Vector3 streamMidWorld = new Vector3(
            (spoutWorldPos.x + basinWorldPos.x) * 0.5f,
            (spoutWorldPos.y + basinWorldPos.y) * 0.5f,
            (spoutWorldPos.z + basinWorldPos.z) * 0.5f);

        waterStreamGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        waterStreamGo.name = "BathroomFaucet_WaterStream";
        waterStreamGo.transform.SetParent(transform, true);
        Destroy(waterStreamGo.GetComponent<Collider>());
        waterStreamGo.transform.position = streamMidWorld;
        waterStreamGo.transform.rotation = Quaternion.identity;
        waterStreamGo.transform.localScale = new Vector3(0.020f, streamHeight * 0.5f, 0.020f);
        waterStreamGo.GetComponent<Renderer>().sharedMaterial = waterMat;
        waterStreamGo.SetActive(false);

        basinRippleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basinRippleGo.name = "BathroomFaucet_BasinRipple";
        basinRippleGo.transform.SetParent(transform, true);
        Destroy(basinRippleGo.GetComponent<Collider>());
        basinRippleGo.transform.position = basinWorldPos;
        basinRippleGo.transform.rotation = Quaternion.identity;
        basinRippleGo.transform.localScale = new Vector3(0.21f, 0.005f, 0.21f);
        basinRippleGo.GetComponent<Renderer>().sharedMaterial = waterMat;
        basinRippleGo.SetActive(false);

        splashDroplets.Clear();
        for (int i = 0; i < 6; i++)
        {
            GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drop.name = "BathroomFaucet_SplashDrop_" + i;
            drop.transform.SetParent(transform, true);
            Destroy(drop.GetComponent<Collider>());
            drop.transform.position = basinWorldPos;
            drop.transform.localScale = new Vector3(0.014f, 0.014f, 0.014f);
            drop.GetComponent<Renderer>().sharedMaterial = waterMat;
            drop.SetActive(false);
            splashDroplets.Add(drop.transform);
        }
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.65f;
        audioSource.minDistance = 0.6f;
        audioSource.maxDistance = 9.0f;
        audioSource.playOnAwake = false;

        int sr = 44100;

        // 1. Faucet handle turn squeak + metallic valve click
        int tapSamples = Mathf.RoundToInt(0.38f * sr);
        float[] tapData = new float[tapSamples];
        for (int i = 0; i < tapSamples; i++)
        {
            float t = (float)i / sr;
            float squeak = Mathf.Sin(2f * Mathf.PI * (1080f - 340f * t) * t) * Mathf.Exp(-t * 15f) * 0.42f;
            float valve = Mathf.Sin(2f * Mathf.PI * 185f * t) * Mathf.Exp(-t * 24f) * 0.38f;
            tapData[i] = Mathf.Clamp(squeak + valve, -1f, 1f);
        }
        faucetTurnClip = AudioClip.Create("BathroomSink_FaucetTurn", tapSamples, 1, sr, false);
        faucetTurnClip.SetData(tapData, 0);

        // 2. Continuous running water loop
        int wSamples = Mathf.RoundToInt(1.8f * sr);
        float[] wData = new float[wSamples];
        float prev = 0f;
        for (int i = 0; i < wSamples; i++)
        {
            float n = Random.value * 2f - 1f;
            prev = Mathf.Lerp(prev, n, 0.28f);
            wData[i] = Mathf.Clamp((n - prev) * 0.42f, -1f, 1f);
        }
        runningWaterClip = AudioClip.Create("BathroomSink_RunningWater", wSamples, 1, sr, false);
        runningWaterClip.SetData(wData, 0);

        // 3. Cupping water in both hands under the stream
        int scSamples = Mathf.RoundToInt(0.85f * sr);
        float[] scData = new float[scSamples];
        float scPrev = 0f;
        for (int i = 0; i < scSamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Sin(Mathf.Clamp01(t / 0.85f) * Mathf.PI);
            float n = Random.value * 2f - 1f;
            scPrev = Mathf.Lerp(scPrev, n, 0.22f);
            float hollowFill = Mathf.Sin(2f * Mathf.PI * (220f + t * 165f) * t) * env * 0.26f;
            scData[i] = Mathf.Clamp((scPrev * 0.45f * env) + hollowFill, -1f, 1f);
        }
        scoopWaterClip = AudioClip.Create("BathroomSink_ScoopWater", scSamples, 1, sr, false);
        scoopWaterClip.SetData(scData, 0);

        // 4. Slow, realistic water splash onto face + wet hands rubbing skin + water dripping into basin
        int spSamples = Mathf.RoundToInt(1.75f * sr);
        float[] spData = new float[spSamples];
        float spCarry = 0f;
        for (int i = 0; i < spSamples; i++)
        {
            float t = (float)i / sr;
            float splashEnv = t < 0.48f ? Mathf.Sin((t / 0.48f) * Mathf.PI) : 0f;
            float rubEnv = t >= 0.32f && t < 1.55f ? Mathf.Sin(((t - 0.32f) / 1.23f) * Mathf.PI) : 0f;

            float raw = Random.value * 2f - 1f;
            spCarry = Mathf.Lerp(spCarry, raw, 0.16f);

            float splashBurst = (raw * 0.50f + spCarry * 0.50f) * splashEnv * 0.72f;
            float sloshLow = Mathf.Sin(2f * Mathf.PI * (260f - t * 140f) * t) * splashEnv * 0.36f;
            float skinRub = spCarry * (0.55f + 0.45f * Mathf.Sin(t * 11f)) * rubEnv * 0.46f;
            spData[i] = Mathf.Clamp(splashBurst + sloshLow + skinRub, -1f, 1f);
        }
        splashFaceClip = AudioClip.Create("BathroomSink_SplashFace", spSamples, 1, sr, false);
        splashFaceClip.SetData(spData, 0);

        // 5. Ominous electrical bulb buzz/flicker right before outage
        int flSamples = Mathf.RoundToInt(0.45f * sr);
        float[] flData = new float[flSamples];
        for (int i = 0; i < flSamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI);
            float buzz = (Mathf.Sin(2f * Mathf.PI * 120f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 240f * t)) * 0.28f;
            float crackle = (Random.value > 0.92f ? (Random.value * 2f - 1f) * 0.65f : 0f);
            flData[i] = Mathf.Clamp((buzz + crackle) * env, -1f, 1f);
        }
        electricalFlickerClip = AudioClip.Create("BathroomSink_LightFlickerBuzz", flSamples, 1, sr, false);
        electricalFlickerClip.SetData(flData, 0);
    }

    public void PlayFaucetTurnOneShot()
    {
        if (audioSource != null && faucetTurnClip != null)
        {
            audioSource.PlayOneShot(faucetTurnClip, 0.90f);
        }
    }

    public void PlayScoopWaterOneShot()
    {
        if (audioSource != null && scoopWaterClip != null)
        {
            audioSource.PlayOneShot(scoopWaterClip, 0.85f);
        }
    }

    public void PlayElectricalFlickerBuzzOneShot()
    {
        if (audioSource != null && electricalFlickerClip != null)
        {
            audioSource.PlayOneShot(electricalFlickerClip, 0.80f);
        }
    }

    public void SetWaterRunning(bool running)
    {
        isWaterRunning = running;
        if (waterStreamGo != null) waterStreamGo.SetActive(running);
        if (basinRippleGo != null) basinRippleGo.SetActive(running);
        for (int i = 0; i < splashDroplets.Count; i++)
        {
            if (splashDroplets[i] != null) splashDroplets[i].gameObject.SetActive(running);
        }
        if (audioSource != null)
        {
            if (running && runningWaterClip != null)
            {
                audioSource.clip = runningWaterClip;
                audioSource.loop = true;
                audioSource.volume = 0.65f;
                audioSource.Play();
            }
            else
            {
                audioSource.Stop();
            }
        }
    }

    public void PlayFaceSplashOneShot()
    {
        if (audioSource != null && splashFaceClip != null)
        {
            audioSource.PlayOneShot(splashFaceClip, 0.85f);
        }
    }

    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;
        if (HouseSceneStoryController.IsAct3WashFaceTaskActive)
        {
            return "E to Wash Your Face";
        }
        return string.Empty;
    }

    public void Interact()
    {
        if (HouseSceneStoryController.IsDialogueActive) return;
        if (!HouseSceneStoryController.IsAct3WashFaceTaskActive) return;

        if (HouseSceneStoryController.Instance != null)
        {
            HouseSceneStoryController.Instance.TriggerAct3FaceWash(transform, null, this);
        }
    }
}

/// <summary>
/// 3D Candle model placed laying down between grp_doorsL and grp_doorsR on PFB_TVStand in the living room.
/// When picked up, holds in the player's LEFT hand (-X side of camera) and allows pressing E to light it up.
/// </summary>
public class CabinCandleInteractable : MonoBehaviour, IInteractable
{
    public static CabinCandleInteractable HeldCandle { get; set; }

    public bool isPickedUp = false;
    public bool isLit = false;

    private Transform meshPivot;
    private GameObject saucerPlateGo;
    private GameObject flameRootGo;
    private Light candlePointLight;
    private AudioSource audioSource;
    private AudioClip pickupClip;
    private AudioClip lighterStrikeClip;

    private readonly Vector3 leftHandLocalPos = new Vector3(-0.27f, -0.23f, 0.44f);
    private readonly Vector3 leftHandLocalEuler = new Vector3(4f, 12f, -3f);
    private float igniteStrikeOffset = 0f;

    public void BuildCandleVisuals()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.02f, 0f);
            col.size = new Vector3(0.32f, 0.22f, 0.32f);
            col.isTrigger = true;
        }

        meshPivot = new GameObject("CandleMeshPivot").transform;
        meshPivot.SetParent(transform, false);
        // Laying down horizontally on its side while on the TV stand shelf
        meshPivot.localPosition = Vector3.zero;
        meshPivot.localRotation = Quaternion.Euler(0f, 0f, 90f);

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? litShader;

        Material waxMat = HouseAct3PropController.CreateLitMaterial(litShader, new Color(0.94f, 0.90f, 0.80f), 0.35f);
        Material wickMat = HouseAct3PropController.CreateLitMaterial(litShader, new Color(0.12f, 0.10f, 0.09f), 0.10f);
        Material brassMat = HouseAct3PropController.CreateLitMaterial(litShader, new Color(0.68f, 0.53f, 0.24f), 0.65f);

        // Main wax pillar cylinder
        HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Cylinder, "WaxBody",
            new Vector3(0f, 0.075f, 0f), Vector3.zero, new Vector3(0.042f, 0.075f, 0.042f), waxMat);

        // Melted wax rim at top
        HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Sphere, "WaxRim",
            new Vector3(0f, 0.148f, 0f), Vector3.zero, new Vector3(0.045f, 0.014f, 0.045f), waxMat);

        // Wax drip down side
        HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Capsule, "WaxDrip",
            new Vector3(0.020f, 0.105f, 0f), Vector3.zero, new Vector3(0.008f, 0.032f, 0.008f), waxMat);

        // Charred wick
        HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Cylinder, "Wick",
            new Vector3(0f, 0.162f, 0f), new Vector3(4f, 0f, -6f), new Vector3(0.005f, 0.014f, 0.005f), wickMat);

        // Brass holder saucer (shown when held upright in left hand)
        saucerPlateGo = HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Cylinder, "BrassSaucer",
            new Vector3(0f, 0.004f, 0f), Vector3.zero, new Vector3(0.085f, 0.005f, 0.085f), brassMat);
        saucerPlateGo.SetActive(false);

        // Flame visual + warm point light (initially off until E to Light it up)
        flameRootGo = new GameObject("CandleFlameRoot");
        flameRootGo.transform.SetParent(meshPivot, false);
        flameRootGo.transform.localPosition = new Vector3(0f, 0.184f, 0f);

        Material outerFlameMat = new Material(unlitShader);
        Color outerCol = new Color(1.0f, 0.58f, 0.14f, 1.0f);
        outerFlameMat.color = outerCol;
        if (outerFlameMat.HasProperty("_BaseColor")) outerFlameMat.SetColor("_BaseColor", outerCol);

        Material innerFlameMat = new Material(unlitShader);
        Color innerCol = new Color(1.0f, 0.95f, 0.68f, 1.0f);
        innerFlameMat.color = innerCol;
        if (innerFlameMat.HasProperty("_BaseColor")) innerFlameMat.SetColor("_BaseColor", innerCol);

        HouseAct3PropController.CreatePrimitivePart(flameRootGo.transform, PrimitiveType.Sphere, "FlameOuter",
            Vector3.zero, Vector3.zero, new Vector3(0.022f, 0.046f, 0.022f), outerFlameMat);
        HouseAct3PropController.CreatePrimitivePart(flameRootGo.transform, PrimitiveType.Sphere, "FlameInner",
            new Vector3(0f, -0.005f, 0f), Vector3.zero, new Vector3(0.012f, 0.026f, 0.012f), innerFlameMat);

        GameObject lightGo = new GameObject("CandlePointLight");
        lightGo.transform.SetParent(flameRootGo.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        candlePointLight = lightGo.AddComponent<Light>();
        candlePointLight.type = LightType.Point;
        candlePointLight.color = new Color(1.0f, 0.68f, 0.32f);
        candlePointLight.intensity = 1.95f;
        candlePointLight.range = 9.5f;
        candlePointLight.shadows = LightShadows.None;

        flameRootGo.SetActive(false);
        SetupCandleAudio();
    }

    private void SetupCandleAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.2f;
        audioSource.playOnAwake = false;

        int sr = 44100;
        int pSamples = Mathf.RoundToInt(0.18f * sr);
        float[] pData = new float[pSamples];
        for (int i = 0; i < pSamples; i++)
        {
            float t = (float)i / sr;
            pData[i] = Mathf.Sin(2f * Mathf.PI * 240f * t) * Mathf.Exp(-t * 28f) * 0.45f;
        }
        pickupClip = AudioClip.Create("Candle_Pickup", pSamples, 1, sr, false);
        pickupClip.SetData(pData, 0);

        int lSamples = Mathf.RoundToInt(0.45f * sr);
        float[] lData = new float[lSamples];
        for (int i = 0; i < lSamples; i++)
        {
            float t = (float)i / sr;
            float spark = t < 0.09f ? (Random.value * 2f - 1f) * Mathf.Exp(-t * 36f) * 0.85f : 0f;
            float flare = t >= 0.06f ? (Random.value * 2f - 1f) * Mathf.Exp(-(t - 0.06f) * 8.5f) * 0.42f : 0f;
            lData[i] = Mathf.Clamp(spark + flare, -1f, 1f);
        }
        lighterStrikeClip = AudioClip.Create("Candle_LighterStrike", lSamples, 1, sr, false);
        lighterStrikeClip.SetData(lData, 0);
    }

    private void LateUpdate()
    {
        if (!isPickedUp) return;

        // Subtle left-hand breathing & walk sway
        float t = Time.time;
        float bobY = Mathf.Sin(t * 2.4f) * 0.004f;
        float bobX = Mathf.Cos(t * 1.2f) * 0.003f;
        Vector3 strikeShift = new Vector3(igniteStrikeOffset * 0.08f, igniteStrikeOffset * 0.04f, -igniteStrikeOffset * 0.05f);
        transform.localPosition = leftHandLocalPos + new Vector3(bobX, bobY, 0f) + strikeShift;
        transform.localRotation = Quaternion.Euler(leftHandLocalEuler);

        if (isLit && flameRootGo != null && candlePointLight != null)
        {
            float flicker = Mathf.PerlinNoise(t * 8.5f, 1.3f);
            candlePointLight.intensity = Mathf.Lerp(1.65f, 2.25f, flicker);
            float scaleY = Mathf.Lerp(0.90f, 1.12f, flicker);
            flameRootGo.transform.localScale = new Vector3(1f, scaleY, 1f);
        }
    }

    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;
        if (!isPickedUp && HouseSceneStoryController.IsAct3FindCandleTaskActive)
        {
            return "E to Pick Up Candle";
        }
        if (isPickedUp && !isLit && HouseSceneStoryController.IsAct3LightCandleTaskActive)
        {
            return "E to Light it up";
        }
        return string.Empty;
    }

    public void Interact()
    {
        if (HouseSceneStoryController.IsDialogueActive) return;

        if (!isPickedUp && HouseSceneStoryController.IsAct3FindCandleTaskActive)
        {
            PickUpToLeftHand();
        }
        else if (isPickedUp && !isLit && HouseSceneStoryController.IsAct3LightCandleTaskActive)
        {
            LightUpCandle();
        }
    }

    public void PickUpToLeftHand()
    {
        if (isPickedUp) return;
        isPickedUp = true;
        HeldCandle = this;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        SimpleFPSController fps = Object.FindAnyObjectByType<SimpleFPSController>();
        Camera cam = fps != null && fps.playerCamera != null ? fps.playerCamera : Camera.main;
        if (cam != null)
        {
            transform.SetParent(cam.transform, false);
        }

        // Stand upright in the player's LEFT hand
        if (meshPivot != null)
        {
            meshPivot.localRotation = Quaternion.identity;
        }
        if (saucerPlateGo != null)
        {
            saucerPlateGo.SetActive(true);
        }

        transform.localPosition = leftHandLocalPos;
        transform.localRotation = Quaternion.Euler(leftHandLocalEuler);
        transform.localScale = Vector3.one;

        if (audioSource != null && pickupClip != null)
        {
            audioSource.PlayOneShot(pickupClip, 0.85f);
        }

        if (HouseSceneStoryController.Instance != null)
        {
            HouseSceneStoryController.Instance.NotifyAct3CandlePickedUp(this);
        }
    }

    public void LightUpCandle()
    {
        if (isLit) return;
        isLit = true;
        StartCoroutine(IgniteRoutine());
    }

    private IEnumerator IgniteRoutine()
    {
        // First lighter strike (brief spark in the dark before catching)
        if (audioSource != null && lighterStrikeClip != null)
        {
            audioSource.pitch = 0.94f;
            audioSource.PlayOneShot(lighterStrikeClip, 0.78f);
        }

        float strike1Dur = 0.36f;
        float elapsed = 0f;
        while (elapsed < strike1Dur)
        {
            elapsed += Time.deltaTime;
            igniteStrikeOffset = Mathf.Sin(Mathf.Clamp01(elapsed / strike1Dur) * Mathf.PI) * 0.75f;
            yield return null;
        }
        igniteStrikeOffset = 0f;

        yield return new WaitForSeconds(0.28f);

        // Second lighter strike catches the wick and blooms the warm flame
        if (audioSource != null && lighterStrikeClip != null)
        {
            audioSource.pitch = 1.02f;
            audioSource.PlayOneShot(lighterStrikeClip, 0.98f);
        }

        float strike2Dur = 0.42f;
        elapsed = 0f;
        while (elapsed < strike2Dur)
        {
            elapsed += Time.deltaTime;
            igniteStrikeOffset = Mathf.Sin(Mathf.Clamp01(elapsed / strike2Dur) * Mathf.PI);
            if (elapsed >= 0.16f && flameRootGo != null && !flameRootGo.activeSelf)
            {
                flameRootGo.SetActive(true);
            }
            yield return null;
        }
        igniteStrikeOffset = 0f;

        if (flameRootGo != null)
        {
            flameRootGo.SetActive(true);
        }

        if (HouseSceneStoryController.Instance != null)
        {
            HouseSceneStoryController.Instance.NotifyAct3CandleLit(this);
        }
    }
}

/// <summary>
/// 3D Phone model placed on top of PFB_BedsideTable in the bedroom from the start of Act 3.
/// Picked up with E ("E to Pick Up"), stored in HorrorHotbar, held in the RIGHT hand while the candle stays in the LEFT hand,
/// and animates realistic button pressing + raising to the right ear when calling the Manager.
/// </summary>
public class CabinPhoneInteractable : MonoBehaviour, IInteractable
{
    public static CabinPhoneInteractable HeldPhone { get; set; }

    public bool isPickedUp = false;
    private Transform meshPivot;
    private Renderer screenRenderer;
    private AudioSource audioSource;
    private AudioClip pickupClip;
    private AudioClip[] dtmfBeepClips;
    private AudioClip ringbackToneClip;
    private AudioClip endCallBeepClip;

    private readonly Vector3 rightHandIdlePos = new Vector3(0.26f, -0.22f, 0.42f);
    private readonly Vector3 rightHandIdleEuler = new Vector3(16f, -16f, 6f);

    private readonly Vector3 rightHandDialPos = new Vector3(0.16f, -0.15f, 0.34f);
    private readonly Vector3 rightHandDialEuler = new Vector3(24f, -24f, 4f);

    private readonly Vector3 rightHandEarPos = new Vector3(0.20f, -0.04f, 0.15f);
    private readonly Vector3 rightHandEarEuler = new Vector3(6f, -82f, -12f);

    private Vector3 currentHandLocalPos;
    private Quaternion currentHandLocalRot;
    private bool isAnimatingCallPose = false;

    public void BuildPhoneVisuals()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.02f, 0f);
            col.size = new Vector3(0.30f, 0.18f, 0.30f);
            col.isTrigger = true;
        }

        meshPivot = new GameObject("PhoneMeshPivot").transform;
        meshPivot.SetParent(transform, false);
        meshPivot.localPosition = Vector3.zero;
        meshPivot.localRotation = Quaternion.identity;

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material bodyMat = HouseAct3PropController.CreateLitMaterial(litShader, new Color(0.12f, 0.13f, 0.15f), 0.45f);
        Material bezelMat = HouseAct3PropController.CreateLitMaterial(litShader, new Color(0.22f, 0.24f, 0.28f), 0.55f);
        Material screenMat = HouseAct3PropController.CreateLitMaterial(litShader, new Color(0.04f, 0.05f, 0.06f), 0.40f);

        // Phone chassis lying flat on nightstand (X=width 0.074, Y=thickness 0.012, Z=length 0.148)
        HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Cube, "PhoneBody",
            new Vector3(0f, 0.006f, 0f), Vector3.zero, new Vector3(0.074f, 0.012f, 0.148f), bodyMat);

        // Metallic side trim bezel
        HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Cube, "PhoneBezel",
            new Vector3(0f, 0.006f, 0f), Vector3.zero, new Vector3(0.076f, 0.008f, 0.150f), bezelMat);

        // Glass screen on top face
        GameObject screenGo = HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Cube, "PhoneScreen",
            new Vector3(0f, 0.0125f, 0.006f), Vector3.zero, new Vector3(0.064f, 0.002f, 0.118f), screenMat);
        screenRenderer = screenGo.GetComponent<Renderer>();

        // Earpiece speaker slot
        HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Cube, "EarpieceSlot",
            new Vector3(0f, 0.013f, 0.068f), Vector3.zero, new Vector3(0.022f, 0.002f, 0.004f), bezelMat);

        // Keypad / home button detail
        HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Cylinder, "HomeButton",
            new Vector3(0f, 0.013f, -0.062f), Vector3.zero, new Vector3(0.012f, 0.0015f, 0.012f), bezelMat);

        SetupPhoneAudio();
    }

    private void SetupPhoneAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.1f;
        audioSource.playOnAwake = false;

        int sr = 44100;

        int pSamples = Mathf.RoundToInt(0.14f * sr);
        float[] pData = new float[pSamples];
        for (int i = 0; i < pSamples; i++)
        {
            float t = (float)i / sr;
            pData[i] = Mathf.Sin(2f * Mathf.PI * 320f * t) * Mathf.Exp(-t * 32f) * 0.45f;
        }
        pickupClip = AudioClip.Create("Phone_Pickup", pSamples, 1, sr, false);
        pickupClip.SetData(pData, 0);

        // 4 Dual-Tone Multi-Frequency (DTMF) keypad tones
        float[,] dtmfPairs =
        {
            { 697f, 1209f },
            { 770f, 1336f },
            { 852f, 1477f },
            { 770f, 1209f }
        };
        dtmfBeepClips = new AudioClip[4];
        for (int b = 0; b < 4; b++)
        {
            int bSamples = Mathf.RoundToInt(0.11f * sr);
            float[] bData = new float[bSamples];
            float f1 = dtmfPairs[b, 0];
            float f2 = dtmfPairs[b, 1];
            for (int i = 0; i < bSamples; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Clamp01(1f - t / 0.11f);
                bData[i] = (Mathf.Sin(2f * Mathf.PI * f1 * t) + Mathf.Sin(2f * Mathf.PI * f2 * t)) * 0.22f * env;
            }
            dtmfBeepClips[b] = AudioClip.Create("Phone_DTMF_" + b, bSamples, 1, sr, false);
            dtmfBeepClips[b].SetData(bData, 0);
        }

        // Standard North American phone ringback tone (440 Hz + 480 Hz)
        int rSamples = Mathf.RoundToInt(1.1f * sr);
        float[] rData = new float[rSamples];
        for (int i = 0; i < rSamples; i++)
        {
            float t = (float)i / sr;
            float env = t < 0.95f ? 1f : Mathf.Clamp01((1.1f - t) / 0.15f);
            rData[i] = (Mathf.Sin(2f * Mathf.PI * 440f * t) + Mathf.Sin(2f * Mathf.PI * 480f * t)) * 0.18f * env;
        }
        ringbackToneClip = AudioClip.Create("Phone_Ringback", rSamples, 1, sr, false);
        ringbackToneClip.SetData(rData, 0);

        // Call end double-beep
        int eSamples = Mathf.RoundToInt(0.32f * sr);
        float[] eData = new float[eSamples];
        for (int i = 0; i < eSamples; i++)
        {
            float t = (float)i / sr;
            bool on = (t < 0.11f) || (t > 0.16f && t < 0.27f);
            eData[i] = on ? Mathf.Sin(2f * Mathf.PI * 480f * t) * 0.28f : 0f;
        }
        endCallBeepClip = AudioClip.Create("Phone_EndCall", eSamples, 1, sr, false);
        endCallBeepClip.SetData(eData, 0);
    }

    public void SetScreenActiveGlow(bool active)
    {
        if (screenRenderer != null && screenRenderer.material != null)
        {
            Color c = active ? new Color(0.16f, 0.22f, 0.28f, 1f) : new Color(0.04f, 0.05f, 0.06f, 1f);
            screenRenderer.material.color = c;
            if (screenRenderer.material.HasProperty("_BaseColor"))
            {
                screenRenderer.material.SetColor("_BaseColor", c);
            }
        }
    }

    private void LateUpdate()
    {
        if (!isPickedUp || isAnimatingCallPose) return;

        float t = Time.time;
        float bobY = Mathf.Sin(t * 2.3f + 0.8f) * 0.0035f;
        transform.localPosition = currentHandLocalPos + new Vector3(0f, bobY, 0f);
        transform.localRotation = currentHandLocalRot;
    }

    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;
        if (!isPickedUp && HouseSceneStoryController.IsAct3FindPhoneTaskActive)
        {
            return "E to Pick Up";
        }
        if (isPickedUp && HouseSceneStoryController.IsAct3MakeCallTaskActive)
        {
            return "E to Make a Call";
        }
        return string.Empty;
    }

    public void Interact()
    {
        if (HouseSceneStoryController.IsDialogueActive) return;
        if (!isPickedUp && HouseSceneStoryController.IsAct3FindPhoneTaskActive)
        {
            PickUpToRightHand();
        }
        else if (isPickedUp && HouseSceneStoryController.IsAct3MakeCallTaskActive)
        {
            if (HouseSceneStoryController.Instance != null)
            {
                HouseSceneStoryController.Instance.NotifyAct3CallInitiated();
            }
        }
    }

    public void PickUpToRightHand()
    {
        if (isPickedUp) return;
        isPickedUp = true;
        HeldPhone = this;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        SimpleFPSController fps = Object.FindAnyObjectByType<SimpleFPSController>();
        Camera cam = fps != null && fps.playerCamera != null ? fps.playerCamera : Camera.main;
        if (cam != null)
        {
            transform.SetParent(cam.transform, false);
        }

        // Rotate phone upright in the RIGHT hand facing the player
        if (meshPivot != null)
        {
            meshPivot.localRotation = Quaternion.Euler(-78f, 180f, 0f);
        }

        currentHandLocalPos = rightHandIdlePos;
        currentHandLocalRot = Quaternion.Euler(rightHandIdleEuler);
        transform.localPosition = currentHandLocalPos;
        transform.localRotation = currentHandLocalRot;
        transform.localScale = Vector3.one;

        SetScreenActiveGlow(true);

        if (audioSource != null && pickupClip != null)
        {
            audioSource.PlayOneShot(pickupClip, 0.85f);
        }

        // Store in HorrorHotbar while keeping the candle in the LEFT hand
        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null)
        {
            hotbar.SetSlot(0, "PHONE", GeneratePhoneHotbarIcon());
            hotbar.SelectSlot(0);
        }

        if (HouseSceneStoryController.Instance != null)
        {
            HouseSceneStoryController.Instance.NotifyAct3PhonePickedUp(this);
        }
    }

    /// <summary>
    /// Animates raising the phone in the right hand, pressing keypad buttons with realistic DTMF tones,
    /// and bringing the phone smoothly up to the right ear.
    /// </summary>
    public IEnumerator PlayDialAndRaiseToEarRoutine()
    {
        isAnimatingCallPose = true;
        SetScreenActiveGlow(true);

        // 1. Bring phone up into dialing position in front of right hand
        Vector3 startPos = transform.localPosition;
        Quaternion startRot = transform.localRotation;
        Quaternion dialRot = Quaternion.Euler(rightHandDialEuler);

        float moveDur = 0.35f;
        float elapsed = 0f;
        while (elapsed < moveDur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, elapsed / moveDur);
            transform.localPosition = Vector3.Lerp(startPos, rightHandDialPos, u);
            transform.localRotation = Quaternion.Slerp(startRot, dialRot, u);
            yield return null;
        }

        // 2. Realistic button-pressing taps with DTMF keypad tones
        for (int b = 0; b < 4; b++)
        {
            if (audioSource != null && dtmfBeepClips != null && b < dtmfBeepClips.Length)
            {
                audioSource.PlayOneShot(dtmfBeepClips[b], 0.75f);
            }

            float tapDur = 0.16f;
            float t = 0f;
            while (t < tapDur)
            {
                t += Time.deltaTime;
                float press = Mathf.Sin(Mathf.Clamp01(t / tapDur) * Mathf.PI);
                transform.localPosition = rightHandDialPos + new Vector3(-press * 0.008f, -press * 0.010f, -press * 0.012f);
                transform.localRotation = dialRot * Quaternion.Euler(press * 4.5f, 0f, -press * 2.5f);
                yield return null;
            }
            yield return new WaitForSeconds(0.05f);
        }

        // 3. Smoothly raise phone to right ear
        Vector3 preEarPos = transform.localPosition;
        Quaternion preEarRot = transform.localRotation;
        Quaternion earRot = Quaternion.Euler(rightHandEarEuler);

        float earDur = 0.55f;
        elapsed = 0f;
        while (elapsed < earDur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, elapsed / earDur);
            transform.localPosition = Vector3.Lerp(preEarPos, rightHandEarPos, u);
            transform.localRotation = Quaternion.Slerp(preEarRot, earRot, u);
            yield return null;
        }

        currentHandLocalPos = rightHandEarPos;
        currentHandLocalRot = earRot;
    }

    public void PlayRingbackToneOneShot()
    {
        if (audioSource != null && ringbackToneClip != null)
        {
            audioSource.PlayOneShot(ringbackToneClip, 0.72f);
        }
    }

    /// <summary>
    /// Plays end-call beep and slowly lowers the phone from the right ear back to the normal right-hand held state.
    /// </summary>
    public IEnumerator PlayEndCallAndLowerToNormalRoutine()
    {
        if (audioSource != null && endCallBeepClip != null)
        {
            audioSource.PlayOneShot(endCallBeepClip, 0.75f);
        }

        SetScreenActiveGlow(false);

        Vector3 startPos = transform.localPosition;
        Quaternion startRot = transform.localRotation;
        Quaternion targetRot = Quaternion.Euler(rightHandIdleEuler);

        float dur = 0.75f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            transform.localPosition = Vector3.Lerp(startPos, rightHandIdlePos, u);
            transform.localRotation = Quaternion.Slerp(startRot, targetRot, u);
            yield return null;
        }

        currentHandLocalPos = rightHandIdlePos;
        currentHandLocalRot = targetRot;
        isAnimatingCallPose = false;
    }

    private static Texture2D GeneratePhoneHotbarIcon()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color clear = Color.clear;
        Color body = new Color(0.18f, 0.20f, 0.24f, 1f);
        Color border = new Color(0.55f, 0.60f, 0.66f, 1f);
        Color screen = new Color(0.42f, 0.74f, 0.96f, 1f);
        Color key = new Color(0.75f, 0.78f, 0.82f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (x < 9 || x > 22 || y < 3 || y > 28)
                {
                    tex.SetPixel(x, y, clear);
                    continue;
                }
                if (x == 9 || x == 22 || y == 3 || y == 28)
                {
                    tex.SetPixel(x, y, border);
                    continue;
                }
                if (x >= 11 && x <= 20 && y >= 13 && y <= 25)
                {
                    tex.SetPixel(x, y, screen);
                    continue;
                }
                if (x >= 11 && x <= 20 && y >= 5 && y <= 11 && (x % 3 == 0) && (y % 2 == 1))
                {
                    tex.SetPixel(x, y, key);
                    continue;
                }
                tex.SetPixel(x, y, body);
            }
        }
        tex.Apply();
        return tex;
    }
}

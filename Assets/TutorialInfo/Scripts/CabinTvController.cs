using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Runtime controller attached to PFB_TV in HouseScene:
/// - Fits a flush emissive 3D CRT screen quad and localized bounce light onto the TV mesh.
/// - Renders 5 real-time animated channels (Ch 1-4 atmospheric late-night TV, Ch 5 Breaking News about Route 9 / Jojo's Gas Station).
/// - Spawns the interactive 3D TV Remote prop + return spot on top of PFB_CoffeeTable.
/// - Strictly gated behind HouseSceneStoryController.IsTvTaskUnlocked.
/// </summary>
public class CabinTvController : MonoBehaviour
{
    public static CabinTvController Instance { get; private set; }

    public static bool IsRemoteCursorActive =>
        CabinTvRemoteInteractable.Instance != null && CabinTvRemoteInteractable.Instance.IsCursorUnlockedForRemote;

    public const int MinChannel = 1;
    public const int MaxChannel = 5;

    [Header("TV State")]
    public bool isPoweredOn = false;
    public int currentChannel = 1;

    private const int ScreenWidth = 256;
    private const int ScreenHeight = 160;
    private const float WarmupDuration = 0.34f;
    private const float StaticTransitionDuration = 0.22f;
    private const float OsdVisibleDuration = 2.2f;

    private Texture2D screenTexture;
    private Color32[] screenPixels;
    private Material screenMaterial;
    private GameObject liveScreenQuadObj;
    private Light crtBounceLight;
    private GameObject standbyLedObj;
    private Material standbyLedMat;

    private AudioSource tvAudioSource;
    private AudioClip powerThumpClip;
    private AudioClip staticBurstClip;
    private AudioClip newsChimeClip;
    private AudioClip limitBeepClip;

    private float warmupTimer = 0f;
    private float staticTimer = 0f;
    private float osdTimer = 0f;
    private float sirenAudioTimer = 0f;
    private float nextRenderTime = 0f;
    private bool hasTriggeredNewsDialogue = false;
    private Transform cachedCoffeeTable;

    private Color currentChannelGlowColor = new Color(0.45f, 0.72f, 0.95f);

    /// <summary>
    /// Entry point invoked by HouseSceneStoryController on HouseScene load.
    /// Locates PFB_TV and PFB_CoffeeTable, attaches the CRT screen/audio, and places the remote on the coffee table.
    /// </summary>
    public static void ConfigureLivingRoomTvAndRemote()
    {
        GameObject tvObj = FindSceneObjectByName("PFB_TV");
        GameObject coffeeTableObj = FindSceneObjectByName("PFB_CoffeeTable");

        if (tvObj == null)
        {
            Debug.LogWarning("[CabinTvController] PFB_TV not found in scene.");
            return;
        }

        tvObj.isStatic = false;
        CabinTvController tv = tvObj.GetComponent<CabinTvController>();
        if (tv == null) tv = tvObj.AddComponent<CabinTvController>();

        tv.cachedCoffeeTable = coffeeTableObj != null ? coffeeTableObj.transform : null;
        tv.EnsureTvInitialized();

        if (coffeeTableObj != null)
        {
            SetupCoffeeTableRemoteAndSpot(coffeeTableObj);
        }
        else
        {
            Debug.LogWarning("[CabinTvController] PFB_CoffeeTable not found in scene; remote not placed.");
        }
    }

    private static GameObject FindSceneObjectByName(string exactName)
    {
        GameObject found = GameObject.Find(exactName);
        if (found != null) return found;

        GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && string.Equals(all[i].name, exactName, System.StringComparison.OrdinalIgnoreCase))
            {
                return all[i];
            }
        }
        return null;
    }

    private static void SetupCoffeeTableRemoteAndSpot(GameObject coffeeTableObj)
    {
        if (CabinTvRemoteInteractable.Instance != null) return;

        coffeeTableObj.isStatic = false;
        Transform tableTr = coffeeTableObj.transform;

        // Compute resting position flush on the tabletop near the sofa-facing half of PFB_CoffeeTable
        Renderer tableRenderer = coffeeTableObj.GetComponent<Renderer>();
        Bounds worldBounds = tableRenderer != null
            ? tableRenderer.bounds
            : new Bounds(tableTr.position + Vector3.up * 0.40f, new Vector3(0.8f, 0.45f, 0.8f));

        Vector3 sampleXZ = new Vector3(worldBounds.center.x, worldBounds.max.y + 0.40f, worldBounds.center.z);
        Collider tableCol = coffeeTableObj.GetComponent<Collider>();
        float surfaceY = worldBounds.max.y;

        if (tableCol != null && tableCol.Raycast(new Ray(sampleXZ, Vector3.down), out RaycastHit hit, 1.2f))
        {
            surfaceY = hit.point.y;
        }

        const float remoteHalfHeight = 0.0085f;
        Vector3 remoteWorldPos = new Vector3(worldBounds.center.x + 0.04f, surfaceY + remoteHalfHeight, worldBounds.center.z - 0.06f);

        GameObject sofaObj = FindSceneObjectByName("PFB_SofaSmall");
        Vector3 faceDir = sofaObj != null ? (sofaObj.transform.position - remoteWorldPos) : -Vector3.right;
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude < 0.001f) faceDir = Vector3.forward;
        Quaternion remoteWorldRot = Quaternion.LookRotation(faceDir.normalized, Vector3.up) * Quaternion.Euler(0f, -14f, 0f);

        // 1. Placement Return Spot on PFB_CoffeeTable
        Transform existingSpot = tableTr.Find("Cabin_TvRemoteSpot");
        if (existingSpot != null) Destroy(existingSpot.gameObject);

        GameObject spotGo = new GameObject("Cabin_TvRemoteSpot");
        spotGo.transform.position = remoteWorldPos;
        spotGo.transform.rotation = remoteWorldRot;
        spotGo.transform.SetParent(tableTr, true);

        BoxCollider spotTrigger = spotGo.AddComponent<BoxCollider>();
        spotTrigger.isTrigger = true;
        spotTrigger.center = new Vector3(0f, 0.025f, 0f);
        spotTrigger.size = new Vector3(0.24f, 0.10f, 0.30f);

        CabinTvRemoteSpotInteractable spotComp = spotGo.AddComponent<CabinTvRemoteSpotInteractable>();

        // 2. 3D TV Remote Prop resting on top of PFB_CoffeeTable
        GameObject remoteGo = BuildProceduralRemoteModel("Cabin_TvRemote");
        remoteGo.transform.position = remoteWorldPos;
        remoteGo.transform.rotation = remoteWorldRot;
        remoteGo.transform.SetParent(tableTr, true);

        BoxCollider remoteCol = remoteGo.AddComponent<BoxCollider>();
        remoteCol.isTrigger = true;
        remoteCol.center = new Vector3(0f, 0.006f, 0f);
        remoteCol.size = new Vector3(0.11f, 0.065f, 0.22f);

        CabinTvRemoteInteractable remoteComp = remoteGo.AddComponent<CabinTvRemoteInteractable>();
        remoteComp.BindHomeSpot(tableTr, remoteGo.transform.localPosition, remoteGo.transform.localRotation, remoteGo.transform.localScale);
        spotComp.targetRemote = remoteComp;
    }

    private static GameObject BuildProceduralRemoteModel(string goName)
    {
        GameObject root = new GameObject(goName);

        Material bodyMat = CreateSimpleMaterial("TvRemote_Body_Mat", new Color(0.10f, 0.10f, 0.12f), 0.25f);
        Material trimMat = CreateSimpleMaterial("TvRemote_Trim_Mat", new Color(0.18f, 0.19f, 0.21f), 0.40f);
        Material powerBtnMat = CreateSimpleMaterial("TvRemote_PowerBtn_Mat", new Color(0.76f, 0.14f, 0.14f), 0.35f);
        Material arrowBtnMat = CreateSimpleMaterial("TvRemote_ArrowBtn_Mat", new Color(0.42f, 0.46f, 0.52f), 0.35f);
        Material keyBtnMat = CreateSimpleMaterial("TvRemote_KeyBtn_Mat", new Color(0.22f, 0.23f, 0.26f), 0.2f);
        Material irMat = CreateSimpleMaterial("TvRemote_Ir_Mat", new Color(0.28f, 0.04f, 0.06f), 0.75f);

        // Main chassis (4.8cm wide x 1.5cm tall x 16.4cm long)
        CreatePrimitivePart(root.transform, "Chassis", PrimitiveType.Cube,
            Vector3.zero, new Vector3(0.048f, 0.015f, 0.164f), bodyMat);

        // Recessed upper faceplate
        CreatePrimitivePart(root.transform, "Faceplate", PrimitiveType.Cube,
            new Vector3(0f, 0.0076f, 0.002f), new Vector3(0.040f, 0.002f, 0.146f), trimMat);

        // Glossy front IR blaster lens (+Z is TV-facing end when pointed forward in hand)
        CreatePrimitivePart(root.transform, "IRWindow", PrimitiveType.Cube,
            new Vector3(0f, 0.001f, 0.0825f), new Vector3(0.022f, 0.007f, 0.004f), irMat);

        // Red Power button at top-left
        CreatePrimitivePart(root.transform, "BtnPower", PrimitiveType.Cylinder,
            new Vector3(-0.011f, 0.0092f, 0.056f), new Vector3(0.010f, 0.002f, 0.010f), powerBtnMat);

        // Left & Right Channel Rocker buttons
        CreatePrimitivePart(root.transform, "BtnLeft", PrimitiveType.Cube,
            new Vector3(-0.010f, 0.0095f, 0.032f), new Vector3(0.015f, 0.003f, 0.014f), arrowBtnMat);
        CreatePrimitivePart(root.transform, "BtnRight", PrimitiveType.Cube,
            new Vector3(0.010f, 0.0095f, 0.032f), new Vector3(0.015f, 0.003f, 0.014f), arrowBtnMat);

        // Keypad buttons (3x3 small tactile rubber buttons)
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                float bx = (col - 1) * 0.011f;
                float bz = 0.006f - row * 0.016f;
                CreatePrimitivePart(root.transform, $"Key_{row}_{col}", PrimitiveType.Cube,
                    new Vector3(bx, 0.0090f, bz), new Vector3(0.0075f, 0.0022f, 0.009f), keyBtnMat);
            }
        }

        return root;
    }

    private static GameObject CreatePrimitivePart(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = localScale;

        Collider c = part.GetComponent<Collider>();
        if (c != null) Object.Destroy(c);

        MeshRenderer mr = part.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }
        return part;
    }

    private static Material CreateSimpleMaterial(string matName, Color albedo, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.name = matName;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", albedo);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", albedo);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        return mat;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        EnsureTvInitialized();
    }

    private void EnsureTvInitialized()
    {
        if (screenTexture != null) return;

        SetupAudio();
        SetupScreenQuadAndLight();
        RefreshPowerVisuals();
        RenderScreenNow(0f);
    }

    private void Update()
    {
        if (!isPoweredOn) return;

        if (warmupTimer > 0f)
        {
            warmupTimer = Mathf.Max(0f, warmupTimer - Time.deltaTime);
        }
        if (staticTimer > 0f)
        {
            staticTimer = Mathf.Max(0f, staticTimer - Time.deltaTime);
        }
        if (osdTimer > 0f)
        {
            osdTimer = Mathf.Max(0f, osdTimer - Time.deltaTime);
        }

        // Channel 5 subtle alternating emergency siren light bounce + low audio pulse
        if (currentChannel == 5 && warmupTimer <= 0f)
        {
            bool redPhase = (Mathf.FloorToInt(Time.time * 3.2f) % 2) == 0;
            currentChannelGlowColor = redPhase
                ? new Color(0.95f, 0.26f, 0.24f)
                : new Color(0.28f, 0.56f, 1.0f);

            sirenAudioTimer -= Time.deltaTime;
            if (sirenAudioTimer <= 0f)
            {
                sirenAudioTimer = 2.6f;
                PlayTvClip(newsChimeClip, 0.16f);
            }
        }

        if (crtBounceLight != null)
        {
            float flicker = 0.92f + 0.08f * Mathf.Sin(Time.time * 21f);
            crtBounceLight.color = currentChannelGlowColor;
            crtBounceLight.intensity = (warmupTimer > 0f ? 0.35f : 0.85f) * flicker;
        }

        if (Time.unscaledTime >= nextRenderTime)
        {
            nextRenderTime = Time.unscaledTime + (1f / 24f);
            RenderScreenNow(Time.time);
        }
    }

    public void TogglePower()
    {
        if (!HouseSceneStoryController.IsTvTaskUnlocked) return;
        if (isPoweredOn && HouseSceneStoryController.IsTvPowerOffLocked)
        {
            PlayTvClip(limitBeepClip, 0.25f);
            return;
        }

        isPoweredOn = !isPoweredOn;
        PlayTvClip(powerThumpClip, 0.65f);

        if (isPoweredOn)
        {
            warmupTimer = WarmupDuration;
            staticTimer = 0.14f;
            osdTimer = OsdVisibleDuration;
            HouseSceneStoryController.NotifyTvTurnedOn();
            CheckChannel5StoryTrigger();
        }
        else
        {
            warmupTimer = 0f;
            staticTimer = 0f;
            osdTimer = 0f;
        }

        RefreshPowerVisuals();
        RenderScreenNow(Time.time);
    }

    public void PowerOffSilentlyForSleep()
    {
        if (!isPoweredOn) return;

        isPoweredOn = false;
        warmupTimer = 0f;
        staticTimer = 0f;
        osdTimer = 0f;
        RefreshPowerVisuals();
        RenderScreenNow(Time.time);
    }

    public void ChangeChannel(int delta)
    {
        if (!HouseSceneStoryController.IsTvTaskUnlocked) return;

        if (!isPoweredOn)
        {
            PlayTvClip(limitBeepClip, 0.25f);
            return;
        }

        int target = Mathf.Clamp(currentChannel + delta, MinChannel, MaxChannel);
        if (target == currentChannel)
        {
            PlayTvClip(limitBeepClip, 0.35f);
            osdTimer = OsdVisibleDuration;
            RenderScreenNow(Time.time);
            return;
        }

        currentChannel = target;
        staticTimer = StaticTransitionDuration;
        osdTimer = OsdVisibleDuration;
        PlayTvClip(staticBurstClip, 0.52f);

        CheckChannel5StoryTrigger();
        RenderScreenNow(Time.time);
    }

    private void CheckChannel5StoryTrigger()
    {
        if (isPoweredOn && currentChannel == MaxChannel && !hasTriggeredNewsDialogue)
        {
            hasTriggeredNewsDialogue = true;
            PlayTvClip(newsChimeClip, 0.55f);
            HouseSceneStoryController.NotifyTvChannel5Reached();
        }
    }

    private void RefreshPowerVisuals()
    {
        if (crtBounceLight != null)
        {
            crtBounceLight.enabled = isPoweredOn;
        }

        if (screenMaterial != null)
        {
            Color emissive = isPoweredOn ? new Color(1.20f, 1.20f, 1.20f, 1f) : new Color(0.02f, 0.02f, 0.02f, 1f);
            if (screenMaterial.HasProperty("_EmissionColor")) screenMaterial.SetColor("_EmissionColor", emissive);
            if (screenMaterial.HasProperty("_BaseColor")) screenMaterial.SetColor("_BaseColor", isPoweredOn ? Color.white : new Color(0.08f, 0.09f, 0.11f, 1f));
        }

        if (standbyLedMat != null)
        {
            Color ledCol = isPoweredOn ? new Color(0.22f, 0.92f, 0.35f) : new Color(0.75f, 0.10f, 0.10f);
            if (standbyLedMat.HasProperty("_BaseColor")) standbyLedMat.SetColor("_BaseColor", ledCol);
            if (standbyLedMat.HasProperty("_EmissionColor"))
            {
                standbyLedMat.EnableKeyword("_EMISSION");
                standbyLedMat.SetColor("_EmissionColor", ledCol * (isPoweredOn ? 1.8f : 1.1f));
            }
        }
    }

    // =========================================================================
    // MESH-FITTED 3D CRT SCREEN QUAD ON PFB_TV
    // =========================================================================

    private void SetupScreenQuadAndLight()
    {
        screenTexture = new Texture2D(ScreenWidth, ScreenHeight, TextureFormat.RGBA32, false);
        screenTexture.filterMode = FilterMode.Point;
        screenTexture.wrapMode = TextureWrapMode.Clamp;
        screenPixels = new Color32[ScreenWidth * ScreenHeight];

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

        screenMaterial = new Material(shader);
        screenMaterial.name = "CabinTV_Screen_Mat";
        screenMaterial.mainTexture = screenTexture;
        if (screenMaterial.HasProperty("_BaseMap")) screenMaterial.SetTexture("_BaseMap", screenTexture);
        if (screenMaterial.HasProperty("_BaseColor")) screenMaterial.SetColor("_BaseColor", Color.white);
        if (screenMaterial.HasProperty("_EmissionColor"))
        {
            screenMaterial.EnableKeyword("_EMISSION");
            screenMaterial.SetTexture("_EmissionMap", screenTexture);
            screenMaterial.SetColor("_EmissionColor", Color.white * 1.15f);
        }
        if (screenMaterial.HasProperty("_Cull")) screenMaterial.SetFloat("_Cull", 2f);

        ComputeLocalScreenCorners(out Vector3 localBL, out Vector3 localBR, out Vector3 localTL, out Vector3 localTR, out Vector3 localFrontDir);

        Transform oldQuad = transform.Find("CabinTV_LiveScreenQuad");
        if (oldQuad != null) Destroy(oldQuad.gameObject);

        liveScreenQuadObj = new GameObject("CabinTV_LiveScreenQuad");
        liveScreenQuadObj.transform.SetParent(transform, false);

        MeshFilter qmf = liveScreenQuadObj.AddComponent<MeshFilter>();
        MeshRenderer qmr = liveScreenQuadObj.AddComponent<MeshRenderer>();
        qmr.sharedMaterial = screenMaterial;
        qmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        qmr.receiveShadows = false;

        Mesh quadMesh = new Mesh();
        quadMesh.name = "CabinTV_FittedScreenMesh";
        quadMesh.vertices = new Vector3[] { localBL, localBR, localTL, localTR };
        quadMesh.uv = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };

        bool reverseWinding = Vector3.Dot(Vector3.Cross(localTL - localBL, localBR - localBL), localFrontDir) < 0f;
        quadMesh.triangles = reverseWinding
            ? new int[] { 0, 1, 2, 2, 1, 3 }
            : new int[] { 0, 2, 1, 2, 3, 1 };

        quadMesh.RecalculateNormals();
        quadMesh.RecalculateBounds();
        qmf.sharedMesh = quadMesh;

        // Subtle CRT room bounce light directly in front of the glass center
        Transform oldLight = transform.Find("CabinTV_CrtGlowLight");
        if (oldLight != null) Destroy(oldLight.gameObject);

        GameObject lightGo = new GameObject("CabinTV_CrtGlowLight");
        lightGo.transform.SetParent(transform, false);
        Vector3 localScreenCenter = (localBL + localBR + localTL + localTR) * 0.25f;
        lightGo.transform.localPosition = localScreenCenter + localFrontDir * 0.16f;
        crtBounceLight = lightGo.AddComponent<Light>();
        crtBounceLight.type = LightType.Point;
        crtBounceLight.range = 3.2f;
        crtBounceLight.intensity = 0.85f;
        crtBounceLight.shadows = LightShadows.None;
        crtBounceLight.enabled = isPoweredOn;

        // Tiny standby/power LED dot tucked in the lower-right corner below the screen
        Transform oldLed = transform.Find("CabinTV_StandbyLED");
        if (oldLed != null) Destroy(oldLed.gameObject);

        standbyLedObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        standbyLedObj.name = "CabinTV_StandbyLED";
        standbyLedObj.transform.SetParent(transform, false);
        Destroy(standbyLedObj.GetComponent<Collider>());
        Vector3 screenRight = (localBR - localBL).normalized;
        standbyLedObj.transform.localPosition = localBR + screenRight * -0.02f + Vector3.up * -0.022f + localFrontDir * 0.002f;
        standbyLedObj.transform.localRotation = Quaternion.LookRotation(localFrontDir, Vector3.up);
        standbyLedObj.transform.localScale = new Vector3(0.012f, 0.008f, 0.004f);

        standbyLedMat = CreateSimpleMaterial("CabinTV_StandbyLED_Mat", new Color(0.75f, 0.10f, 0.10f), 0.2f);
        standbyLedObj.GetComponent<MeshRenderer>().sharedMaterial = standbyLedMat;
    }

    private void ComputeLocalScreenCorners(out Vector3 localBL, out Vector3 localBR, out Vector3 localTL, out Vector3 localTR, out Vector3 localFrontDir)
    {
        Vector3 viewerTargetWorld = cachedCoffeeTable != null
            ? cachedCoffeeTable.position
            : (transform.position + transform.forward * 2.0f);
        Vector3 toViewerWorld = viewerTargetWorld - transform.position;
        toViewerWorld.y = 0f;
        if (toViewerWorld.sqrMagnitude < 0.001f) toViewerWorld = transform.forward;

        Vector3 approxFrontLocal = transform.InverseTransformDirection(toViewerWorld.normalized).normalized;
        // Snap onto dominant local cardinal axis so quad stays flush with the TV mesh panel
        localFrontDir = Mathf.Abs(approxFrontLocal.z) >= Mathf.Abs(approxFrontLocal.x)
            ? new Vector3(0f, 0f, Mathf.Sign(approxFrontLocal.z))
            : new Vector3(Mathf.Sign(approxFrontLocal.x), 0f, 0f);

        Vector3 localUp = Vector3.up;
        // Unity Cross(front, up) points to the viewer's right when facing -front
        Vector3 localRight = Vector3.Cross(localFrontDir, localUp).normalized;

        MeshFilter mf = GetComponent<MeshFilter>();
        Bounds b = (mf != null && mf.sharedMesh != null)
            ? mf.sharedMesh.bounds
            : new Bounds(new Vector3(0f, 0.36f, 0f), new Vector3(1.02f, 0.68f, 0.16f));

        float minR = float.MaxValue, maxR = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float bestDepth = float.MinValue;
        bool foundScreenRegion = false;

        if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
        {
            Mesh mesh = mf.sharedMesh;
            Vector3[] verts = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            int[] tris = mesh.triangles;
            float pedestalCutoffY = b.min.y + b.size.y * 0.22f;

            // Pass 1: Match triangles facing viewer inside TV_Lamp_Table dark screen UV block (u < 0.40, v < 0.56)
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                Vector3 v0 = verts[i0], v1 = verts[i1], v2 = verts[i2];
                Vector3 triNormal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                if (Vector3.Dot(triNormal, localFrontDir) < 0.85f) continue;

                Vector3 c = (v0 + v1 + v2) / 3f;
                if (c.y < pedestalCutoffY) continue;

                if (uvs != null && uvs.Length == verts.Length)
                {
                    Vector2 uvC = (uvs[i0] + uvs[i1] + uvs[i2]) / 3f;
                    if (uvC.x > 0.42f || uvC.y > 0.56f) continue;
                }

                Vector3[] triVerts = { v0, v1, v2 };
                for (int k = 0; k < 3; k++)
                {
                    float r = Vector3.Dot(triVerts[k], localRight);
                    float y = triVerts[k].y;
                    float d = Vector3.Dot(triVerts[k], localFrontDir);
                    if (r < minR) minR = r;
                    if (r > maxR) maxR = r;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    if (d > bestDepth) bestDepth = d;
                    foundScreenRegion = true;
                }
            }
        }

        float centerR, halfW, botY, topY, faceDepth;
        if (foundScreenRegion && (maxR - minR) > 0.35f && (maxY - minY) > 0.22f)
        {
            // Inset inside the outer front face bezel (~6.5% sides, ~9% top/bottom frame)
            float spanR = maxR - minR;
            float spanY = maxY - minY;
            float insetR = spanR * 0.065f;
            float insetBot = spanY * 0.105f;
            float insetTop = spanY * 0.085f;

            centerR = (minR + maxR) * 0.5f;
            halfW = Mathf.Max(0.20f, (spanR * 0.5f) - insetR);
            botY = minY + insetBot;
            topY = maxY - insetTop;
            faceDepth = bestDepth + 0.002f;
        }
        else
        {
            centerR = Vector3.Dot(b.center, localRight);
            float meshWidth = Mathf.Abs(Vector3.Dot(b.size, localRight));
            halfW = meshWidth * 0.43f;
            botY = b.min.y + b.size.y * 0.26f;
            topY = b.max.y - b.size.y * 0.07f;
            faceDepth = Vector3.Dot(b.center, localFrontDir) + Mathf.Abs(Vector3.Dot(b.extents, localFrontDir)) + 0.002f;
        }

        Vector3 frontOffset = localFrontDir * faceDepth;
        localBL = localRight * (centerR - halfW) + localUp * botY + frontOffset;
        localBR = localRight * (centerR + halfW) + localUp * botY + frontOffset;
        localTL = localRight * (centerR - halfW) + localUp * topY + frontOffset;
        localTR = localRight * (centerR + halfW) + localUp * topY + frontOffset;
    }

    // =========================================================================
    // PROCEDURAL LIVE CRT RENDERING (CHANNELS 1-5)
    // =========================================================================

    private void RenderScreenNow(float time)
    {
        if (screenTexture == null || screenPixels == null) return;

        if (!isPoweredOn)
        {
            FillRect(0, 0, ScreenWidth, ScreenHeight, new Color32(8, 10, 13, 255));
            ApplyCrtVignette();
            screenTexture.SetPixels32(screenPixels);
            screenTexture.Apply(false);
            return;
        }

        if (warmupTimer > 0f)
        {
            RenderWarmupFrame();
            screenTexture.SetPixels32(screenPixels);
            screenTexture.Apply(false);
            return;
        }

        if (staticTimer > 0f)
        {
            RenderStaticFrame(time, true);
        }
        else
        {
            switch (currentChannel)
            {
                case 1:
                    RenderChannel1Weather(time);
                    break;
                case 2:
                    RenderChannel2LateNightCartoon(time);
                    break;
                case 3:
                    RenderChannel3Wilderness(time);
                    break;
                case 4:
                    RenderChannel4SignalBars(time);
                    break;
                case 5:
                    RenderChannel5BreakingNews(time);
                    break;
            }
            ApplyScanlines(time);
        }

        if (osdTimer > 0f)
        {
            DrawChannelOsdBadge();
        }

        ApplyCrtVignette();
        screenTexture.SetPixels32(screenPixels);
        screenTexture.Apply(false);
    }

    private void RenderWarmupFrame()
    {
        FillRect(0, 0, ScreenWidth, ScreenHeight, new Color32(6, 7, 10, 255));
        float progress = 1f - Mathf.Clamp01(warmupTimer / WarmupDuration);
        int beamH = Mathf.Max(2, Mathf.RoundToInt(progress * progress * ScreenHeight));
        int y0 = (ScreenHeight - beamH) / 2;
        FillRect(8, y0, ScreenWidth - 16, beamH, new Color32(215, 232, 245, 255));
        if (beamH > 10)
        {
            FillRect(12, y0 + 2, ScreenWidth - 24, beamH - 4, new Color32(60, 82, 105, 255));
        }
    }

    private void RenderStaticFrame(float time, bool drawBadge)
    {
        int seedOffset = Mathf.FloorToInt(time * 60f) * 31;
        for (int y = 0; y < ScreenHeight; y++)
        {
            bool tearBand = ((y + seedOffset) % 37) < 4;
            for (int x = 0; x < ScreenWidth; x++)
            {
                int n = HashByte(x, y, seedOffset);
                if (tearBand) n = Mathf.Min(255, n + 70);
                byte v = (byte)n;
                screenPixels[y * ScreenWidth + x] = new Color32(v, v, (byte)Mathf.Min(255, v + 8), 255);
            }
        }
        currentChannelGlowColor = new Color(0.75f, 0.78f, 0.85f);
        if (drawBadge) DrawChannelOsdBadge();
    }

    // CH 1: Local Weather Doppler Radar
    private void RenderChannel1Weather(float time)
    {
        currentChannelGlowColor = new Color(0.20f, 0.62f, 0.58f);
        FillRect(0, 0, ScreenWidth, ScreenHeight, new Color32(10, 24, 44, 255));

        // Top station bar
        FillRect(0, 140, ScreenWidth, 20, new Color32(16, 48, 86, 255));
        DrawPixelText("KWRN-4 LOCAL RADAR", 10, 146, new Color32(240, 248, 255, 255), 1);
        DrawPixelText("11C FOG", 198, 146, new Color32(130, 235, 255, 255), 1);

        // Radar scope on left (cx=82, cy=80, r=50)
        const int cx = 82, cy = 80, r = 50;
        DrawFilledCircle(cx, cy, r, new Color32(12, 34, 38, 255));
        DrawCircleOutline(cx, cy, r, new Color32(45, 130, 115, 255));
        DrawCircleOutline(cx, cy, 32, new Color32(28, 75, 70, 255));
        DrawCircleOutline(cx, cy, 16, new Color32(28, 75, 70, 255));

        // Storm blobs drifting across radar
        int drift = Mathf.RoundToInt((time * 8f) % 64f) - 32;
        DrawFilledCircle(cx - 12 + drift / 2, cy + 12, 16, new Color32(45, 165, 65, 255));
        DrawFilledCircle(cx - 8 + drift / 2, cy + 14, 8, new Color32(220, 200, 45, 255));
        DrawFilledCircle(cx + 10 + drift / 3, cy - 14, 13, new Color32(38, 140, 62, 255));

        // Rotating sweep arm
        float ang = time * 1.9f;
        int sx = cx + Mathf.RoundToInt(Mathf.Cos(ang) * r);
        int sy = cy + Mathf.RoundToInt(Mathf.Sin(ang) * r);
        DrawLine(cx, cy, sx, sy, new Color32(140, 255, 165, 255));

        // Right forecast box
        FillRect(146, 36, 100, 96, new Color32(14, 34, 62, 255));
        DrawBorder(146, 36, 100, 96, new Color32(72, 132, 185, 255));
        DrawPixelText("PINEWOOD VAL", 154, 116, new Color32(235, 240, 250, 255), 1);
        DrawPixelText("VISIB: 25M", 154, 96, new Color32(255, 215, 95, 255), 1);
        DrawPixelText("WIND:  NW 9", 154, 78, new Color32(170, 215, 245, 255), 1);
        DrawPixelText("HUMID: 94 PCT", 154, 60, new Color32(170, 215, 245, 255), 1);
        DrawPixelText("FOG ADVISORY", 154, 44, new Color32(255, 115, 80, 255), 1);

        // Scrolling yellow ticker at bottom
        FillRect(0, 0, ScreenWidth, 24, new Color32(180, 145, 24, 255));
        string ticker = "HEAVY FOG ADVISORY ALONG ROUTE 9 UNTIL 05:00  --  DRIVE WITH LOW BEAMS  --  ";
        int scrollX = -Mathf.FloorToInt(time * 38f) % (ticker.Length * 6);
        DrawPixelText(ticker + ticker, scrollX, 8, new Color32(18, 22, 28, 255), 1);
    }

    // CH 2: Late-Night Retro Animated Show
    private void RenderChannel2LateNightCartoon(float time)
    {
        currentChannelGlowColor = new Color(0.55f, 0.38f, 0.78f);
        FillRect(0, 0, ScreenWidth, ScreenHeight, new Color32(34, 18, 56, 255));

        // Starry retro cartoon sky
        for (int s = 0; s < 24; s++)
        {
            int sx = (s * 43 + 17) % ScreenWidth;
            int sy = 56 + ((s * 29 + 11) % 92);
            bool twinkle = ((Mathf.FloorToInt(time * 3f) + s) % 5) == 0;
            FillRect(sx, sy, twinkle ? 3 : 2, twinkle ? 3 : 2, new Color32(255, 248, 175, 255));
        }

        // Stylized crescent moon
        int moonY = 112 + Mathf.RoundToInt(Mathf.Sin(time * 1.4f) * 5f);
        DrawFilledCircle(54, moonY, 18, new Color32(250, 232, 125, 255));
        DrawFilledCircle(61, moonY + 3, 15, new Color32(34, 18, 56, 255));

        // Bouncing cartoon ghost mascot on right side
        int gx = 166 + Mathf.RoundToInt(Mathf.Sin(time * 2.1f) * 26f);
        int gy = 74 + Mathf.RoundToInt(Mathf.Abs(Mathf.Sin(time * 3.4f)) * 18f);
        DrawFilledCircle(gx, gy + 10, 15, new Color32(235, 242, 250, 255));
        FillRect(gx - 15, gy - 8, 30, 18, new Color32(235, 242, 250, 255));
        FillRect(gx - 7, gy + 8, 4, 5, new Color32(22, 24, 32, 255));
        FillRect(gx + 3, gy + 8, 4, 5, new Color32(22, 24, 32, 255));

        // Rolling teal hills
        FillRect(0, 24, ScreenWidth, 26, new Color32(26, 66, 62, 255));
        FillRect(0, 0, ScreenWidth, 24, new Color32(18, 14, 32, 255));
        DrawPixelText("MIDNIGHT CARTOON HOUR", 18, 8, new Color32(255, 145, 220, 255), 1);
    }

    // CH 3: Late-Night Wilderness Documentary
    private void RenderChannel3Wilderness(float time)
    {
        currentChannelGlowColor = new Color(0.18f, 0.34f, 0.56f);
        FillRect(0, 0, ScreenWidth, ScreenHeight, new Color32(9, 15, 28, 255));

        // Pale dim moon
        DrawFilledCircle(206, 122, 14, new Color32(210, 222, 235, 255));

        // Dark pine tree silhouettes across bottom
        FillRect(0, 0, ScreenWidth, 34, new Color32(6, 12, 10, 255));
        for (int t = 0; t < 11; t++)
        {
            int tx = 14 + t * 23;
            int treeH = 52 + (t % 3) * 16;
            for (int row = 0; row < treeH; row += 4)
            {
                int halfW = Mathf.Max(2, (treeH - row) / 4);
                FillRect(tx - halfW, 30 + row, halfW * 2, 4, new Color32(10, 22, 18, 255));
            }
        }

        // Eerie blinking eyes between trees periodically
        float blinkCycle = time % 4.5f;
        if (blinkCycle > 1.2f && blinkCycle < 3.6f)
        {
            FillRect(118, 56, 3, 2, new Color32(235, 245, 165, 255));
            FillRect(126, 56, 3, 2, new Color32(235, 245, 165, 255));
        }

        // Drifting snow particles
        for (int p = 0; p < 28; p++)
        {
            int px = (p * 37 + Mathf.FloorToInt(time * (14f + (p % 3) * 4f))) % ScreenWidth;
            int py = 28 + (ScreenHeight - 28 - ((p * 23 + Mathf.FloorToInt(time * (22f + (p % 4) * 5f))) % (ScreenHeight - 28)));
            FillRect(px, py, 2, 2, new Color32(205, 220, 238, 255));
        }

        FillRect(0, 0, ScreenWidth, 24, new Color32(8, 10, 14, 255));
        DrawPixelText("...THE WOODS STAY QUIET PAST 2 AM.", 16, 8, new Color32(220, 225, 210, 255), 1);
    }

    // CH 4: Off-Air SMPTE Color Bars & Signal Loss
    private void RenderChannel4SignalBars(float time)
    {
        currentChannelGlowColor = new Color(0.62f, 0.62f, 0.68f);

        // Classic 7 vertical SMPTE color bars
        Color32[] bars = new Color32[]
        {
            new Color32(195, 195, 195, 255),
            new Color32(205, 200, 38, 255),
            new Color32(40, 195, 200, 255),
            new Color32(45, 195, 45, 255),
            new Color32(190, 42, 190, 255),
            new Color32(195, 38, 38, 255),
            new Color32(38, 48, 200, 255)
        };

        int barW = ScreenWidth / bars.Length;
        int glitchSeed = Mathf.FloorToInt(time * 24f);
        bool heavySnowPulse = ((Mathf.FloorToInt(time * 2.5f)) % 6) == 0;

        for (int i = 0; i < bars.Length; i++)
        {
            int bx = i * barW;
            int bw = (i == bars.Length - 1) ? (ScreenWidth - bx) : barW;
            FillRect(bx, 34, bw, ScreenHeight - 34, bars[i]);
        }
        FillRect(0, 0, ScreenWidth, 34, new Color32(22, 24, 30, 255));

        if (heavySnowPulse)
        {
            RenderStaticFrame(time, false);
        }

        // Rolling horizontal interference line
        int rollY = ScreenHeight - ((Mathf.FloorToInt(time * 95f)) % ScreenHeight);
        FillRect(0, rollY, ScreenWidth, 5, new Color32(225, 225, 235, 255));

        // Centered "NO SIGNAL" card
        FillRect(68, 62, 120, 36, new Color32(12, 14, 18, 255));
        DrawBorder(68, 62, 120, 36, new Color32(210, 210, 210, 255));
        bool blink = (glitchSeed % 16) < 11;
        if (blink)
        {
            DrawPixelText("NO SIGNAL", 101, 82, new Color32(245, 245, 245, 255), 1);
        }
        DrawPixelText("CH 04 - OFF AIR", 83, 68, new Color32(175, 185, 195, 255), 1);
    }

    // CH 5: Breaking News — Car Crash Near Jojo's Gas Station
    private void RenderChannel5BreakingNews(float time)
    {
        bool flash = (Mathf.FloorToInt(time * 3.2f) % 2) == 0;
        currentChannelGlowColor = flash ? new Color(0.95f, 0.25f, 0.22f) : new Color(0.26f, 0.54f, 1.0f);

        // Dark foggy highway night backdrop
        FillRect(0, 0, ScreenWidth, ScreenHeight, new Color32(12, 14, 22, 255));
        FillRect(0, 26, ScreenWidth, 34, new Color32(26, 28, 34, 255)); // Asphalt road surface

        // Center road dashed yellow line
        for (int dx = 8; dx < ScreenWidth; dx += 24)
        {
            FillRect(dx, 41, 12, 2, new Color32(175, 145, 42, 255));
        }

        // Station Canopy silhouette in background ("JOJO'S")
        FillRect(164, 62, 74, 34, new Color32(18, 22, 28, 255));
        FillRect(160, 92, 82, 10, new Color32(195, 142, 34, 255)); // Lit station canopy fascia
        DrawPixelText("JOJO'S GAS", 171, 94, new Color32(24, 18, 10, 255), 1);
        FillRect(172, 62, 6, 30, new Color32(95, 98, 104, 255));
        FillRect(226, 62, 6, 30, new Color32(95, 98, 104, 255));

        // Guardrail & crashed car tilted off the roadside on the left-center
        FillRect(10, 56, 130, 4, new Color32(130, 136, 142, 255));
        FillRect(78, 52, 24, 8, new Color32(18, 20, 24, 255)); // Smashed gap in guardrail
        FillRect(52, 46, 46, 14, new Color32(82, 26, 26, 255)); // Wrecked car body
        FillRect(62, 58, 24, 9, new Color32(55, 18, 18, 255)); // Roof
        FillRect(94, 48, 8, 5, new Color32(245, 220, 110, 255)); // Flickering broken headlight

        // Flashing Red & Blue Siren Glows
        Color32 redFlash = flash ? new Color32(255, 55, 55, 255) : new Color32(115, 22, 25, 255);
        Color32 blueFlash = !flash ? new Color32(75, 145, 255, 255) : new Color32(22, 42, 110, 255);
        DrawFilledCircle(122, 76, flash ? 18 : 10, redFlash);
        DrawFilledCircle(140, 76, !flash ? 18 : 10, blueFlash);
        FillRect(116, 48, 34, 14, new Color32(225, 232, 242, 255)); // Police cruiser silhouette

        // Top Breaking News Header Bar
        Color32 bannerCol = flash ? new Color32(175, 20, 24, 255) : new Color32(138, 16, 20, 255);
        FillRect(0, 138, ScreenWidth, 22, bannerCol);
        DrawPixelText("WNTV-7", 8, 145, new Color32(255, 225, 95, 255), 1);
        DrawPixelText("BREAKING NEWS", 76, 145, new Color32(255, 255, 255, 255), 1);

        // Live Camcorder REC Bug (upper-left below banner)
        FillRect(8, 121, 44, 12, new Color32(15, 15, 18, 255));
        if (flash) DrawFilledCircle(14, 127, 3, new Color32(245, 45, 45, 255));
        DrawPixelText("LIVE", 21, 124, new Color32(245, 245, 245, 255), 1);

        // Bottom Scrolling News Ticker
        FillRect(0, 0, ScreenWidth, 24, new Color32(160, 20, 24, 255));
        FillRect(0, 23, ScreenWidth, 2, new Color32(245, 205, 55, 255));
        string newsTicker = "CAR ACCIDENT REPORTED NEAR JOJO'S GAS STATION ON ROUTE 9  --  AVOID AREA  --  ";
        int scrollX = -Mathf.FloorToInt(time * 42f) % (newsTicker.Length * 6);
        DrawPixelText(newsTicker + newsTicker, scrollX, 8, new Color32(255, 250, 235, 255), 1);
    }

    private void DrawChannelOsdBadge()
    {
        FillRect(194, 114, 54, 18, new Color32(10, 18, 14, 255));
        DrawBorder(194, 114, 54, 18, new Color32(85, 235, 120, 255));
        DrawPixelText($"CH 0{currentChannel}", 204, 119, new Color32(110, 255, 145, 255), 1);
    }

    private void ApplyScanlines(float time)
    {
        int noiseSeed = Mathf.FloorToInt(time * 30f);
        for (int y = 0; y < ScreenHeight; y++)
        {
            bool dimRow = (y % 2) == 0;
            for (int x = 0; x < ScreenWidth; x++)
            {
                Color32 c = screenPixels[y * ScreenWidth + x];
                int grain = (HashByte(x, y, noiseSeed) & 15) - 7;
                int scale = dimRow ? 225 : 255;
                byte r = (byte)Mathf.Clamp((c.r * scale) / 255 + grain, 0, 255);
                byte g = (byte)Mathf.Clamp((c.g * scale) / 255 + grain, 0, 255);
                byte b = (byte)Mathf.Clamp((c.b * scale) / 255 + grain, 0, 255);
                screenPixels[y * ScreenWidth + x] = new Color32(r, g, b, 255);
            }
        }
    }

    private void ApplyCrtVignette()
    {
        for (int y = 0; y < ScreenHeight; y++)
        {
            float ny = (y / (float)(ScreenHeight - 1)) * 2f - 1f;
            for (int x = 0; x < ScreenWidth; x++)
            {
                float nx = (x / (float)(ScreenWidth - 1)) * 2f - 1f;
                float edge = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                if (edge > 0.88f)
                {
                    float factor = Mathf.Clamp01(1f - (edge - 0.88f) * 5.2f);
                    Color32 c = screenPixels[y * ScreenWidth + x];
                    screenPixels[y * ScreenWidth + x] = new Color32(
                        (byte)(c.r * factor),
                        (byte)(c.g * factor),
                        (byte)(c.b * factor),
                        255);
                }
            }
        }
    }

    // =========================================================================
    // PROCEDURAL PIXEL PRIMITIVES & FONT RENDERER
    // =========================================================================

    private static int HashByte(int x, int y, int seed)
    {
        int h = (x * 374761393) ^ (y * 668265263) ^ (seed * 1274126177);
        h = (h ^ (h >> 13)) * 1274126177;
        return (h ^ (h >> 16)) & 255;
    }

    private void FillRect(int x, int y, int w, int h, Color32 c)
    {
        int x0 = Mathf.Clamp(x, 0, ScreenWidth);
        int x1 = Mathf.Clamp(x + w, 0, ScreenWidth);
        int y0 = Mathf.Clamp(y, 0, ScreenHeight);
        int y1 = Mathf.Clamp(y + h, 0, ScreenHeight);

        for (int py = y0; py < y1; py++)
        {
            int row = py * ScreenWidth;
            for (int px = x0; px < x1; px++) screenPixels[row + px] = c;
        }
    }

    private void DrawBorder(int x, int y, int w, int h, Color32 c)
    {
        FillRect(x, y, w, 1, c);
        FillRect(x, y + h - 1, w, 1, c);
        FillRect(x, y, 1, h, c);
        FillRect(x + w - 1, y, 1, h, c);
    }

    private void DrawFilledCircle(int cx, int cy, int r, Color32 c)
    {
        int r2 = r * r;
        int y0 = Mathf.Clamp(cy - r, 0, ScreenHeight - 1);
        int y1 = Mathf.Clamp(cy + r, 0, ScreenHeight - 1);
        int x0 = Mathf.Clamp(cx - r, 0, ScreenWidth - 1);
        int x1 = Mathf.Clamp(cx + r, 0, ScreenWidth - 1);
        for (int y = y0; y <= y1; y++)
        {
            int dy = y - cy;
            for (int x = x0; x <= x1; x++)
            {
                int dx = x - cx;
                if (dx * dx + dy * dy <= r2) screenPixels[y * ScreenWidth + x] = c;
            }
        }
    }

    private void DrawCircleOutline(int cx, int cy, int r, Color32 c)
    {
        int r2Min = (r - 1) * (r - 1);
        int r2Max = r * r;
        for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(ScreenHeight - 1, cy + r); y++)
        {
            int dy = y - cy;
            for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(ScreenWidth - 1, cx + r); x++)
            {
                int d2 = (x - cx) * (x - cx) + dy * dy;
                if (d2 >= r2Min && d2 <= r2Max) screenPixels[y * ScreenWidth + x] = c;
            }
        }
    }

    private void DrawLine(int x0, int y0, int x1, int y1, Color32 c)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        if (steps <= 0) return;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int px = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int py = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            if (px >= 0 && px < ScreenWidth && py >= 0 && py < ScreenHeight)
            {
                screenPixels[py * ScreenWidth + px] = c;
            }
        }
    }

    private void DrawPixelText(string text, int startX, int startY, Color32 color, int scale = 1)
    {
        if (string.IsNullOrEmpty(text)) return;
        int curX = startX;
        for (int i = 0; i < text.Length; i++)
        {
            byte[] glyph = GetGlyph5x7(char.ToUpperInvariant(text[i]));
            for (int row = 0; row < 7; row++)
            {
                byte bits = glyph[row];
                int py = startY + (6 - row) * scale;
                for (int col = 0; col < 5; col++)
                {
                    if ((bits & (1 << (4 - col))) != 0)
                    {
                        FillRect(curX + col * scale, py, scale, scale, color);
                    }
                }
            }
            curX += 6 * scale;
        }
    }

    private static byte[] GetGlyph5x7(char c)
    {
        switch (c)
        {
            case 'A': return new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
            case 'B': return new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E };
            case 'C': return new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E };
            case 'D': return new byte[] { 0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E };
            case 'E': return new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F };
            case 'F': return new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 };
            case 'G': return new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F };
            case 'H': return new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
            case 'I': return new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E };
            case 'J': return new byte[] { 0x07, 0x02, 0x02, 0x02, 0x12, 0x12, 0x0C };
            case 'K': return new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 };
            case 'L': return new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F };
            case 'M': return new byte[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 };
            case 'N': return new byte[] { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 };
            case 'O': return new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
            case 'P': return new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 };
            case 'Q': return new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D };
            case 'R': return new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 };
            case 'S': return new byte[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E };
            case 'T': return new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 };
            case 'U': return new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
            case 'V': return new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 };
            case 'W': return new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 };
            case 'X': return new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 };
            case 'Y': return new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 };
            case 'Z': return new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F };
            case '0': return new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E };
            case '1': return new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E };
            case '2': return new byte[] { 0x0E, 0x11, 0x01, 0x06, 0x08, 0x10, 0x1F };
            case '3': return new byte[] { 0x1F, 0x02, 0x04, 0x06, 0x01, 0x11, 0x0E };
            case '4': return new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 };
            case '5': return new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E };
            case '6': return new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E };
            case '7': return new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 };
            case '8': return new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E };
            case '9': return new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C };
            case ':': return new byte[] { 0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00 };
            case '.': return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C };
            case '-': return new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 };
            case '\'': return new byte[] { 0x04, 0x04, 0x08, 0x00, 0x00, 0x00, 0x00 };
            default: return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        }
    }

    // =========================================================================
    // PROCEDURAL TV & REMOTE AUDIO CLIPS
    // =========================================================================

    private void SetupAudio()
    {
        tvAudioSource = gameObject.GetComponent<AudioSource>();
        if (tvAudioSource == null) tvAudioSource = gameObject.AddComponent<AudioSource>();
        tvAudioSource.spatialBlend = 0.85f;
        tvAudioSource.minDistance = 1.5f;
        tvAudioSource.maxDistance = 14f;
        tvAudioSource.rolloffMode = AudioRolloffMode.Linear;
        tvAudioSource.playOnAwake = false;

        int sr = 44100;

        // Power thump / CRT degauss hum
        powerThumpClip = BuildClip("TV_PowerThump", (int)(sr * 0.28f), sr, (t, i) =>
        {
            float thump = Mathf.Sin(2f * Mathf.PI * 75f * t) * Mathf.Exp(-t * 20f) * 0.6f;
            float ring = Mathf.Sin(2f * Mathf.PI * 195f * t) * Mathf.Exp(-t * 10f) * 0.2f;
            return thump + ring;
        });

        // Static channel switch burst
        staticBurstClip = BuildClip("TV_StaticBurst", (int)(sr * 0.20f), sr, (t, i) =>
        {
            float click = Mathf.Sin(2f * Mathf.PI * 1100f * t) * Mathf.Exp(-t * 80f) * 0.35f;
            float snow = (Random.value * 2f - 1f) * Mathf.Exp(-t * 11f) * 0.42f;
            return click + snow;
        });

        // News chime / siren alert
        newsChimeClip = BuildClip("TV_NewsChime", (int)(sr * 0.46f), sr, (t, i) =>
        {
            float f = t < 0.14f ? 660f : (t < 0.28f ? 880f : 990f);
            float lt = t < 0.14f ? t : (t < 0.28f ? t - 0.14f : t - 0.28f);
            return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-lt * 10f) * 0.40f;
        });

        // Quiet button limit beep
        limitBeepClip = BuildClip("TV_LimitBeep", (int)(sr * 0.06f), sr, (t, i) =>
            Mathf.Sin(2f * Mathf.PI * 420f * t) * Mathf.Exp(-t * 45f) * 0.30f);
    }

    private static AudioClip BuildClip(string clipName, int samples, int sr, System.Func<float, int, float> generator)
    {
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            data[i] = Mathf.Clamp(generator(i / (float)sr, i), -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(clipName, samples, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void PlayTvClip(AudioClip clip, float volume)
    {
        if (tvAudioSource != null && clip != null) tvAudioSource.PlayOneShot(clip, volume);
    }
}

/// <summary>
/// Interactive 3D TV Remote prop placed on PFB_CoffeeTable:
/// - Equips into HorrorHotbar ("REMOTE") as a 1st-person hand viewmodel.
/// - Displays a clickable/keyboard-controlled Remote Control UI on the right side of the screen
///   with Power On/Off and Left/Right channel buttons across 5 channels.
/// </summary>
public class CabinTvRemoteInteractable : MonoBehaviour, IInteractable
{
    public static CabinTvRemoteInteractable Instance { get; private set; }

    private Transform homeParent;
    private Vector3 homeLocalPos;
    private Quaternion homeLocalRot;
    private Vector3 homeLocalScale;

    private bool isPickedUp = false;
    private int assignedSlot = -1;
    private Transform cameraTransform;
    private Renderer[] renderers;
    private Collider[] colliders;

    private bool cursorToggleActive = false;
    private bool wasRmbHeld = false;
    private float pressRecoilTimer = 0f;
    private float buttonFlashTimer = 0f;
    private int lastPressedButton = 0; // 1=Power, 2=Left, 3=Right

    private Texture2D uiPixelTex;
    private Font uiFont;
    private GUIStyle headerStyle;
    private GUIStyle readoutStyle;
    private GUIStyle btnStyle;
    private GUIStyle hintStyle;

    private AudioSource audioSource;
    private AudioClip clickClip;
    private AudioClip placeClip;

    public bool IsPickedUp => isPickedUp;
    public bool IsCursorUnlockedForRemote { get; private set; }

    public bool IsEquippedActive
    {
        get
        {
            if (!isPickedUp) return false;
            return HorrorHotbar.Instance == null || HorrorHotbar.Instance.IsSlotActive(assignedSlot);
        }
    }

    public void BindHomeSpot(Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale)
    {
        homeParent = parent;
        homeLocalPos = localPos;
        homeLocalRot = localRot;
        homeLocalScale = localScale;
    }

    private void Awake()
    {
        Instance = this;
        RefreshCachedComponents();
        SetupAudio();
    }

    private void Start()
    {
        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null)
        {
            hotbar.OnSlotChanged -= OnHotbarSlotChanged;
            hotbar.OnSlotChanged += OnHotbarSlotChanged;
        }
    }

    private void OnDestroy()
    {
        if (HorrorHotbar.Instance != null) HorrorHotbar.Instance.OnSlotChanged -= OnHotbarSlotChanged;
        ReleaseRemoteCursor();
    }

    private void RefreshCachedComponents()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnHotbarSlotChanged(int activeSlot)
    {
        if (!isPickedUp) return;
        bool visible = activeSlot == assignedSlot;
        SetVisible(visible);
        if (!visible) ReleaseRemoteCursor();
    }

    public void SetVisible(bool visible)
    {
        if (renderers == null) RefreshCachedComponents();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = visible;
        }
    }

    public string GetPromptText()
    {
        if (isPickedUp) return string.Empty;
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;
        if (!HouseSceneStoryController.IsTvTaskUnlocked) return string.Empty;
        if (HorrorHotbar.IsInventoryFull) return "Inventory Full";
        return "E to Pick Up TV Remote";
    }

    public void Interact()
    {
        if (isPickedUp) return;
        if (HouseSceneStoryController.IsDialogueActive) return;
        if (!HouseSceneStoryController.IsTvTaskUnlocked) return;
        if (HorrorHotbar.IsInventoryFull) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return;

        EquipToCamera(cam.transform);
    }

    private void EquipToCamera(Transform camTransform)
    {
        cameraTransform = camTransform;
        isPickedUp = true;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = false;
        }

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        assignedSlot = 0;
        if (hotbar != null)
        {
            for (int s = 0; s < hotbar.slotCount; s++)
            {
                if (!hotbar.HasItem(s))
                {
                    assignedSlot = s;
                    break;
                }
            }
            hotbar.SetSlot(assignedSlot, "REMOTE");
            hotbar.SetActiveSlot(assignedSlot);
        }

        transform.SetParent(cameraTransform, false);
        ApplyViewmodelTransform(0f);
        SetVisible(true);
        PlayClip(placeClip, 0.55f);
    }

    public void PutDownOnCoffeeTable()
    {
        if (!isPickedUp || homeParent == null) return;

        ReleaseRemoteCursor();
        isPickedUp = false;

        if (HorrorHotbar.Instance != null && assignedSlot >= 0)
        {
            HorrorHotbar.Instance.ClearSlot(assignedSlot);
        }
        assignedSlot = -1;

        transform.SetParent(homeParent, false);
        transform.localPosition = homeLocalPos;
        transform.localRotation = homeLocalRot;
        transform.localScale = homeLocalScale;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = true;
        }

        SetVisible(true);
        PlayClip(placeClip, 0.65f);
    }

    public static void ForceClearForSceneTransition()
    {
        if (Instance != null)
        {
            Instance.ReleaseRemoteCursor();
            Instance.isPickedUp = false;
            if (HorrorHotbar.Instance != null && Instance.assignedSlot >= 0)
            {
                HorrorHotbar.Instance.ClearSlot(Instance.assignedSlot);
            }
            Instance.assignedSlot = -1;
            Instance.SetVisible(false);
        }

        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.ClearItem("REMOTE");
        }
    }

    public void ReleaseRemoteCursor()
    {
        cursorToggleActive = false;
        wasRmbHeld = false;
        if (IsCursorUnlockedForRemote)
        {
            IsCursorUnlockedForRemote = false;
            if (!InteractableCashierPC.IsPosScreenOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void Update()
    {
        bool canOperate = IsEquippedActive &&
                          HouseSceneStoryController.IsTvTaskUnlocked &&
                          !HouseSceneStoryController.IsDialogueActive &&
                          !InteractableCashierPC.IsPosScreenOpen;

        if (!canOperate)
        {
            ReleaseRemoteCursor();
            return;
        }

        if (buttonFlashTimer > 0f) buttonFlashTimer = Mathf.Max(0f, buttonFlashTimer - Time.deltaTime);

        // Cursor toggle via Tab or holding Right Mouse Button so players can click the Remote UI buttons
        if (WasTabPressed())
        {
            cursorToggleActive = !cursorToggleActive;
        }
        else if (cursorToggleActive && WasKeyDown(KeyCode.Escape))
        {
            cursorToggleActive = false;
        }

        bool rmbHeld = IsRightMouseHeld();
        bool wantCursor = cursorToggleActive || rmbHeld;

        if (wantCursor)
        {
            if (!IsCursorUnlockedForRemote || Cursor.lockState != CursorLockMode.None)
            {
                IsCursorUnlockedForRemote = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        else if (IsCursorUnlockedForRemote || wasRmbHeld)
        {
            IsCursorUnlockedForRemote = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        wasRmbHeld = rmbHeld;

        // Direct keyboard shortcuts (F for Power, Left/Right Arrows for channels)
        if (WasKeyDown(KeyCode.F)) TriggerPowerButton();
        else if (WasKeyDown(KeyCode.LeftArrow)) TriggerChannelButton(-1);
        else if (WasKeyDown(KeyCode.RightArrow)) TriggerChannelButton(1);
    }

    private void LateUpdate()
    {
        if (!isPickedUp || cameraTransform == null || !IsEquippedActive) return;

        if (pressRecoilTimer > 0f) pressRecoilTimer = Mathf.Max(0f, pressRecoilTimer - Time.deltaTime);
        ApplyViewmodelTransform(pressRecoilTimer / 0.14f);
    }

    private void ApplyViewmodelTransform(float recoilWeight)
    {
        Vector3 basePos = new Vector3(0.24f, -0.22f, 0.42f);
        Vector3 baseEuler = new Vector3(26f, -14f, 4f);

        transform.localPosition = basePos + new Vector3(0f, -0.008f * recoilWeight, 0.015f * recoilWeight);
        transform.localRotation = Quaternion.Euler(baseEuler + new Vector3(-6f * recoilWeight, 0f, 0f));
        transform.localScale = Vector3.one;
    }

    private void TriggerPowerButton()
    {
        if (CabinTvController.Instance == null) return;
        pressRecoilTimer = 0.14f;
        buttonFlashTimer = 0.16f;
        lastPressedButton = 1;
        PlayClip(clickClip, 0.70f);
        CabinTvController.Instance.TogglePower();
    }

    private void TriggerChannelButton(int delta)
    {
        if (CabinTvController.Instance == null) return;
        pressRecoilTimer = 0.14f;
        buttonFlashTimer = 0.16f;
        lastPressedButton = delta < 0 ? 2 : 3;
        PlayClip(clickClip, 0.60f);
        CabinTvController.Instance.ChangeChannel(delta);
    }

    // =========================================================================
    // RIGHT-SIDE REMOTE CONTROL UI (POWER + LEFT/RIGHT ACROSS 5 CHANNELS)
    // =========================================================================

    private void OnGUI()
    {
        if (!IsEquippedActive) return;
        if (!HouseSceneStoryController.IsTvTaskUnlocked) return;
        if (HouseSceneStoryController.IsDialogueActive || InteractableCashierPC.IsPosScreenOpen) return;

        InitGuiResources();

        int prevDepth = GUI.depth;
        GUI.depth = -900;

        float panelW = 166f;
        float panelH = 224f;
        float x = Screen.width - panelW - 24f;
        float y = Mathf.Max(94f, Screen.height * 0.5f - panelH * 0.5f);
        Rect panel = new Rect(x, y, panelW, panelH);

        // Sleek dark retro handheld remote housing
        DrawSolid(panel, new Color(0.08f, 0.08f, 0.10f, 0.94f));
        DrawBorder(panel, new Color(0.35f, 0.36f, 0.40f, 0.90f), 1.5f);

        CabinTvController tv = CabinTvController.Instance;
        bool tvOn = tv != null && tv.isPoweredOn;
        int ch = tv != null ? tv.currentChannel : 1;

        // Top Remote Label & Status LED
        GUI.Label(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, 18f), "TV REMOTE", headerStyle);

        Rect ledRect = new Rect(panel.xMax - 20f, panel.y + 12f, 8f, 8f);
        DrawSolid(ledRect, tvOn ? new Color(0.24f, 0.90f, 0.42f, 1f) : new Color(0.76f, 0.18f, 0.18f, 1f));

        // Digital Readout Window ("OFF" or "CH 0X / 05")
        Rect lcdRect = new Rect(panel.x + 14f, panel.y + 32f, panel.width - 28f, 34f);
        DrawSolid(lcdRect, new Color(0.04f, 0.06f, 0.07f, 1f));
        DrawBorder(lcdRect, new Color(0.22f, 0.30f, 0.34f, 1f), 1f);
        readoutStyle.normal.textColor = tvOn ? new Color(0.45f, 0.96f, 0.62f, 1f) : new Color(0.65f, 0.32f, 0.32f, 1f);
        GUI.Label(lcdRect, tvOn ? $"CH 0{ch} / 0{CabinTvController.MaxChannel}" : "POWER OFF", readoutStyle);

        // Power Button (ON / OFF)
        bool powerOffLocked = tvOn && HouseSceneStoryController.IsTvPowerOffLocked;
        Rect powerBtn = new Rect(panel.x + 18f, panel.y + 78f, panel.width - 36f, 38f);
        bool flashPower = buttonFlashTimer > 0f && lastPressedButton == 1;
        Color powerBg = powerOffLocked
            ? new Color(0.24f, 0.25f, 0.28f, 1f)
            : (tvOn
                ? (flashPower ? new Color(0.78f, 0.25f, 0.22f, 1f) : new Color(0.56f, 0.14f, 0.14f, 1f))
                : (flashPower ? new Color(0.26f, 0.62f, 0.34f, 1f) : new Color(0.14f, 0.40f, 0.20f, 1f)));

        string powerLabel = powerOffLocked ? "WATCHING..." : (tvOn ? "TURN OFF [F]" : "TURN ON [F]");
        if (DrawRemoteButton(powerBtn, powerLabel, powerBg))
        {
            TriggerPowerButton();
        }

        // Left / Right Channel Buttons
        float chY = panel.y + 126f;
        float halfBtnW = (panel.width - 42f) * 0.5f;
        Rect leftBtn = new Rect(panel.x + 18f, chY, halfBtnW, 42f);
        Rect rightBtn = new Rect(leftBtn.xMax + 6f, chY, halfBtnW, 42f);

        Color chLeftBg = (buttonFlashTimer > 0f && lastPressedButton == 2)
            ? new Color(0.32f, 0.42f, 0.54f, 1f)
            : new Color(0.16f, 0.20f, 0.26f, 1f);
        Color chRightBg = (buttonFlashTimer > 0f && lastPressedButton == 3)
            ? new Color(0.32f, 0.42f, 0.54f, 1f)
            : new Color(0.16f, 0.20f, 0.26f, 1f);

        if (DrawRemoteButton(leftBtn, "<\n[<-]", chLeftBg))
        {
            TriggerChannelButton(-1);
        }
        if (DrawRemoteButton(rightBtn, ">\n[->]", chRightBg))
        {
            TriggerChannelButton(1);
        }

        // Control hints footer
        string cursorHint = IsCursorUnlockedForRemote ? "Click buttons (Tab lock)" : "Hold RMB / Tab: Click";
        GUI.Label(new Rect(panel.x + 8f, panel.y + 178f, panel.width - 16f, 38f),
            $"{cursorHint}\nAim Table [E]: Put Back", hintStyle);

        GUI.depth = prevDepth;
    }

    private bool DrawRemoteButton(Rect r, string label, Color bgColor)
    {
        bool hovered = IsCursorUnlockedForRemote && r.Contains(Event.current.mousePosition);
        Color finalBg = hovered ? Color.Lerp(bgColor, Color.white, 0.16f) : bgColor;

        DrawSolid(r, finalBg);
        DrawBorder(r, hovered ? new Color(0.88f, 0.90f, 0.95f, 1f) : new Color(0.45f, 0.48f, 0.54f, 0.9f), 1f);
        GUI.Label(r, label, btnStyle);

        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    private void InitGuiResources()
    {
        if (uiPixelTex == null)
        {
            uiPixelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            uiPixelTex.SetPixel(0, 0, Color.white);
            uiPixelTex.Apply();
        }

        if (uiFont == null)
        {
            uiFont = Font.CreateDynamicFontFromOSFont(new string[] { "Consolas", "Lucida Console", "Courier New" }, 13);
        }

        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = new Color(0.82f, 0.84f, 0.88f, 1f);

            readoutStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            btnStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            btnStyle.normal.textColor = new Color(0.95f, 0.96f, 0.98f, 1f);

            hintStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            hintStyle.normal.textColor = new Color(0.66f, 0.68f, 0.72f, 0.95f);
        }
    }

    private void DrawSolid(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, uiPixelTex);
        GUI.color = prev;
    }

    private void DrawBorder(Rect r, Color c, float t)
    {
        DrawSolid(new Rect(r.x, r.y, r.width, t), c);
        DrawSolid(new Rect(r.x, r.yMax - t, r.width, t), c);
        DrawSolid(new Rect(r.x, r.y, t, r.height), c);
        DrawSolid(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.1f;
        audioSource.playOnAwake = false;

        int sr = 44100;
        float[] clickData = new float[(int)(sr * 0.03f)];
        for (int i = 0; i < clickData.Length; i++)
        {
            float t = i / (float)sr;
            clickData[i] = Mathf.Sin(2f * Mathf.PI * 1900f * t) * Mathf.Exp(-t * 140f) * 0.4f;
        }
        clickClip = AudioClip.Create("RemoteButtonClick", clickData.Length, 1, sr, false);
        clickClip.SetData(clickData, 0);

        float[] placeData = new float[(int)(sr * 0.12f)];
        for (int i = 0; i < placeData.Length; i++)
        {
            float t = i / (float)sr;
            placeData[i] = (Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.5f + (Random.value * 2f - 1f) * 0.35f) * Mathf.Exp(-t * 35f) * 0.45f;
        }
        placeClip = AudioClip.Create("RemotePlaceWood", placeData.Length, 1, sr, false);
        placeClip.SetData(placeData, 0);
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
    }

    private static bool IsRightMouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
        return Input.GetMouseButton(1);
#endif
    }

    private static bool WasTabPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Tab);
#endif
    }

    private static bool WasKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        switch (key)
        {
            case KeyCode.F: return Keyboard.current.fKey.wasPressedThisFrame;
            case KeyCode.LeftArrow: return Keyboard.current.leftArrowKey.wasPressedThisFrame;
            case KeyCode.RightArrow: return Keyboard.current.rightArrowKey.wasPressedThisFrame;
            case KeyCode.Escape: return Keyboard.current.escapeKey.wasPressedThisFrame;
            default: return false;
        }
#else
        return Input.GetKeyDown(key);
#endif
    }
}

/// <summary>
/// Coffee table placement trigger left at the TV remote's original spot so the player can put it back cleanly.
/// </summary>
public class CabinTvRemoteSpotInteractable : MonoBehaviour, IInteractable
{
    public CabinTvRemoteInteractable targetRemote;

    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;
        if (targetRemote == null || !targetRemote.IsPickedUp) return string.Empty;
        if (!targetRemote.IsEquippedActive) return string.Empty;
        return "E to Put Down TV Remote";
    }

    public void Interact()
    {
        if (HouseSceneStoryController.IsDialogueActive) return;
        if (targetRemote == null || !targetRemote.IsPickedUp) return;
        if (!targetRemote.IsEquippedActive) return;
        targetRemote.PutDownOnCoffeeTable();
    }
}

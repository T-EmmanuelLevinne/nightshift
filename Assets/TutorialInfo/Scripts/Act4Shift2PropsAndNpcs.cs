using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// =============================================================================
// 1. PROCEDURAL 3D UMBRELLA MODEL BUILDER (USED BY PLAYER & STEVE NPC)
// =============================================================================
public static class Act4UmbrellaVisualBuilder
{
    private static Mesh cachedDomeMesh;
    private static Material cachedCanopyMat;
    private static Material cachedMetalMat;
    private static Material cachedHandleMat;

    public static GameObject CreateUmbrellaModel(string name, out Transform canopyPivot)
    {
        EnsureMaterialsAndMesh();

        GameObject root = new GameObject(name);

        // 1. Central metal shaft (Y = 0.06 .. 0.56)
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Umbrella_Shaft";
        Object.Destroy(shaft.GetComponent<Collider>());
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localPosition = new Vector3(0f, 0.31f, 0f);
        shaft.transform.localScale = new Vector3(0.015f, 0.25f, 0.015f);
        shaft.GetComponent<Renderer>().sharedMaterial = cachedMetalMat;
        shaft.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 2. Top ferrule tip (Y = 0.55 .. 0.61)
        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tip.name = "Umbrella_Tip";
        Object.Destroy(tip.GetComponent<Collider>());
        tip.transform.SetParent(root.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0.58f, 0f);
        tip.transform.localScale = new Vector3(0.009f, 0.032f, 0.009f);
        tip.GetComponent<Renderer>().sharedMaterial = cachedMetalMat;
        tip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 3. Ergonomic J-Hook Handle at bottom (Y = -0.02 .. 0.09)
        GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        grip.name = "Umbrella_HandleGrip";
        Object.Destroy(grip.GetComponent<Collider>());
        grip.transform.SetParent(root.transform, false);
        grip.transform.localPosition = new Vector3(0f, 0.045f, 0f);
        grip.transform.localScale = new Vector3(0.028f, 0.055f, 0.028f);
        grip.GetComponent<Renderer>().sharedMaterial = cachedHandleMat;
        grip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameObject hookBottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hookBottom.name = "Umbrella_HandleHookBottom";
        Object.Destroy(hookBottom.GetComponent<Collider>());
        hookBottom.transform.SetParent(root.transform, false);
        hookBottom.transform.localPosition = new Vector3(0f, -0.012f, -0.024f);
        hookBottom.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
        hookBottom.transform.localScale = new Vector3(0.024f, 0.024f, 0.055f);
        hookBottom.GetComponent<Renderer>().sharedMaterial = cachedHandleMat;
        hookBottom.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameObject hookTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hookTip.name = "Umbrella_HandleHookTip";
        Object.Destroy(hookTip.GetComponent<Collider>());
        hookTip.transform.SetParent(root.transform, false);
        hookTip.transform.localPosition = new Vector3(0f, 0.016f, -0.046f);
        hookTip.transform.localScale = new Vector3(0.024f, 0.045f, 0.022f);
        hookTip.GetComponent<Renderer>().sharedMaterial = cachedHandleMat;
        hookTip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 4. Canopy Pivot at top of shaft (Y = 0.54) so scaling XZ folds the umbrella downward around the shaft
        GameObject pivotGo = new GameObject("Canopy_Pivot");
        pivotGo.transform.SetParent(root.transform, false);
        pivotGo.transform.localPosition = new Vector3(0f, 0.54f, 0f);
        pivotGo.transform.localRotation = Quaternion.identity;
        pivotGo.transform.localScale = Vector3.one;
        canopyPivot = pivotGo.transform;

        GameObject domeGo = new GameObject("Umbrella_Canopy_Dome");
        domeGo.transform.SetParent(canopyPivot, false);
        domeGo.transform.localPosition = Vector3.zero;
        domeGo.transform.localRotation = Quaternion.identity;
        domeGo.transform.localScale = Vector3.one;

        MeshFilter mf = domeGo.AddComponent<MeshFilter>();
        mf.sharedMesh = cachedDomeMesh;
        MeshRenderer mr = domeGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = cachedCanopyMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 8 radial ribs under the dome
        for (int i = 0; i < 8; i++)
        {
            float ang = i * 45f;
            GameObject rib = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rib.name = $"Umbrella_Rib_{i}";
            Object.Destroy(rib.GetComponent<Collider>());
            rib.transform.SetParent(canopyPivot, false);
            Quaternion rot = Quaternion.Euler(18f, ang, 0f);
            rib.transform.localRotation = rot;
            rib.transform.localPosition = rot * new Vector3(0f, -0.01f, 0.21f);
            rib.transform.localScale = new Vector3(0.007f, 0.007f, 0.42f);
            rib.GetComponent<Renderer>().sharedMaterial = cachedMetalMat;
            rib.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        return root;
    }

    public static void SetCanopyOpenImmediate(Transform canopyPivot, bool open)
    {
        SetCanopyOpenBlend(canopyPivot, open ? 1f : 0f);
    }

    public static void SetCanopyOpenBlend(Transform canopyPivot, float openBlend)
    {
        if (canopyPivot == null) return;
        float u = Mathf.Clamp01(openBlend);
        // When open (u=1): scale (1, 1, 1). When sheathed/closed (u=0): folded tightly around shaft (0.13, 1.95, 0.13)
        float xz = Mathf.Lerp(0.13f, 1.0f, u);
        float y = Mathf.Lerp(1.95f, 1.0f, u);
        canopyPivot.localScale = new Vector3(xz, y, xz);
    }

    private static void EnsureMaterialsAndMesh()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        if (cachedCanopyMat == null)
        {
            cachedCanopyMat = new Material(sh);
            cachedCanopyMat.name = "Procedural_UmbrellaCanopy_MAT";
            Color c = new Color(0.03f, 0.03f, 0.035f, 1.0f);
            cachedCanopyMat.color = c;
            if (cachedCanopyMat.HasProperty("_BaseColor")) cachedCanopyMat.SetColor("_BaseColor", c);
            if (cachedCanopyMat.HasProperty("_Color")) cachedCanopyMat.SetColor("_Color", c);
            if (cachedCanopyMat.HasProperty("_Surface")) cachedCanopyMat.SetFloat("_Surface", 0f); // Strictly Opaque
            if (cachedCanopyMat.HasProperty("_Blend")) cachedCanopyMat.SetFloat("_Blend", 0f);
            cachedCanopyMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            cachedCanopyMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            cachedCanopyMat.SetInt("_ZWrite", 1);
            cachedCanopyMat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            cachedCanopyMat.DisableKeyword("_ALPHABLEND_ON");
            cachedCanopyMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            cachedCanopyMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            if (cachedCanopyMat.HasProperty("_Smoothness")) cachedCanopyMat.SetFloat("_Smoothness", 0.36f);
            if (cachedCanopyMat.HasProperty("_Cull")) cachedCanopyMat.SetFloat("_Cull", 0f); // Double-sided dome
            if (cachedCanopyMat.HasProperty("_EmissionColor"))
            {
                cachedCanopyMat.EnableKeyword("_EMISSION");
                cachedCanopyMat.SetColor("_EmissionColor", new Color(0.02f, 0.02f, 0.025f, 1f));
            }
        }

        if (cachedMetalMat == null)
        {
            cachedMetalMat = new Material(sh);
            cachedMetalMat.name = "Procedural_UmbrellaMetal_MAT";
            Color c = new Color(0.70f, 0.74f, 0.78f, 1f);
            cachedMetalMat.color = c;
            if (cachedMetalMat.HasProperty("_BaseColor")) cachedMetalMat.SetColor("_BaseColor", c);
            if (cachedMetalMat.HasProperty("_Smoothness")) cachedMetalMat.SetFloat("_Smoothness", 0.70f);
            if (cachedMetalMat.HasProperty("_EmissionColor"))
            {
                cachedMetalMat.EnableKeyword("_EMISSION");
                cachedMetalMat.SetColor("_EmissionColor", new Color(0.12f, 0.13f, 0.15f));
            }
        }

        if (cachedHandleMat == null)
        {
            cachedHandleMat = new Material(sh);
            cachedHandleMat.name = "Procedural_UmbrellaHandle_MAT";
            Color c = new Color(0.32f, 0.20f, 0.12f, 1f);
            cachedHandleMat.color = c;
            if (cachedHandleMat.HasProperty("_BaseColor")) cachedHandleMat.SetColor("_BaseColor", c);
            if (cachedHandleMat.HasProperty("_Smoothness")) cachedHandleMat.SetFloat("_Smoothness", 0.45f);
            if (cachedHandleMat.HasProperty("_EmissionColor"))
            {
                cachedHandleMat.EnableKeyword("_EMISSION");
                cachedHandleMat.SetColor("_EmissionColor", new Color(0.08f, 0.05f, 0.03f));
            }
        }

        if (cachedDomeMesh == null)
        {
            cachedDomeMesh = BuildOctagonDomeMesh();
        }
    }

    private static Mesh BuildOctagonDomeMesh()
    {
        int sides = 8;
        int rings = 4;
        float maxRadius = 0.46f;
        float dropHeight = 0.18f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        // 1. TOP SURFACE VERTICES (with upward/outward normals so they never cancel with underside normals)
        verts.Add(Vector3.zero);
        norms.Add(Vector3.up);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int r = 1; r <= rings; r++)
        {
            float frac = (float)r / rings;
            float rad = frac * maxRadius;
            float y = -Mathf.Pow(frac, 1.45f) * dropHeight;

            for (int s = 0; s < sides; s++)
            {
                float ang = (s / (float)sides) * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * rad;
                float z = Mathf.Sin(ang) * rad;
                verts.Add(new Vector3(x, y, z));
                Vector3 outN = new Vector3(Mathf.Cos(ang) * 0.45f, 0.90f, Mathf.Sin(ang) * 0.45f).normalized;
                norms.Add(outN);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(ang) * frac * 0.5f, 0.5f + Mathf.Sin(ang) * frac * 0.5f));
            }
        }

        for (int s = 0; s < sides; s++)
        {
            int next = (s + 1) % sides;
            tris.Add(0); tris.Add(1 + next); tris.Add(1 + s);
        }

        for (int r = 1; r < rings; r++)
        {
            int ringStart = 1 + (r - 1) * sides;
            int nextRingStart = 1 + r * sides;
            for (int s = 0; s < sides; s++)
            {
                int next = (s + 1) % sides;
                int a = ringStart + s;
                int b = ringStart + next;
                int c = nextRingStart + s;
                int d = nextRingStart + next;

                tris.Add(a); tris.Add(b); tris.Add(d);
                tris.Add(a); tris.Add(d); tris.Add(c);
            }
        }

        // 2. SEPARATE UNDERSIDE VERTICES (with downward/inward normals so the inside of the black umbrella is 100% solid opaque black)
        int bottomBaseIdx = verts.Count;
        float thicknessOffset = -0.003f;
        verts.Add(new Vector3(0f, thicknessOffset, 0f));
        norms.Add(Vector3.down);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int r = 1; r <= rings; r++)
        {
            float frac = (float)r / rings;
            float rad = frac * maxRadius;
            float y = -Mathf.Pow(frac, 1.45f) * dropHeight + thicknessOffset;

            for (int s = 0; s < sides; s++)
            {
                float ang = (s / (float)sides) * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * rad;
                float z = Mathf.Sin(ang) * rad;
                verts.Add(new Vector3(x, y, z));
                Vector3 inN = new Vector3(-Mathf.Cos(ang) * 0.35f, -0.94f, -Mathf.Sin(ang) * 0.35f).normalized;
                norms.Add(inN);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(ang) * frac * 0.5f, 0.5f + Mathf.Sin(ang) * frac * 0.5f));
            }
        }

        for (int s = 0; s < sides; s++)
        {
            int next = (s + 1) % sides;
            tris.Add(bottomBaseIdx);
            tris.Add(bottomBaseIdx + 1 + s);
            tris.Add(bottomBaseIdx + 1 + next);
        }

        for (int r = 1; r < rings; r++)
        {
            int ringStart = bottomBaseIdx + 1 + (r - 1) * sides;
            int nextRingStart = bottomBaseIdx + 1 + r * sides;
            for (int s = 0; s < sides; s++)
            {
                int next = (s + 1) % sides;
                int a = ringStart + s;
                int b = ringStart + next;
                int c = nextRingStart + s;
                int d = nextRingStart + next;

                tris.Add(a); tris.Add(d); tris.Add(b);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }
        }

        Mesh m = new Mesh();
        m.name = "Procedural_Umbrella_Dome";
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    public static Texture2D GenerateUmbrellaHotbarIcon()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color domeCol = new Color(0.08f, 0.08f, 0.09f, 1f);
        Color domeHi = new Color(0.18f, 0.18f, 0.20f, 1f);
        Color shaftCol = new Color(0.75f, 0.78f, 0.82f, 1f);
        Color handleCol = new Color(0.62f, 0.42f, 0.24f, 1f);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.clear);

        // Shaft (x=15..16, y=7..27)
        for (int y = 7; y <= 27; y++)
        {
            tex.SetPixel(15, y, shaftCol);
            tex.SetPixel(16, y, shaftCol);
        }

        // J-Hook handle (y=4..7)
        for (int x = 12; x <= 16; x++)
        {
            tex.SetPixel(x, 4, handleCol);
            tex.SetPixel(x, 5, handleCol);
        }
        tex.SetPixel(12, 6, handleCol);
        tex.SetPixel(12, 7, handleCol);

        // Dome canopy (y=17..25)
        for (int y = 17; y <= 25; y++)
        {
            float v = (y - 17f) / 8f;
            int halfW = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)) * 11f);
            for (int x = 15 - halfW; x <= 16 + halfW; x++)
            {
                tex.SetPixel(x, y, (x < 15) ? domeHi : domeCol);
            }
        }

        tex.Apply();
        return tex;
    }
}

// =============================================================================
// 2. PLAYER HANDHELD UMBRELLA & COUNTER PLACEMENT SPOT FOR ACT 4
// =============================================================================
public class Act4PlayerUmbrella : MonoBehaviour
{
    public static Act4PlayerUmbrella Instance { get; private set; }

    public bool isEquippedInHand = false;
    public bool isSheathed = false;
    public bool isPutDown = false;
    private bool isAnimatingSheath = false;

    private GameObject umbrellaViewmodel;
    private Transform canopyPivot;
    private Renderer[] viewmodelRenderers;
    private AudioSource audioSource;
    private AudioClip sheathClip;

    private readonly Vector3 openHandLocalPos = new Vector3(0.20f, -0.26f, 0.56f);
    private readonly Vector3 openHandLocalEuler = new Vector3(5f, -18f, 4f);

    private readonly Vector3 sheathedHandLocalPos = new Vector3(0.24f, -0.24f, 0.54f);
    private readonly Vector3 sheathedHandLocalEuler = new Vector3(24f, -18f, 8f);

    private Vector3 currentHandLocalPos;
    private Quaternion currentHandLocalRot;

    private Font promptFont;
    private GUIStyle promptStyle;
    private GUIStyle promptShadowStyle;

    public static bool IsOpenUmbrellaBlockingRain =>
        Instance != null && Instance.isEquippedInHand && !Instance.isSheathed && !Instance.isPutDown;

    public static Act4PlayerUmbrella EnsureEquippedOnPlayer()
    {
        if (Instance != null)
        {
            Instance.EquipOpenInHand();
            return Instance;
        }

        GameObject go = new GameObject("Act4_PlayerUmbrella_Controller");
        Instance = go.AddComponent<Act4PlayerUmbrella>();
        Instance.EquipOpenInHand();
        return Instance;
    }

    private void Awake()
    {
        Instance = this;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.1f;
        audioSource.playOnAwake = false;

        int sr = 44100;
        int len = (int)(sr * 0.28f);
        float[] samples = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Sin((t / 0.28f) * Mathf.PI);
            float rustle = (Random.value * 2f - 1f) * 0.45f * env;
            float click = (t > 0.20f && t < 0.24f) ? Mathf.Sin(2f * Mathf.PI * 1250f * t) * 0.4f : 0f;
            samples[i] = (rustle + click) * 0.7f;
        }
        sheathClip = AudioClip.Create("UmbrellaSheath", len, 1, sr, false);
        sheathClip.SetData(samples, 0);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.OnSlotChanged -= HandleHotbarSlotChanged;
        }
    }

    public void EquipOpenInHand()
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Prevent the camera's near clip plane from clipping the upper dome canopy
        cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, 0.05f);

        if (umbrellaViewmodel == null)
        {
            umbrellaViewmodel = Act4UmbrellaVisualBuilder.CreateUmbrellaModel("Player_Handheld_Umbrella", out canopyPivot);
            viewmodelRenderers = umbrellaViewmodel.GetComponentsInChildren<Renderer>(true);
        }

        umbrellaViewmodel.transform.SetParent(cam.transform, false);
        currentHandLocalPos = openHandLocalPos;
        currentHandLocalRot = Quaternion.Euler(openHandLocalEuler);
        umbrellaViewmodel.transform.localPosition = currentHandLocalPos;
        umbrellaViewmodel.transform.localRotation = currentHandLocalRot;
        umbrellaViewmodel.transform.localScale = Vector3.one;

        Act4UmbrellaVisualBuilder.SetCanopyOpenImmediate(canopyPivot, true);
        isEquippedInHand = true;
        isSheathed = false;
        isPutDown = false;
        isAnimatingSheath = false;

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null)
        {
            hotbar.OnSlotChanged -= HandleHotbarSlotChanged;
            hotbar.OnSlotChanged += HandleHotbarSlotChanged;
            hotbar.SetSlot(0, "UMBRELLA", Act4UmbrellaVisualBuilder.GenerateUmbrellaHotbarIcon());
            hotbar.SetActiveSlot(0);
        }

        SetViewmodelVisible(true);
    }

    private void HandleHotbarSlotChanged(int slot)
    {
        if (!isEquippedInHand || isPutDown) return;
        bool active = (HorrorHotbar.Instance == null || HorrorHotbar.Instance.GetSlotIndexWithItem("UMBRELLA") == slot);
        SetViewmodelVisible(active);
    }

    private void SetViewmodelVisible(bool visible)
    {
        if (viewmodelRenderers == null && umbrellaViewmodel != null)
        {
            viewmodelRenderers = umbrellaViewmodel.GetComponentsInChildren<Renderer>(true);
        }
        if (viewmodelRenderers == null) return;
        for (int i = 0; i < viewmodelRenderers.Length; i++)
        {
            if (viewmodelRenderers[i] != null) viewmodelRenderers[i].enabled = visible;
        }
    }

    private void Update()
    {
        if (!isEquippedInHand || isPutDown || isAnimatingSheath) return;

        // Allow sheathing (closing) the umbrella once inside the convenience store via F, R, Q, or Left Mouse Click
        if (!isSheathed && NightShiftStoryController.IsPlayerInsideStoreStatic() && !NightShiftStoryController.IsDialogueActive)
        {
            bool sheathPressed = HorrorInteractionReticle.WasMouseClicked() ||
                                 HorrorInteractionReticle.WasQPressed() ||
                                 WasSheathKeyPressed();
            if (sheathPressed)
            {
                TriggerSheathUmbrella();
            }
        }
    }

    private static bool WasSheathKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.rKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.R);
#endif
    }

    public void TriggerSheathUmbrella()
    {
        if (isSheathed || isAnimatingSheath || isPutDown) return;
        StartCoroutine(SheathUmbrellaRoutine());
    }

    private IEnumerator SheathUmbrellaRoutine()
    {
        isAnimatingSheath = true;
        if (audioSource != null && sheathClip != null)
        {
            audioSource.PlayOneShot(sheathClip, 0.85f);
        }

        Vector3 startPos = currentHandLocalPos;
        Quaternion startRot = currentHandLocalRot;
        Quaternion targetRot = Quaternion.Euler(sheathedHandLocalEuler);

        float dur = 0.42f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            Act4UmbrellaVisualBuilder.SetCanopyOpenBlend(canopyPivot, 1f - u);
            currentHandLocalPos = Vector3.Lerp(startPos, sheathedHandLocalPos, u);
            currentHandLocalRot = Quaternion.Slerp(startRot, targetRot, u);
            yield return null;
        }

        Act4UmbrellaVisualBuilder.SetCanopyOpenImmediate(canopyPivot, false);
        currentHandLocalPos = sheathedHandLocalPos;
        currentHandLocalRot = targetRot;
        isSheathed = true;
        isAnimatingSheath = false;
    }

    public void PutDownAtStand(Transform standAnchor)
    {
        if (isPutDown) return;
        isPutDown = true;
        isEquippedInHand = false;
        isSheathed = true;

        if (audioSource != null && sheathClip != null)
        {
            audioSource.pitch = 0.88f;
            audioSource.PlayOneShot(sheathClip, 0.75f);
        }

        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.ClearItem("UMBRELLA");
        }

        if (umbrellaViewmodel != null && standAnchor != null)
        {
            Act4UmbrellaVisualBuilder.SetCanopyOpenImmediate(canopyPivot, false);
            umbrellaViewmodel.transform.SetParent(standAnchor, false);
            // Place point-down inside the stand so the ferrule tip rests on the bottom plate and the J-hook handle sticks out the top
            umbrellaViewmodel.transform.localPosition = new Vector3(0f, 0.60f, 0f);
            umbrellaViewmodel.transform.localRotation = Quaternion.Euler(172f, 32f, -6f);
            umbrellaViewmodel.transform.localScale = Vector3.one;
            SetViewmodelVisible(true);
        }
    }

    private void LateUpdate()
    {
        if (!isEquippedInHand || isPutDown || umbrellaViewmodel == null) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam != null && umbrellaViewmodel.transform.parent != cam.transform)
        {
            umbrellaViewmodel.transform.SetParent(cam.transform, false);
        }

        float bob = Mathf.Sin(Time.time * 2.4f) * 0.005f;
        umbrellaViewmodel.transform.localPosition = currentHandLocalPos + new Vector3(0f, bob, 0f);
        umbrellaViewmodel.transform.localRotation = currentHandLocalRot;
    }

    private void OnGUI()
    {
        if (!isEquippedInHand || isPutDown || isSheathed || Event.current.type != EventType.Repaint) return;
        if (NightShiftStoryController.IsDialogueActive || !NightShiftStoryController.IsPlayerInsideStoreStatic()) return;

        if (promptStyle == null)
        {
            promptFont = HorrorHudStyle.GetRetroFont();
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                font = promptFont,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            promptStyle.normal.textColor = new Color(0.95f, 0.90f, 0.72f, 0.95f);
            promptShadowStyle = new GUIStyle(promptStyle);
            promptShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.95f);
        }

        string msg = "[Left Click / F] Sheath Umbrella";
        Rect r = new Rect(Screen.width * 0.5f - 200f, Screen.height - 118f, 400f, 26f);
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), msg, promptShadowStyle);
        GUI.Label(r, msg, promptStyle);
    }
}

public class Act4UmbrellaStandSpot : MonoBehaviour, IInteractable
{
    public static Act4UmbrellaStandSpot Instance { get; private set; }

    private BoxCollider triggerCollider;
    private GameObject standVisualRoot;

    public Vector3 IndicatorWorldPosition => transform.position + Vector3.up * 0.52f;

    public bool ShouldShowIndicator =>
        Act4PlayerUmbrella.Instance != null &&
        !Act4PlayerUmbrella.Instance.isPutDown &&
        NightShiftStoryController.IsPlayerInsideStoreStatic();

    public static Act4UmbrellaStandSpot EnsureCreatedNearCashier(Vector3 counterSpotWorld)
    {
        if (Instance != null) return Instance;

        Act4UmbrellaStandSpot existing = Object.FindAnyObjectByType<Act4UmbrellaStandSpot>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("Act4_Umbrella_Counter_Spot");
        // Place cleanly on the open floor in the inside corner behind the cashier counter (completely clear of the black counter cabinet)
        go.transform.position = new Vector3(counterSpotWorld.x - 1.18f, 0.16f, counterSpotWorld.z + 0.22f);
        go.transform.rotation = Quaternion.identity;

        Instance = go.AddComponent<Act4UmbrellaStandSpot>();
        return Instance;
    }

    private void Awake()
    {
        Instance = this;
        triggerCollider = gameObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.center = new Vector3(0f, 0.45f, 0f);
        triggerCollider.size = new Vector3(0.68f, 0.95f, 0.68f);

        BuildStandVisual();
    }

    private void BuildStandVisual()
    {
        standVisualRoot = new GameObject("UmbrellaStand_Holder");
        standVisualRoot.transform.SetParent(transform, false);

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material standMat = new Material(sh) { color = new Color(0.18f, 0.20f, 0.23f) };
        if (standMat.HasProperty("_BaseColor")) standMat.SetColor("_BaseColor", new Color(0.18f, 0.20f, 0.23f));
        if (standMat.HasProperty("_Smoothness")) standMat.SetFloat("_Smoothness", 0.55f);

        GameObject baseDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseDisc.name = "StandBase";
        Destroy(baseDisc.GetComponent<Collider>());
        baseDisc.transform.SetParent(standVisualRoot.transform, false);
        baseDisc.transform.localPosition = new Vector3(0f, 0.015f, 0f);
        baseDisc.transform.localScale = new Vector3(0.24f, 0.015f, 0.24f);
        baseDisc.GetComponent<Renderer>().sharedMaterial = standMat;

        GameObject topRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        topRing.name = "StandTopCollar";
        Destroy(topRing.GetComponent<Collider>());
        topRing.transform.SetParent(standVisualRoot.transform, false);
        topRing.transform.localPosition = new Vector3(0f, 0.34f, 0f);
        topRing.transform.localScale = new Vector3(0.22f, 0.008f, 0.22f);
        topRing.GetComponent<Renderer>().sharedMaterial = standMat;

        for (int i = 0; i < 4; i++)
        {
            float ang = i * 90f * Mathf.Deg2Rad;
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = $"StandPost_{i}";
            Destroy(post.GetComponent<Collider>());
            post.transform.SetParent(standVisualRoot.transform, false);
            post.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.095f, 0.18f, Mathf.Sin(ang) * 0.095f);
            post.transform.localScale = new Vector3(0.014f, 0.17f, 0.014f);
            post.GetComponent<Renderer>().sharedMaterial = standMat;
        }
    }

    private void Update()
    {
        // Keep the umbrella stand visible in both the first gas scene (Act 2) and the second gas scene (Act 4)
        if (standVisualRoot != null && !standVisualRoot.activeSelf)
        {
            standVisualRoot.SetActive(true);
        }
    }

    public string GetPromptText()
    {
        if (NightShiftStoryController.IsDialogueActive) return string.Empty;
        if (!NightShiftStoryController.IsPlayerInsideStoreStatic()) return string.Empty;
        if (Act4PlayerUmbrella.Instance == null || Act4PlayerUmbrella.Instance.isPutDown) return string.Empty;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position + Vector3.up * 0.4f) > 2.25f)
        {
            return string.Empty;
        }

        if (!Act4PlayerUmbrella.Instance.isSheathed)
        {
            return "E to sheath umbrella";
        }

        return "E to put it down";
    }

    public void Interact()
    {
        if (NightShiftStoryController.IsDialogueActive) return;
        if (!NightShiftStoryController.IsPlayerInsideStoreStatic()) return;
        if (Act4PlayerUmbrella.Instance == null || Act4PlayerUmbrella.Instance.isPutDown) return;

        if (!Act4PlayerUmbrella.Instance.isSheathed)
        {
            Act4PlayerUmbrella.Instance.TriggerSheathUmbrella();
            return;
        }

        Act4PlayerUmbrella.Instance.PutDownAtStand(transform);
    }
}

// =============================================================================
// 3. JOHN NPC (ACT 4 BATHROOM REQUEST, DOOR LOCKING & RESTROOM MURDER BODY)
// =============================================================================
public class Act4JohnNpc : MonoBehaviour, IInteractable
{
    public static Act4JohnNpc Instance { get; private set; }

    public bool IsNpcActiveInWorld => isNpcVisible && gameObject.activeInHierarchy;

    public enum JohnState
    {
        HiddenWaiting,
        WalkingToCounter,
        WaitingForCashierInteract,
        TalkingBathroomRequest,
        WalkingToRestroom,
        InsideRestroomLocked,
        DeadOnRestroomFloor
    }

    public JohnState currentState = JohnState.HiddenWaiting;
    public float walkSpeed = 1.32f;
    public float turnSpeed = 5.5f;
    public float ps1AnimFps = 12f;

    private Vector3 spawnPosition = new Vector3(-30.30f, -0.04f, -13.20f);
    private AutomaticSlidingDoors slidingDoors;
    private Transform playerNpcInteractTransform;
    private Transform invisibleNpcItemTransform;
    private Transform storeRootTransform;
    private InteractableRestroomDoor maleRestroomDoor;
    private Transform handleOutTransform;
    private Transform handleInTransform;

    private Renderer[] npcRenderers;
    private CapsuleCollider npcCapsule;
    private bool isNpcVisible = false;
    private bool cashierInteractTriggered = false;

    // Rig bones
    private Transform pelvis, spine, chest, neck, head;
    private Transform upperArmL, forearmL, handL;
    private Transform upperArmR, forearmR, handR;
    private Transform thighL, shinL, footL, footLEnd;
    private Transform thighR, shinR, footR, footREnd;
    private readonly Dictionary<Transform, Quaternion> bindLocalRots = new Dictionary<Transform, Quaternion>();
    private Vector3 pelvisBindLocalPos;
    private Vector3 modelRightLocal = Vector3.right;
    private Vector3 modelForwardLocal = Vector3.forward;
    private float modelYawCorrectionDeg = 0f;
    private bool rigInitialized = false;

    private bool isCurrentlyWalking = false;
    private bool lookAtPlayerHead = false;
    private float walkCycleTimer = 0f;
    private float idleTimer = 0f;
    private float footstepTimer = 0f;
    private float rightArmReachWeight = 0f;
    private Vector3 rightArmWorldTarget = Vector3.zero;

    private AudioSource npcAudioSource;
    private AudioClip footstepClip;

    private GameObject deadManOverrideObj;
    private readonly List<GameObject> spawnedRestroomBloodStains = new List<GameObject>();

    public Vector3 PrimaryBloodStainWorldPos { get; private set; }
    public Vector3 SecondaryBloodStainWorldPos { get; private set; }
    public Vector3 DeadBodyLookWorldPos { get; private set; }

    public bool IsPlayerInsideCashierZone
    {
        get
        {
            if (CustomerNpcSteve.Instance != null) return CustomerNpcSteve.Instance.IsPlayerInsideCashierZone;
            if (playerNpcInteractTransform == null) return true;
            Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
            if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
            if (cam == null) return false;
            Vector3 pPos = cam.transform.position;
            CharacterController cc = cam.GetComponentInParent<CharacterController>();
            if (cc != null) pPos = cc.transform.position;
            return Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(playerNpcInteractTransform.position.x, playerNpcInteractTransform.position.z)) <= 1.25f;
        }
    }

    private static Transform ResolveCharacterRoot(Transform candidate)
    {
        Transform cur = candidate;
        while (cur.parent != null)
        {
            string pn = cur.parent.name.Trim();
            if (cur.parent.GetComponent<Animator>() != null ||
                pn.IndexOf("John", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                pn.IndexOf("Character_Male", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                pn.IndexOf("manJohn", System.StringComparison.OrdinalIgnoreCase) >= 0)
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

    public static Act4JohnNpc EnsureExistsInScene()
    {
        if (Instance != null) return Instance;

        Act4JohnNpc existing = Object.FindAnyObjectByType<Act4JohnNpc>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        GameObject johnGo = null;

        // Pass 1: Prefer exact root name "JohnNPC"
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "JohnNPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "John_NPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "John NPC", System.StringComparison.OrdinalIgnoreCase))
            {
                johnGo = ResolveCharacterRoot(t).gameObject;
                break;
            }
        }

        // Pass 2: Fallback to "manJohn" or "Character_Male" climbed to root
        if (johnGo == null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                string nm = t.name.Trim();
                if (nm.IndexOf("John", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    string.Equals(nm, "manJohn", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(nm, "Character_Male", System.StringComparison.OrdinalIgnoreCase))
                {
                    johnGo = ResolveCharacterRoot(t).gameObject;
                    break;
                }
            }
        }

#if UNITY_EDITOR
        if (johnGo == null)
        {
            GameObject fbxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/manJohn/source/Character_Male.fbx");
            if (fbxPrefab == null)
            {
                fbxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/character--male-01 1/source/Character_Male_01.fbx");
            }
            if (fbxPrefab != null)
            {
                johnGo = Instantiate(fbxPrefab);
                johnGo.name = "JohnNPC";
                Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/manJohn/textures/Character_Male.png");
                if (tex != null)
                {
                    Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    Material m = new Material(sh) { mainTexture = tex };
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                    Renderer[] rends = johnGo.GetComponentsInChildren<Renderer>(true);
                    for (int r = 0; r < rends.Length; r++) rends[r].sharedMaterial = m;
                }
            }
        }
#endif

        // Fallback: clone NPC1 (Steve) if no separate John object was placed
        if (johnGo == null && CustomerNpcSteve.Instance != null)
        {
            johnGo = Instantiate(CustomerNpcSteve.Instance.gameObject);
            johnGo.name = "JohnNPC";
            CustomerNpcSteve clonedSteve = johnGo.GetComponent<CustomerNpcSteve>();
            if (clonedSteve != null) DestroyImmediate(clonedSteve);
        }

        if (johnGo != null)
        {
            johnGo.SetActive(true);
            Instance = johnGo.GetComponent<Act4JohnNpc>();
            if (Instance == null) Instance = johnGo.AddComponent<Act4JohnNpc>();
            return Instance;
        }

        return null;
    }

    private void Awake()
    {
        Instance = this;

        Animator anim = GetComponentInChildren<Animator>(true);

        npcRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < npcRenderers.Length; i++)
        {
            if (npcRenderers[i] is SkinnedMeshRenderer smr)
            {
                smr.updateWhenOffscreen = true;
            }
        }

        npcCapsule = GetComponent<CapsuleCollider>();
        if (npcCapsule == null) npcCapsule = gameObject.AddComponent<CapsuleCollider>();
        float sy = Mathf.Max(0.5f, Mathf.Abs(transform.lossyScale.y));
        float sx = Mathf.Max(0.5f, Mathf.Abs(transform.lossyScale.x));
        npcCapsule.height = 1.76f / sy;
        npcCapsule.radius = 0.30f / sx;
        npcCapsule.center = new Vector3(0f, 0.88f / sy, 0f);

        CacheRigAndBindPose(anim);

        // Disable Animator unconditionally so procedural PS1 bone animation has 100% control over Mixamo/Humanoid bones
        if (anim != null)
        {
            anim.enabled = false;
        }

        SetupAudio();
        ResolveTargets();
        SetNpcVisible(false);
    }

    private void SetupAudio()
    {
        npcAudioSource = GetComponent<AudioSource>();
        if (npcAudioSource == null) npcAudioSource = gameObject.AddComponent<AudioSource>();
        npcAudioSource.playOnAwake = false;
        npcAudioSource.spatialBlend = 0.82f;

        int sr = 44100;
        int stepSamples = (int)(sr * 0.075f);
        float[] stepBuf = new float[stepSamples];
        for (int i = 0; i < stepSamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 58f);
            stepBuf[i] = (Mathf.Sin(2f * Mathf.PI * 92f * t) * 0.65f + (Random.value * 2f - 1f) * 0.35f) * env * 0.6f;
        }
        footstepClip = AudioClip.Create("JohnFootstep", stepSamples, 1, sr, false);
        footstepClip.SetData(stepBuf, 0);
    }

    private void ResolveTargets()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            string nm = t.name.Trim();

            if (string.Equals(nm, "SpawnerNPC", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "NPCSpawner", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "NPC Spawner", System.StringComparison.OrdinalIgnoreCase))
            {
                spawnPosition = t.position;
            }
            else if (string.Equals(nm, "PlayerNpcInteract", System.StringComparison.OrdinalIgnoreCase))
            {
                playerNpcInteractTransform = t;
            }
            else if (nm.IndexOf("InvisibleNPCItem", System.StringComparison.OrdinalIgnoreCase) >= 0 && invisibleNpcItemTransform == null)
            {
                invisibleNpcItemTransform = t;
            }
            else if (string.Equals(nm, "Convenience_Store_V2", System.StringComparison.OrdinalIgnoreCase))
            {
                storeRootTransform = t;
            }
            else if (deadManOverrideObj == null &&
                     (nm.IndexOf("Chapa-Giratoria", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                      nm.IndexOf("dead-man", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                      nm.IndexOf("DeadMan", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                deadManOverrideObj = t.gameObject;
                deadManOverrideObj.SetActive(false);
            }
        }

        slidingDoors = Object.FindAnyObjectByType<AutomaticSlidingDoors>();

        InteractableRestroomDoor[] doors = Object.FindObjectsByType<InteractableRestroomDoor>(FindObjectsInactive.Include);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null && doors[i].IsMaleRestroomDoor)
            {
                maleRestroomDoor = doors[i];
                Transform[] doorChildren = maleRestroomDoor.GetComponentsInChildren<Transform>(true);
                for (int c = 0; c < doorChildren.Length; c++)
                {
                    if (doorChildren[c] == null) continue;
                    string cn = doorChildren[c].name.Trim();
                    if (cn.IndexOf("Handle_Out", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        handleOutTransform = doorChildren[c];
                    }
                    else if (cn.IndexOf("Handle_In", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        handleInTransform = doorChildren[c];
                    }
                }
                break;
            }
        }
    }

    public void SetNpcVisible(bool visible)
    {
        isNpcVisible = visible;
        if (npcRenderers == null) npcRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < npcRenderers.Length; i++)
        {
            if (npcRenderers[i] != null) npcRenderers[i].enabled = visible;
        }
        if (npcCapsule != null) npcCapsule.enabled = visible;
    }

    public IEnumerator WalkInAndRequestBathroomRoutine()
    {
        ResolveTargets();

        transform.position = spawnPosition;
        SnapToGroundImmediate();
        SetNpcVisible(true);
        currentState = JohnState.WalkingToCounter;

        float doorZ = slidingDoors != null ? slidingDoors.transform.position.z : -1.60f;
        float doorX = slidingDoors != null ? slidingDoors.transform.position.x : -1.77f;

        Vector3 counterStandPos = new Vector3(2.64f, 0.17f, -5.78f);
        Vector3 cashierLookPos = new Vector3(2.64f, 1.35f, -7.50f);
        if (invisibleNpcItemTransform != null)
        {
            Vector3 p = invisibleNpcItemTransform.position;
            float zSign = (playerNpcInteractTransform != null && playerNpcInteractTransform.position.z <= p.z) ? 1f : -1f;
            counterStandPos = new Vector3(p.x - 0.12f, 0.17f, p.z + 0.90f * zSign);
            cashierLookPos = playerNpcInteractTransform != null
                ? new Vector3(playerNpcInteractTransform.position.x, 1.35f, playerNpcInteractTransform.position.z)
                : new Vector3(p.x - 0.25f, 1.35f, p.z - 0.90f * zSign);
        }

        List<Vector3> pathToCounter = new List<Vector3>();
        if (spawnPosition.x < -22f)
        {
            pathToCounter.Add(new Vector3(Mathf.Clamp(spawnPosition.x, -30.8f, -29.8f), spawnPosition.y, -13.20f));
            pathToCounter.Add(new Vector3(-25.50f, 0.02f, -12.80f));
        }
        pathToCounter.Add(new Vector3(-11.50f, 0.08f, -12.00f));
        pathToCounter.Add(new Vector3(-5.60f, 0.14f, -9.80f));
        pathToCounter.Add(new Vector3(-4.80f, 0.14f, -4.80f));
        pathToCounter.Add(new Vector3(doorX - 1.35f, 0.15f, doorZ));
        pathToCounter.Add(new Vector3(-0.72f, 0.17f, doorZ));
        pathToCounter.Add(new Vector3(-0.72f, 0.17f, -3.50f));
        pathToCounter.Add(new Vector3(-0.52f, 0.17f, -4.75f));
        pathToCounter.Add(new Vector3(0.45f, 0.17f, -5.25f));
        pathToCounter.Add(new Vector3(counterStandPos.x, 0.17f, Mathf.Min(-5.25f, counterStandPos.z + 0.35f)));
        pathToCounter.Add(counterStandPos);

        yield return StartCoroutine(FollowWaypointsRoutine(pathToCounter, checkEntranceDoorway: true));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(cashierLookPos, 0.40f));

        lookAtPlayerHead = true;
        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("John: Excuse me."));
        }

        // Wait for player to interact from PlayerNpcInteract
        cashierInteractTriggered = false;
        currentState = JohnState.WaitingForCashierInteract;
        while (!cashierInteractTriggered)
        {
            yield return null;
        }

        currentState = JohnState.TalkingBathroomRequest;
        if (NightShiftStoryController.Instance != null)
        {
            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("John: Can I use the bathroom? It's really raining outside too."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("John: This is the only nearest bathroom I could find."));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("You: Umm sure?"));
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(NightShiftStoryController.Instance.PlayCustomDialogueRoutine("John: Thanks."));
            yield return new WaitForSeconds(2.0f);
        }
        lookAtPlayerHead = false;

        // Walk accurately into Male_Restroom without bumping anything, open door, step inside, close & lock door
        currentState = JohnState.WalkingToRestroom;

        Vector3 doorOutsidePos = new Vector3(-0.08f, 0.17f, 1.38f);
        Vector3 doorThresholdPos = new Vector3(0.04f, 0.17f, 2.08f);
        Vector3 restroomCloseStandPos = new Vector3(0.04f, 0.17f, 2.86f);
        Vector3 restroomInsidePos = new Vector3(0.04f, 0.17f, 3.85f);

        if (storeRootTransform != null)
        {
            // In Convenience_Store_V2 local space:
            // Partition wall is at localX = -4.44, Male Restroom doorway center is localZ = 2.65
            doorOutsidePos = storeRootTransform.TransformPoint(new Vector3(-5.15f, 0.03f, 2.75f));
            doorThresholdPos = storeRootTransform.TransformPoint(new Vector3(-4.44f, 0.03f, 2.65f));
            restroomCloseStandPos = storeRootTransform.TransformPoint(new Vector3(-3.65f, 0.03f, 2.75f));
            restroomInsidePos = storeRootTransform.TransformPoint(new Vector3(-2.45f, 0.03f, 2.65f));
        }
        else if (maleRestroomDoor != null)
        {
            Vector3 dp = maleRestroomDoor.transform.position;
            doorThresholdPos = new Vector3(dp.x - 0.45f, 0.17f, dp.z);
            doorOutsidePos = doorThresholdPos + new Vector3(-0.10f, 0f, -0.75f);
            restroomCloseStandPos = doorThresholdPos + new Vector3(-0.10f, 0f, 0.78f);
            restroomInsidePos = doorThresholdPos + new Vector3(0f, 0f, 1.65f);
        }

        List<Vector3> pathToRestroomDoor = new List<Vector3>
        {
            new Vector3(0.45f, 0.17f, -5.25f),
            new Vector3(-0.52f, 0.17f, -4.75f),
            new Vector3(-0.72f, 0.17f, -3.50f),
            new Vector3(-0.72f, 0.17f, doorOutsidePos.z),
            doorOutsidePos
        };

        yield return StartCoroutine(FollowWaypointsRoutine(pathToRestroomDoor, checkEntranceDoorway: false));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(doorThresholdPos, 0.35f));

        // 1. Realistic Door Opening Animation: Reach right hand to Handle_Out and push door open along its swing arc
        float elapsed = 0f;
        while (elapsed < 0.42f)
        {
            elapsed += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(0f, 1f, elapsed / 0.42f);
            rightArmWorldTarget = handleOutTransform != null
                ? handleOutTransform.position
                : (doorThresholdPos + Vector3.up * 1.05f);
            yield return null;
        }

        if (maleRestroomDoor != null && !maleRestroomDoor.isOpen)
        {
            maleRestroomDoor.ToggleDoor();
        }

        // Keep right hand tracking Handle_Out as the door swings open
        elapsed = 0f;
        while (elapsed < 0.56f)
        {
            elapsed += Time.deltaTime;
            float u = elapsed / 0.56f;
            rightArmReachWeight = u < 0.65f ? 1f : Mathf.SmoothStep(1f, 0f, (u - 0.65f) / 0.35f);
            rightArmWorldTarget = handleOutTransform != null
                ? handleOutTransform.position
                : (doorThresholdPos + Vector3.up * 1.05f);
            yield return null;
        }
        rightArmReachWeight = 0f;

        // 2. Step inside Male_Restroom and turn around to face the doorway
        List<Vector3> stepInsidePath = new List<Vector3>
        {
            doorThresholdPos,
            restroomCloseStandPos
        };
        yield return StartCoroutine(FollowWaypointsRoutine(stepInsidePath, checkEntranceDoorway: false));
        yield return StartCoroutine(TurnSmoothlyTowardsRoutine(doorOutsidePos, 0.38f));

        // 3. Realistic Door Closing & Locking Animation: Reach right hand to Handle_In, pull door closed, and lock
        elapsed = 0f;
        while (elapsed < 0.38f)
        {
            elapsed += Time.deltaTime;
            rightArmReachWeight = Mathf.SmoothStep(0f, 1f, elapsed / 0.38f);
            rightArmWorldTarget = handleInTransform != null
                ? handleInTransform.position
                : (doorThresholdPos + Vector3.up * 1.05f);
            yield return null;
        }

        if (maleRestroomDoor != null)
        {
            if (maleRestroomDoor.isOpen)
            {
                maleRestroomDoor.ToggleDoor();
            }
            maleRestroomDoor.isLockedByJohnAct4 = true;
            maleRestroomDoor.isUnlockedWithKey = false;
        }

        // Follow Handle_In as the door swings closed and twist the lock
        elapsed = 0f;
        while (elapsed < 0.58f)
        {
            elapsed += Time.deltaTime;
            float u = elapsed / 0.58f;
            rightArmReachWeight = u < 0.65f ? 1f : Mathf.SmoothStep(1f, 0f, (u - 0.65f) / 0.35f);
            rightArmWorldTarget = handleInTransform != null
                ? handleInTransform.position
                : (doorThresholdPos + Vector3.up * 1.05f);
            yield return null;
        }
        rightArmReachWeight = 0f;

        // Step deeper into the restroom
        List<Vector3> settlePath = new List<Vector3> { restroomInsidePos };
        yield return StartCoroutine(FollowWaypointsRoutine(settlePath, checkEntranceDoorway: false));

        currentState = JohnState.InsideRestroomLocked;
    }

    public void PrepareMurderSceneInRestroom()
    {
        ResolveTargets();
        currentState = JohnState.DeadOnRestroomFloor;
        isCurrentlyWalking = false;
        lookAtPlayerHead = false;

        Vector3 floorSpot1 = new Vector3(-0.72f, 0.18f, 4.85f);
        Vector3 floorSpot2 = new Vector3(-1.35f, 0.18f, 5.45f);
        Vector3 bodySpot = new Vector3(-1.10f, 0.21f, 5.25f);

        if (storeRootTransform != null)
        {
            floorSpot1 = storeRootTransform.TransformPoint(new Vector3(-3.05f, 0.038f, 2.55f));
            floorSpot2 = storeRootTransform.TransformPoint(new Vector3(-2.05f, 0.038f, 3.15f));
            bodySpot = storeRootTransform.TransformPoint(new Vector3(-2.15f, 0.06f, 2.95f));
        }

        PrimaryBloodStainWorldPos = floorSpot1;
        SecondaryBloodStainWorldPos = floorSpot2;
        DeadBodyLookWorldPos = bodySpot;

        // Pose JohnNPC (or deadManOverrideObj) lying flat on the restroom floor
        if (deadManOverrideObj != null)
        {
            SetNpcVisible(false);
            deadManOverrideObj.SetActive(true);
            deadManOverrideObj.transform.position = bodySpot;
        }
        else
        {
            SetNpcVisible(true);
            if (npcCapsule != null) npcCapsule.enabled = false;
            transform.position = bodySpot + Vector3.up * 0.12f;
            transform.rotation = Quaternion.Euler(-88f, 35f, 0f);
        }

        // Spawn realistic layered arterial blood pools & splatters on the restroom floor around JohnNPC
        for (int i = 0; i < spawnedRestroomBloodStains.Count; i++)
        {
            if (spawnedRestroomBloodStains[i] != null) Destroy(spawnedRestroomBloodStains[i]);
        }
        spawnedRestroomBloodStains.Clear();

        float floorY = Mathf.Min(floorSpot1.y, floorSpot2.y);
        Vector3 poolPos1 = new Vector3(floorSpot1.x, floorY + 0.004f, floorSpot1.z);
        Vector3 poolPos2 = new Vector3(floorSpot2.x, floorY + 0.0045f, floorSpot2.z);
        Vector3 bodyPoolPos = new Vector3(bodySpot.x - 0.08f, floorY + 0.005f, bodySpot.z - 0.06f);

        spawnedRestroomBloodStains.Add(CreateBloodStainDecal("Restroom_BloodStain_1", poolPos1, new Vector3(1.28f, 1.18f, 1f), 18f, 0));
        spawnedRestroomBloodStains.Add(CreateBloodStainDecal("Restroom_BloodStain_2", poolPos2, new Vector3(1.56f, 1.42f, 1f), -42f, 1));
        spawnedRestroomBloodStains.Add(CreateBloodStainDecal("Restroom_BloodStain_3", bodyPoolPos, new Vector3(1.38f, 1.25f, 1f), 74f, 2));
    }

    private static Material[] cachedRestroomBloodPoolMats;

    private static GameObject CreateBloodStainDecal(string name, Vector3 worldPos, Vector3 scale, float yaw, int variantIdx = 0)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.Destroy(quad.GetComponent<Collider>());
        quad.transform.position = worldPos;
        quad.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
        quad.transform.localScale = scale;

        if (cachedRestroomBloodPoolMats == null || cachedRestroomBloodPoolMats.Length < 3)
        {
            cachedRestroomBloodPoolMats = new Material[3];
        }
        int idx = Mathf.Clamp(variantIdx, 0, 2);
        if (cachedRestroomBloodPoolMats[idx] == null)
        {
            cachedRestroomBloodPoolMats[idx] = BuildRealisticBloodPoolMaterial(idx);
        }

        Renderer r = quad.GetComponent<Renderer>();
        r.sharedMaterial = cachedRestroomBloodPoolMats[idx];
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return quad;
    }

    private static Material BuildRealisticBloodPoolMaterial(int variantSeed)
    {
        int size = 256;
        Texture2D albedoTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normalTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D glossTex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        albedoTex.filterMode = FilterMode.Bilinear;
        albedoTex.wrapMode = TextureWrapMode.Clamp;
        normalTex.filterMode = FilterMode.Bilinear;
        normalTex.wrapMode = TextureWrapMode.Clamp;
        glossTex.filterMode = FilterMode.Bilinear;
        glossTex.wrapMode = TextureWrapMode.Clamp;

        float[,] heightField = new float[size, size];
        float seedX = 13.7f + variantSeed * 31.4f;
        float seedY = 29.1f + variantSeed * 19.8f;

        Color coagulatedCore = new Color(0.10f, 0.005f, 0.005f, 0.98f);
        Color deepVenous = new Color(0.24f, 0.012f, 0.012f, 0.96f);
        Color arterialCrimson = new Color(0.42f, 0.024f, 0.020f, 0.93f);
        Color wetRimBlood = new Color(0.28f, 0.014f, 0.012f, 0.95f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float v = y / (float)(size - 1);
                float dx = (u - 0.5f) * 2f;
                float dy = (v - 0.5f) * 2f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // Domain-warped organic liquid pooling FBM
                float warpX = Mathf.PerlinNoise(u * 3.8f + seedX, v * 3.8f + seedY) * 0.35f;
                float warpY = Mathf.PerlinNoise(u * 3.8f + seedY, v * 3.8f + seedX) * 0.35f;
                float fbm1 = Mathf.PerlinNoise((u + warpX) * 4.5f + seedX, (v + warpY) * 4.5f + seedY);
                float fbm2 = Mathf.PerlinNoise((u - warpY) * 9.5f + seedY, (v + warpX) * 9.5f + seedX) * 0.5f;
                float fbm3 = Mathf.PerlinNoise(u * 20f + seedX, v * 20f + seedY) * 0.25f;
                float organicNoise = (fbm1 + fbm2 + fbm3) / 1.75f;

                // Angular liquid lobes & splash tendrils radiating outward
                float lobeWave = Mathf.PerlinNoise(Mathf.Cos(angle) * 2.4f + seedX, Mathf.Sin(angle) * 2.4f + seedY);
                float splashTendril = Mathf.PerlinNoise(Mathf.Cos(angle) * 6.5f + seedY, Mathf.Sin(angle) * 6.5f + seedX);
                float poolRadius = 0.42f + lobeWave * 0.28f + Mathf.Pow(splashTendril, 2.2f) * 0.22f + (organicNoise - 0.5f) * 0.16f;

                float alpha = 0f;
                float hVal = 0f;

                if (dist < poolRadius)
                {
                    float edgeDist = (poolRadius - dist) / Mathf.Max(0.001f, poolRadius);
                    // Viscous liquid meniscus profile (steep rounded edge, thick center)
                    alpha = Mathf.Clamp01(edgeDist * 14.0f);
                    float meniscusBump = Mathf.Sin(Mathf.Clamp01(edgeDist * 5.0f) * Mathf.PI) * 0.28f;
                    hVal = Mathf.Clamp01(Mathf.Pow(edgeDist, 0.45f) * 0.78f + meniscusBump + (organicNoise - 0.5f) * 0.15f);
                }

                // High-velocity satellite arterial droplets & streaks around the main pool
                if (alpha < 0.15f && dist < 0.94f && dist > 0.36f)
                {
                    float radialStreak = Mathf.PerlinNoise(angle * 7.5f + seedX, dist * 3.2f + seedY);
                    float dropletNoise = Mathf.PerlinNoise(u * 32f + seedX * 1.7f, v * 32f + seedY * 1.3f);
                    float threshold = Mathf.Lerp(0.71f, 0.88f, (dist - 0.36f) / 0.58f);
                    if (dropletNoise > threshold && radialStreak > 0.44f)
                    {
                        float dropA = Mathf.Clamp01((dropletNoise - threshold) * 9.5f);
                        alpha = Mathf.Max(alpha, dropA * 0.94f);
                        hVal = Mathf.Max(hVal, dropA * 0.65f);
                    }
                }

                heightField[x, y] = hVal * alpha;

                if (alpha > 0.01f)
                {
                    // Realistic coagulation gradient: dark clotted center -> rich crimson mid-pool -> surface-tension dark wet rim
                    float coreBlend = Mathf.Clamp01(dist / Mathf.Max(0.01f, poolRadius));
                    Color c;
                    if (coreBlend < 0.45f)
                    {
                        c = Color.Lerp(coagulatedCore, deepVenous, coreBlend / 0.45f);
                    }
                    else if (coreBlend < 0.85f)
                    {
                        c = Color.Lerp(deepVenous, arterialCrimson, (coreBlend - 0.45f) / 0.40f);
                    }
                    else
                    {
                        c = Color.Lerp(arterialCrimson, wetRimBlood, (coreBlend - 0.85f) / 0.15f);
                    }

                    c = Color.Lerp(c, coagulatedCore, Mathf.Clamp01((0.45f - organicNoise) * 0.8f));
                    c.a = Mathf.Clamp01(alpha * 0.97f);
                    albedoTex.SetPixel(x, y, c);

                    // Mask wet specular reflections strictly to opaque blood pixels so transparent quad corners never shine
                    float smoothness = Mathf.Clamp01(alpha * 0.95f);
                    glossTex.SetPixel(x, y, new Color(0f, 0f, 0f, smoothness));
                }
                else
                {
                    albedoTex.SetPixel(x, y, Color.clear);
                    glossTex.SetPixel(x, y, Color.clear);
                }
            }
        }

        // Compute 3D liquid meniscus normal map from heightField gradient
        float bumpStrength = 3.2f;
        for (int y = 0; y < size; y++)
        {
            int y0 = Mathf.Max(0, y - 1);
            int y1 = Mathf.Min(size - 1, y + 1);
            for (int x = 0; x < size; x++)
            {
                int x0 = Mathf.Max(0, x - 1);
                int x1 = Mathf.Min(size - 1, x + 1);
                float dhdx = (heightField[x1, y] - heightField[x0, y]) * bumpStrength;
                float dhdy = (heightField[x, y1] - heightField[x, y0]) * bumpStrength;
                Vector3 n = new Vector3(-dhdx, -dhdy, 1.0f).normalized;
                normalTex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
            }
        }

        albedoTex.Apply();
        normalTex.Apply();
        glossTex.Apply();

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(sh);
        mat.name = $"Act4_RestroomBloodPool_MAT_{variantSeed}";
        mat.mainTexture = albedoTex;
        mat.SetColor("_BaseColor", Color.white);
        mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedoTex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedoTex);
        if (mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normalTex);
            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.25f);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (mat.HasProperty("_MetallicGlossMap"))
        {
            mat.SetTexture("_MetallicGlossMap", glossTex);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_SmoothnessTextureChannel")) mat.SetFloat("_SmoothnessTextureChannel", 0f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1.0f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
        if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 1.0f);
        if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 1.0f);
        if (mat.HasProperty("_BlendModePreserveSpecular")) mat.SetFloat("_BlendModePreserveSpecular", 0f);
        if (mat.HasProperty("_ReceiveShadows")) mat.SetFloat("_ReceiveShadows", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetOverrideTag("RenderType", "Transparent");
        return mat;
    }

    public string GetPromptText()
    {
        if (NightShiftStoryController.IsDialogueActive) return string.Empty;
        if (currentState != JohnState.WaitingForCashierInteract) return string.Empty;

        if (!IsPlayerInsideCashierZone)
        {
            return "Go to Cashier area first.";
        }
        return "E to Interact";
    }

    public void Interact()
    {
        if (NightShiftStoryController.IsDialogueActive) return;
        if (currentState == JohnState.WaitingForCashierInteract && IsPlayerInsideCashierZone)
        {
            cashierInteractTriggered = true;
        }
    }

    private static Vector3 PushOutOfBox2D(Vector3 pos, float minX, float maxX, float minZ, float maxZ, float margin)
    {
        float x0 = minX - margin;
        float x1 = maxX + margin;
        float z0 = minZ - margin;
        float z1 = maxZ + margin;

        if (pos.x > x0 && pos.x < x1 && pos.z > z0 && pos.z < z1)
        {
            float dW = pos.x - x0;
            float dE = x1 - pos.x;
            float dS = pos.z - z0;
            float dN = z1 - pos.z;
            float m = Mathf.Min(Mathf.Min(dW, dE), Mathf.Min(dS, dN));
            if (m == dW) pos.x = x0;
            else if (m == dE) pos.x = x1;
            else if (m == dS) pos.z = z0;
            else pos.z = z1;
        }
        return pos;
    }

    private static Vector3 EnforceStoreFurnitureClearance(Vector3 pos)
    {
        // Gondola shelves (Shelf1, Shelf2, Shelf3) and Front-West Snack Racks (chipShelve)
        pos = PushOutOfBox2D(pos, -0.16f, 2.96f, -0.19f, 0.44f, 0.30f);  // Shelf2
        pos = PushOutOfBox2D(pos, -0.16f, 2.96f, -2.15f, -1.51f, 0.30f); // Shelf3
        pos = PushOutOfBox2D(pos, -0.07f, 2.75f, -4.43f, -3.80f, 0.30f); // Shelf1
        pos = PushOutOfBox2D(pos, -1.45f, -0.98f, -6.32f, -4.99f, 0.28f); // chipShelve
        return pos;
    }

    private IEnumerator FollowWaypointsRoutine(List<Vector3> waypoints, bool checkEntranceDoorway)
    {
        isCurrentlyWalking = true;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 target = waypoints[i];
            while (true)
            {
                Vector3 cur = transform.position;
                Vector3 toTarget = new Vector3(target.x - cur.x, 0f, target.z - cur.z);
                float dist = toTarget.magnitude;
                if (dist <= 0.09f) break;

                if (checkEntranceDoorway && slidingDoors != null)
                {
                    float dDoor = Vector2.Distance(new Vector2(cur.x, cur.z), new Vector2(slidingDoors.transform.position.x, slidingDoors.transform.position.z));
                    if (dDoor < 1.35f)
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

                RotateTowardsWorldDir(toTarget.normalized, Time.deltaTime * turnSpeed);
                float step = Mathf.Min(dist, walkSpeed * Time.deltaTime);
                Vector3 nextPos = cur + toTarget.normalized * step;
                nextPos = EnforceStoreFurnitureClearance(nextPos);
                nextPos.y = SampleGroundHeight(nextPos, cur.y);
                transform.position = nextPos;

                walkCycleTimer += Time.deltaTime * 5.2f;
                footstepTimer += Time.deltaTime;
                if (footstepTimer >= 0.56f)
                {
                    footstepTimer = 0f;
                    if (npcAudioSource != null && footstepClip != null)
                    {
                        npcAudioSource.pitch = Random.Range(0.90f, 1.05f);
                        npcAudioSource.PlayOneShot(footstepClip, 0.24f);
                    }
                }
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
            Vector3 dir = worldLookTarget - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                RotateTowardsWorldDir(dir.normalized, Time.deltaTime * (turnSpeed * 1.5f));
            }
            yield return null;
        }
    }

    private void RotateTowardsWorldDir(Vector3 worldForwardDir, float maxRadiansDelta)
    {
        if (worldForwardDir.sqrMagnitude < 0.0001f) return;
        Quaternion lookRot = Quaternion.LookRotation(worldForwardDir.normalized, Vector3.up);
        Quaternion corrected = lookRot * Quaternion.Euler(0f, modelYawCorrectionDeg, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, corrected, Mathf.Clamp01(maxRadiansDelta));
    }

    private float SampleGroundHeight(Vector3 pos, float currentY)
    {
        Vector3 origin = new Vector3(pos.x, currentY + 0.65f, pos.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 2.2f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return currentY;

        float highestFloorY = float.MinValue;
        float maxStepUpY = currentY + 0.48f;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
            if (h.normal.y < 0.70f || h.point.y > maxStepUpY) continue;
            if (h.point.y > highestFloorY) highestFloorY = h.point.y;
        }
        if (highestFloorY == float.MinValue) return currentY;
        return Mathf.Lerp(currentY, highestFloorY, Mathf.Clamp01(Time.deltaTime * 22f));
    }

    private void SnapToGroundImmediate()
    {
        Vector3 origin = transform.position + Vector3.up * 0.65f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore);
        float highestFloorY = float.MinValue;
        float maxStepUpY = transform.position.y + 0.55f;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
            if (h.normal.y < 0.70f || h.point.y > maxStepUpY) continue;
            if (h.point.y > highestFloorY) highestFloorY = h.point.y;
        }
        if (highestFloorY > float.MinValue)
        {
            transform.position = new Vector3(transform.position.x, highestFloorY, transform.position.z);
        }
    }

    private float footLSoleOffset = 0.08f;
    private float footLEndSoleOffset = 0.02f;
    private float footRSoleOffset = 0.08f;
    private float footREndSoleOffset = 0.02f;

    private void CacheRigAndBindPose(Animator anim)
    {
        Quaternion savedRot = transform.rotation;
        transform.rotation = Quaternion.identity;

        Transform[] allBones = GetComponentsInChildren<Transform>(true);
        bindLocalRots.Clear();

        for (int i = 0; i < allBones.Length; i++)
        {
            Transform b = allBones[i];
            if (b == transform) continue;
            bindLocalRots[b] = b.localRotation;

            // Strip any "mixamorig:" prefix so Mixamo, PS1, and standard humanoid rigs all match cleanly
            string raw = b.name.ToLowerInvariant();
            int colonIdx = raw.LastIndexOf(':');
            string n = colonIdx >= 0 ? raw.Substring(colonIdx + 1).Trim() : raw.Trim();

            if (n == "hips" || n == "pelvis") pelvis = b;
            else if (n == "spine") spine = b;
            else if (n == "spine1" || n == "spine2" || n == "chest" || n == "spine.001") chest = b;
            else if (n == "neck") neck = b;
            else if (n == "head") head = b;
            else if (n == "leftarm" || n == "upper_arm_left" || n == "upper_arm.l" || n == "upperarm_l") upperArmL = b;
            else if (n == "leftforearm" || n == "forearm_left" || n == "forearm.l" || n == "lowerarm_l") forearmL = b;
            else if (n == "lefthand" || n == "hand_left" || n == "hand.l") handL = b;
            else if (n == "rightarm" || n == "upper_arm_right" || n == "upper_arm.r" || n == "upperarm_r") upperArmR = b;
            else if (n == "rightforearm" || n == "forearm_right" || n == "forearm.r" || n == "lowerarm_r") forearmR = b;
            else if (n == "righthand" || n == "hand_right" || n == "hand.r") handR = b;
            else if (n == "leftupleg" || n == "thigh_left" || n == "thigh.l") thighL = b;
            else if (n == "leftleg" || n == "shin_left" || n == "shin.l" || n == "calf_l") shinL = b;
            else if (n == "leftfoot" || n == "foot_left" || n == "foot.l") footL = b;
            else if (n == "lefttoebase" || n == "foot_left_end" || n == "toe.l") footLEnd = b;
            else if (n == "rightupleg" || n == "thigh_right" || n == "thigh.r") thighR = b;
            else if (n == "rightleg" || n == "shin_right" || n == "shin.r" || n == "calf_r") shinR = b;
            else if (n == "rightfoot" || n == "foot_right" || n == "foot.r") footR = b;
            else if (n == "righttoebase" || n == "foot_right_end" || n == "toe.r") footREnd = b;
        }

        // Also query Humanoid Avatar if available as an additional guarantee
        if (anim != null && anim.isHuman)
        {
            if (pelvis == null) pelvis = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (spine == null) spine = anim.GetBoneTransform(HumanBodyBones.Spine);
            if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            if (neck == null) neck = anim.GetBoneTransform(HumanBodyBones.Neck);
            if (head == null) head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (upperArmL == null) upperArmL = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (forearmL == null) forearmL = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (handL == null) handL = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            if (upperArmR == null) upperArmR = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (forearmR == null) forearmR = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (handR == null) handR = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (thighL == null) thighL = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            if (shinL == null) shinL = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            if (footL == null) footL = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (footLEnd == null) footLEnd = anim.GetBoneTransform(HumanBodyBones.LeftToes);
            if (thighR == null) thighR = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (shinR == null) shinR = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            if (footR == null) footR = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if (footREnd == null) footREnd = anim.GetBoneTransform(HumanBodyBones.RightToes);
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

    private static void ApplyArmReach(
        Transform upperArm,
        Transform forearm,
        Transform hand,
        Vector3 worldTarget,
        Vector3 wRight,
        float weight)
    {
        Vector3 shoulderPos = upperArm.position;
        Vector3 toTarget = worldTarget - shoulderPos;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Vector3 curUpperDir = (forearm.position - shoulderPos).normalized;
        float dist = toTarget.magnitude;
        float elbowBend = Mathf.Clamp((0.66f - dist) * 90f, 12f, 44f);
        Quaternion upperGoal = Quaternion.FromToRotation(curUpperDir, toTarget.normalized)
                               * Quaternion.AngleAxis(-elbowBend * 0.42f, wRight)
                               * upperArm.rotation;
        upperArm.rotation = Quaternion.Slerp(upperArm.rotation, upperGoal, weight);

        Vector3 newForearmDir = (hand.position - forearm.position).normalized;
        Vector3 elbowToTarget = (worldTarget - forearm.position).normalized;
        if (newForearmDir.sqrMagnitude > 0.0001f && elbowToTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion forearmGoal = Quaternion.FromToRotation(newForearmDir, elbowToTarget) * forearm.rotation;
            forearm.rotation = Quaternion.Slerp(forearm.rotation, forearmGoal, weight);
        }
    }

    private void LateUpdate()
    {
        if (!isNpcVisible || currentState == JohnState.DeadOnRestroomFloor) return;
        idleTimer += Time.deltaTime;
        if (!rigInitialized) return;

        foreach (var kvp in bindLocalRots)
        {
            if (kvp.Key != null) kvp.Key.localRotation = kvp.Value;
        }
        if (pelvis != null) pelvis.localPosition = pelvisBindLocalPos;

        Vector3 wRight = transform.TransformDirection(modelRightLocal).normalized;
        Vector3 wFwd = transform.TransformDirection(modelForwardLocal).normalized;

        // Bring T-pose arms down naturally beside John's torso
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
        if (forearmL != null) forearmL.rotation = Quaternion.AngleAxis(-10f, wRight) * forearmL.rotation;
        if (forearmR != null) forearmR.rotation = Quaternion.AngleAxis(-10f, wRight) * forearmR.rotation;

        float qWalk = Mathf.Floor(walkCycleTimer * (ps1AnimFps / 5.2f)) / (ps1AnimFps / 5.2f);
        float qIdle = Mathf.Floor(idleTimer * ps1AnimFps) / ps1AnimFps;

        if (isCurrentlyWalking)
        {
            float stride = Mathf.Sin(qWalk);
            float swingPass = Mathf.Cos(qWalk);
            float legAngle = stride * 25f;

            if (thighL != null) thighL.rotation = Quaternion.AngleAxis(-legAngle, wRight) * thighL.rotation;
            if (thighR != null) thighR.rotation = Quaternion.AngleAxis(legAngle, wRight) * thighR.rotation;
            if (shinL != null) shinL.rotation = Quaternion.AngleAxis(Mathf.Max(0f, swingPass) * 32f, wRight) * shinL.rotation;
            if (shinR != null) shinR.rotation = Quaternion.AngleAxis(Mathf.Max(0f, -swingPass) * 32f, wRight) * shinR.rotation;
            if (footL != null) footL.rotation = Quaternion.AngleAxis(-Mathf.Max(0f, swingPass) * 10f, wRight) * footL.rotation;
            if (footR != null) footR.rotation = Quaternion.AngleAxis(-Mathf.Max(0f, -swingPass) * 10f, wRight) * footR.rotation;

            if (spine != null)
            {
                spine.rotation = Quaternion.AngleAxis(-3.5f, wRight) * spine.rotation;
                spine.rotation = Quaternion.AngleAxis(stride * 4.0f, Vector3.up) * spine.rotation;
            }

            if (upperArmL != null) upperArmL.rotation = Quaternion.AngleAxis(legAngle * 0.65f, wRight) * upperArmL.rotation;
            if (forearmL != null) forearmL.rotation = Quaternion.AngleAxis(-Mathf.Max(0f, -legAngle) * 0.45f, wRight) * forearmL.rotation;
            if (upperArmR != null) upperArmR.rotation = Quaternion.AngleAxis(-legAngle * 0.65f, wRight) * upperArmR.rotation;
            if (forearmR != null) forearmR.rotation = Quaternion.AngleAxis(-Mathf.Max(0f, legAngle) * 0.45f, wRight) * forearmR.rotation;
        }
        else
        {
            float breath = Mathf.Sin(qIdle * 1.9f) * 1.8f;
            if (chest != null) chest.rotation = Quaternion.AngleAxis(breath, wRight) * chest.rotation;
        }

        if (lookAtPlayerHead && head != null)
        {
            Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - head.position;
                Vector3 flatToCam = new Vector3(toCam.x, 0f, toCam.z);
                if (flatToCam.sqrMagnitude > 0.01f)
                {
                    float yaw = Mathf.Clamp(Vector3.SignedAngle(wFwd, flatToCam.normalized, Vector3.up), -50f, 50f);
                    head.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * head.rotation;
                }
            }
        }

        if (rightArmReachWeight > 0.001f && upperArmR != null && forearmR != null && handR != null)
        {
            if (spine != null)
            {
                spine.rotation = Quaternion.AngleAxis(-6f * rightArmReachWeight, wRight) * spine.rotation;
            }
            ApplyArmReach(upperArmR, forearmR, handR, rightArmWorldTarget, wRight, rightArmReachWeight);
        }

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
}

// =============================================================================
// 4. MULTIPURPOSE KEY ("KeyMultiPurpose" / "KeyMultiPurposes" ON REGISTER - UNTOUCHED TRANSFORM)
// =============================================================================
public class InteractableMultipurposeKey : MonoBehaviour, IInteractable
{
    public static InteractableMultipurposeKey Instance { get; private set; }

    public bool isPickedUp = false;
    private readonly List<Renderer> allKeyPartRenderers = new List<Renderer>();
    private Collider[] keyColliders;
    private GameObject heldKeyViewmodel;
    private Renderer[] heldKeyViewmodelRenderers;
    private AudioSource audioSource;
    private AudioClip keyPickupClip;
    private Vector3 keyWorldCenter;

    public static InteractableMultipurposeKey EnsureAttachedToSceneKey()
    {
        if (Instance != null) return Instance;

        InteractableMultipurposeKey existing = Object.FindAnyObjectByType<InteractableMultipurposeKey>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        Transform keyTarget = null;

        // Pass 1: Exact or trimmed match for "KeyMultiPurpose" / "KeyMultiPurposes"
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            string trimmed = t.name.Trim();
            if (string.Equals(trimmed, "KeyMultiPurpose", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "KeyMultiPurposes", System.StringComparison.OrdinalIgnoreCase) ||
                trimmed.IndexOf("KeyMultiPurpose", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.IndexOf("MultiPurpose", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                keyTarget = t;
                break;
            }
        }

        // Pass 2: Any child of Register containing "Key"
        if (keyTarget == null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                if (t.name.IndexOf("Key", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    t.GetComponent<InteractableMultipurposeKey>() == null)
                {
                    keyTarget = t;
                    break;
                }
            }
        }

        if (keyTarget == null) return null;

        // CRITICAL: Do NOT move or touch keyTarget's Transform! Only attach InteractableMultipurposeKey.
        keyTarget.gameObject.SetActive(true);
        Instance = keyTarget.gameObject.GetComponent<InteractableMultipurposeKey>();
        if (Instance == null)
        {
            Instance = keyTarget.gameObject.AddComponent<InteractableMultipurposeKey>();
        }
        return Instance;
    }

    private void Awake()
    {
        Instance = this;
        CollectRegisterKeyRenderers();

        // Size a tight local-space BoxCollider directly from the key's MeshFilter.sharedMesh.bounds
        // (Register has large lossyScale ~500 due to FBX units, so using sharedMesh.bounds keeps the trigger tight around the key!)
        MeshFilter mf = GetComponent<MeshFilter>();
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
        }
        box.isTrigger = true;
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds mb = mf.sharedMesh.bounds;
            box.center = mb.center;
            box.size = new Vector3(
                Mathf.Max(mb.size.x * 3.2f, 0.0002f),
                Mathf.Max(mb.size.y * 3.2f, 0.0002f),
                Mathf.Max(mb.size.z * 3.2f, 0.0002f));
        }

        Renderer r = GetComponent<Renderer>();
        keyWorldCenter = (r != null) ? r.bounds.center : transform.position;
        keyColliders = GetComponentsInChildren<Collider>(true);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.2f;
        audioSource.playOnAwake = false;

        int sr = 44100;
        int len = (int)(sr * 0.22f);
        float[] buf = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 28f);
            float jingle = Mathf.Sin(2f * Mathf.PI * 3120f * t) * 0.45f + Mathf.Sin(2f * Mathf.PI * 4480f * t) * 0.35f;
            buf[i] = jingle * env * 0.65f;
        }
        keyPickupClip = AudioClip.Create("KeyPickupJingle", len, 1, sr, false);
        keyPickupClip.SetData(buf, 0);
    }

    private void CollectRegisterKeyRenderers()
    {
        allKeyPartRenderers.Clear();
        Renderer[] ownRends = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < ownRends.Length; i++)
        {
            if (ownRends[i] != null && !allKeyPartRenderers.Contains(ownRends[i]))
            {
                allKeyPartRenderers.Add(ownRends[i]);
            }
        }

        // In kasa_fbx.fbx (Register), the key in the keyhole has sibling mesh parts ("klíč" and "Torus") right next to KeyMultiPurpose
        if (transform.parent != null)
        {
            for (int i = 0; i < transform.parent.childCount; i++)
            {
                Transform sib = transform.parent.GetChild(i);
                if (sib == null || sib == transform) continue;
                string sn = sib.name.Trim();
                if (sn.StartsWith("kl", System.StringComparison.OrdinalIgnoreCase) ||
                    sn.StartsWith("Torus", System.StringComparison.OrdinalIgnoreCase) ||
                    sn.IndexOf("Key", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Renderer sr = sib.GetComponent<Renderer>();
                    if (sr != null && !allKeyPartRenderers.Contains(sr))
                    {
                        allKeyPartRenderers.Add(sr);
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.OnSlotChanged -= HandleHotbarSlotChanged;
        }
    }

    public string GetPromptText()
    {
        if (isPickedUp || NightShiftStoryController.IsDialogueActive) return string.Empty;

        // Key can ONLY be picked up once needed ("Task: Force to open the door")!
        if (!NightShiftStoryController.IsAct4ForceOpenRestroomTaskActive) return string.Empty;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, keyWorldCenter) > 2.25f)
        {
            return string.Empty;
        }

        return "E to get the Multipurpose Key";
    }

    public void Interact()
    {
        if (isPickedUp || NightShiftStoryController.IsDialogueActive) return;

        // Key can ONLY be picked up once needed ("Task: Force to open the door")!
        if (!NightShiftStoryController.IsAct4ForceOpenRestroomTaskActive) return;

        isPickedUp = true;

        if (audioSource != null && keyPickupClip != null)
        {
            audioSource.PlayOneShot(keyPickupClip, 0.85f);
        }

        // Create viewmodel in player's hand using the actual KeyMultiPurpose model meshes from the scene BEFORE hiding them
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null)
        {
            heldKeyViewmodel = BuildHeldViewmodelFromSceneKey();
            heldKeyViewmodel.transform.SetParent(cam.transform, false);
            heldKeyViewmodel.transform.localPosition = new Vector3(0.24f, -0.19f, 0.36f);
            heldKeyViewmodel.transform.localRotation = Quaternion.Euler(15f, -35f, 10f);
            heldKeyViewmodelRenderers = heldKeyViewmodel.GetComponentsInChildren<Renderer>(true);
        }

        // Hide the key on the register so it disappears when grabbed
        for (int i = 0; i < allKeyPartRenderers.Count; i++)
        {
            if (allKeyPartRenderers[i] != null) allKeyPartRenderers[i].enabled = false;
        }
        if (keyColliders != null)
        {
            for (int i = 0; i < keyColliders.Length; i++)
            {
                if (keyColliders[i] != null) keyColliders[i].enabled = false;
            }
        }

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null)
        {
            int slot = 0;
            for (int i = 0; i < hotbar.slotCount; i++)
            {
                if (!hotbar.HasItem(i))
                {
                    slot = i;
                    break;
                }
            }
            hotbar.OnSlotChanged -= HandleHotbarSlotChanged;
            hotbar.OnSlotChanged += HandleHotbarSlotChanged;
            hotbar.SetSlot(slot, "KEY", GenerateKeyHotbarIcon());
            hotbar.SetActiveSlot(slot);
            UpdateHeldKeyVisibility(hotbar.activeSlotIndex);
        }
    }

    private GameObject BuildHeldViewmodelFromSceneKey()
    {
        GameObject holder = new GameObject("Held_KeyMultiPurpose_Viewmodel");

        Bounds combinedWorldBounds = new Bounds(keyWorldCenter, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < allKeyPartRenderers.Count; i++)
        {
            Renderer r = allKeyPartRenderers[i];
            if (r == null) continue;
            if (!hasBounds)
            {
                combinedWorldBounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                combinedWorldBounds.Encapsulate(r.bounds);
            }
        }

        Vector3 centerWorld = hasBounds ? combinedWorldBounds.center : transform.position;
        float maxExtent = hasBounds ? Mathf.Max(combinedWorldBounds.size.x, Mathf.Max(combinedWorldBounds.size.y, combinedWorldBounds.size.z)) : 0.08f;
        float scaleFactor = maxExtent > 0.0001f ? (0.11f / maxExtent) : 1f;

        for (int i = 0; i < allKeyPartRenderers.Count; i++)
        {
            Renderer srcRend = allKeyPartRenderers[i];
            if (srcRend == null) continue;
            MeshFilter srcMf = srcRend.GetComponent<MeshFilter>();
            if (srcMf == null || srcMf.sharedMesh == null) continue;

            GameObject partGo = new GameObject(srcRend.name + "_HeldClone");
            partGo.transform.position = (srcRend.transform.position - centerWorld) * scaleFactor;
            partGo.transform.rotation = srcRend.transform.rotation;
            partGo.transform.localScale = srcRend.transform.lossyScale * scaleFactor;
            partGo.transform.SetParent(holder.transform, true);

            MeshFilter mf = partGo.AddComponent<MeshFilter>();
            mf.sharedMesh = srcMf.sharedMesh;

            MeshRenderer mr = partGo.AddComponent<MeshRenderer>();
            mr.sharedMaterials = srcRend.sharedMaterials;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        return holder;
    }

    private void HandleHotbarSlotChanged(int activeSlot)
    {
        UpdateHeldKeyVisibility(activeSlot);
    }

    private void UpdateHeldKeyVisibility(int activeSlot)
    {
        if (heldKeyViewmodelRenderers == null) return;
        bool show = isPickedUp &&
                    HorrorHotbar.Instance != null &&
                    HorrorHotbar.Instance.GetSlotIndexWithItem("KEY") == activeSlot;
        for (int i = 0; i < heldKeyViewmodelRenderers.Length; i++)
        {
            if (heldKeyViewmodelRenderers[i] != null) heldKeyViewmodelRenderers[i].enabled = show;
        }
    }

    private static Texture2D GenerateKeyHotbarIcon()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color gold = new Color(0.92f, 0.78f, 0.32f, 1f);
        Color dark = new Color(0.55f, 0.42f, 0.14f, 1f);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.clear);

        // Key ring bow on left (center 10, 16, radius 5)
        for (int y = 10; y <= 22; y++)
        {
            for (int x = 4; x <= 16; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(10f, 16f));
                if (d >= 2.5f && d <= 5.5f) tex.SetPixel(x, y, gold);
            }
        }

        // Key shaft (x=15..27, y=15..17)
        for (int x = 15; x <= 27; x++)
        {
            tex.SetPixel(x, 16, gold);
            tex.SetPixel(x, 15, dark);
        }

        // Key teeth (x=22..23, 25..26, y=11..15)
        for (int y = 11; y <= 15; y++)
        {
            tex.SetPixel(22, y, gold);
            tex.SetPixel(23, y, gold);
            if (y >= 12)
            {
                tex.SetPixel(26, y, gold);
                tex.SetPixel(27, y, gold);
            }
        }

        tex.Apply();
        return tex;
    }
}

// =============================================================================
// 5. ACT 4 COUNTER PHONE (FROM HOUSESCENE) & SMOOTH 0-9 911 DIALPAD UI
// =============================================================================
public class Act4CounterPhoneAndDialpad : MonoBehaviour, IInteractable
{
    public static Act4CounterPhoneAndDialpad Instance { get; private set; }
    public static bool IsDialpadOpen { get; private set; } = false;

    public bool isPhonePickedUp = false;
    public bool hasCompleted911Dial = false;

    private Transform meshPivot;
    private Light phoneScreenLight;
    private string dialedNumber = "";
    private string statusMessage = "ENTER NUMBER TO DIAL";

    private AudioSource audioSource;
    private AudioClip[] dtmfClips;
    private AudioClip ringbackClip;

    private Texture2D pixelTex;
    private Font uiFont;
    private GUIStyle headerStyle;
    private GUIStyle displayStyle;
    private GUIStyle keyButtonStyle;
    private GUIStyle dialButtonStyle;
    private GUIStyle statusStyle;

    public static Act4CounterPhoneAndDialpad EnsurePlacedOnCounter(Vector3 counterSpotWorld)
    {
        if (Instance != null)
        {
            Instance.PositionOnPhoneBasket(counterSpotWorld);
            Instance.RefreshVisibilityGate();
            return Instance;
        }

        Act4CounterPhoneAndDialpad existing = Object.FindAnyObjectByType<Act4CounterPhoneAndDialpad>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            Instance.PositionOnPhoneBasket(counterSpotWorld);
            Instance.RefreshVisibilityGate();
            return existing;
        }

        GameObject phoneGo = new GameObject("Act4_Cashier_Phone");
        Instance = phoneGo.AddComponent<Act4CounterPhoneAndDialpad>();
        Instance.BuildPhoneModel();
        Instance.PositionOnPhoneBasket(counterSpotWorld);
        Instance.RefreshVisibilityGate();
        return Instance;
    }

    public void PositionOnPhoneBasket(Vector3 counterSpotWorld)
    {
        if (isPhonePickedUp) return;

        // Place directly on top of the flat basket ("PhoneUserHerePut") that the user placed on the counter
        Transform basketTr = null;
        GameObject directFind = GameObject.Find("PhoneUserHerePut");
        if (directFind != null)
        {
            basketTr = directFind.transform;
        }
        else
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && string.Equals(all[i].name.Trim(), "PhoneUserHerePut", System.StringComparison.OrdinalIgnoreCase))
                {
                    basketTr = all[i];
                    break;
                }
            }
        }

        Vector3 targetPos = new Vector3(counterSpotWorld.x + 0.15f, 1.04f, counterSpotWorld.z + 0.88f);
        if (basketTr != null)
        {
            Renderer r = basketTr.GetComponentInChildren<Renderer>(true);
            if (r != null && r.bounds.size.sqrMagnitude > 0.0001f)
            {
                Bounds b = r.bounds;
                targetPos = new Vector3(b.center.x, b.max.y + 0.003f, b.center.z);
            }
            else
            {
                targetPos = basketTr.position + Vector3.up * 0.015f;
            }
        }
        else
        {
            Transform cashierPc = GameObject.Find("CashierPC")?.transform;
            if (cashierPc != null)
            {
                targetPos = cashierPc.position + new Vector3(0.42f, 0.02f, 0.10f);
            }
        }

        transform.position = targetPos;
        transform.rotation = Quaternion.Euler(0f, 15f, 0f);
    }

    public void RefreshVisibilityGate()
    {
        if (isPhonePickedUp) return;
        bool shouldShow = NightShiftStoryController.IsAct4Active &&
                          (Act4PlayerUmbrella.Instance != null && Act4PlayerUmbrella.Instance.isPutDown);
        if (meshPivot != null && meshPivot.gameObject.activeSelf != shouldShow)
        {
            meshPivot.gameObject.SetActive(shouldShow);
        }
    }

    private void Awake()
    {
        Instance = this;
        IsDialpadOpen = false;
        SetupAudio();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (IsDialpadOpen)
        {
            IsDialpadOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void BuildPhoneModel()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.04f, 0f);
            col.size = new Vector3(0.32f, 0.24f, 0.32f);
        }

        meshPivot = new GameObject("PhoneMeshPivot").transform;
        meshPivot.SetParent(transform, false);

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material bodyMat = HouseAct3PropController.CreateLitMaterial(sh, new Color(0.12f, 0.13f, 0.15f), 0.45f);
        Material bezelMat = HouseAct3PropController.CreateLitMaterial(sh, new Color(0.22f, 0.24f, 0.28f), 0.55f);

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? sh;
        Material screenMat = new Material(unlit);
        Color screenCol = new Color(0.35f, 0.65f, 0.92f, 1f);
        screenMat.color = screenCol;
        if (screenMat.HasProperty("_BaseColor")) screenMat.SetColor("_BaseColor", screenCol);

        HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Cube, "PhoneBody",
            new Vector3(0f, 0.006f, 0f), Vector3.zero, new Vector3(0.074f, 0.012f, 0.148f), bodyMat);

        HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Cube, "PhoneBezel",
            new Vector3(0f, 0.006f, 0f), Vector3.zero, new Vector3(0.076f, 0.008f, 0.150f), bezelMat);

        HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Cube, "PhoneScreen",
            new Vector3(0f, 0.0125f, 0.006f), Vector3.zero, new Vector3(0.064f, 0.002f, 0.118f), screenMat);

        HouseAct3PropController.CreatePrimitivePart(meshPivot, PrimitiveType.Cube, "EarpieceSlot",
            new Vector3(0f, 0.013f, 0.068f), Vector3.zero, new Vector3(0.022f, 0.002f, 0.004f), bezelMat);

        // Subtle screen glow light so the player can easily spot their phone on the counter during the blackout
        GameObject lightGo = new GameObject("Phone_ScreenGlow_Light");
        lightGo.transform.SetParent(meshPivot, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        phoneScreenLight = lightGo.AddComponent<Light>();
        phoneScreenLight.type = LightType.Point;
        phoneScreenLight.color = new Color(0.45f, 0.72f, 0.98f);
        phoneScreenLight.range = 1.65f;
        phoneScreenLight.intensity = 0.85f;
        phoneScreenLight.shadows = LightShadows.None;
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.1f;
        audioSource.playOnAwake = false;

        int sr = 44100;
        dtmfClips = new AudioClip[10];
        for (int d = 0; d < 10; d++)
        {
            int samples = (int)(sr * 0.10f);
            float[] buf = new float[samples];
            float f1 = 697f + (d % 3) * 75f;
            float f2 = 1209f + (d / 3) * 120f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Clamp01(1f - t / 0.10f);
                buf[i] = (Mathf.Sin(2f * Mathf.PI * f1 * t) + Mathf.Sin(2f * Mathf.PI * f2 * t)) * 0.22f * env;
            }
            dtmfClips[d] = AudioClip.Create("DTMF_" + d, samples, 1, sr, false);
            dtmfClips[d].SetData(buf, 0);
        }

        int rSamples = (int)(sr * 1.2f);
        float[] rBuf = new float[rSamples];
        for (int i = 0; i < rSamples; i++)
        {
            float t = (float)i / sr;
            float env = t < 1.0f ? 1f : Mathf.Clamp01((1.2f - t) / 0.2f);
            rBuf[i] = (Mathf.Sin(2f * Mathf.PI * 440f * t) + Mathf.Sin(2f * Mathf.PI * 480f * t)) * 0.20f * env;
        }
        ringbackClip = AudioClip.Create("Phone911Ringback", rSamples, 1, sr, false);
        ringbackClip.SetData(rBuf, 0);
    }

    public void PlayRingbackTone()
    {
        if (audioSource != null && ringbackClip != null)
        {
            audioSource.PlayOneShot(ringbackClip, 0.80f);
        }
    }

    public string GetPromptText()
    {
        if (NightShiftStoryController.IsDialogueActive || IsDialpadOpen || hasCompleted911Dial) return string.Empty;
        if (!NightShiftStoryController.IsAct4Call911TaskActive) return string.Empty;

        if (!isPhonePickedUp)
        {
            return "E to pick up Phone";
        }
        return "E to call 911";
    }

    public void Interact()
    {
        if (NightShiftStoryController.IsDialogueActive || IsDialpadOpen || hasCompleted911Dial) return;
        if (!NightShiftStoryController.IsAct4Call911TaskActive) return;

        if (!isPhonePickedUp)
        {
            PickUpPhoneToHand();
        }

        OpenDialpadUI();
    }

    private void PickUpPhoneToHand()
    {
        isPhonePickedUp = true;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null)
        {
            transform.SetParent(cam.transform, false);
            transform.localPosition = new Vector3(0.24f, -0.20f, 0.38f);
            transform.localRotation = Quaternion.Euler(16f, -16f, 6f);
            transform.localScale = Vector3.one;
        }

        if (meshPivot != null)
        {
            meshPivot.localRotation = Quaternion.Euler(-78f, 180f, 0f);
        }
        if (phoneScreenLight != null)
        {
            phoneScreenLight.range = 3.2f;
            phoneScreenLight.intensity = 1.25f;
        }

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null)
        {
            hotbar.SetSlot(0, "PHONE", null);
            hotbar.SetActiveSlot(0);
        }
    }

    public void OpenDialpadUI()
    {
        if (IsDialpadOpen || hasCompleted911Dial) return;
        dialedNumber = "";
        statusMessage = "EMERGENCY DIALER";
        IsDialpadOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseDialpadUI()
    {
        if (!IsDialpadOpen) return;
        IsDialpadOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!isPhonePickedUp)
        {
            RefreshVisibilityGate();
        }

        if (isPhonePickedUp && !IsDialpadOpen && !hasCompleted911Dial &&
            NightShiftStoryController.IsAct4Call911TaskActive &&
            !NightShiftStoryController.IsDialogueActive)
        {
            if (HorrorInteractionReticle.WasMouseClicked() || HorrorInteractionReticle.WasInteractPressed())
            {
                OpenDialpadUI();
                return;
            }
        }

        if (!IsDialpadOpen) return;

        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Allow keyboard digit entry 0-9, Backspace, and Enter
        for (int d = 0; d <= 9; d++)
        {
            if (WasDigitPressedThisFrame(d))
            {
                AppendDigit(d);
            }
        }

        if (WasBackspacePressedThisFrame() && dialedNumber.Length > 0)
        {
            dialedNumber = dialedNumber.Substring(0, dialedNumber.Length - 1);
            PlayDigitBeep(0);
        }

        if (WasEnterPressedThisFrame())
        {
            AttemptDial();
        }
    }

    private static bool WasDigitPressedThisFrame(int digit)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        switch (digit)
        {
            case 0: return Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame;
            case 1: return Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame;
            case 2: return Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame;
            case 3: return Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame;
            case 4: return Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame;
            case 5: return Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame;
            case 6: return Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame;
            case 7: return Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame;
            case 8: return Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame;
            case 9: return Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.Alpha0 + digit) || Input.GetKeyDown(KeyCode.Keypad0 + digit);
#endif
    }

    private static bool WasBackspacePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Backspace);
#endif
    }

    private static bool WasEnterPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    private void AppendDigit(int digit)
    {
        if (dialedNumber.Length >= 8) return;
        dialedNumber += digit.ToString();
        statusMessage = "READY TO DIAL";
        PlayDigitBeep(digit);
    }

    private void PlayDigitBeep(int digit)
    {
        if (audioSource != null && dtmfClips != null && digit >= 0 && digit < dtmfClips.Length)
        {
            audioSource.PlayOneShot(dtmfClips[digit], 0.75f);
        }
    }

    private void AttemptDial()
    {
        if (string.Equals(dialedNumber.Trim(), "911", System.StringComparison.Ordinal))
        {
            hasCompleted911Dial = true;
            CloseDialpadUI();
            if (NightShiftStoryController.Instance != null)
            {
                NightShiftStoryController.Instance.NotifyAct4Dialed911(this);
            }
        }
        else
        {
            statusMessage = "CALL FAILED - DIAL 911";
            dialedNumber = "";
            PlayDigitBeep(0);
        }
    }

    private void InitStyles()
    {
        if (pixelTex == null)
        {
            pixelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixelTex.SetPixel(0, 0, Color.white);
            pixelTex.Apply();
        }
        if (uiFont == null)
        {
            uiFont = HorrorHudStyle.GetRetroFont();
        }
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = new Color(0.72f, 0.84f, 0.96f, 1f);

            displayStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            displayStyle.normal.textColor = new Color(0.95f, 0.98f, 1.0f, 1f);

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            statusStyle.normal.textColor = new Color(0.96f, 0.78f, 0.42f, 0.95f);

            keyButtonStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            keyButtonStyle.normal.textColor = Color.white;

            dialButtonStyle = new GUIStyle(GUI.skin.label)
            {
                font = uiFont,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            dialButtonStyle.normal.textColor = Color.white;
        }
    }

    private void OnGUI()
    {
        if (!IsDialpadOpen) return;
        InitStyles();

        float sw = Screen.width;
        float sh = Screen.height;

        // Subtle dim backdrop
        DrawRect(new Rect(0f, 0f, sw, sh), new Color(0.01f, 0.01f, 0.02f, 0.65f));

        // Phone Chassis Frame
        float pw = 310f;
        float ph = 490f;
        Rect phoneRect = new Rect((sw - pw) * 0.5f, (sh - ph) * 0.5f, pw, ph);
        DrawRect(phoneRect, new Color(0.08f, 0.10f, 0.14f, 0.98f));
        DrawBorder(phoneRect, new Color(0.38f, 0.46f, 0.56f, 1f), 2.5f);

        // Top Earpiece & Status Header
        GUI.Label(new Rect(phoneRect.x, phoneRect.y + 12f, pw, 22f), "EMERGENCY CALL  |  02:18 AM", headerStyle);
        GUI.Label(new Rect(phoneRect.x, phoneRect.y + 34f, pw, 18f), statusMessage, statusStyle);

        // Number Display Screen
        Rect dispRect = new Rect(phoneRect.x + 22f, phoneRect.y + 60f, pw - 44f, 64f);
        DrawRect(dispRect, new Color(0.04f, 0.06f, 0.09f, 1f));
        DrawBorder(dispRect, new Color(0.24f, 0.36f, 0.48f, 1f), 1.5f);
        string shownText = string.IsNullOrEmpty(dialedNumber) ? "---" : dialedNumber;
        GUI.Label(dispRect, shownText, displayStyle);

        // 0-9 Clickable Keypad Grid
        float startX = phoneRect.x + 26f;
        float startY = phoneRect.y + 140f;
        float btnW = 78f;
        float btnH = 56f;
        float gapX = 12f;
        float gapY = 10f;

        string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "CLR", "0", "DEL" };
        for (int i = 0; i < 12; i++)
        {
            int row = i / 3;
            int col = i % 3;
            Rect bRect = new Rect(startX + col * (btnW + gapX), startY + row * (btnH + gapY), btnW, btnH);
            bool hover = bRect.Contains(Event.current.mousePosition);

            Color bg = hover ? new Color(0.24f, 0.32f, 0.44f, 1f) : new Color(0.14f, 0.18f, 0.25f, 1f);
            DrawRect(bRect, bg);
            DrawBorder(bRect, new Color(0.42f, 0.52f, 0.64f, 0.9f), 1.5f);
            GUI.Label(bRect, labels[i], keyButtonStyle);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover)
            {
                if (labels[i] == "CLR")
                {
                    dialedNumber = "";
                    PlayDigitBeep(0);
                }
                else if (labels[i] == "DEL")
                {
                    if (dialedNumber.Length > 0) dialedNumber = dialedNumber.Substring(0, dialedNumber.Length - 1);
                    PlayDigitBeep(0);
                }
                else if (int.TryParse(labels[i], out int d))
                {
                    AppendDigit(d);
                }
                Event.current.Use();
            }
        }

        // Green [ DIAL ] Button at bottom
        Rect dialRect = new Rect(phoneRect.x + 26f, startY + 4 * (btnH + gapY) + 6f, pw - 52f, 54f);
        bool dialHover = dialRect.Contains(Event.current.mousePosition);
        Color dialBg = dialHover ? new Color(0.18f, 0.76f, 0.32f, 1f) : new Color(0.12f, 0.58f, 0.24f, 1f);
        DrawRect(dialRect, dialBg);
        DrawBorder(dialRect, new Color(0.65f, 0.98f, 0.72f, 1f), 2f);
        GUI.Label(dialRect, "DIAL", dialButtonStyle);

        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && dialHover)
        {
            AttemptDial();
            Event.current.Use();
        }
    }

    private void DrawRect(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, pixelTex);
        GUI.color = Color.white;
    }

    private void DrawBorder(Rect r, Color c, float t)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), pixelTex);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), pixelTex);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), pixelTex);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), pixelTex);
        GUI.color = Color.white;
    }
}

// =============================================================================
// 6. ACT 4 STOCKROOM CLIMAX: BLOODY FOOTSTEPS, DOOR SLAM, FLICKER MONSTER CREEP & EXIT JUMPSCARE
// =============================================================================
public class Act4StockroomHorrorSequence : MonoBehaviour
{
    public static Act4StockroomHorrorSequence Instance { get; private set; }

    private Transform storeRootTransform;
    private InteractableRestroomDoor stockroomDoor;
    private GameObject monsterObj;
    private Renderer[] monsterRenderers;
    private Transform pelvisBone, neckBone, headBone, leftArmBone, rightArmBone;
    private Quaternion pelvisBaseRot, neckBaseRot, headBaseRot, leftArmBaseRot, rightArmBaseRot;
    private Vector3 pelvisBasePos;
    private float targetMonsterHeight = 1.78f;

    private Light stockroomOverheadLight;
    private Light jumpscareKeyLight;

    private AudioSource sfxSource;
    private AudioSource footstepAudioSource;
    private AudioClip corruptedGlitchClip;
    private AudioClip bloodyStepClip;
    private AudioClip heartbeatClip;
    private AudioClip electricalFlickerClip;
    private AudioClip jumpscareScreamClip;

    private Texture2D leftFootTex;
    private Texture2D rightFootTex;
    private Material leftFootMat;
    private Material rightFootMat;
    private readonly List<GameObject> spawnedFootsteps = new List<GameObject>();

    public InteractableRestroomDoor StockroomDoor => stockroomDoor;

    public static Act4StockroomHorrorSequence EnsureExists()
    {
        if (Instance != null) return Instance;

        Act4StockroomHorrorSequence existing = Object.FindAnyObjectByType<Act4StockroomHorrorSequence>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("Act4_StockroomHorror_Controller");
        Instance = go.AddComponent<Act4StockroomHorrorSequence>();
        return Instance;
    }

    private void Awake()
    {
        Instance = this;
        ResolveSceneReferences();
        GenerateAudioClips();
        BuildFootstepTexturesAndMaterials();
        EnsureMonsterPreparedAndHidden();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ResolveSceneReferences()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "Convenience_Store_V2", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(nm, "StockRoom", System.StringComparison.OrdinalIgnoreCase))
            {
                if (storeRootTransform == null) storeRootTransform = t;
            }
        }

        InteractableRestroomDoor[] doors = Object.FindObjectsByType<InteractableRestroomDoor>(FindObjectsInactive.Include);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null && doors[i].IsStockroomDoor)
            {
                stockroomDoor = doors[i];
                break;
            }
        }
    }

    private void GenerateAudioClips()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.spatialBlend = 0.08f;
        sfxSource.playOnAwake = false;

        GameObject stepGo = new GameObject("BloodyFootstep_3DAudio");
        stepGo.transform.SetParent(transform, false);
        footstepAudioSource = stepGo.AddComponent<AudioSource>();
        footstepAudioSource.spatialBlend = 0.85f;
        footstepAudioSource.minDistance = 1.2f;
        footstepAudioSource.maxDistance = 16.0f;
        footstepAudioSource.rolloffMode = AudioRolloffMode.Linear;
        footstepAudioSource.playOnAwake = false;

        int sr = 44100;

        // 1. Corrupted / glitching digital horror task SFX
        int glitchSamples = Mathf.RoundToInt(0.92f * sr);
        float[] glitchBuf = new float[glitchSamples];
        float holdVal = 0f;
        int holdCnt = 0;
        for (int i = 0; i < glitchSamples; i++)
        {
            float t = (float)i / sr;
            if (holdCnt <= 0)
            {
                holdVal = Random.value * 2f - 1f;
                holdCnt = Random.Range(4, 36);
            }
            holdCnt--;
            float gate = Mathf.Sin(t * 74f) > -0.15f ? 1.0f : 0.08f;
            float sub = Mathf.Sin(2f * Mathf.PI * (96f - t * 42f) * t) * 0.48f;
            float bitTear = Mathf.Sin(2f * Mathf.PI * (880f + Mathf.Sin(t * 140f) * 620f) * t) * 0.34f;
            float env = Mathf.Clamp01(1f - t / 0.92f) * gate;
            glitchBuf[i] = Mathf.Clamp((holdVal * 0.55f + sub + bitTear) * env, -1f, 1f);
        }
        corruptedGlitchClip = AudioClip.Create("Act4_CorruptedGlitch", glitchSamples, 1, sr, false);
        corruptedGlitchClip.SetData(glitchBuf, 0);

        // 2. Wet bloody footstep squelch + heel thud
        int stepSamples = Mathf.RoundToInt(0.24f * sr);
        float[] stepBuf = new float[stepSamples];
        for (int i = 0; i < stepSamples; i++)
        {
            float t = (float)i / sr;
            float thud = Mathf.Sin(2f * Mathf.PI * (105f - t * 240f) * t) * Mathf.Exp(-t * 28f) * 0.68f;
            float squelch = (Random.value * 2f - 1f) * Mathf.Sin(2f * Mathf.PI * (640f + Mathf.Sin(t * 90f) * 280f) * t) * Mathf.Exp(-t * 18f) * 0.45f;
            stepBuf[i] = Mathf.Clamp(thud + squelch, -1f, 1f);
        }
        bloodyStepClip = AudioClip.Create("Act4_BloodyFootstep", stepSamples, 1, sr, false);
        bloodyStepClip.SetData(stepBuf, 0);

        // 3. Tense double heartbeat thump
        int hbSamples = Mathf.RoundToInt(0.55f * sr);
        float[] hbBuf = new float[hbSamples];
        for (int i = 0; i < hbSamples; i++)
        {
            float t = (float)i / sr;
            float b1 = (t < 0.18f) ? Mathf.Sin(2f * Mathf.PI * 58f * t) * Mathf.Exp(-t * 24f) * 0.85f : 0f;
            float dt2 = t - 0.22f;
            float b2 = (dt2 >= 0f && dt2 < 0.20f) ? Mathf.Sin(2f * Mathf.PI * 48f * dt2) * Mathf.Exp(-dt2 * 28f) * 0.65f : 0f;
            hbBuf[i] = Mathf.Clamp(b1 + b2, -1f, 1f);
        }
        heartbeatClip = AudioClip.Create("Act4_Heartbeat", hbSamples, 1, sr, false);
        heartbeatClip.SetData(hbBuf, 0);

        // 4. Electrical flicker buzz
        int flSamples = Mathf.RoundToInt(0.16f * sr);
        float[] flBuf = new float[flSamples];
        for (int i = 0; i < flSamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 20f);
            float buzz = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.45f + (Random.value * 2f - 1f) * 0.40f;
            flBuf[i] = Mathf.Clamp(buzz * env * 0.65f, -1f, 1f);
        }
        electricalFlickerClip = AudioClip.Create("Act4_FlickerBuzz", flSamples, 1, sr, false);
        electricalFlickerClip.SetData(flBuf, 0);

        // 5. High-impact horror jumpscare scream + sub-bass slam
        jumpscareScreamClip = BuildDramaticJumpscareScreamClip(sr);
    }

    public static AudioClip BuildDramaticJumpscareScreamClip(int sr = 44100)
    {
        int len = Mathf.RoundToInt(1.05f * sr);
        float[] buf = new float[len];
        float hold = 0f;
        int holdCounter = 0;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sr;
            if (holdCounter <= 0)
            {
                hold = Random.value * 2f - 1f;
                holdCounter = Random.Range(2, 10);
            }
            holdCounter--;

            // Immediate violent attack (0..0.02s), sustained terrifying shriek, sharp cut at end
            float env = t < 0.015f ? (t / 0.015f) : Mathf.Clamp01(1.0f - Mathf.Pow(t / 1.05f, 2.2f));
            float subSlam = Mathf.Sin(2f * Mathf.PI * (82f - t * 38f) * t) * Mathf.Exp(-t * 4.5f) * 0.85f;
            float shriek1 = Mathf.Sin(2f * Mathf.PI * (740f + Mathf.Sin(t * 65f) * 180f) * t) * 0.52f;
            float shriek2 = Mathf.Sin(2f * Mathf.PI * (1420f - t * 290f + Mathf.Sin(t * 110f) * 240f) * t) * 0.48f;
            float dissonant = Mathf.Sin(2f * Mathf.PI * 2180f * t) * 0.30f;
            float roar = hold * 0.62f;

            // Soft overdrive saturation for maximum punch
            float raw = (subSlam + shriek1 + shriek2 + dissonant + roar) * env * 1.35f;
            buf[i] = (float)System.Math.Tanh(raw);
        }
        AudioClip clip = AudioClip.Create("Horror_JumpscareScream", len, 1, sr, false);
        clip.SetData(buf, 0);
        return clip;
    }

    public void PlayCorruptedTaskGlitchSfx()
    {
        if (sfxSource != null && corruptedGlitchClip != null)
        {
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(corruptedGlitchClip, 0.95f);
        }
    }

    private void BuildFootstepTexturesAndMaterials()
    {
        leftFootTex = GenerateBloodyFootprintTexture(isRightFoot: false);
        rightFootTex = GenerateBloodyFootprintTexture(isRightFoot: true);

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        leftFootMat = CreateBloodFootprintMaterial(sh, isRightFoot: false, "Act4_BloodyFoot_Left_MAT");
        rightFootMat = CreateBloodFootprintMaterial(sh, isRightFoot: true, "Act4_BloodyFoot_Right_MAT");
    }

    private static Material CreateBloodFootprintMaterial(Shader sh, bool isRightFoot, string name)
    {
        GenerateBloodyFootprintTextures(isRightFoot, out Texture2D albedoTex, out Texture2D normalTex, out Texture2D glossTex);

        Material m = new Material(sh);
        m.name = name;
        m.mainTexture = albedoTex;
        m.SetColor("_BaseColor", Color.white);
        m.SetColor("_Color", Color.white);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", albedoTex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", albedoTex);
        if (m.HasProperty("_BumpMap"))
        {
            m.SetTexture("_BumpMap", normalTex);
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1.35f);
            m.EnableKeyword("_NORMALMAP");
        }
        if (m.HasProperty("_MetallicGlossMap"))
        {
            m.SetTexture("_MetallicGlossMap", glossTex);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);

        // Mask wet specular reflections strictly to the bloody footprint pixels (0 outside on transparent floor)
        if (m.HasProperty("_SmoothnessTextureChannel")) m.SetFloat("_SmoothnessTextureChannel", 0f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1.0f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.0f);
        if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1.0f);
        if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 1.0f);
        if (m.HasProperty("_BlendModePreserveSpecular")) m.SetFloat("_BlendModePreserveSpecular", 0f);
        if (m.HasProperty("_ReceiveShadows")) m.SetFloat("_ReceiveShadows", 0f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.renderQueue = 3010;
        m.SetOverrideTag("RenderType", "Transparent");
        return m;
    }

    private static Texture2D GenerateBloodyFootprintTexture(bool isRightFoot)
    {
        GenerateBloodyFootprintTextures(isRightFoot, out Texture2D albedo, out _, out _);
        return albedo;
    }

    private static void GenerateBloodyFootprintTextures(
        bool isRightFoot,
        out Texture2D albedoTex,
        out Texture2D normalTex,
        out Texture2D glossTex)
    {
        int w = 256;
        int h = 512;
        albedoTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        normalTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        glossTex = new Texture2D(w, h, TextureFormat.RGBA32, false);

        albedoTex.filterMode = FilterMode.Bilinear;
        albedoTex.wrapMode = TextureWrapMode.Clamp;
        normalTex.filterMode = FilterMode.Bilinear;
        normalTex.wrapMode = TextureWrapMode.Clamp;
        glossTex.filterMode = FilterMode.Bilinear;
        glossTex.wrapMode = TextureWrapMode.Clamp;

        float[,] heightField = new float[w, h];
        float seed = isRightFoot ? 41.7f : 12.3f;

        Color clottedDarkBlood = new Color(0.11f, 0.005f, 0.005f, 0.98f);
        Color venousCrimson = new Color(0.26f, 0.012f, 0.012f, 0.96f);
        Color freshArterial = new Color(0.44f, 0.022f, 0.018f, 0.94f);
        Color pooledEdgeRim = new Color(0.18f, 0.008f, 0.008f, 0.96f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                if (isRightFoot) u = 1f - u;
                float v = y / (float)(h - 1);

                // Multi-octave organic blood texture noise
                float n1 = Mathf.PerlinNoise(u * 9.5f + seed, v * 16.5f + seed);
                float n2 = Mathf.PerlinNoise(u * 24f + seed * 1.4f, v * 38f + seed * 0.8f) * 0.5f;
                float n3 = Mathf.PerlinNoise(u * 55f + seed * 2.1f, v * 80f + seed * 1.7f) * 0.25f;
                float fbm = (n1 + n2 + n3) / 1.75f;

                float soleMask = 0f;
                float isTreadGroove = 0f;

                // 1. Anatomical Shoe / Boot Heel Block (v = 0.07 .. 0.31) with curved back & straight front heel edge
                if (v >= 0.06f && v <= 0.32f)
                {
                    float heelCenterU = 0.49f;
                    float heelHalfW = Mathf.Lerp(0.16f, 0.195f, Mathf.Sin(Mathf.Clamp01((v - 0.07f) / 0.24f) * Mathf.PI));
                    float dx = (u - heelCenterU) / Mathf.Max(0.01f, heelHalfW);
                    float dy = (v < 0.18f) ? ((v - 0.18f) / 0.105f) : ((v - 0.18f) / 0.135f);
                    // Rounded back heel, squared-off front heel breast at v ~ 0.305
                    float pDist = (v > 0.22f)
                        ? Mathf.Max(Mathf.Abs(dx), Mathf.Abs((v - 0.19f) / 0.115f))
                        : Mathf.Sqrt(dx * dx + dy * dy);
                    float warpDist = pDist + (fbm - 0.5f) * 0.14f;
                    if (warpDist <= 1.0f)
                    {
                        soleMask = Mathf.Max(soleMask, Mathf.Clamp01((1.0f - warpDist) * 7.5f));
                        // Heel lug tread ridges & peripheral heel border channel
                        float lugWave = Mathf.Sin(v * 215f);
                        if (lugWave > 0.52f && pDist < 0.84f)
                        {
                            isTreadGroove = Mathf.Max(isTreadGroove, (lugWave - 0.52f) * 1.45f);
                        }
                    }
                }

                // 2. Arched Lateral Midfoot Waist (v = 0.29 .. 0.47, curves along outer lateral edge u ~ 0.40)
                if (v >= 0.28f && v <= 0.48f)
                {
                    float tArch = (v - 0.28f) / 0.20f;
                    float archCenterU = Mathf.Lerp(0.46f, 0.41f, Mathf.Sin(tArch * Mathf.PI));
                    float archHalfW = Mathf.Lerp(0.15f, 0.105f, Mathf.Sin(tArch * Mathf.PI));
                    float dx = (u - archCenterU) / archHalfW;
                    float dy = (v - 0.38f) / 0.105f;
                    // Medial hollow arch carve-out on inner foot side (u > 0.52)
                    float dist = Mathf.Sqrt(dx * dx * 0.92f + dy * dy * 0.55f) + (fbm - 0.5f) * 0.18f;
                    if (dist <= 1.0f)
                    {
                        soleMask = Mathf.Max(soleMask, Mathf.Clamp01((1.0f - dist) * 5.8f) * 0.92f);
                    }
                }

                // 3. Forefoot Ball Pad & Rounded Asymmetric Toe Box (v = 0.44 .. 0.91)
                if (v >= 0.43f && v <= 0.93f)
                {
                    float tFore = Mathf.Clamp01((v - 0.44f) / 0.47f);
                    // Asymmetric anatomical foot centerline (curves slightly toward medial big-toe side u ~ 0.54 at tip)
                    float centerU = Mathf.Lerp(0.47f, 0.53f, Mathf.Pow(tFore, 1.25f));
                    // Width widens across the ball of the foot (tFore ~ 0.35) then tapers smoothly round at the toe cap
                    float widthProfile = Mathf.Sin(Mathf.Pow(tFore, 0.72f) * Mathf.PI);
                    float foreHalfW = 0.08f + widthProfile * 0.175f;
                    float dx = (u - centerU) / Mathf.Max(0.01f, foreHalfW);
                    float dy = (v - 0.675f) / 0.235f;
                    float radial = Mathf.Sqrt(dx * dx + dy * dy) + (fbm - 0.5f) * 0.13f;

                    if (radial <= 1.0f)
                    {
                        float foreAlpha = Mathf.Clamp01((1.0f - radial) * 8.0f);
                        soleMask = Mathf.Max(soleMask, foreAlpha);

                        // Realistic boot chevron/diamond tread lugs pressed into wet blood
                        if (radial < 0.86f)
                        {
                            float chev = Mathf.Sin((v * 240f) - Mathf.Abs(u - centerU) * 140f);
                            float crossSlot = Mathf.Sin((u - centerU) * 190f);
                            if (chev > 0.50f || (crossSlot > 0.68f && Mathf.Abs(u - centerU) < 0.12f))
                            {
                                float grooveDepth = Mathf.Max(
                                    Mathf.Clamp01((chev - 0.50f) * 1.6f),
                                    Mathf.Clamp01((crossSlot - 0.68f) * 1.8f));
                                isTreadGroove = Mathf.Max(isTreadGroove, grooveDepth);
                            }
                        }
                    }
                }

                // 4. Organic blood squeeze/pooling rim & pressure variation across the sole
                float alpha = soleMask;
                if (alpha > 0.01f)
                {
                    // Inside tread grooves, partial blood smear remains in some grooves while others expose floor gaps
                    if (isTreadGroove > 0.05f)
                    {
                        float grooveBloodBridge = Mathf.SmoothStep(0.32f, 0.78f, fbm);
                        alpha *= Mathf.Lerp(1.0f, grooveBloodBridge * 0.45f, isTreadGroove);
                    }

                    // Realistic pressure breakup so edge & internal air pockets look like genuine viscous liquid print
                    float pressureBreak = Mathf.SmoothStep(0.16f, 0.42f, fbm + alpha * 0.32f);
                    alpha *= pressureBreak;
                }

                // 5. Viscous forward toe-off drag streaks & micro-splatter droplets around heel and forefoot
                if (alpha < 0.20f)
                {
                    // Forward drag smears off the toe & ball
                    if (v > 0.82f && v < 0.97f && Mathf.Abs(u - 0.52f) < 0.16f)
                    {
                        float streak = Mathf.PerlinNoise(u * 48f + seed, v * 12f + seed);
                        float fadeV = 1f - Mathf.Clamp01((v - 0.82f) / 0.15f);
                        if (streak > 0.68f)
                        {
                            alpha = Mathf.Max(alpha, (streak - 0.68f) * 3.0f * fadeV);
                        }
                    }

                    // Radial impact micro-splatter droplets around heel & ball
                    float dropField = Mathf.PerlinNoise(u * 42f + seed * 1.9f, v * 78f + seed * 2.3f);
                    float dropCluster = Mathf.PerlinNoise(u * 11f + seed, v * 19f + seed);
                    float distFromAxis = Mathf.Abs(u - 0.49f);
                    if (dropField > 0.79f && dropCluster > 0.48f && distFromAxis < 0.42f && v > 0.04f && v < 0.96f)
                    {
                        float dropA = Mathf.Clamp01((dropField - 0.79f) * 8.5f);
                        alpha = Mathf.Max(alpha, dropA * 0.92f);
                    }
                }

                float hVal = Mathf.Clamp01(alpha * (0.75f + (1f - isTreadGroove) * 0.25f));
                heightField[x, y] = hVal;

                if (alpha > 0.015f)
                {
                    // Viscous arterial blood shading: dark coagulated core + squeezed meniscus edge rim + wet crimson variation
                    float edgeFactor = Mathf.Clamp01(1.0f - Mathf.Abs(soleMask - 0.55f) * 2.2f);
                    Color c = Color.Lerp(clottedDarkBlood, venousCrimson, Mathf.Clamp01(fbm * 1.25f));
                    c = Color.Lerp(c, freshArterial, Mathf.Clamp01((fbm - 0.45f) * 1.8f));
                    c = Color.Lerp(c, pooledEdgeRim, edgeFactor * 0.55f);
                    c.a = Mathf.Clamp01(alpha * 0.96f);
                    albedoTex.SetPixel(x, y, c);

                    float smoothness = Mathf.Clamp01(alpha * 0.94f);
                    glossTex.SetPixel(x, y, new Color(0f, 0f, 0f, smoothness));
                }
                else
                {
                    albedoTex.SetPixel(x, y, Color.clear);
                    glossTex.SetPixel(x, y, Color.clear);
                }
            }
        }

        // Compute 3D liquid meniscus normal map so overhead lights glint off the raised bloody tread ridges
        float bumpScale = 3.4f;
        for (int y = 0; y < h; y++)
        {
            int y0 = Mathf.Max(0, y - 1);
            int y1 = Mathf.Min(h - 1, y + 1);
            for (int x = 0; x < w; x++)
            {
                int x0 = Mathf.Max(0, x - 1);
                int x1 = Mathf.Min(w - 1, x + 1);
                float dhdx = (heightField[x1, y] - heightField[x0, y]) * bumpScale;
                float dhdy = (heightField[x, y1] - heightField[x, y0]) * bumpScale;
                Vector3 n = new Vector3(-dhdx, -dhdy, 1.0f).normalized;
                normalTex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
            }
        }

        albedoTex.Apply();
        normalTex.Apply();
        glossTex.Apply();
    }

    public IEnumerator SpawnWalkingBloodyFootstepsRoutine()
    {
        ResolveSceneReferences();

        for (int i = 0; i < spawnedFootsteps.Count; i++)
        {
            if (spawnedFootsteps[i] != null) Destroy(spawnedFootsteps[i]);
        }
        spawnedFootsteps.Clear();

        // Trail starting right in front of the cashier counter, turning North up the East Aisle (X = 3.35),
        // passing right through the Stockroom doorway (Z = 2.13) and into the Stockroom (Z = 3.45)
        Vector3[] pathCenters = new Vector3[]
        {
            new Vector3(2.65f, 0.173f, -5.65f),
            new Vector3(3.05f, 0.173f, -5.42f),
            new Vector3(3.35f, 0.173f, -4.95f),
            new Vector3(3.35f, 0.173f, -4.35f),
            new Vector3(3.35f, 0.173f, -3.75f),
            new Vector3(3.35f, 0.173f, -3.15f),
            new Vector3(3.35f, 0.173f, -2.55f),
            new Vector3(3.35f, 0.173f, -1.95f),
            new Vector3(3.35f, 0.173f, -1.35f),
            new Vector3(3.35f, 0.173f, -0.75f),
            new Vector3(3.35f, 0.173f, -0.15f),
            new Vector3(3.35f, 0.173f,  0.45f),
            new Vector3(3.35f, 0.173f,  1.05f),
            new Vector3(3.34f, 0.173f,  1.65f),
            new Vector3(3.34f, 0.173f,  2.25f),
            new Vector3(3.34f, 0.173f,  2.85f),
            new Vector3(3.34f, 0.173f,  3.42f)
        };

        for (int i = 0; i < pathCenters.Length; i++)
        {
            bool isRight = (i % 2 == 1);
            Vector3 fwd = (i + 1 < pathCenters.Length)
                ? (pathCenters[i + 1] - pathCenters[i]).normalized
                : (pathCenters[i] - pathCenters[i - 1]).normalized;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            Vector3 footPos = pathCenters[i] + right * (isRight ? 0.11f : -0.11f);
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg + (isRight ? 5f : -5f);

            GameObject stepQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            stepQuad.name = $"Act4_BloodyFootstep_{i:D2}_{(isRight ? "R" : "L")}";
            Destroy(stepQuad.GetComponent<Collider>());
            stepQuad.transform.position = footPos;
            stepQuad.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            stepQuad.transform.localScale = new Vector3(0.18f, 0.32f, 1f);

            Renderer r = stepQuad.GetComponent<Renderer>();
            r.sharedMaterial = isRight ? rightFootMat : leftFootMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spawnedFootsteps.Add(stepQuad);

            if (footstepAudioSource != null && bloodyStepClip != null)
            {
                footstepAudioSource.transform.position = footPos;
                footstepAudioSource.pitch = Random.Range(0.92f, 1.06f);
                footstepAudioSource.PlayOneShot(bloodyStepClip, 0.72f);
            }

            yield return new WaitForSeconds(0.32f);
        }
    }

    public bool IsPlayerInsideStockroom()
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        Vector3 p = cam.transform.position;
        if (storeRootTransform != null)
        {
            Vector3 lp = storeRootTransform.InverseTransformPoint(p);
            // Stockroom interior is localX > -3.85 (north of partition wall at -4.39) and localZ in [-1.85 .. 0.45]
            return lp.x > -3.82f && lp.x < -0.80f && lp.z > -1.85f && lp.z < 0.45f;
        }

        return p.z > 2.68f && p.x >= 2.15f && p.x <= 4.55f;
    }

    public bool HasPlayerFullyExitedStockroom()
    {
        if (stockroomDoor != null)
        {
            if (!stockroomDoor.isOpen) return false;
            Quaternion openTargetRot = Quaternion.Euler(0f, stockroomDoor.openAngleY, 0f);
            if (Quaternion.Angle(stockroomDoor.transform.localRotation, openTargetRot) > 12f)
            {
                return false;
            }
        }

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        Vector3 p = cam.transform.position;
        if (storeRootTransform != null)
        {
            Vector3 lp = storeRootTransform.InverseTransformPoint(p);
            // Stockroom doorway wall is at localX = -4.39; stepping fully outside into the store aisle is lp.x < -4.55f
            return lp.x < -4.55f;
        }

        return p.z < 1.98f;
    }

    private void EnsureMonsterPreparedAndHidden()
    {
        if (monsterObj == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Act3_Monster_LowQual");
            if (prefab != null)
            {
                monsterObj = Instantiate(prefab);
                monsterObj.name = "Act4_Stockroom_MonsterNPC";
            }
        }

        if (monsterObj == null) return;
        monsterObj.SetActive(true);

        // Remove any Act3MonsterStandingAnimator so our Stockroom seizure & slow walk animator has 100% control
        Act3MonsterStandingAnimator idleAnim = monsterObj.GetComponent<Act3MonsterStandingAnimator>();
        if (idleAnim != null) Destroy(idleAnim);

        Animator unityAnim = monsterObj.GetComponentInChildren<Animator>(true);
        if (unityAnim != null) unityAnim.enabled = false;

        monsterRenderers = monsterObj.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < monsterRenderers.Length; i++)
        {
            Renderer r = monsterRenderers[i];
            if (r == null) continue;
            r.enabled = true;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;

            Material[] mats = r.materials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null) continue;
                mat.SetFloat("_BlendModePreserveSpecular", 0f);
                mat.SetFloat("_SpecularHighlights", 0f);
                mat.SetFloat("_EnvironmentReflections", 0f);
                mat.SetFloat("_Smoothness", 0.08f);
                mat.SetFloat("_Metallic", 0f);
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                mat.DisableKeyword("_EMISSION");

                Color tint = new Color(0.62f, 0.66f, 0.72f, 1f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
                if (mat.HasProperty("_SpecColor")) mat.SetColor("_SpecColor", Color.black);
            }
        }

        // Normalize height to 1.78m
        Bounds b = new Bounds(monsterObj.transform.position, Vector3.zero);
        bool hasB = false;
        for (int i = 0; i < monsterRenderers.Length; i++)
        {
            if (monsterRenderers[i] == null) continue;
            if (!hasB) { b = monsterRenderers[i].bounds; hasB = true; }
            else b.Encapsulate(monsterRenderers[i].bounds);
        }
        if (hasB && b.size.y > 0.05f)
        {
            float scaleRatio = targetMonsterHeight / b.size.y;
            if (Mathf.Abs(scaleRatio - 1f) > 0.02f)
            {
                monsterObj.transform.localScale *= scaleRatio;
            }
        }

        // Cache psx_horror_creature armature bones
        Transform[] children = monsterObj.GetComponentsInChildren<Transform>(true);
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

        SetMonsterVisible(false);
    }

    public void SetMonsterVisible(bool visible)
    {
        if (monsterRenderers == null && monsterObj != null)
        {
            monsterRenderers = monsterObj.GetComponentsInChildren<Renderer>(true);
        }
        if (monsterRenderers == null) return;
        for (int i = 0; i < monsterRenderers.Length; i++)
        {
            if (monsterRenderers[i] != null) monsterRenderers[i].enabled = visible;
        }
    }

    private void PlaceMonsterOnFloorFacingPoint(Vector3 floorSpot, Vector3 faceTargetWorld, float extraVerticalOffset = 0f)
    {
        if (monsterObj == null) return;

        Vector3 lookDir = faceTargetWorld - floorSpot;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.0001f)
        {
            // psx_horror_creature faces local -X, so +90 deg Y rotation points its face toward lookDir
            monsterObj.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
        }

        monsterObj.transform.position = floorSpot;
        if (monsterRenderers != null && monsterRenderers.Length > 0)
        {
            bool wasEnabled = monsterRenderers[0] != null && monsterRenderers[0].enabled;
            if (!wasEnabled) SetMonsterVisible(true);

            Bounds b = monsterRenderers[0].bounds;
            for (int i = 1; i < monsterRenderers.Length; i++)
            {
                if (monsterRenderers[i] != null) b.Encapsulate(monsterRenderers[i].bounds);
            }
            float deltaY = (floorSpot.y - b.min.y) + extraVerticalOffset;
            monsterObj.transform.position = new Vector3(floorSpot.x, monsterObj.transform.position.y + deltaY, floorSpot.z);

            if (!wasEnabled) SetMonsterVisible(false);
        }
    }

    private Vector3 GetMonsterEyeWorldPosition()
    {
        if (headBone != null) return headBone.position + Vector3.up * 0.06f;
        if (neckBone != null) return neckBone.position + Vector3.up * 0.14f;
        if (monsterObj != null) return monsterObj.transform.position + Vector3.up * 1.56f;
        return Vector3.zero;
    }

    public IEnumerator PlayStockroomDoorShutAndMonsterCreepRoutine(NightShiftStoryController storyCtrl)
    {
        ResolveSceneReferences();
        EnsureMonsterPreparedAndHidden();

        SimpleFPSController fps = Object.FindAnyObjectByType<SimpleFPSController>();
        Camera cam = fps != null && fps.playerCamera != null ? fps.playerCamera : Camera.main;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();

        SimpleFPSController.IsExternalCameraAnimationActive = true;

        // Hide held phone in hand so view is unobstructed
        if (Act4CounterPhoneAndDialpad.Instance != null)
        {
            Renderer[] phoneRends = Act4CounterPhoneAndDialpad.Instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < phoneRends.Length; i++) if (phoneRends[i] != null) phoneRends[i].enabled = false;
            Light[] phoneLights = Act4CounterPhoneAndDialpad.Instance.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < phoneLights.Length; i++) if (phoneLights[i] != null) phoneLights[i].enabled = false;
        }

        // 1. Slam the Stockroom door shut behind the player & play "Doors shuts from behind"
        if (stockroomDoor != null)
        {
            stockroomDoor.SlamShutAndLockForStockroomSequence();
        }

        Coroutine doorShutDialogue = null;
        if (storyCtrl != null)
        {
            doorShutDialogue = storyCtrl.StartCoroutine(storyCtrl.PlayCustomDialogueRoutine("Doors shuts from behind"));
        }

        Vector3 doorLookPoint = new Vector3(3.34f, 1.35f, 2.13f);
        Vector3 stockroomBackFloor = new Vector3(3.34f, 0.17f, 5.15f);
        if (storeRootTransform != null)
        {
            doorLookPoint = storeRootTransform.TransformPoint(new Vector3(-4.44f, 1.20f, -0.65f));
            stockroomBackFloor = storeRootTransform.TransformPoint(new Vector3(-1.35f, 0.03f, -0.65f));
        }

        // 2. Slowly animate the player looking behind at the closed Stockroom door (1.65s)
        if (cam != null)
        {
            Quaternion startRot = cam.transform.rotation;
            Vector3 toDoor = (doorLookPoint - cam.transform.position).normalized;
            Quaternion lookDoorRot = toDoor.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toDoor, Vector3.up)
                : startRot;

            float d1 = 1.65f;
            float elapsed = 0f;
            while (elapsed < d1)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, elapsed / d1);
                cam.transform.rotation = Quaternion.Slerp(startRot, lookDoorRot, u);
                yield return null;
            }
            cam.transform.rotation = lookDoorRot;
        }

        if (doorShutDialogue != null) yield return doorShutDialogue;
        yield return new WaitForSeconds(0.45f);

        // 3. Slowly animate the player looking back around into the Stockroom (1.65s)
        Vector3 intoRoomLookPoint = new Vector3(stockroomBackFloor.x, cam != null ? cam.transform.position.y : 1.70f, stockroomBackFloor.z);
        if (cam != null)
        {
            Quaternion fromDoorRot = cam.transform.rotation;
            Vector3 toRoom = (intoRoomLookPoint - cam.transform.position).normalized;
            Quaternion lookRoomRot = toRoom.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toRoom, Vector3.up)
                : fromDoorRot;

            float d2 = 1.65f;
            float elapsed = 0f;
            while (elapsed < d2)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, elapsed / d2);
                cam.transform.rotation = Quaternion.Slerp(fromDoorRot, lookRoomRot, u);
                yield return null;
            }
            cam.transform.rotation = lookRoomRot;
        }

        // 4. Stockroom Light Flickering + MonsterNPC Strobe Appearance, Head Seizure, Forced Scared POV & Unnaturally Slow Creep to Eyes
        if (stockroomOverheadLight == null)
        {
            GameObject lightGo = new GameObject("Act4_Stockroom_FlickerLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.position = (cam != null ? cam.transform.position : new Vector3(3.34f, 1.75f, 3.10f)) + new Vector3(0f, 0.85f, 0.85f);
            stockroomOverheadLight = lightGo.AddComponent<Light>();
            stockroomOverheadLight.type = LightType.Point;
            stockroomOverheadLight.color = new Color(0.92f, 0.88f, 0.78f);
            stockroomOverheadLight.range = 8.5f;
            stockroomOverheadLight.intensity = 2.4f;
            stockroomOverheadLight.shadows = LightShadows.Soft;
        }

        Vector3 playerEyePos = cam != null ? cam.transform.position : new Vector3(3.34f, 1.75f, 3.00f);
        Vector3 baseCamLocalPos = cam != null ? cam.transform.localPosition : new Vector3(0f, 1.75f, 0f);
        float baseFov = cam != null ? cam.fieldOfView : 60f;

        Vector3 flatToBack = new Vector3(stockroomBackFloor.x - playerEyePos.x, 0f, stockroomBackFloor.z - playerEyePos.z);
        if (flatToBack.sqrMagnitude < 0.01f) flatToBack = Vector3.forward;
        flatToBack.Normalize();

        float floorY = playerEyePos.y - (fps != null ? fps.eyeHeight : 1.75f);
        Vector3 monsterStartFloor = new Vector3(playerEyePos.x, floorY, playerEyePos.z) + flatToBack * 2.55f;
        Vector3 monsterEndFloor = new Vector3(playerEyePos.x, floorY, playerEyePos.z) + flatToBack * 0.34f;

        float creepDuration = 6.8f;
        float timer = 0f;
        float nextFlickerTime = 0f;
        float nextSeizureStepTime = 0f;
        float nextHeartbeatTime = 0f;
        bool lightOn = true;
        Quaternion currentHeadSeizureRot = Quaternion.identity;
        Quaternion currentNeckSeizureRot = Quaternion.identity;

        while (timer < creepDuration)
        {
            timer += Time.deltaTime;
            float u = Mathf.Clamp01(timer / creepDuration);

            // Unnaturally slow walk toward the player's POV
            Vector3 curMonsterFloor = Vector3.Lerp(monsterStartFloor, monsterEndFloor, u);

            // As it gets closer, gradually lower & lean its head toward the player's head height
            float leanProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.15f) / 0.85f));
            float verticalHeadAlignOffset = Mathf.Lerp(0f, -0.06f, leanProgress);
            PlaceMonsterOnFloorFacingPoint(curMonsterFloor, playerEyePos, verticalHeadAlignOffset);

            // Stepped low-FPS (16 FPS) horror head seizure + slow creepy stride + forward head lean
            if (timer >= nextSeizureStepTime)
            {
                nextSeizureStepTime = timer + 0.062f;
                float seizureIntensity = Mathf.Lerp(1.0f, 1.55f, u);
                currentHeadSeizureRot = Quaternion.Euler(
                    Random.Range(-32f, 32f) * seizureIntensity,
                    Random.Range(-44f, 44f) * seizureIntensity,
                    Random.Range(-36f, 36f) * seizureIntensity);
                currentNeckSeizureRot = Quaternion.Euler(
                    Random.Range(-18f, 24f) * seizureIntensity,
                    Random.Range(-22f, 22f) * seizureIntensity,
                    Random.Range(-25f, 25f) * seizureIntensity);
            }

            float walkPhase = timer * 4.2f;
            float walkBob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.028f * (1f - u * 0.4f);
            if (pelvisBone != null)
            {
                pelvisBone.localPosition = pelvisBasePos + new Vector3(0f, walkBob, 0f);
                // Lean torso forward toward the player's face as it gets close
                pelvisBone.localRotation = pelvisBaseRot * Quaternion.Euler(Mathf.Sin(walkPhase) * 3.5f, 0f, -leanProgress * 18f);
            }
            if (neckBone != null)
            {
                neckBone.localRotation = neckBaseRot * Quaternion.Euler(0f, 0f, -leanProgress * 22f) * currentNeckSeizureRot;
            }
            if (headBone != null)
            {
                headBone.localRotation = headBaseRot * currentHeadSeizureRot;
            }
            if (leftArmBone != null)
            {
                leftArmBone.localRotation = leftArmBaseRot * Quaternion.Euler(Mathf.Sin(walkPhase) * 10f, 0f, -leanProgress * 15f);
            }
            if (rightArmBone != null)
            {
                rightArmBone.localRotation = rightArmBaseRot * Quaternion.Euler(-Mathf.Sin(walkPhase) * 10f, 0f, leanProgress * 15f);
            }

            // Flickering light + Monster appearing & disappearing with the light
            if (timer >= nextFlickerTime)
            {
                nextFlickerTime = timer + Random.Range(0.045f, 0.14f);
                // Keep visible slightly more often as it gets right into the player's eyes
                float onChance = u > 0.72f ? 0.68f : 0.52f;
                lightOn = Random.value < onChance;

                if (stockroomOverheadLight != null)
                {
                    stockroomOverheadLight.enabled = lightOn;
                    stockroomOverheadLight.intensity = lightOn ? Random.Range(1.4f, 3.2f) : 0f;
                }
                SetMonsterVisible(lightOn);

                if (!lightOn && sfxSource != null && electricalFlickerClip != null && Random.value < 0.55f)
                {
                    sfxSource.pitch = Random.Range(0.75f, 1.25f);
                    sfxSource.PlayOneShot(electricalFlickerClip, 0.38f);
                }
            }

            // Heartbeat & glitch static escalating as the monster's face gets inches from the camera
            if (timer >= nextHeartbeatTime)
            {
                nextHeartbeatTime = timer + Mathf.Lerp(0.68f, 0.34f, u);
                if (sfxSource != null && heartbeatClip != null)
                {
                    sfxSource.pitch = Mathf.Lerp(0.95f, 1.25f, u);
                    sfxSource.PlayOneShot(heartbeatClip, Mathf.Lerp(0.45f, 0.90f, u));
                }
            }

            // Force player camera to lock directly onto the monster's eyes with scared trembling POV
            if (cam != null)
            {
                Vector3 monsterEye = GetMonsterEyeWorldPosition();
                // As it leans right into the camera, bring eye target directly in front of player's eyes
                Vector3 closeEyeTarget = Vector3.Lerp(monsterEye, playerEyePos + flatToBack * Mathf.Max(0.28f, Vector3.Distance(playerEyePos, monsterEye)), leanProgress * 0.65f);
                Vector3 toMonsterEye = (closeEyeTarget - cam.transform.position).normalized;
                if (toMonsterEye.sqrMagnitude > 0.0001f)
                {
                    Quaternion forcedLook = Quaternion.LookRotation(toMonsterEye, Vector3.up);
                    float scaredTremble = Mathf.Lerp(0.45f, 1.85f, u);
                    Quaternion shakeRot = Quaternion.Euler(
                        (Mathf.PerlinNoise(timer * 28f, 1.2f) - 0.5f) * 2.4f * scaredTremble,
                        (Mathf.PerlinNoise(2.3f, timer * 28f) - 0.5f) * 2.4f * scaredTremble,
                        Mathf.Sin(timer * 19f) * 0.85f * scaredTremble);
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, forcedLook * shakeRot, Time.deltaTime * 18f);
                }

                // Scared rapid shallow breathing bob + FOV tunnel-vision
                float panicBreath = Mathf.Sin(timer * 11.5f) * 0.012f * Mathf.Lerp(0.5f, 1.3f, u);
                cam.transform.localPosition = baseCamLocalPos + new Vector3(0f, panicBreath, 0f);
                cam.fieldOfView = Mathf.Lerp(baseFov, 46f, u);
            }

            yield return null;
        }

        // 5. Monster vanishes & the light comes back ON!
        PlayCorruptedTaskGlitchSfx();
        SetMonsterVisible(false);

        if (stockroomOverheadLight != null)
        {
            stockroomOverheadLight.enabled = true;
            stockroomOverheadLight.intensity = 2.2f;
        }
        RestoreAllGasStationLightsOn();

        if (cam != null)
        {
            cam.transform.localPosition = baseCamLocalPos;
            cam.fieldOfView = baseFov;
        }
        if (fps != null)
        {
            fps.SyncLookAnglesFromCamera();
        }
        SimpleFPSController.IsExternalCameraAnimationActive = false;

        // Unlock Stockroom door and arm it for the immediate exit jumpscare
        if (stockroomDoor != null)
        {
            stockroomDoor.isLockedInStockroomAct4 = false;
            stockroomDoor.isArmedForStockroomExitJumpscare = true;
        }
    }

    public IEnumerator PlayStockroomExitJumpscareRoutine()
    {
        EnsureMonsterPreparedAndHidden();

        SimpleFPSController fps = Object.FindAnyObjectByType<SimpleFPSController>();
        Camera cam = fps != null && fps.playerCamera != null ? fps.playerCamera : Camera.main;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();

        SimpleFPSController.IsExternalCameraAnimationActive = true;

        if (sfxSource != null && jumpscareScreamClip != null)
        {
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(jumpscareScreamClip, 1.0f);
        }

        Vector3 camPos = cam != null ? cam.transform.position : new Vector3(3.34f, 1.75f, 1.65f);
        Vector3 baseCamLocalPos = cam != null ? cam.transform.localPosition : new Vector3(0f, 1.75f, 0f);
        float baseFov = cam != null ? cam.fieldOfView : 60f;
        float floorY = camPos.y - (fps != null ? fps.eyeHeight : 1.75f);

        Vector3 lungeDir = cam != null ? new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z) : Vector3.back;
        if (lungeDir.sqrMagnitude < 0.001f) lungeDir = Vector3.back;
        lungeDir.Normalize();

        Vector3 lungeStartFloor = new Vector3(camPos.x, floorY, camPos.z) + lungeDir * 1.15f;
        Vector3 lungeEndFloor = new Vector3(camPos.x, floorY, camPos.z) + lungeDir * 0.24f;

        if (jumpscareKeyLight == null)
        {
            GameObject jlGo = new GameObject("Act4_Jumpscare_Light");
            jlGo.transform.SetParent(transform, false);
            jumpscareKeyLight = jlGo.AddComponent<Light>();
            jumpscareKeyLight.type = LightType.Point;
            jumpscareKeyLight.range = 4.5f;
            jumpscareKeyLight.shadows = LightShadows.None;
        }
        jumpscareKeyLight.enabled = true;

        SetMonsterVisible(true);

        float dur = 0.54f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / dur);
            float lungeT = 1f - Mathf.Pow(1f - u, 3f);

            Vector3 mPos = Vector3.Lerp(lungeStartFloor, lungeEndFloor, lungeT);
            PlaceMonsterOnFloorFacingPoint(mPos, camPos, -0.05f);

            // Violent head & arm lunge seizure
            if (pelvisBone != null)
            {
                pelvisBone.localRotation = pelvisBaseRot * Quaternion.Euler(0f, 0f, -22f);
            }
            if (neckBone != null)
            {
                neckBone.localRotation = neckBaseRot * Quaternion.Euler(Random.Range(-28f, 28f), Random.Range(-32f, 32f), -24f);
            }
            if (headBone != null)
            {
                headBone.localRotation = headBaseRot * Quaternion.Euler(Random.Range(-42f, 42f), Random.Range(-52f, 52f), Random.Range(-42f, 42f));
            }
            if (leftArmBone != null)
            {
                leftArmBone.localRotation = leftArmBaseRot * Quaternion.Euler(-45f, 0f, -55f);
            }
            if (rightArmBone != null)
            {
                rightArmBone.localRotation = rightArmBaseRot * Quaternion.Euler(45f, 0f, 55f);
            }

            Vector3 monsterEye = GetMonsterEyeWorldPosition();
            if (jumpscareKeyLight != null)
            {
                jumpscareKeyLight.transform.position = Vector3.Lerp(camPos, monsterEye, 0.5f) + Vector3.up * 0.1f;
                jumpscareKeyLight.color = (Random.value > 0.35f) ? new Color(1.0f, 0.88f, 0.82f) : new Color(0.95f, 0.18f, 0.15f);
                jumpscareKeyLight.intensity = Random.Range(2.2f, 4.8f);
            }

            if (cam != null)
            {
                Vector3 toFace = (monsterEye - cam.transform.position).normalized;
                if (toFace.sqrMagnitude > 0.001f)
                {
                    Quaternion snapRot = Quaternion.LookRotation(toFace, Vector3.up) *
                                         Quaternion.Euler(Random.Range(-4.5f, 4.5f), Random.Range(-4.5f, 4.5f), Random.Range(-6f, 6f));
                    cam.transform.rotation = snapRot;
                }
                cam.transform.localPosition = baseCamLocalPos + Random.insideUnitSphere * 0.035f;
                cam.fieldOfView = Mathf.Lerp(baseFov, 36f, lungeT);
            }

            yield return null;
        }

        if (jumpscareKeyLight != null) jumpscareKeyLight.enabled = false;
        SetMonsterVisible(false);
        if (cam != null)
        {
            cam.transform.localPosition = baseCamLocalPos;
            cam.fieldOfView = baseFov;
        }
    }

    private static void RestoreAllGasStationLightsOn()
    {
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        for (int i = 0; i < allLights.Length; i++)
        {
            Light l = allLights[i];
            if (l == null || l.type == LightType.Directional) continue;
            if (l.name.IndexOf("Lightning", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                l.name.IndexOf("Phone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                l.name.IndexOf("Jumpscare", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            l.enabled = true;
            if (l.intensity < 0.2f) l.intensity = 1.2f;
        }

        Renderer[] allRends = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        for (int i = 0; i < allRends.Length; i++)
        {
            Renderer r = allRends[i];
            if (r == null || r.sharedMaterial == null) continue;
            string matName = r.sharedMaterial.name;
            if (matName.IndexOf("glowin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.name.IndexOf("Light_", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                r.enabled = true;
            }
        }
    }
}

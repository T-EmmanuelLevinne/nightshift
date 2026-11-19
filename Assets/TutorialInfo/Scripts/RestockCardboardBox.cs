using System.Collections.Generic;
using UnityEngine;

public class RestockCardboardBox : MonoBehaviour, IInteractable
{
    public static RestockCardboardBox Instance { get; private set; }

    public enum BoxState
    {
        AtOrigin,
        InInventory,
        PlacedOnPutter
    }

    [Header("State")]
    public BoxState currentState = BoxState.AtOrigin;
    [SerializeField] private bool isHoldingCan = false;
    [SerializeField] private int cansTakenFromBox = 0;

    [Header("Held Box Viewmodel (In Hand)")]
    public Vector3 boxViewmodelOffset = new Vector3(0.0f, -0.25f, 0.54f);
    public Vector3 boxViewmodelRotation = new Vector3(10f, 180f, 0f);
    public float boxViewmodelScale = 0.16f;

    [Header("Held Can Viewmodel (In Hand)")]
    public Vector3 canViewmodelOffset = new Vector3(0.22f, -0.18f, 0.42f);
    public Vector3 canViewmodelRotation = new Vector3(-8f, 25f, 4f);
    public Vector3 canViewmodelScale = new Vector3(0.095f, 0.115f, 0.095f);

    // Custom Cardboard Box local dimensions (at scale 0.25 -> 44cm x 26cm x 35cm world size)
    public const float LocalBoxWidth = 1.76f;
    public const float LocalBoxHeight = 1.04f;
    public const float LocalBoxDepth = 1.40f;
    public const float LocalWallThick = 0.045f;

    private Vector3 originalWorldPos;
    private Quaternion originalWorldRot;
    private Vector3 originalWorldScale;

    private UserBoxPutter currentPutter;
    private BoxOriginReturnSpot originReturnSpot;
    private Transform playerCameraTransform;

    private Collider[] boxColliders;
    private Renderer[] boxRenderers;
    private BoxCollider rootInteractionCollider;

    private GameObject heldCanViewmodel;
    private Renderer[] heldCanRenderers;

    private GameObject boxInteriorCansRoot;
    private List<GameObject> interiorCanVisuals = new List<GameObject>();

    private int assignedBoxHotbarSlot = 1;
    private int assignedCanHotbarSlot = 1;
    private float lastStateChangeTime = -10f;

    private AudioSource audioSource;
    private AudioClip boxPickupClip;
    private AudioClip boxPlaceClip;
    private AudioClip canPickupClip;

    private static Mesh cachedCustomBoxMesh;
    private static Mesh cachedCanMesh;

    public bool IsInInventory => currentState == BoxState.InInventory;
    public bool IsPlacedOnPutter => currentState == BoxState.PlacedOnPutter;
    public bool IsAtOrigin => currentState == BoxState.AtOrigin;
    public bool IsHoldingCan => isHoldingCan;
    public Vector3 OriginalWorldPos => originalWorldPos;
    public Quaternion OriginalWorldRot => originalWorldRot;
    public BoxOriginReturnSpot OriginReturnSpot => originReturnSpot;

    public bool IsStockingFinished
    {
        get
        {
            ShelfRestockTarget shelfTarget = ShelfRestockTarget.Instance;
            if (shelfTarget == null) shelfTarget = FindAnyObjectByType<ShelfRestockTarget>();
            return shelfTarget != null && shelfTarget.IsFullyStocked;
        }
    }

    private void Awake()
    {
        Instance = this;

        ApplyCustomCardboardBoxModel();
        EnsureRootInteractionCollider();

        boxColliders = GetComponentsInChildren<Collider>(true);
        boxRenderers = GetComponentsInChildren<Renderer>(true);

        CaptureOriginTransform();
        CreateOrUpdateOriginReturnSpot();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f;
            audioSource.volume = 0.8f;
        }

        GenerateProceduralAudio();
    }

    private void Start()
    {
        EnsureActiveShelfRestockTarget();

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null)
        {
            hotbar.OnSlotChanged -= HandleHotbarSlotChanged;
            hotbar.OnSlotChanged += HandleHotbarSlotChanged;
        }

        BuildInteriorBoxCans();
        UpdateInteriorBoxCansVisual();
    }

    private void EnsureActiveShelfRestockTarget()
    {
        if (ShelfRestockTarget.Instance != null && ShelfRestockTarget.Instance.gameObject.activeInHierarchy)
        {
            return;
        }

        ShelfRestockTarget[] allTargets = FindObjectsByType<ShelfRestockTarget>(FindObjectsInactive.Exclude);
        if (allTargets != null && allTargets.Length > 0)
        {
            return;
        }

        // Find any active shelf in the scene that has a Restock_Cans_Slot (such as Store_Gondola_Shelf_Stocked)
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform tr = allTransforms[i];
            if (tr != null && tr.name == "Restock_Cans_Slot" && tr.parent != null && tr.parent.gameObject.activeInHierarchy)
            {
                GameObject activeShelf = tr.parent.gameObject;
                ShelfRestockTarget target = activeShelf.GetComponent<ShelfRestockTarget>();
                if (target == null)
                {
                    target = activeShelf.AddComponent<ShelfRestockTarget>();
                }
                target.restockCansSlot = tr.gameObject;
                break;
            }
        }
    }

    /// <summary>
    /// Replaces any imported mesh with our custom-designed open-flap 3D Cardboard Box mesh
    /// mapped directly to the existing cardboard box texture material.
    /// </summary>
    public void ApplyCustomCardboardBoxModel()
    {
        Material cardboardMat = ResolveCardboardMaterial();
        Mesh customMesh = GetOrCreateCustomCardboardBoxMesh();

        // Find or create the primary mesh object on this Cardboard_Box
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        MeshFilter mainFilter = null;

        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i] == null) continue;
            string n = filters[i].gameObject.name;
            if (n.StartsWith("BoxCan_") || n == "Held_Stock_Can_Viewmodel") continue;

            if (mainFilter == null)
            {
                mainFilter = filters[i];
            }
            else
            {
                // Disable any extra legacy sub-meshes
                Renderer extraRend = filters[i].GetComponent<Renderer>();
                if (extraRend != null) extraRend.enabled = false;
            }
        }

        if (mainFilter == null)
        {
            mainFilter = gameObject.GetComponent<MeshFilter>();
            if (mainFilter == null) mainFilter = gameObject.AddComponent<MeshFilter>();
        }

        // Reset local transform if mainFilter is on a child (e.g., from model.dae which had -90 X rotation)
        if (mainFilter.transform != transform)
        {
            mainFilter.transform.localPosition = Vector3.zero;
            mainFilter.transform.localRotation = Quaternion.identity;
            mainFilter.transform.localScale = Vector3.one;
        }

        mainFilter.sharedMesh = customMesh;

        MeshRenderer mr = mainFilter.GetComponent<MeshRenderer>();
        if (mr == null) mr = mainFilter.gameObject.AddComponent<MeshRenderer>();
        if (cardboardMat != null)
        {
            mr.sharedMaterials = new Material[] { cardboardMat };
        }
    }

    private Material ResolveCardboardMaterial()
    {
        MeshRenderer[] mrs = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < mrs.Length; i++)
        {
            if (mrs[i] != null && mrs[i].sharedMaterial != null && !mrs[i].gameObject.name.StartsWith("BoxCan_"))
            {
                return mrs[i].sharedMaterial;
            }
        }
        return null;
    }

    public static Mesh GetOrCreateCustomCardboardBoxMesh()
    {
        if (cachedCustomBoxMesh != null) return cachedCustomBoxMesh;

        Mesh mesh = new Mesh();
        mesh.name = "Custom_Cardboard_Box_Design";

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float hx = LocalBoxWidth * 0.5f;   // 0.88
        float yTop = LocalBoxHeight;       // 1.04
        float hz = LocalBoxDepth * 0.5f;   // 0.70
        float t = LocalWallThick;          // 0.045
        float ihx = hx - t;
        float ihz = hz - t;
        float iyFloor = t;

        // UV Atlas regions from cardboadtexture.jpg / Material_albedo.jpeg:
        // - Front Wall (with FRAGILE + Barcode): U [0.01, 0.34], V [0.01, 0.145]
        Rect uvFront = new Rect(0.01f, 0.01f, 0.33f, 0.135f);
        // - Back Wall (with Priority Label + Tape): U [0.01, 0.34], V [0.148, 0.285]
        Rect uvBack = new Rect(0.01f, 0.148f, 0.33f, 0.137f);
        // - Left & Right Walls (with Recycle icon & vertical tape): U [0.695, 0.965], V [0.74, 0.885]
        Rect uvSide = new Rect(0.695f, 0.74f, 0.27f, 0.145f);
        // - Clean Weathered Cardboard (Inner walls / Bottom / Flaps): U [0.355, 0.680], V [0.56, 0.83]
        Rect uvClean = new Rect(0.355f, 0.56f, 0.325f, 0.27f);
        // - Cardboard with Horizontal Tape Strip: U [0.355, 0.680], V [0.30, 0.54]
        Rect uvTape = new Rect(0.355f, 0.30f, 0.325f, 0.24f);
        // - Flap Panel: U [0.698, 0.960], V [0.30, 0.48]
        Rect uvFlap = new Rect(0.698f, 0.30f, 0.262f, 0.18f);
        // - Corrugated Cardboard Rim Edge: U [0.40, 0.60], V [0.94, 0.96]
        Rect uvRim = new Rect(0.40f, 0.94f, 0.20f, 0.02f);

        // 1. OUTER WALLS & BOTTOM
        // Front Outer (-Z facing)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-hx, 0f, -hz),
            new Vector3(-hx, yTop, -hz),
            new Vector3(hx, yTop, -hz),
            new Vector3(hx, 0f, -hz),
            Vector3.back, uvFront);

        // Back Outer (+Z facing)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(hx, 0f, hz),
            new Vector3(hx, yTop, hz),
            new Vector3(-hx, yTop, hz),
            new Vector3(-hx, 0f, hz),
            Vector3.forward, uvBack);

        // Left Outer (-X facing)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-hx, 0f, hz),
            new Vector3(-hx, yTop, hz),
            new Vector3(-hx, yTop, -hz),
            new Vector3(-hx, 0f, -hz),
            Vector3.left, uvSide);

        // Right Outer (+X facing)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(hx, 0f, -hz),
            new Vector3(hx, yTop, -hz),
            new Vector3(hx, yTop, hz),
            new Vector3(hx, 0f, hz),
            Vector3.right, uvSide);

        // Bottom Outer (-Y facing)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-hx, 0f, hz),
            new Vector3(-hx, 0f, -hz),
            new Vector3(hx, 0f, -hz),
            new Vector3(hx, 0f, hz),
            Vector3.down, uvTape);

        // 2. INNER CAVITY WALLS & INNER FLOOR
        // Inner Floor (+Y facing)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-ihx, iyFloor, -ihz),
            new Vector3(-ihx, iyFloor, ihz),
            new Vector3(ihx, iyFloor, ihz),
            new Vector3(ihx, iyFloor, -ihz),
            Vector3.up, uvClean);

        // Inner Front (+Z facing inside)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(ihx, iyFloor, -ihz),
            new Vector3(ihx, yTop, -ihz),
            new Vector3(-ihx, yTop, -ihz),
            new Vector3(-ihx, iyFloor, -ihz),
            Vector3.forward, uvClean);

        // Inner Back (-Z facing inside)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-ihx, iyFloor, ihz),
            new Vector3(-ihx, yTop, ihz),
            new Vector3(ihx, yTop, ihz),
            new Vector3(ihx, iyFloor, ihz),
            Vector3.back, uvClean);

        // Inner Left (+X facing inside)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-ihx, iyFloor, -ihz),
            new Vector3(-ihx, yTop, -ihz),
            new Vector3(-ihx, yTop, ihz),
            new Vector3(-ihx, iyFloor, ihz),
            Vector3.right, uvClean);

        // Inner Right (-X facing inside)
        AddQuad(verts, norms, uvs, tris,
            new Vector3(ihx, iyFloor, ihz),
            new Vector3(ihx, yTop, ihz),
            new Vector3(ihx, yTop, -ihz),
            new Vector3(ihx, iyFloor, -ihz),
            Vector3.left, uvClean);

        // 3. TOP RIM CORRUGATED LIPS
        // Front Lip
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-hx, yTop, -hz),
            new Vector3(-ihx, yTop, -ihz),
            new Vector3(ihx, yTop, -ihz),
            new Vector3(hx, yTop, -hz),
            Vector3.up, uvRim);
        // Back Lip
        AddQuad(verts, norms, uvs, tris,
            new Vector3(hx, yTop, hz),
            new Vector3(ihx, yTop, ihz),
            new Vector3(-ihx, yTop, ihz),
            new Vector3(-hx, yTop, hz),
            Vector3.up, uvRim);
        // Left Lip
        AddQuad(verts, norms, uvs, tris,
            new Vector3(-hx, yTop, hz),
            new Vector3(-ihx, yTop, ihz),
            new Vector3(-ihx, yTop, -ihz),
            new Vector3(-hx, yTop, -hz),
            Vector3.up, uvRim);
        // Right Lip
        AddQuad(verts, norms, uvs, tris,
            new Vector3(hx, yTop, -hz),
            new Vector3(ihx, yTop, -ihz),
            new Vector3(ihx, yTop, ihz),
            new Vector3(hx, yTop, hz),
            Vector3.up, uvRim);

        // 4. FOUR ANGLED OPEN CARDBOARD FLAPS
        float fbFlapLen = LocalBoxDepth * 0.44f; // 0.616
        float lrFlapLen = LocalBoxWidth * 0.32f; // 0.563
        float flapThick = 0.025f;

        // Front Flap: hinged at (-hx..hx, yTop, -hz), folded outward (-Z) and slightly downward (-28 deg)
        Vector3 frontDir = new Vector3(0f, -Mathf.Sin(28f * Mathf.Deg2Rad), -Mathf.Cos(28f * Mathf.Deg2Rad)).normalized;
        AddOpenFlap(verts, norms, uvs, tris,
            new Vector3(-hx + 0.02f, yTop, -hz),
            new Vector3(hx - 0.02f, yTop, -hz),
            frontDir, fbFlapLen, flapThick, uvFlap, uvTape, uvRim);

        // Back Flap: hinged at (hx..-hx, yTop, hz), folded outward (+Z) and slightly downward (-28 deg)
        Vector3 backDir = new Vector3(0f, -Mathf.Sin(28f * Mathf.Deg2Rad), Mathf.Cos(28f * Mathf.Deg2Rad)).normalized;
        AddOpenFlap(verts, norms, uvs, tris,
            new Vector3(hx - 0.02f, yTop, hz),
            new Vector3(-hx + 0.02f, yTop, hz),
            backDir, fbFlapLen, flapThick, uvFlap, uvTape, uvRim);

        // Left Flap: hinged at (-hx, yTop, hz..-hz), folded outward (-X) and slightly downward (-22 deg)
        Vector3 leftDir = new Vector3(-Mathf.Cos(22f * Mathf.Deg2Rad), -Mathf.Sin(22f * Mathf.Deg2Rad), 0f).normalized;
        AddOpenFlap(verts, norms, uvs, tris,
            new Vector3(-hx, yTop, hz - 0.02f),
            new Vector3(-hx, yTop, -hz + 0.02f),
            leftDir, lrFlapLen, flapThick, uvFlap, uvClean, uvRim);

        // Right Flap: hinged at (hx, yTop, -hz..hz), folded outward (+X) and slightly downward (-22 deg)
        Vector3 rightDir = new Vector3(Mathf.Cos(22f * Mathf.Deg2Rad), -Mathf.Sin(22f * Mathf.Deg2Rad), 0f).normalized;
        AddOpenFlap(verts, norms, uvs, tris,
            new Vector3(hx, yTop, -hz + 0.02f),
            new Vector3(hx, yTop, hz - 0.02f),
            rightDir, lrFlapLen, flapThick, uvFlap, uvClean, uvRim);

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        cachedCustomBoxMesh = mesh;
        return mesh;
    }

    private static void AddOpenFlap(
        List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3 hingeLeft, Vector3 hingeRight, Vector3 outDir, float length, float thickness,
        Rect topUv, Rect botUv, Rect rimUv)
    {
        Vector3 edgeAlong = (hingeRight - hingeLeft).normalized;
        Vector3 topNormal = Vector3.Cross(outDir, edgeAlong).normalized;
        if (topNormal.y < 0f) topNormal = -topNormal;

        Vector3 tipLeft = hingeLeft + outDir * length;
        Vector3 tipRight = hingeRight + outDir * length;

        Vector3 hL_Top = hingeLeft + topNormal * (thickness * 0.5f);
        Vector3 hR_Top = hingeRight + topNormal * (thickness * 0.5f);
        Vector3 tL_Top = tipLeft + topNormal * (thickness * 0.5f);
        Vector3 tR_Top = tipRight + topNormal * (thickness * 0.5f);

        Vector3 hL_Bot = hingeLeft - topNormal * (thickness * 0.5f);
        Vector3 hR_Bot = hingeRight - topNormal * (thickness * 0.5f);
        Vector3 tL_Bot = tipLeft - topNormal * (thickness * 0.5f);
        Vector3 tR_Bot = tipRight - topNormal * (thickness * 0.5f);

        // Top face of flap
        AddQuad(verts, norms, uvs, tris, hL_Top, tL_Top, tR_Top, hR_Top, topNormal, topUv);
        // Bottom/underside face of flap
        AddQuad(verts, norms, uvs, tris, hR_Bot, tR_Bot, tL_Bot, hL_Bot, -topNormal, botUv);
        // Outer tip rim
        AddQuad(verts, norms, uvs, tris, tL_Bot, tR_Bot, tR_Top, tL_Top, outDir, rimUv);
        // Left side rim
        AddQuad(verts, norms, uvs, tris, hL_Bot, tL_Bot, tL_Top, hL_Top, -edgeAlong, rimUv);
        // Right side rim
        AddQuad(verts, norms, uvs, tris, tR_Bot, hR_Bot, hR_Top, tR_Top, edgeAlong, rimUv);
    }

    private static void AddQuad(
        List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal, Rect uvRect)
    {
        int baseIdx = verts.Count;
        verts.Add(v0);
        verts.Add(v1);
        verts.Add(v2);
        verts.Add(v3);

        norms.Add(normal);
        norms.Add(normal);
        norms.Add(normal);
        norms.Add(normal);

        uvs.Add(new Vector2(uvRect.xMin, uvRect.yMin));
        uvs.Add(new Vector2(uvRect.xMin, uvRect.yMax));
        uvs.Add(new Vector2(uvRect.xMax, uvRect.yMax));
        uvs.Add(new Vector2(uvRect.xMax, uvRect.yMin));

        tris.Add(baseIdx + 0);
        tris.Add(baseIdx + 1);
        tris.Add(baseIdx + 2);

        tris.Add(baseIdx + 0);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx + 3);
    }

    private void CaptureOriginTransform()
    {
        originalWorldPos = transform.position;
        originalWorldRot = transform.rotation;
        originalWorldScale = transform.localScale;
    }

    private void EnsureRootInteractionCollider()
    {
        rootInteractionCollider = GetComponent<BoxCollider>();
        if (rootInteractionCollider == null)
        {
            rootInteractionCollider = gameObject.AddComponent<BoxCollider>();
        }

        rootInteractionCollider.center = new Vector3(0f, LocalBoxHeight * 0.5f, 0f);
        rootInteractionCollider.size = new Vector3(LocalBoxWidth * 1.15f, LocalBoxHeight * 1.05f, LocalBoxDepth * 1.15f);
    }

    private void CreateOrUpdateOriginReturnSpot()
    {
        if (originReturnSpot == null)
        {
            GameObject existing = GameObject.Find("CardboardBox_OriginSpot");
            if (existing != null)
            {
                originReturnSpot = existing.GetComponent<BoxOriginReturnSpot>();
                if (originReturnSpot == null)
                {
                    originReturnSpot = existing.AddComponent<BoxOriginReturnSpot>();
                }
            }
            else
            {
                GameObject spotGo = new GameObject("CardboardBox_OriginSpot");
                originReturnSpot = spotGo.AddComponent<BoxOriginReturnSpot>();
            }
        }

        originReturnSpot.ownerBox = this;
        originReturnSpot.transform.position = originalWorldPos;
        originReturnSpot.transform.rotation = originalWorldRot;
    }

    private void BuildInteriorBoxCans()
    {
        if (boxInteriorCansRoot != null) return;

        boxInteriorCansRoot = new GameObject("Box_Interior_Cans");
        boxInteriorCansRoot.transform.SetParent(transform, false);
        boxInteriorCansRoot.transform.localPosition = Vector3.zero;
        boxInteriorCansRoot.transform.localRotation = Quaternion.identity;
        boxInteriorCansRoot.transform.localScale = Vector3.one;

        Material canLabelMat = null;
        Material canLidMat = null;
        ResolveShelfCanMaterials(out canLabelMat, out canLidMat);

        Mesh canMesh = CreateSharedCanMesh();

        // Arrange 12 cans (4x3 grid) sitting on the inner floor of our custom cardboard box
        int cols = 4;
        int rows = 3;
        float spanX = (LocalBoxWidth - LocalWallThick * 4f) * 0.72f;
        float spanZ = (LocalBoxDepth - LocalWallThick * 4f) * 0.68f;
        float canHeight = LocalBoxHeight * 0.62f;
        float canDiam = (LocalBoxWidth / cols) * 0.68f;
        float canCenterY = LocalWallThick + canHeight * 0.5f;

        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                idx++;
                GameObject canObj = new GameObject($"BoxCan_{idx}");
                canObj.transform.SetParent(boxInteriorCansRoot.transform, false);

                float tx = (cols > 1) ? ((float)c / (cols - 1) - 0.5f) : 0f;
                float tz = (rows > 1) ? ((float)r / (rows - 1) - 0.5f) : 0f;

                canObj.transform.localPosition = new Vector3(tx * spanX, canCenterY, tz * spanZ);
                canObj.transform.localRotation = Quaternion.Euler(0f, (idx * 37f) % 360f, 0f);
                canObj.transform.localScale = new Vector3(canDiam, canHeight, canDiam);

                MeshFilter mf = canObj.AddComponent<MeshFilter>();
                mf.sharedMesh = canMesh;

                MeshRenderer mr = canObj.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (canLabelMat != null && canLidMat != null)
                {
                    mr.sharedMaterials = new Material[] { canLabelMat, canLidMat };
                }
                else if (canLabelMat != null)
                {
                    mr.sharedMaterials = new Material[] { canLabelMat, canLabelMat };
                }

                interiorCanVisuals.Add(canObj);
            }
        }
    }

    public int RemainingCansInBox
    {
        get
        {
            ShelfRestockTarget shelfTarget = ShelfRestockTarget.Instance;
            if (shelfTarget == null) shelfTarget = FindAnyObjectByType<ShelfRestockTarget>();
            int goal = shelfTarget != null ? shelfTarget.goalCount : 12;
            int stocked = shelfTarget != null ? shelfTarget.stockedCount : 0;
            int held = isHoldingCan ? 1 : 0;
            int maxVisuals = interiorCanVisuals != null && interiorCanVisuals.Count > 0 ? interiorCanVisuals.Count : 12;
            return Mathf.Clamp(goal - stocked - held, 0, maxVisuals);
        }
    }

    public void UpdateInteriorBoxCansVisual()
    {
        if (boxInteriorCansRoot == null || interiorCanVisuals == null) return;

        int remaining = RemainingCansInBox;

        for (int i = 0; i < interiorCanVisuals.Count; i++)
        {
            if (interiorCanVisuals[i] != null)
            {
                interiorCanVisuals[i].SetActive(i < remaining);
            }
        }
    }

    private bool isRadialGrabActive = false;
    private float radialGrabProgress = 0f;
    private const float RadialGrabDuration = 0.48f;
    private int pendingRadialAction = 0; // 1 = pickup can, 2 = pickup box from origin, 3 = pickup empty box
    private static Texture2D radialDotTex;

    private void Update()
    {
        // Keep origin spot synced if user drags the box in Scene View while it sits at its initial origin
        if (currentState == BoxState.AtOrigin && transform.parent == null)
        {
            int stocked = ShelfRestockTarget.Instance != null ? ShelfRestockTarget.Instance.stockedCount : 0;
            if (stocked == 0 && cansTakenFromBox == 0 && Vector3.Distance(transform.position, originalWorldPos) > 0.005f)
            {
                CaptureOriginTransform();
                if (originReturnSpot != null)
                {
                    originReturnSpot.transform.position = originalWorldPos;
                    originReturnSpot.transform.rotation = originalWorldRot;
                }
            }
        }

        // Keep box adaptively snapped to UserBoxPutter if user moves UserBoxPutter in Scene View
        if (currentState == BoxState.PlacedOnPutter && currentPutter != null && transform.parent == null)
        {
            transform.position = currentPutter.transform.position;
            transform.rotation = currentPutter.transform.rotation;
        }

        // Advance 360-degree radial circle fill when grabbing a can or picking up the box
        if (isRadialGrabActive)
        {
            Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
            if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
            if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > 2.15f)
            {
                isRadialGrabActive = false;
                radialGrabProgress = 0f;
                pendingRadialAction = 0;
            }
            else
            {
                radialGrabProgress += Time.unscaledDeltaTime / RadialGrabDuration;
                if (radialGrabProgress >= 1f)
                {
                    int act = pendingRadialAction;
                    isRadialGrabActive = false;
                    radialGrabProgress = 0f;
                    pendingRadialAction = 0;

                    if (act == 1 && !isHoldingCan && RemainingCansInBox > 0)
                    {
                        PickupCanFromBox();
                    }
                    else if (act == 2)
                    {
                        PickupBoxIntoInventory(fromOrigin: true);
                    }
                    else if (act == 3)
                    {
                        PickupBoxIntoInventory(fromOrigin: false);
                    }
                }
            }
        }

        // Allow Q key or E key to put back the box ONLY after all 12 cans are stocked
        if (currentState == BoxState.InInventory && IsStockingFinished && IsPlayerNearOriginSpot(1.45f) && HorrorInteractionReticle.WasQPressed())
        {
            PutBackAtOrigin();
        }
    }

    private void OnGUI()
    {
        if (!isRadialGrabActive || Event.current.type != EventType.Repaint) return;
        DrawRadialFillCircle(Screen.width * 0.5f, Screen.height * 0.5f, Mathf.Clamp01(radialGrabProgress));
    }

    public static void DrawRadialFillCircle(float centerX, float centerY, float progress)
    {
        if (radialDotTex == null)
        {
            int sz = 16;
            radialDotTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            float c = (sz - 1) * 0.5f;
            float r = sz * 0.45f;
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                    float a = Mathf.Clamp01((r - d) * 1.4f);
                    radialDotTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            radialDotTex.Apply();
        }

        int totalSegs = 64;
        float radius = 25f;
        float dotSize = 5.5f;
        int filledSegs = Mathf.RoundToInt(progress * totalSegs);

        Color prevCol = GUI.color;

        // 1. Background full 360-degree dark track ring
        GUI.color = new Color(0.06f, 0.06f, 0.06f, 0.78f);
        for (int i = 0; i < totalSegs; i++)
        {
            float ang = ((float)i / totalSegs) * Mathf.PI * 2f - Mathf.PI * 0.5f;
            float px = centerX + Mathf.Cos(ang) * radius;
            float py = centerY + Mathf.Sin(ang) * radius;
            GUI.DrawTexture(new Rect(px - (dotSize + 2f) * 0.5f, py - (dotSize + 2f) * 0.5f, dotSize + 2f, dotSize + 2f), radialDotTex);
        }

        // 2. Foreground 360-degree radial fill arc sweeping clockwise all around
        Color fillCol = Color.Lerp(new Color(0.96f, 0.84f, 0.42f, 0.96f), new Color(0.75f, 0.98f, 0.65f, 1.0f), progress);
        GUI.color = fillCol;
        for (int i = 0; i <= filledSegs && i < totalSegs; i++)
        {
            float ang = ((float)i / totalSegs) * Mathf.PI * 2f - Mathf.PI * 0.5f;
            float px = centerX + Mathf.Cos(ang) * radius;
            float py = centerY + Mathf.Sin(ang) * radius;
            GUI.DrawTexture(new Rect(px - dotSize * 0.5f, py - dotSize * 0.5f, dotSize, dotSize), radialDotTex);
        }

        GUI.color = prevCol;
    }

    private void LateUpdate()
    {
        if (currentState == BoxState.InInventory && playerCameraTransform != null)
        {
            bool isSlotActive = (HorrorHotbar.Instance == null || HorrorHotbar.Instance.GetSlotIndexWithItem("BOX") == HorrorHotbar.Instance.activeSlotIndex);
            if (isSlotActive)
            {
                transform.localPosition = boxViewmodelOffset;
                transform.localRotation = Quaternion.Euler(boxViewmodelRotation);
                transform.localScale = Vector3.one * boxViewmodelScale;
            }
        }

        if (isHoldingCan && heldCanViewmodel != null && playerCameraTransform != null)
        {
            bool isSlotActive = (HorrorHotbar.Instance == null || HorrorHotbar.Instance.GetSlotIndexWithItem("CAN") == HorrorHotbar.Instance.activeSlotIndex);
            if (isSlotActive)
            {
                heldCanViewmodel.transform.localPosition = canViewmodelOffset;
                heldCanViewmodel.transform.localRotation = Quaternion.Euler(canViewmodelRotation);
                heldCanViewmodel.transform.localScale = canViewmodelScale;
            }
        }
    }

    public bool IsPlayerNearOriginSpot(float maxDist = 1.45f)
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        Vector3 pPos = cam.transform.position;
        float hDist = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(originalWorldPos.x, originalWorldPos.z));
        float vDist = Mathf.Abs(pPos.y - originalWorldPos.y);
        return hDist <= maxDist && vDist <= 2.2f;
    }

    public string GetPromptText()
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > 1.75f)
        {
            return string.Empty;
        }

        bool shelfDone = IsStockingFinished;

        if (currentState == BoxState.AtOrigin)
        {
            if (!NightShiftStoryController.IsStockingTaskActive)
            {
                return string.Empty;
            }
            return "E to pickup box";
        }

        if (currentState == BoxState.PlacedOnPutter)
        {
        
            if (shelfDone)
            {
                return "E to pickup empty box";
            }

            if (isHoldingCan)
            {
                ShelfRestockTarget shelfTarget = ShelfRestockTarget.Instance;
                int count = shelfTarget != null ? shelfTarget.stockedCount : 0;
                int goal = shelfTarget != null ? shelfTarget.goalCount : 12;
                return $"Stock can on shelf ({count}/{goal})";
            }

            int remaining = RemainingCansInBox;
            if (remaining > 0)
            {
                ShelfRestockTarget shelfTarget = ShelfRestockTarget.Instance;
                int stocked = shelfTarget != null ? shelfTarget.stockedCount : 0;
                return $"E to take can ({stocked + 1}/12)";
            }

            return "Box is empty";
        }

        return string.Empty;
    }

    public void Interact()
    {
        // Guard against same-frame double-trigger when placing box onto UserBoxPutter
        if (Time.unscaledTime - lastStateChangeTime < 0.18f) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > 1.75f)
        {
            return;
        }

        bool shelfDone = IsStockingFinished;

        if (currentState == BoxState.AtOrigin)
        {
            if (!NightShiftStoryController.IsStockingTaskActive)
            {
                return;
            }
            PickupBoxIntoInventory(fromOrigin: true);
            return;
        }

        if (currentState == BoxState.PlacedOnPutter)
        {
            // When placed on UserBoxPutter, allow picking up the box ONLY when shelf is fully stocked
            if (shelfDone)
            {
                PickupBoxIntoInventory(fromOrigin: false);
            }
            else if (!isHoldingCan && RemainingCansInBox > 0 && !isRadialGrabActive)
            {
                pendingRadialAction = 1;
                radialGrabProgress = 0f;
                isRadialGrabActive = true;
            }
        }
    }

    public void PickupBoxIntoInventory(bool fromOrigin)
    {
        // Safety guard: never allow picking up from putter before all 12 cans are stocked
        if (!fromOrigin && !IsStockingFinished)
        {
            return;
        }

        // Clean up any remaining can state
        isHoldingCan = false;
        if (heldCanViewmodel != null) heldCanViewmodel.SetActive(false);
        if (HorrorHotbar.Instance != null) HorrorHotbar.Instance.ClearItem("CAN");

        if (fromOrigin && transform.parent == null)
        {
            int stocked = ShelfRestockTarget.Instance != null ? ShelfRestockTarget.Instance.stockedCount : 0;
            if (stocked == 0 && cansTakenFromBox == 0)
            {
                CaptureOriginTransform();
                CreateOrUpdateOriginReturnSpot();
            }
        }

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return;

        lastStateChangeTime = Time.unscaledTime;
        playerCameraTransform = cam.transform;
        currentState = BoxState.InInventory;
        currentPutter = null;

        SetBoxCollidersEnabled(false);

        transform.SetParent(playerCameraTransform, false);
        transform.localPosition = boxViewmodelOffset;
        transform.localRotation = Quaternion.Euler(boxViewmodelRotation);
        transform.localScale = Vector3.one * boxViewmodelScale;

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null)
        {
            hotbar.ClearItem("BOX");
            assignedBoxHotbarSlot = ChooseBestHotbarSlot(hotbar);
            hotbar.OnSlotChanged -= HandleHotbarSlotChanged;
            hotbar.OnSlotChanged += HandleHotbarSlotChanged;
            hotbar.SetSlot(assignedBoxHotbarSlot, "BOX", null);
            hotbar.SetActiveSlot(assignedBoxHotbarSlot);
            SetBoxRenderersVisible(hotbar.IsSlotActive(assignedBoxHotbarSlot));
        }
        else
        {
            SetBoxRenderersVisible(true);
        }

        UpdateInteriorBoxCansVisual();
        PlaySound(boxPickupClip, 0.98f);
    }

    public void PlaceOnPutter(UserBoxPutter putter)
    {
        if (putter == null) return;

        lastStateChangeTime = Time.unscaledTime;
        currentPutter = putter;
        currentState = BoxState.PlacedOnPutter;

        transform.SetParent(null, true);
        transform.position = putter.transform.position;
        transform.rotation = putter.transform.rotation;
        transform.localScale = originalWorldScale;

        SetBoxCollidersEnabled(true);
        SetBoxRenderersVisible(true);
        UpdateInteriorBoxCansVisual();

        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.ClearItem("BOX");
        }

        PlaySound(boxPlaceClip, 0.95f);
    }

    public void PutBackAtOrigin()
    {
        // Only allow putting back at origin once the 12-can stocking goal is finished
        if (currentState != BoxState.InInventory || !IsStockingFinished) return;

        lastStateChangeTime = Time.unscaledTime;
        currentState = BoxState.AtOrigin;
        currentPutter = null;

        transform.SetParent(null, true);
        transform.position = originalWorldPos;
        transform.rotation = originalWorldRot;
        transform.localScale = originalWorldScale;

        SetBoxCollidersEnabled(true);
        SetBoxRenderersVisible(true);
        UpdateInteriorBoxCansVisual();

        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.ClearItem("BOX");
        }

        PlaySound(boxPlaceClip, 1.02f);
    }

    public void PickupCanFromBox()
    {
        if (isHoldingCan || IsStockingFinished || RemainingCansInBox <= 0) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return;

        lastStateChangeTime = Time.unscaledTime;
        playerCameraTransform = cam.transform;
        isHoldingCan = true;
        cansTakenFromBox++;

        EnsureHeldCanViewmodel();
        heldCanViewmodel.transform.SetParent(playerCameraTransform, false);
        heldCanViewmodel.transform.localPosition = canViewmodelOffset;
        heldCanViewmodel.transform.localRotation = Quaternion.Euler(canViewmodelRotation);
        heldCanViewmodel.transform.localScale = canViewmodelScale;
        heldCanViewmodel.SetActive(true);

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null)
        {
            hotbar.ClearItem("CAN");
            assignedCanHotbarSlot = ChooseBestHotbarSlot(hotbar);
            hotbar.OnSlotChanged -= HandleHotbarSlotChanged;
            hotbar.OnSlotChanged += HandleHotbarSlotChanged;
            hotbar.SetSlot(assignedCanHotbarSlot, "CAN", null);
            hotbar.SetActiveSlot(assignedCanHotbarSlot);
            SetHeldCanVisible(hotbar.IsSlotActive(assignedCanHotbarSlot));
        }
        else
        {
            SetHeldCanVisible(true);
        }

        UpdateInteriorBoxCansVisual();
        PlaySound(canPickupClip, Random.Range(0.95f, 1.06f));
    }

    public void ConsumeHeldCan()
    {
        isHoldingCan = false;

        if (heldCanViewmodel != null)
        {
            heldCanViewmodel.SetActive(false);
        }

        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.ClearItem("CAN");
        }

        UpdateInteriorBoxCansVisual();
    }

    public void OnShelfFullyStocked()
    {
        isHoldingCan = false;

        if (heldCanViewmodel != null)
        {
            heldCanViewmodel.SetActive(false);
        }

        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.ClearItem("CAN");
        }

        UpdateInteriorBoxCansVisual();
    }

    private int ChooseBestHotbarSlot(HorrorHotbar hotbar)
    {
        if (hotbar == null) return 1;

        int active = hotbar.activeSlotIndex;
        if (!hotbar.HasItem(active)) return active;

        for (int i = 0; i < hotbar.slotCount; i++)
        {
            if (!hotbar.HasItem(i)) return i;
        }

        return 1;
    }

    private void HandleHotbarSlotChanged(int newSlot)
    {
        if (currentState == BoxState.InInventory)
        {
            bool isBoxActive = (HorrorHotbar.Instance != null && HorrorHotbar.Instance.GetSlotIndexWithItem("BOX") == newSlot);
            SetBoxRenderersVisible(isBoxActive);
            if (isBoxActive)
            {
                transform.localPosition = boxViewmodelOffset;
                transform.localRotation = Quaternion.Euler(boxViewmodelRotation);
                transform.localScale = Vector3.one * boxViewmodelScale;
            }
        }

        if (isHoldingCan && heldCanViewmodel != null)
        {
            bool isCanActive = (HorrorHotbar.Instance != null && HorrorHotbar.Instance.GetSlotIndexWithItem("CAN") == newSlot);
            SetHeldCanVisible(isCanActive);
            if (isCanActive)
            {
                heldCanViewmodel.transform.localPosition = canViewmodelOffset;
                heldCanViewmodel.transform.localRotation = Quaternion.Euler(canViewmodelRotation);
                heldCanViewmodel.transform.localScale = canViewmodelScale;
            }
        }
    }

    private void EnsureHeldCanViewmodel()
    {
        if (heldCanViewmodel != null) return;

        heldCanViewmodel = new GameObject("Held_Stock_Can_Viewmodel");
        MeshFilter mf = heldCanViewmodel.AddComponent<MeshFilter>();
        mf.sharedMesh = CreateSharedCanMesh();

        MeshRenderer mr = heldCanViewmodel.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Material canLabelMat = null;
        Material canLidMat = null;
        ResolveShelfCanMaterials(out canLabelMat, out canLidMat);

        if (canLabelMat != null && canLidMat != null)
        {
            mr.sharedMaterials = new Material[] { canLabelMat, canLidMat };
        }
        else if (canLabelMat != null)
        {
            mr.sharedMaterials = new Material[] { canLabelMat, canLabelMat };
        }

        heldCanRenderers = heldCanViewmodel.GetComponentsInChildren<Renderer>(true);
    }

    private void ResolveShelfCanMaterials(out Material labelMat, out Material lidMat)
    {
        labelMat = null;
        lidMat = null;

        ShelfRestockTarget target = ShelfRestockTarget.Instance;
        if (target == null) target = FindAnyObjectByType<ShelfRestockTarget>();

        if (target != null && target.restockCansSlot != null)
        {
            MeshRenderer r = target.restockCansSlot.GetComponent<MeshRenderer>();
            if (r != null && r.sharedMaterials != null && r.sharedMaterials.Length > 0)
            {
                labelMat = r.sharedMaterials[0];
                lidMat = r.sharedMaterials.Length > 1 ? r.sharedMaterials[1] : r.sharedMaterials[0];
                return;
            }
        }

        GameObject shelf1 = GameObject.Find("Shelf1");
        if (shelf1 != null)
        {
            MeshRenderer r = shelf1.GetComponent<MeshRenderer>();
            if (r != null && r.sharedMaterials != null && r.sharedMaterials.Length >= 5)
            {
                labelMat = r.sharedMaterials[3]; // Material.003 (soup can label)
                lidMat = r.sharedMaterials[4];   // Material.004 (tin lid)
                return;
            }
        }

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = Shader.Find("Standard");
        labelMat = new Material(lit) { color = new Color(0.76f, 0.22f, 0.18f) };
        lidMat = new Material(lit) { color = new Color(0.75f, 0.77f, 0.78f) };
    }

    public static Mesh CreateSharedCanMesh()
    {
        if (cachedCanMesh != null) return cachedCanMesh;

        Mesh mesh = new Mesh();
        mesh.name = "Procedural_Stock_Can_Mesh";

        int segs = 20;
        float radius = 0.5f;
        float halfH = 0.5f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> sideTris = new List<int>();
        List<int> capTris = new List<int>();

        float uMin = 0.431f;
        float uMax = 0.471f;
        float vMin = 0.305f;
        float vMax = 0.332f;

        // 1. Cylinder Side Wall (Submesh 0 -> Material.003 Soup Can Label)
        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs;
            float ang = t * Mathf.PI * 2f;
            float x = Mathf.Cos(ang) * radius;
            float z = Mathf.Sin(ang) * radius;
            Vector3 n = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));

            float u = Mathf.Lerp(uMin, uMax, t);

            verts.Add(new Vector3(x, -halfH, z));
            norms.Add(n);
            uvs.Add(new Vector2(u, vMin));

            verts.Add(new Vector3(x, halfH, z));
            norms.Add(n);
            uvs.Add(new Vector2(u, vMax));
        }

        for (int i = 0; i < segs; i++)
        {
            int b0 = i * 2;
            int t0 = b0 + 1;
            int b1 = b0 + 2;
            int t1 = b0 + 3;

            sideTris.Add(b0);
            sideTris.Add(t0);
            sideTris.Add(t1);

            sideTris.Add(b0);
            sideTris.Add(t1);
            sideTris.Add(b1);
        }

        // 2. Top & Bottom Lids (Submesh 1 -> Material.004 Metallic Lid)
        int topCenterIdx = verts.Count;
        verts.Add(new Vector3(0f, halfH, 0f));
        norms.Add(Vector3.up);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs;
            float ang = t * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(ang) * radius, halfH, Mathf.Sin(ang) * radius));
            norms.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.2f, 0.5f + Mathf.Sin(ang) * 0.2f));
        }

        for (int i = 0; i < segs; i++)
        {
            capTris.Add(topCenterIdx);
            capTris.Add(topCenterIdx + 1 + i + 1);
            capTris.Add(topCenterIdx + 1 + i);
        }

        int botCenterIdx = verts.Count;
        verts.Add(new Vector3(0f, -halfH, 0f));
        norms.Add(Vector3.down);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs;
            float ang = t * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(ang) * radius, -halfH, Mathf.Sin(ang) * radius));
            norms.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.2f, 0.5f + Mathf.Sin(ang) * 0.2f));
        }

        for (int i = 0; i < segs; i++)
        {
            capTris.Add(botCenterIdx);
            capTris.Add(botCenterIdx + 1 + i);
            capTris.Add(botCenterIdx + 1 + i + 1);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(sideTris, 0);
        mesh.SetTriangles(capTris, 1);
        mesh.RecalculateBounds();

        cachedCanMesh = mesh;
        return mesh;
    }

    private void SetBoxCollidersEnabled(bool enabled)
    {
        if (boxColliders == null) boxColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < boxColliders.Length; i++)
        {
            if (boxColliders[i] != null)
            {
                boxColliders[i].enabled = enabled;
            }
        }
    }

    private void SetBoxRenderersVisible(bool visible)
    {
        if (boxRenderers == null) boxRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < boxRenderers.Length; i++)
        {
            if (boxRenderers[i] != null)
            {
                boxRenderers[i].enabled = visible;
            }
        }

        if (boxInteriorCansRoot != null)
        {
            Renderer[] interiorRends = boxInteriorCansRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < interiorRends.Length; i++)
            {
                if (interiorRends[i] != null)
                {
                    interiorRends[i].enabled = visible;
                }
            }
        }
    }

    private void SetHeldCanVisible(bool visible)
    {
        if (heldCanRenderers == null && heldCanViewmodel != null)
        {
            heldCanRenderers = heldCanViewmodel.GetComponentsInChildren<Renderer>(true);
        }
        if (heldCanRenderers == null) return;

        for (int i = 0; i < heldCanRenderers.Length; i++)
        {
            if (heldCanRenderers[i] != null)
            {
                heldCanRenderers[i].enabled = visible;
            }
        }
    }

    private void PlaySound(AudioClip clip, float pitch)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip, 0.85f);
        }
    }

    private void GenerateProceduralAudio()
    {
        int sampleRate = 44100;

        int count1 = (int)(sampleRate * 0.24f);
        float[] samples1 = new float[count1];
        float filter = 0f;
        for (int i = 0; i < count1; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Sin((t / 0.24f) * Mathf.PI);
            float white = Random.Range(-1f, 1f);
            filter += 0.18f * (white - filter);
            float thud = Mathf.Sin(2f * Mathf.PI * 115f * t) * Mathf.Exp(-t * 20f);
            samples1[i] = (filter * 0.55f + thud * 0.45f) * env * 0.8f;
        }
        boxPickupClip = AudioClip.Create("CardboardPickup", count1, 1, sampleRate, false);
        boxPickupClip.SetData(samples1, 0);

        int count2 = (int)(sampleRate * 0.26f);
        float[] samples2 = new float[count2];
        for (int i = 0; i < count2; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 18f);
            float lowThump = Mathf.Sin(2f * Mathf.PI * 92f * t) * 0.65f;
            float flap = Random.Range(-0.35f, 0.35f) * Mathf.Exp(-t * 32f);
            samples2[i] = (lowThump + flap) * env * 0.85f;
        }
        boxPlaceClip = AudioClip.Create("CardboardPlace", count2, 1, sampleRate, false);
        boxPlaceClip.SetData(samples2, 0);

        int count3 = (int)(sampleRate * 0.18f);
        float[] samples3 = new float[count3];
        for (int i = 0; i < count3; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 30f);
            float tin = Mathf.Sin(2f * Mathf.PI * 980f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 1420f * t) * 0.2f;
            float rustle = Random.Range(-0.25f, 0.25f) * Mathf.Exp(-t * 35f);
            samples3[i] = (tin + rustle) * env * 0.75f;
        }
        canPickupClip = AudioClip.Create("CanPickupFromBox", count3, 1, sampleRate, false);
        canPickupClip.SetData(samples3, 0);
    }

    private void OnDestroy()
    {
        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.OnSlotChanged -= HandleHotbarSlotChanged;
        }
    }
}

[RequireComponent(typeof(BoxCollider))]
public class BoxOriginReturnSpot : MonoBehaviour, IInteractable
{
    public RestockCardboardBox ownerBox;
    private BoxCollider triggerCollider;
    private GameObject glowIndicatorRoot;
    private Material glowMaterial;
    private Light glowPointLight;

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(0.75f, 0.65f, 0.75f);
        triggerCollider.center = new Vector3(0f, 0.32f, 0f);

        BuildGlowIndicator();
    }

    private void BuildGlowIndicator()
    {
        if (glowIndicatorRoot != null) return;

        glowIndicatorRoot = new GameObject("OriginReturn_Glow_Indicator");
        glowIndicatorRoot.transform.SetParent(transform, false);
        glowIndicatorRoot.transform.localPosition = Vector3.zero;
        glowIndicatorRoot.transform.localRotation = Quaternion.identity;
        glowIndicatorRoot.transform.localScale = Vector3.one;

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        glowMaterial = new Material(sh);
        glowMaterial.name = "BoxOriginReturnSpot_Glow_MAT";
        Color initCol = new Color(1.0f, 0.85f, 0.32f, 0.85f);
        glowMaterial.color = initCol;
        if (glowMaterial.HasProperty("_BaseColor")) glowMaterial.SetColor("_BaseColor", initCol);

        float w = 0.50f;
        float d = 0.42f;
        float h = 0.32f;
        float bar = 0.022f;

        CreateGlowBar("BasePad", new Vector3(0f, 0.012f, 0f), new Vector3(w, 0.012f, d));
        CreateGlowBar("BotFront", new Vector3(0f, bar * 0.5f, -d * 0.5f), new Vector3(w, bar, bar));
        CreateGlowBar("BotBack", new Vector3(0f, bar * 0.5f, d * 0.5f), new Vector3(w, bar, bar));
        CreateGlowBar("BotLeft", new Vector3(-w * 0.5f, bar * 0.5f, 0f), new Vector3(bar, bar, d));
        CreateGlowBar("BotRight", new Vector3(w * 0.5f, bar * 0.5f, 0f), new Vector3(bar, bar, d));
        CreateGlowBar("TopFront", new Vector3(0f, h, -d * 0.5f), new Vector3(w, bar, bar));
        CreateGlowBar("TopBack", new Vector3(0f, h, d * 0.5f), new Vector3(w, bar, bar));
        CreateGlowBar("TopLeft", new Vector3(-w * 0.5f, h, 0f), new Vector3(bar, bar, d));
        CreateGlowBar("TopRight", new Vector3(w * 0.5f, h, 0f), new Vector3(bar, bar, d));
        CreateGlowBar("CornerFL", new Vector3(-w * 0.5f, h * 0.5f, -d * 0.5f), new Vector3(bar, h, bar));
        CreateGlowBar("CornerFR", new Vector3(w * 0.5f, h * 0.5f, -d * 0.5f), new Vector3(bar, h, bar));
        CreateGlowBar("CornerBL", new Vector3(-w * 0.5f, h * 0.5f, d * 0.5f), new Vector3(bar, h, bar));
        CreateGlowBar("CornerBR", new Vector3(w * 0.5f, h * 0.5f, d * 0.5f), new Vector3(bar, h, bar));

        GameObject lightGo = new GameObject("OriginReturn_Glow_Light");
        lightGo.transform.SetParent(glowIndicatorRoot.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        glowPointLight = lightGo.AddComponent<Light>();
        glowPointLight.type = LightType.Point;
        glowPointLight.color = new Color(1.0f, 0.84f, 0.35f);
        glowPointLight.range = 1.65f;
        glowPointLight.intensity = 1.15f;
        glowPointLight.shadows = LightShadows.None;

        glowIndicatorRoot.SetActive(false);
    }

    private void CreateGlowBar(string barName, Vector3 localPos, Vector3 localScale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = barName;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        go.transform.SetParent(glowIndicatorRoot.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;

        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial = glowMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    private void Update()
    {
        if (glowIndicatorRoot == null) return;
        if (ownerBox == null) ownerBox = RestockCardboardBox.Instance;

        bool shouldGlow = NightShiftStoryController.IsStockingTaskActive &&
                          ownerBox != null &&
                          ownerBox.IsStockingFinished &&
                          !ownerBox.IsAtOrigin;

        if (glowIndicatorRoot.activeSelf != shouldGlow)
        {
            glowIndicatorRoot.SetActive(shouldGlow);
        }

        if (shouldGlow)
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4.2f);
            Color c = Color.Lerp(new Color(0.95f, 0.72f, 0.18f, 0.75f), new Color(1.0f, 0.95f, 0.52f, 1.0f), pulse);
            if (glowMaterial != null)
            {
                glowMaterial.color = c;
                if (glowMaterial.HasProperty("_BaseColor")) glowMaterial.SetColor("_BaseColor", c);
            }
            if (glowPointLight != null)
            {
                glowPointLight.intensity = Mathf.Lerp(0.65f, 1.55f, pulse);
            }
        }
    }

    public string GetPromptText()
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > 1.75f)
        {
            return string.Empty;
        }

        if (ownerBox == null) ownerBox = RestockCardboardBox.Instance;

        // Only allow putting the box back at its origin spot AFTER all 12 cans are stocked!
        if (ownerBox != null && ownerBox.IsInInventory && ownerBox.IsStockingFinished)
        {
            return "E to put back box";
        }
        return string.Empty;
    }

    public void Interact()
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > 1.75f)
        {
            return;
        }

        if (ownerBox == null) ownerBox = RestockCardboardBox.Instance;
        if (ownerBox != null && ownerBox.IsInInventory && ownerBox.IsStockingFinished)
        {
            ownerBox.PutBackAtOrigin();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.85f, 0.55f, 0.45f);
        Gizmos.DrawWireCube(new Vector3(0f, 0.2f, 0f), new Vector3(0.55f, 0.4f, 0.5f));
    }
}

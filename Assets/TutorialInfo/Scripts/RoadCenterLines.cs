using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[SelectionBase]
public class RoadCenterLines : MonoBehaviour
{
    public enum LineStyle
    {
        DashedWhite,
        SolidWhite,
        DashedYellow,
        DoubleSolidYellow,
        DoubleDashedYellow
    }

    [Header("Line Style & Dimensions")]
    [Tooltip("Choose line pattern (Single dashed, double solid, white/yellow)")]
    public LineStyle style = LineStyle.DashedWhite;

    [Tooltip("Width of each painted stripe in meters (standard is 0.12m - 0.20m)")]
    [Range(0.05f, 0.6f)]
    public float lineWidth = 0.20f;

    [Tooltip("Length of each dashed segment in meters")]
    [Range(0.5f, 15f)]
    public float dashLength = 3.0f;

    [Tooltip("Gap between dashed segments in meters")]
    [Range(0.5f, 15f)]
    public float dashGap = 3.5f;

    [Tooltip("Spacing between double lines in meters")]
    [Range(0.05f, 0.8f)]
    public float doubleLineSpacing = 0.22f;

    [Tooltip("Height offset above road surface in meters to prevent Z-fighting")]
    [Range(0.005f, 0.1f)]
    public float heightOffset = 0.03f;

    [Header("Colors & Custom Material")]
    public Color whiteColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
    public Color yellowColor = new Color(1.0f, 0.82f, 0.12f, 1.0f);
    public Material customMaterial = null;

    [HideInInspector]
    [SerializeField]
    private GameObject linesChild;

    private Mesh linesMesh;
    private Vector3 lastLossyScale;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private LineStyle lastStyle;
    private float lastLineWidth;
    private float lastDashLength;
    private float lastDashGap;
    private float lastHeightOffset;

    private const string MaterialPathWhite = "Assets/small-red-gas-station/Materials/Road_Stripe_White.mat";
    private const string MaterialPathYellow = "Assets/small-red-gas-station/Materials/Road_Stripe_Yellow.mat";

    private void OnEnable()
    {
        RebuildLines();
    }

    private void OnValidate()
    {
        RebuildLines();
    }

    private void Update()
    {
        if (transform.hasChanged ||
            transform.lossyScale != lastLossyScale ||
            transform.position != lastPosition ||
            transform.rotation != lastRotation ||
            style != lastStyle ||
            lineWidth != lastLineWidth ||
            dashLength != lastDashLength ||
            dashGap != lastDashGap ||
            heightOffset != lastHeightOffset)
        {
            transform.hasChanged = false;
            lastLossyScale = transform.lossyScale;
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            lastStyle = style;
            lastLineWidth = lineWidth;
            lastDashLength = dashLength;
            lastDashGap = dashGap;
            lastHeightOffset = heightOffset;

            RebuildLines();
        }
    }

    [ContextMenu("Rebuild Center Lines Now")]
    public void RebuildLines()
    {
        MeshFilter roadMf = GetComponent<MeshFilter>();
        if (roadMf == null || roadMf.sharedMesh == null) return;

        Mesh roadMesh = roadMf.sharedMesh;
        Bounds localBounds = roadMesh.bounds;

        EnsureChildObject();
        if (linesChild == null) return;

        // 1. Identify which local axis is the Road's surface Normal (pointing towards the sky in world space)
        Vector3 worldAxisX = transform.TransformDirection(Vector3.right).normalized;
        Vector3 worldAxisY = transform.TransformDirection(Vector3.up).normalized;
        Vector3 worldAxisZ = transform.TransformDirection(Vector3.forward).normalized;

        float dotX = Mathf.Abs(Vector3.Dot(worldAxisX, Vector3.up));
        float dotY = Mathf.Abs(Vector3.Dot(worldAxisY, Vector3.up));
        float dotZ = Mathf.Abs(Vector3.Dot(worldAxisZ, Vector3.up));

        Vector3 localNormalAxis;
        Vector3 localAxisA;
        Vector3 localAxisB;
        float localSizeA;
        float localSizeB;

        if (dotZ >= dotX && dotZ >= dotY)
        {
            // Local Z is facing up in world space
            localNormalAxis = Vector3.forward;
            localAxisA = Vector3.up;
            localSizeA = localBounds.size.y;
            localAxisB = Vector3.right;
            localSizeB = localBounds.size.x;
        }
        else if (dotY >= dotX && dotY >= dotZ)
        {
            // Local Y is facing up in world space (standard Unity plane/cube)
            localNormalAxis = Vector3.up;
            localAxisA = Vector3.forward;
            localSizeA = localBounds.size.z;
            localAxisB = Vector3.right;
            localSizeB = localBounds.size.x;
        }
        else
        {
            // Local X is facing up in world space
            localNormalAxis = Vector3.right;
            localAxisA = Vector3.forward;
            localSizeA = localBounds.size.z;
            localAxisB = Vector3.up;
            localSizeB = localBounds.size.y;
        }

        // Ensure world surface normal points UP towards the sky
        Vector3 worldNormal = transform.TransformDirection(localNormalAxis).normalized;
        if (Vector3.Dot(worldNormal, Vector3.up) < 0f)
        {
            localNormalAxis = -localNormalAxis;
            worldNormal = -worldNormal;
        }

        // Determine which remaining axis is road length (longer in world space) vs road width
        Vector3 worldVecA = transform.TransformVector(localAxisA * localSizeA);
        Vector3 worldVecB = transform.TransformVector(localAxisB * localSizeB);
        float worldLenA = worldVecA.magnitude;
        float worldLenB = worldVecB.magnitude;

        Vector3 localLengthAxis;
        Vector3 localWidthAxis;
        float localLength;
        float localWidth;

        if (worldLenA >= worldLenB)
        {
            localLengthAxis = localAxisA;
            localLength = localSizeA;
            localWidthAxis = localAxisB;
            localWidth = localSizeB;
        }
        else
        {
            localLengthAxis = localAxisB;
            localLength = localSizeB;
            localWidthAxis = localAxisA;
            localWidth = localSizeA;
        }

        Vector3 localCenter = localBounds.center;
        // Shift local center to top face along the normal axis
        float normalExtent = 0f;
        if (localNormalAxis == Vector3.forward || localNormalAxis == -Vector3.forward)
            normalExtent = localBounds.extents.z * Mathf.Sign(Vector3.Dot(localNormalAxis, Vector3.forward));
        else if (localNormalAxis == Vector3.up || localNormalAxis == -Vector3.up)
            normalExtent = localBounds.extents.y * Mathf.Sign(Vector3.Dot(localNormalAxis, Vector3.up));
        else
            normalExtent = localBounds.extents.x * Mathf.Sign(Vector3.Dot(localNormalAxis, Vector3.right));

        Vector3 localSurfaceCenter = localCenter + localNormalAxis.normalized * Mathf.Abs(normalExtent);

        Vector3 localStart = localSurfaceCenter - localLengthAxis * (localLength * 0.5f);
        Vector3 localEnd = localSurfaceCenter + localLengthAxis * (localLength * 0.5f);

        Vector3 worldStart = transform.TransformPoint(localStart);
        Vector3 worldEnd = transform.TransformPoint(localEnd);
        Vector3 worldRoadDir = (worldEnd - worldStart).normalized;
        Vector3 worldWidthDir = Vector3.Cross(worldNormal, worldRoadDir).normalized;
        float totalWorldLength = Vector3.Distance(worldStart, worldEnd);

        if (totalWorldLength < 0.5f) return;

        // 2. Generate stripe quads
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        bool isYellow = (style == LineStyle.DashedYellow || style == LineStyle.DoubleSolidYellow || style == LineStyle.DoubleDashedYellow);
        bool isDouble = (style == LineStyle.DoubleSolidYellow || style == LineStyle.DoubleDashedYellow);
        bool isDashed = (style == LineStyle.DashedWhite || style == LineStyle.DashedYellow || style == LineStyle.DoubleDashedYellow);

        float halfW = lineWidth * 0.5f;
        float elev = Mathf.Max(0.005f, heightOffset);

        if (!isDashed)
        {
            // SOLID
            if (!isDouble)
            {
                AddStripeQuad(verts, norms, uvs, tris, worldStart, worldEnd, worldRoadDir, worldWidthDir, worldNormal, halfW, 0f, elev);
            }
            else
            {
                float offsetD = doubleLineSpacing * 0.5f;
                AddStripeQuad(verts, norms, uvs, tris, worldStart, worldEnd, worldRoadDir, worldWidthDir, worldNormal, halfW, -offsetD, elev);
                AddStripeQuad(verts, norms, uvs, tris, worldStart, worldEnd, worldRoadDir, worldWidthDir, worldNormal, halfW, offsetD, elev);
            }
        }
        else
        {
            // DASHED
            float cycle = dashLength + dashGap;
            if (cycle <= 0.1f) cycle = 1f;
            int count = Mathf.FloorToInt((totalWorldLength + dashGap) / cycle);
            if (count < 1) count = 1;

            float margin = (totalWorldLength - (count * cycle - dashGap)) * 0.5f;
            if (margin < 0) margin = 0f;

            for (int i = 0; i < count; i++)
            {
                float segStartDist = margin + i * cycle;
                float segEndDist = segStartDist + dashLength;
                if (segEndDist > totalWorldLength) segEndDist = totalWorldLength;
                if (segEndDist <= segStartDist) break;

                Vector3 p0 = worldStart + worldRoadDir * segStartDist;
                Vector3 p1 = worldStart + worldRoadDir * segEndDist;

                if (!isDouble)
                {
                    AddStripeQuad(verts, norms, uvs, tris, p0, p1, worldRoadDir, worldWidthDir, worldNormal, halfW, 0f, elev);
                }
                else
                {
                    float offsetD = doubleLineSpacing * 0.5f;
                    AddStripeQuad(verts, norms, uvs, tris, p0, p1, worldRoadDir, worldWidthDir, worldNormal, halfW, -offsetD, elev);
                    AddStripeQuad(verts, norms, uvs, tris, p0, p1, worldRoadDir, worldWidthDir, worldNormal, halfW, offsetD, elev);
                }
            }
        }

        // 3. Assign mesh
        if (linesMesh == null)
        {
            linesMesh = new Mesh();
            linesMesh.name = "Road_Center_Stripes_Mesh";
        }
        linesMesh.Clear();
        linesMesh.SetVertices(verts);
        linesMesh.SetNormals(norms);
        linesMesh.SetUVs(0, uvs);
        linesMesh.SetTriangles(tris, 0);
        linesMesh.RecalculateBounds();

        MeshFilter mf = linesChild.GetComponent<MeshFilter>();
        if (mf == null) mf = linesChild.AddComponent<MeshFilter>();
        mf.sharedMesh = linesMesh;

        // 4. Update material
        MeshRenderer mr = linesChild.GetComponent<MeshRenderer>();
        if (mr == null) mr = linesChild.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetOrCreateStripeMaterial(isYellow);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false; // Road markings receive lighting directly, don't self-shadow

        // Ensure child transform is identity relative to parent
        linesChild.transform.localPosition = Vector3.zero;
        linesChild.transform.localRotation = Quaternion.identity;
        linesChild.transform.localScale = Vector3.one;
    }

    private void AddStripeQuad(
        List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3 worldA, Vector3 worldB,
        Vector3 roadDir, Vector3 widthDir, Vector3 normal,
        float halfW, float lateralOffset, float elevation)
    {
        Vector3 lat = widthDir * lateralOffset + normal * elevation;
        Vector3 wHalf = widthDir * halfW;

        Vector3 pA = worldA + lat;
        Vector3 pB = worldB + lat;

        // In child's local coordinates (Road's local coordinates)
        Vector3 v0 = transform.InverseTransformPoint(pA - wHalf); // Start-Left
        Vector3 v1 = transform.InverseTransformPoint(pA + wHalf); // Start-Right
        Vector3 v2 = transform.InverseTransformPoint(pB - wHalf); // End-Left
        Vector3 v3 = transform.InverseTransformPoint(pB + wHalf); // End-Right

        // Surface normal in child's local space pointing directly up toward sky
        Vector3 localNorm = transform.InverseTransformDirection(normal).normalized;

        int baseIdx = verts.Count;
        verts.Add(v0);
        verts.Add(v1);
        verts.Add(v2);
        verts.Add(v3);

        norms.Add(localNorm);
        norms.Add(localNorm);
        norms.Add(localNorm);
        norms.Add(localNorm);

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(0f, 1f));
        uvs.Add(new Vector2(1f, 1f));

        // Front face (Clockwise from top looking down)
        tris.Add(baseIdx + 0);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx + 3);

        tris.Add(baseIdx + 0);
        tris.Add(baseIdx + 3);
        tris.Add(baseIdx + 1);
    }

    private void EnsureChildObject()
    {
        if (linesChild != null) return;

        Transform existing = transform.Find("Road_Center_Lines");
        if (existing != null)
        {
            linesChild = existing.gameObject;
            return;
        }

        linesChild = new GameObject("Road_Center_Lines");
        linesChild.transform.SetParent(transform, false);
        linesChild.transform.localPosition = Vector3.zero;
        linesChild.transform.localRotation = Quaternion.identity;
        linesChild.transform.localScale = Vector3.one;
    }

    private Material GetOrCreateStripeMaterial(bool isYellow)
    {
        if (customMaterial != null) return customMaterial;

#if UNITY_EDITOR
        string path = isYellow ? MaterialPathYellow : MaterialPathWhite;
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Color col = isYellow ? yellowColor : whiteColor;

        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = isYellow ? "Road_Stripe_Yellow" : "Road_Stripe_White";
            mat.SetColor("_BaseColor", col);
            mat.SetColor("_Color", col);
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_Cull", 0f); // Two-Sided
            mat.enableInstancing = true;

            // Subtle emission for clear retroreflective road paint visibility
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", col * 0.75f);

            string folder = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.SetColor("_BaseColor", col);
            mat.SetColor("_Color", col);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", col * 0.75f);
            EditorUtility.SetDirty(mat);
        }
        return mat;
#else
        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material m = new Material(s);
        Color c = isYellow ? yellowColor : whiteColor;
        m.color = c;
        return m;
#endif
    }
}

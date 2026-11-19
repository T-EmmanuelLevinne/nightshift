using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class UserBoxPutter : MonoBehaviour, IInteractable
{
    public static UserBoxPutter Instance { get; private set; }

    [Header("Putter Settings")]
    [SerializeField] private string putBoxPrompt = "E to put box";
    [SerializeField] private Vector3 triggerSize = new Vector3(0.75f, 0.65f, 0.75f);

    private BoxCollider boxCollider;
    private GameObject glowIndicatorRoot;
    private Material glowMaterial;
    private Light glowPointLight;
    private readonly System.Collections.Generic.List<Renderer> glowRenderers = new System.Collections.Generic.List<Renderer>();

    private void Awake()
    {
        Instance = this;

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.enabled = false;
        }

        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
        }
        boxCollider.isTrigger = true;
        boxCollider.size = triggerSize;
        boxCollider.center = new Vector3(0f, triggerSize.y * 0.5f, 0f);

        BuildGlowIndicator();
    }

    private void BuildGlowIndicator()
    {
        if (glowIndicatorRoot != null) return;

        glowIndicatorRoot = new GameObject("Putter_Glow_Indicator");
        glowIndicatorRoot.transform.SetParent(transform, false);
        glowIndicatorRoot.transform.localPosition = Vector3.zero;
        glowIndicatorRoot.transform.localRotation = Quaternion.identity;
        glowIndicatorRoot.transform.localScale = Vector3.one;

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        glowMaterial = new Material(sh);
        glowMaterial.name = "UserBoxPutter_Glow_MAT";
        Color initCol = new Color(1.0f, 0.85f, 0.32f, 0.85f);
        glowMaterial.color = initCol;
        if (glowMaterial.HasProperty("_BaseColor")) glowMaterial.SetColor("_BaseColor", initCol);

        float w = 0.50f;
        float d = 0.42f;
        float h = 0.32f;
        float bar = 0.022f;

        // Glowing base pad
        CreateGlowBar("BasePad", new Vector3(0f, 0.012f, 0f), new Vector3(w, 0.012f, d));

        // Bottom 4 edges
        CreateGlowBar("BotFront", new Vector3(0f, bar * 0.5f, -d * 0.5f), new Vector3(w, bar, bar));
        CreateGlowBar("BotBack", new Vector3(0f, bar * 0.5f, d * 0.5f), new Vector3(w, bar, bar));
        CreateGlowBar("BotLeft", new Vector3(-w * 0.5f, bar * 0.5f, 0f), new Vector3(bar, bar, d));
        CreateGlowBar("BotRight", new Vector3(w * 0.5f, bar * 0.5f, 0f), new Vector3(bar, bar, d));

        // Top 4 edges
        CreateGlowBar("TopFront", new Vector3(0f, h, -d * 0.5f), new Vector3(w, bar, bar));
        CreateGlowBar("TopBack", new Vector3(0f, h, d * 0.5f), new Vector3(w, bar, bar));
        CreateGlowBar("TopLeft", new Vector3(-w * 0.5f, h, 0f), new Vector3(bar, bar, d));
        CreateGlowBar("TopRight", new Vector3(w * 0.5f, h, 0f), new Vector3(bar, bar, d));

        // 4 vertical corner posts
        CreateGlowBar("CornerFL", new Vector3(-w * 0.5f, h * 0.5f, -d * 0.5f), new Vector3(bar, h, bar));
        CreateGlowBar("CornerFR", new Vector3(w * 0.5f, h * 0.5f, -d * 0.5f), new Vector3(bar, h, bar));
        CreateGlowBar("CornerBL", new Vector3(-w * 0.5f, h * 0.5f, d * 0.5f), new Vector3(bar, h, bar));
        CreateGlowBar("CornerBR", new Vector3(w * 0.5f, h * 0.5f, d * 0.5f), new Vector3(bar, h, bar));

        GameObject lightGo = new GameObject("Putter_Glow_Light");
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
            glowRenderers.Add(r);
        }
    }

    private void Update()
    {
        if (glowIndicatorRoot == null) return;

        RestockCardboardBox box = RestockCardboardBox.Instance;
        if (box == null) box = FindAnyObjectByType<RestockCardboardBox>();

        bool shouldGlow = NightShiftStoryController.IsStockingTaskActive &&
                          (box == null || (!box.IsPlacedOnPutter && !box.IsStockingFinished));

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

    public bool IsPlayerNearPutter(float maxDist = 1.45f)
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        Vector3 pPos = cam.transform.position;
        float hDist = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(transform.position.x, transform.position.z));
        float vDist = Mathf.Abs(pPos.y - transform.position.y);
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

        RestockCardboardBox box = RestockCardboardBox.Instance;
        if (box == null) box = FindAnyObjectByType<RestockCardboardBox>();

        if (box != null && box.IsInInventory && !box.IsPlacedOnPutter && !box.IsStockingFinished)
        {
            return putBoxPrompt;
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

        RestockCardboardBox box = RestockCardboardBox.Instance;
        if (box == null) box = FindAnyObjectByType<RestockCardboardBox>();

        if (box != null && box.IsInInventory && !box.IsPlacedOnPutter && !box.IsStockingFinished)
        {
            box.PlaceOnPutter(this);
        }
    }

    private void OnDrawGizmos()
    {
        // Subtle wireframe gizmo in Scene View so user can easily see and move the invisible UserBoxPutter
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.95f, 0.78f, 0.25f, 0.35f);
        Vector3 center = new Vector3(0f, triggerSize.y * 0.35f, 0f);
        Vector3 size = new Vector3(0.50f, triggerSize.y * 0.7f, 0.45f);
        Gizmos.DrawWireCube(center, size);

        // Floor footprint pad
        Gizmos.color = new Color(0.95f, 0.78f, 0.25f, 0.65f);
        Gizmos.DrawWireCube(new Vector3(0f, 0.01f, 0f), new Vector3(0.55f, 0.02f, 0.50f));
    }
}

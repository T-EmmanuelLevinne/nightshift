using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class HorrorHotbar : MonoBehaviour
{
    public static HorrorHotbar Instance { get; private set; }

    [Header("Hotbar Configuration")]
    public int slotCount = 3;
    public int activeSlotIndex = 0;
    public bool isVisible = true; // Always visible so player can select slots anytime

    [Header("Slot Dimensions")]
    public float slotWidth = 66f;
    public float slotHeight = 66f;
    public float slotSpacing = 10f;
    public float bottomMargin = 22f;

    [Header("Retro Horror Colors (Subtle & Muted - No Oversaturated Yellow)")]
    public Color slotBgColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);
    public Color slotScanlineColor = new Color(0.03f, 0.03f, 0.03f, 0.90f);
    public Color normalBorderColor = new Color(0.25f, 0.25f, 0.25f, 0.65f); 
    public Color activeBorderColor = new Color(0.85f, 0.85f, 0.82f, 0.95f); 
    public Color activeGlowColor = new Color(1.0f, 1.0f, 1.0f, 0.04f);  
    public Color textColor = new Color(0.50f, 0.50f, 0.50f, 0.70f);        
    public Color textActiveColor = new Color(0.92f, 0.92f, 0.88f, 1.0f);   
    public Color textShadowColor = new Color(0.02f, 0.02f, 0.02f, 0.95f);

    [Header("Slot Data")]
    private string[] slotNames;
    private Texture2D[] slotIcons;

    private Texture2D pixelTexture;
    private Texture2D scanlineTexture;
    private Texture2D defaultMopIcon;
    private Texture2D defaultBoxIcon;
    private Texture2D defaultCanIcon;
    private Texture2D defaultTrashIcon;
    private Texture2D defaultEggsIcon;
    private Texture2D defaultBaconIcon;
    private Texture2D defaultFryingPanIcon;
    private Texture2D defaultSingleEggIcon;
    private Texture2D defaultTwoBaconsIcon;
    private Texture2D defaultPlateIcon;
    private Texture2D defaultPlatedFoodIcon;
    private Texture2D defaultForkIcon;
    private Texture2D defaultRemoteIcon;

    private Font retroFont;
    private GUIStyle slotNumberStyle;
    private GUIStyle slotNumberShadowStyle;
    private GUIStyle itemLabelStyle;
    private GUIStyle itemLabelShadowStyle;

    public event Action<int> OnSlotChanged;

    public static HorrorHotbar EnsureExists()
    {
        if (Instance != null) return Instance;

        HorrorHotbar existing = FindAnyObjectByType<HorrorHotbar>();
        if (existing != null)
        {
            Instance = existing;
            existing.isVisible = true;
            return Instance;
        }

        GameObject go = new GameObject("Horror_Hotbar");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<HorrorHotbar>();
        Instance.isVisible = true;
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            // Never destroy the whole GameObject when HorrorHotbar is a component on FPS_Player;
            // destroying gameObject here would wipe out Test_FPS_Player, PlayerCamera, and AudioListener.
            bool isAttachedToSharedObject =
                GetComponent<SimpleFPSController>() != null ||
                GetComponent<CharacterController>() != null ||
                transform.childCount > 0 ||
                GetComponents<Component>().Length > 2;

            if (isAttachedToSharedObject)
            {
                Destroy(this);
            }
            else
            {
                Destroy(gameObject);
            }
            return;
        }

        // Enforce visibility and muted palette in code (bypasses any stale scene overrides)
        isVisible = true;
        activeBorderColor = new Color(0.85f, 0.85f, 0.82f, 0.95f);
        activeGlowColor = new Color(1.0f, 1.0f, 1.0f, 0.04f);
        textActiveColor = new Color(0.92f, 0.92f, 0.88f, 1.0f);
        normalBorderColor = new Color(0.25f, 0.25f, 0.25f, 0.65f);

        slotNames = new string[slotCount];
        slotIcons = new Texture2D[slotCount];

        InitTextures();
        InitFont();
    }

    private void Start()
    {
        isVisible = true;

        // Check if mop is already equipped in scene
        EquippableMop mop = FindAnyObjectByType<EquippableMop>();
        if (mop != null && mop.IsEquipped)
        {
            SetSlot(0, "MOP", null);
        }
    }

    private void InitTextures()
    {
        if (pixelTexture == null)
        {
            pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixelTexture.SetPixel(0, 0, Color.white);
            pixelTexture.Apply();
        }

        if (scanlineTexture == null)
        {
            int size = 64;
            scanlineTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            scanlineTexture.filterMode = FilterMode.Point;
            scanlineTexture.wrapMode = TextureWrapMode.Repeat;

            for (int y = 0; y < size; y++)
            {
                Color col = (y % 2 == 0) ? slotScanlineColor : slotBgColor;
                for (int x = 0; x < size; x++)
                {
                    float noise = UnityEngine.Random.Range(-0.02f, 0.02f);
                    Color noisyCol = new Color(
                        Mathf.Clamp01(col.r + noise),
                        Mathf.Clamp01(col.g + noise),
                        Mathf.Clamp01(col.b + noise),
                        col.a
                    );
                    scanlineTexture.SetPixel(x, y, noisyCol);
                }
            }
            scanlineTexture.Apply();
        }

        if (defaultMopIcon == null)
        {
            defaultMopIcon = GenerateProceduralMopIcon();
        }
        if (defaultBoxIcon == null)
        {
            defaultBoxIcon = GenerateProceduralBoxIcon();
        }
        if (defaultCanIcon == null)
        {
            defaultCanIcon = GenerateProceduralCanIcon();
        }
        if (defaultTrashIcon == null)
        {
            defaultTrashIcon = GenerateProceduralTrashIcon();
        }
        if (defaultEggsIcon == null)
        {
            defaultEggsIcon = GenerateProceduralEggsIcon();
        }
        if (defaultBaconIcon == null)
        {
            defaultBaconIcon = GenerateProceduralBaconIcon();
        }
        if (defaultFryingPanIcon == null)
        {
            defaultFryingPanIcon = GenerateProceduralFryingPanIcon();
        }
        if (defaultSingleEggIcon == null)
        {
            defaultSingleEggIcon = GenerateProceduralSingleEggIcon();
        }
        if (defaultTwoBaconsIcon == null)
        {
            defaultTwoBaconsIcon = GenerateProceduralTwoBaconsIcon();
        }
        if (defaultPlateIcon == null)
        {
            defaultPlateIcon = GenerateProceduralPlateIcon(false);
        }
        if (defaultPlatedFoodIcon == null)
        {
            defaultPlatedFoodIcon = GenerateProceduralPlateIcon(true);
        }
        if (defaultForkIcon == null)
        {
            defaultForkIcon = GenerateProceduralForkIcon();
        }
        if (defaultRemoteIcon == null)
        {
            defaultRemoteIcon = GenerateProceduralRemoteIcon();
        }
    }

    private Texture2D GenerateProceduralTrashIcon()
    {
        int size = 32;
        Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.filterMode = FilterMode.Point;
        icon.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                icon.SetPixel(x, y, Color.clear);

        Color bagBase = new Color(0.18f, 0.20f, 0.22f, 1f);
        Color bagHighlight = new Color(0.32f, 0.35f, 0.38f, 1f);
        Color bagShadow = new Color(0.10f, 0.11f, 0.12f, 1f);
        Color tieCord = new Color(0.68f, 0.22f, 0.18f, 1f);

        // Rounded bulging trashbag body (y: 4..21)
        for (int y = 4; y <= 21; y++)
        {
            float v = (y - 4f) / 17f;
            int halfW = Mathf.RoundToInt(Mathf.Lerp(8f, 4f, v * v) + Mathf.Sin(v * Mathf.PI) * 2.5f);
            for (int x = 16 - halfW; x <= 16 + halfW; x++)
            {
                bool isBorder = (x == 16 - halfW || x == 16 + halfW || y == 4);
                if (isBorder) icon.SetPixel(x, y, bagShadow);
                else if (x < 14 && (x + y) % 3 == 0) icon.SetPixel(x, y, bagHighlight);
                else icon.SetPixel(x, y, bagBase);
            }
        }

        // Red tie-off neck (y: 22..23)
        for (int x = 13; x <= 19; x++)
        {
            icon.SetPixel(x, 22, tieCord);
            icon.SetPixel(x, 23, tieCord);
        }

        // Flared top knot ears (y: 24..27)
        for (int y = 24; y <= 27; y++)
        {
            int spread = y - 22;
            for (int x = 15 - spread; x <= 17 + spread; x++)
            {
                icon.SetPixel(x, y, (x % 2 == 0) ? bagBase : bagHighlight);
            }
        }

        icon.Apply();
        return icon;
    }

    private Texture2D GenerateProceduralBoxIcon()
    {
        int size = 32;
        Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.filterMode = FilterMode.Point;
        icon.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                icon.SetPixel(x, y, Color.clear);

        Color boxMain = new Color(0.68f, 0.50f, 0.32f, 1f);
        Color boxDark = new Color(0.46f, 0.33f, 0.20f, 1f);
        Color boxLight = new Color(0.79f, 0.62f, 0.42f, 1f);
        Color boxBorder = new Color(0.24f, 0.16f, 0.09f, 1f);
        Color tapeCol = new Color(0.84f, 0.76f, 0.58f, 1f);

        // Main box body (x: 5..26, y: 5..23)
        for (int y = 5; y <= 23; y++)
        {
            for (int x = 5; x <= 26; x++)
            {
                bool border = (x == 5 || x == 26 || y == 5 || y == 23 || y == 18);
                if (border)
                    icon.SetPixel(x, y, boxBorder);
                else if (y > 18)
                    icon.SetPixel(x, y, boxLight);
                else if (x > 16)
                    icon.SetPixel(x, y, boxDark);
                else
                    icon.SetPixel(x, y, boxMain);
            }
        }

        // Packing tape strip down center top
        for (int y = 14; y <= 22; y++)
        {
            icon.SetPixel(15, y, tapeCol);
            icon.SetPixel(16, y, tapeCol);
        }

        icon.Apply();
        return icon;
    }

    private Texture2D GenerateProceduralCanIcon()
    {
        int size = 32;
        Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.filterMode = FilterMode.Point;
        icon.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                icon.SetPixel(x, y, Color.clear);

        Color rimSilver = new Color(0.78f, 0.80f, 0.82f, 1f);
        Color labelRed = new Color(0.74f, 0.20f, 0.18f, 1f);
        Color labelCream = new Color(0.88f, 0.84f, 0.74f, 1f);
        Color goldBand = new Color(0.82f, 0.66f, 0.24f, 1f);
        Color borderCol = new Color(0.18f, 0.16f, 0.15f, 1f);

        for (int y = 5; y <= 26; y++)
        {
            for (int x = 9; x <= 22; x++)
            {
                bool border = (x == 9 || x == 22 || y == 5 || y == 26);
                if (border)
                    icon.SetPixel(x, y, borderCol);
                else if (y == 6 || y == 25 || y == 24)
                    icon.SetPixel(x, y, rimSilver);
                else if (y == 15)
                    icon.SetPixel(x, y, goldBand);
                else if (y > 15)
                    icon.SetPixel(x, y, labelRed);
                else
                    icon.SetPixel(x, y, labelCream);
            }
        }

        icon.Apply();
        return icon;
    }

    private Texture2D GenerateProceduralMopIcon()
    {
        int size = 32;
        Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.filterMode = FilterMode.Point;
        icon.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                icon.SetPixel(x, y, Color.clear);
            }
        }

        // Muted realistic tones (no bright neon yellow)
        Color handleWood = new Color(0.70f, 0.52f, 0.35f, 1f);
        Color handleHighlight = new Color(0.82f, 0.65f, 0.45f, 1f);
        Color handleShadow = new Color(0.48f, 0.32f, 0.20f, 1f);
        Color clampRed = new Color(0.65f, 0.22f, 0.20f, 1f);
        Color clampDark = new Color(0.45f, 0.12f, 0.12f, 1f);
        Color yarnMain = new Color(0.85f, 0.83f, 0.75f, 1f); // Pale linen / off-white
        Color yarnShade = new Color(0.68f, 0.66f, 0.58f, 1f);
        Color yarnTip = new Color(0.55f, 0.53f, 0.46f, 1f);

        // 1. Diagonal Wooden Handle
        for (int i = 0; i <= 14; i++)
        {
            int x = 25 - i * 11 / 14;
            int y = 28 - i * 13 / 14;

            icon.SetPixel(x, y, handleWood);
            icon.SetPixel(x + 1, y, handleHighlight);
            icon.SetPixel(x - 1, y, handleShadow);
        }

        // Top loop
        icon.SetPixel(25, 29, clampDark);
        icon.SetPixel(26, 29, clampDark);
        icon.SetPixel(25, 30, clampRed);
        icon.SetPixel(26, 30, clampRed);

        // 2. Plastic Clamp
        for (int cy = 13; cy <= 15; cy++)
        {
            for (int cx = 11; cx <= 16; cx++)
            {
                bool isBorder = (cy == 13 || cy == 15 || cx == 11 || cx == 16);
                icon.SetPixel(cx, cy, isBorder ? clampDark : clampRed);
            }
        }

        // 3. Hanging Yarn / Cloth Head
        for (int y = 3; y <= 12; y++)
        {
            int halfWidth = (13 - y) * 5 / 10 + 3;
            int startX = 14 - halfWidth;
            int endX = 14 + halfWidth;

            for (int x = startX; x <= endX; x++)
            {
                if (y <= 4)
                {
                    if ((x + y) % 2 == 0) icon.SetPixel(x, y, yarnTip);
                }
                else
                {
                    bool isStrand = ((x - startX) % 2 == 0);
                    icon.SetPixel(x, y, isStrand ? yarnMain : yarnShade);
                }
            }
        }

        icon.Apply();
        return icon;
    }

    private Texture2D GenerateProceduralEggsIcon()
    {
        int size = 32;
        Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.filterMode = FilterMode.Point;
        icon.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                icon.SetPixel(x, y, Color.clear);

        Color cartonBase = new Color(0.62f, 0.52f, 0.40f, 1f);
        Color cartonBorder = new Color(0.40f, 0.32f, 0.22f, 1f);
        Color cartonHighlight = new Color(0.75f, 0.65f, 0.52f, 1f);

        Color eggBrown = new Color(0.88f, 0.74f, 0.58f, 1f);
        Color eggHighlight = new Color(0.97f, 0.88f, 0.75f, 1f);
        Color eggShadow = new Color(0.70f, 0.55f, 0.40f, 1f);

        // Molded carton tray base (y: 6..14, x: 4..27)
        for (int y = 6; y <= 14; y++)
        {
            for (int x = 4; x <= 27; x++)
            {
                bool isBorder = (y == 6 || x == 4 || x == 27);
                if (isBorder) icon.SetPixel(x, y, cartonBorder);
                else if (y == 14) icon.SetPixel(x, y, cartonHighlight);
                else icon.SetPixel(x, y, cartonBase);
            }
        }

        // 4 visible eggs sitting in front row (centers at x = 7, 12, 18, 23, y = 14)
        int[] eggCenters = new int[] { 7, 12, 18, 23 };
        for (int e = 0; e < eggCenters.Length; e++)
        {
            int cx = eggCenters[e];
            int cy = 15;

            for (int dy = -2; dy <= 7; dy++)
            {
                int halfW = (dy <= 2) ? 2 : ((dy <= 5) ? 2 : 1);
                for (int dx = -halfW; dx <= halfW; dx++)
                {
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px >= 0 && px < size && py >= 0 && py < size)
                    {
                        if (dx == -halfW || dx == halfW || dy == 7) icon.SetPixel(px, py, eggShadow);
                        else if (dx == 0 && dy >= 3 && dy <= 5) icon.SetPixel(px, py, eggHighlight);
                        else icon.SetPixel(px, py, eggBrown);
                    }
                }
            }
        }

        icon.Apply();
        return icon;
    }

    private Texture2D GenerateProceduralBaconIcon()
    {
        int size = 32;
        Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.filterMode = FilterMode.Point;
        icon.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                icon.SetPixel(x, y, Color.clear);

        Color packBorder = new Color(0.12f, 0.12f, 0.12f, 1f);
        Color packHeader = new Color(0.20f, 0.22f, 0.25f, 1f);
        Color packGold = new Color(0.85f, 0.70f, 0.25f, 1f);
        Color plasticBg = new Color(0.18f, 0.15f, 0.15f, 0.9f);

        Color meatRed = new Color(0.72f, 0.18f, 0.18f, 1f);
        Color meatDark = new Color(0.50f, 0.12f, 0.12f, 1f);
        Color fatWhite = new Color(0.92f, 0.88f, 0.82f, 1f);

        // Rectangular pack body (x: 4..27, y: 5..26)
        for (int y = 5; y <= 26; y++)
        {
            for (int x = 4; x <= 27; x++)
            {
                bool isBorder = (x == 4 || x == 27 || y == 5 || y == 26);
                if (isBorder) icon.SetPixel(x, y, packBorder);
                else if (y >= 22) icon.SetPixel(x, y, packHeader);
                else if (y == 21) icon.SetPixel(x, y, packGold);
                else icon.SetPixel(x, y, plasticBg);
            }
        }

        // Hanging hole punch (x: 15..16, y: 24..25)
        icon.SetPixel(15, 24, Color.clear);
        icon.SetPixel(16, 24, Color.clear);
        icon.SetPixel(15, 25, Color.clear);
        icon.SetPixel(16, 25, Color.clear);

        // 3 wavy bacon strips inside window (y: 7..19)
        for (int strip = 0; strip < 3; strip++)
        {
            int baseLevel = 7 + strip * 4;
            for (int x = 6; x <= 25; x++)
            {
                int wave = (int)(Mathf.Sin((x + strip * 3) * 0.5f) * 1.2f);
                int yPos = baseLevel + wave;

                if (yPos >= 6 && yPos <= 20)
                {
                    icon.SetPixel(x, yPos, meatRed);
                    icon.SetPixel(x, yPos + 1, fatWhite);
                    icon.SetPixel(x, yPos + 2, meatDark);
                }
            }
        }

        icon.Apply();
        return icon;
    }

    private Texture2D GenerateProceduralFryingPanIcon()
    {
        int size = 32;
        Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.filterMode = FilterMode.Point;
        icon.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                icon.SetPixel(x, y, Color.clear);

        Color panRim = new Color(0.32f, 0.34f, 0.37f, 1f);
        Color panFloor = new Color(0.14f, 0.15f, 0.17f, 1f);
        Color panShine = new Color(0.24f, 0.25f, 0.28f, 1f);
        Color handleCol = new Color(0.10f, 0.10f, 0.11f, 1f);
        Color ferruleCol = new Color(0.72f, 0.75f, 0.78f, 1f);

        // Circular pan body centered at (12, 13), radius 9
        for (int y = 3; y <= 23; y++)
        {
            for (int x = 2; x <= 22; x++)
            {
                float dist = Mathf.Sqrt((x - 12f) * (x - 12f) + (y - 13f) * (y - 13f));
                if (dist <= 9.2f)
                {
                    if (dist >= 7.6f) icon.SetPixel(x, y, panRim);
                    else if (x < 10 && y > 14 && dist < 6f) icon.SetPixel(x, y, panShine);
                    else icon.SetPixel(x, y, panFloor);
                }
            }
        }

        // Angled handle from (19, 19) to (29, 29)
        for (int i = 0; i <= 10; i++)
        {
            int hx = 19 + i;
            int hy = 19 + i;
            Color c = (i <= 2) ? ferruleCol : handleCol;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = hx + dx;
                    int py = hy + dy;
                    if (px >= 0 && px < size && py >= 0 && py < size)
                        icon.SetPixel(px, py, c);
                }
            }
        }

        icon.Apply();
        return icon;
    }

    private Texture2D GenerateProceduralSingleEggIcon()
    {
        int size = 32;
        Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.filterMode = FilterMode.Point;
        icon.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                icon.SetPixel(x, y, Color.clear);

        Color shellBase = new Color(0.84f, 0.67f, 0.50f, 1f);
        Color shellHigh = new Color(0.95f, 0.82f, 0.66f, 1f);
        Color shellDark = new Color(0.62f, 0.46f, 0.31f, 1f);

        for (int y = 5; y <= 26; y++)
        {
            float v = (y - 5f) / 21f;
            float normY = Mathf.Lerp(-1f, 1f, v);
            float rad = 8.2f * Mathf.Sqrt(Mathf.Max(0f, 1f - normY * normY)) * (1f - 0.16f * normY);
            int halfW = Mathf.RoundToInt(rad);
            for (int x = 16 - halfW; x <= 16 + halfW; x++)
            {
                if (x == 16 - halfW || x == 16 + halfW || y == 5 || y == 26)
                    icon.SetPixel(x, y, shellDark);
                else if (x >= 13 && x <= 15 && y >= 15 && y <= 21)
                    icon.SetPixel(x, y, shellHigh);
                else
                    icon.SetPixel(x, y, shellBase);
            }
        }

        icon.Apply();
        return icon;
    }

    private Texture2D GenerateProceduralTwoBaconsIcon()
    {
        int size = 32;
        Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.filterMode = FilterMode.Point;
        icon.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                icon.SetPixel(x, y, Color.clear);

        Color meatRed = new Color(0.74f, 0.20f, 0.18f, 1f);
        Color meatDark = new Color(0.50f, 0.12f, 0.11f, 1f);
        Color fatCream = new Color(0.95f, 0.90f, 0.84f, 1f);

        int[] stripBases = new int[] { 10, 19 };
        for (int s = 0; s < stripBases.Length; s++)
        {
            int by = stripBases[s];
            for (int x = 4; x <= 27; x++)
            {
                int wave = Mathf.RoundToInt(Mathf.Sin((x + s * 4) * 0.45f) * 1.6f);
                int y = by + wave;
                icon.SetPixel(x, y - 2, meatDark);
                icon.SetPixel(x, y - 1, meatRed);
                icon.SetPixel(x, y, fatCream);
                icon.SetPixel(x, y + 1, fatCream);
                icon.SetPixel(x, y + 2, meatRed);
                icon.SetPixel(x, y + 3, meatDark);
            }
        }

        icon.Apply();
        return icon;
    }

    private Texture2D GenerateProceduralPlateIcon(bool withFood)
    {
        int size = 32;
        Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.filterMode = FilterMode.Point;
        icon.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                icon.SetPixel(x, y, Color.clear);

        Color plateEdge = new Color(0.68f, 0.70f, 0.72f, 1f);
        Color plateRim = new Color(0.92f, 0.93f, 0.94f, 1f);
        Color plateWell = new Color(0.84f, 0.86f, 0.88f, 1f);

        for (int y = 4; y <= 27; y++)
        {
            for (int x = 4; x <= 27; x++)
            {
                float dist = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f));
                if (dist <= 11.5f)
                {
                    if (dist >= 10.2f) icon.SetPixel(x, y, plateEdge);
                    else if (dist >= 7.5f) icon.SetPixel(x, y, plateRim);
                    else icon.SetPixel(x, y, plateWell);
                }
            }
        }

        if (withFood)
        {
            Color eggWhite = new Color(0.98f, 0.97f, 0.94f, 1f);
            Color yolkCol = new Color(0.98f, 0.68f, 0.12f, 1f);
            Color baconCooked = new Color(0.56f, 0.18f, 0.12f, 1f);
            Color baconFat = new Color(0.82f, 0.62f, 0.44f, 1f);

            // Sunny side up egg on left well
            for (int y = 11; y <= 20; y++)
            {
                for (int x = 9; x <= 18; x++)
                {
                    float d = Mathf.Sqrt((x - 13.5f) * (x - 13.5f) + (y - 15.5f) * (y - 15.5f));
                    if (d <= 2.2f) icon.SetPixel(x, y, yolkCol);
                    else if (d <= 4.5f) icon.SetPixel(x, y, eggWhite);
                }
            }

            // 2 cooked bacon strips on right well
            for (int y = 10; y <= 21; y++)
            {
                int wave = Mathf.RoundToInt(Mathf.Sin(y * 0.6f) * 0.8f);
                icon.SetPixel(18 + wave, y, baconCooked);
                icon.SetPixel(19 + wave, y, baconFat);
                icon.SetPixel(21 + wave, y, baconCooked);
                icon.SetPixel(22 + wave, y, baconFat);
            }
        }

        icon.Apply();
        return icon;
    }

    private Texture2D GenerateProceduralForkIcon()
    {
        int size = 32;
        Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.filterMode = FilterMode.Point;
        icon.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                icon.SetPixel(x, y, Color.clear);

        Color steelBright = new Color(0.88f, 0.90f, 0.94f, 1f);
        Color steelDark = new Color(0.55f, 0.58f, 0.62f, 1f);

        // Diagonal fork handle from (6, 6) to (19, 19)
        for (int i = 0; i <= 13; i++)
        {
            int x = 6 + i;
            int y = 6 + i;
            icon.SetPixel(x, y, steelBright);
            icon.SetPixel(x + 1, y, steelDark);
            icon.SetPixel(x, y + 1, steelBright);
        }

        // Fork bridge & 4 tines at top right (19..26)
        for (int t = -2; t <= 2; t++)
        {
            icon.SetPixel(19 + t, 19 - t, steelBright);
        }
        for (int len = 1; len <= 6; len++)
        {
            icon.SetPixel(17 + len, 21 + len, steelBright);
            icon.SetPixel(19 + len, 19 + len, steelBright);
            icon.SetPixel(21 + len, 17 + len, steelDark);
        }

        icon.Apply();
        return icon;
    }

    private Texture2D GenerateProceduralRemoteIcon()
    {
        int size = 32;
        Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.filterMode = FilterMode.Point;
        icon.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                icon.SetPixel(x, y, Color.clear);

        Color bodyDark = new Color(0.14f, 0.15f, 0.18f, 1f);
        Color bodyEdge = new Color(0.28f, 0.30f, 0.35f, 1f);
        Color powerRed = new Color(0.84f, 0.22f, 0.20f, 1f);
        Color btnLight = new Color(0.62f, 0.68f, 0.76f, 1f);
        Color keyMuted = new Color(0.35f, 0.38f, 0.42f, 1f);

        // Centered handheld remote silhouette
        for (int y = 4; y <= 27; y++)
        {
            for (int x = 11; x <= 20; x++)
            {
                bool edge = (x == 11 || x == 20 || y == 4 || y == 27);
                icon.SetPixel(x, y, edge ? bodyEdge : bodyDark);
            }
        }

        // Red power dot (top-left)
        icon.SetPixel(13, 24, powerRed);
        icon.SetPixel(14, 24, powerRed);
        icon.SetPixel(13, 23, powerRed);

        // Left / Right rocker buttons
        icon.SetPixel(13, 20, btnLight);
        icon.SetPixel(14, 20, btnLight);
        icon.SetPixel(17, 20, btnLight);
        icon.SetPixel(18, 20, btnLight);

        // Keypad dots
        for (int ky = 8; ky <= 16; ky += 3)
        {
            for (int kx = 13; kx <= 18; kx += 2)
            {
                icon.SetPixel(kx, ky, keyMuted);
            }
        }

        icon.Apply();
        return icon;
    }

    private void InitFont()
    {
        if (retroFont == null)
        {
            string[] fontNames = new string[] { "Lucida Console", "Courier New", "Consolas", "Courier" };
            retroFont = Font.CreateDynamicFontFromOSFont(fontNames, 12);
        }
    }

    public bool HasAnyItem()
    {
        if (slotNames == null) return false;
        for (int i = 0; i < slotNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(slotNames[i])) return true;
        }
        return false;
    }

    public bool IsVisible => isVisible && HasAnyItem();

    private void Update()
    {
        // Only process hotbar slot navigation when hotbar is active with items
        if (!IsVisible) return;

        // Hotkey selection 1, 2, 3 (allows equipping 2 and 3 even empty-handed!)
        if (WasKeyPressed(KeyCode.Alpha1, KeyCode.Keypad1, 1)) SelectSlot(0);
        else if (WasKeyPressed(KeyCode.Alpha2, KeyCode.Keypad2, 2)) SelectSlot(1);
        else if (WasKeyPressed(KeyCode.Alpha3, KeyCode.Keypad3, 3)) SelectSlot(2);

        // Mouse Scroll Wheel cycling
        float scroll = GetScrollDelta();
        if (scroll > 0.05f)
        {
            // Scroll UP -> previous slot
            int prev = (activeSlotIndex - 1 + slotCount) % slotCount;
            SelectSlot(prev);
        }
        else if (scroll < -0.05f)
        {
            // Scroll DOWN -> next slot
            int next = (activeSlotIndex + 1) % slotCount;
            SelectSlot(next);
        }
    }

    private static float GetScrollDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.scroll.ReadValue().y;
        }
        return 0f;
#else
        return Input.mouseScrollDelta.y != 0 ? Input.mouseScrollDelta.y : Input.GetAxis("Mouse ScrollWheel");
#endif
    }

    private static float lastSlotKeyTime = -1f;

    private static bool WasKeyPressed(KeyCode alpha, KeyCode keypad, int num)
    {
#if ENABLE_INPUT_SYSTEM
        foreach (var device in InputSystem.devices)
        {
            if (device is Keyboard kb)
            {
                bool hit = false;
                switch (num)
                {
                    case 1:
                        hit = kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame ||
                            ((kb.digit1Key.isPressed || kb.numpad1Key.isPressed) && Time.unscaledTime - lastSlotKeyTime > 0.25f);
                        break;
                    case 2:
                        hit = kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame ||
                            ((kb.digit2Key.isPressed || kb.numpad2Key.isPressed) && Time.unscaledTime - lastSlotKeyTime > 0.25f);
                        break;
                    case 3:
                        hit = kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame ||
                            ((kb.digit3Key.isPressed || kb.numpad3Key.isPressed) && Time.unscaledTime - lastSlotKeyTime > 0.25f);
                        break;
                }

                if (hit)
                {
                    lastSlotKeyTime = Time.unscaledTime;
                    return true;
                }
            }
        }
        return false;
#else
        return Input.GetKeyDown(alpha) || Input.GetKeyDown(keypad);
#endif
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= slotCount) return;
        if (activeSlotIndex == index) return;

        activeSlotIndex = index;
        OnSlotChanged?.Invoke(activeSlotIndex);
    }

    public void SetSlot(int index, string itemName, Texture2D customIcon = null)
    {
        if (index < 0 || index >= slotCount) return;

        InitTextures();
        slotNames[index] = itemName;
        if (customIcon != null)
        {
            slotIcons[index] = customIcon;
        }
        else if (!string.IsNullOrEmpty(itemName))
        {
            if (itemName.Equals("MOP", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultMopIcon;
            else if (itemName.Equals("BOX", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultBoxIcon;
            else if (itemName.Equals("CAN", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultCanIcon;
            else if (itemName.Equals("TRASH", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultTrashIcon;
            else if (itemName.Equals("EGGS", StringComparison.OrdinalIgnoreCase) || itemName.Equals("EGG TRAY", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultEggsIcon;
            else if (itemName.Equals("BACON", StringComparison.OrdinalIgnoreCase) || itemName.Equals("BACON PACK", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultBaconIcon;
            else if (itemName.Equals("FRYING PAN", StringComparison.OrdinalIgnoreCase) || itemName.Equals("PAN", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultFryingPanIcon;
            else if (itemName.Equals("1 EGG", StringComparison.OrdinalIgnoreCase) || itemName.Equals("EGG", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultSingleEggIcon;
            else if (itemName.Equals("2 BACONS", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultTwoBaconsIcon;
            else if (itemName.Equals("PLATE", StringComparison.OrdinalIgnoreCase) || itemName.Equals("DIRTY PLATE", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultPlateIcon;
            else if (itemName.Equals("PLATED FOOD", StringComparison.OrdinalIgnoreCase) || itemName.Equals("COOKED FOOD", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultPlatedFoodIcon;
            else if (itemName.Equals("FORK", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultForkIcon;
            else if (itemName.Equals("REMOTE", StringComparison.OrdinalIgnoreCase))
                slotIcons[index] = defaultRemoteIcon;
            else
                slotIcons[index] = null;
        }
        else
        {
            slotIcons[index] = null;
        }
        OnSlotChanged?.Invoke(activeSlotIndex);
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= slotCount) return;

        slotNames[index] = null;
        slotIcons[index] = null;

        if (!HasAnyItem())
        {
            activeSlotIndex = 0;
        }

        OnSlotChanged?.Invoke(activeSlotIndex);
    }

    public void ClearAllSlots()
    {
        if (slotNames != null)
        {
            for (int i = 0; i < slotNames.Length; i++)
            {
                slotNames[i] = null;
            }
        }
        if (slotIcons != null)
        {
            for (int i = 0; i < slotIcons.Length; i++)
            {
                slotIcons[i] = null;
            }
        }

        activeSlotIndex = 0;
        OnSlotChanged?.Invoke(activeSlotIndex);
    }

    public bool HasItemNamed(string itemName)
    {
        if (string.IsNullOrEmpty(itemName) || slotNames == null) return false;
        for (int i = 0; i < slotNames.Length; i++)
        {
            if (string.Equals(slotNames[i], itemName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public int GetSlotIndexWithItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName) || slotNames == null) return -1;
        for (int i = 0; i < slotNames.Length; i++)
        {
            if (string.Equals(slotNames[i], itemName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    public void ClearItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName) || slotNames == null) return;
        bool changed = false;
        for (int i = 0; i < slotNames.Length; i++)
        {
            if (string.Equals(slotNames[i], itemName, StringComparison.OrdinalIgnoreCase))
            {
                slotNames[i] = null;
                slotIcons[i] = null;
                changed = true;
            }
        }
        if (changed)
        {
            if (!HasAnyItem()) activeSlotIndex = 0;
            OnSlotChanged?.Invoke(activeSlotIndex);
        }
    }

    public bool HasItem(int index)
    {
        if (index < 0 || index >= slotCount) return false;
        return !string.IsNullOrEmpty(slotNames[index]);
    }

    public string GetItemName(int index)
    {
        if (index < 0 || index >= slotCount) return null;
        return slotNames[index];
    }

    public void SetActiveSlot(int index)
    {
        if (index < 0 || index >= slotCount) return;
        activeSlotIndex = index;
        OnSlotChanged?.Invoke(activeSlotIndex);
    }

    public void ShowHotbar(bool show)
    {
        isVisible = show;
    }

    public bool IsSlotActive(int index)
    {
        return activeSlotIndex == index;
    }

    public bool IsActiveSlotEmpty => !HasItem(activeSlotIndex);

    public bool HasEmptySlot()
    {
        if (slotNames == null) return true;
        for (int i = 0; i < slotCount; i++)
        {
            if (string.IsNullOrEmpty(slotNames[i])) return true;
        }
        return false;
    }

    public static bool IsInventoryFull
    {
        get
        {
            if (Instance == null) return false;
            return !Instance.HasEmptySlot();
        }
    }

    private void SetupStyles()
    {
        if (slotNumberStyle == null)
        {
            slotNumberStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            if (retroFont != null) slotNumberStyle.font = retroFont;
            slotNumberStyle.normal.textColor = textColor;

            slotNumberShadowStyle = new GUIStyle(slotNumberStyle);
            slotNumberShadowStyle.normal.textColor = textShadowColor;

            itemLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
            if (retroFont != null) itemLabelStyle.font = retroFont;
            itemLabelStyle.normal.textColor = textActiveColor;

            itemLabelShadowStyle = new GUIStyle(itemLabelStyle);
            itemLabelShadowStyle.normal.textColor = textShadowColor;
        }
    }

    private void OnGUI()
    {
        if (!IsVisible ||
            InteractableCashierPC.IsPosScreenOpen ||
            NightShiftStoryController.IsIntroOverlayBlockingUI ||
            HouseSceneStoryController.IsCrosshairHidden ||
            SimpleFPSController.IsExternalCameraAnimationActive ||
            Event.current.type != EventType.Repaint) return;

        InitTextures();
        InitFont();
        SetupStyles();

        float totalWidth = slotCount * slotWidth + (slotCount - 1) * slotSpacing;
        float startX = (Screen.width - totalWidth) * 0.5f;
        float startY = Screen.height - slotHeight - bottomMargin;

        for (int i = 0; i < slotCount; i++)
        {
            float x = startX + i * (slotWidth + slotSpacing);
            Rect slotRect = new Rect(x, startY, slotWidth, slotHeight);
            bool isActive = (i == activeSlotIndex);

            DrawSlot(slotRect, i, isActive);
        }
    }

    private void DrawSlot(Rect rect, int slotIndex, bool isActive)
    {
        // 1. Draw Slot Background with Scanlines
        GUI.color = Color.white;
        GUI.DrawTexture(rect, scanlineTexture, ScaleMode.StretchToFill);

        // 2. Subtle neutral inner highlight (no bright yellow wash)
        if (isActive)
        {
            GUI.color = activeGlowColor;
            GUI.DrawTexture(rect, pixelTexture);
        }

        // 3. Draw Pixel Border (muted bone/silver for active, dark charcoal for inactive)
        Color borderColor = isActive ? activeBorderColor : normalBorderColor;
        float borderThickness = isActive ? 2f : 1.2f;
        DrawRectBorder(rect, borderColor, borderThickness);

        // Corner tick accents for active slot
        if (isActive)
        {
            GUI.color = activeBorderColor;
            float tickLen = 4f;
            // Top-left
            GUI.DrawTexture(new Rect(rect.x, rect.y, tickLen, 2f), pixelTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, tickLen), pixelTexture);
            // Top-right
            GUI.DrawTexture(new Rect(rect.xMax - tickLen, rect.y, tickLen, 2f), pixelTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, tickLen), pixelTexture);
            // Bottom-left
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, tickLen, 2f), pixelTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - tickLen, 2f, tickLen), pixelTexture);
            // Bottom-right
            GUI.DrawTexture(new Rect(rect.xMax - tickLen, rect.yMax - 2f, tickLen, 2f), pixelTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.yMax - tickLen, 2f, tickLen), pixelTexture);
        }

        // 4. Draw Slot Number [ 1 ], [ 2 ], [ 3 ] in upper left (unmistakable hotkey format, not stack count)
        string slotNumStr = $"[{slotIndex + 1}]";
        Rect numRect = new Rect(rect.x + 4f, rect.y + 3f, 26f, 16f);

        slotNumberStyle.normal.textColor = isActive ? textActiveColor : textColor;
        // Pixel drop shadow
        GUI.Label(new Rect(numRect.x + 1f, numRect.y + 1f, numRect.width, numRect.height), slotNumStr, slotNumberShadowStyle);
        GUI.Label(numRect, slotNumStr, slotNumberStyle);

        // 5. Draw Item Icon and Label if slot has an item
        Texture2D icon = slotIcons[slotIndex];
        string itemName = slotNames[slotIndex];

        if (icon != null)
        {
            float iconSize = 36f;
            Rect iconRect = new Rect(
                rect.x + (rect.width - iconSize) * 0.5f,
                rect.y + 8f,
                iconSize,
                iconSize
            );
            GUI.color = Color.white;
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
        }

        if (!string.IsNullOrEmpty(itemName))
        {
            Rect labelRect = new Rect(rect.x, rect.yMax - 18f, rect.width, 16f);
            itemLabelStyle.normal.textColor = isActive ? textActiveColor : textColor;

            // Pixel drop shadow
            GUI.Label(new Rect(labelRect.x + 1f, labelRect.y + 1f, labelRect.width, labelRect.height), itemName, itemLabelShadowStyle);
            GUI.Label(labelRect, itemName, itemLabelStyle);
        }
        else
        {
            // Empty slot indicator in center
            if (!isActive)
            {
                GUI.color = new Color(0.25f, 0.25f, 0.25f, 0.4f);
                float dotSize = 4f;
                GUI.DrawTexture(new Rect(rect.x + (rect.width - dotSize) * 0.5f, rect.y + (rect.height - dotSize) * 0.5f, dotSize, dotSize), pixelTexture);
            }
        }

        GUI.color = Color.white;
    }

    private void DrawRectBorder(Rect r, Color color, float thickness)
    {
        GUI.color = color;
        // Top
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), pixelTexture);
        // Bottom
        GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), pixelTexture);
        // Left
        GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), pixelTexture);
        // Right
        GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), pixelTexture);
    }
}

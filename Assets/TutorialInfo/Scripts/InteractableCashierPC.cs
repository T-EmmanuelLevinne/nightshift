using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Point-of-Sale (POS) terminal on the "CashierPC" counter model.
/// - Updates the 3D monitor screen on the CashierPC model in real time as items are scanned.
/// - Becomes interactable ("E to Checkout") when Steve or Melia pays at the counter.
/// - Opens a full-screen "Jojo's Gas Station" POS UI with unlocked mouse cursor, complete receipt details,
///   open/close button, and a "Checkout" button with a 2-second loading bar before continuing the story.
/// </summary>
public class InteractableCashierPC : MonoBehaviour, IInteractable
{
    public static InteractableCashierPC Instance { get; private set; }

    public static bool IsPosScreenOpen { get; private set; } = false;

    [System.Serializable]
    public struct ScannedPosItem
    {
        public string sku;
        public string name;
        public int quantity;
        public float unitPrice;

        public ScannedPosItem(string sku, string name, int quantity, float unitPrice)
        {
            this.sku = sku;
            this.name = name;
            this.quantity = quantity;
            this.unitPrice = unitPrice;
        }

        public float LineTotal => quantity * unitPrice;
    }

    public enum PosState
    {
        IdleReady,
        ScanningItems,
        AwaitingCheckout,
        ProcessingCheckout,
        TransactionApproved
    }

    [Header("POS State")]
    public PosState currentState = PosState.IdleReady;
    public string currentCustomerName = "Walk-in Customer";
    public string currentTransactionId = "#JG-0040";
    public float cashTendered = 0f;

    private readonly List<ScannedPosItem> scannedItems = new List<ScannedPosItem>();
    private readonly List<string> terminalLog = new List<string>();

    private bool isCheckoutReady = false;
    private bool isCheckoutCompleted = false;
    private bool isProcessingCheckout = false;
    private float processingTimer = 0f;
    private const float CheckoutLoadingDuration = 2.0f;
    private int transactionCounter = 40;

    // 3D Monitor Real-Time Display
    private const int ScreenTexWidth = 256;
    private const int ScreenTexHeight = 144;
    private Texture2D monitorScreenTex;
    private Color32[] monitorPixels;
    private Material monitorEmissiveMaterial;
    private GameObject fittedScreenQuadObj;

    // Full-Screen GUI Resources
    private Texture2D uiPixelTex;
    private Font posFont;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle sectionHeaderStyle;
    private GUIStyle tableHeaderStyle;
    private GUIStyle tableCellStyle;
    private GUIStyle tableCellRightStyle;
    private GUIStyle summaryLabelStyle;
    private GUIStyle summaryValueStyle;
    private GUIStyle totalDueLabelStyle;
    private GUIStyle totalDueValueStyle;
    private GUIStyle logStyle;
    private GUIStyle buttonStyle;
    private GUIStyle closeButtonStyle;

    // Audio
    private AudioSource audioSource;
    private AudioClip uiOpenClip;
    private AudioClip uiClickClip;
    private AudioClip checkoutSuccessClip;

    public bool IsCheckoutReady => isCheckoutReady && !isCheckoutCompleted;
    public bool IsCheckoutCompleted => isCheckoutCompleted;

    public Vector3 IndicatorWorldPosition =>
        fittedScreenQuadObj != null
            ? fittedScreenQuadObj.GetComponent<Renderer>().bounds.center + new Vector3(0f, 0.19f, 0f)
            : transform.position + new Vector3(0.265f, 0.36f, -0.04f);

    public static InteractableCashierPC EnsureAttachedToScenePC()
    {
        if (Instance != null && Instance.gameObject != null)
        {
            return Instance;
        }

        InteractableCashierPC existing = Object.FindAnyObjectByType<InteractableCashierPC>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        Transform targetTransform = null;
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            string nm = t.name.Trim();
            if (string.Equals(nm, "CashierPC", System.StringComparison.OrdinalIgnoreCase))
            {
                targetTransform = t;
                break;
            }
        }

        if (targetTransform != null)
        {
            InteractableCashierPC comp = targetTransform.GetComponent<InteractableCashierPC>();
            if (comp == null)
            {
                comp = targetTransform.gameObject.AddComponent<InteractableCashierPC>();
            }
            Instance = comp;
            return comp;
        }

        return null;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        IsPosScreenOpen = false;

        EnsureColliderSetup();
        SetupAudio();
        Setup3DMonitorScreen();
        ResetToIdleScreen();
    }

    private void OnDestroy()
    {
        if (IsPosScreenOpen)
        {
            ClosePosUI();
        }
    }

    private void EnsureColliderSetup()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(-0.25f, 0.0f, 0.14f);
            box.size = new Vector3(0.50f, 0.25f, 0.36f);
        }
    }

    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.2f;
            audioSource.volume = 0.7f;
        }

        int sampleRate = 44100;

        // 1. UI Open / Close beep
        int openLen = (int)(sampleRate * 0.07f);
        float[] openSamples = new float[openLen];
        for (int i = 0; i < openLen; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 42f);
            openSamples[i] = Mathf.Sin(2f * Mathf.PI * 1240f * t) * env * 0.35f;
        }
        uiOpenClip = AudioClip.Create("PosUiOpenBeep", openLen, 1, sampleRate, false);
        uiOpenClip.SetData(openSamples, 0);

        // 2. UI Button Click
        int clickLen = (int)(sampleRate * 0.045f);
        float[] clickSamples = new float[clickLen];
        for (int i = 0; i < clickLen; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 65f);
            clickSamples[i] = (Mathf.Sin(2f * Mathf.PI * 1680f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 840f * t) * 0.4f) * env * 0.42f;
        }
        uiClickClip = AudioClip.Create("PosUiClick", clickLen, 1, sampleRate, false);
        uiClickClip.SetData(clickSamples, 0);

        // 3. Checkout Approved Chime + Register Drawer Bell
        int chimeLen = (int)(sampleRate * 0.52f);
        float[] chimeSamples = new float[chimeLen];
        for (int i = 0; i < chimeLen; i++)
        {
            float t = (float)i / sampleRate;
            float freq = (t < 0.11f) ? 1046.5f : 1567.98f; // C6 -> G6 approval chime
            float localT = (t < 0.11f) ? t : (t - 0.11f);
            float env = Mathf.Exp(-localT * 8.5f);
            float harmonic = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.65f + Mathf.Sin(2f * Mathf.PI * (freq * 2f) * t) * 0.25f;
            chimeSamples[i] = harmonic * env * 0.48f;
        }
        checkoutSuccessClip = AudioClip.Create("PosCheckoutSuccess", chimeLen, 1, sampleRate, false);
        checkoutSuccessClip.SetData(chimeSamples, 0);
    }

    // =========================================================================
    // PUBLIC TRANSACTION API (CALLED BY STEVE & MELIA)
    // =========================================================================

    public void ResetToIdleScreen()
    {
        scannedItems.Clear();
        terminalLog.Clear();
        currentCustomerName = "Walk-in Customer";
        cashTendered = 0f;
        isCheckoutReady = false;
        isCheckoutCompleted = false;
        isProcessingCheckout = false;
        processingTimer = 0f;
        currentState = PosState.IdleReady;

        AddTerminalLog("POS Terminal #01 initialized - Jojo's Gas Station.");
        AddTerminalLog("Scanner online. Waiting for items...");
        Refresh3DMonitorTexture();
    }

    public void BeginCustomerTransaction(string customerName)
    {
        scannedItems.Clear();
        terminalLog.Clear();
        transactionCounter++;
        currentTransactionId = $"#JG-00{transactionCounter}";
        currentCustomerName = string.IsNullOrEmpty(customerName) ? "Customer" : customerName;
        cashTendered = 0f;
        isCheckoutReady = false;
        isCheckoutCompleted = false;
        isProcessingCheckout = false;
        processingTimer = 0f;
        currentState = PosState.ScanningItems;

        AddTerminalLog($"New transaction {currentTransactionId} started for {currentCustomerName}.");
        Refresh3DMonitorTexture();
    }

    public void RecordScannedItem(string sku, string itemName, float unitPrice, string customerName = null)
    {
        if (currentState == PosState.IdleReady || currentState == PosState.TransactionApproved)
        {
            BeginCustomerTransaction(customerName ?? "Customer");
        }
        else if (!string.IsNullOrEmpty(customerName))
        {
            currentCustomerName = customerName;
        }

        // Check if same SKU already scanned
        for (int i = 0; i < scannedItems.Count; i++)
        {
            if (string.Equals(scannedItems[i].sku, sku, System.StringComparison.OrdinalIgnoreCase))
            {
                ScannedPosItem existing = scannedItems[i];
                existing.quantity += 1;
                scannedItems[i] = existing;
                AddTerminalLog($"[SCAN] {itemName} (x{existing.quantity}) - ${existing.LineTotal:0.00}");
                Refresh3DMonitorTexture();
                return;
            }
        }

        scannedItems.Add(new ScannedPosItem(sku, itemName, 1, unitPrice));
        currentState = PosState.ScanningItems;
        AddTerminalLog($"[SCAN] {itemName} added - ${unitPrice:0.00}");
        Refresh3DMonitorTexture();
    }

    public void SetReadyForCheckout(string customerName, float tenderedAmount)
    {
        if (!string.IsNullOrEmpty(customerName))
        {
            currentCustomerName = customerName;
        }

        cashTendered = Mathf.Max(GetTotalAmount(), tenderedAmount);
        isCheckoutReady = true;
        isCheckoutCompleted = false;
        isProcessingCheckout = false;
        processingTimer = 0f;
        currentState = PosState.AwaitingCheckout;

        AddTerminalLog($"[CASH] {currentCustomerName} tendered ${cashTendered:0.00}.");
        AddTerminalLog("Ready for checkout. Press [CHECKOUT] to finalize.");
        Refresh3DMonitorTexture();

        if (NightShiftStoryController.Instance != null)
        {
            NightShiftStoryController.Instance.NotifyCustomerTaskUpdated();
        }
    }

    public float GetTotalAmount()
    {
        float sum = 0f;
        for (int i = 0; i < scannedItems.Count; i++)
        {
            sum += scannedItems[i].LineTotal;
        }
        return sum;
    }

    public int GetTotalItemCount()
    {
        int count = 0;
        for (int i = 0; i < scannedItems.Count; i++)
        {
            count += scannedItems[i].quantity;
        }
        return count;
    }

    private void AddTerminalLog(string message)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        terminalLog.Add($"[{timestamp}] {message}");
        if (terminalLog.Count > 6)
        {
            terminalLog.RemoveAt(0);
        }
    }

    // =========================================================================
    // IINTERACTABLE IMPLEMENTATION
    // =========================================================================

    public bool IsPlayerInsideCashierZone
    {
        get
        {
            if (CustomerNpcSteve.Instance != null && CustomerNpcSteve.Instance.IsNpcActiveInWorld)
            {
                return CustomerNpcSteve.Instance.IsPlayerInsideCashierZone;
            }
            if (CustomerNpcMelia.Instance != null && CustomerNpcMelia.Instance.IsNpcActiveInWorld)
            {
                return CustomerNpcMelia.Instance.IsPlayerInsideCashierZone;
            }

            Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
            if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
            if (cam == null) return false;

            return Vector3.Distance(cam.transform.position, transform.position) <= 2.35f;
        }
    }

    public void Interact()
    {
        if (NightShiftStoryController.IsDialogueActive) return;
        if (IsPosScreenOpen) return;

        if (!IsPlayerInsideCashierZone)
        {
            return;
        }

        // Open the POS UI when checkout is ready, or allow viewing the POS screen while in the cashier area
        if (isCheckoutReady && !isCheckoutCompleted)
        {
            OpenPosUI();
        }
        else if (scannedItems.Count > 0 || currentState != PosState.IdleReady)
        {
            OpenPosUI();
        }
    }

    public string GetPromptText()
    {
        if (NightShiftStoryController.IsDialogueActive) return string.Empty;
        if (IsPosScreenOpen) return string.Empty;

        if (isCheckoutReady && !isCheckoutCompleted)
        {
            if (!IsPlayerInsideCashierZone)
            {
                return "Go to Cashier area first.";
            }
            return "E to Checkout";
        }

        if (scannedItems.Count > 0 && IsPlayerInsideCashierZone)
        {
            return "E to view Cashier PC";
        }

        return string.Empty;
    }

    public void OpenPosUI()
    {
        if (IsPosScreenOpen) return;

        IsPosScreenOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (audioSource != null && uiOpenClip != null)
        {
            audioSource.PlayOneShot(uiOpenClip, 0.65f);
        }
    }

    public void ClosePosUI()
    {
        if (!IsPosScreenOpen) return;
        if (isProcessingCheckout) return; // Wait for 2s checkout loading to complete

        IsPosScreenOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (audioSource != null && uiOpenClip != null)
        {
            audioSource.PlayOneShot(uiOpenClip, 0.50f);
        }
    }

    private void StartCheckoutLoading()
    {
        if (!isCheckoutReady || isCheckoutCompleted || isProcessingCheckout) return;

        isProcessingCheckout = true;
        processingTimer = 0f;
        currentState = PosState.ProcessingCheckout;
        AddTerminalLog("Processing payment & opening register drawer...");

        if (audioSource != null && uiClickClip != null)
        {
            audioSource.PlayOneShot(uiClickClip, 0.85f);
        }

        Refresh3DMonitorTexture();
    }

    private void Update()
    {
        if (IsPosScreenOpen)
        {
            // Ensure mouse cursor stays unlocked and visible while POS UI is open
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        if (isProcessingCheckout)
        {
            processingTimer += Time.unscaledDeltaTime;
            Refresh3DMonitorTexture();

            if (processingTimer >= CheckoutLoadingDuration)
            {
                CompleteCheckoutTransaction();
            }
        }
    }

    private void CompleteCheckoutTransaction()
    {
        float completedTotal = GetTotalAmount();
        string completedTxId = currentTransactionId;

        isProcessingCheckout = false;
        isCheckoutReady = false;
        isCheckoutCompleted = true;

        // Immediately empty the POS screen (clear scanned items, totals, and tender) after transaction completes
        scannedItems.Clear();
        cashTendered = 0f;
        currentCustomerName = "Walk-in Customer";
        currentState = PosState.IdleReady;

        terminalLog.Clear();
        AddTerminalLog($"[APPROVED] Transaction {completedTxId} PAID (${completedTotal:0.00}).");
        AddTerminalLog("Register cleared. Waiting for next customer...");
        Refresh3DMonitorTexture();

        if (audioSource != null && checkoutSuccessClip != null)
        {
            audioSource.PlayOneShot(checkoutSuccessClip, 0.90f);
        }

        // Automatically close full-screen POS UI and restore FPS cursor lock so story/dialogue continues smoothly
        IsPosScreenOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // =========================================================================
    // FULL-SCREEN POS UI ("Jojo's Gas Station")
    // =========================================================================

    private void InitGuiResources()
    {
        if (uiPixelTex == null)
        {
            uiPixelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            uiPixelTex.SetPixel(0, 0, Color.white);
            uiPixelTex.Apply();
        }

        if (posFont == null)
        {
            string[] fontNames = new string[] { "Consolas", "Lucida Console", "Courier New", "Courier" };
            posFont = Font.CreateDynamicFontFromOSFont(fontNames, 16);
        }

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = posFont,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color(0.98f, 0.88f, 0.42f, 1f);

            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = posFont,
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            subtitleStyle.normal.textColor = new Color(0.68f, 0.78f, 0.86f, 0.95f);

            sectionHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                font = posFont,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            sectionHeaderStyle.normal.textColor = new Color(0.55f, 0.88f, 0.98f, 1f);

            tableHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                font = posFont,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            tableHeaderStyle.normal.textColor = new Color(0.75f, 0.85f, 0.92f, 1f);

            tableCellStyle = new GUIStyle(GUI.skin.label)
            {
                font = posFont,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            tableCellStyle.normal.textColor = new Color(0.94f, 0.96f, 0.98f, 1f);

            tableCellRightStyle = new GUIStyle(tableCellStyle)
            {
                alignment = TextAnchor.MiddleRight
            };

            summaryLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = posFont,
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            summaryLabelStyle.normal.textColor = new Color(0.76f, 0.82f, 0.88f, 1f);

            summaryValueStyle = new GUIStyle(GUI.skin.label)
            {
                font = posFont,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            summaryValueStyle.normal.textColor = new Color(0.95f, 0.97f, 1.0f, 1f);

            totalDueLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = posFont,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            totalDueLabelStyle.normal.textColor = new Color(0.98f, 0.88f, 0.42f, 1f);

            totalDueValueStyle = new GUIStyle(GUI.skin.label)
            {
                font = posFont,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            totalDueValueStyle.normal.textColor = new Color(0.42f, 0.98f, 0.58f, 1f);

            logStyle = new GUIStyle(GUI.skin.label)
            {
                font = posFont,
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            logStyle.normal.textColor = new Color(0.58f, 0.84f, 0.68f, 0.92f);

            buttonStyle = new GUIStyle(GUI.skin.label)
            {
                font = posFont,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            buttonStyle.normal.textColor = Color.white;

            closeButtonStyle = new GUIStyle(GUI.skin.label)
            {
                font = posFont,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            closeButtonStyle.normal.textColor = Color.white;
        }
    }

    private void OnGUI()
    {
        if (!IsPosScreenOpen) return;

        InitGuiResources();

        float sw = Screen.width;
        float sh = Screen.height;

        // 1. Full-Screen CRT Monitor Backdrop & Outer Bezel
        DrawSolidRect(new Rect(0f, 0f, sw, sh), new Color(0.04f, 0.06f, 0.09f, 0.98f));
        DrawBorderRect(new Rect(8f, 8f, sw - 16f, sh - 16f), new Color(0.18f, 0.28f, 0.36f, 1f), 2f);

        // 2. Top Header Bar ("Jojo's Gas Station" on top-left + Terminal Metadata + Close Button on top-right)
        Rect headerRect = new Rect(12f, 12f, sw - 24f, 68f);
        DrawSolidRect(headerRect, new Color(0.08f, 0.13f, 0.19f, 1f));
        DrawSolidRect(new Rect(headerRect.x, headerRect.yMax - 2f, headerRect.width, 2f), new Color(0.95f, 0.78f, 0.25f, 0.9f));

        // Store icon badge + "Jojo's Gas Station" Top-Left
        Rect logoBadge = new Rect(headerRect.x + 16f, headerRect.y + 12f, 44f, 44f);
        DrawSolidRect(logoBadge, new Color(0.88f, 0.22f, 0.18f, 1f));
        DrawBorderRect(logoBadge, new Color(0.98f, 0.88f, 0.42f, 1f), 2f);
        GUI.Label(logoBadge, "JG", buttonStyle);

        GUI.Label(new Rect(logoBadge.xMax + 14f, headerRect.y + 8f, 420f, 30f), "Jojo's Gas Station", titleStyle);
        GUI.Label(new Rect(logoBadge.xMax + 14f, headerRect.y + 38f, 520f, 22f), "POS REGISTER TERMINAL #01  |  SHIFT: MIDNIGHT  |  CASHIER: ON DUTY", subtitleStyle);

        // Close Button (Top-Right)
        Rect closeBtnRect = new Rect(headerRect.xMax - 174f, headerRect.y + 16f, 158f, 36f);
        bool closeHover = !isProcessingCheckout && closeBtnRect.Contains(Event.current.mousePosition);
        Color closeBg = isProcessingCheckout
            ? new Color(0.32f, 0.32f, 0.35f, 0.6f)
            : (closeHover ? new Color(0.86f, 0.24f, 0.24f, 1f) : new Color(0.65f, 0.18f, 0.18f, 1f));
        DrawSolidRect(closeBtnRect, closeBg);
        DrawBorderRect(closeBtnRect, new Color(1f, 0.75f, 0.75f, 0.8f), 1.5f);
        GUI.Label(closeBtnRect, "X  CLOSE (ESC)", closeButtonStyle);

        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && closeHover)
        {
            Event.current.Use();
            ClosePosUI();
            return;
        }

        // Content Layout Split: Left Scanned Items Table (62%) | Right Payment & Checkout Panel (38%)
        float contentY = headerRect.yMax + 14f;
        float contentHeight = sh - contentY - 22f;
        float leftWidth = Mathf.Floor((sw - 40f) * 0.62f);
        float rightWidth = (sw - 40f) - leftWidth - 14f;

        Rect leftPanel = new Rect(16f, contentY, leftWidth, contentHeight);
        Rect rightPanel = new Rect(leftPanel.xMax + 14f, contentY, rightWidth, contentHeight);

        DrawLeftScannedItemsPanel(leftPanel);
        DrawRightCheckoutPanel(rightPanel);
    }

    private void DrawLeftScannedItemsPanel(Rect panel)
    {
        DrawSolidRect(panel, new Color(0.06f, 0.09f, 0.14f, 1f));
        DrawBorderRect(panel, new Color(0.18f, 0.28f, 0.38f, 1f), 1.5f);

        // Section Header
        Rect secHeader = new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, 28f);
        GUI.Label(secHeader, $"SCANNED ITEMS LIST  ({GetTotalItemCount()} ITEM{(GetTotalItemCount() == 1 ? "" : "S")})", sectionHeaderStyle);

        // Table Header Bar
        Rect thRect = new Rect(panel.x + 12f, panel.y + 42f, panel.width - 24f, 32f);
        DrawSolidRect(thRect, new Color(0.11f, 0.18f, 0.26f, 1f));

        float cNo = thRect.x + 10f;
        float cSku = thRect.x + 60f;
        float cDesc = thRect.x + 195f;
        float cQty = thRect.xMax - 235f;
        float cUnit = thRect.xMax - 155f;
        float cTotal = thRect.xMax - 85f;

        GUI.Label(new Rect(cNo, thRect.y, 45f, thRect.height), "NO.", tableHeaderStyle);
        GUI.Label(new Rect(cSku, thRect.y, 130f, thRect.height), "SKU CODE", tableHeaderStyle);
        GUI.Label(new Rect(cDesc, thRect.y, cQty - cDesc - 8f, thRect.height), "ITEM DESCRIPTION", tableHeaderStyle);
        GUI.Label(new Rect(cQty, thRect.y, 60f, thRect.height), "QTY", tableHeaderStyle);
        GUI.Label(new Rect(cUnit, thRect.y, 70f, thRect.height), "PRICE", tableHeaderStyle);
        GUI.Label(new Rect(cTotal, thRect.y, 75f, thRect.height), "TOTAL", tableHeaderStyle);

        // Item Rows
        float rowY = thRect.yMax + 6f;
        float rowH = 42f;

        if (scannedItems.Count == 0)
        {
            Rect emptyRect = new Rect(thRect.x, rowY + 30f, thRect.width, 40f);
            GUI.Label(emptyRect, "No items scanned in current transaction.", subtitleStyle);
        }
        else
        {
            for (int i = 0; i < scannedItems.Count; i++)
            {
                ScannedPosItem item = scannedItems[i];
                Rect rowRect = new Rect(thRect.x, rowY + i * (rowH + 4f), thRect.width, rowH);
                Color rowBg = (i % 2 == 0)
                    ? new Color(0.09f, 0.14f, 0.20f, 0.95f)
                    : new Color(0.07f, 0.11f, 0.16f, 0.95f);
                DrawSolidRect(rowRect, rowBg);
                DrawBorderRect(rowRect, new Color(0.20f, 0.32f, 0.44f, 0.65f), 1f);

                GUI.Label(new Rect(cNo, rowRect.y, 45f, rowH), $"#{i + 1:00}", tableCellStyle);
                GUI.Label(new Rect(cSku, rowRect.y, 130f, rowH), item.sku, subtitleStyle);
                GUI.Label(new Rect(cDesc, rowRect.y, cQty - cDesc - 8f, rowH), item.name, tableCellStyle);
                GUI.Label(new Rect(cQty, rowRect.y, 60f, rowH), $"x{item.quantity}", tableCellStyle);
                GUI.Label(new Rect(cUnit, rowRect.y, 65f, rowH), $"${item.unitPrice:0.00}", tableCellStyle);
                GUI.Label(new Rect(cTotal, rowRect.y, 72f, rowH), $"${item.LineTotal:0.00}", tableCellRightStyle);
            }
        }

        // Bottom Terminal Activity Log Box
        float logBoxHeight = 148f;
        Rect logBox = new Rect(panel.x + 12f, panel.yMax - logBoxHeight - 12f, panel.width - 24f, logBoxHeight);
        DrawSolidRect(logBox, new Color(0.03f, 0.05f, 0.08f, 1f));
        DrawBorderRect(logBox, new Color(0.16f, 0.26f, 0.34f, 1f), 1f);

        GUI.Label(new Rect(logBox.x + 10f, logBox.y + 6f, 320f, 20f), "BARCODE SCANNER & REGISTER LOG:", tableHeaderStyle);

        for (int i = 0; i < terminalLog.Count; i++)
        {
            GUI.Label(new Rect(logBox.x + 12f, logBox.y + 28f + i * 19f, logBox.width - 24f, 20f), terminalLog[i], logStyle);
        }
    }

    private void DrawRightCheckoutPanel(Rect panel)
    {
        DrawSolidRect(panel, new Color(0.07f, 0.10f, 0.15f, 1f));
        DrawBorderRect(panel, new Color(0.18f, 0.28f, 0.38f, 1f), 1.5f);

        float pad = 16f;
        float innerW = panel.width - pad * 2f;
        float curY = panel.y + 12f;

        GUI.Label(new Rect(panel.x + pad, curY, innerW, 28f), "TRANSACTION SUMMARY", sectionHeaderStyle);
        curY += 34f;

        // Customer & Receipt Details Card
        Rect detailsCard = new Rect(panel.x + pad, curY, innerW, 134f);
        DrawSolidRect(detailsCard, new Color(0.05f, 0.08f, 0.12f, 1f));
        DrawBorderRect(detailsCard, new Color(0.16f, 0.24f, 0.34f, 1f), 1f);

        DrawSummaryRow(detailsCard.x + 12f, detailsCard.y + 10f, detailsCard.width - 24f, "STORE LOCATION:", "Jojo's Gas Station");
        DrawSummaryRow(detailsCard.x + 12f, detailsCard.y + 38f, detailsCard.width - 24f, "RECEIPT NO.:", currentTransactionId);
        DrawSummaryRow(detailsCard.x + 12f, detailsCard.y + 66f, detailsCard.width - 24f, "CUSTOMER:", currentCustomerName);
        DrawSummaryRow(detailsCard.x + 12f, detailsCard.y + 94f, detailsCard.width - 24f, "PAYMENT TYPE:", "CASH TENDER");

        curY = detailsCard.yMax + 14f;

        // Financial Totals Card
        float totalDue = GetTotalAmount();
        float tendered = isCheckoutReady || isCheckoutCompleted ? Mathf.Max(totalDue, cashTendered) : 0f;
        float changeDue = Mathf.Max(0f, tendered - totalDue);

        Rect totalsCard = new Rect(panel.x + pad, curY, innerW, 168f);
        DrawSolidRect(totalsCard, new Color(0.05f, 0.08f, 0.12f, 1f));
        DrawBorderRect(totalsCard, new Color(0.22f, 0.34f, 0.46f, 1f), 1.5f);

        DrawSummaryRow(totalsCard.x + 12f, totalsCard.y + 10f, totalsCard.width - 24f, "SUBTOTAL:", $"${totalDue:0.00}");
        DrawSummaryRow(totalsCard.x + 12f, totalsCard.y + 36f, totalsCard.width - 24f, "SALES TAX (0%):", "$0.00");

        // Divider Line
        DrawSolidRect(new Rect(totalsCard.x + 12f, totalsCard.y + 66f, totalsCard.width - 24f, 1.5f), new Color(0.3f, 0.4f, 0.5f, 0.7f));

        // Total Due Highlight Row
        GUI.Label(new Rect(totalsCard.x + 12f, totalsCard.y + 74f, 180f, 32f), "TOTAL DUE:", totalDueLabelStyle);
        GUI.Label(new Rect(totalsCard.x + 12f, totalsCard.y + 74f, totalsCard.width - 24f, 32f), $"${totalDue:0.00}", totalDueValueStyle);

        DrawSummaryRow(totalsCard.x + 12f, totalsCard.y + 112f, totalsCard.width - 24f, "CASH RECEIVED:", $"${tendered:0.00}");
        DrawSummaryRow(totalsCard.x + 12f, totalsCard.y + 136f, totalsCard.width - 24f, "CHANGE DUE:", $"${changeDue:0.00}");

        curY = totalsCard.yMax + 18f;

        // Checkout Action / 2-Second Loading Area
        if (isProcessingCheckout)
        {
            float progress = Mathf.Clamp01(processingTimer / CheckoutLoadingDuration);
            int pct = Mathf.Clamp(Mathf.RoundToInt(progress * 100f), 0, 100);

            Rect loadBox = new Rect(panel.x + pad, curY, innerW, 88f);
            DrawSolidRect(loadBox, new Color(0.08f, 0.16f, 0.24f, 1f));
            DrawBorderRect(loadBox, new Color(0.38f, 0.78f, 0.98f, 1f), 2f);

            GUI.Label(new Rect(loadBox.x, loadBox.y + 10f, loadBox.width, 26f), $"Processing Checkout... {pct}%", buttonStyle);

            Rect barBg = new Rect(loadBox.x + 16f, loadBox.y + 44f, loadBox.width - 32f, 26f);
            DrawSolidRect(barBg, new Color(0.04f, 0.06f, 0.09f, 1f));
            DrawSolidRect(new Rect(barBg.x + 2f, barBg.y + 2f, (barBg.width - 4f) * progress, barBg.height - 4f), new Color(0.24f, 0.88f, 0.48f, 1f));
            DrawBorderRect(barBg, new Color(0.55f, 0.85f, 0.95f, 0.9f), 1f);
        }
        else
        {
            Rect checkoutBtn = new Rect(panel.x + pad, curY, innerW, 64f);
            bool canClickCheckout = isCheckoutReady && !isCheckoutCompleted;
            bool hoverCheckout = canClickCheckout && checkoutBtn.Contains(Event.current.mousePosition);

            Color btnBg;
            string btnLabel;
            if (isCheckoutCompleted)
            {
                btnBg = new Color(0.18f, 0.52f, 0.30f, 0.85f);
                btnLabel = "TRANSACTION COMPLETED";
            }
            else if (canClickCheckout)
            {
                btnBg = hoverCheckout
                    ? new Color(0.24f, 0.86f, 0.44f, 1f)
                    : new Color(0.16f, 0.72f, 0.34f, 1f);
                btnLabel = $"CHECKOUT  (${totalDue:0.00})";
            }
            else
            {
                btnBg = new Color(0.22f, 0.26f, 0.32f, 0.85f);
                btnLabel = "WAITING FOR PAYMENT...";
            }

            DrawSolidRect(checkoutBtn, btnBg);
            DrawBorderRect(checkoutBtn, canClickCheckout ? new Color(0.75f, 1f, 0.82f, 1f) : new Color(0.4f, 0.45f, 0.5f, 0.6f), 2f);
            GUI.Label(checkoutBtn, btnLabel, buttonStyle);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hoverCheckout)
            {
                Event.current.Use();
                StartCheckoutLoading();
            }
        }
    }

    private void DrawSummaryRow(float x, float y, float width, string label, string value)
    {
        GUI.Label(new Rect(x, y, width * 0.55f, 24f), label, summaryLabelStyle);
        GUI.Label(new Rect(x, y, width, 24f), value, summaryValueStyle);
    }

    private void DrawSolidRect(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, uiPixelTex);
        GUI.color = prev;
    }

    private void DrawBorderRect(Rect r, Color c, float thickness)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), uiPixelTex);
        GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), uiPixelTex);
        GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), uiPixelTex);
        GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), uiPixelTex);
        GUI.color = prev;
    }

    // =========================================================================
    // REAL-TIME 3D MONITOR SCREEN ON CASHIERPC MODEL
    // =========================================================================

    private void Setup3DMonitorScreen()
    {
        monitorScreenTex = new Texture2D(ScreenTexWidth, ScreenTexHeight, TextureFormat.RGBA32, false);
        monitorScreenTex.filterMode = FilterMode.Point;
        monitorScreenTex.wrapMode = TextureWrapMode.Clamp;
        monitorPixels = new Color32[ScreenTexWidth * ScreenTexHeight];

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");
        if (unlitShader == null) unlitShader = Shader.Find("Universal Render Pipeline/Lit");

        monitorEmissiveMaterial = new Material(unlitShader);
        monitorEmissiveMaterial.name = "CashierPC_LiveScreen_Mat";
        monitorEmissiveMaterial.mainTexture = monitorScreenTex;
        if (monitorEmissiveMaterial.HasProperty("_BaseMap"))
        {
            monitorEmissiveMaterial.SetTexture("_BaseMap", monitorScreenTex);
        }
        if (monitorEmissiveMaterial.HasProperty("_BaseColor"))
        {
            monitorEmissiveMaterial.SetColor("_BaseColor", Color.white);
        }
        if (monitorEmissiveMaterial.HasProperty("_EmissionColor"))
        {
            monitorEmissiveMaterial.EnableKeyword("_EMISSION");
            monitorEmissiveMaterial.SetTexture("_EmissionMap", monitorScreenTex);
            monitorEmissiveMaterial.SetColor("_EmissionColor", Color.white * 1.15f);
        }
        if (monitorEmissiveMaterial.HasProperty("_Cull"))
        {
            monitorEmissiveMaterial.SetFloat("_Cull", 2f); // Front-facing towards cashier
        }

        // Fit a real-time 3D screen quad directly inside the recessed LCD bezel of CashierPC (Cube.009 in stacja.fbx).
        // CashierPC has localScale = (100, 100, 100) and Unity FBX import factor 0.01 with -X handedness flip:
        // Exact recessed LCD panel corners in Unity Mesh local space:
        //   X: [+0.00047801 .. +0.00482156]  (43.44cm wide)
        //   Bottom edge: Y = +0.00062310, Z = +0.00062816
        //   Top edge:    Y = +0.00012840, Z = +0.00293574  (23.60cm tall, tilted 12.1 deg, recessed 3.3mm inside bezel)
        MeshFilter mf = GetComponent<MeshFilter>();

        Vector3 localBL = new Vector3(0.00047801f, 0.00062310f, 0.00062816f);
        Vector3 localBR = new Vector3(0.00482156f, 0.00062310f, 0.00062816f);
        Vector3 localTL = new Vector3(0.00047801f, 0.00012840f, 0.00293574f);
        Vector3 localTR = new Vector3(0.00482156f, 0.00012840f, 0.00293574f);

        if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable && mf.sharedMesh.subMeshCount > 0)
        {
            Mesh m = mf.sharedMesh;
            int screenSubmesh = m.subMeshCount - 1; // Submesh 6 (Material.024) is the recessed LCD glass
            int[] tris = m.GetTriangles(screenSubmesh);
            Vector3[] verts = m.vertices;

            if (tris != null && tris.Length > 0 && verts != null && verts.Length > 0)
            {
                float minX = float.MaxValue;
                float maxX = float.MinValue;
                float minWorldY = float.MaxValue;
                float maxWorldY = float.MinValue;
                Vector3 bottomSample = verts[tris[0]];
                Vector3 topSample = verts[tris[0]];

                for (int i = 0; i < tris.Length; i++)
                {
                    Vector3 v = verts[tris[i]];
                    if (v.x < minX) minX = v.x;
                    if (v.x > maxX) maxX = v.x;

                    Vector3 w = transform.TransformPoint(v);
                    if (w.y < minWorldY)
                    {
                        minWorldY = w.y;
                        bottomSample = v;
                    }
                    if (w.y > maxWorldY)
                    {
                        maxWorldY = w.y;
                        topSample = v;
                    }
                }

                localBL = new Vector3(minX, bottomSample.y, bottomSample.z);
                localBR = new Vector3(maxX, bottomSample.y, bottomSample.z);
                localTL = new Vector3(minX, topSample.y, topSample.z);
                localTR = new Vector3(maxX, topSample.y, topSample.z);
            }
        }

        // Compensate for CashierPC's (100, 100, 100) scale so the quad sits just 0.5mm (0.0005m) in front of the
        // recessed LCD surface — nestled 2.8mm INSIDE the 3.3mm 3D plastic monitor bezel with zero floating gap!
        Vector3 edgeRight = (localBR - localBL).normalized;
        Vector3 edgeUp = (localTL - localBL).normalized;
        Vector3 normalOut = Vector3.Cross(edgeUp, edgeRight).normalized;
        if (normalOut.y < 0f) normalOut = -normalOut;

        const float flushWorldOffsetMeters = 0.0005f;
        Vector3 lossy = transform.lossyScale;
        Vector3 offset = new Vector3(
            normalOut.x * (flushWorldOffsetMeters / Mathf.Max(0.0001f, Mathf.Abs(lossy.x))),
            normalOut.y * (flushWorldOffsetMeters / Mathf.Max(0.0001f, Mathf.Abs(lossy.y))),
            normalOut.z * (flushWorldOffsetMeters / Mathf.Max(0.0001f, Mathf.Abs(lossy.z))));

        Transform existingQuad = transform.Find("CashierPC_LiveScreenQuad");
        if (existingQuad != null)
        {
            Destroy(existingQuad.gameObject);
        }

        fittedScreenQuadObj = new GameObject("CashierPC_LiveScreenQuad");
        fittedScreenQuadObj.transform.SetParent(transform, false);
        fittedScreenQuadObj.transform.localPosition = Vector3.zero;
        fittedScreenQuadObj.transform.localRotation = Quaternion.identity;
        fittedScreenQuadObj.transform.localScale = Vector3.one;

        MeshFilter qmf = fittedScreenQuadObj.AddComponent<MeshFilter>();
        MeshRenderer qmr = fittedScreenQuadObj.AddComponent<MeshRenderer>();
        qmr.sharedMaterial = monitorEmissiveMaterial;
        qmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        qmr.receiveShadows = false;

        Mesh quadMesh = new Mesh();
        quadMesh.name = "CashierPC_FittedMonitorMesh";
        quadMesh.vertices = new Vector3[]
        {
            localBL + offset,
            localBR + offset,
            localTL + offset,
            localTR + offset
        };
        quadMesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        quadMesh.triangles = new int[]
        {
            0, 2, 1,
            2, 3, 1
        };
        quadMesh.RecalculateNormals();
        quadMesh.RecalculateBounds();
        qmf.sharedMesh = quadMesh;
    }

    public void Refresh3DMonitorTexture()
    {
        if (monitorScreenTex == null || monitorPixels == null) return;

        // 1. Background CRT dark navy
        FillTexRect(0, 0, ScreenTexWidth, ScreenTexHeight, new Color32(12, 19, 30, 255));

        // Outer border
        DrawTexBorder(1, 1, ScreenTexWidth - 2, ScreenTexHeight - 2, new Color32(48, 76, 104, 255));

        // 2. Top Header Bar (y = 122..141)
        FillTexRect(3, 122, ScreenTexWidth - 6, 19, new Color32(22, 38, 58, 255));
        FillTexRect(3, 121, ScreenTexWidth - 6, 1, new Color32(238, 196, 68, 255));
        DrawPixelText("JOJO'S GAS STATION", 8, 128, new Color32(250, 218, 90, 255), 1);
        DrawPixelText("POS #1", 206, 128, new Color32(120, 220, 250, 255), 1);

        // 3. Column Header (y = 107..118)
        FillTexRect(4, 107, ScreenTexWidth - 8, 12, new Color32(28, 48, 70, 255));
        DrawPixelText("ITEM", 8, 110, new Color32(180, 210, 235, 255), 1);
        DrawPixelText("QTY", 164, 110, new Color32(180, 210, 235, 255), 1);
        DrawPixelText("PRICE", 206, 110, new Color32(180, 210, 235, 255), 1);

        // 4. Scanned Items Rows (y = 48..104)
        if (scannedItems.Count == 0)
        {
            DrawPixelText("READY - SCAN ITEMS", 54, 76, new Color32(130, 165, 195, 255), 1);
        }
        else
        {
            int maxRows = Mathf.Min(scannedItems.Count, 3);
            for (int i = 0; i < maxRows; i++)
            {
                ScannedPosItem it = scannedItems[i];
                int ry = 90 - i * 17;
                FillTexRect(6, ry - 3, ScreenTexWidth - 12, 14, (i % 2 == 0) ? new Color32(20, 34, 50, 255) : new Color32(16, 26, 40, 255));

                string shortName = it.name.ToUpperInvariant();
                if (shortName.Length > 20) shortName = shortName.Substring(0, 20);

                DrawPixelText($"{i + 1}.{shortName}", 10, ry, new Color32(240, 246, 255, 255), 1);
                DrawPixelText($"X{it.quantity}", 168, ry, new Color32(210, 235, 255, 255), 1);
                DrawPixelText($"${it.LineTotal:0.00}", 206, ry, new Color32(120, 248, 155, 255), 1);
            }
        }

        // 5. Bottom Summary & Status Bar (y = 4..44)
        FillTexRect(4, 4, ScreenTexWidth - 8, 38, new Color32(18, 30, 44, 255));
        FillTexRect(4, 42, ScreenTexWidth - 8, 1, new Color32(70, 110, 145, 255));

        float total = GetTotalAmount();
        DrawPixelText($"TOTAL: ${total:0.00}", 10, 26, new Color32(252, 224, 98, 255), 1);
        DrawPixelText($"ITEMS: {GetTotalItemCount()}", 10, 11, new Color32(170, 200, 225, 255), 1);

        // Status / Action Button on right half of 3D monitor bottom bar
        if (isProcessingCheckout)
        {
            float p = Mathf.Clamp01(processingTimer / CheckoutLoadingDuration);
            FillTexRect(122, 8, 124, 30, new Color32(26, 56, 86, 255));
            int fillW = Mathf.RoundToInt(118 * p);
            FillTexRect(125, 11, fillW, 10, new Color32(55, 225, 115, 255));
            DrawPixelText("LOADING...", 148, 25, new Color32(255, 255, 255, 255), 1);
        }
        else if (isCheckoutReady && !isCheckoutCompleted)
        {
            FillTexRect(122, 8, 124, 30, new Color32(36, 175, 82, 255));
            DrawTexBorder(122, 8, 124, 30, new Color32(180, 255, 200, 255));
            DrawPixelText("CHECKOUT", 156, 20, new Color32(255, 255, 255, 255), 1);
        }
        else
        {
            FillTexRect(122, 8, 124, 30, new Color32(32, 46, 64, 255));
            string st = scannedItems.Count > 0 ? "SCANNING" : "STANDBY";
            DrawPixelText(st, 156, 20, new Color32(150, 185, 215, 255), 1);
        }

        monitorScreenTex.SetPixels32(monitorPixels);
        monitorScreenTex.Apply(false);
    }

    private void FillTexRect(int x, int y, int w, int h, Color32 c)
    {
        int x0 = Mathf.Clamp(x, 0, ScreenTexWidth);
        int x1 = Mathf.Clamp(x + w, 0, ScreenTexWidth);
        int y0 = Mathf.Clamp(y, 0, ScreenTexHeight);
        int y1 = Mathf.Clamp(y + h, 0, ScreenTexHeight);

        for (int py = y0; py < y1; py++)
        {
            int rowOffset = py * ScreenTexWidth;
            for (int px = x0; px < x1; px++)
            {
                monitorPixels[rowOffset + px] = c;
            }
        }
    }

    private void DrawTexBorder(int x, int y, int w, int h, Color32 c)
    {
        FillTexRect(x, y, w, 1, c);
        FillTexRect(x, y + h - 1, w, 1, c);
        FillTexRect(x, y, 1, h, c);
        FillTexRect(x + w - 1, y, 1, h, c);
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
                byte rowBits = glyph[row];
                int py = startY + (6 - row) * scale;
                for (int col = 0; col < 5; col++)
                {
                    if ((rowBits & (1 << (4 - col))) != 0)
                    {
                        int px = curX + col * scale;
                        FillTexRect(px, py, scale, scale, color);
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
            case '$': return new byte[] { 0x04, 0x0F, 0x14, 0x0E, 0x05, 0x1E, 0x04 };
            case '.': return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C };
            case ':': return new byte[] { 0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00 };
            case '-': return new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 };
            case '#': return new byte[] { 0x0A, 0x1F, 0x0A, 0x0A, 0x1F, 0x0A, 0x00 };
            case '\'': return new byte[] { 0x04, 0x04, 0x08, 0x00, 0x00, 0x00, 0x00 };
            default:  return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        }
    }
}


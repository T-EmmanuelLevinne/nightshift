using System.Collections.Generic;
using UnityEngine;

public class ShelfRestockTarget : MonoBehaviour, IInteractable
{
    public static ShelfRestockTarget Instance { get; private set; }

    [Header("Restock Goal")]
    public int goalCount = 12;
    public int stockedCount = 0;

    [Header("References")]
    public GameObject restockCansSlot;

    private MeshFilter slotMeshFilter;
    private MeshRenderer slotMeshRenderer;
    private Mesh fullCansMesh;
    private Mesh[] progressiveMeshes;

    private GameObject stepCansRoot;
    private List<GameObject> individualShelfCans = new List<GameObject>();

    private BoxCollider interactionTrigger;
    private Vector3 slotWorldCenter;
    private Vector3 shelfTopWorldPos;

    private AudioSource audioSource;
    private AudioClip canPlaceClip;
    private AudioClip goalCompleteClip;

    private Font horrorFont;
    private GUIStyle cachedMainStyle;
    private GUIStyle cachedShadowStyle;

    public bool IsFullyStocked => stockedCount >= goalCount;

    public static ShelfRestockTarget EnsureExists()
    {
        if (Instance != null && Instance.gameObject.activeInHierarchy)
        {
            return Instance;
        }

        ShelfRestockTarget[] activeTargets = FindObjectsByType<ShelfRestockTarget>(FindObjectsInactive.Exclude);
        if (activeTargets != null && activeTargets.Length > 0)
        {
            Instance = activeTargets[0];
            return Instance;
        }

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
                Instance = target;
                return Instance;
            }
        }

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform tr = allTransforms[i];
            if (tr != null && tr.gameObject.activeInHierarchy &&
                string.Equals(tr.name.Trim(), "Shelf1", System.StringComparison.OrdinalIgnoreCase))
            {
                ShelfRestockTarget target = tr.gameObject.GetComponent<ShelfRestockTarget>();
                if (target == null)
                {
                    target = tr.gameObject.AddComponent<ShelfRestockTarget>();
                }
                Instance = target;
                return Instance;
            }
        }

        return null;
    }

    private void Awake()
    {
        if (Instance == null || !Instance.gameObject.activeInHierarchy)
        {
            Instance = this;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.7f;
            audioSource.volume = 0.8f;
        }

        GenerateProceduralAudio();
        EnsureFontLoaded();
    }

    private void OnEnable()
    {
        Instance = this;
    }

    private void Start()
    {
        SetupShelfSlotAndTrigger();
        UpdateShelfCansVisual();
    }

    private Bounds ComputeCombinedShelfBounds()
    {
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds combined = new Bounds(transform.position, new Vector3(2.4f, 1.4f, 0.7f));

        if (rends != null)
        {
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                string n = rends[i].gameObject.name;
                if (n.StartsWith("StockedCan_")) continue;

                if (!hasBounds)
                {
                    combined = rends[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(rends[i].bounds);
                }
            }
        }
        return combined;
    }

    public void SetupShelfSlotAndTrigger()
    {
        if (restockCansSlot == null)
        {
            Transform found = transform.Find("Restock_Cans_Slot");
            if (found != null)
            {
                restockCansSlot = found.gameObject;
            }
        }

        Bounds shelfBounds = ComputeCombinedShelfBounds();

        if (restockCansSlot != null)
        {
            slotMeshFilter = restockCansSlot.GetComponent<MeshFilter>();
            slotMeshRenderer = restockCansSlot.GetComponent<MeshRenderer>();

            if (slotMeshFilter != null && slotMeshFilter.sharedMesh != null)
            {
                if (fullCansMesh == null)
                {
                    fullCansMesh = slotMeshFilter.sharedMesh;
                }

                slotWorldCenter = restockCansSlot.transform.TransformPoint(fullCansMesh.bounds.center);

                BuildProgressiveCansMeshes();
                BuildIndividualStepCans();
            }
            else
            {
                slotWorldCenter = shelfBounds.center;
            }
        }
        else
        {
            slotWorldCenter = shelfBounds.center;
        }

        // Position directly on top of the shelf above the empty can restock bay
        shelfTopWorldPos = new Vector3(slotWorldCenter.x, shelfBounds.max.y + 0.14f, slotWorldCenter.z);

        // Create or update dedicated trigger collider on the empty shelf bay
        Transform triggerTr = transform.Find("Shelf_Restock_Bay_Trigger");
        GameObject triggerObj;
        if (triggerTr == null)
        {
            triggerObj = new GameObject("Shelf_Restock_Bay_Trigger");
            triggerObj.transform.SetParent(transform, true);
        }
        else
        {
            triggerObj = triggerTr.gameObject;
        }

        triggerObj.transform.position = slotWorldCenter;
        triggerObj.transform.rotation = Quaternion.identity;
        triggerObj.transform.localScale = Vector3.one;

        Vector3 parentScale = triggerObj.transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
        float sz = Mathf.Max(0.0001f, Mathf.Abs(parentScale.z));

        interactionTrigger = triggerObj.GetComponent<BoxCollider>();
        if (interactionTrigger == null)
        {
            interactionTrigger = triggerObj.AddComponent<BoxCollider>();
        }
        interactionTrigger.isTrigger = true;
        interactionTrigger.center = Vector3.zero;
        interactionTrigger.size = new Vector3(0.92f / sx, 0.56f / sy, 0.65f / sz);

        ShelfBayTriggerForwarder forwarder = triggerObj.GetComponent<ShelfBayTriggerForwarder>();
        if (forwarder == null)
        {
            forwarder = triggerObj.AddComponent<ShelfBayTriggerForwarder>();
        }
        forwarder.target = this;
    }

    private void BuildIndividualStepCans()
    {
        if (restockCansSlot == null || fullCansMesh == null) return;
        if (stepCansRoot != null) return;

        stepCansRoot = new GameObject("Shelf_StepByStep_Cans");
        stepCansRoot.transform.SetParent(restockCansSlot.transform.parent, false);
        stepCansRoot.transform.localPosition = restockCansSlot.transform.localPosition;
        stepCansRoot.transform.localRotation = restockCansSlot.transform.localRotation;
        stepCansRoot.transform.localScale = restockCansSlot.transform.localScale;

        Bounds b = fullCansMesh.bounds;
        Material[] canMats = slotMeshRenderer != null ? slotMeshRenderer.sharedMaterials : null;
        Mesh unitCanMesh = RestockCardboardBox.CreateSharedCanMesh();

        int cols = 4;
        int rows = 3;
        float sizeX = b.size.x;
        float sizeY = b.size.y;
        float sizeZ = b.size.z;

        bool zIsUp = sizeZ > sizeY;
        float widthSpan = sizeX;
        float heightSpan = zIsUp ? sizeZ : sizeY;
        float depthSpan = zIsUp ? sizeY : sizeZ;

        float canDiam = (widthSpan / cols) * 0.92f;
        float canHeight = (heightSpan / rows) * 0.95f;

        int index = 0;
        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                index++;
                GameObject canGo = new GameObject($"StockedCan_{index}");
                canGo.transform.SetParent(stepCansRoot.transform, false);

                float tx = (cols > 1) ? ((c + 0.5f) / cols - 0.5f) : 0f;
                float tr = (rows > 1) ? ((r + 0.5f) / rows - 0.5f) : 0f;

                Vector3 localPos = b.center;
                localPos.x += tx * widthSpan;
                if (zIsUp)
                {
                    localPos.z += tr * heightSpan;
                    localPos.y -= depthSpan * 0.22f;
                    canGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                else
                {
                    localPos.y += tr * heightSpan;
                    localPos.z -= depthSpan * 0.22f;
                    canGo.transform.localRotation = Quaternion.identity;
                }

                canGo.transform.localPosition = localPos;
                canGo.transform.localScale = new Vector3(canDiam, canHeight, canDiam);

                MeshFilter mf = canGo.AddComponent<MeshFilter>();
                mf.sharedMesh = unitCanMesh;

                MeshRenderer mr = canGo.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (canMats != null && canMats.Length >= 2)
                {
                    mr.sharedMaterials = new Material[] { canMats[0], canMats[1] };
                }
                else if (canMats != null && canMats.Length == 1)
                {
                    mr.sharedMaterials = new Material[] { canMats[0], canMats[0] };
                }

                canGo.SetActive(false);
                individualShelfCans.Add(canGo);
            }
        }
    }

    private void BuildProgressiveCansMeshes()
    {
        if (fullCansMesh == null || !fullCansMesh.isReadable) return;

        progressiveMeshes = new Mesh[goalCount];
        Vector3[] verts = fullCansMesh.vertices;
        Vector3[] normals = fullCansMesh.normals;
        Vector2[] uvs = fullCansMesh.uv;
        int subMeshCount = fullCansMesh.subMeshCount;

        float minX = fullCansMesh.bounds.min.x;
        float maxX = fullCansMesh.bounds.max.x;

        for (int step = 1; step <= goalCount; step++)
        {
            if (step == goalCount)
            {
                progressiveMeshes[step - 1] = fullCansMesh;
                continue;
            }

            float cutoffX = Mathf.Lerp(minX, maxX, (float)step / goalCount);
            Mesh m = new Mesh();
            m.name = $"Restock_Cans_Step_{step}";
            m.vertices = verts;
            if (normals != null && normals.Length == verts.Length) m.normals = normals;
            if (uvs != null && uvs.Length == verts.Length) m.uv = uvs;
            m.subMeshCount = subMeshCount;

            for (int sub = 0; sub < subMeshCount; sub++)
            {
                int[] tris = fullCansMesh.GetTriangles(sub);
                List<int> keptTris = new List<int>(tris.Length);

                for (int t = 0; t < tris.Length; t += 3)
                {
                    int i0 = tris[t];
                    int i1 = tris[t + 1];
                    int i2 = tris[t + 2];
                    float cx = (verts[i0].x + verts[i1].x + verts[i2].x) / 3f;
                    if (cx <= cutoffX)
                    {
                        keptTris.Add(i0);
                        keptTris.Add(i1);
                        keptTris.Add(i2);
                    }
                }

                m.SetTriangles(keptTris, sub);
            }

            m.RecalculateBounds();
            progressiveMeshes[step - 1] = m;
        }
    }

    public void UpdateShelfCansVisual()
    {
        if (restockCansSlot == null) return;

        if (stockedCount <= 0)
        {
            restockCansSlot.SetActive(false);
            if (stepCansRoot != null) stepCansRoot.SetActive(false);
        }
        else if (stockedCount >= goalCount)
        {
            if (stepCansRoot != null) stepCansRoot.SetActive(false);
            if (slotMeshFilter != null && fullCansMesh != null)
            {
                slotMeshFilter.sharedMesh = fullCansMesh;
            }
            restockCansSlot.SetActive(true);
        }
        else
        {
            if (stepCansRoot != null && individualShelfCans.Count > 0)
            {
                restockCansSlot.SetActive(false);
                stepCansRoot.SetActive(true);
                for (int i = 0; i < individualShelfCans.Count; i++)
                {
                    if (individualShelfCans[i] != null)
                    {
                        individualShelfCans[i].SetActive(i < stockedCount);
                    }
                }
            }
            else if (slotMeshFilter != null && progressiveMeshes != null && progressiveMeshes[stockedCount - 1] != null)
            {
                slotMeshFilter.sharedMesh = progressiveMeshes[stockedCount - 1];
                restockCansSlot.SetActive(true);
            }
        }
    }

    private bool CanInspectEmptyBay()
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        if (interactionTrigger == null)
        {
            SetupShelfSlotAndTrigger();
        }

        Bounds shelfBounds = ComputeCombinedShelfBounds();
        Vector3 bayOutward = shelfBounds.size.x < shelfBounds.size.z
            ? new Vector3(slotWorldCenter.x - shelfBounds.center.x, 0f, 0f)
            : new Vector3(0f, 0f, slotWorldCenter.z - shelfBounds.center.z);

        if (bayOutward.sqrMagnitude > 0.0016f)
        {
            Vector3 toPlayer = cam.transform.position - shelfBounds.center;
            toPlayer.y = 0f;
            if (Vector3.Dot(toPlayer, bayOutward) < -0.02f)
            {
                return false;
            }
        }

        Bounds inspectBounds = new Bounds(slotWorldCenter, new Vector3(1.05f, 0.72f, 1.05f));
        if (Vector3.Distance(cam.transform.position, inspectBounds.ClosestPoint(cam.transform.position)) > 1.75f)
        {
            return false;
        }

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        return inspectBounds.IntersectRay(ray, out float hitDist) && hitDist <= 1.75f;
    }

    public string GetPromptText()
    {
        if (NightShiftStoryController.IsDialogueActive)
        {
            return string.Empty;
        }

        if (NightShiftStoryController.IsInspectShelfTaskActive && !NightShiftStoryController.HasInspectedEmptyShelf)
        {
            return CanInspectEmptyBay() ? "E to inspect" : string.Empty;
        }

        if (IsFullyStocked || !NightShiftStoryController.IsStockingTaskActive)
        {
            return string.Empty;
        }

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, slotWorldCenter) > 1.75f)
        {
            return string.Empty;
        }

        RestockCardboardBox box = RestockCardboardBox.Instance;
        if (box == null) box = FindAnyObjectByType<RestockCardboardBox>();

        if (box != null && box.IsHoldingCan)
        {
            return $"E to stock can ({stockedCount + 1}/{goalCount})";
        }

        return string.Empty;
    }

    public void Interact()
    {
        if (NightShiftStoryController.IsDialogueActive) return;

        if (NightShiftStoryController.IsInspectShelfTaskActive && !NightShiftStoryController.HasInspectedEmptyShelf)
        {
            if (CanInspectEmptyBay() && NightShiftStoryController.Instance != null)
            {
                NightShiftStoryController.Instance.NotifyEmptyShelfInspected();
            }
            return;
        }

        if (IsFullyStocked || !NightShiftStoryController.IsStockingTaskActive) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, slotWorldCenter) > 1.75f)
        {
            return;
        }

        RestockCardboardBox box = RestockCardboardBox.Instance;
        if (box == null) box = FindAnyObjectByType<RestockCardboardBox>();

        if (box != null && box.IsHoldingCan)
        {
            // 1. Increment stockedCount FIRST so any downstream box calculations have the accurate stocked total
            stockedCount = Mathf.Clamp(stockedCount + 1, 0, goalCount);
            UpdateShelfCansVisual();

            // 2. Consume the held can (clears held viewmodel, removes CAN from hotbar, updates box visuals)
            box.ConsumeHeldCan();

            if (IsFullyStocked)
            {
                PlayGoalCompleteSound();
                box.OnShelfFullyStocked();
            }
            else
            {
                PlayCanPlaceSound();
            }
        }
    }

    private void Update()
    {
        if (restockCansSlot != null && fullCansMesh != null)
        {
            slotWorldCenter = restockCansSlot.transform.TransformPoint(fullCansMesh.bounds.center);
            Bounds sb = ComputeCombinedShelfBounds();
            shelfTopWorldPos = new Vector3(slotWorldCenter.x, sb.max.y + 0.14f, slotWorldCenter.z);
        }
    }

    private void EnsureFontLoaded()
    {
        if (horrorFont == null)
        {
            string[] fontNames = new string[] { "Courier New", "Lucida Console", "Consolas", "Courier" };
            horrorFont = Font.CreateDynamicFontFromOSFont(fontNames, 18);
        }
    }

    private void EnsureGuiStylesInOnGUI()
    {
        EnsureFontLoaded();

        if (cachedMainStyle == null || cachedShadowStyle == null)
        {
            cachedMainStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            if (horrorFont != null) cachedMainStyle.font = horrorFont;

            cachedShadowStyle = new GUIStyle(cachedMainStyle);
            cachedShadowStyle.normal.textColor = new Color(0.01f, 0.01f, 0.01f, 0.98f);
        }
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        // Do not show shelf stocking UI until the cleaning task is completed and stocking is active!
        if (!NightShiftStoryController.IsStockingTaskActive) return;

        // Once the 12 items are fully stocked, hide the stock text completely
        if (IsFullyStocked) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        float dist = Vector3.Distance(camPos, shelfTopWorldPos);
        if (dist > 10.0f) return;

        // Responsive 3D world anchor on top of the shelf:
        // When standing very close to the shelf (dist < 2.2m), gently lower the anchor slightly
        // toward the upper shelf edge so it remains comfortably on-screen above the shelf!
        float closeT = Mathf.InverseLerp(2.2f, 0.9f, dist);
        Vector3 responsiveTopAnchor = Vector3.Lerp(
            shelfTopWorldPos,
            new Vector3(slotWorldCenter.x, Mathf.Lerp(shelfTopWorldPos.y, slotWorldCenter.y + 0.32f, 0.65f), slotWorldCenter.z),
            closeT
        );

        Vector3 screenPos = cam.WorldToScreenPoint(responsiveTopAnchor);
        if (screenPos.z <= 0.12f) return;

        EnsureGuiStylesInOnGUI();

        // Smoothly scale font size with distance while keeping wordWrap=false and wide rect so it NEVER breaks
        int responsiveFontSize = Mathf.Clamp(Mathf.RoundToInt(40f / Mathf.Max(1.45f, dist)), 14, 24);
        cachedMainStyle.fontSize = responsiveFontSize;
        cachedShadowStyle.fontSize = responsiveFontSize;

        float guiX = Mathf.Floor(screenPos.x);
        float guiY = Mathf.Floor(Screen.height - screenPos.y);

        // Keep comfortably within screen bounds when standing close in front of the shelf
        if (dist < 2.3f)
        {
            guiX = Mathf.Clamp(guiX, 140f, Screen.width - 140f);
            guiY = Mathf.Clamp(guiY, 32f, Screen.height - 90f);
        }

        // Low-framerate stepped horror jitter
        float steppedTime = Mathf.Floor(Time.unscaledTime * 7f);
        float jitterX = ((int)steppedTime % 3 == 0) ? 1f : (((int)steppedTime % 5 == 0) ? -1f : 0f);
        float jitterY = ((int)steppedTime % 4 == 0) ? 1f : 0f;

        Color horrorTextCol = IsFullyStocked
            ? new Color(0.72f, 0.88f, 0.68f, 0.96f)
            : new Color(0.92f, 0.86f, 0.76f, 0.98f);

        cachedMainStyle.normal.textColor = horrorTextCol;

        string statusText = IsFullyStocked
            ? $"SHELF STOCKED: {stockedCount} / {goalCount}"
            : $"STOCK SHELF: {stockedCount} / {goalCount}";

        float boxW = 600f;
        float boxH = 50f;
        Rect labelRect = new Rect(guiX - boxW * 0.5f + jitterX, guiY - boxH * 0.5f + jitterY, boxW, boxH);

        // Chunky low-quality horror pixel outline & drop shadow
        GUI.Label(new Rect(labelRect.x - 1f, labelRect.y, labelRect.width, labelRect.height), statusText, cachedShadowStyle);
        GUI.Label(new Rect(labelRect.x + 1f, labelRect.y, labelRect.width, labelRect.height), statusText, cachedShadowStyle);
        GUI.Label(new Rect(labelRect.x, labelRect.y - 1f, labelRect.width, labelRect.height), statusText, cachedShadowStyle);
        GUI.Label(new Rect(labelRect.x, labelRect.y + 1f, labelRect.width, labelRect.height), statusText, cachedShadowStyle);
        GUI.Label(new Rect(labelRect.x + 2f, labelRect.y + 2f, labelRect.width, labelRect.height), statusText, cachedShadowStyle);

        // Foreground low-quality horror text
        GUI.Label(labelRect, statusText, cachedMainStyle);
    }

    private void PlayCanPlaceSound()
    {
        if (audioSource != null && canPlaceClip != null)
        {
            audioSource.pitch = Random.Range(0.94f, 1.08f);
            audioSource.PlayOneShot(canPlaceClip, 0.85f);
        }
    }

    private void PlayGoalCompleteSound()
    {
        if (audioSource != null && goalCompleteClip != null)
        {
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(goalCompleteClip, 0.95f);
        }
    }

    private void GenerateProceduralAudio()
    {
        int sampleRate = 44100;

        int count1 = (int)(sampleRate * 0.22f);
        float[] samples1 = new float[count1];
        for (int i = 0; i < count1; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 28f);
            float tinRing = Mathf.Sin(2f * Mathf.PI * 1120f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 1680f * t) * 0.2f;
            float shelfThud = Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.45f;
            samples1[i] = (tinRing + shelfThud) * env * 0.8f;
        }
        canPlaceClip = AudioClip.Create("CanShelfPlace", count1, 1, sampleRate, false);
        canPlaceClip.SetData(samples1, 0);

        int count2 = (int)(sampleRate * 0.45f);
        float[] samples2 = new float[count2];
        for (int i = 0; i < count2; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 10f);
            float freq = (t < 0.18f) ? 520f : 780f;
            samples2[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.55f;
        }
        goalCompleteClip = AudioClip.Create("ShelfStockComplete", count2, 1, sampleRate, false);
        goalCompleteClip.SetData(samples2, 0);
    }
}

public class ShelfBayTriggerForwarder : MonoBehaviour, IInteractable
{
    public ShelfRestockTarget target;

    public string GetPromptText()
    {
        return target != null ? target.GetPromptText() : string.Empty;
    }

    public void Interact()
    {
        if (target != null)
        {
            target.Interact();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Allows food items (Egg Tray with 8 Eggs, Pack of Bacon) inside the cabin refrigerator
/// to be picked up, equipped into HorrorHotbar, and held in first-person view.
/// Also provides procedural 3D sculpted mesh generation and runtime fridge repair.
/// </summary>
public class InteractableFoodItem : MonoBehaviour, IInteractable
{
    [Header("Item Identification")]
    public string itemName = "EGGS";
    public string promptText = "E to Pick Up Egg Tray";

    [Header("First-Person Viewmodel Placement")]
    public Vector3 viewmodelOffset = new Vector3(0.22f, -0.24f, 0.44f);
    public Vector3 viewmodelRotation = new Vector3(16f, -16f, 6f);
    public Vector3 viewmodelScale = new Vector3(0.82f, 0.82f, 0.82f);

    [Header("Interaction Distance")]
    public float maxInteractDistance = 2.6f;

    public static readonly List<InteractableFoodItem> AllItems = new List<InteractableFoodItem>();

    private bool isPickedUp = false;
    private bool isPlacedOnCounter = false;
    private bool hasTakenPortion = false;
    private bool isReturnedToFridge = false;
    private int assignedHotbarSlot = -1;
    private Collider[] allColliders;
    private Renderer[] allRenderers;
    private Transform playerCameraTransform;
    private CabinFridgeInteractable parentFridge;
    private Transform originalFridgeInterior;
    private CabinFridgeInteractable originalFridge;
    private Vector3 originalFridgeLocalPos;
    private Quaternion originalFridgeLocalRot = Quaternion.identity;
    private AudioSource audioSource;
    private AudioClip pickupAudioClip;

    public bool IsPickedUp => isPickedUp;
    public bool IsPlacedOnCounter => isPlacedOnCounter;
    public bool HasTakenPortion => hasTakenPortion;
    public bool IsReturnedToFridge => isReturnedToFridge;
    public int AssignedHotbarSlot => assignedHotbarSlot;
    public bool IsHeldInActiveHand =>
        isPickedUp && (HorrorHotbar.Instance == null || HorrorHotbar.Instance.IsSlotActive(assignedHotbarSlot));

    private void Awake()
    {
        if (!AllItems.Contains(this)) AllItems.Add(this);
        RefreshCachedComponents();
        parentFridge = GetComponentInParent<CabinFridgeInteractable>();
        CacheOriginalFridgePlacement();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0.5f;
            audioSource.playOnAwake = false;
            audioSource.volume = 0.85f;
        }

        GenerateProceduralPickupSound();
    }

    public void RefreshCachedComponents()
    {
        allColliders = GetComponentsInChildren<Collider>(true);
        allRenderers = GetComponentsInChildren<Renderer>(true);
        if (parentFridge == null) parentFridge = GetComponentInParent<CabinFridgeInteractable>();
    }

    public void CacheOriginalFridgePlacement()
    {
        if (originalFridgeInterior == null && transform.parent != null && transform.parent.name == "Fridge_Interior")
        {
            originalFridgeInterior = transform.parent;
            originalFridge = GetComponentInParent<CabinFridgeInteractable>();
            originalFridgeLocalPos = transform.localPosition;
            originalFridgeLocalRot = transform.localRotation;
        }
    }

    private void Start()
    {
        RefreshCachedComponents();

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null)
        {
            hotbar.OnSlotChanged -= HandleHotbarSlotChanged;
            hotbar.OnSlotChanged += HandleHotbarSlotChanged;
        }
    }

    private void OnDestroy()
    {
        AllItems.Remove(this);
        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.OnSlotChanged -= HandleHotbarSlotChanged;
        }
    }

    public static InteractableFoodItem FindPickedUpItem(string targetItemName)
    {
        for (int i = 0; i < AllItems.Count; i++)
        {
            InteractableFoodItem item = AllItems[i];
            if (item != null && item.isPickedUp && !item.isPlacedOnCounter &&
                string.Equals(item.itemName, targetItemName, System.StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }
        return null;
    }

    public static InteractableFoodItem FindActiveOrHeldFoodItem()
    {
        InteractableFoodItem eggs = FindPickedUpItem("EGGS");
        InteractableFoodItem bacon = FindPickedUpItem("BACON");

        if (eggs != null && eggs.IsHeldInActiveHand) return eggs;
        if (bacon != null && bacon.IsHeldInActiveHand) return bacon;
        if (eggs != null) return eggs;
        if (bacon != null) return bacon;

        return null;
    }

    public static bool IsItemPlacedOnCounter(string targetItemName)
    {
        for (int i = 0; i < AllItems.Count; i++)
        {
            InteractableFoodItem item = AllItems[i];
            if (item != null && item.IsPlacedOnCounter &&
                string.Equals(item.itemName, targetItemName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Scene search fallback
        InteractableFoodItem[] sceneItems = Object.FindObjectsByType<InteractableFoodItem>(FindObjectsInactive.Include);
        for (int i = 0; i < sceneItems.Length; i++)
        {
            InteractableFoodItem item = sceneItems[i];
            if (item != null && item.IsPlacedOnCounter &&
                string.Equals(item.itemName, targetItemName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static bool AreBothEggAndBaconPlacedOnCounter()
    {
        return IsItemPlacedOnCounter("EGGS") && IsItemPlacedOnCounter("BACON");
    }

    public static bool IsItemReturnedToFridge(string targetItemName)
    {
        for (int i = 0; i < AllItems.Count; i++)
        {
            InteractableFoodItem item = AllItems[i];
            if (item != null && item.IsReturnedToFridge &&
                string.Equals(item.itemName, targetItemName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        InteractableFoodItem[] sceneItems = Object.FindObjectsByType<InteractableFoodItem>(FindObjectsInactive.Include);
        for (int i = 0; i < sceneItems.Length; i++)
        {
            InteractableFoodItem item = sceneItems[i];
            if (item != null && item.IsReturnedToFridge &&
                string.Equals(item.itemName, targetItemName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static bool AreBothEggAndBaconReturnedToFridge()
    {
        return IsItemReturnedToFridge("EGGS") && IsItemReturnedToFridge("BACON");
    }

    public void PlaceOnCounter(Transform counterParent, Vector3 localPos, Quaternion localRot)
    {
        CacheOriginalFridgePlacement();
        isPickedUp = false;
        isPlacedOnCounter = true;
        isReturnedToFridge = false;
        parentFridge = null;

        if (HorrorHotbar.Instance != null && assignedHotbarSlot >= 0)
        {
            HorrorHotbar.Instance.ClearSlot(assignedHotbarSlot);
        }
        assignedHotbarSlot = -1;

        transform.SetParent(counterParent, false);
        transform.localPosition = localPos;
        transform.localRotation = localRot;
        transform.localScale = Vector3.one;

        RefreshCachedComponents();
        if (allColliders != null)
        {
            for (int i = 0; i < allColliders.Length; i++)
            {
                if (allColliders[i] != null) allColliders[i].enabled = true;
            }
        }
        SetRenderersVisible(true);

        if (audioSource != null && pickupAudioClip != null)
        {
            audioSource.PlayOneShot(pickupAudioClip, 0.85f);
        }

        if (AreBothEggAndBaconPlacedOnCounter())
        {
            HouseSceneStoryController.NotifyFoodPlacedOnCounter();
        }
    }

    public void ReturnToFridge()
    {
        isPickedUp = false;
        isPlacedOnCounter = false;
        isReturnedToFridge = true;

        if (HorrorHotbar.Instance != null && assignedHotbarSlot >= 0)
        {
            HorrorHotbar.Instance.ClearSlot(assignedHotbarSlot);
        }
        assignedHotbarSlot = -1;

        Transform targetInterior = originalFridgeInterior;
        if (targetInterior == null && originalFridge != null)
        {
            targetInterior = originalFridge.transform.Find("Fridge_Interior");
        }
        if (targetInterior == null)
        {
            GameObject fridgeObj = GameObject.Find("PFB_Fridge");
            if (fridgeObj != null) targetInterior = fridgeObj.transform.Find("Fridge_Interior");
        }

        if (targetInterior != null)
        {
            transform.SetParent(targetInterior, false);
            transform.localPosition = originalFridgeLocalPos;
            transform.localRotation = originalFridgeLocalRot;
            transform.localScale = Vector3.one;
        }

        parentFridge = originalFridge != null ? originalFridge : GetComponentInParent<CabinFridgeInteractable>();

        RefreshCachedComponents();
        if (allColliders != null)
        {
            for (int i = 0; i < allColliders.Length; i++)
            {
                if (allColliders[i] != null) allColliders[i].enabled = false;
            }
        }
        SetRenderersVisible(true);

        if (audioSource != null && pickupAudioClip != null)
        {
            audioSource.PlayOneShot(pickupAudioClip, 0.85f);
        }

        if (AreBothEggAndBaconReturnedToFridge())
        {
            HouseSceneStoryController.NotifyFoodReturnedToFridge();
        }
    }

    private void LateUpdate()
    {
        if (!isPickedUp) return;

        if (IsHeldInActiveHand && playerCameraTransform != null)
        {
            transform.localPosition = viewmodelOffset;
            transform.localRotation = Quaternion.Euler(viewmodelRotation);
            transform.localScale = viewmodelScale;
        }
    }

    private void HandleHotbarSlotChanged(int activeSlot)
    {
        if (!isPickedUp) return;

        bool isThisSlotActive = (activeSlot == assignedHotbarSlot);
        SetRenderersVisible(isThisSlotActive);
    }

    public void SetRenderersVisible(bool visible)
    {
        if (allRenderers == null || allRenderers.Length == 0)
        {
            allRenderers = GetComponentsInChildren<Renderer>(true);
        }

        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null) allRenderers[i].enabled = visible;
        }
    }

    public string GetPromptText()
    {
        if (isPickedUp || isReturnedToFridge) return string.Empty;
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, transform.position) > maxInteractDistance)
        {
            return string.Empty;
        }

        // Once placed on KitchenBench_base, allow taking 1 Egg / 2 Bacons during cooking,
        // or picking the whole Egg Tray / Bacon Pack back up once the fridge-return task starts!
        if (isPlacedOnCounter)
        {
            if (HouseSceneStoryController.IsReturnFoodToFridgeUnlocked)
            {
                if (HorrorHotbar.IsInventoryFull)
                {
                    return "Inventory Full";
                }
                return promptText;
            }

            if (hasTakenPortion) return string.Empty;

            if (HorrorHotbar.IsInventoryFull)
            {
                return "Inventory Full";
            }

            if (string.Equals(itemName, "EGGS", System.StringComparison.OrdinalIgnoreCase))
            {
                return "E to Pick Up 1 Egg";
            }
            if (string.Equals(itemName, "BACON", System.StringComparison.OrdinalIgnoreCase))
            {
                return "E to Pick Up 2 Bacons";
            }
            return string.Empty;
        }

        if (parentFridge == null) parentFridge = GetComponentInParent<CabinFridgeInteractable>();
        if (parentFridge != null)
        {
            if (!parentFridge.isOpen || !HouseSceneStoryController.IsFoodPickupUnlocked)
            {
                return string.Empty;
            }
        }

        if (HorrorHotbar.IsInventoryFull)
        {
            return "Inventory Full";
        }

        return promptText;
    }

    public void Interact()
    {
        if (isPickedUp || isReturnedToFridge) return;
        if (HouseSceneStoryController.IsDialogueActive) return;
        if (HorrorHotbar.IsInventoryFull) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return;
        if (Vector3.Distance(cam.transform.position, transform.position) > maxInteractDistance) return;

        if (isPlacedOnCounter)
        {
            if (HouseSceneStoryController.IsReturnFoodToFridgeUnlocked)
            {
                PickUp(cam.transform);
                return;
            }

            if (hasTakenPortion) return;

            if (string.Equals(itemName, "EGGS", System.StringComparison.OrdinalIgnoreCase))
            {
                hasTakenPortion = true;
                Transform egg1 = transform.Find("Egg_01");
                if (egg1 != null) egg1.gameObject.SetActive(false);

                CabinCookingSystem.EquipSingleEggPortion(cam.transform);
                if (audioSource != null && pickupAudioClip != null)
                {
                    audioSource.PlayOneShot(pickupAudioClip, 0.85f);
                }
                return;
            }

            if (string.Equals(itemName, "BACON", System.StringComparison.OrdinalIgnoreCase))
            {
                hasTakenPortion = true;
                Transform s1 = transform.Find("Bacon_Strips_Group/BaconStrip_01");
                Transform s2 = transform.Find("Bacon_Strips_Group/BaconStrip_02");
                if (s1 != null) s1.gameObject.SetActive(false);
                if (s2 != null) s2.gameObject.SetActive(false);

                CabinCookingSystem.EquipTwoBaconsPortion(cam.transform);
                if (audioSource != null && pickupAudioClip != null)
                {
                    audioSource.PlayOneShot(pickupAudioClip, 0.85f);
                }
                return;
            }
            return;
        }

        if (parentFridge == null) parentFridge = GetComponentInParent<CabinFridgeInteractable>();
        if (parentFridge != null)
        {
            if (!parentFridge.isOpen || !HouseSceneStoryController.IsFoodPickupUnlocked) return;
        }

        PickUp(cam.transform);
        if (!HouseSceneStoryController.IsReturnFoodToFridgeUnlocked)
        {
            HouseSceneStoryController.NotifyFoodItemPickedUp();
        }
    }

    public void PickUp(Transform camTransform)
    {
        CacheOriginalFridgePlacement();
        RefreshCachedComponents();
        playerCameraTransform = camTransform;
        isPickedUp = true;
        isPlacedOnCounter = false;
        isReturnedToFridge = false;

        if (allColliders != null)
        {
            for (int i = 0; i < allColliders.Length; i++)
            {
                if (allColliders[i] != null) allColliders[i].enabled = false;
            }
        }

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        assignedHotbarSlot = 0;
        if (hotbar != null)
        {
            for (int s = 0; s < hotbar.slotCount; s++)
            {
                if (!hotbar.HasItem(s))
                {
                    assignedHotbarSlot = s;
                    break;
                }
            }

            hotbar.SetSlot(assignedHotbarSlot, itemName);
            hotbar.SetActiveSlot(assignedHotbarSlot);
        }

        transform.SetParent(playerCameraTransform, false);
        transform.localPosition = viewmodelOffset;
        transform.localRotation = Quaternion.Euler(viewmodelRotation);
        transform.localScale = viewmodelScale;

        SetRenderersVisible(true);

        if (audioSource != null && pickupAudioClip != null)
        {
            audioSource.PlayOneShot(pickupAudioClip, 0.85f);
        }
    }

    private void GenerateProceduralPickupSound()
    {
        int sampleRate = 44100;
        float dur = 0.22f;
        int samples = Mathf.RoundToInt(dur * sampleRate);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float rustle = (Random.value * 2f - 1f) * Mathf.Exp(-t * 24f) * 0.35f;
            float tap = Mathf.Sin(2f * Mathf.PI * 340f * t) * Mathf.Exp(-t * 40f) * 0.25f;
            data[i] = Mathf.Clamp(rustle + tap, -1f, 1f);
        }

        pickupAudioClip = AudioClip.Create("Food_Pickup_" + itemName, samples, 1, sampleRate, false);
        pickupAudioClip.SetData(data, 0);
    }

    // =========================================================================
    // RUNTIME FRIDGE REPAIR & 3D SCULPTED FOOD ITEM BUILDER
    // =========================================================================

    public static void RepairAndPopulateFridge(GameObject fridgeObj)
    {
        if (fridgeObj == null) return;

        // 1. Repair split meshes using 3D vertex coordinate classification
        MeshFilter bodyMf = fridgeObj.GetComponent<MeshFilter>();
        MeshRenderer bodyMr = fridgeObj.GetComponent<MeshRenderer>();
        Material origMat = bodyMr != null ? bodyMr.sharedMaterial : null;

        Transform pivot = fridgeObj.transform.Find("Door_Pivot");
        if (pivot == null)
        {
            GameObject pivotGo = new GameObject("Door_Pivot");
            pivot = pivotGo.transform;
            pivot.SetParent(fridgeObj.transform, false);
        }

        Transform doorModel = pivot.Find("Door_Model");
        if (doorModel == null)
        {
            GameObject dmGo = new GameObject("Door_Model");
            doorModel = dmGo.transform;
            doorModel.SetParent(pivot, false);
        }

        MeshFilter doorMf = doorModel.GetComponent<MeshFilter>();
        if (doorMf == null) doorMf = doorModel.gameObject.AddComponent<MeshFilter>();

        MeshRenderer doorMr = doorModel.GetComponent<MeshRenderer>();
        if (doorMr == null) doorMr = doorModel.gameObject.AddComponent<MeshRenderer>();
        if (origMat != null) doorMr.sharedMaterial = origMat;

        // Reconstruct full 196-triangle fridge mesh from current body + door meshes and split cleanly by vertex coords
        if (bodyMf != null && bodyMf.sharedMesh != null)
        {
            ReSplitFridgeMeshesByCoordinates(bodyMf, doorMf);
        }

        // Right-hand hinge position (X = -0.400m is the right edge when facing the fridge front +Z)
        pivot.localPosition = new Vector3(-0.400f, 0f, 0.436f);
        CabinFridgeInteractable cfi = fridgeObj.GetComponent<CabinFridgeInteractable>();
        bool currentlyOpen = cfi != null && cfi.isOpen;
        pivot.localRotation = Quaternion.Euler(0f, currentlyOpen ? -105f : 0f, 0f);

        // Offset Door_Model by (+0.400, 0, -0.436) so at 0 deg it aligns 100% flush with body
        doorModel.localPosition = new Vector3(0.400f, 0f, -0.436f);
        doorModel.localRotation = Quaternion.identity;

        // Door trigger collider on Door_Pivot (rotates with the door)
        BoxCollider doorCol = pivot.GetComponent<BoxCollider>();
        if (doorCol == null) doorCol = pivot.gameObject.AddComponent<BoxCollider>();
        doorCol.isTrigger = true;
        doorCol.size = new Vector3(0.84f, 1.20f, 0.14f);
        doorCol.center = new Vector3(0.40f, 1.35f, 0f);

        // Body physics collider: only covers lower freezer + back wall so upper cavity is open for item raycasts!
        BoxCollider bodyCol = fridgeObj.GetComponent<BoxCollider>();
        if (bodyCol == null) bodyCol = fridgeObj.AddComponent<BoxCollider>();
        bodyCol.isTrigger = false;
        bodyCol.size = new Vector3(0.82f, 0.78f, 0.90f);
        bodyCol.center = new Vector3(0.01f, 0.39f, -0.02f);

        // Rebuild Door Inner Lining
        Material whiteMat = GetOrCreateMaterial("Fridge_Interior_White", null, new Color(0.94f, 0.95f, 0.96f), 0.65f);
        Material shelfMat = GetOrCreateMaterial("Fridge_Glass_Shelf", null, new Color(0.82f, 0.91f, 0.94f), 0.90f);

        Transform lining = doorModel.Find("Door_InnerLining");
        if (lining != null) DestroyImmediateSafe(lining.gameObject);
        BuildDoorInnerLining(doorModel, whiteMat);

        // Rebuild Interior Cavity & Shelves
        Transform interior = fridgeObj.transform.Find("Fridge_Interior");
        if (interior != null) DestroyImmediateSafe(interior.gameObject);

        Light interiorLight = BuildInteriorCavityAndFood(fridgeObj.transform, whiteMat, shelfMat);

        if (cfi == null) cfi = fridgeObj.AddComponent<CabinFridgeInteractable>();
        cfi.doorPivot = pivot;
        cfi.interiorLight = interiorLight;
        cfi.closedAngleY = 0f;
        cfi.openAngleY = -105f;
        cfi.swingSpeed = 5.0f;
        cfi.maxLightIntensity = 0.55f;
    }

    private static void ReSplitFridgeMeshesByCoordinates(MeshFilter bodyMf, MeshFilter doorMf)
    {
        List<Vector3> allV = new List<Vector3>();
        List<Vector3> allN = new List<Vector3>();
        List<Vector2> allUV = new List<Vector2>();
        List<Vector4> allTan = new List<Vector4>();
        List<int> allTris = new List<int>();

        AppendMeshData(bodyMf.sharedMesh, allV, allN, allUV, allTan, allTris);
        if (doorMf.sharedMesh != null && doorMf.sharedMesh != bodyMf.sharedMesh)
        {
            AppendMeshData(doorMf.sharedMesh, allV, allN, allUV, allTan, allTris);
        }

        List<int> bodyTris = new List<int>();
        List<int> doorTris = new List<int>();

        int triCount = allTris.Count / 3;
        for (int i = 0; i < triCount; i++)
        {
            int i0 = allTris[i * 3];
            int i1 = allTris[i * 3 + 1];
            int i2 = allTris[i * 3 + 2];

            Vector3 p0 = allV[i0];
            Vector3 p1 = allV[i1];
            Vector3 p2 = allV[i2];

            // Top door slab, bevels, and top handle all have Y > 0.758m and Z > 0.415m
            bool isTopDoor = (p0.y > 0.758f && p1.y > 0.758f && p2.y > 0.758f) &&
                             (p0.z > 0.415f && p1.z > 0.415f && p2.z > 0.415f);

            if (isTopDoor)
            {
                doorTris.Add(i0);
                doorTris.Add(i1);
                doorTris.Add(i2);
            }
            else
            {
                bodyTris.Add(i0);
                bodyTris.Add(i1);
                bodyTris.Add(i2);
            }
        }

        if (doorTris.Count > 0 && bodyTris.Count > 0)
        {
            bodyMf.sharedMesh = ExtractSubMesh(allV, allN, allUV, allTan, bodyTris, "Fridge_Body_Clean");
            doorMf.sharedMesh = ExtractSubMesh(allV, allN, allUV, allTan, doorTris, "Fridge_Door_Clean");
        }
    }

    private static void AppendMeshData(Mesh m, List<Vector3> allV, List<Vector3> allN, List<Vector2> allUV, List<Vector4> allTan, List<int> allTris)
    {
        if (m == null) return;
        int offset = allV.Count;
        Vector3[] v = m.vertices;
        Vector3[] n = m.normals;
        Vector2[] uv = m.uv;
        Vector4[] tan = m.tangents;
        int[] tris = m.triangles;

        for (int i = 0; i < v.Length; i++)
        {
            allV.Add(v[i]);
            allN.Add(n != null && i < n.Length ? n[i] : Vector3.up);
            allUV.Add(uv != null && i < uv.Length ? uv[i] : Vector2.zero);
            allTan.Add(tan != null && i < tan.Length ? tan[i] : new Vector4(1, 0, 0, 1));
        }

        for (int i = 0; i < tris.Length; i++)
        {
            allTris.Add(offset + tris[i]);
        }
    }

    private static Mesh ExtractSubMesh(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<Vector4> tan, List<int> triIndices, string name)
    {
        Dictionary<int, int> remap = new Dictionary<int, int>();
        List<Vector3> newV = new List<Vector3>();
        List<Vector3> newN = new List<Vector3>();
        List<Vector2> newUV = new List<Vector2>();
        List<Vector4> newTan = new List<Vector4>();
        List<int> newTris = new List<int>();

        for (int i = 0; i < triIndices.Count; i++)
        {
            int oldIdx = triIndices[i];
            if (!remap.TryGetValue(oldIdx, out int newIdx))
            {
                newIdx = newV.Count;
                remap[oldIdx] = newIdx;
                newV.Add(v[oldIdx]);
                newN.Add(n[oldIdx]);
                newUV.Add(uv[oldIdx]);
                newTan.Add(tan[oldIdx]);
            }
            newTris.Add(newIdx);
        }

        Mesh m = new Mesh();
        m.name = name;
        m.vertices = newV.ToArray();
        m.normals = newN.ToArray();
        m.uv = newUV.ToArray();
        m.tangents = newTan.ToArray();
        m.triangles = newTris.ToArray();
        m.RecalculateBounds();
        return m;
    }

    private static void BuildDoorInnerLining(Transform doorModel, Material whiteMat)
    {
        GameObject liningGo = new GameObject("Door_InnerLining");
        Transform lining = liningGo.transform;
        lining.SetParent(doorModel, false);

        CreateBoxBlock(lining, "Panel_Back", new Vector3(0.01f, 1.345f, 0.412f), new Vector3(0.76f, 1.14f, 0.018f), whiteMat);
        CreateBoxBlock(lining, "Door_Tray_Upper", new Vector3(0.01f, 1.55f, 0.375f), new Vector3(0.68f, 0.07f, 0.06f), whiteMat);
        CreateBoxBlock(lining, "Door_Tray_Lower", new Vector3(0.01f, 1.10f, 0.375f), new Vector3(0.68f, 0.07f, 0.06f), whiteMat);
    }

    private static Light BuildInteriorCavityAndFood(Transform fridgeRoot, Material whiteMat, Material shelfMat)
    {
        GameObject interiorGo = new GameObject("Fridge_Interior");
        Transform interior = interiorGo.transform;
        interior.SetParent(fridgeRoot, false);

        // Cavity liner walls
        CreateBoxBlock(interior, "Wall_Back", new Vector3(0.01f, 1.345f, -0.415f), new Vector3(0.74f, 1.14f, 0.02f), whiteMat);
        CreateBoxBlock(interior, "Wall_Right", new Vector3(-0.365f, 1.345f, -0.025f), new Vector3(0.02f, 1.14f, 0.78f), whiteMat);
        CreateBoxBlock(interior, "Wall_Left", new Vector3(0.385f, 1.345f, -0.025f), new Vector3(0.02f, 1.14f, 0.78f), whiteMat);
        CreateBoxBlock(interior, "Ceiling", new Vector3(0.01f, 1.905f, -0.025f), new Vector3(0.74f, 0.02f, 0.78f), whiteMat);
        CreateBoxBlock(interior, "Floor", new Vector3(0.01f, 0.775f, -0.025f), new Vector3(0.74f, 0.02f, 0.78f), whiteMat);

        // Shelves
        CreateBoxBlock(interior, "Shelf_Middle", new Vector3(0.01f, 1.15f, -0.03f), new Vector3(0.72f, 0.014f, 0.72f), shelfMat);
        CreateBoxBlock(interior, "Shelf_Upper", new Vector3(0.01f, 1.50f, -0.03f), new Vector3(0.72f, 0.014f, 0.72f), shelfMat);

        // Interior Light
        GameObject lightGo = new GameObject("Fridge_Interior_Light");
        lightGo.transform.SetParent(interior, false);
        lightGo.transform.localPosition = new Vector3(0.01f, 1.83f, 0.08f);

        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1.0f, 0.96f, 0.88f);
        l.range = 1.35f;
        l.intensity = 0.65f;
        l.shadows = LightShadows.None;
        l.enabled = false;

        // Spawn 3D Molded Egg Tray (with 8 Eggs) and 3D Shingled Pack of Bacon
        EnsureFridgeFoodItems(interior);

        // Trigger zone inside upper fridge cavity so Egg Tray & Bacon Pack can be returned to the shelf
        EnsureFridgeReturnZone(interior, fridgeRoot.GetComponent<CabinFridgeInteractable>());

        return l;
    }

    public static void EnsureFridgeReturnZone(Transform interior, CabinFridgeInteractable fridge)
    {
        if (interior == null) return;

        Transform existing = interior.Find("Fridge_ReturnZone");
        GameObject zoneGo = existing != null ? existing.gameObject : new GameObject("Fridge_ReturnZone");
        if (existing == null)
        {
            zoneGo.transform.SetParent(interior, false);
        }
        zoneGo.transform.localPosition = Vector3.zero;
        zoneGo.transform.localRotation = Quaternion.identity;
        zoneGo.transform.localScale = Vector3.one;

        BoxCollider zoneCol = zoneGo.GetComponent<BoxCollider>();
        if (zoneCol == null) zoneCol = zoneGo.AddComponent<BoxCollider>();
        zoneCol.isTrigger = true;
        zoneCol.center = new Vector3(0.01f, 1.34f, -0.02f);
        zoneCol.size = new Vector3(0.70f, 0.96f, 0.72f);

        CabinFridgeReturnSpot spot = zoneGo.GetComponent<CabinFridgeReturnSpot>();
        if (spot == null) spot = zoneGo.AddComponent<CabinFridgeReturnSpot>();
        if (fridge != null) spot.parentFridge = fridge;
    }

    private static void CreateBoxBlock(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPos;
        cube.transform.localScale = localScale;
        cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
        DestroyImmediateSafe(cube.GetComponent<BoxCollider>());
    }

    private static void DestroyImmediateSafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    public static void EnsureFridgeFoodItems(Transform interior)
    {
        if (interior == null) return;

        Transform cans = interior.Find("Stocked_Cans");
        if (cans != null) DestroyImmediateSafe(cans.gameObject);

        Transform oldEggTray = interior.Find("PFB_EggTray");
        if (oldEggTray != null) DestroyImmediateSafe(oldEggTray.gameObject);

        Transform oldBacon = interior.Find("PFB_BaconPack");
        if (oldBacon != null) DestroyImmediateSafe(oldBacon.gameObject);

        // Spawn Egg Tray on right half of middle shelf (x = -0.14) and Bacon Pack on left half (x = +0.15)
        CreateEggTray(interior, new Vector3(-0.14f, 1.158f, 0.12f));
        CreateBaconPack(interior, new Vector3(0.15f, 1.158f, 0.12f));
    }

    public static Transform CreateEggTray(Transform parent, Vector3 localPos)
    {
        GameObject trayGo = new GameObject("PFB_EggTray");
        trayGo.transform.SetParent(parent, false);
        trayGo.transform.localPosition = localPos;
        trayGo.transform.localRotation = Quaternion.Euler(0f, -8f, 0f);
        trayGo.transform.localScale = Vector3.one;

        MeshFilter mf = trayGo.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildEggTrayMesh();

        MeshRenderer mr = trayGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetOrCreateMaterial("EggCarton_MAT", "Assets/FurnishedCabin/Textures/EggCarton_Albedo.png", Color.white, 0.08f);

        // 8 Organic 3D Eggs (2 rows of 4) nestled into the 8 molded cups
        Mesh eggMesh = BuildOrganicEggMesh();
        float[] cupX = new float[] { -0.072f, -0.024f, 0.024f, 0.072f };
        float[] cupZ = new float[] { -0.026f, 0.026f };

        Color[] shellTints = new Color[]
        {
            new Color(1.00f, 0.98f, 0.95f),
            new Color(0.96f, 0.91f, 0.86f),
            new Color(1.02f, 0.99f, 0.96f),
            new Color(0.94f, 0.88f, 0.82f),
            new Color(0.98f, 0.94f, 0.90f),
            new Color(1.00f, 0.96f, 0.92f),
            new Color(0.95f, 0.89f, 0.84f),
            new Color(0.99f, 0.95f, 0.91f)
        };

        int eggCount = 0;
        for (int r = 0; r < cupZ.Length; r++)
        {
            for (int c = 0; c < cupX.Length; c++)
            {
                eggCount++;
                GameObject egg = new GameObject($"Egg_0{eggCount}");
                egg.transform.SetParent(trayGo.transform, false);

                float yaw = (eggCount * 47f) % 360f;
                float tiltX = ((eggCount % 3) - 1) * 2.2f;
                float tiltZ = (((eggCount + 1) % 3) - 1) * 2.0f;

                egg.transform.localPosition = new Vector3(cupX[c], 0.029f, cupZ[r]);
                egg.transform.localRotation = Quaternion.Euler(tiltX, yaw, tiltZ);
                egg.transform.localScale = Vector3.one;

                MeshFilter eggMf = egg.AddComponent<MeshFilter>();
                eggMf.sharedMesh = eggMesh;

                MeshRenderer eggMr = egg.AddComponent<MeshRenderer>();
                eggMr.sharedMaterial = GetOrCreateMaterial($"EggShell_MAT_{eggCount}", "Assets/FurnishedCabin/Textures/EggShell_Albedo.png", shellTints[eggCount - 1], 0.36f);
            }
        }

        BoxCollider bc = trayGo.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.24f, 0.11f, 0.15f);
        bc.center = new Vector3(0f, 0.045f, 0f);

        InteractableFoodItem food = trayGo.AddComponent<InteractableFoodItem>();
        food.itemName = "EGGS";
        food.promptText = "E to Pick Up Egg Tray";
        food.viewmodelOffset = new Vector3(0.22f, -0.24f, 0.44f);
        food.viewmodelRotation = new Vector3(16f, -16f, 6f);
        food.viewmodelScale = new Vector3(0.85f, 0.85f, 0.85f);

        return trayGo.transform;
    }

    public static Transform CreateBaconPack(Transform parent, Vector3 localPos)
    {
        GameObject baconGo = new GameObject("PFB_BaconPack");
        baconGo.transform.SetParent(parent, false);
        baconGo.transform.localPosition = localPos;
        baconGo.transform.localRotation = Quaternion.Euler(0f, 7f, 0f);
        baconGo.transform.localScale = Vector3.one;

        // 1. Dark Butcher Backing Tray
        MeshFilter mf = baconGo.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildBaconBackingBoardMesh();

        MeshRenderer mr = baconGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetOrCreateMaterial("BaconBoard_MAT", null, new Color(0.11f, 0.12f, 0.14f), 0.45f, 0.05f);

        // 2. Seven 3D Sculpted Wavy Shingled Bacon Strips
        Material stripMat = GetOrCreateMaterial("BaconStrip_MAT", "Assets/FurnishedCabin/Textures/BaconStrip_Albedo.png", Color.white, 0.68f, 0.04f);
        int numStrips = 7;
        for (int s = 0; s < numStrips; s++)
        {
            GameObject stripGo = new GameObject($"BaconStrip_0{s + 1}");
            stripGo.transform.SetParent(baconGo.transform, false);

            // Shingled arrangement from Z = -0.045 to +0.045
            float zPos = Mathf.Lerp(-0.044f, 0.044f, (float)s / (numStrips - 1));
            float yPos = 0.006f + (s * 0.0006f);
            stripGo.transform.localPosition = new Vector3(0.012f, yPos, zPos);
            // Slight shingle pitch so each slice overlaps the previous slice
            stripGo.transform.localRotation = Quaternion.Euler(-7.5f, 0f, 0f);
            stripGo.transform.localScale = Vector3.one;

            MeshFilter sMf = stripGo.AddComponent<MeshFilter>();
            sMf.sharedMesh = BuildSculptedBaconStripMesh(s * 1.3f);

            MeshRenderer sMr = stripGo.AddComponent<MeshRenderer>();
            sMr.sharedMaterial = stripMat;
        }

        // 3. Printed Butcher Cardboard Label Band wrapping the left side of the package
        GameObject labelGo = new GameObject("Bacon_LabelSleeve");
        labelGo.transform.SetParent(baconGo.transform, false);
        labelGo.transform.localPosition = Vector3.zero;
        labelGo.transform.localRotation = Quaternion.identity;

        MeshFilter lMf = labelGo.AddComponent<MeshFilter>();
        lMf.sharedMesh = BuildBaconLabelSleeveMesh();

        MeshRenderer lMr = labelGo.AddComponent<MeshRenderer>();
        lMr.sharedMaterial = GetOrCreateMaterial("BaconLabel_MAT", "Assets/FurnishedCabin/Textures/BaconPack_Albedo.png", Color.white, 0.55f, 0.05f);

        // Interaction Collider
        BoxCollider bc = baconGo.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.25f, 0.06f, 0.17f);
        bc.center = new Vector3(0f, 0.02f, 0f);

        InteractableFoodItem food = baconGo.AddComponent<InteractableFoodItem>();
        food.itemName = "BACON";
        food.promptText = "E to Pick Up Bacon";
        food.viewmodelOffset = new Vector3(0.22f, -0.24f, 0.44f);
        food.viewmodelRotation = new Vector3(22f, -20f, 10f);
        food.viewmodelScale = new Vector3(0.85f, 0.85f, 0.85f);

        return baconGo.transform;
    }

    public static Material GetOrCreateMaterial(string matName, string texPath, Color tint, float smoothness, float metallic = 0f)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");

        Material mat = new Material(s);
        mat.name = matName;
        mat.color = tint;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);

        if (!string.IsNullOrEmpty(texPath) && System.IO.File.Exists(texPath))
        {
            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(texPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (tex.LoadImage(bytes))
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    mat.mainTexture = tex;
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                }
            }
            catch { }
        }

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

        return mat;
    }

    // =========================================================================
    // HIGH-DETAIL 3D SCULPTED MESH GENERATORS (WITH GUARANTEED OUTWARD WINDING)
    // =========================================================================

    public static void AddTriangleSafe(List<Vector3> verts, List<int> tris, int i0, int i1, int i2, Vector3 expectedNormal)
    {
        Vector3 p0 = verts[i0];
        Vector3 p1 = verts[i1];
        Vector3 p2 = verts[i2];
        Vector3 cross = Vector3.Cross(p1 - p0, p2 - p0);
        if (Vector3.Dot(cross, expectedNormal) < 0f)
        {
            tris.Add(i0);
            tris.Add(i2);
            tris.Add(i1);
        }
        else
        {
            tris.Add(i0);
            tris.Add(i1);
            tris.Add(i2);
        }
    }

    public static void AddQuadSafe(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 expectedNormal,
        Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
    {
        int start = verts.Count;
        verts.Add(v0);
        verts.Add(v1);
        verts.Add(v2);
        verts.Add(v3);

        uvs.Add(uv0);
        uvs.Add(uv1);
        uvs.Add(uv2);
        uvs.Add(uv3);

        AddTriangleSafe(verts, tris, start, start + 1, start + 2, expectedNormal);
        AddTriangleSafe(verts, tris, start, start + 2, start + 3, expectedNormal);
    }

    public static Mesh BuildOrganicEggMesh()
    {
        Mesh m = new Mesh();
        m.name = "Egg_Mesh";

        int latSegments = 22;
        int lonSegments = 26;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float rMax = 0.0205f; // 4.1cm diameter

        for (int i = 0; i <= latSegments; i++)
        {
            float v = (float)i / latSegments;
            float normY = Mathf.Lerp(-1f, 1f, v);

            // Authentic biological chicken egg profile
            float yPos = normY >= 0f ? normY * 0.030f : normY * 0.024f;
            float sinLat = Mathf.Sqrt(Mathf.Max(0f, 1f - normY * normY));
            float radius = rMax * sinLat * (1f - 0.15f * normY);

            for (int j = 0; j <= lonSegments; j++)
            {
                float u = (float)j / lonSegments;
                float angle = u * Mathf.PI * 2f;

                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                verts.Add(new Vector3(x, yPos, z));
                uvs.Add(new Vector2(u, v));
            }
        }

        for (int i = 0; i < latSegments; i++)
        {
            for (int j = 0; j < lonSegments; j++)
            {
                int cur = i * (lonSegments + 1) + j;
                int next = cur + (lonSegments + 1);

                Vector3 center = (verts[cur] + verts[next] + verts[cur + 1] + verts[next + 1]) * 0.25f;
                Vector3 outward = center.normalized;
                if (outward.sqrMagnitude < 0.0001f) outward = Vector3.up;

                AddTriangleSafe(verts, tris, cur, next, cur + 1, outward);
                AddTriangleSafe(verts, tris, cur + 1, next, next + 1, outward);
            }
        }

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateBounds();
        return m;
    }

    public static Mesh BuildEggTrayMesh()
    {
        Mesh m = new Mesh();
        m.name = "EggTray_Mesh";

        int nx = 44;
        int nz = 26;
        float halfX = 0.102f; // 20.4cm total width
        float halfZ = 0.056f; // 11.2cm total depth

        float[] cupX = new float[] { -0.072f, -0.024f, 0.024f, 0.072f };
        float[] cupZ = new float[] { -0.026f, 0.026f };
        float[] coneX = new float[] { -0.048f, 0.000f, 0.048f };

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        // 1. Top sculpted molded pulp surface (nx+1 by nz+1 grid)
        for (int iz = 0; iz <= nz; iz++)
        {
            float vz = (float)iz / nz;
            float z = Mathf.Lerp(-halfZ, halfZ, vz);

            for (int ix = 0; ix <= nx; ix++)
            {
                float ux = (float)ix / nx;
                float x = Mathf.Lerp(-halfX, halfX, ux);

                float y = EvaluateMoldedTrayHeight(x, z, halfX, halfZ, cupX, cupZ, coneX);
                verts.Add(new Vector3(x, y, z));
                uvs.Add(new Vector2(ux, vz));
            }
        }

        for (int iz = 0; iz < nz; iz++)
        {
            for (int ix = 0; ix < nx; ix++)
            {
                int i0 = iz * (nx + 1) + ix;
                int i1 = i0 + 1;
                int i2 = i0 + (nx + 1);
                int i3 = i2 + 1;

                AddTriangleSafe(verts, tris, i0, i2, i1, Vector3.up);
                AddTriangleSafe(verts, tris, i1, i2, i3, Vector3.up);
            }
        }

        // 2. Outer Molded Skirt Walls (Front, Back, Left, Right) dropping down to y = 0
        float bottomInset = 0.006f;

        // Front wall (iz = nz, z = +halfZ, normal = Vector3.forward)
        for (int ix = 0; ix < nx; ix++)
        {
            float ux0 = (float)ix / nx;
            float ux1 = (float)(ix + 1) / nx;
            Vector3 top0 = verts[nz * (nx + 1) + ix];
            Vector3 top1 = verts[nz * (nx + 1) + ix + 1];
            Vector3 bot0 = new Vector3(top0.x * (1f - bottomInset / halfX), 0f, halfZ - bottomInset);
            Vector3 bot1 = new Vector3(top1.x * (1f - bottomInset / halfX), 0f, halfZ - bottomInset);

            AddQuadSafe(verts, uvs, tris, bot0, bot1, top1, top0, Vector3.forward,
                new Vector2(ux0, 0f), new Vector2(ux1, 0f), new Vector2(ux1, 0.15f), new Vector2(ux0, 0.15f));
        }

        // Back wall (iz = 0, z = -halfZ, normal = Vector3.back)
        for (int ix = 0; ix < nx; ix++)
        {
            float ux0 = (float)ix / nx;
            float ux1 = (float)(ix + 1) / nx;
            Vector3 top0 = verts[ix];
            Vector3 top1 = verts[ix + 1];
            Vector3 bot0 = new Vector3(top0.x * (1f - bottomInset / halfX), 0f, -halfZ + bottomInset);
            Vector3 bot1 = new Vector3(top1.x * (1f - bottomInset / halfX), 0f, -halfZ + bottomInset);

            AddQuadSafe(verts, uvs, tris, bot1, bot0, top0, top1, Vector3.back,
                new Vector2(ux1, 0f), new Vector2(ux0, 0f), new Vector2(ux0, 0.15f), new Vector2(ux1, 0.15f));
        }

        // Left wall (ix = 0, x = -halfX, normal = Vector3.left)
        for (int iz = 0; iz < nz; iz++)
        {
            float vz0 = (float)iz / nz;
            float vz1 = (float)(iz + 1) / nz;
            Vector3 top0 = verts[iz * (nx + 1)];
            Vector3 top1 = verts[(iz + 1) * (nx + 1)];
            Vector3 bot0 = new Vector3(-halfX + bottomInset, 0f, top0.z * (1f - bottomInset / halfZ));
            Vector3 bot1 = new Vector3(-halfX + bottomInset, 0f, top1.z * (1f - bottomInset / halfZ));

            AddQuadSafe(verts, uvs, tris, bot0, bot1, top1, top0, Vector3.left,
                new Vector2(vz0, 0f), new Vector2(vz1, 0f), new Vector2(vz1, 0.15f), new Vector2(vz0, 0.15f));
        }

        // Right wall (ix = nx, x = +halfX, normal = Vector3.right)
        for (int iz = 0; iz < nz; iz++)
        {
            float vz0 = (float)iz / nz;
            float vz1 = (float)(iz + 1) / nz;
            Vector3 top0 = verts[iz * (nx + 1) + nx];
            Vector3 top1 = verts[(iz + 1) * (nx + 1) + nx];
            Vector3 bot0 = new Vector3(halfX - bottomInset, 0f, top0.z * (1f - bottomInset / halfZ));
            Vector3 bot1 = new Vector3(halfX - bottomInset, 0f, top1.z * (1f - bottomInset / halfZ));

            AddQuadSafe(verts, uvs, tris, bot1, bot0, top0, top1, Vector3.right,
                new Vector2(vz1, 0f), new Vector2(vz0, 0f), new Vector2(vz0, 0.15f), new Vector2(vz1, 0.15f));
        }

        // 3. Solid Bottom Base
        AddQuadSafe(verts, uvs, tris,
            new Vector3(-halfX + bottomInset, 0f, -halfZ + bottomInset),
            new Vector3(halfX - bottomInset, 0f, -halfZ + bottomInset),
            new Vector3(halfX - bottomInset, 0f, halfZ - bottomInset),
            new Vector3(-halfX + bottomInset, 0f, halfZ - bottomInset),
            Vector3.down,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateBounds();
        return m;
    }

    private static float EvaluateMoldedTrayHeight(float x, float z, float halfX, float halfZ, float[] cupX, float[] cupZ, float[] coneX)
    {
        float baseDeck = 0.031f;
        float y = baseDeck;

        // 8 Recessed Egg Cups
        float cupRadius = 0.0225f;
        for (int r = 0; r < cupZ.Length; r++)
        {
            for (int c = 0; c < cupX.Length; c++)
            {
                float dx = x - cupX[c];
                float dz = z - cupZ[r];
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist < cupRadius)
                {
                    float t = dist / cupRadius;
                    float bowl = 0.5f * (1f + Mathf.Cos(t * Mathf.PI));
                    y -= 0.024f * bowl;
                }
            }
        }

        // 3 Central Divider Cones between rows & columns
        float coneRadius = 0.0155f;
        for (int k = 0; k < coneX.Length; k++)
        {
            float dx = x - coneX[k];
            float dz = z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist < coneRadius)
            {
                float t = dist / coneRadius;
                float peak = 0.5f * (1f + Mathf.Cos(t * Mathf.PI));
                y += 0.016f * peak;
            }
        }

        // Rolled outer rim lip
        float edgeDistX = halfX - Mathf.Abs(x);
        float edgeDistZ = halfZ - Mathf.Abs(z);
        float edgeDist = Mathf.Min(edgeDistX, edgeDistZ);
        if (edgeDist < 0.008f)
        {
            float t = edgeDist / 0.008f;
            y = Mathf.Lerp(0.024f, y + 0.003f, Mathf.Sin(t * Mathf.PI * 0.5f));
        }

        return y;
    }

    public static Mesh BuildSculptedBaconStripMesh(float phaseOffset)
    {
        Mesh m = new Mesh();
        m.name = "BaconStrip_Mesh";

        int segX = 28;
        int segZ = 6;
        float halfLen = 0.088f; // 17.6cm strip length
        float halfWid = 0.013f; // 2.6cm strip width
        float thickness = 0.0042f; // 4.2mm thick-cut slice

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        // Top surface grid
        for (int iz = 0; iz <= segZ; iz++)
        {
            float vz = (float)iz / segZ;
            float z = Mathf.Lerp(-halfWid, halfWid, vz);

            for (int ix = 0; ix <= segX; ix++)
            {
                float ux = (float)ix / segX;
                float x = Mathf.Lerp(-halfLen, halfLen, ux);

                // Natural wavy pork belly contour
                float waveY = Mathf.Sin(ux * Mathf.PI * 3.5f + phaseOffset) * 0.0022f +
                              Mathf.Cos(ux * Mathf.PI * 7.0f + phaseOffset * 0.6f) * 0.0008f;
                float waveZ = Mathf.Sin(ux * Mathf.PI * 2.5f + phaseOffset) * 0.0025f;

                // Slightly rounded edge profile across width
                float edgeRound = Mathf.Sin(vz * Mathf.PI);
                float yTop = waveY + thickness * (0.65f + 0.35f * edgeRound);

                verts.Add(new Vector3(x, yTop, z + waveZ));
                uvs.Add(new Vector2(ux, vz));
            }
        }

        for (int iz = 0; iz < segZ; iz++)
        {
            for (int ix = 0; ix < segX; ix++)
            {
                int i0 = iz * (segX + 1) + ix;
                int i1 = i0 + 1;
                int i2 = i0 + (segX + 1);
                int i3 = i2 + 1;

                AddTriangleSafe(verts, tris, i0, i2, i1, Vector3.up);
                AddTriangleSafe(verts, tris, i1, i2, i3, Vector3.up);
            }
        }

        // Front and back slice thickness edges so the strip looks solid and thick-cut
        for (int ix = 0; ix < segX; ix++)
        {
            float ux0 = (float)ix / segX;
            float ux1 = (float)(ix + 1) / segX;

            // Front edge (iz = segZ)
            Vector3 fTop0 = verts[segZ * (segX + 1) + ix];
            Vector3 fTop1 = verts[segZ * (segX + 1) + ix + 1];
            Vector3 fBot0 = new Vector3(fTop0.x, fTop0.y - thickness, fTop0.z);
            Vector3 fBot1 = new Vector3(fTop1.x, fTop1.y - thickness, fTop1.z);
            AddQuadSafe(verts, uvs, tris, fBot0, fBot1, fTop1, fTop0, Vector3.forward,
                new Vector2(ux0, 0.9f), new Vector2(ux1, 0.9f), new Vector2(ux1, 1f), new Vector2(ux0, 1f));

            // Back edge (iz = 0)
            Vector3 bTop0 = verts[ix];
            Vector3 bTop1 = verts[ix + 1];
            Vector3 bBot0 = new Vector3(bTop0.x, bTop0.y - thickness, bTop0.z);
            Vector3 bBot1 = new Vector3(bTop1.x, bTop1.y - thickness, bTop1.z);
            AddQuadSafe(verts, uvs, tris, bBot1, bBot0, bTop0, bTop1, Vector3.back,
                new Vector2(ux1, 0f), new Vector2(ux0, 0f), new Vector2(ux0, 0.1f), new Vector2(ux1, 0.1f));
        }

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateBounds();
        return m;
    }

    public static Mesh BuildBaconBackingBoardMesh()
    {
        Mesh m = new Mesh();
        m.name = "BaconBacking_Mesh";

        float hx = 0.115f; // 23cm width
        float hz = 0.070f; // 14cm depth
        float h = 0.005f;  // 5mm backing tray

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        // Top face
        AddQuadSafe(verts, uvs, tris,
            new Vector3(-hx, h, -hz), new Vector3(hx, h, -hz),
            new Vector3(hx, h, hz), new Vector3(-hx, h, hz),
            Vector3.up, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));

        // Bottom face
        AddQuadSafe(verts, uvs, tris,
            new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz),
            new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz),
            Vector3.down, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));

        // Front edge
        AddQuadSafe(verts, uvs, tris,
            new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz),
            new Vector3(hx, h, hz), new Vector3(-hx, h, hz),
            Vector3.forward, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));

        // Back edge
        AddQuadSafe(verts, uvs, tris,
            new Vector3(hx, 0f, -hz), new Vector3(-hx, 0f, -hz),
            new Vector3(-hx, h, -hz), new Vector3(hx, h, -hz),
            Vector3.back, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));

        // Left edge
        AddQuadSafe(verts, uvs, tris,
            new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz),
            new Vector3(-hx, h, hz), new Vector3(-hx, h, -hz),
            Vector3.left, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));

        // Right edge
        AddQuadSafe(verts, uvs, tris,
            new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz),
            new Vector3(hx, h, -hz), new Vector3(hx, h, hz),
            Vector3.right, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateBounds();
        return m;
    }

    public static Mesh BuildBaconLabelSleeveMesh()
    {
        Mesh m = new Mesh();
        m.name = "BaconLabelSleeve_Mesh";

        // Wrap-around cardboard label sleeve on the left portion of the bacon pack
        float xMin = -0.116f;
        float xMax = -0.028f;
        float hz = 0.071f;
        float yTop = 0.017f;
        float yBot = -0.001f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        // Top printed label face
        AddQuadSafe(verts, uvs, tris,
            new Vector3(xMin, yTop, -hz), new Vector3(xMax, yTop, -hz),
            new Vector3(xMax, yTop, hz), new Vector3(xMin, yTop, hz),
            Vector3.up, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));

        // Front fold of sleeve
        AddQuadSafe(verts, uvs, tris,
            new Vector3(xMin, yBot, hz), new Vector3(xMax, yBot, hz),
            new Vector3(xMax, yTop, hz), new Vector3(xMin, yTop, hz),
            Vector3.forward, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.12f), new Vector2(0f, 0.12f));

        // Back fold of sleeve
        AddQuadSafe(verts, uvs, tris,
            new Vector3(xMax, yBot, -hz), new Vector3(xMin, yBot, -hz),
            new Vector3(xMin, yTop, -hz), new Vector3(xMax, yTop, -hz),
            Vector3.back, new Vector2(0f, 0.88f), new Vector2(1f, 0.88f), new Vector2(1f, 1f), new Vector2(0f, 1f));

        // Left fold of sleeve
        AddQuadSafe(verts, uvs, tris,
            new Vector3(xMin, yBot, -hz), new Vector3(xMin, yBot, hz),
            new Vector3(xMin, yTop, hz), new Vector3(xMin, yTop, -hz),
            Vector3.left, new Vector2(0f, 0f), new Vector2(0.12f, 0f), new Vector2(0.12f, 1f), new Vector2(0f, 1f));

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateBounds();
        return m;
    }
}

/// <summary>
/// Interactive shelf trigger inside PFB_Fridge/Fridge_Interior allowing the player
/// to place the Egg Tray and Bacon Pack back inside once TaskPutFoodBackInFridge starts.
/// </summary>
public class CabinFridgeReturnSpot : MonoBehaviour, IInteractable
{
    public CabinFridgeInteractable parentFridge;
    private const float MAX_INTERACT_DISTANCE = 2.6f;

    private CabinFridgeInteractable ResolveFridge()
    {
        if (parentFridge == null) parentFridge = GetComponentInParent<CabinFridgeInteractable>();
        return parentFridge;
    }

    private InteractableFoodItem GetCandidateItem()
    {
        if (HouseSceneStoryController.IsDialogueActive) return null;
        if (!HouseSceneStoryController.IsReturnFoodToFridgeUnlocked) return null;
        if (HouseSceneStoryController.HasPutFoodBackInFridge) return null;

        CabinFridgeInteractable fridge = ResolveFridge();
        if (fridge == null || !fridge.isOpen) return null;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam != null && Vector3.Distance(cam.transform.position, fridge.transform.position) > MAX_INTERACT_DISTANCE)
        {
            return null;
        }

        return InteractableFoodItem.FindActiveOrHeldFoodItem();
    }

    public string GetPromptText()
    {
        InteractableFoodItem itemToReturn = GetCandidateItem();
        if (itemToReturn == null) return string.Empty;

        if (string.Equals(itemToReturn.itemName, "EGGS", System.StringComparison.OrdinalIgnoreCase))
        {
            return "E to Put Egg Tray in Fridge";
        }
        if (string.Equals(itemToReturn.itemName, "BACON", System.StringComparison.OrdinalIgnoreCase))
        {
            return "E to Put Bacon Pack in Fridge";
        }
        return string.Empty;
    }

    public void Interact()
    {
        InteractableFoodItem itemToReturn = GetCandidateItem();
        if (itemToReturn == null) return;

        itemToReturn.ReturnToFridge();
    }
}

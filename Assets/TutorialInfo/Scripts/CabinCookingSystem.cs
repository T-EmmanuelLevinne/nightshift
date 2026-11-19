using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Complete Cabin Kitchen & Breakfast Cooking System for HouseScene:
/// 1. Place Egg Tray & Pack of Bacon on top of KitchenBench_base.
/// 2. Once placed, tray/pack cannot be picked up whole; player can only pick up 1 Egg and 2 Bacons.
/// 3. Inside door04 (grp_drawsDoors/doors/door04): Realistic 3D Frying Pan ("E to Pick Up Frying Pan").
/// 4. Place Frying Pan on PFB_Stove ("E to place frying pan on stove"), then turn on stove ("E to turn on the stove")
///    with 3D gas burner fire at the bottom of the Frying Pan.
/// 5. Fry the 1 Egg and 2 Bacons on the pan and wait 15 seconds to cook.
/// 6. Inside door01 (grp_drawsDoors/doors/door01): Stack of realistic 3D Porcelain Plates ("E to Pick Up Plate").
/// 7. Put cooked Egg & 2 Bacons onto the held Plate, then place it on PFB_DiningTable at the specific spot
///    in front of PFB_DiningChairUserInteract.
/// 8. Inside draws/draw01: Messy 3D Forks & Spoons; after placing the plate on the dining table, pick up the Fork ("E to Pick Up Fork").
/// 9. Eat the plated food for 15 seconds with a first-person POV fork-to-mouth eating animation (strictly while sitting!).
/// </summary>
public static class CabinCookingSystem
{
    public static CabinHeldCookingItem HeldSingleEgg { get; private set; }
    public static CabinHeldCookingItem HeldTwoBacons { get; private set; }
    public static CabinHeldCookingItem HeldFryingPan { get; private set; }
    public static CabinHeldCookingItem HeldPlate { get; private set; }
    public static CabinHeldCookingItem HeldFork { get; private set; }
    public static CabinHeldCookingItem HeldDirtyPlate { get; private set; }
    public static CabinHeldCookingItem HeldWashedCleanPlate { get; private set; }

    public static bool IsPlateContainingCookedFood { get; set; }
    public static bool IsPlatedFoodPlacedOnDiningTable { get; set; }
    public static bool HasPickedUpFork { get; set; }
    public static bool IsMealFinished { get; set; }

    private static Mesh cachedPanBowlMesh;
    private static Mesh cachedPanInteriorMesh;
    private static Mesh cachedPanHandleMesh;
    private static Mesh cachedPlateMesh;
    private static Mesh cachedSunnyEggWhiteMesh;
    private static Mesh cachedSunnyEggYolkMesh;
    private static Mesh cachedForkMesh;
    private static Mesh cachedSpoonMesh;
    private static Mesh cachedFlamePetalMesh;

    public static void ConfigureKitchenAndCooking()
    {
        HeldSingleEgg = null;
        HeldTwoBacons = null;
        HeldFryingPan = null;
        HeldPlate = null;
        HeldFork = null;
        HeldDirtyPlate = null;
        HeldWashedCleanPlate = null;
        IsPlateContainingCookedFood = false;
        IsPlatedFoodPlacedOnDiningTable = false;
        HasPickedUpFork = false;
        IsMealFinished = false;
        CabinChairInteractable.IsPlayerEating = false;
        CabinKitchenSinkStation.ResetStaticWashState();

        GameObject kitchenBench = FindSceneObject("PFB_KitchenBench");
        if (kitchenBench != null)
        {
            ConfigureKitchenBenchDrawersAndCounter(kitchenBench);
            ConfigureKitchenSinkStation(kitchenBench);
            SetupCabinetDoor04FryingPan(kitchenBench);
            SetupCabinetDoor01Plates(kitchenBench);
            SetupDrawer01MessyUtensils(kitchenBench);
        }

        GameObject stoveObj = FindSceneObject("PFB_Stove");
        if (stoveObj != null)
        {
            ConfigureStoveStation(stoveObj);
        }

        GameObject diningTable = FindSceneObject("PFB_DiningTable");
        GameObject diningChair = FindSceneObject("PFB_DiningChairUserInteract");
        if (diningTable != null)
        {
            ConfigureDiningTableSpot(diningTable, diningChair);
        }
    }

    private static GameObject FindSceneObject(string exactOrPartialName)
    {
        GameObject go = GameObject.Find(exactOrPartialName);
        if (go != null) return go;

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        for (int i = 0; i < allObjects.Length; i++)
        {
            if (allObjects[i].name.Equals(exactOrPartialName, System.StringComparison.OrdinalIgnoreCase))
            {
                return allObjects[i];
            }
        }
        return null;
    }

    private static void ConfigureKitchenBenchDrawersAndCounter(GameObject kitchenBench)
    {
        Animator benchAnim = kitchenBench.GetComponent<Animator>();
        if (benchAnim != null) benchAnim.enabled = true;

        // Disable the oversized trigger BoxCollider on PFB_KitchenBench root so it never intercepts raycasts
        BoxCollider rootTrigger = kitchenBench.GetComponent<BoxCollider>();
        if (rootTrigger != null && rootTrigger.isTrigger)
        {
            rootTrigger.enabled = false;
        }

        // Configure draw01..draw04 (Animator parameters isOpen_Obj_6 .. isOpen_Obj_9)
        for (int i = 1; i <= 4; i++)
        {
            string drawName = $"draw0{i}";
            Transform drawTr = FindChildRecursive(kitchenBench.transform, drawName);
            if (drawTr == null) continue;

            MeshCollider mc = drawTr.GetComponent<MeshCollider>();
            if (mc != null) Object.Destroy(mc);

            Rigidbody rb = drawTr.GetComponent<Rigidbody>();
            if (rb != null) Object.Destroy(rb);

            MeshFilter mf = drawTr.GetComponent<MeshFilter>();
            BoxCollider bc = drawTr.GetComponent<BoxCollider>();
            if (bc == null) bc = drawTr.gameObject.AddComponent<BoxCollider>();

            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                // Only cover the front face/handle of the drawer so items inside the drawer can be aimed at directly!
                bc.size = new Vector3(b.size.x, b.size.y, 0.08f);
                bc.center = new Vector3(b.center.x, b.center.y, b.max.z - 0.03f);
            }
            else
            {
                bc.size = new Vector3(0.52f, 0.18f, 0.08f);
                bc.center = new Vector3(0f, 0f, 0.30f);
            }
            bc.isTrigger = true;

            CabinDoorInteractable cdi = drawTr.GetComponent<CabinDoorInteractable>();
            if (cdi == null) cdi = drawTr.gameObject.AddComponent<CabinDoorInteractable>();
            cdi.objectNumber = 5 + i; // draw01 = 6, draw02 = 7, draw03 = 8, draw04 = 9
            cdi.targetAnimator = benchAnim;
            cdi.parameterPrefix = "isOpen_Obj_";
        }

        // Configure KitchenBench_base countertop surface for placing Egg Tray & Pack of Bacon
        Transform benchBase = FindChildRecursive(kitchenBench.transform, "KitchenBench_base");
        if (benchBase != null)
        {
            Transform oldSpot = kitchenBench.transform.Find("Counter_Placement_Surface");
            if (oldSpot != null) Object.Destroy(oldSpot.gameObject);

            GameObject surfaceGo = new GameObject("Counter_Placement_Surface");
            surfaceGo.transform.SetParent(kitchenBench.transform, false);
            // Flat marble countertop spans X in [-2.20, 0.60], top surface Y = 0.946m, Z in [-0.45, 0.45]
            surfaceGo.transform.localPosition = new Vector3(-0.85f, 0.95f, 0.02f);
            surfaceGo.transform.localRotation = Quaternion.identity;

            BoxCollider counterTrigger = surfaceGo.AddComponent<BoxCollider>();
            counterTrigger.isTrigger = true;
            counterTrigger.size = new Vector3(2.60f, 0.12f, 0.92f);
            counterTrigger.center = new Vector3(0f, 0.04f, 0f);

            CabinKitchenCounterSurface surface = surfaceGo.AddComponent<CabinKitchenCounterSurface>();
            surface.kitchenBenchRoot = kitchenBench.transform;
        }
    }

    private static void ConfigureKitchenSinkStation(GameObject kitchenBench)
    {
        Transform existing = kitchenBench.transform.Find("Kitchen_Sink_Station");
        if (existing != null) Object.Destroy(existing.gameObject);

        DetectSinkAndFaucetGeometry(
            kitchenBench,
            out Vector3 spoutTipBenchLocal,
            out Vector3 basinCenterBenchLocal,
            out Vector3 basinSizeLocal);

        GameObject sinkGo = new GameObject("Kitchen_Sink_Station");
        sinkGo.transform.SetParent(kitchenBench.transform, false);
        sinkGo.transform.localPosition = new Vector3(
            basinCenterBenchLocal.x,
            Mathf.Max(0.94f, spoutTipBenchLocal.y - 0.08f),
            basinCenterBenchLocal.z);
        sinkGo.transform.localRotation = Quaternion.identity;

        BoxCollider bc = sinkGo.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(
            Mathf.Max(basinSizeLocal.x + 0.18f, 0.70f),
            0.56f,
            Mathf.Max(basinSizeLocal.z + 0.24f, 0.68f));
        bc.center = Vector3.zero;

        CabinKitchenSinkStation sinkStation = sinkGo.AddComponent<CabinKitchenSinkStation>();
        sinkStation.InitializeSinkGeometry(spoutTipBenchLocal, basinCenterBenchLocal);
    }

    private static void DetectSinkAndFaucetGeometry(
        GameObject kitchenBench,
        out Vector3 spoutTipBenchLocal,
        out Vector3 basinCenterBenchLocal,
        out Vector3 basinSizeLocal)
    {
        // Positive-X (+0.95) is the left side of PFB_KitchenBench (above door01 / dishwasher)
        spoutTipBenchLocal = new Vector3(0.92f, 1.025f, 0.08f);
        basinCenterBenchLocal = new Vector3(0.95f, 0.755f, 0.02f);
        basinSizeLocal = new Vector3(0.55f, 0.22f, 0.45f);

        if (kitchenBench == null) return;

        Transform benchBase = FindChildRecursive(kitchenBench.transform, "KitchenBench_base");
        if (benchBase == null) return;

        MeshFilter mf = benchBase.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return;

        Mesh mesh = mf.sharedMesh;
        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        if (verts == null || verts.Length == 0) return;

        bool hasNormals = normals != null && normals.Length == verts.Length;

        // 1. Convert all vertices to PFB_KitchenBench root local space and measure marble countertop height
        Vector3[] localVerts = new Vector3[verts.Length];
        float countertopY = 0f;
        int countertopSamples = 0;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 lv = kitchenBench.transform.InverseTransformPoint(benchBase.TransformPoint(verts[i]));
            localVerts[i] = lv;

            // Middle of the marble countertop away from sink/faucet
            if (Mathf.Abs(lv.x) < 0.25f && Mathf.Abs(lv.z) < 0.20f && lv.y > 0.80f && lv.y < 1.05f)
            {
                if (lv.y > countertopY) countertopY = lv.y;
                countertopSamples++;
            }
        }

        if (countertopSamples == 0 || countertopY < 0.80f)
        {
            countertopY = 0.946f;
        }

        // 2. Collect all vertices strictly protruding above the flat marble countertop -> Faucet mesh
        List<Vector3> faucetVerts = new List<Vector3>();
        for (int i = 0; i < localVerts.Length; i++)
        {
            if (localVerts[i].y > countertopY + 0.022f)
            {
                faucetVerts.Add(localVerts[i]);
            }
        }

        if (faucetVerts.Count < 6)
        {
            return;
        }

        // Compute faucet mounting stem XZ (lowest collar of the faucet just above countertop)
        float stemSumX = 0f, stemSumZ = 0f;
        int stemCount = 0;
        float fSumX = 0f, fSumZ = 0f;

        for (int i = 0; i < faucetVerts.Count; i++)
        {
            Vector3 fv = faucetVerts[i];
            fSumX += fv.x;
            fSumZ += fv.z;

            if (fv.y <= countertopY + 0.055f)
            {
                stemSumX += fv.x;
                stemSumZ += fv.z;
                stemCount++;
            }
        }

        Vector2 stemXZ = stemCount > 0
            ? new Vector2(stemSumX / stemCount, stemSumZ / stemCount)
            : new Vector2(fSumX / faucetVerts.Count, fSumZ / faucetVerts.Count);

        // 3. Find sink basin floor near stemXZ (upward-facing horizontal interior surface below countertop)
        float bMinX = float.MaxValue, bMaxX = float.MinValue;
        float bMinZ = float.MaxValue, bMaxZ = float.MinValue;
        float bSumY = 0f;
        int basinCount = 0;

        for (int i = 0; i < localVerts.Length; i++)
        {
            Vector3 lv = localVerts[i];
            if (Mathf.Abs(lv.x - stemXZ.x) > 0.58f) continue;
            if (Mathf.Abs(lv.z) > 0.28f) continue;
            if (lv.y < 0.65f || lv.y > countertopY - 0.05f) continue;

            if (hasNormals)
            {
                Vector3 ln = kitchenBench.transform.InverseTransformDirection(benchBase.TransformDirection(normals[i]));
                if (ln.y < 0.65f) continue;
            }

            if (lv.x < bMinX) bMinX = lv.x;
            if (lv.x > bMaxX) bMaxX = lv.x;
            if (lv.z < bMinZ) bMinZ = lv.z;
            if (lv.z > bMaxZ) bMaxZ = lv.z;
            bSumY += lv.y;
            basinCount++;
        }

        if (basinCount > 0)
        {
            float floorY = bSumY / basinCount;
            basinCenterBenchLocal = new Vector3((bMinX + bMaxX) * 0.5f, floorY, (bMinZ + bMaxZ) * 0.5f);
            basinSizeLocal = new Vector3(
                Mathf.Clamp(bMaxX - bMinX, 0.35f, 0.85f),
                Mathf.Clamp(countertopY - floorY, 0.14f, 0.32f),
                Mathf.Clamp(bMaxZ - bMinZ, 0.30f, 0.65f));
        }
        else
        {
            basinCenterBenchLocal = new Vector3(stemXZ.x, countertopY - 0.19f, 0f);
        }

        // 4. Locate exact Faucet Spout Nozzle Opening projecting over the sink basin
        Vector2 toBasinDir = new Vector2(basinCenterBenchLocal.x - stemXZ.x, basinCenterBenchLocal.z - stemXZ.y);
        if (toBasinDir.sqrMagnitude > 0.0001f)
        {
            toBasinDir.Normalize();
        }
        else
        {
            toBasinDir = Vector2.up;
        }

        float maxReachOverBasin = -1f;
        for (int i = 0; i < faucetVerts.Count; i++)
        {
            Vector3 fv = faucetVerts[i];
            Vector2 rel = new Vector2(fv.x - stemXZ.x, fv.z - stemXZ.y);
            float forwardProj = Vector2.Dot(rel, toBasinDir);
            if (forwardProj < 0.015f) continue;

            float reach = rel.magnitude;
            if (reach > maxReachOverBasin)
            {
                maxReachOverBasin = reach;
            }
        }

        if (maxReachOverBasin > 0.03f)
        {
            float nozzleMinY = float.MaxValue;
            List<Vector3> tipRingVerts = new List<Vector3>();

            for (int i = 0; i < faucetVerts.Count; i++)
            {
                Vector3 fv = faucetVerts[i];
                Vector2 rel = new Vector2(fv.x - stemXZ.x, fv.z - stemXZ.y);
                float forwardProj = Vector2.Dot(rel, toBasinDir);
                if (forwardProj < 0.015f) continue;

                if (rel.magnitude >= maxReachOverBasin - 0.018f)
                {
                    tipRingVerts.Add(fv);
                    if (fv.y < nozzleMinY) nozzleMinY = fv.y;
                }
            }

            if (tipRingVerts.Count > 0)
            {
                float sumTipX = 0f, sumTipZ = 0f;
                int tipBottomCount = 0;
                for (int i = 0; i < tipRingVerts.Count; i++)
                {
                    if (tipRingVerts[i].y <= nozzleMinY + 0.014f)
                    {
                        sumTipX += tipRingVerts[i].x;
                        sumTipZ += tipRingVerts[i].z;
                        tipBottomCount++;
                    }
                }

                if (tipBottomCount > 0)
                {
                    spoutTipBenchLocal = new Vector3(
                        sumTipX / tipBottomCount,
                        nozzleMinY,
                        sumTipZ / tipBottomCount);
                }
            }
        }

        Debug.Log($"[CabinCookingSystem] Sink Geometry -> countertopY={countertopY:F3}, stemXZ={stemXZ}, basinCenter={basinCenterBenchLocal}, spoutTip={spoutTipBenchLocal}");
    }

    private static void SetupCabinetDoor04FryingPan(GameObject kitchenBench)
    {
        Transform existing = kitchenBench.transform.Find("Door04_Cabinet_Contents");
        if (existing != null) Object.Destroy(existing.gameObject);

        Transform door04 = FindChildRecursive(kitchenBench.transform, "door04");
        CabinDoorInteractable door04Interactable = door04 != null ? door04.GetComponent<CabinDoorInteractable>() : null;

        GameObject container = new GameObject("Door04_Cabinet_Contents");
        container.transform.SetParent(kitchenBench.transform, false);
        // Compartment behind door04 is centered at X = -1.31m, Y = [0.174, 0.861], Z = [-0.41, 0.40]
        container.transform.localPosition = new Vector3(-1.31f, 0.44f, 0.08f);
        container.transform.localRotation = Quaternion.identity;

        // Clean interior shelf board tucked inside the cabinet so it never pokes through closed door seams
        Material shelfMat = InteractableFoodItem.GetOrCreateMaterial("Cabinet_Shelf_MAT", null, new Color(0.72f, 0.70f, 0.66f), 0.25f);
        GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.name = "Door04_InnerShelf";
        shelf.transform.SetParent(container.transform, false);
        shelf.transform.localPosition = new Vector3(0f, -0.01f, -0.09f);
        shelf.transform.localScale = new Vector3(0.48f, 0.018f, 0.42f);
        shelf.GetComponent<MeshRenderer>().sharedMaterial = shelfMat;
        Object.Destroy(shelf.GetComponent<Collider>());

        // Build Realistic 3D Frying Pan inside door04
        GameObject panGo = CreateRealisticFryingPanModel("PFB_FryingPan");
        panGo.transform.SetParent(container.transform, false);
        panGo.transform.localPosition = new Vector3(-0.02f, 0.002f, 0.03f);
        panGo.transform.localRotation = Quaternion.Euler(0f, 32f, 0f);
        panGo.transform.localScale = Vector3.one;

        BoxCollider bc = panGo.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.32f, 0.14f, 0.44f);
        bc.center = new Vector3(0f, 0.04f, 0.06f);

        CabinHeldCookingItem item = panGo.AddComponent<CabinHeldCookingItem>();
        item.itemName = "FRYING PAN";
        item.pickupPrompt = "E to Pick Up Frying Pan";
        item.requiredCabinetDoor = door04Interactable;
        item.viewmodelOffset = new Vector3(0.24f, -0.24f, 0.50f);
        item.viewmodelRotation = new Vector3(12f, -155f, -6f);
        item.viewmodelScale = new Vector3(0.85f, 0.85f, 0.85f);

        HeldFryingPan = item;
    }

    private static void SetupCabinetDoor01Plates(GameObject kitchenBench)
    {
        Transform existing = kitchenBench.transform.Find("Door01_Cabinet_Contents");
        if (existing != null) Object.Destroy(existing.gameObject);

        Transform door01 = FindChildRecursive(kitchenBench.transform, "door01");
        CabinDoorInteractable door01Interactable = door01 != null ? door01.GetComponent<CabinDoorInteractable>() : null;

        GameObject container = new GameObject("Door01_Cabinet_Contents");
        container.transform.SetParent(kitchenBench.transform, false);
        // Compartment behind door01 is centered at X = +0.945m, Y = [0.174, 0.861], Z = [-0.41, 0.40]
        container.transform.localPosition = new Vector3(0.945f, 0.44f, 0.08f);
        container.transform.localRotation = Quaternion.identity;

        // Clean interior shelf board tucked inside door01 so it never pokes through closed door seams
        Material shelfMat = InteractableFoodItem.GetOrCreateMaterial("Cabinet_Shelf_MAT", null, new Color(0.72f, 0.70f, 0.66f), 0.25f);
        GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.name = "Door01_InnerShelf";
        shelf.transform.SetParent(container.transform, false);
        shelf.transform.localPosition = new Vector3(0f, -0.01f, -0.09f);
        shelf.transform.localScale = new Vector3(0.52f, 0.018f, 0.42f);
        shelf.GetComponent<MeshRenderer>().sharedMaterial = shelfMat;
        Object.Destroy(shelf.GetComponent<Collider>());

        // Stack of 4 realistic 3D porcelain dinner plates
        GameObject stackGo = new GameObject("PFB_PlateStack");
        stackGo.transform.SetParent(container.transform, false);
        stackGo.transform.localPosition = new Vector3(0f, 0.002f, 0.05f);
        stackGo.transform.localRotation = Quaternion.identity;

        GameObject topPlateGo = null;
        for (int p = 0; p < 4; p++)
        {
            GameObject plate = CreatePorcelainPlateModel($"Plate_0{p + 1}");
            plate.transform.SetParent(stackGo.transform, false);
            plate.transform.localPosition = new Vector3(0f, p * 0.014f, 0f);
            plate.transform.localRotation = Quaternion.Euler(0f, p * 17f, 0f);
            if (p == 3) topPlateGo = plate;
        }

        BoxCollider bc = stackGo.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.40f, 0.24f, 0.40f);
        bc.center = new Vector3(0f, 0.08f, 0f);

        CabinPlateStackInteractable stackInteractable = stackGo.AddComponent<CabinPlateStackInteractable>();
        stackInteractable.requiredCabinetDoor = door01Interactable;
        stackInteractable.topPlateVisual = topPlateGo;
    }

    private static void SetupDrawer01MessyUtensils(GameObject kitchenBench)
    {
        Transform draw01 = FindChildRecursive(kitchenBench.transform, "draw01");
        if (draw01 == null) return;

        Transform existing = draw01.Find("Draw01_MessyUtensils");
        if (existing != null) Object.Destroy(existing.gameObject);

        CabinDoorInteractable draw01Interactable = draw01.GetComponent<CabinDoorInteractable>();

        GameObject utensilsRoot = new GameObject("Draw01_MessyUtensils");
        utensilsRoot.transform.SetParent(draw01, false);

        // Position on the inner floor of draw01
        MeshFilter mf = draw01.GetComponent<MeshFilter>();
        Vector3 floorCenter = new Vector3(0f, -0.072f, 0.06f);
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            floorCenter = new Vector3(b.center.x, b.min.y + 0.018f, b.center.z + 0.02f);
        }
        utensilsRoot.transform.localPosition = floorCenter;
        utensilsRoot.transform.localRotation = Quaternion.identity;

        // Drawer liner pad so silverware gleams clearly against dark felt liner
        Material linerMat = InteractableFoodItem.GetOrCreateMaterial("Drawer_Liner_MAT", null, new Color(0.18f, 0.19f, 0.22f), 0.20f);
        GameObject liner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        liner.name = "Drawer_FeltLiner";
        liner.transform.SetParent(utensilsRoot.transform, false);
        liner.transform.localPosition = new Vector3(0f, -0.004f, 0f);
        liner.transform.localScale = new Vector3(0.42f, 0.006f, 0.44f);
        liner.GetComponent<MeshRenderer>().sharedMaterial = linerMat;
        Object.Destroy(liner.GetComponent<Collider>());

        // Messy arrangement of 4 Forks and 4 Spoons scattered across the drawer
        Vector3[] forkPositions = new Vector3[]
        {
            new Vector3(-0.06f, 0.008f,  0.04f),
            new Vector3( 0.05f, 0.006f, -0.03f),
            new Vector3(-0.09f, 0.005f, -0.06f),
            new Vector3( 0.02f, 0.011f,  0.07f)
        };
        float[] forkYaws = new float[] { 18f, -34f, 62f, -14f };

        Vector3[] spoonPositions = new Vector3[]
        {
            new Vector3( 0.08f, 0.006f,  0.03f),
            new Vector3(-0.02f, 0.005f,  0.01f),
            new Vector3( 0.04f, 0.009f, -0.08f),
            new Vector3(-0.07f, 0.007f,  0.09f)
        };
        float[] spoonYaws = new float[] { -22f, 41f, -68f, 125f };

        GameObject topForkVisual = null;
        for (int i = 0; i < forkPositions.Length; i++)
        {
            GameObject fork = CreateStainlessForkModel($"Drawer_Fork_0{i + 1}");
            fork.transform.SetParent(utensilsRoot.transform, false);
            fork.transform.localPosition = forkPositions[i];
            fork.transform.localRotation = Quaternion.Euler(2f * (i % 2 == 0 ? 1 : -1), forkYaws[i], 0f);
            if (i == 3) topForkVisual = fork;
        }

        for (int i = 0; i < spoonPositions.Length; i++)
        {
            GameObject spoon = CreateStainlessSpoonModel($"Drawer_Spoon_0{i + 1}");
            spoon.transform.SetParent(utensilsRoot.transform, false);
            spoon.transform.localPosition = spoonPositions[i];
            spoon.transform.localRotation = Quaternion.Euler(-2f, spoonYaws[i], 0f);
        }

        // Trigger collider for picking up the Fork inside draw01
        BoxCollider bc = utensilsRoot.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.42f, 0.12f, 0.42f);
        bc.center = new Vector3(0f, 0.03f, 0f);

        CabinDrawerForkInteractable forkPickup = utensilsRoot.AddComponent<CabinDrawerForkInteractable>();
        forkPickup.requiredDrawer = draw01Interactable;
        forkPickup.topForkVisual = topForkVisual;
    }

    private static void ConfigureStoveStation(GameObject stoveObj)
    {
        stoveObj.isStatic = false;

        Transform existing = stoveObj.transform.Find("Stove_Cooking_Station");
        if (existing != null) Object.Destroy(existing.gameObject);

        GameObject stationGo = new GameObject("Stove_Cooking_Station");
        stationGo.transform.SetParent(stoveObj.transform, false);
        // Front-right burner center in Stove.fbx local space is (0.234, 0.9243, 0.165)
        stationGo.transform.localPosition = new Vector3(0.234f, 0.925f, 0.165f);
        stationGo.transform.localRotation = Quaternion.identity;

        BoxCollider bc = stationGo.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.88f, 0.36f, 0.88f);
        bc.center = new Vector3(-0.18f, 0.08f, -0.10f);

        stationGo.AddComponent<CabinStoveStation>();
    }

    private static void ConfigureDiningTableSpot(GameObject diningTable, GameObject diningChair)
    {
        diningTable.isStatic = false;

        Transform existing = diningTable.transform.Find("DiningTable_UserPlaceSpot");
        if (existing != null) Object.Destroy(existing.gameObject);

        GameObject spotGo = new GameObject("DiningTable_UserPlaceSpot");
        spotGo.transform.SetParent(diningTable.transform, false);

        // Compute the exact tabletop point right in front of PFB_DiningChairUserInteract
        MeshFilter tableMf = diningTable.GetComponent<MeshFilter>();
        float topY = (tableMf != null && tableMf.sharedMesh != null) ? tableMf.sharedMesh.bounds.max.y : 0.828f;

        Vector3 localSpotPos = new Vector3(0.52f, topY + 0.002f, 0.30f);
        if (diningChair != null)
        {
            // Chair forward points directly at the table edge in front of the user's seat
            Vector3 worldSpot = diningChair.transform.position + diningChair.transform.forward * 0.24f;
            Vector3 inTableLocal = diningTable.transform.InverseTransformPoint(worldSpot);
            inTableLocal.y = topY + 0.002f;
            localSpotPos = inTableLocal;
        }

        spotGo.transform.localPosition = localSpotPos;
        spotGo.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

        // Subtle woven placemat so the specific spot in front of the user's chair is clearly marked
        Material matPlacemat = InteractableFoodItem.GetOrCreateMaterial("Dining_Placemat_MAT", null, new Color(0.28f, 0.23f, 0.19f), 0.18f);
        GameObject placemat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placemat.name = "UserSeat_Placemat";
        placemat.transform.SetParent(spotGo.transform, false);
        placemat.transform.localPosition = new Vector3(0f, 0.001f, 0f);
        placemat.transform.localScale = new Vector3(0.36f, 0.003f, 0.28f);
        placemat.GetComponent<MeshRenderer>().sharedMaterial = matPlacemat;
        Object.Destroy(placemat.GetComponent<Collider>());

        BoxCollider bc = spotGo.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.48f, 0.22f, 0.42f);
        bc.center = new Vector3(0f, 0.08f, 0f);

        spotGo.AddComponent<CabinDiningTablePlaceSpot>();
    }

    public static Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null) return null;
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }
        return null;
    }

    // =========================================================================
    // EQUIPPING PORTIONS (1 EGG, 2 BACONS, PLATE, FORK)
    // =========================================================================

    public static void EquipSingleEggPortion(Transform camTransform)
    {
        if (HeldSingleEgg != null) Object.Destroy(HeldSingleEgg.gameObject);

        GameObject eggGo = new GameObject("Held_SingleEgg");
        MeshFilter mf = eggGo.AddComponent<MeshFilter>();
        mf.sharedMesh = InteractableFoodItem.BuildOrganicEggMesh();

        MeshRenderer mr = eggGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = InteractableFoodItem.GetOrCreateMaterial(
            "EggShell_MAT", "Assets/FurnishedCabin/Textures/EggShell_Albedo.png", new Color(0.95f, 0.86f, 0.76f), 0.35f);

        CabinHeldCookingItem held = eggGo.AddComponent<CabinHeldCookingItem>();
        held.itemName = "1 EGG";
        held.viewmodelOffset = new Vector3(0.20f, -0.20f, 0.38f);
        held.viewmodelRotation = new Vector3(10f, -15f, 8f);
        held.viewmodelScale = new Vector3(1.15f, 1.15f, 1.15f);
        held.EquipToCamera(camTransform);

        HeldSingleEgg = held;
    }

    public static void EquipTwoBaconsPortion(Transform camTransform)
    {
        if (HeldTwoBacons != null) Object.Destroy(HeldTwoBacons.gameObject);

        GameObject baconsGo = new GameObject("Held_TwoBacons");
        Material stripMat = InteractableFoodItem.GetOrCreateMaterial(
            "BaconStrip_MAT", "Assets/FurnishedCabin/Textures/BaconStrip_Albedo.png", Color.white, 0.62f);

        for (int i = 0; i < 2; i++)
        {
            GameObject strip = new GameObject($"Held_BaconStrip_0{i + 1}");
            strip.transform.SetParent(baconsGo.transform, false);
            strip.transform.localPosition = new Vector3(0f, i * 0.004f, (i - 0.5f) * 0.028f);
            strip.transform.localRotation = Quaternion.Euler(0f, i * 4f, 0f);

            MeshFilter mf = strip.AddComponent<MeshFilter>();
            mf.sharedMesh = InteractableFoodItem.BuildSculptedBaconStripMesh(i * 1.3f);

            MeshRenderer mr = strip.AddComponent<MeshRenderer>();
            mr.sharedMaterial = stripMat;
        }

        CabinHeldCookingItem held = baconsGo.AddComponent<CabinHeldCookingItem>();
        held.itemName = "2 BACONS";
        held.viewmodelOffset = new Vector3(0.22f, -0.22f, 0.42f);
        held.viewmodelRotation = new Vector3(18f, -20f, 6f);
        held.viewmodelScale = new Vector3(0.95f, 0.95f, 0.95f);
        held.EquipToCamera(camTransform);

        HeldTwoBacons = held;
    }

    public static void EquipSinglePlate(Transform camTransform)
    {
        if (HeldPlate != null) Object.Destroy(HeldPlate.gameObject);

        GameObject plateGo = CreatePorcelainPlateModel("Held_PorcelainPlate");

        CabinHeldCookingItem held = plateGo.AddComponent<CabinHeldCookingItem>();
        held.itemName = "PLATE";
        held.viewmodelOffset = new Vector3(0.22f, -0.24f, 0.46f);
        held.viewmodelRotation = new Vector3(14f, -15f, 4f);
        held.viewmodelScale = new Vector3(0.88f, 0.88f, 0.88f);
        held.EquipToCamera(camTransform);

        HeldPlate = held;
    }

    public static void AttachCookedFoodToHeldPlate()
    {
        if (HeldPlate == null) return;

        IsPlateContainingCookedFood = true;
        PopulateCookedEggAndBaconsOnPlate(HeldPlate.transform);
        HeldPlate.RefreshRenderers();

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null && HeldPlate.AssignedSlot >= 0)
        {
            hotbar.SetSlot(HeldPlate.AssignedSlot, "PLATED FOOD");
            hotbar.SetActiveSlot(HeldPlate.AssignedSlot);
        }
    }

    public static void EquipForkUtensil(Transform camTransform)
    {
        if (HeldFork != null) Object.Destroy(HeldFork.gameObject);

        GameObject forkGo = CreateStainlessForkModel("Held_ForkUtensil");

        CabinHeldCookingItem held = forkGo.AddComponent<CabinHeldCookingItem>();
        held.itemName = "FORK";
        held.viewmodelOffset = new Vector3(0.22f, -0.20f, 0.40f);
        held.viewmodelRotation = new Vector3(-25f, 165f, 10f);
        held.viewmodelScale = new Vector3(1.15f, 1.15f, 1.15f);
        held.EquipToCamera(camTransform);

        HeldFork = held;
        HasPickedUpFork = true;
    }

    public static void EquipDirtyPlate(Transform camTransform)
    {
        if (HeldDirtyPlate != null) Object.Destroy(HeldDirtyPlate.gameObject);

        GameObject plateGo = CreateDirtyPlateModel("Held_DirtyPlate");

        CabinHeldCookingItem held = plateGo.AddComponent<CabinHeldCookingItem>();
        held.itemName = "DIRTY PLATE";
        held.pickupPrompt = "E to Pick Up Dirty Plate";
        held.viewmodelOffset = new Vector3(0.22f, -0.24f, 0.46f);
        held.viewmodelRotation = new Vector3(14f, -15f, 4f);
        held.viewmodelScale = new Vector3(0.88f, 0.88f, 0.88f);
        held.EquipToCamera(camTransform);

        HeldDirtyPlate = held;

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        if (hotbar != null && held.AssignedSlot >= 0)
        {
            hotbar.SetSlot(held.AssignedSlot, "DIRTY PLATE");
            hotbar.SetActiveSlot(held.AssignedSlot);
        }
    }

    public static void ConvertDirtyPlateToWashedCleanPlate()
    {
        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();

        if (HeldDirtyPlate != null)
        {
            int targetSlot = HeldDirtyPlate.AssignedSlot;
            if (targetSlot < 0 && hotbar != null)
            {
                for (int i = 0; i < hotbar.slotCount; i++)
                {
                    if (string.Equals(hotbar.GetItemName(i), "DIRTY PLATE", System.StringComparison.OrdinalIgnoreCase))
                    {
                        targetSlot = i;
                        break;
                    }
                }
            }

            // Remove all grease residue spots so the held plate becomes clean porcelain
            Transform[] children = HeldDirtyPlate.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name.StartsWith("Grease_Spot", System.StringComparison.OrdinalIgnoreCase))
                {
                    Object.Destroy(children[i].gameObject);
                }
            }

            HeldDirtyPlate.itemName = "PLATE";
            HeldDirtyPlate.gameObject.name = "Held_CleanWashedPlate";
            HeldDirtyPlate.IsSuppressingViewmodelFollow = false;
            HeldDirtyPlate.RefreshRenderers();

            bool showViewmodel = (hotbar == null || targetSlot < 0 || hotbar.IsSlotActive(targetSlot));
            HeldDirtyPlate.SetVisible(showViewmodel);

            if (hotbar != null && targetSlot >= 0)
            {
                hotbar.SetSlot(targetSlot, "PLATE");
                hotbar.SetActiveSlot(targetSlot);
            }

            HeldWashedCleanPlate = HeldDirtyPlate;
            HeldDirtyPlate = null;
        }
        else
        {
            Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
            if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
            if (cam != null)
            {
                if (hotbar != null)
                {
                    hotbar.ClearItem("DIRTY PLATE");
                }
                EquipSinglePlate(cam.transform);
                HeldWashedCleanPlate = HeldPlate;
                HeldPlate = null;
            }
        }
    }

    public static void ClearHeldWashedCleanPlateRef()
    {
        HeldWashedCleanPlate = null;
    }

    public static GameObject CreateDirtyPlateModel(string name)
    {
        GameObject go = CreatePorcelainPlateModel(name);

        Material greaseMat = InteractableFoodItem.GetOrCreateMaterial(
            "Grease_Residue_MAT", null, new Color(0.44f, 0.28f, 0.15f), 0.75f, 0.10f);

        Vector3[] spots = new Vector3[]
        {
            new Vector3(-0.024f, 0.009f, 0.012f),
            new Vector3(0.018f, 0.009f, -0.016f),
            new Vector3(0.005f, 0.009f, 0.022f)
        };
        Vector3[] spotScales = new Vector3[]
        {
            new Vector3(0.038f, 0.001f, 0.026f),
            new Vector3(0.046f, 0.001f, 0.032f),
            new Vector3(0.028f, 0.001f, 0.022f)
        };

        for (int i = 0; i < spots.Length; i++)
        {
            GameObject spot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spot.name = $"Grease_Spot_{i + 1}";
            spot.transform.SetParent(go.transform, false);
            spot.transform.localPosition = spots[i];
            spot.transform.localScale = spotScales[i];
            spot.GetComponent<MeshRenderer>().sharedMaterial = greaseMat;
            Object.Destroy(spot.GetComponent<Collider>());
        }

        return go;
    }

    // =========================================================================
    // 3D SCULPTED MODELS: FRYING PAN, PLATES, SUNNY EGG, UTENSILS, FLAMES
    // =========================================================================

    public static GameObject CreateRealisticFryingPanModel(string name)
    {
        GameObject root = new GameObject(name);

        Material outerMat = InteractableFoodItem.GetOrCreateMaterial(
            "FryingPan_Outer_MAT", null, new Color(0.16f, 0.17f, 0.19f), 0.62f, 0.75f);
        Material innerMat = InteractableFoodItem.GetOrCreateMaterial(
            "FryingPan_NonStick_MAT", null, new Color(0.10f, 0.10f, 0.11f), 0.45f, 0.25f);
        Material copperBaseMat = InteractableFoodItem.GetOrCreateMaterial(
            "FryingPan_CopperBase_MAT", null, new Color(0.76f, 0.48f, 0.30f), 0.78f, 0.88f);
        Material steelBracketMat = InteractableFoodItem.GetOrCreateMaterial(
            "FryingPan_Steel_MAT", null, new Color(0.82f, 0.84f, 0.87f), 0.82f, 0.90f);
        Material handleMat = InteractableFoodItem.GetOrCreateMaterial(
            "FryingPan_Handle_MAT", null, new Color(0.08f, 0.08f, 0.09f), 0.35f, 0.05f);

        if (cachedPanBowlMesh == null) cachedPanBowlMesh = BuildFryingPanOuterMesh();
        if (cachedPanInteriorMesh == null) cachedPanInteriorMesh = BuildFryingPanInteriorMesh();
        if (cachedPanHandleMesh == null) cachedPanHandleMesh = BuildFryingPanHandleMesh();

        // 1. Outer Forged Skillet Bowl
        GameObject outerGo = new GameObject("Pan_OuterBody");
        outerGo.transform.SetParent(root.transform, false);
        outerGo.AddComponent<MeshFilter>().sharedMesh = cachedPanBowlMesh;
        outerGo.AddComponent<MeshRenderer>().sharedMaterial = outerMat;

        // 2. Non-Stick Interior Cooking Surface
        GameObject innerGo = new GameObject("Pan_CookingSurface");
        innerGo.transform.SetParent(root.transform, false);
        innerGo.AddComponent<MeshFilter>().sharedMesh = cachedPanInteriorMesh;
        innerGo.AddComponent<MeshRenderer>().sharedMaterial = innerMat;

        // 3. Copper Heat-Diffuser Base Disc
        GameObject baseDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseDisc.name = "Pan_CopperDiffuser";
        baseDisc.transform.SetParent(root.transform, false);
        baseDisc.transform.localPosition = new Vector3(0f, 0.0015f, 0f);
        baseDisc.transform.localScale = new Vector3(0.195f, 0.0015f, 0.195f);
        baseDisc.GetComponent<MeshRenderer>().sharedMaterial = copperBaseMat;
        Object.Destroy(baseDisc.GetComponent<Collider>());

        // 4. Stainless Steel Riveted Yoke Bracket + 2 Rivets
        GameObject bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bracket.name = "Pan_SteelBracket";
        bracket.transform.SetParent(root.transform, false);
        bracket.transform.localPosition = new Vector3(0f, 0.036f, 0.142f);
        bracket.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
        bracket.transform.localScale = new Vector3(0.026f, 0.007f, 0.042f);
        bracket.GetComponent<MeshRenderer>().sharedMaterial = steelBracketMat;
        Object.Destroy(bracket.GetComponent<Collider>());

        for (int r = -1; r <= 1; r += 2)
        {
            GameObject rivet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rivet.name = "Pan_Rivet";
            rivet.transform.SetParent(root.transform, false);
            rivet.transform.localPosition = new Vector3(r * 0.011f, 0.031f, 0.121f);
            rivet.transform.localScale = new Vector3(0.006f, 0.006f, 0.006f);
            rivet.GetComponent<MeshRenderer>().sharedMaterial = steelBracketMat;
            Object.Destroy(rivet.GetComponent<Collider>());
        }

        // 5. Sculpted Contoured Phenolic Handle
        GameObject handleGo = new GameObject("Pan_ErgonomicHandle");
        handleGo.transform.SetParent(root.transform, false);
        handleGo.AddComponent<MeshFilter>().sharedMesh = cachedPanHandleMesh;
        handleGo.AddComponent<MeshRenderer>().sharedMaterial = handleMat;

        return root;
    }

    private static Mesh BuildFryingPanOuterMesh()
    {
        Mesh m = new Mesh();
        m.name = "FryingPan_Outer_Mesh";

        int segs = 36;
        float rBot = 0.105f;
        float rTop = 0.132f;
        float rLip = 0.136f;
        float h = 0.045f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        // Profile rings: (radius, height)
        Vector2[] profile = new Vector2[]
        {
            new Vector2(0.000f, 0.000f),
            new Vector2(rBot,   0.000f),
            new Vector2(rBot + 0.006f, 0.006f),
            new Vector2(rTop,   h - 0.004f),
            new Vector2(rLip,   h),
            new Vector2(rTop - 0.005f, h)
        };

        for (int p = 0; p < profile.Length; p++)
        {
            float rad = profile[p].x;
            float y = profile[p].y;
            float v = (float)p / (profile.Length - 1);

            for (int s = 0; s <= segs; s++)
            {
                float u = (float)s / segs;
                float ang = u * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad));
                uvs.Add(new Vector2(u, v));
            }
        }

        int stride = segs + 1;
        for (int p = 0; p < profile.Length - 1; p++)
        {
            for (int s = 0; s < segs; s++)
            {
                int i0 = p * stride + s;
                int i1 = i0 + 1;
                int i2 = (p + 1) * stride + s;
                int i3 = i2 + 1;

                Vector3 mid = (verts[i0] + verts[i1] + verts[i2] + verts[i3]) * 0.25f;
                Vector3 expectedNormal = (p == 0) ? Vector3.down :
                                         (p >= 4) ? Vector3.up :
                                         new Vector3(mid.x, -0.25f, mid.z).normalized;

                InteractableFoodItem.AddTriangleSafe(verts, tris, i0, i2, i1, expectedNormal);
                InteractableFoodItem.AddTriangleSafe(verts, tris, i1, i2, i3, expectedNormal);
            }
        }

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static Mesh BuildFryingPanInteriorMesh()
    {
        Mesh m = new Mesh();
        m.name = "FryingPan_Interior_Mesh";

        int segs = 36;
        float rBot = 0.100f;
        float rTop = 0.127f;
        float yBot = 0.006f;
        float yTop = 0.045f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        Vector2[] profile = new Vector2[]
        {
            new Vector2(0.000f, yBot),
            new Vector2(rBot * 0.5f, yBot),
            new Vector2(rBot, yBot + 0.001f),
            new Vector2(rBot + 0.006f, yBot + 0.006f),
            new Vector2(rTop, yTop)
        };

        for (int p = 0; p < profile.Length; p++)
        {
            float rad = profile[p].x;
            float y = profile[p].y;
            float v = (float)p / (profile.Length - 1);

            for (int s = 0; s <= segs; s++)
            {
                float u = (float)s / segs;
                float ang = u * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad));
                uvs.Add(new Vector2(u, v));
            }
        }

        int stride = segs + 1;
        for (int p = 0; p < profile.Length - 1; p++)
        {
            for (int s = 0; s < segs; s++)
            {
                int i0 = p * stride + s;
                int i1 = i0 + 1;
                int i2 = (p + 1) * stride + s;
                int i3 = i2 + 1;

                Vector3 mid = (verts[i0] + verts[i1] + verts[i2] + verts[i3]) * 0.25f;
                Vector3 expectedNormal = (p <= 1) ? Vector3.up : new Vector3(-mid.x, 0.55f, -mid.z).normalized;

                InteractableFoodItem.AddTriangleSafe(verts, tris, i0, i2, i1, expectedNormal);
                InteractableFoodItem.AddTriangleSafe(verts, tris, i1, i2, i3, expectedNormal);
            }
        }

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static Mesh BuildFryingPanHandleMesh()
    {
        Mesh m = new Mesh();
        m.name = "FryingPan_Handle_Mesh";

        int lenSegs = 16;
        int radSegs = 12;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        for (int i = 0; i <= lenSegs; i++)
        {
            float t = (float)i / lenSegs;
            float z = Mathf.Lerp(0.155f, 0.325f, t);
            float y = 0.039f + (z - 0.155f) * 0.25f;

            // Ergonomic swell in the middle of the handle grip
            float swell = 1.0f + 0.25f * Mathf.Sin(t * Mathf.PI) - 0.15f * Mathf.Sin(t * Mathf.PI * 2f);
            float rx = 0.013f * swell * (t > 0.92f ? 0.5f : 1f);
            float ry = 0.0085f * swell * (t > 0.92f ? 0.5f : 1f);

            for (int j = 0; j <= radSegs; j++)
            {
                float u = (float)j / radSegs;
                float ang = u * Mathf.PI * 2f;
                float ox = Mathf.Cos(ang) * rx;
                float oy = Mathf.Sin(ang) * ry;
                verts.Add(new Vector3(ox, y + oy, z));
                uvs.Add(new Vector2(u, t));
            }
        }

        int stride = radSegs + 1;
        for (int i = 0; i < lenSegs; i++)
        {
            for (int j = 0; j < radSegs; j++)
            {
                int i0 = i * stride + j;
                int i1 = i0 + 1;
                int i2 = (i + 1) * stride + j;
                int i3 = i2 + 1;

                float ang = ((j + 0.5f) / radSegs) * Mathf.PI * 2f;
                Vector3 expectedNormal = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f).normalized;

                InteractableFoodItem.AddTriangleSafe(verts, tris, i0, i2, i1, expectedNormal);
                InteractableFoodItem.AddTriangleSafe(verts, tris, i1, i2, i3, expectedNormal);
            }
        }

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    public static GameObject CreatePorcelainPlateModel(string name)
    {
        GameObject go = new GameObject(name);
        if (cachedPlateMesh == null) cachedPlateMesh = BuildPorcelainPlateMesh();

        Material plateMat = InteractableFoodItem.GetOrCreateMaterial(
            "Porcelain_Plate_MAT", null, new Color(0.95f, 0.95f, 0.93f), 0.88f, 0.04f);

        go.AddComponent<MeshFilter>().sharedMesh = cachedPlateMesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = plateMat;
        return go;
    }

    private static Mesh BuildPorcelainPlateMesh()
    {
        Mesh m = new Mesh();
        m.name = "PorcelainPlate_Mesh";

        int segs = 32;
        // Profile from top center -> top well -> flared rim -> outer lip -> bottom foot -> bottom center
        Vector2[] profile = new Vector2[]
        {
            new Vector2(0.000f, 0.006f), // Top center well
            new Vector2(0.072f, 0.0065f), // Top inner well edge
            new Vector2(0.114f, 0.021f), // Top outer rim
            new Vector2(0.118f, 0.020f), // Outer lip edge
            new Vector2(0.076f, 0.002f), // Underside slope
            new Vector2(0.072f, 0.000f), // Foot ring bottom
            new Vector2(0.066f, 0.002f), // Inner foot ring
            new Vector2(0.000f, 0.002f)  // Underside center
        };

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        for (int p = 0; p < profile.Length; p++)
        {
            float rad = profile[p].x;
            float y = profile[p].y;
            float v = (float)p / (profile.Length - 1);

            for (int s = 0; s <= segs; s++)
            {
                float u = (float)s / segs;
                float ang = u * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad));
                uvs.Add(new Vector2(u, v));
            }
        }

        int stride = segs + 1;
        for (int p = 0; p < profile.Length - 1; p++)
        {
            for (int s = 0; s < segs; s++)
            {
                int i0 = p * stride + s;
                int i1 = i0 + 1;
                int i2 = (p + 1) * stride + s;
                int i3 = i2 + 1;

                Vector3 mid = (verts[i0] + verts[i1] + verts[i2] + verts[i3]) * 0.25f;
                Vector3 expectedNormal = (p <= 2) ? Vector3.up :
                                         (p == 3) ? new Vector3(mid.x, 0f, mid.z).normalized :
                                         Vector3.down;

                InteractableFoodItem.AddTriangleSafe(verts, tris, i0, i2, i1, expectedNormal);
                InteractableFoodItem.AddTriangleSafe(verts, tris, i1, i2, i3, expectedNormal);
            }
        }

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    public static GameObject CreateSunnySideUpEggModel(string name, out MeshRenderer whiteRenderer, out MeshRenderer yolkRenderer)
    {
        GameObject root = new GameObject(name);
        if (cachedSunnyEggWhiteMesh == null) cachedSunnyEggWhiteMesh = BuildSunnyEggWhiteMesh();
        if (cachedSunnyEggYolkMesh == null) cachedSunnyEggYolkMesh = BuildSunnyEggYolkMesh();

        Material whiteMat = InteractableFoodItem.GetOrCreateMaterial(
            "EggWhite_MAT", null, new Color(0.97f, 0.96f, 0.92f), 0.72f, 0.0f);
        Material yolkMat = InteractableFoodItem.GetOrCreateMaterial(
            "EggYolk_MAT", null, new Color(0.98f, 0.64f, 0.08f), 0.88f, 0.0f);

        GameObject whiteGo = new GameObject("Egg_White");
        whiteGo.transform.SetParent(root.transform, false);
        whiteGo.AddComponent<MeshFilter>().sharedMesh = cachedSunnyEggWhiteMesh;
        whiteRenderer = whiteGo.AddComponent<MeshRenderer>();
        whiteRenderer.sharedMaterial = whiteMat;

        GameObject yolkGo = new GameObject("Egg_Yolk");
        yolkGo.transform.SetParent(root.transform, false);
        yolkGo.transform.localPosition = new Vector3(0.003f, 0.0035f, -0.002f);
        yolkGo.AddComponent<MeshFilter>().sharedMesh = cachedSunnyEggYolkMesh;
        yolkRenderer = yolkGo.AddComponent<MeshRenderer>();
        yolkRenderer.sharedMaterial = yolkMat;

        return root;
    }

    private static Mesh BuildSunnyEggWhiteMesh()
    {
        Mesh m = new Mesh();
        m.name = "SunnyEggWhite_Mesh";

        int rings = 10;
        int segs = 28;
        float maxR = 0.048f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        for (int r = 0; r <= rings; r++)
        {
            float vr = (float)r / rings;
            for (int s = 0; s <= segs; s++)
            {
                float us = (float)s / segs;
                float ang = us * Mathf.PI * 2f;

                // Natural organic wavy egg-white perimeter
                float organicR = maxR * (1.0f + 0.14f * Mathf.Sin(ang * 3f + 0.5f) + 0.07f * Mathf.Cos(ang * 5f));
                float rad = vr * organicR;

                float y = 0.0048f * Mathf.Cos(vr * Mathf.PI * 0.5f);
                if (vr >= 0.95f) y = 0.0005f;

                verts.Add(new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad));
                uvs.Add(new Vector2(0.5f + 0.5f * vr * Mathf.Cos(ang), 0.5f + 0.5f * vr * Mathf.Sin(ang)));
            }
        }

        int stride = segs + 1;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segs; s++)
            {
                int i0 = r * stride + s;
                int i1 = i0 + 1;
                int i2 = (r + 1) * stride + s;
                int i3 = i2 + 1;

                InteractableFoodItem.AddTriangleSafe(verts, tris, i0, i2, i1, Vector3.up);
                InteractableFoodItem.AddTriangleSafe(verts, tris, i1, i2, i3, Vector3.up);
            }
        }

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static Mesh BuildSunnyEggYolkMesh()
    {
        Mesh m = new Mesh();
        m.name = "SunnyEggYolk_Mesh";

        int rings = 10;
        int segs = 24;
        float maxR = 0.0185f;
        float maxH = 0.011f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        for (int r = 0; r <= rings; r++)
        {
            float vr = (float)r / rings;
            float rad = Mathf.Sin(vr * Mathf.PI * 0.5f) * maxR;
            float y = Mathf.Cos(vr * Mathf.PI * 0.5f) * maxH;

            for (int s = 0; s <= segs; s++)
            {
                float us = (float)s / segs;
                float ang = us * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad));
                uvs.Add(new Vector2(us, vr));
            }
        }

        int stride = segs + 1;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segs; s++)
            {
                int i0 = r * stride + s;
                int i1 = i0 + 1;
                int i2 = (r + 1) * stride + s;
                int i3 = i2 + 1;

                InteractableFoodItem.AddTriangleSafe(verts, tris, i0, i2, i1, Vector3.up);
                InteractableFoodItem.AddTriangleSafe(verts, tris, i1, i2, i3, Vector3.up);
            }
        }

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    public static void PopulateCookedEggAndBaconsOnPlate(Transform plateRoot)
    {
        Transform existing = plateRoot.Find("Plated_Food_Contents");
        if (existing != null) Object.Destroy(existing.gameObject);

        GameObject foodRoot = new GameObject("Plated_Food_Contents");
        foodRoot.transform.SetParent(plateRoot, false);
        foodRoot.transform.localPosition = new Vector3(0f, 0.0105f, 0f);
        foodRoot.transform.localRotation = Quaternion.identity;

        // 5 distinct bite groups so during the 15s POV eating animation each bite removes a real piece from the plate!
        Material cookedBaconMat = InteractableFoodItem.GetOrCreateMaterial(
            "CookedBacon_MAT", "Assets/FurnishedCabin/Textures/BaconStrip_Albedo.png", new Color(0.68f, 0.36f, 0.28f), 0.72f);

        // Bite 0 & Bite 1: Bacon Strip 1 (split into 2 half-strips)
        GameObject bite0 = new GameObject("Bite_0");
        bite0.transform.SetParent(foodRoot.transform, false);
        bite0.transform.localPosition = new Vector3(-0.025f, 0.002f, 0.026f);
        bite0.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        bite0.transform.localScale = new Vector3(0.45f, 1f, 0.90f);
        bite0.AddComponent<MeshFilter>().sharedMesh = InteractableFoodItem.BuildSculptedBaconStripMesh(0.4f);
        bite0.AddComponent<MeshRenderer>().sharedMaterial = cookedBaconMat;

        GameObject bite1 = new GameObject("Bite_1");
        bite1.transform.SetParent(foodRoot.transform, false);
        bite1.transform.localPosition = new Vector3(0.032f, 0.002f, 0.032f);
        bite1.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        bite1.transform.localScale = new Vector3(0.45f, 1f, 0.90f);
        bite1.AddComponent<MeshFilter>().sharedMesh = InteractableFoodItem.BuildSculptedBaconStripMesh(1.9f);
        bite1.AddComponent<MeshRenderer>().sharedMaterial = cookedBaconMat;

        // Bite 3: Second Cooked Bacon Strip
        GameObject bite3 = new GameObject("Bite_3");
        bite3.transform.SetParent(foodRoot.transform, false);
        bite3.transform.localPosition = new Vector3(0.015f, 0.003f, 0.004f);
        bite3.transform.localRotation = Quaternion.Euler(0f, -8f, 0f);
        bite3.transform.localScale = new Vector3(0.75f, 1f, 0.90f);
        bite3.AddComponent<MeshFilter>().sharedMesh = InteractableFoodItem.BuildSculptedBaconStripMesh(3.1f);
        bite3.AddComponent<MeshRenderer>().sharedMaterial = cookedBaconMat;

        // Bite 2 & Bite 4: Cooked Sunny-Side-Up Egg
        GameObject eggObj = CreateSunnySideUpEggModel("Cooked_SunnyEgg", out MeshRenderer wr, out MeshRenderer yr);
        eggObj.transform.SetParent(foodRoot.transform, false);
        eggObj.transform.localPosition = new Vector3(-0.014f, 0.001f, -0.026f);
        eggObj.transform.localScale = new Vector3(0.92f, 1.0f, 0.92f);

        // Name white & yolk so bites 2 and 4 consume them cleanly
        if (wr != null) wr.gameObject.name = "Bite_2";
        if (yr != null) yr.gameObject.name = "Bite_4";
    }

    public static GameObject CreateStainlessForkModel(string name)
    {
        GameObject go = new GameObject(name);
        if (cachedForkMesh == null) cachedForkMesh = BuildStainlessForkMesh();

        Material steelMat = InteractableFoodItem.GetOrCreateMaterial(
            "Stainless_Cutlery_MAT", null, new Color(0.85f, 0.87f, 0.90f), 0.86f, 0.90f);

        go.AddComponent<MeshFilter>().sharedMesh = cachedForkMesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = steelMat;
        return go;
    }

    private static Mesh BuildStainlessForkMesh()
    {
        Mesh m = new Mesh();
        m.name = "StainlessFork_Mesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float thick = 0.0024f;

        // 1. Contoured Handle (z: -0.095 to 0.015)
        AddBoxPart(verts, uvs, tris, new Vector3(0f, 0f, -0.045f), new Vector3(0.0065f, thick, 0.052f));

        // 2. Narrow Neck (z: 0.005 to 0.032)
        AddBoxPart(verts, uvs, tris, new Vector3(0f, 0.001f, 0.018f), new Vector3(0.0040f, thick, 0.016f));

        // 3. Flared Tine Bridge (z: 0.032 to 0.040)
        AddBoxPart(verts, uvs, tris, new Vector3(0f, 0.0015f, 0.036f), new Vector3(0.0125f, thick, 0.005f));

        // 4. Four Individual Pointed Tines (z: 0.040 to 0.086)
        float[] tineX = new float[] { -0.0096f, -0.0032f, 0.0032f, 0.0096f };
        for (int t = 0; t < tineX.Length; t++)
        {
            AddBoxPart(verts, uvs, tris, new Vector3(tineX[t], 0.002f, 0.063f), new Vector3(0.0014f, thick * 0.85f, 0.023f));
        }

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    public static GameObject CreateStainlessSpoonModel(string name)
    {
        GameObject go = new GameObject(name);
        if (cachedSpoonMesh == null) cachedSpoonMesh = BuildStainlessSpoonMesh();

        Material steelMat = InteractableFoodItem.GetOrCreateMaterial(
            "Stainless_Cutlery_MAT", null, new Color(0.85f, 0.87f, 0.90f), 0.86f, 0.90f);

        go.AddComponent<MeshFilter>().sharedMesh = cachedSpoonMesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = steelMat;
        return go;
    }

    private static Mesh BuildStainlessSpoonMesh()
    {
        Mesh m = new Mesh();
        m.name = "StainlessSpoon_Mesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float thick = 0.0024f;

        // 1. Handle
        AddBoxPart(verts, uvs, tris, new Vector3(0f, 0f, -0.045f), new Vector3(0.0065f, thick, 0.052f));

        // 2. Neck
        AddBoxPart(verts, uvs, tris, new Vector3(0f, 0.001f, 0.018f), new Vector3(0.0040f, thick, 0.016f));

        // 3. Concave Oval Spoon Bowl (z: 0.032 to 0.084, center z = 0.058)
        int rings = 6;
        int segs = 16;
        float rx = 0.016f;
        float rz = 0.025f;
        int baseIdx = verts.Count;

        for (int r = 0; r <= rings; r++)
        {
            float vr = (float)r / rings;
            float depth = -0.0055f * Mathf.Cos(vr * Mathf.PI * 0.5f);
            for (int s = 0; s <= segs; s++)
            {
                float us = (float)s / segs;
                float ang = us * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * rx * vr;
                float z = 0.058f + Mathf.Sin(ang) * rz * vr;
                verts.Add(new Vector3(x, 0.002f + depth, z));
                uvs.Add(new Vector2(us, vr));
            }
        }

        int stride = segs + 1;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segs; s++)
            {
                int i0 = baseIdx + r * stride + s;
                int i1 = i0 + 1;
                int i2 = baseIdx + (r + 1) * stride + s;
                int i3 = i2 + 1;

                InteractableFoodItem.AddTriangleSafe(verts, tris, i0, i2, i1, Vector3.up);
                InteractableFoodItem.AddTriangleSafe(verts, tris, i1, i2, i3, Vector3.up);
            }
        }

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static void AddBoxPart(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 center, Vector3 halfExtents)
    {
        float x0 = center.x - halfExtents.x, x1 = center.x + halfExtents.x;
        float y0 = center.y - halfExtents.y, y1 = center.y + halfExtents.y;
        float z0 = center.z - halfExtents.z, z1 = center.z + halfExtents.z;

        Vector2 uv00 = new Vector2(0, 0), uv10 = new Vector2(1, 0), uv11 = new Vector2(1, 1), uv01 = new Vector2(0, 1);

        // Top
        InteractableFoodItem.AddQuadSafe(verts, uvs, tris,
            new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1),
            Vector3.up, uv00, uv10, uv11, uv01);
        // Bottom
        InteractableFoodItem.AddQuadSafe(verts, uvs, tris,
            new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1),
            Vector3.down, uv00, uv10, uv11, uv01);
        // Front
        InteractableFoodItem.AddQuadSafe(verts, uvs, tris,
            new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1),
            Vector3.forward, uv00, uv10, uv11, uv01);
        // Back
        InteractableFoodItem.AddQuadSafe(verts, uvs, tris,
            new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0),
            Vector3.back, uv00, uv10, uv11, uv01);
        // Left
        InteractableFoodItem.AddQuadSafe(verts, uvs, tris,
            new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0),
            Vector3.left, uv00, uv10, uv11, uv01);
        // Right
        InteractableFoodItem.AddQuadSafe(verts, uvs, tris,
            new Vector3(x1, y0, z1), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1),
            Vector3.right, uv00, uv10, uv11, uv01);
    }

    public static Mesh GetOrBuildFlamePetalMesh()
    {
        if (cachedFlamePetalMesh != null) return cachedFlamePetalMesh;

        Mesh m = new Mesh();
        m.name = "GasFlamePetal_Mesh";

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        // Double-sided low-profile gas flame tongue that stays strictly beneath the pan bottom
        float w = 0.0065f;
        float h = 0.0080f;
        float outZ = 0.022f;

        InteractableFoodItem.AddQuadSafe(verts, uvs, tris,
            new Vector3(-w, 0f, 0f), new Vector3(w, 0f, 0f),
            new Vector3(w * 0.25f, h, outZ), new Vector3(-w * 0.25f, h, outZ),
            Vector3.up, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));

        InteractableFoodItem.AddQuadSafe(verts, uvs, tris,
            new Vector3(-w, 0f, 0f), new Vector3(w, 0f, 0f),
            new Vector3(w * 0.25f, h, outZ), new Vector3(-w * 0.25f, h, outZ),
            Vector3.down, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));

        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        cachedFlamePetalMesh = m;
        return m;
    }
}

// =============================================================================
// 1. KITCHEN COUNTER SURFACE: PLACE EGG TRAY & BACON PACK ON KITCHENBENCH_BASE
// =============================================================================
public class CabinKitchenCounterSurface : MonoBehaviour, IInteractable
{
    public Transform kitchenBenchRoot;

    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;
        if (HouseSceneStoryController.IsReturnFoodToFridgeUnlocked) return string.Empty;

        InteractableFoodItem itemToPlace = GetCandidateFoodItemToPlace();
        if (itemToPlace == null) return string.Empty;

        if (string.Equals(itemToPlace.itemName, "EGGS", System.StringComparison.OrdinalIgnoreCase))
        {
            return "E to Place Egg Tray on Counter";
        }
        if (string.Equals(itemToPlace.itemName, "BACON", System.StringComparison.OrdinalIgnoreCase))
        {
            return "E to Place Bacon Pack on Counter";
        }
        return string.Empty;
    }

    public void Interact()
    {
        if (HouseSceneStoryController.IsDialogueActive) return;
        if (HouseSceneStoryController.IsReturnFoodToFridgeUnlocked) return;

        InteractableFoodItem itemToPlace = GetCandidateFoodItemToPlace();
        if (itemToPlace == null) return;

        Transform parent = kitchenBenchRoot != null ? kitchenBenchRoot : transform;

        if (string.Equals(itemToPlace.itemName, "EGGS", System.StringComparison.OrdinalIgnoreCase))
        {
            // Place Egg Tray on right marble countertop above door03
            itemToPlace.PlaceOnCounter(parent, new Vector3(-0.68f, 0.946f, 0.06f), Quaternion.Euler(0f, 4f, 0f));
        }
        else if (string.Equals(itemToPlace.itemName, "BACON", System.StringComparison.OrdinalIgnoreCase))
        {
            // Place Bacon Pack next to Egg Tray on marble countertop above door04
            itemToPlace.PlaceOnCounter(parent, new Vector3(-1.02f, 0.946f, 0.06f), Quaternion.Euler(0f, -5f, 0f));
        }

        // Check if both food items are now placed on the counter
        if (InteractableFoodItem.AreBothEggAndBaconPlacedOnCounter())
        {
            HouseSceneStoryController.NotifyFoodPlacedOnCounter();
        }
    }

    private InteractableFoodItem GetCandidateFoodItemToPlace()
    {
        return InteractableFoodItem.FindActiveOrHeldFoodItem();
    }
}

// =============================================================================
// 2. HELD / PICKABLE COOKING ITEM (FRYING PAN, 1 EGG, 2 BACONS, PLATE, FORK)
// =============================================================================
public class CabinHeldCookingItem : MonoBehaviour, IInteractable
{
    public string itemName = "FRYING PAN";
    public string pickupPrompt = "E to Pick Up Frying Pan";
    public CabinDoorInteractable requiredCabinetDoor;

    public Vector3 viewmodelOffset = new Vector3(0.24f, -0.24f, 0.48f);
    public Vector3 viewmodelRotation = new Vector3(12f, -155f, -6f);
    public Vector3 viewmodelScale = new Vector3(0.85f, 0.85f, 0.85f);

    private bool isPickedUp = false;
    private bool isPlacedInWorld = false;
    private int assignedSlot = -1;
    private Transform cameraTransform;
    private Collider[] allColliders;
    private Renderer[] allRenderers;
    private AudioSource audioSource;
    private AudioClip soundClip;

    public bool IsPickedUp => isPickedUp;
    public int AssignedSlot => assignedSlot;
    public bool IsSuppressingViewmodelFollow { get; set; }

    private void Awake()
    {
        RefreshRenderers();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.4f;
        audioSource.playOnAwake = false;
        GeneratePickupClip();
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
        if (HorrorHotbar.Instance != null)
        {
            HorrorHotbar.Instance.OnSlotChanged -= OnHotbarSlotChanged;
        }
    }

    public void RefreshRenderers()
    {
        allColliders = GetComponentsInChildren<Collider>(true);
        allRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void LateUpdate()
    {
        if (!isPickedUp || isPlacedInWorld || IsSuppressingViewmodelFollow) return;

        bool isActive = HorrorHotbar.Instance == null || HorrorHotbar.Instance.IsSlotActive(assignedSlot);
        if (isActive && cameraTransform != null)
        {
            transform.localPosition = viewmodelOffset;
            transform.localRotation = Quaternion.Euler(viewmodelRotation);
            transform.localScale = viewmodelScale;
        }
    }

    private void OnHotbarSlotChanged(int activeSlot)
    {
        if (!isPickedUp || isPlacedInWorld) return;
        SetVisible(activeSlot == assignedSlot);
    }

    public void SetVisible(bool visible)
    {
        RefreshRenderers();
        if (allRenderers == null) return;
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null) allRenderers[i].enabled = visible;
        }
    }

    public string GetPromptText()
    {
        if (isPickedUp || isPlacedInWorld) return string.Empty;
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;
        if (requiredCabinetDoor != null && !requiredCabinetDoor.isOpen) return string.Empty;

        if (itemName.Equals("FRYING PAN", System.StringComparison.OrdinalIgnoreCase) && !HouseSceneStoryController.IsStoveAndPanUnlocked)
        {
            return string.Empty;
        }

        if (HorrorHotbar.IsInventoryFull)
        {
            return "Inventory Full";
        }

        return pickupPrompt;
    }

    public void Interact()
    {
        if (isPickedUp || isPlacedInWorld) return;
        if (HouseSceneStoryController.IsDialogueActive) return;
        if (requiredCabinetDoor != null && !requiredCabinetDoor.isOpen) return;

        if (itemName.Equals("FRYING PAN", System.StringComparison.OrdinalIgnoreCase) && !HouseSceneStoryController.IsStoveAndPanUnlocked)
        {
            return;
        }

        if (HorrorHotbar.IsInventoryFull) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return;

        EquipToCamera(cam.transform);
    }

    public void EquipToCamera(Transform camTransform)
    {
        RefreshRenderers();
        cameraTransform = camTransform;
        isPickedUp = true;
        isPlacedInWorld = false;

        if (allColliders != null)
        {
            for (int i = 0; i < allColliders.Length; i++)
            {
                if (allColliders[i] != null) allColliders[i].enabled = false;
            }
        }

        HorrorHotbar hotbar = HorrorHotbar.EnsureExists();
        assignedSlot = 0;
        if (hotbar != null)
        {
            bool foundEmpty = false;
            for (int s = 0; s < hotbar.slotCount; s++)
            {
                if (!hotbar.HasItem(s))
                {
                    assignedSlot = s;
                    foundEmpty = true;
                    break;
                }
            }
            if (!foundEmpty) assignedSlot = hotbar.activeSlotIndex;

            hotbar.SetSlot(assignedSlot, itemName);
            hotbar.SetActiveSlot(assignedSlot);
        }

        transform.SetParent(cameraTransform, false);
        transform.localPosition = viewmodelOffset;
        transform.localRotation = Quaternion.Euler(viewmodelRotation);
        transform.localScale = viewmodelScale;

        SetVisible(true);
        if (audioSource != null && soundClip != null) audioSource.PlayOneShot(soundClip, 0.80f);
    }

    public void PlaceInWorld(Transform parent, Vector3 localPos, Quaternion localRot, Vector3 scale)
    {
        isPickedUp = false;
        isPlacedInWorld = true;

        if (HorrorHotbar.Instance != null && assignedSlot >= 0)
        {
            HorrorHotbar.Instance.ClearSlot(assignedSlot);
        }
        assignedSlot = -1;

        transform.SetParent(parent, false);
        transform.localPosition = localPos;
        transform.localRotation = localRot;
        transform.localScale = scale;

        SetVisible(true);
        if (audioSource != null && soundClip != null) audioSource.PlayOneShot(soundClip, 0.85f);
    }

    public void ConsumeAndDestroy()
    {
        if (HorrorHotbar.Instance != null && assignedSlot >= 0)
        {
            HorrorHotbar.Instance.ClearSlot(assignedSlot);
        }
        assignedSlot = -1;
        Destroy(gameObject);
    }

    private void GeneratePickupClip()
    {
        int sr = 44100;
        int samples = Mathf.RoundToInt(0.18f * sr);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float clink = Mathf.Sin(2f * Mathf.PI * 580f * t) * Mathf.Exp(-t * 36f) * 0.28f;
            float rustle = (Random.value * 2f - 1f) * Mathf.Exp(-t * 28f) * 0.22f;
            data[i] = Mathf.Clamp(clink + rustle, -1f, 1f);
        }
        soundClip = AudioClip.Create("CookingItem_Clip", samples, 1, sr, false);
        soundClip.SetData(data, 0);
    }
}

// =============================================================================
// 3. CABINET DOOR01 PLATE STACK INTERACTABLE (PICK UP 1 PLATE & PUT CLEAN PLATE BACK)
// =============================================================================
public class CabinPlateStackInteractable : MonoBehaviour, IInteractable
{
    public CabinDoorInteractable requiredCabinetDoor;
    public GameObject topPlateVisual;
    private bool hasPickedUpOnePlate = false;
    private bool hasPutPlateBack = false;
    private AudioSource audioSource;
    private AudioClip placeClip;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.85f;
        audioSource.minDistance = 0.4f;
        audioSource.maxDistance = 8f;
        audioSource.playOnAwake = false;

        int sr = 44100;
        int samples = Mathf.RoundToInt(0.18f * sr);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float clink = Mathf.Sin(2f * Mathf.PI * 1950f * t) * Mathf.Exp(-t * 42f) * 0.35f;
            float thud = Mathf.Sin(2f * Mathf.PI * 240f * t) * Mathf.Exp(-t * 32f) * 0.25f;
            data[i] = Mathf.Clamp(clink + thud, -1f, 1f);
        }
        placeClip = AudioClip.Create("PlateStack_Clink", samples, 1, sr, false);
        placeClip.SetData(data, 0);
    }

    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;
        if (requiredCabinetDoor != null && !requiredCabinetDoor.isOpen) return string.Empty;

        // Phase 2: Returning the clean washed plate back to the cabinet stack where it came from
        if (HouseSceneStoryController.HasWashedPlate)
        {
            if (hasPutPlateBack || HouseSceneStoryController.HasPutPlateBack) return string.Empty;

            if (IsPlayerHoldingWashedPlate())
            {
                return "E to Put Plate Back";
            }
            return string.Empty;
        }

        // Phase 1: Initial plate pickup
        if (hasPickedUpOnePlate) return string.Empty;

        if (!HouseSceneStoryController.HasCookedFood)
        {
            return "Cook the food first";
        }

        if (HorrorHotbar.IsInventoryFull)
        {
            return "Inventory Full";
        }

        return "E to Pick Up Plate";
    }

    public void Interact()
    {
        if (HouseSceneStoryController.IsDialogueActive) return;
        if (requiredCabinetDoor != null && !requiredCabinetDoor.isOpen) return;

        // Phase 2: Place clean washed plate back onto stack
        if (HouseSceneStoryController.HasWashedPlate)
        {
            if (hasPutPlateBack || HouseSceneStoryController.HasPutPlateBack) return;
            if (!IsPlayerHoldingWashedPlate()) return;

            hasPutPlateBack = true;
            if (topPlateVisual != null) topPlateVisual.SetActive(true);

            if (CabinCookingSystem.HeldWashedCleanPlate != null)
            {
                CabinCookingSystem.HeldWashedCleanPlate.IsSuppressingViewmodelFollow = false;
                CabinCookingSystem.HeldWashedCleanPlate.ConsumeAndDestroy();
                CabinCookingSystem.ClearHeldWashedCleanPlateRef();
            }

            if (HorrorHotbar.Instance != null)
            {
                HorrorHotbar.Instance.ClearItem("PLATE");
            }

            if (audioSource != null && placeClip != null)
            {
                audioSource.PlayOneShot(placeClip, 0.85f);
            }

            HouseSceneStoryController.NotifyPlatePutBack();
            return;
        }

        // Phase 1: Initial plate pickup
        if (hasPickedUpOnePlate) return;
        if (!HouseSceneStoryController.HasCookedFood) return;
        if (HorrorHotbar.IsInventoryFull) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return;

        hasPickedUpOnePlate = true;
        if (topPlateVisual != null) topPlateVisual.SetActive(false);

        CabinCookingSystem.EquipSinglePlate(cam.transform);
    }

    private static bool IsPlayerHoldingWashedPlate()
    {
        if (CabinCookingSystem.HeldWashedCleanPlate != null && CabinCookingSystem.HeldWashedCleanPlate.IsPickedUp)
        {
            return true;
        }
        if (HorrorHotbar.Instance != null)
        {
            for (int i = 0; i < HorrorHotbar.Instance.slotCount; i++)
            {
                if (string.Equals(HorrorHotbar.Instance.GetItemName(i), "PLATE", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }
}

// =============================================================================
// 4. DRAWER DRAW01 MESSY FORK & SPOON INTERACTABLE (PICK UP FORK)
// =============================================================================
public class CabinDrawerForkInteractable : MonoBehaviour, IInteractable
{
    public CabinDoorInteractable requiredDrawer;
    public GameObject topForkVisual;
    private bool hasPickedUpFork = false;

    public string GetPromptText()
    {
        if (hasPickedUpFork) return string.Empty;
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;
        if (requiredDrawer != null && !requiredDrawer.isOpen) return string.Empty;

        if (!CabinCookingSystem.IsPlatedFoodPlacedOnDiningTable)
        {
            return "Place plated food on dining table first";
        }

        if (HorrorHotbar.IsInventoryFull)
        {
            return "Inventory Full";
        }

        return "E to Pick Up Fork";
    }

    public void Interact()
    {
        if (hasPickedUpFork) return;
        if (HouseSceneStoryController.IsDialogueActive) return;
        if (requiredDrawer != null && !requiredDrawer.isOpen) return;
        if (!CabinCookingSystem.IsPlatedFoodPlacedOnDiningTable) return;
        if (HorrorHotbar.IsInventoryFull) return;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return;

        hasPickedUpFork = true;
        if (topForkVisual != null) topForkVisual.SetActive(false);

        CabinCookingSystem.EquipForkUtensil(cam.transform);
    }
}

// =============================================================================
// 5. STOVE COOKING STATION: PLACE PAN, TURN ON STOVE FIRE, FRY 15s, PLATE FOOD
// =============================================================================
public class CabinStoveStation : MonoBehaviour, IInteractable
{
    public bool isPanPlaced = false;
    public bool isStoveOn = false;
    public bool hasEggInPan = false;
    public bool hasBaconInPan = false;
    public bool isCooked = false;
    public bool isFoodTransferredToPlate = false;

    public float cookDuration = 15.0f;
    private float cookTimer = 0f;

    private GameObject burnerFireRoot;
    private List<Transform> flamePetals = new List<Transform>();
    private Light burnerLight;

    private GameObject panEggRoot;
    private MeshRenderer panEggWhiteRenderer;
    private MeshRenderer panEggYolkRenderer;
    private GameObject panBaconsRoot;
    private List<MeshRenderer> panBaconRenderers = new List<MeshRenderer>();

    private AudioSource sizzleAudioSource;
    private AudioSource oneshotAudioSource;
    private AudioClip igniteClip;
    private AudioClip sizzleLoopClip;

    private const float PanSeatedLocalY = 0.015f;
    private const float PanCookingSurfaceLocalY = 0.0265f;

    private void Awake()
    {
        cookDuration = 15.0f;
        BuildBurnerFireRing();
        BuildPanFoodVisuals();
        SetupStoveAudio();
    }

    private void BuildBurnerFireRing()
    {
        burnerFireRoot = new GameObject("Stove_BurnerFire_Ring");
        burnerFireRoot.transform.SetParent(transform, false);
        // Positioned strictly on the burner grate below the pan base (PanSeatedLocalY = 0.015f)
        burnerFireRoot.transform.localPosition = new Vector3(0f, 0.002f, 0f);

        Material blueFlameMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
        blueFlameMat.color = new Color(0.18f, 0.65f, 1.0f);
        if (blueFlameMat.HasProperty("_BaseColor")) blueFlameMat.SetColor("_BaseColor", new Color(0.18f, 0.65f, 1.0f));

        Material orangeFlameMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
        orangeFlameMat.color = new Color(1.0f, 0.56f, 0.12f);
        if (orangeFlameMat.HasProperty("_BaseColor")) orangeFlameMat.SetColor("_BaseColor", new Color(1.0f, 0.56f, 0.12f));

        Mesh petalMesh = CabinCookingSystem.GetOrBuildFlamePetalMesh();
        int numPetals = 20;
        float ringRadius = 0.086f;

        for (int i = 0; i < numPetals; i++)
        {
            float ang = ((float)i / numPetals) * Mathf.PI * 2f;
            GameObject petal = new GameObject($"FlamePetal_{i}");
            petal.transform.SetParent(burnerFireRoot.transform, false);
            petal.transform.localPosition = new Vector3(Mathf.Cos(ang) * ringRadius, 0f, Mathf.Sin(ang) * ringRadius);
            petal.transform.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(ang), 0.05f, Mathf.Sin(ang)), Vector3.up);

            petal.AddComponent<MeshFilter>().sharedMesh = petalMesh;
            petal.AddComponent<MeshRenderer>().sharedMaterial = (i % 3 == 0) ? orangeFlameMat : blueFlameMat;
            flamePetals.Add(petal.transform);
        }

        GameObject lightGo = new GameObject("Burner_FlameLight");
        lightGo.transform.SetParent(burnerFireRoot.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.005f, 0f);
        burnerLight = lightGo.AddComponent<Light>();
        burnerLight.type = LightType.Point;
        burnerLight.color = new Color(1.0f, 0.58f, 0.24f);
        burnerLight.range = 0.60f;
        burnerLight.intensity = 0.32f;
        burnerLight.shadows = LightShadows.None;

        burnerFireRoot.SetActive(false);
    }

    private void BuildPanFoodVisuals()
    {
        // 1. Sunny-side-up Egg resting firmly on top of the Frying Pan's inner cooking surface
        panEggRoot = CabinCookingSystem.CreateSunnySideUpEggModel("Pan_SunnyEgg", out panEggWhiteRenderer, out panEggYolkRenderer);
        panEggRoot.transform.SetParent(transform, false);
        panEggRoot.transform.localPosition = new Vector3(-0.022f, PanCookingSurfaceLocalY, -0.016f);
        panEggRoot.transform.localScale = new Vector3(0.84f, 1.0f, 0.84f);
        panEggRoot.SetActive(false);

        // 2. Two Bacon Strips resting firmly on top of the Frying Pan's inner cooking surface
        panBaconsRoot = new GameObject("Pan_TwoBacons");
        panBaconsRoot.transform.SetParent(transform, false);
        panBaconsRoot.transform.localPosition = new Vector3(0.020f, PanCookingSurfaceLocalY + 0.003f, 0.014f);

        Material rawBaconMat = InteractableFoodItem.GetOrCreateMaterial(
            "PanBacon_Dynamic_MAT", "Assets/FurnishedCabin/Textures/BaconStrip_Albedo.png", Color.white, 0.68f);

        for (int i = 0; i < 2; i++)
        {
            GameObject strip = new GameObject($"Pan_Bacon_0{i + 1}");
            strip.transform.SetParent(panBaconsRoot.transform, false);
            strip.transform.localPosition = new Vector3((i - 0.5f) * 0.028f, 0f, 0f);
            strip.transform.localRotation = Quaternion.Euler(0f, 80f + i * 6f, 0f);
            strip.transform.localScale = new Vector3(0.58f, 1.0f, 0.82f);

            strip.AddComponent<MeshFilter>().sharedMesh = InteractableFoodItem.BuildSculptedBaconStripMesh(i * 1.6f);
            MeshRenderer mr = strip.AddComponent<MeshRenderer>();
            mr.material = new Material(rawBaconMat);
            panBaconRenderers.Add(mr);
        }

        panBaconsRoot.SetActive(false);
    }

    private void SetupStoveAudio()
    {
        oneshotAudioSource = gameObject.AddComponent<AudioSource>();
        oneshotAudioSource.spatialBlend = 0.85f;
        oneshotAudioSource.playOnAwake = false;

        sizzleAudioSource = gameObject.AddComponent<AudioSource>();
        sizzleAudioSource.spatialBlend = 0.90f;
        sizzleAudioSource.loop = true;
        sizzleAudioSource.playOnAwake = false;
        sizzleAudioSource.volume = 0.42f;

        int sr = 44100;
        // Igniter click + gas whoosh
        int igSamples = Mathf.RoundToInt(0.45f * sr);
        float[] igData = new float[igSamples];
        for (int i = 0; i < igSamples; i++)
        {
            float t = (float)i / sr;
            float click1 = (t < 0.03f) ? Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-t * 180f) * 0.7f : 0f;
            float click2 = (t >= 0.08f && t < 0.11f) ? Mathf.Sin(2f * Mathf.PI * 2400f * (t - 0.08f)) * Mathf.Exp(-(t - 0.08f) * 180f) * 0.7f : 0f;
            float whoosh = (t >= 0.10f) ? (Random.value * 2f - 1f) * Mathf.Exp(-(t - 0.10f) * 8f) * 0.45f : 0f;
            igData[i] = Mathf.Clamp(click1 + click2 + whoosh, -1f, 1f);
        }
        igniteClip = AudioClip.Create("Stove_Ignite", igSamples, 1, sr, false);
        igniteClip.SetData(igData, 0);

        // Sizzle loop (1.5s seamless frying sizzle)
        int szSamples = Mathf.RoundToInt(1.5f * sr);
        float[] szData = new float[szSamples];
        float lp = 0f;
        for (int i = 0; i < szSamples; i++)
        {
            float white = Random.value * 2f - 1f;
            lp = Mathf.Lerp(lp, white, 0.25f);
            float pop = (Random.value > 0.996f) ? (Random.value * 2f - 1f) * 0.65f : 0f;
            szData[i] = Mathf.Clamp((white - lp) * 0.32f + pop, -1f, 1f);
        }
        sizzleLoopClip = AudioClip.Create("Stove_SizzleLoop", szSamples, 1, sr, false);
        sizzleLoopClip.SetData(szData, 0);
        sizzleAudioSource.clip = sizzleLoopClip;
    }

    private void Update()
    {
        // Animate 3D gas burner flames when stove is ON (capped so flames never clip through the pan bottom)
        if (isStoveOn && burnerFireRoot != null && burnerFireRoot.activeSelf)
        {
            float time = Time.time * 18f;
            for (int i = 0; i < flamePetals.Count; i++)
            {
                if (flamePetals[i] == null) continue;
                float sy = 0.78f + 0.16f * Mathf.Sin(time + i * 1.7f);
                float sz = 0.88f + 0.22f * Mathf.Sin(time + i * 1.7f);
                flamePetals[i].localScale = new Vector3(1.0f, sy, sz);
            }
            if (burnerLight != null)
            {
                burnerLight.intensity = 0.30f + 0.05f * Mathf.Sin(time * 1.3f);
            }
        }

        // Progress 15-second cooking timer once Stove is ON and both Egg & 2 Bacons are in the pan
        if (isPanPlaced && isStoveOn && hasEggInPan && hasBaconInPan && !isCooked)
        {
            if (!sizzleAudioSource.isPlaying) sizzleAudioSource.Play();

            cookTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(cookTimer / cookDuration);

            // Gradually transition egg white & bacon colors from raw to crispy cooked
            if (panEggWhiteRenderer != null)
            {
                Color rawWhite = new Color(0.92f, 0.92f, 0.88f);
                Color cookedWhite = new Color(0.99f, 0.96f, 0.86f);
                Color c = Color.Lerp(rawWhite, cookedWhite, progress);
                panEggWhiteRenderer.material.color = c;
                if (panEggWhiteRenderer.material.HasProperty("_BaseColor"))
                    panEggWhiteRenderer.material.SetColor("_BaseColor", c);
            }

            for (int i = 0; i < panBaconRenderers.Count; i++)
            {
                if (panBaconRenderers[i] != null)
                {
                    Color rawTint = Color.white;
                    Color cookedTint = new Color(0.65f, 0.34f, 0.26f);
                    Color c = Color.Lerp(rawTint, cookedTint, progress);
                    panBaconRenderers[i].material.color = c;
                    if (panBaconRenderers[i].material.HasProperty("_BaseColor"))
                        panBaconRenderers[i].material.SetColor("_BaseColor", c);
                }
            }

            if (cookTimer >= cookDuration)
            {
                cookTimer = cookDuration;
                isCooked = true;
                isStoveOn = false;
                if (burnerFireRoot != null) burnerFireRoot.SetActive(false);
                if (sizzleAudioSource.isPlaying) sizzleAudioSource.Stop();
                HouseSceneStoryController.NotifyFoodCooked();
            }
        }
    }

    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;
        if (!HouseSceneStoryController.IsStoveAndPanUnlocked) return string.Empty;
        if (isFoodTransferredToPlate) return string.Empty;

        // 1. Before Frying Pan is placed on stove
        if (!isPanPlaced)
        {
            if (CabinCookingSystem.HeldFryingPan != null && CabinCookingSystem.HeldFryingPan.IsPickedUp)
            {
                return "E to place frying pan on stove";
            }
            return string.Empty;
        }

        // 2. Once Frying Pan is placed on stove, allow turning on the stove
        if (!isStoveOn && !isCooked)
        {
            return "E to turn on the stove";
        }

        // 3. Adding 1 Egg and 2 Bacons to the Frying Pan
        bool holdingEgg = CabinCookingSystem.HeldSingleEgg != null && CabinCookingSystem.HeldSingleEgg.IsPickedUp;
        bool holdingBacon = CabinCookingSystem.HeldTwoBacons != null && CabinCookingSystem.HeldTwoBacons.IsPickedUp;

        if (!hasEggInPan && holdingEgg)
        {
            return "E to Fry 1 Egg on Pan";
        }
        if (!hasBaconInPan && holdingBacon)
        {
            return "E to Fry 2 Bacons on Pan";
        }

        if (!hasEggInPan || !hasBaconInPan)
        {
            if (!hasEggInPan && !hasBaconInPan) return "Put 1 Egg and 2 Bacons in Frying Pan";
            if (!hasEggInPan) return "Put 1 Egg in Frying Pan";
            return "Put 2 Bacons in Frying Pan";
        }

        // 4. Waiting 15 seconds for Egg & Bacon to cook
        if (!isCooked)
        {
            int rem = Mathf.CeilToInt(cookDuration - cookTimer);
            return $"Cooking Egg & Bacon... ({rem}s)";
        }

        // 5. Once cooked, put cooked Egg & Bacon onto held Plate
        if (CabinCookingSystem.HeldPlate != null && CabinCookingSystem.HeldPlate.IsPickedUp)
        {
            return "E to Put Cooked Egg & Bacon on Plate";
        }

        return "Pick up a Plate from cabinet first";
    }

    public void Interact()
    {
        if (HouseSceneStoryController.IsDialogueActive) return;
        if (!HouseSceneStoryController.IsStoveAndPanUnlocked) return;
        if (isFoodTransferredToPlate) return;

        // 1. Place Frying Pan on stove (with physical settle onto the burner grate)
        if (!isPanPlaced)
        {
            if (CabinCookingSystem.HeldFryingPan != null && CabinCookingSystem.HeldFryingPan.IsPickedUp)
            {
                isPanPlaced = true;
                Transform panTr = CabinCookingSystem.HeldFryingPan.transform;
                CabinCookingSystem.HeldFryingPan.PlaceInWorld(
                    transform, new Vector3(0f, PanSeatedLocalY, 0f), Quaternion.Euler(0f, -22f, 0f), Vector3.one);
                StartCoroutine(PhysicalPanSettleRoutine(panTr, new Vector3(0f, PanSeatedLocalY, 0f)));
            }
            return;
        }

        // 2. Turn on the stove with fire at bottom of the Frying Pan
        if (!isStoveOn && !isCooked)
        {
            isStoveOn = true;
            if (burnerFireRoot != null) burnerFireRoot.SetActive(true);
            if (oneshotAudioSource != null && igniteClip != null)
            {
                oneshotAudioSource.PlayOneShot(igniteClip, 0.85f);
            }
            return;
        }

        // 3. Add 1 Egg or 2 Bacons to the pan with gravity drop & non-penetrating pan surface collision
        bool holdingEgg = CabinCookingSystem.HeldSingleEgg != null && CabinCookingSystem.HeldSingleEgg.IsPickedUp;
        bool holdingBacon = CabinCookingSystem.HeldTwoBacons != null && CabinCookingSystem.HeldTwoBacons.IsPickedUp;

        if (!hasEggInPan && holdingEgg)
        {
            hasEggInPan = true;
            if (panEggRoot != null)
            {
                panEggRoot.SetActive(true);
                StartCoroutine(PhysicalFoodDropIntoPanRoutine(
                    panEggRoot.transform,
                    new Vector3(-0.022f, PanCookingSurfaceLocalY, -0.016f),
                    new Vector3(0.84f, 1.0f, 0.84f),
                    true));
            }
            CabinCookingSystem.HeldSingleEgg.ConsumeAndDestroy();
            if (!sizzleAudioSource.isPlaying) sizzleAudioSource.Play();
            return;
        }

        if (!hasBaconInPan && holdingBacon)
        {
            hasBaconInPan = true;
            if (panBaconsRoot != null)
            {
                panBaconsRoot.SetActive(true);
                StartCoroutine(PhysicalFoodDropIntoPanRoutine(
                    panBaconsRoot.transform,
                    new Vector3(0.020f, PanCookingSurfaceLocalY + 0.003f, 0.014f),
                    Vector3.one,
                    false));
            }
            CabinCookingSystem.HeldTwoBacons.ConsumeAndDestroy();
            if (!sizzleAudioSource.isPlaying) sizzleAudioSource.Play();
            return;
        }

        // 4. Transfer cooked Egg & 2 Bacons onto held Plate
        if (isCooked && !isFoodTransferredToPlate)
        {
            if (CabinCookingSystem.HeldPlate != null && CabinCookingSystem.HeldPlate.IsPickedUp)
            {
                isFoodTransferredToPlate = true;
                if (panEggRoot != null) panEggRoot.SetActive(false);
                if (panBaconsRoot != null) panBaconsRoot.SetActive(false);

                CabinCookingSystem.AttachCookedFoodToHeldPlate();
                HouseSceneStoryController.NotifyFoodCookedAndPlated();
            }
        }
    }

    private IEnumerator PhysicalPanSettleRoutine(Transform panTransform, Vector3 restLocalPos)
    {
        if (panTransform == null) yield break;

        float dropHeight = 0.045f;
        float velocityY = 0f;
        float gravity = -1.8f;
        float currentY = restLocalPos.y + dropHeight;
        int bounces = 0;

        while (bounces < 2)
        {
            velocityY += gravity * Time.deltaTime;
            currentY += velocityY * Time.deltaTime;

            if (currentY <= restLocalPos.y)
            {
                currentY = restLocalPos.y;
                velocityY = -velocityY * 0.22f;
                bounces++;
            }

            panTransform.localPosition = new Vector3(restLocalPos.x, currentY, restLocalPos.z);
            yield return null;
        }

        panTransform.localPosition = restLocalPos;
    }

    private IEnumerator PhysicalFoodDropIntoPanRoutine(Transform foodTransform, Vector3 targetSurfacePos, Vector3 targetScale, bool isEgg)
    {
        if (foodTransform == null) yield break;

        float currentY = targetSurfacePos.y + 0.085f;
        float velocityY = 0f;
        float gravity = -2.4f;
        int bounceCount = 0;

        if (isEgg)
        {
            foodTransform.localScale = new Vector3(targetScale.x * 0.65f, targetScale.y * 1.35f, targetScale.z * 0.65f);
        }

        while (bounceCount < 2)
        {
            velocityY += gravity * Time.deltaTime;
            currentY += velocityY * Time.deltaTime;

            // Strict physical floor clamp at targetSurfacePos.y so food NEVER glitches through the frying pan
            if (currentY <= targetSurfacePos.y)
            {
                currentY = targetSurfacePos.y;
                velocityY = -velocityY * (isEgg ? 0.12f : 0.20f);
                bounceCount++;
            }

            foodTransform.localPosition = new Vector3(targetSurfacePos.x, Mathf.Max(targetSurfacePos.y, currentY), targetSurfacePos.z);

            if (isEgg && bounceCount >= 1)
            {
                foodTransform.localScale = Vector3.Lerp(foodTransform.localScale, targetScale, Time.deltaTime * 18f);
            }

            yield return null;
        }

        foodTransform.localPosition = targetSurfacePos;
        foodTransform.localScale = targetScale;
    }
}

// =============================================================================
// 6. DINING TABLE USER SPOT: PLACE PLATED MEAL & EAT FOR 15 SECONDS WHILE SITTING
// =============================================================================
public class CabinDiningTablePlaceSpot : MonoBehaviour, IInteractable
{
    public bool isPlatePlaced = false;
    public bool isEating = false;
    public bool isFinishedEating = false;
    public bool hasPickedUpDirtyPlate = false;

    public float eatDuration = 15.0f;
    private float eatTimer = 0f;
    private Transform placedPlateTransform;
    private AudioSource eatAudioSource;
    private AudioClip forkClinkClip;
    private AudioClip chewClip;

    private void Awake()
    {
        eatAudioSource = gameObject.AddComponent<AudioSource>();
        eatAudioSource.spatialBlend = 0.3f;
        eatAudioSource.playOnAwake = false;
        GenerateEatingAudioClips();
    }

    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;
        if (isFinishedEating)
        {
            if (HouseSceneStoryController.IsDirtyPlatePickupUnlocked && !hasPickedUpDirtyPlate)
            {
                if (HorrorHotbar.IsInventoryFull)
                {
                    return "Inventory Full";
                }
                return "E to Pick Up Dirty Plate";
            }
            return string.Empty;
        }

        // 1. Placing the plated cooked Egg & 2 Bacons on the dining table spot
        if (!isPlatePlaced)
        {
            if (CabinCookingSystem.HeldPlate != null &&
                CabinCookingSystem.HeldPlate.IsPickedUp &&
                CabinCookingSystem.IsPlateContainingCookedFood)
            {
                return "E to Place Plate on Dining Table";
            }
            return string.Empty;
        }

        // 2. While eating (15s POV animation)
        if (isEating)
        {
            int rem = Mathf.CeilToInt(eatDuration - eatTimer);
            return $"Eating Egg & Bacon... ({rem}s)";
        }

        // 3. Strictly require sitting in the dining chair to eat!
        if (!CabinChairInteractable.IsPlayerSitting)
        {
            if (CabinCookingSystem.HasPickedUpFork)
            {
                return "Sit on chair first to eat";
            }
            return "Pick up Fork from drawer and sit on chair to eat";
        }

        // 4. Sitting in the chair: check for Fork
        if (!CabinCookingSystem.HasPickedUpFork)
        {
            return "Pick up Fork from drawer first";
        }

        return "E to Eat Egg & Bacon";
    }

    public void Interact()
    {
        if (HouseSceneStoryController.IsDialogueActive) return;
        if (isEating) return;

        if (isFinishedEating)
        {
            if (HouseSceneStoryController.IsDirtyPlatePickupUnlocked && !hasPickedUpDirtyPlate)
            {
                if (HorrorHotbar.IsInventoryFull) return;
                hasPickedUpDirtyPlate = true;
                if (placedPlateTransform != null)
                {
                    placedPlateTransform.gameObject.SetActive(false);
                }
                Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
                if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
                if (cam != null)
                {
                    CabinCookingSystem.EquipDirtyPlate(cam.transform);
                }
            }
            return;
        }

        // 1. Place the plated meal on the dining table spot
        if (!isPlatePlaced)
        {
            if (CabinCookingSystem.HeldPlate != null &&
                CabinCookingSystem.HeldPlate.IsPickedUp &&
                CabinCookingSystem.IsPlateContainingCookedFood)
            {
                isPlatePlaced = true;
                CabinCookingSystem.IsPlatedFoodPlacedOnDiningTable = true;

                placedPlateTransform = CabinCookingSystem.HeldPlate.transform;
                CabinCookingSystem.HeldPlate.PlaceInWorld(
                    transform, new Vector3(0f, 0.003f, 0f), Quaternion.identity, Vector3.one);
            }
            return;
        }

        // 2. Eat the meal for 15 seconds (strictly when sitting on the chair with the Fork!)
        if (isPlatePlaced && CabinChairInteractable.IsPlayerSitting && CabinCookingSystem.HasPickedUpFork)
        {
            StartCoroutine(EatMealPOVRoutine());
        }
    }

    private IEnumerator EatMealPOVRoutine()
    {
        isEating = true;
        CabinChairInteractable.IsPlayerEating = true;
        eatTimer = 0f;

        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();

        CabinHeldCookingItem forkItem = CabinCookingSystem.HeldFork;
        Transform forkTr = forkItem != null ? forkItem.transform : null;

        if (forkItem != null && cam != null)
        {
            forkItem.IsSuppressingViewmodelFollow = true;
            forkItem.SetVisible(true);
            forkTr.SetParent(cam.transform, false);
        }

        // Create a temporary 3D morsel on the fork tines that appears during each bite lift
        GameObject forkMorsel = null;
        if (forkTr != null)
        {
            forkMorsel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            forkMorsel.name = "Fork_FoodMorsel";
            forkMorsel.transform.SetParent(forkTr, false);
            forkMorsel.transform.localPosition = new Vector3(0f, 0.006f, 0.068f);
            forkMorsel.transform.localScale = new Vector3(0.024f, 0.008f, 0.024f);
            Object.Destroy(forkMorsel.GetComponent<Collider>());
            forkMorsel.SetActive(false);
        }

        Material baconMorselMat = InteractableFoodItem.GetOrCreateMaterial(
            "Morsel_Bacon_MAT", "Assets/FurnishedCabin/Textures/BaconStrip_Albedo.png", new Color(0.68f, 0.36f, 0.28f), 0.72f);
        Material eggMorselMat = InteractableFoodItem.GetOrCreateMaterial(
            "Morsel_Egg_MAT", null, new Color(0.98f, 0.80f, 0.24f), 0.78f);

        int totalBites = 5;
        float biteDuration = eatDuration / totalBites; // 3.0 seconds per bite (5 * 3.0s = 15.0s)

        Vector3 idleForkLocalPos = new Vector3(0.20f, -0.18f, 0.38f);
        Quaternion idleForkLocalRot = Quaternion.Euler(-20f, 168f, 8f);

        Vector3 mouthForkLocalPos = new Vector3(0.02f, -0.11f, 0.22f);
        Quaternion mouthForkLocalRot = Quaternion.Euler(-5f, 180f, 0f);

        for (int bite = 0; bite < totalBites; bite++)
        {
            float biteElapsed = 0f;
            bool spearedMorsel = false;
            bool ateMorsel = false;

            while (biteElapsed < biteDuration)
            {
                float dt = Time.deltaTime;
                biteElapsed += dt;
                eatTimer = Mathf.Min(eatDuration, bite * biteDuration + biteElapsed);

                float phase = Mathf.Clamp01(biteElapsed / biteDuration);

                if (forkTr != null && cam != null)
                {
                    Vector3 plateWorldTarget = transform.position + new Vector3(0f, 0.025f, 0f);
                    Vector3 plateInCamLocal = cam.transform.InverseTransformPoint(plateWorldTarget);
                    // Clamp reach so it always stays visibly framed in first-person POV
                    plateInCamLocal.x = Mathf.Clamp(plateInCamLocal.x, -0.15f, 0.15f);
                    plateInCamLocal.y = Mathf.Clamp(plateInCamLocal.y, -0.32f, -0.12f);
                    plateInCamLocal.z = Mathf.Clamp(plateInCamLocal.z, 0.35f, 0.62f);
                    Quaternion spearLocalRot = Quaternion.Euler(38f, 175f, 0f);

                    if (phase < 0.32f)
                    {
                        // Phase 1: Lower fork from idle hand position down to the plate on the dining table
                        float t = Mathf.SmoothStep(0f, 1f, phase / 0.32f);
                        forkTr.localPosition = Vector3.Lerp(idleForkLocalPos, plateInCamLocal, t);
                        forkTr.localRotation = Quaternion.Slerp(idleForkLocalRot, spearLocalRot, t);
                    }
                    else if (phase < 0.68f)
                    {
                        // Spear piece of food from the plate onto the fork tines
                        if (!spearedMorsel)
                        {
                            spearedMorsel = true;
                            if (eatAudioSource != null && forkClinkClip != null)
                            {
                                eatAudioSource.PlayOneShot(forkClinkClip, 0.70f);
                            }

                            if (placedPlateTransform != null)
                            {
                                Transform bitePiece = placedPlateTransform.Find($"Plated_Food_Contents/Bite_{bite}");
                                if (bitePiece == null)
                                {
                                    bitePiece = placedPlateTransform.Find($"Plated_Food_Contents/Cooked_SunnyEgg/Bite_{bite}");
                                }
                                if (bitePiece != null) bitePiece.gameObject.SetActive(false);
                            }

                            if (forkMorsel != null)
                            {
                                forkMorsel.GetComponent<MeshRenderer>().sharedMaterial =
                                    (bite == 2 || bite == 4) ? eggMorselMat : baconMorselMat;
                                forkMorsel.SetActive(true);
                            }
                        }

                        // Phase 2: Lift fork with speared egg/bacon up to player's mouth in POV
                        float t = Mathf.SmoothStep(0f, 1f, (phase - 0.32f) / 0.36f);
                        forkTr.localPosition = Vector3.Lerp(plateInCamLocal, mouthForkLocalPos, t);
                        forkTr.localRotation = Quaternion.Slerp(spearLocalRot, mouthForkLocalRot, t);
                    }
                    else
                    {
                        // Phase 3: Eat morsel off the fork & chew with subtle POV chewing motion
                        if (!ateMorsel)
                        {
                            ateMorsel = true;
                            if (forkMorsel != null) forkMorsel.SetActive(false);
                            if (eatAudioSource != null && chewClip != null)
                            {
                                eatAudioSource.PlayOneShot(chewClip, 0.85f);
                            }
                        }

                        float t = Mathf.SmoothStep(0f, 1f, (phase - 0.68f) / 0.32f);
                        float chewBob = Mathf.Sin((phase - 0.68f) * Mathf.PI * 12f) * 0.008f;
                        forkTr.localPosition = Vector3.Lerp(mouthForkLocalPos, idleForkLocalPos, t) + new Vector3(0f, chewBob, 0f);
                        forkTr.localRotation = Quaternion.Slerp(mouthForkLocalRot, idleForkLocalRot, t);
                    }
                }

                yield return null;
            }
        }

        if (forkMorsel != null) Destroy(forkMorsel);
        if (forkItem != null)
        {
            forkItem.IsSuppressingViewmodelFollow = false;
            forkItem.ConsumeAndDestroy();
        }

        eatTimer = eatDuration;
        isEating = false;
        isFinishedEating = true;
        CabinCookingSystem.IsMealFinished = true;
        CabinChairInteractable.IsPlayerEating = false;
        HouseSceneStoryController.NotifyMealFinished();
    }

    private void GenerateEatingAudioClips()
    {
        int sr = 44100;

        // 1. Fork clink on porcelain plate
        int clinkSamples = Mathf.RoundToInt(0.14f * sr);
        float[] clinkData = new float[clinkSamples];
        for (int i = 0; i < clinkSamples; i++)
        {
            float t = (float)i / sr;
            float ring1 = Mathf.Sin(2f * Mathf.PI * 1920f * t) * Mathf.Exp(-t * 48f) * 0.32f;
            float ring2 = Mathf.Sin(2f * Mathf.PI * 3150f * t) * Mathf.Exp(-t * 65f) * 0.20f;
            clinkData[i] = Mathf.Clamp(ring1 + ring2, -1f, 1f);
        }
        forkClinkClip = AudioClip.Create("Fork_Porcelain_Clink", clinkSamples, 1, sr, false);
        forkClinkClip.SetData(clinkData, 0);

        // 2. Crisp bite & chew sound (~0.75s)
        int chewSamples = Mathf.RoundToInt(0.75f * sr);
        float[] chewData = new float[chewSamples];
        for (int i = 0; i < chewSamples; i++)
        {
            float t = (float)i / sr;
            float env1 = Mathf.Exp(-Mathf.Pow((t - 0.10f) * 16f, 2f));
            float env2 = Mathf.Exp(-Mathf.Pow((t - 0.42f) * 14f, 2f));
            float crunch = (Random.value * 2f - 1f) * (env1 + env2 * 0.8f) * 0.35f;
            float low = Mathf.Sin(2f * Mathf.PI * 140f * t) * (env1 + env2) * 0.20f;
            chewData[i] = Mathf.Clamp(crunch + low, -1f, 1f);
        }
        chewClip = AudioClip.Create("Breakfast_Chew", chewSamples, 1, sr, false);
        chewClip.SetData(chewData, 0);
    }
}

// =============================================================================
// 7. KITCHEN SINK STATION: WASH DIRTY PLATE UNDER RUNNING WATER
// =============================================================================
public class CabinKitchenSinkStation : MonoBehaviour, IInteractable
{
    public static bool IsPlayerWashingPlate { get; private set; } = false;

    public bool isWashingActive = false;
    public float washDuration = 10.0f;
    private float washTimer = 0f;

    public static void ResetStaticWashState()
    {
        IsPlayerWashingPlate = false;
    }

    private AudioSource waterAudioSource;
    private AudioSource spongeAudioSource;
    private AudioClip faucetTurnClip;
    private AudioClip waterRunningClip;
    private AudioClip spongeScrubClip;
    private AudioClip cleanClinkClip;

    private GameObject waterStreamGo;
    private GameObject splashRippleGo;
    private GameObject splashPoolGo;
    private Material waterStreamMat;
    private Material dropletMat;
    private readonly List<Transform> waterDroplets = new List<Transform>();
    private readonly List<float> dropletPhases = new List<float>();

    private Vector3 spoutTipBenchLocal = new Vector3(0.92f, 1.025f, 0.08f);
    private Vector3 basinCenterBenchLocal = new Vector3(0.95f, 0.755f, 0.02f);
    private bool visualsBuilt = false;
    private bool isWaterRunning = false;

    private void Awake()
    {
        SetupAudio();
    }

    private void OnDisable()
    {
        if (isWashingActive)
        {
            isWashingActive = false;
            IsPlayerWashingPlate = false;
        }
    }

    private void OnDestroy()
    {
        if (isWashingActive)
        {
            isWashingActive = false;
            IsPlayerWashingPlate = false;
        }
    }

    private void Start()
    {
        if (!visualsBuilt)
        {
            BuildWaterStreamVisuals();
        }
    }

    public void InitializeSinkGeometry(Vector3 spoutTipLocal, Vector3 basinCenterLocal)
    {
        spoutTipBenchLocal = spoutTipLocal;
        basinCenterBenchLocal = basinCenterLocal;
        BuildWaterStreamVisuals();
    }

    private void SetupAudio()
    {
        waterAudioSource = gameObject.AddComponent<AudioSource>();
        waterAudioSource.spatialBlend = 0.80f;
        waterAudioSource.minDistance = 0.5f;
        waterAudioSource.maxDistance = 10f;
        waterAudioSource.playOnAwake = false;

        spongeAudioSource = gameObject.AddComponent<AudioSource>();
        spongeAudioSource.spatialBlend = 0.70f;
        spongeAudioSource.minDistance = 0.5f;
        spongeAudioSource.maxDistance = 10f;
        spongeAudioSource.playOnAwake = false;

        int sr = 44100;

        // 1. Faucet tap turn / squeak
        int tapSamples = Mathf.RoundToInt(0.24f * sr);
        float[] tapData = new float[tapSamples];
        for (int i = 0; i < tapSamples; i++)
        {
            float t = (float)i / sr;
            float squeak = Mathf.Sin(2f * Mathf.PI * (1200f - 400f * t) * t) * Mathf.Exp(-t * 22f) * 0.40f;
            float thunk = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 30f) * 0.35f;
            tapData[i] = Mathf.Clamp(squeak + thunk, -1f, 1f);
        }
        faucetTurnClip = AudioClip.Create("Faucet_Turn", tapSamples, 1, sr, false);
        faucetTurnClip.SetData(tapData, 0);

        // 2. Continuous running water stream loop
        int wSamples = Mathf.RoundToInt(1.6f * sr);
        float[] wData = new float[wSamples];
        float prev = 0f;
        for (int i = 0; i < wSamples; i++)
        {
            float noise = Random.value * 2f - 1f;
            prev = Mathf.Lerp(prev, noise, 0.30f);
            float rush = (noise - prev) * 0.38f;
            float low = Mathf.Sin(2f * Mathf.PI * 220f * (float)i / sr) * 0.08f;
            wData[i] = Mathf.Clamp(rush + low, -1f, 1f);
        }
        waterRunningClip = AudioClip.Create("Faucet_RunningWaterLoop", wSamples, 1, sr, false);
        waterRunningClip.SetData(wData, 0);

        // 3. Wet sponge friction squelch
        int spSamples = Mathf.RoundToInt(0.38f * sr);
        float[] spData = new float[spSamples];
        for (int i = 0; i < spSamples; i++)
        {
            float t = (float)i / sr;
            float sq = Mathf.Sin(2f * Mathf.PI * (850f + 300f * Mathf.Sin(t * 35f)) * t) * Mathf.Exp(-t * 12f) * 0.28f;
            float wet = (Random.value * 2f - 1f) * Mathf.Exp(-t * 10f) * 0.22f;
            spData[i] = Mathf.Clamp(sq + wet, -1f, 1f);
        }
        spongeScrubClip = AudioClip.Create("Sponge_Scrub", spSamples, 1, sr, false);
        spongeScrubClip.SetData(spData, 0);

        // 4. Clean porcelain squeak / clink
        int clSamples = Mathf.RoundToInt(0.20f * sr);
        float[] clData = new float[clSamples];
        for (int i = 0; i < clSamples; i++)
        {
            float t = (float)i / sr;
            float ring = Mathf.Sin(2f * Mathf.PI * 2600f * t) * Mathf.Exp(-t * 40f) * 0.30f;
            clData[i] = Mathf.Clamp(ring, -1f, 1f);
        }
        cleanClinkClip = AudioClip.Create("Porcelain_CleanClink", clSamples, 1, sr, false);
        cleanClinkClip.SetData(clData, 0);
    }

    private static Material CreateWaterMaterial(Color tint, Texture2D tex)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = tint;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        if (tex != null)
        {
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        }

        // Enable URP transparency reliably so water is translucent, bright, and never clipped or pitch black
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
        }
        return mat;
    }

    private static Texture2D CreateAeratedWaterTexture()
    {
        int w = 32;
        int h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point; // Crisp retro PS1 aerated water grain

        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float streak = Mathf.Sin(x * 1.15f + Mathf.Cos(y * 0.25f) * 1.4f) * 0.5f + 0.5f;
                float foam = Mathf.PerlinNoise(x * 0.45f, y * 0.28f);
                float whiteMix = Mathf.Clamp01(streak * 0.45f + foam * 0.65f);
                Color c = Color.Lerp(
                    new Color(0.56f, 0.83f, 0.98f, 0.62f),
                    new Color(0.92f, 0.98f, 1.00f, 0.88f),
                    whiteMix);
                pixels[y * w + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void BuildWaterStreamVisuals()
    {
        visualsBuilt = true;
        Transform parentTransform = transform.parent != null ? transform.parent : transform;

        // Clear previous runtime water children if rebuilt
        Transform oldStream = parentTransform.Find("Faucet_WaterStream");
        if (oldStream != null) Object.Destroy(oldStream.gameObject);
        Transform oldRipple = parentTransform.Find("Sink_SplashRipple");
        if (oldRipple != null) Object.Destroy(oldRipple.gameObject);
        Transform oldPool = parentTransform.Find("Sink_SplashPool");
        if (oldPool != null) Object.Destroy(oldPool.gameObject);
        for (int i = 0; i < waterDroplets.Count; i++)
        {
            if (waterDroplets[i] != null) Object.Destroy(waterDroplets[i].gameObject);
        }
        waterDroplets.Clear();
        dropletPhases.Clear();

        Texture2D waterTex = CreateAeratedWaterTexture();
        waterStreamMat = CreateWaterMaterial(new Color(0.86f, 0.96f, 1.0f, 0.85f), waterTex);
        dropletMat = CreateWaterMaterial(new Color(0.92f, 0.98f, 1.0f, 0.80f), null);

        float topY = spoutTipBenchLocal.y + 0.004f; // Tucked directly inside the faucet nozzle orifice
        float bottomY = basinCenterBenchLocal.y + 0.003f;
        float streamHeight = Mathf.Max(0.10f, topY - bottomY);
        float midY = (topY + bottomY) * 0.5f;

        // 1. Primary falling water stream jet straight out of faucet nozzle
        waterStreamGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        waterStreamGo.name = "Faucet_WaterStream";
        waterStreamGo.transform.SetParent(parentTransform, false);
        waterStreamGo.transform.localPosition = new Vector3(spoutTipBenchLocal.x, midY, spoutTipBenchLocal.z);
        waterStreamGo.transform.localScale = new Vector3(0.017f, streamHeight * 0.5f, 0.017f);
        Object.Destroy(waterStreamGo.GetComponent<Collider>());
        waterStreamGo.GetComponent<MeshRenderer>().sharedMaterial = waterStreamMat;
        waterStreamGo.SetActive(false);

        // 2. Cascading water droplets / streaks falling out of the nozzle
        for (int i = 0; i < 6; i++)
        {
            GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            drop.name = $"Faucet_WaterDrop_{i}";
            drop.transform.SetParent(parentTransform, false);
            drop.transform.localScale = new Vector3(0.008f, 0.022f, 0.008f);
            Object.Destroy(drop.GetComponent<Collider>());
            drop.GetComponent<MeshRenderer>().sharedMaterial = dropletMat;
            drop.SetActive(false);
            waterDroplets.Add(drop.transform);
            dropletPhases.Add(i / 6f);
        }

        // 3. Basin splash ring directly beneath faucet nozzle
        splashRippleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        splashRippleGo.name = "Sink_SplashRipple";
        splashRippleGo.transform.SetParent(parentTransform, false);
        splashRippleGo.transform.localPosition = new Vector3(spoutTipBenchLocal.x, bottomY + 0.002f, spoutTipBenchLocal.z);
        splashRippleGo.transform.localScale = new Vector3(0.095f, 0.0015f, 0.095f);
        Object.Destroy(splashRippleGo.GetComponent<Collider>());
        splashRippleGo.GetComponent<MeshRenderer>().sharedMaterial = dropletMat;
        splashRippleGo.SetActive(false);

        // 4. Shallow glistening basin puddle while faucet runs
        splashPoolGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        splashPoolGo.name = "Sink_SplashPool";
        splashPoolGo.transform.SetParent(parentTransform, false);
        splashPoolGo.transform.localPosition = new Vector3(basinCenterBenchLocal.x, bottomY + 0.001f, basinCenterBenchLocal.z);
        splashPoolGo.transform.localScale = new Vector3(0.28f, 0.001f, 0.24f);
        Object.Destroy(splashPoolGo.GetComponent<Collider>());
        splashPoolGo.GetComponent<MeshRenderer>().sharedMaterial = waterStreamMat;
        splashPoolGo.SetActive(false);
    }

    private void Update()
    {
        if (!isWaterRunning) return;

        // Scroll aerated water stream downward for crisp running water flow
        if (waterStreamMat != null)
        {
            Vector2 offset = waterStreamMat.mainTextureOffset;
            offset.y -= Time.deltaTime * 5.2f;
            waterStreamMat.mainTextureOffset = offset;
            if (waterStreamMat.HasProperty("_BaseMap"))
            {
                waterStreamMat.SetTextureOffset("_BaseMap", offset);
            }
        }

        if (waterStreamGo != null)
        {
            float pulse = 0.016f + Mathf.Sin(Time.time * 36f) * 0.002f;
            Vector3 sc = waterStreamGo.transform.localScale;
            sc.x = pulse;
            sc.z = pulse;
            waterStreamGo.transform.localScale = sc;
        }

        // Animate cascading falling water droplets down from nozzle tip
        float topY = spoutTipBenchLocal.y;
        float bottomY = basinCenterBenchLocal.y + 0.01f;
        for (int i = 0; i < waterDroplets.Count; i++)
        {
            Transform d = waterDroplets[i];
            if (d == null) continue;
            float phase = (dropletPhases[i] + Time.time * 3.4f) % 1.0f;
            float angle = i * 1.047f + Time.time * 4f;
            float radius = 0.006f + phase * 0.012f;
            d.localPosition = new Vector3(
                spoutTipBenchLocal.x + Mathf.Cos(angle) * radius,
                Mathf.Lerp(topY, bottomY, phase),
                spoutTipBenchLocal.z + Mathf.Sin(angle) * radius);
        }

        // Pulse splash ripple where stream strikes basin/plate
        if (splashRippleGo != null)
        {
            float rippleScale = 0.085f + ((Time.time * 1.8f) % 1.0f) * 0.085f;
            splashRippleGo.transform.localScale = new Vector3(rippleScale, 0.0015f, rippleScale);
        }
    }

    public void SetWaterRunning(bool running)
    {
        isWaterRunning = running;
        if (waterStreamGo != null) waterStreamGo.SetActive(running);
        if (splashRippleGo != null) splashRippleGo.SetActive(running);
        if (splashPoolGo != null) splashPoolGo.SetActive(running);
        for (int i = 0; i < waterDroplets.Count; i++)
        {
            if (waterDroplets[i] != null) waterDroplets[i].gameObject.SetActive(running);
        }
    }

    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsDialogueActive) return string.Empty;
        if (HouseSceneStoryController.IsAct3WashFaceTaskActive)
        {
            return "E to Wash Your Face";
        }
        if (HouseSceneStoryController.Instance == null || HouseSceneStoryController.Instance.currentStage < HouseSceneStoryController.HouseStoryStage.TaskWashPlate)
        {
            return string.Empty;
        }
        if (HouseSceneStoryController.HasWashedPlate) return string.Empty;
        if (isWashingActive)
        {
            int rem = Mathf.CeilToInt(Mathf.Max(0f, washDuration - washTimer));
            return rem > 0 ? $"Washing plate... ({rem}s)" : "Rinsing plate...";
        }

        string activeItem = HorrorHotbar.Instance != null ? HorrorHotbar.Instance.GetItemName(HorrorHotbar.Instance.activeSlotIndex) : "";
        bool holdingDirtyPlate = (CabinCookingSystem.HeldDirtyPlate != null && CabinCookingSystem.HeldDirtyPlate.IsPickedUp) ||
                                string.Equals(activeItem, "DIRTY PLATE", System.StringComparison.OrdinalIgnoreCase);

        if (holdingDirtyPlate)
        {
            return "E to Wash Plate";
        }

        return "Pick up dirty plate from dining table first";
    }

    public void Interact()
    {
        if (HouseSceneStoryController.IsDialogueActive || isWashingActive) return;
        if (HouseSceneStoryController.IsAct3WashFaceTaskActive)
        {
            if (HouseSceneStoryController.Instance != null)
            {
                HouseSceneStoryController.Instance.TriggerAct3FaceWash(transform, this);
            }
            return;
        }
        if (HouseSceneStoryController.HasWashedPlate) return;

        string activeItem = HorrorHotbar.Instance != null ? HorrorHotbar.Instance.GetItemName(HorrorHotbar.Instance.activeSlotIndex) : "";
        bool holdingDirtyPlate = (CabinCookingSystem.HeldDirtyPlate != null && CabinCookingSystem.HeldDirtyPlate.IsPickedUp) ||
                                string.Equals(activeItem, "DIRTY PLATE", System.StringComparison.OrdinalIgnoreCase);

        if (!holdingDirtyPlate) return;

        StartCoroutine(WashPlateRoutine());
    }

    private IEnumerator WashPlateRoutine()
    {
        isWashingActive = true;
        IsPlayerWashingPlate = true;
        washTimer = 0f;

        if (CabinCookingSystem.HeldDirtyPlate != null)
        {
            CabinCookingSystem.HeldDirtyPlate.IsSuppressingViewmodelFollow = true;
            CabinCookingSystem.HeldDirtyPlate.SetVisible(false);
        }

        // 1. Turn on faucet
        if (waterAudioSource != null && faucetTurnClip != null)
        {
            waterAudioSource.PlayOneShot(faucetTurnClip, 0.85f);
        }
        yield return new WaitForSeconds(0.28f);

        // 2. Running water pours directly from the real faucet spout nozzle
        SetWaterRunning(true);
        if (waterAudioSource != null && waterRunningClip != null)
        {
            waterAudioSource.clip = waterRunningClip;
            waterAudioSource.loop = true;
            waterAudioSource.volume = 0.55f;
            waterAudioSource.Play();
        }

        // 3. Hold dirty plate inside the real sink basin directly underneath the falling water stream
        Transform parentTransform = transform.parent != null ? transform.parent : transform;
        GameObject washingPlateGo = CabinCookingSystem.CreateDirtyPlateModel("Washing_Plate_InSink");
        washingPlateGo.transform.SetParent(parentTransform, false);
        float plateWashY = Mathf.Clamp(basinCenterBenchLocal.y + 0.048f, basinCenterBenchLocal.y + 0.025f, spoutTipBenchLocal.y - 0.06f);
        washingPlateGo.transform.localPosition = new Vector3(spoutTipBenchLocal.x, plateWashY, spoutTipBenchLocal.z);
        washingPlateGo.transform.localRotation = Quaternion.Euler(14f, 18f, -5f);
        washingPlateGo.transform.localScale = new Vector3(0.84f, 0.84f, 0.84f);

        // Move splash ripple onto the surface of the plate while scrubbing
        if (splashRippleGo != null)
        {
            splashRippleGo.transform.localPosition = new Vector3(spoutTipBenchLocal.x, plateWashY + 0.012f, spoutTipBenchLocal.z);
        }

        // Find grease spots on washing plate
        List<GameObject> greaseSpots = new List<GameObject>();
        foreach (Transform child in washingPlateGo.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.StartsWith("Grease_Spot", System.StringComparison.OrdinalIgnoreCase))
            {
                greaseSpots.Add(child.gameObject);
            }
        }

        // 4. Dual-layer Yellow/Green dish sponge + soap suds bubbles on the plate
        GameObject spongeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spongeGo.name = "Sink_DishSponge";
        spongeGo.transform.SetParent(washingPlateGo.transform, false);
        spongeGo.transform.localPosition = new Vector3(0f, 0.022f, 0f);
        spongeGo.transform.localScale = new Vector3(0.062f, 0.018f, 0.092f);
        Object.Destroy(spongeGo.GetComponent<Collider>());
        Material spongeMat = InteractableFoodItem.GetOrCreateMaterial("DishSponge_MAT", null, new Color(0.88f, 0.84f, 0.20f), 0.25f);
        spongeGo.GetComponent<MeshRenderer>().sharedMaterial = spongeMat;

        GameObject scrubPadGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        scrubPadGo.name = "Sink_DishSponge_Pad";
        scrubPadGo.transform.SetParent(spongeGo.transform, false);
        scrubPadGo.transform.localPosition = new Vector3(0f, 0.52f, 0f);
        scrubPadGo.transform.localScale = new Vector3(1.01f, 0.30f, 1.01f);
        Object.Destroy(scrubPadGo.GetComponent<Collider>());
        Material padMat = InteractableFoodItem.GetOrCreateMaterial("DishSpongePad_MAT", null, new Color(0.14f, 0.44f, 0.16f), 0.10f);
        scrubPadGo.GetComponent<MeshRenderer>().sharedMaterial = padMat;

        // Soap suds bubbles that froth up and rinse away cleanly
        List<Transform> soapBubbles = new List<Transform>();
        Material sudsMat = dropletMat ?? InteractableFoodItem.GetOrCreateMaterial("SoapSuds_MAT", null, new Color(0.95f, 0.98f, 1.0f), 0.70f);
        for (int b = 0; b < 6; b++)
        {
            GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = $"Soap_Bubble_{b}";
            bubble.transform.SetParent(washingPlateGo.transform, false);
            float ang = b * Mathf.PI / 3f;
            bubble.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.042f, 0.013f, Mathf.Sin(ang) * 0.042f);
            bubble.transform.localScale = Vector3.zero;
            Object.Destroy(bubble.GetComponent<Collider>());
            bubble.GetComponent<MeshRenderer>().sharedMaterial = sudsMat;
            soapBubbles.Add(bubble.transform);
        }

        // 5. Deliberate slower scrubbing & rinsing animation (10 seconds total)
        float totalWashTime = washDuration;
        float elapsed = 0f;
        float nextScrubSfxTime = 0.2f;

        while (elapsed < totalWashTime)
        {
            elapsed += Time.deltaTime;
            washTimer = elapsed;
            float progress = Mathf.Clamp01(elapsed / totalWashTime);
            bool isRinsingPhase = progress >= 0.80f;

            // Slower, methodical circular sponge scrub during scrub phase; lift sponge aside for clean rinse
            if (spongeGo != null)
            {
                if (!isRinsingPhase)
                {
                    float scrubAngle = elapsed * 4.2f;
                    float sx = Mathf.Cos(scrubAngle) * 0.032f;
                    float sz = Mathf.Sin(scrubAngle) * 0.032f;
                    float sy = 0.022f + Mathf.Sin(elapsed * 8.4f) * 0.002f;
                    spongeGo.transform.localPosition = new Vector3(sx, sy, sz);
                    spongeGo.transform.localRotation = Quaternion.Euler(0f, -scrubAngle * Mathf.Rad2Deg, 0f);
                }
                else
                {
                    float rinseT = Mathf.Clamp01((progress - 0.80f) / 0.20f);
                    spongeGo.transform.localPosition = Vector3.Lerp(
                        spongeGo.transform.localPosition,
                        new Vector3(0.085f, 0.040f, 0.06f),
                        Time.deltaTime * 4.0f);
                    spongeGo.transform.localScale = Vector3.Lerp(
                        spongeGo.transform.localScale,
                        new Vector3(0.062f, 0.018f, 0.092f) * (1f - rinseT * 0.15f),
                        Time.deltaTime * 4.0f);
                }
            }

            // Soap bubbles froth up slowly then rinse clean in the final rinse phase
            float sudsScale = progress < 0.72f
                ? Mathf.Clamp01(progress * 1.8f) * 0.022f
                : Mathf.Clamp01((1.0f - progress) / 0.28f) * 0.022f;
            for (int b = 0; b < soapBubbles.Count; b++)
            {
                if (soapBubbles[b] != null)
                {
                    float wob = 1.0f + Mathf.Sin(elapsed * 5.2f + b) * 0.18f;
                    soapBubbles[b].localScale = Vector3.one * (sudsScale * wob);
                }
            }

            // Measured periodic scrubbing sound during scrub phase
            if (!isRinsingPhase && spongeAudioSource != null && spongeScrubClip != null &&
                !spongeAudioSource.isPlaying && elapsed >= nextScrubSfxTime)
            {
                spongeAudioSource.pitch = Random.Range(0.90f, 1.06f);
                spongeAudioSource.PlayOneShot(spongeScrubClip, 0.42f);
                nextScrubSfxTime = elapsed + 0.55f;
            }

            // Gradually shrink and clean off grease spots over the first 78% of wash time
            float greaseAlpha = Mathf.Clamp01(1.0f - progress * 1.28f);
            for (int i = 0; i < greaseSpots.Count; i++)
            {
                if (greaseSpots[i] != null)
                {
                    greaseSpots[i].transform.localScale = Vector3.Lerp(greaseSpots[i].transform.localScale, Vector3.zero, Time.deltaTime * 1.4f);
                    if (greaseAlpha <= 0.05f) greaseSpots[i].SetActive(false);
                }
            }

            yield return null;
        }

        if (spongeGo != null) Object.Destroy(spongeGo);

        // 6. Clean porcelain squeak and shut off faucet
        if (spongeAudioSource != null && cleanClinkClip != null)
        {
            spongeAudioSource.PlayOneShot(cleanClinkClip, 0.85f);
        }

        yield return new WaitForSeconds(0.55f);

        if (waterAudioSource != null && faucetTurnClip != null)
        {
            waterAudioSource.PlayOneShot(faucetTurnClip, 0.75f);
        }
        if (waterAudioSource != null && waterAudioSource.isPlaying)
        {
            waterAudioSource.Stop();
        }
        SetWaterRunning(false);
        if (splashRippleGo != null)
        {
            splashRippleGo.transform.localPosition = new Vector3(spoutTipBenchLocal.x, basinCenterBenchLocal.y + 0.005f, spoutTipBenchLocal.z);
        }

        // 7. Remove temporary in-sink washing mesh and keep the clean washed plate in the player's hotbar & hand
        if (washingPlateGo != null)
        {
            Object.Destroy(washingPlateGo);
        }

        CabinCookingSystem.ConvertDirtyPlateToWashedCleanPlate();

        isWashingActive = false;
        IsPlayerWashingPlate = false;
        HouseSceneStoryController.NotifyPlateWashed();
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GenerateConvenienceStore
{
    private const string MaterialsFolder = "Assets/small-red-gas-station/Materials";

    [MenuItem("Tools/Gas Station/Generate Convenience Store (Sliding Doors + Male & Female Rooms)")]
    public static void Execute()
    {
        GameObject stacja = GameObject.Find("stacja");
        if (stacja == null)
        {
            Debug.LogWarning("[GenerateConvenienceStore] 'stacja' GameObject not found in the active scene.");
            return;
        }

        Debug.Log("[GenerateConvenienceStore] Generating modular Convenience Store fitted onto Gas Station...");

        // 1. Hide old building shell (Plane.001) and grocery aisle shelves
        //    (Keep cashier area active and exterior gas station objects intact!)
        string[] interiorPropsToHide = new string[]
        {
            "Plane.001",
            "Bodyframe.001", "Bodyframe.004",
            "Cube.006", "Cube.038",
            "Shelf1", "Shelf2", "Shelf3", "Shelf4",
            "Cube.014", "Cube.015", "Cube.016", "Cube.017", "Cube.018", "Cube.019", "Cube.020",
            "Point", "Point.003"
        };

        // Cashier counter, POS registers, display racks to show
        string[] cashierPropsToShow = new string[]
        {
            "Cashier", "Cashier2", "Cube.008", "Cube.049", "Cube.009", "Cube.021",
            "tuna can large.005", "Cube.010", "Cube.013", "Cube.029", "Cube.027", "Cube.028", "Cube.012"
        };

        HashSet<string> hideSet = new HashSet<string>(interiorPropsToHide, StringComparer.OrdinalIgnoreCase);
        HashSet<string> cashierSet = new HashSet<string>(cashierPropsToShow, StringComparer.OrdinalIgnoreCase);

        foreach (Transform child in stacja.GetComponentsInChildren<Transform>(true))
        {
            if (cashierSet.Contains(child.name))
            {
                child.gameObject.SetActive(true);
                MeshFilter mf = child.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider mc = child.GetComponent<MeshCollider>();
                    if (mc == null) mc = child.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
                EditorUtility.SetDirty(child.gameObject);
            }
            else if (hideSet.Contains(child.name))
            {
                child.gameObject.SetActive(false);
                EditorUtility.SetDirty(child.gameObject);
            }
            else if (child.name == "Plane" || child.name == "Plane.002" || child.name == "Plane.003" || child.name == "Plane.004")
            {
                // Ensure asphalt ground and concrete foundation slab are active and have MeshColliders
                child.gameObject.SetActive(true);
                MeshFilter mf = child.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider mc = child.GetComponent<MeshCollider>();
                    if (mc == null) mc = child.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
        }

        // 2. Remove any previous generated store or standalone doors
        string[] oldNames = { "Store_Entrance_Doors", "Convenience_Store", "Convenience_Store_V2" };
        foreach (string oldName in oldNames)
        {
            Transform oldObj = stacja.transform.Find(oldName);
            if (oldObj != null) UnityEngine.Object.DestroyImmediate(oldObj.gameObject);
        }

        // 3. Create Convenience Store Root on stacja aligned to Plane.002 foundation slab:
        //    In stacja local space (meters):
        //      Plane.002 slab spans X = [3.14, 12.84], Y = 0.1388 (top surface), Z = [-8.93, +9.77]
        //      Plane.001 floor origin is at stacja local (8.992011f, 0.138806f, 8.110930f)
        //      With localRotation = Euler(0, -90, 0), StorePos(along, height, depth) maps to:
        //        stacja_X = 8.992011 - depth  (Front wall depth=4.46 -> X=4.532m; Back wall depth=-2.55 -> X=11.542m)
        //        stacja_Y = 0.138806 + height (Floor height=0 -> Y=0.1388m right on Plane.002)
        //        stacja_Z = 8.110930 - along  (Left wall along=-0.60 -> Z=+8.711m; Right wall along=15.58 -> Z=-7.469m)
        GameObject storeRoot = new GameObject("Convenience_Store_V2");
        storeRoot.transform.SetParent(stacja.transform, false);
        storeRoot.transform.localPosition = new Vector3(8.992011f, 0.138806f, 8.110930f);
        storeRoot.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        storeRoot.transform.localScale = Vector3.one;

        // 4. Load Materials
        Material floorMat = LoadMat("floor");
        Material whiteMat = LoadMat("Material");
        Material grayWallMat = LoadMat("gray walls");
        Material redWallMat = LoadMat("walls");
        Material roofMat = LoadMat("obudowa");
        Material darkFrameMat = LoadMat("czarny mat");
        Material glassMat = LoadMat("Material.032");
        Material chromeMat = LoadMat("chrome");
        Material toiletDoorMat = LoadMat("toilet doors");
        Material glowMat = LoadMat("glowin");
        Material acMat = LoadMat("ac");
        Material ac2Mat = LoadMat("ac2");
        Material maleSignMat = GetOrCreateColorMaterial("MaleSign", new Color(0.10f, 0.45f, 0.88f));
        Material femaleSignMat = GetOrCreateColorMaterial("FemaleSign", new Color(0.88f, 0.18f, 0.38f));

        if (floorMat != null)
        {
            floorMat.mainTextureScale = new Vector2(8f, 4f);
            if (floorMat.HasProperty("_BaseMap"))
            {
                floorMat.SetTextureScale("_BaseMap", new Vector2(8f, 4f));
            }
        }

        // =========================================================================
        // A. FLOOR
        // =========================================================================
        GameObject floorGroup = CreateGroup("Floor", storeRoot.transform);
        CreateStoreBox("Store_Tiled_Floor", floorGroup.transform,
            alongMin: -0.50f, alongMax: 15.48f,
            yMin: -0.01f, yMax: 0.03f,
            depthMin: -2.50f, depthMax: 4.46f, floorMat);

        // =========================================================================
        // B. EXTERIOR STRUCTURAL PILLARS & PERIMETER WALLS
        // =========================================================================
        GameObject exteriorGroup = CreateGroup("Exterior_Structure", storeRoot.transform);

        // 4 Front White Pillars (matching the gas station aesthetic & aligned with exterior bins)
        CreateStoreBox("Pillar_Front_FarLeft", exteriorGroup.transform,
            -0.60f, 0.30f, 0f, 3.00f, 4.26f, 4.72f, whiteMat);
        CreateStoreBox("Pillar_Front_MidLeft", exteriorGroup.transform,
            4.44f, 5.37f, 0f, 3.00f, 4.26f, 4.72f, whiteMat);
        CreateStoreBox("Pillar_Front_Center", exteriorGroup.transform,
            9.48f, 10.36f, 0f, 3.00f, 4.26f, 4.72f, whiteMat);
        CreateStoreBox("Pillar_Front_FarRight", exteriorGroup.transform,
            14.48f, 15.58f, 0f, 3.00f, 4.26f, 4.72f, whiteMat);

        // 2 Back Corner White Pillars
        CreateStoreBox("Pillar_Back_Left", exteriorGroup.transform,
            -0.60f, 0.15f, 0f, 3.00f, -2.68f, -2.28f, whiteMat);
        CreateStoreBox("Pillar_Back_Right", exteriorGroup.transform,
            14.83f, 15.58f, 0f, 3.00f, -2.68f, -2.28f, whiteMat);

        // Solid Perimeter Walls (Back, Left, Right)
        CreateStoreBox("Wall_Back", exteriorGroup.transform,
            -0.50f, 15.48f, 0f, 3.00f, -2.55f, -2.35f, whiteMat);
        CreateStoreBox("Wall_Left_Side", exteriorGroup.transform,
            -0.55f, -0.35f, 0f, 3.00f, -2.55f, 4.46f, whiteMat);
        CreateStoreBox("Wall_Right_Side", exteriorGroup.transform,
            15.38f, 15.55f, 0f, 3.00f, -2.55f, 4.46f, whiteMat);

        // Front-Left Solid Wall Bay (along = [0.30, 4.44], right behind the green/blue/brown recycling bins & jug rack)
        CreateStoreBox("Wall_FrontLeft_Lower", exteriorGroup.transform,
            0.30f, 4.44f, 0f, 2.26f, 4.32f, 4.52f, whiteMat);
        CreateStoreBox("Wall_FrontLeft_RedBanner", exteriorGroup.transform,
            0.30f, 4.44f, 2.26f, 3.00f, 4.30f, 4.55f, redWallMat);

        // =========================================================================
        // C. STOREFRONT WINDOWS & AUTOMATIC SLIDING ENTRANCE DOORS
        // =========================================================================
        GameObject storefrontGroup = CreateGroup("Storefront_Facade", storeRoot.transform);

        // Red Fascia Headers above Entrance Bay and Right Window Bay
        CreateStoreBox("Header_EntranceBay_Red", storefrontGroup.transform,
            5.37f, 9.48f, 2.26f, 3.00f, 4.32f, 4.55f, redWallMat);
        CreateStoreBox("Header_RightBay_Red", storefrontGroup.transform,
            10.36f, 14.48f, 2.26f, 3.00f, 4.32f, 4.55f, redWallMat);

        // Left Fixed Window next to Entrance Doors: along = [5.37, 6.75]
        CreateStoreBox("EntranceSide_Window_Sill", storefrontGroup.transform,
            5.37f, 6.75f, 0f, 0.30f, 4.38f, 4.52f, grayWallMat);
        CreateFramedStoreWindow("EntranceSide_Fixed_Window", storefrontGroup.transform,
            5.37f, 6.75f, 0.30f, 2.26f, 4.46f, darkFrameMat, glassMat);

        // Right Storefront Window Bay: along = [10.36, 14.48] (3 large glass panels + low sill)
        CreateStoreBox("RightBay_Window_Sill", storefrontGroup.transform,
            10.36f, 14.48f, 0f, 0.30f, 4.38f, 4.52f, whiteMat);
        float rBayStart = 10.36f;
        float rBayEnd = 14.48f;
        float panelW = (rBayEnd - rBayStart) / 3f;
        for (int i = 0; i < 3; i++)
        {
            CreateFramedStoreWindow($"RightBay_Window_{i + 1}", storefrontGroup.transform,
                rBayStart + i * panelW, rBayStart + (i + 1) * panelW, 0.30f, 2.26f, 4.46f, darkFrameMat, glassMat);
        }

        // AUTOMATIC SLIDING DOORS in Entrance Opening: along = [6.75, 9.48], Y = [0, 2.26], depth = 4.46
        // Doorway center: along = 8.115 (local X = -8.115), Y = 1.115, depth = 4.46
        CreateAutomaticSlidingEntrance(storeRoot.transform, StorePos(8.115f, 1.115f, 4.46f), darkFrameMat, glassMat, chromeMat);

        // =========================================================================
        // D. MALE & FEMALE RESTROOMS (Inside Left Section: along = [-0.35, 4.44], depth = [-2.35, 4.32])
        // =========================================================================
        GameObject restroomsGroup = CreateGroup("Restrooms_Male_And_Female", storeRoot.transform);

        // Central dividing wall between Male Restroom (Front: depth=[1.05, 4.32]) and Female Restroom (Back: depth=[-2.35, 0.90])
        CreateStoreBox("Wall_Divider_Male_Female", restroomsGroup.transform,
            -0.35f, 4.44f, 0f, 3.00f, 0.90f, 1.06f, whiteMat);

        // Interior Partition Wall at along = [4.36, 4.52] separating Main Store from Male & Female Restrooms:
        //   - Female Restroom Doorway Opening: depth = [-1.15, -0.15], Y = [0, 2.15]
        //   - Male Restroom Doorway Opening:   depth = [2.15, 3.15],   Y = [0, 2.15]
        CreateStoreBox("Partition_Wall_BackSegment", restroomsGroup.transform,
            4.36f, 4.52f, 0f, 3.00f, -2.35f, -1.15f, grayWallMat);
        CreateStoreBox("Partition_Wall_FemaleDoorHeader", restroomsGroup.transform,
            4.36f, 4.52f, 2.15f, 3.00f, -1.15f, -0.15f, grayWallMat);
        CreateStoreBox("Partition_Wall_MiddleSegment", restroomsGroup.transform,
            4.36f, 4.52f, 0f, 3.00f, -0.15f, 2.15f, grayWallMat);
        CreateStoreBox("Partition_Wall_MaleDoorHeader", restroomsGroup.transform,
            4.36f, 4.52f, 2.15f, 3.00f, 2.15f, 3.15f, grayWallMat);
        CreateStoreBox("Partition_Wall_FrontSegment", restroomsGroup.transform,
            4.36f, 4.52f, 0f, 3.00f, 3.15f, 4.32f, grayWallMat);

        // Build Male Restroom Door, Frame & Signage (depth = [2.15, 3.15])
        BuildRestroomEntrance("Male_Restroom", restroomsGroup.transform,
            doorDepthMin: 2.15f, doorDepthMax: 3.15f, wallAlong: 4.44f,
            isMale: true, darkFrameMat, toiletDoorMat, chromeMat, maleSignMat, whiteMat);

        // Build Female Restroom Door, Frame & Signage (depth = [-1.15, -0.15])
        BuildRestroomEntrance("Female_Restroom", restroomsGroup.transform,
            doorDepthMin: -1.15f, doorDepthMax: -0.15f, wallAlong: 4.44f,
            isMale: false, darkFrameMat, toiletDoorMat, chromeMat, femaleSignMat, whiteMat);

        // =========================================================================
        // E. CASHIER ATTENDANT & CHECKOUT LIGHTING
        // =========================================================================
        GameObject cashierStaffGroup = CreateGroup("Cashier_Staff", storeRoot.transform);
        Material skinMat = GetOrCreateColorMaterial("ClerkSkin", new Color(0.92f, 0.76f, 0.65f));
        Material clerkHairMat = GetOrCreateColorMaterial("ClerkHair", new Color(0.18f, 0.12f, 0.08f));
        CreateCashierClerk(cashierStaffGroup.transform, StorePos(13.95f, 0f, 0.85f), Quaternion.Euler(0f, 90f, 0f),
            redWallMat, darkFrameMat, whiteMat, skinMat, clerkHairMat, chromeMat);

        // Dedicated warm downlight over the Cashier Checkout Counter
        CreateCashierSpotlight("Light_Cashier_Counter", cashierStaffGroup.transform, StorePos(13.50f, 2.98f, 0.85f), glowMat);

        // =========================================================================
        // F. ROOF, CEILING, AC UNIT & LIGHTING
        // =========================================================================
        GameObject roofGroup = CreateGroup("Roof_And_Lighting", storeRoot.transform);

        // Solid Interior Ceiling (Y = [3.00, 3.12])
        CreateStoreBox("Interior_Ceiling", roofGroup.transform,
            -0.55f, 15.55f, 3.00f, 3.12f, -2.55f, 4.55f, whiteMat);

        // Overhanging Exterior Roof Cornice (Y = [3.12, 3.74], along = [-0.85, 15.85], depth = [-2.95, 4.96])
        CreateStoreBox("Roof_Overhang_Main", roofGroup.transform,
            -0.85f, 15.85f, 3.12f, 3.74f, -2.95f, 4.96f, roofMat);

        // Red Perimeter Fascia Accent Band on Roof Overhang (matches Gas Station Canopy)
        CreateStoreBox("Roof_RedStripe_Front", roofGroup.transform,
            -0.87f, 15.87f, 3.25f, 3.58f, 4.94f, 4.99f, redWallMat);
        CreateStoreBox("Roof_RedStripe_Back", roofGroup.transform,
            -0.87f, 15.87f, 3.25f, 3.58f, -2.99f, -2.94f, redWallMat);
        CreateStoreBox("Roof_RedStripe_Left", roofGroup.transform,
            -0.89f, -0.84f, 3.25f, 3.58f, -2.98f, 4.98f, redWallMat);
        CreateStoreBox("Roof_RedStripe_Right", roofGroup.transform,
            15.84f, 15.89f, 3.25f, 3.58f, -2.98f, 4.98f, redWallMat);

        // Rooftop HVAC / Air Conditioner Unit
        CreateStoreBox("Rooftop_AC_Unit", roofGroup.transform,
            1.40f, 3.20f, 3.74f, 4.48f, 0.80f, 2.20f, acMat != null ? acMat : roofMat);
        if (ac2Mat != null)
        {
            CreateStoreBox("Rooftop_AC_FanGrille", roofGroup.transform,
                1.60f, 3.00f, 3.84f, 4.38f, 2.18f, 2.23f, ac2Mat);
        }

        // Ceiling Fluorescent Light Fixtures + Point Lights
        // 4 in Main Store Sales Area + 1 in Male Restroom + 1 in Female Restroom
        CreateCeilingLight("Light_MainStore_FrontLeft", roofGroup.transform, StorePos(7.40f, 2.98f, 2.40f), new Vector3(2.20f, 0.04f, 0.35f), glowMat, 6.0f, 12f);
        CreateCeilingLight("Light_MainStore_BackLeft", roofGroup.transform, StorePos(7.40f, 2.98f, -0.60f), new Vector3(2.20f, 0.04f, 0.35f), glowMat, 6.0f, 12f);
        CreateCeilingLight("Light_MainStore_FrontRight", roofGroup.transform, StorePos(12.40f, 2.98f, 2.40f), new Vector3(2.20f, 0.04f, 0.35f), glowMat, 6.0f, 12f);
        CreateCeilingLight("Light_MainStore_BackRight", roofGroup.transform, StorePos(12.40f, 2.98f, -0.60f), new Vector3(2.20f, 0.04f, 0.35f), glowMat, 6.0f, 12f);
        CreateCeilingLight("Light_Male_Restroom", roofGroup.transform, StorePos(2.00f, 2.98f, 2.65f), new Vector3(1.40f, 0.04f, 0.30f), glowMat, 4.5f, 8f);
        CreateCeilingLight("Light_Female_Restroom", roofGroup.transform, StorePos(2.00f, 2.98f, -0.70f), new Vector3(1.40f, 0.04f, 0.30f), glowMat, 4.5f, 8f);

        // =========================================================================
        // F. POSITION TEST FPS PLAYER IN FRONT OF THE SLIDING DOORS
        // =========================================================================
        PositionPlayerAtEntrance(storeRoot.transform);

        EditorUtility.SetDirty(storeRoot);
        EditorUtility.SetDirty(stacja);
        Scene activeScene = SceneManager.GetActiveScene();
        if (!EditorApplication.isPlaying && !Application.isPlaying)
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("[GenerateConvenienceStore] Successfully generated Convenience Store with Automatic Sliding Doors, Male & Female Restrooms, and clean empty interior!");
    }

    private static Vector3 StorePos(float alongStore, float height, float depthTowardPumps)
    {
        return new Vector3(-alongStore, height, depthTowardPumps);
    }

    private static GameObject CreateStoreBox(
        string name, Transform parent,
        float alongMin, float alongMax,
        float yMin, float yMax,
        float depthMin, float depthMax,
        Material mat, bool addCollider = true)
    {
        float cx = -0.5f * (alongMin + alongMax);
        float cy = 0.5f * (yMin + yMax);
        float cz = 0.5f * (depthMin + depthMax);
        Vector3 size = new Vector3(
            Mathf.Abs(alongMax - alongMin),
            Mathf.Abs(yMax - yMin),
            Mathf.Abs(depthMax - depthMin)
        );
        return CreateBox(name, parent, new Vector3(cx, cy, cz), size, mat, addCollider);
    }

    private static void CreateAutomaticSlidingEntrance(Transform storeRoot, Vector3 doorCenterLocal, Material frameMat, Material glassMat, Material handleMat)
    {
        GameObject doorsRoot = new GameObject("Store_Entrance_Doors");
        doorsRoot.transform.SetParent(storeRoot, false);
        doorsRoot.transform.localPosition = doorCenterLocal;
        doorsRoot.transform.localRotation = Quaternion.identity;
        doorsRoot.transform.localScale = Vector3.one;

        // Fixed Side Jambs
        CreateBox("Jamb_Left", doorsRoot.transform, new Vector3(-1.34f, 0f, 0f), new Vector3(0.06f, 2.26f, 0.12f), frameMat);
        CreateBox("Jamb_Right", doorsRoot.transform, new Vector3(1.34f, 0f, 0f), new Vector3(0.06f, 2.26f, 0.12f), frameMat);

        // Overhead Header Track Beam
        GameObject header = CreateBox("Door_Header_Track", doorsRoot.transform, new Vector3(0f, 1.10f, 0f), new Vector3(2.74f, 0.10f, 0.14f), frameMat);

        // Overhead Motion Sensor (Exterior & Interior)
        CreateBox("Motion_Sensor_Ext", header.transform, new Vector3(0f, 0.15f, 0.60f), new Vector3(0.10f, 0.55f, 0.45f), handleMat);
        CreateBox("Motion_Sensor_Int", header.transform, new Vector3(0f, 0.15f, -0.60f), new Vector3(0.10f, 0.55f, 0.45f), handleMat);

        // Left & Right Sliding Door Leaves
        GameObject leftDoor = CreateSlidingDoorLeaf("Left_Sliding_Door", doorsRoot.transform, new Vector3(-0.66f, -0.03f, 0f), frameMat, glassMat, handleMat, isLeft: true);
        GameObject rightDoor = CreateSlidingDoorLeaf("Right_Sliding_Door", doorsRoot.transform, new Vector3(0.66f, -0.03f, 0f), frameMat, glassMat, handleMat, isLeft: false);

        // Attach AutomaticSlidingDoors script
        AutomaticSlidingDoors controller = doorsRoot.AddComponent<AutomaticSlidingDoors>();
        controller.leftDoor = leftDoor.transform;
        controller.rightDoor = rightDoor.transform;
        controller.openDistance = 1.18f;
        controller.speed = 3.5f;
        controller.isOpen = true; // Open in Editor so user sees the open sliding doorway immediately, and auto-triggers in Play Mode!
        controller.autoTrigger = true;

        // Trigger Zone for Automatic Opening
        BoxCollider trigger = doorsRoot.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = Vector3.zero;
        trigger.size = new Vector3(3.40f, 2.40f, 4.80f);
    }

    private static GameObject CreateSlidingDoorLeaf(string name, Transform parent, Vector3 localPos, Material frameMat, Material glassMat, Material handleMat, bool isLeft)
    {
        GameObject panelRoot = new GameObject(name);
        panelRoot.transform.SetParent(parent, false);
        panelRoot.transform.localPosition = localPos;
        panelRoot.transform.localRotation = Quaternion.identity;
        panelRoot.transform.localScale = Vector3.one;

        float width = 1.30f;
        float height = 2.16f;
        float thickness = 0.045f;
        float frameThick = 0.065f;

        CreateBox("Frame_Top", panelRoot.transform, new Vector3(0f, (height - frameThick) * 0.5f, 0f), new Vector3(width, frameThick, thickness), frameMat);
        CreateBox("Frame_Bottom", panelRoot.transform, new Vector3(0f, -(height - frameThick) * 0.5f, 0f), new Vector3(width, frameThick * 1.4f, thickness), frameMat);
        CreateBox("Frame_Left", panelRoot.transform, new Vector3(-(width - frameThick) * 0.5f, 0f, 0f), new Vector3(frameThick, height, thickness), frameMat);
        CreateBox("Frame_Right", panelRoot.transform, new Vector3((width - frameThick) * 0.5f, 0f, 0f), new Vector3(frameThick, height, thickness), frameMat);

        CreateBox("Glass_Pane", panelRoot.transform, Vector3.zero, new Vector3(width - frameThick * 2f, height - frameThick * 2f, 0.015f), glassMat);

        float handleX = isLeft ? (width * 0.5f - 0.14f) : -(width * 0.5f - 0.14f);
        CreateBox("Handle_Exterior", panelRoot.transform, new Vector3(handleX, 0f, 0.045f), new Vector3(0.03f, 0.85f, 0.03f), handleMat);
        CreateBox("Handle_Interior", panelRoot.transform, new Vector3(handleX, 0f, -0.045f), new Vector3(0.03f, 0.85f, 0.03f), handleMat);

        return panelRoot;
    }

    private static void BuildRestroomEntrance(
        string roomName, Transform parent,
        float doorDepthMin, float doorDepthMax, float wallAlong,
        bool isMale, Material frameMat, Material doorMat, Material chromeMat, Material signMat, Material whiteMat)
    {
        GameObject root = CreateGroup(roomName, parent);
        float doorWidth = doorDepthMax - doorDepthMin; // 1.00m
        float doorHeight = 2.15f;
        float doorCenterDepth = (doorDepthMin + doorDepthMax) * 0.5f;

        // Doorway Frame (Jambs & Header)
        CreateBox("DoorFrame_South", root.transform, StorePos(wallAlong, doorHeight * 0.5f, doorDepthMin + 0.025f), new Vector3(0.18f, doorHeight, 0.05f), frameMat);
        CreateBox("DoorFrame_North", root.transform, StorePos(wallAlong, doorHeight * 0.5f, doorDepthMax - 0.025f), new Vector3(0.18f, doorHeight, 0.05f), frameMat);
        CreateBox("DoorFrame_Top", root.transform, StorePos(wallAlong, doorHeight - 0.025f, doorCenterDepth), new Vector3(0.18f, 0.05f, doorWidth), frameMat);

        // Hinged Door Pivot
        GameObject doorPivot = new GameObject($"{roomName}_DoorPivot");
        doorPivot.transform.SetParent(root.transform, false);
        doorPivot.transform.localPosition = StorePos(wallAlong - 0.05f, 0f, doorDepthMin + 0.05f);
        doorPivot.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        InteractableRestroomDoor doorInteract = doorPivot.AddComponent<InteractableRestroomDoor>();
        doorInteract.doorLabel = isMale ? "Male Restroom Door" : "Female Restroom Door";
        doorInteract.isOpen = false;
        doorInteract.closedAngleY = 0f;
        doorInteract.openAngleY = 75f;

        float leafWidth = doorWidth - 0.10f; // 0.90m
        float leafHeight = doorHeight - 0.05f; // 2.10m
        GameObject doorLeaf = CreateBox($"{roomName}_Door", doorPivot.transform,
            new Vector3(0f, leafHeight * 0.5f, leafWidth * 0.5f),
            new Vector3(0.045f, leafHeight, leafWidth), doorMat);

        // Door Handles
        CreateBox("Handle_Out", doorLeaf.transform, new Vector3(-0.65f, 0f, 0.38f), new Vector3(0.6f, 0.07f, 0.04f), chromeMat);
        CreateBox("Handle_In", doorLeaf.transform, new Vector3(0.65f, 0f, 0.38f), new Vector3(0.6f, 0.07f, 0.04f), chromeMat);

        // Wall Sign Plaque & 3D Restroom Figure Emblem (Mounted on store side of partition wall next to doorway)
        float signDepth = doorDepthMax + 0.28f;
        CreateRestroomSign($"{roomName}_WallSign", root.transform, StorePos(wallAlong + 0.09f, 1.62f, signDepth), isMale, signMat, whiteMat);

        // Overhead Sign Plaque above Doorway
        CreateBox($"{roomName}_HeaderSignBoard", root.transform, StorePos(wallAlong + 0.09f, 2.38f, doorCenterDepth), new Vector3(0.03f, 0.24f, 0.70f), signMat);
    }

    private static void CreateRestroomSign(string name, Transform parent, Vector3 pos, bool isMale, Material signMat, Material figureMat)
    {
        GameObject signRoot = new GameObject(name);
        signRoot.transform.SetParent(parent, false);
        signRoot.transform.localPosition = pos;
        signRoot.transform.localRotation = Quaternion.identity;

        // Coloured Backing Plaque (Faces -X in storeRoot, which is toward the Main Store interior!)
        CreateBox("Sign_Plaque", signRoot.transform, Vector3.zero, new Vector3(0.02f, 0.36f, 0.24f), signMat, addCollider: false);

        // 3D Pictogram Figure on the store-facing side (-0.015f in local X)
        CreateBox("Icon_Head", signRoot.transform, new Vector3(-0.015f, 0.10f, 0f), new Vector3(0.015f, 0.055f, 0.055f), figureMat, addCollider: false);

        if (isMale)
        {
            CreateBox("Icon_Torso_Male", signRoot.transform, new Vector3(-0.015f, 0.015f, 0f), new Vector3(0.015f, 0.09f, 0.085f), figureMat, addCollider: false);
            CreateBox("Icon_Leg_L", signRoot.transform, new Vector3(-0.015f, -0.075f, -0.02f), new Vector3(0.015f, 0.08f, 0.028f), figureMat, addCollider: false);
            CreateBox("Icon_Leg_R", signRoot.transform, new Vector3(-0.015f, -0.075f, 0.02f), new Vector3(0.015f, 0.08f, 0.028f), figureMat, addCollider: false);
        }
        else
        {
            CreateBox("Icon_Torso_Upper", signRoot.transform, new Vector3(-0.015f, 0.035f, 0f), new Vector3(0.015f, 0.045f, 0.065f), figureMat, addCollider: false);
            CreateBox("Icon_Skirt_Mid", signRoot.transform, new Vector3(-0.015f, 0.00f, 0f), new Vector3(0.015f, 0.04f, 0.095f), figureMat, addCollider: false);
            CreateBox("Icon_Skirt_Low", signRoot.transform, new Vector3(-0.015f, -0.03f, 0f), new Vector3(0.015f, 0.035f, 0.125f), figureMat, addCollider: false);
            CreateBox("Icon_Leg_L", signRoot.transform, new Vector3(-0.015f, -0.085f, -0.02f), new Vector3(0.015f, 0.065f, 0.024f), figureMat, addCollider: false);
            CreateBox("Icon_Leg_R", signRoot.transform, new Vector3(-0.015f, -0.085f, 0.02f), new Vector3(0.015f, 0.065f, 0.024f), figureMat, addCollider: false);
        }
    }

    private static void CreateFramedStoreWindow(string name, Transform parent, float alongMin, float alongMax, float yMin, float yMax, float depthPos, Material frameMat, Material glassMat)
    {
        GameObject winRoot = CreateGroup(name, parent);
        float frameT = 0.055f;
        float halfD = 0.03f;

        CreateStoreBox("Mullion_Top", winRoot.transform, alongMin, alongMax, yMax - frameT, yMax, depthPos - halfD, depthPos + halfD, frameMat);
        CreateStoreBox("Mullion_Bottom", winRoot.transform, alongMin, alongMax, yMin, yMin + frameT, depthPos - halfD, depthPos + halfD, frameMat);
        CreateStoreBox("Mullion_Left", winRoot.transform, alongMin, alongMin + frameT, yMin, yMax, depthPos - halfD, depthPos + halfD, frameMat);
        CreateStoreBox("Mullion_Right", winRoot.transform, alongMax - frameT, alongMax, yMin, yMax, depthPos - halfD, depthPos + halfD, frameMat);
        CreateStoreBox("Window_Glass", winRoot.transform, alongMin + frameT, alongMax - frameT, yMin + frameT, yMax - frameT, depthPos - 0.008f, depthPos + 0.008f, glassMat);
    }

    private static void CreateCeilingLight(string name, Transform parent, Vector3 localPos, Vector3 panelScale, Material glowMat, float intensity, float range)
    {
        GameObject fixture = CreateBox(name, parent, localPos, panelScale, glowMat, addCollider: false);
        GameObject lightObj = new GameObject($"{name}_PointLight");
        lightObj.transform.SetParent(fixture.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        Light pt = lightObj.AddComponent<Light>();
        pt.type = LightType.Point;
        pt.color = new Color(1.0f, 0.98f, 0.92f);
        pt.intensity = intensity;
        pt.range = range;
        pt.shadows = LightShadows.Soft;
        pt.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
        pt.shadowBias = 0.05f;
        pt.shadowNormalBias = 0.4f;
    }

    private static void CreateCashierSpotlight(string name, Transform parent, Vector3 localPos, Material glowMat)
    {
        GameObject fixture = CreateBox(name, parent, localPos, new Vector3(0.30f, 0.06f, 0.30f), glowMat, addCollider: false);
        GameObject lightObj = new GameObject($"{name}_SpotLight");
        lightObj.transform.SetParent(fixture.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        lightObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Light spot = lightObj.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(1.0f, 0.95f, 0.85f);
        spot.intensity = 8.0f;
        spot.range = 6.0f;
        spot.spotAngle = 65f;
        spot.shadows = LightShadows.Soft;
        spot.shadowBias = 0.05f;
        spot.shadowNormalBias = 0.4f;
    }

    private static void CreateCashierClerk(
        Transform parent, Vector3 localPos, Quaternion localRot,
        Material shirtMat, Material pantsMat, Material whiteMat, Material skinMat, Material hairMat, Material chromeMat)
    {
        GameObject clerkRoot = new GameObject("Cashier_Clerk");
        clerkRoot.transform.SetParent(parent, false);
        clerkRoot.transform.localPosition = localPos;
        clerkRoot.transform.localRotation = localRot;
        clerkRoot.transform.localScale = Vector3.one;

        // Shoes
        CreateBox("Shoe_L", clerkRoot.transform, new Vector3(-0.11f, 0.04f, 0.02f), new Vector3(0.12f, 0.08f, 0.24f), pantsMat);
        CreateBox("Shoe_R", clerkRoot.transform, new Vector3(0.11f, 0.04f, 0.02f), new Vector3(0.12f, 0.08f, 0.24f), pantsMat);

        // Legs / Dark Trousers
        CreateBox("Leg_L", clerkRoot.transform, new Vector3(-0.11f, 0.44f, 0f), new Vector3(0.13f, 0.74f, 0.15f), pantsMat);
        CreateBox("Leg_R", clerkRoot.transform, new Vector3(0.11f, 0.44f, 0f), new Vector3(0.13f, 0.74f, 0.15f), pantsMat);

        // Pelvis / Belt
        CreateBox("Pelvis", clerkRoot.transform, new Vector3(0f, 0.84f, 0f), new Vector3(0.35f, 0.10f, 0.20f), pantsMat);
        CreateBox("Belt_Buckle", clerkRoot.transform, new Vector3(0f, 0.84f, 0.105f), new Vector3(0.06f, 0.04f, 0.015f), chromeMat, addCollider: false);

        // Torso / Red Gas Station Employee Polo
        CreateBox("Torso", clerkRoot.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.38f, 0.54f, 0.22f), shirtMat);

        // Employee Nametag Badge (Chest left)
        CreateBox("Nametag", clerkRoot.transform, new Vector3(-0.10f, 1.30f, 0.115f), new Vector3(0.08f, 0.04f, 0.01f), whiteMat, addCollider: false);

        // White Undershirt Collar
        CreateBox("Collar", clerkRoot.transform, new Vector3(0f, 1.42f, 0.03f), new Vector3(0.16f, 0.06f, 0.12f), whiteMat, addCollider: false);

        // Neck
        CreateBox("Neck", clerkRoot.transform, new Vector3(0f, 1.47f, 0f), new Vector3(0.12f, 0.08f, 0.11f), skinMat, addCollider: false);

        // Head
        CreateBox("Head", clerkRoot.transform, new Vector3(0f, 1.62f, 0f), new Vector3(0.20f, 0.22f, 0.20f), skinMat);

        // Hair
        CreateBox("Hair_Back", clerkRoot.transform, new Vector3(0f, 1.64f, -0.06f), new Vector3(0.22f, 0.20f, 0.12f), hairMat, addCollider: false);

        // Store Visor / Cap
        CreateBox("Cap_Crown", clerkRoot.transform, new Vector3(0f, 1.74f, 0f), new Vector3(0.21f, 0.06f, 0.21f), shirtMat, addCollider: false);
        CreateBox("Cap_Brim", clerkRoot.transform, new Vector3(0f, 1.72f, 0.13f), new Vector3(0.20f, 0.02f, 0.12f), shirtMat, addCollider: false);

        // Arms (Posed naturally forward over the register counter)
        // Left Upper Arm & Forearm
        CreateBox("UpperArm_L", clerkRoot.transform, new Vector3(-0.24f, 1.25f, 0.02f), new Vector3(0.10f, 0.30f, 0.11f), shirtMat);
        CreateBox("Forearm_L", clerkRoot.transform, new Vector3(-0.24f, 1.05f, 0.18f), new Vector3(0.09f, 0.09f, 0.26f), skinMat);
        CreateBox("Hand_L", clerkRoot.transform, new Vector3(-0.24f, 1.04f, 0.32f), new Vector3(0.08f, 0.07f, 0.09f), skinMat);

        // Right Upper Arm & Forearm (reaching towards barcode scanner/keyboard)
        CreateBox("UpperArm_R", clerkRoot.transform, new Vector3(0.24f, 1.25f, 0.02f), new Vector3(0.10f, 0.30f, 0.11f), shirtMat);
        CreateBox("Forearm_R", clerkRoot.transform, new Vector3(0.22f, 1.05f, 0.18f), new Vector3(0.09f, 0.09f, 0.26f), skinMat);
        CreateBox("Hand_R", clerkRoot.transform, new Vector3(0.20f, 1.04f, 0.32f), new Vector3(0.08f, 0.07f, 0.09f), skinMat);
    }

    private static void PositionPlayerAtEntrance(Transform storeRoot)
    {
        GameObject player = GameObject.Find("Test_FPS_Player");
        if (player == null) return;

        // Place player 3.8m outside the automatic sliding doors facing directly into the convenience store
        Vector3 spawnLocal = StorePos(8.115f, 0.08f, 8.20f);
        Vector3 worldPos = storeRoot.TransformPoint(spawnLocal);
        Quaternion worldRot = Quaternion.LookRotation(-storeRoot.forward, Vector3.up);

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.position = worldPos;
        player.transform.rotation = worldRot;
        if (cc != null) cc.enabled = true;
        EditorUtility.SetDirty(player);
    }

    private static GameObject CreateGroup(string name, Transform parent)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.identity;
        g.transform.localScale = Vector3.one;
        return g;
    }

    private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, bool addCollider = true)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = localScale;
        if (mat != null)
        {
            box.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        if (!addCollider)
        {
            BoxCollider bc = box.GetComponent<BoxCollider>();
            if (bc != null) UnityEngine.Object.DestroyImmediate(bc);
        }
        return box;
    }

    private static Material LoadMat(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{name}.mat");
    }

    private static Material GetOrCreateColorMaterial(string name, Color color)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", 0.5f);
            mat.SetFloat("_Cull", 0f);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }
}
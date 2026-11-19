using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// [InitializeOnLoad]
public static class SpawnDoubleBeverageFridge
{
    private const string MaterialsFolder = "Assets/small-red-gas-station/Materials";

    static SpawnDoubleBeverageFridge()
    {
        // EditorApplication.delayCall += AutoSpawnOnce;
    }

    private static void AutoSpawnOnce()
    {
        GameObject stacja = GameObject.Find("stacja");
        if (stacja == null) return;

        Transform store = stacja.transform.Find("Convenience_Store_V2");
        if (store == null) return;

        Transform existingFridge = store.Find("Double_Beverage_Fridge");
        if (existingFridge == null)
        {
            BuildFridge();
        }
    }

    [MenuItem("Tools/Gas Station/Spawn Double Beverage Fridge (With Can Drinks & Openable Glass)")]
    public static void BuildFridge()
    {
        GameObject stacja = GameObject.Find("stacja");
        if (stacja == null)
        {
            Debug.LogWarning("[SpawnDoubleBeverageFridge] 'stacja' not found in active scene.");
            return;
        }

        Transform store = stacja.transform.Find("Convenience_Store_V2");
        if (store == null)
        {
            Debug.LogWarning("[SpawnDoubleBeverageFridge] 'Convenience_Store_V2' not found under stacja.");
            return;
        }

        // 1. Remove previous fridge if present
        Transform oldFridge = store.Find("Double_Beverage_Fridge");
        if (oldFridge != null)
        {
            UnityEngine.Object.DestroyImmediate(oldFridge.gameObject);
        }

        Debug.Log("[SpawnDoubleBeverageFridge] Building commercial double beverage fridge with can drinks and interactable glass doors...");

        // 2. Load and create materials
        Material darkFrameMat = LoadMat("czarny mat");
        Material glassMat = LoadMat("Material.032");
        Material chromeMat = LoadMat("chrome");
        Material whiteMat = LoadMat("Material");
        Material grayMat = LoadMat("gray walls");
        Material glowMat = LoadMat("glowin");

        // Drink Can Flavor Materials
        Material matColaRed = GetOrCreateColorMaterial("Drink_Cola_Red", new Color(0.85f, 0.08f, 0.08f), 0.7f, 0.2f);
        Material matLimeGreen = GetOrCreateColorMaterial("Drink_Lime_Green", new Color(0.12f, 0.75f, 0.18f), 0.7f, 0.2f);
        Material matEnergyBlue = GetOrCreateColorMaterial("Drink_Energy_Blue", new Color(0.08f, 0.45f, 0.95f), 0.8f, 0.3f);
        Material matOrange = GetOrCreateColorMaterial("Drink_Orange_Citrus", new Color(0.96f, 0.48f, 0.05f), 0.7f, 0.2f);
        Material matGrapePurple = GetOrCreateColorMaterial("Drink_Grape_Purple", new Color(0.55f, 0.12f, 0.78f), 0.7f, 0.2f);
        Material matCherryRed = GetOrCreateColorMaterial("Drink_Cherry_Red", new Color(0.70f, 0.05f, 0.25f), 0.7f, 0.2f);
        Material matGoldTea = GetOrCreateColorMaterial("Drink_Gold_Tea", new Color(0.85f, 0.68f, 0.15f), 0.85f, 0.5f);
        Material matDietSilver = GetOrCreateColorMaterial("Drink_Diet_Silver", new Color(0.88f, 0.90f, 0.92f), 0.9f, 0.6f);

        // 3. Create Root Fridge in Store Coordinates:
        // Positioned against back wall: along = 8.50m (straight across from sliding entrance), height = 0m, depth = -1.95m
        // Facing +depth (forward toward store entrance and front glass)
        Vector3 fridgePosInStore = new Vector3(-8.50f, 0f, -1.95f);
        Quaternion fridgeRotInStore = Quaternion.identity;

        GameObject fridgeRoot = new GameObject("Double_Beverage_Fridge");
        fridgeRoot.transform.SetParent(store, false);
        fridgeRoot.transform.localPosition = fridgePosInStore;
        fridgeRoot.transform.localRotation = fridgeRotInStore;
        fridgeRoot.transform.localScale = Vector3.one;

        float width = 2.04f;
        float height = 2.16f;
        float depth = 0.78f;
        float wallT = 0.05f;
        float baseH = 0.32f; // Bottom vent base height (matching reference image)
        float topHeaderH = 0.18f;

        // =========================================================================
        // A. OUTER CABINET HOUSING
        // =========================================================================
        GameObject cabinet = CreateGroup("Cabinet_Housing", fridgeRoot.transform);

        // Back Wall
        CreateBox("Cabinet_Back", cabinet.transform, new Vector3(0f, height * 0.5f, -depth * 0.5f + wallT * 0.5f), new Vector3(width, height, wallT), darkFrameMat);
        // Left Side Wall
        CreateBox("Cabinet_Left_Wall", cabinet.transform, new Vector3(-width * 0.5f + wallT * 0.5f, height * 0.5f, 0f), new Vector3(wallT, height, depth), darkFrameMat);
        // Right Side Wall
        CreateBox("Cabinet_Right_Wall", cabinet.transform, new Vector3(width * 0.5f - wallT * 0.5f, height * 0.5f, 0f), new Vector3(wallT, height, depth), darkFrameMat);
        // Top Roof / Canopy
        CreateBox("Cabinet_Top", cabinet.transform, new Vector3(0f, height - wallT * 0.5f, 0f), new Vector3(width, wallT, depth), darkFrameMat);
        // Interior Bottom Floor (above bottom vent)
        CreateBox("Cabinet_Floor", cabinet.transform, new Vector3(0f, baseH + wallT * 0.5f, 0f), new Vector3(width - wallT * 2f, wallT, depth - wallT), grayMat);

        // Bottom Foundation Base (Front face)
        CreateBox("Base_Face", cabinet.transform, new Vector3(0f, baseH * 0.5f, depth * 0.5f - 0.02f), new Vector3(width, baseH, 0.04f), darkFrameMat);

        // Bottom Ventilation Slats (Vertical cooling grille matching reference photo)
        int slatCount = 22;
        float slatWidth = 0.022f;
        float slatHeight = 0.18f;
        float slatSpacing = (width - 0.30f) / slatCount;
        float slatStartX = -(width - 0.30f) * 0.5f;
        GameObject ventGroup = CreateGroup("Bottom_Vent_Grille", cabinet.transform);
        for (int i = 0; i < slatCount; i++)
        {
            float sx = slatStartX + i * slatSpacing + slatSpacing * 0.5f;
            CreateBox($"Vent_Slat_{i + 1}", ventGroup.transform, new Vector3(sx, baseH * 0.52f, depth * 0.5f), new Vector3(slatWidth, slatHeight, 0.015f), grayMat, addCollider: false);
        }

        // Top Header Fascia Band (Front face)
        CreateBox("Top_Header_Fascia", cabinet.transform, new Vector3(0f, height - topHeaderH * 0.5f, depth * 0.5f - 0.02f), new Vector3(width, topHeaderH, 0.04f), darkFrameMat);

        // Center Vertical Divider Mullion separating Left Bay from Right Bay
        float interiorH = height - baseH - topHeaderH;
        float interiorCenterY = baseH + interiorH * 0.5f;
        CreateBox("Center_Mullion", cabinet.transform, new Vector3(0f, interiorCenterY, depth * 0.5f - 0.025f), new Vector3(0.06f, interiorH, 0.05f), darkFrameMat);
        CreateBox("Center_Divider_Interior", cabinet.transform, new Vector3(0f, interiorCenterY, 0f), new Vector3(0.02f, interiorH, depth - wallT * 2f), grayMat, addCollider: false);

        // Interior White Liner on back wall
        CreateBox("Interior_Back_Liner", cabinet.transform, new Vector3(0f, interiorCenterY, -depth * 0.5f + wallT + 0.005f), new Vector3(width - wallT * 2f, interiorH, 0.01f), whiteMat, addCollider: false);

        // =========================================================================
        // B. INTERIOR WIRE SHELVES
        // =========================================================================
        GameObject shelvesGroup = CreateGroup("Shelves", fridgeRoot.transform);
        float[] shelfHeights = new float[] { baseH + 0.10f, baseH + 0.50f, baseH + 0.90f, baseH + 1.30f };
        float shelfW = (width - wallT * 2f - 0.06f) * 0.5f - 0.02f; // ~0.92m per bay
        float shelfD = depth - wallT * 2f - 0.08f; // ~0.60m
        float leftBayCenterX = -width * 0.25f - 0.01f;
        float rightBayCenterX = width * 0.25f + 0.01f;

        for (int s = 0; s < shelfHeights.Length; s++)
        {
            float shY = shelfHeights[s];
            // Left shelf
            CreateWireShelf($"Shelf_L_{s + 1}", shelvesGroup.transform, new Vector3(leftBayCenterX, shY, 0f), shelfW, shelfD, chromeMat);
            // Right shelf
            CreateWireShelf($"Shelf_R_{s + 1}", shelvesGroup.transform, new Vector3(rightBayCenterX, shY, 0f), shelfW, shelfD, chromeMat);
        }

        // =========================================================================
        // C. STOCKED DRINK CANS (Rows of branded commercial soda cans)
        // =========================================================================
        // Will be populated with 100% full branded cans via StockAllCommercialFridges()

        // =========================================================================
        // D. COOL REFRIGERATOR INTERIOR LIGHTING
        // =========================================================================
        GameObject lightsGroup = CreateGroup("Interior_Cooler_Lighting", fridgeRoot.transform);
        float lightY = height - topHeaderH - 0.03f;
        CreateFridgeLight("Light_Left_Bay", lightsGroup.transform, new Vector3(leftBayCenterX, lightY, 0.10f), glowMat);
        CreateFridgeLight("Light_Right_Bay", lightsGroup.transform, new Vector3(rightBayCenterX, lightY, 0.10f), glowMat);

        // =========================================================================
        // E. OPENABLE GLASS DOORS (Left & Right Hinge with 'E' Key Interaction)
        // =========================================================================
        GameObject doorsGroup = CreateGroup("Fridge_Glass_Doors", fridgeRoot.transform);

        float doorW = (width - 0.06f) * 0.5f; // ~0.99m
        float doorH = interiorH - 0.02f; // ~1.64m
        float doorZ = depth * 0.5f + 0.015f;

        // --- 1. LEFT GLASS DOOR ---
        // Pivot is on the LEFT edge of the left opening: X = -width * 0.5f + 0.03f
        float leftPivotX = -width * 0.5f + 0.03f;
        GameObject leftPivot = new GameObject("Left_Fridge_Door_Pivot");
        leftPivot.transform.SetParent(doorsGroup.transform, false);
        leftPivot.transform.localPosition = new Vector3(leftPivotX, interiorCenterY, doorZ);
        leftPivot.transform.localRotation = Quaternion.identity;

        BuildGlassDoorPanel("Left_Door_Leaf", leftPivot.transform,
            doorW, doorH, isLeftHinge: true, darkFrameMat, glassMat, chromeMat);

        InteractableFridgeDoor leftDoorInteract = leftPivot.AddComponent<InteractableFridgeDoor>();
        leftDoorInteract.doorLabel = "Left Fridge Door";
        leftDoorInteract.closedAngleY = 0f;
        leftDoorInteract.openAngleY = -85f; // Swings open outward to the left!
        leftDoorInteract.swingSpeed = 5.5f;
        leftDoorInteract.isOpen = false;

        // --- 2. RIGHT GLASS DOOR ---
        // Pivot is on the RIGHT edge of the right opening: X = width * 0.5f - 0.03f
        float rightPivotX = width * 0.5f - 0.03f;
        GameObject rightPivot = new GameObject("Right_Fridge_Door_Pivot");
        rightPivot.transform.SetParent(doorsGroup.transform, false);
        rightPivot.transform.localPosition = new Vector3(rightPivotX, interiorCenterY, doorZ);
        rightPivot.transform.localRotation = Quaternion.identity;

        BuildGlassDoorPanel("Right_Door_Leaf", rightPivot.transform,
            doorW, doorH, isLeftHinge: false, darkFrameMat, glassMat, chromeMat);

        InteractableFridgeDoor rightDoorInteract = rightPivot.AddComponent<InteractableFridgeDoor>();
        rightDoorInteract.doorLabel = "Right Fridge Door";
        rightDoorInteract.closedAngleY = 0f;
        rightDoorInteract.openAngleY = 85f; // Swings open outward to the right!
        rightDoorInteract.swingSpeed = 5.5f;
        rightDoorInteract.isOpen = false;

        // Fully pack all fridges with the branded soda cans
        StockAllFridgesWithBrandedCans.StockAllCommercialFridges();

        EditorUtility.SetDirty(fridgeRoot);
        EditorUtility.SetDirty(store);
        Scene activeScene = SceneManager.GetActiveScene();
        if (!EditorApplication.isPlaying && !Application.isPlaying)
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("[SpawnDoubleBeverageFridge] Successfully created Double Beverage Refrigerator with stocked can drinks and interactive openable glass doors!");
    }

    private static void StockShelfBay(Transform parent, float bayCenterX, float baseY, float shelfW, float shelfD, Material drinkMat, Material canSilverMat, string rowName)
    {
        int cansX = 4;
        int cansZ = 2;
        float spacingX = (shelfW - 0.16f) / (cansX - 1);
        float startX = bayCenterX - (shelfW - 0.16f) * 0.5f;
        float startZ = -shelfD * 0.25f;
        float zSpacing = shelfD * 0.45f;

        for (int z = 0; z < cansZ; z++)
        {
            for (int x = 0; x < cansX; x++)
            {
                float cx = startX + x * spacingX;
                float cz = startZ + z * zSpacing;
                CreateDrinkCan($"{rowName}_Can_{z}_{x}", parent, new Vector3(cx, baseY, cz), drinkMat, canSilverMat);
            }
        }
    }

    private static void CreateDrinkCan(string name, Transform parent, Vector3 basePos, Material labelMat, Material silverMat)
    {
        GameObject can = new GameObject(name);
        can.transform.SetParent(parent, false);
        can.transform.localPosition = basePos;
        can.transform.localRotation = Quaternion.identity;
        can.transform.localScale = Vector3.one;

        float canRadius = 0.038f;
        float canHeight = 0.14f;

        // Colored Can Body Cylinder
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(can.transform, false);
        body.transform.localPosition = new Vector3(0f, canHeight * 0.5f, 0f);
        body.transform.localScale = new Vector3(canRadius * 2f, canHeight * 0.5f, canRadius * 2f);
        if (labelMat != null) body.GetComponent<MeshRenderer>().sharedMaterial = labelMat;

        // Top Lid Disc (Silver)
        GameObject topLid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        topLid.name = "Top_Lid";
        topLid.transform.SetParent(can.transform, false);
        topLid.transform.localPosition = new Vector3(0f, canHeight - 0.004f, 0f);
        topLid.transform.localScale = new Vector3(canRadius * 1.95f, 0.008f, canRadius * 1.95f);
        if (silverMat != null) topLid.GetComponent<MeshRenderer>().sharedMaterial = silverMat;
        Collider topCol = topLid.GetComponent<Collider>();
        if (topCol != null) UnityEngine.Object.DestroyImmediate(topCol);

        // Pull Tab detailing
        CreateBox("Pull_Tab", can.transform, new Vector3(0f, canHeight + 0.002f, 0.008f), new Vector3(0.014f, 0.003f, 0.024f), silverMat, addCollider: false);

        // Bottom Rim Disc (Silver)
        GameObject bottomRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bottomRim.name = "Bottom_Rim";
        bottomRim.transform.SetParent(can.transform, false);
        bottomRim.transform.localPosition = new Vector3(0f, 0.004f, 0f);
        bottomRim.transform.localScale = new Vector3(canRadius * 1.95f, 0.008f, canRadius * 1.95f);
        if (silverMat != null) bottomRim.GetComponent<MeshRenderer>().sharedMaterial = silverMat;
        Collider botCol = bottomRim.GetComponent<Collider>();
        if (botCol != null) UnityEngine.Object.DestroyImmediate(botCol);
    }

    private static void CreateWireShelf(string name, Transform parent, Vector3 localPos, float width, float depth, Material chromeMat)
    {
        GameObject shelf = new GameObject(name);
        shelf.transform.SetParent(parent, false);
        shelf.transform.localPosition = localPos;
        shelf.transform.localRotation = Quaternion.identity;
        shelf.transform.localScale = Vector3.one;

        float wireT = 0.012f;

        // Main shelf plate
        CreateBox("Shelf_Plate", shelf.transform, Vector3.zero, new Vector3(width, wireT, depth), chromeMat);
        // Front lip guard rail
        CreateBox("Front_Lip", shelf.transform, new Vector3(0f, 0.018f, depth * 0.5f - 0.006f), new Vector3(width, 0.025f, 0.012f), chromeMat, addCollider: false);
    }

    private static void BuildGlassDoorPanel(
        string name, Transform pivotParent,
        float doorWidth, float doorHeight, bool isLeftHinge,
        Material frameMat, Material glassMat, Material handleMat)
    {
        GameObject leaf = new GameObject(name);
        leaf.transform.SetParent(pivotParent, false);

        // Center the leaf relative to pivot hinge:
        // If isLeftHinge: pivot is on left edge, so leaf center is at +doorWidth * 0.5f
        // If rightHinge: pivot is on right edge, so leaf center is at -doorWidth * 0.5f
        float leafCenterX = isLeftHinge ? (doorWidth * 0.5f) : (-doorWidth * 0.5f);
        leaf.transform.localPosition = new Vector3(leafCenterX, 0f, 0f);
        leaf.transform.localRotation = Quaternion.identity;
        leaf.transform.localScale = Vector3.one;

        float frameThick = 0.065f;
        float frameDepth = 0.045f;

        // Top Frame
        CreateBox("Frame_Top", leaf.transform, new Vector3(0f, (doorHeight - frameThick) * 0.5f, 0f), new Vector3(doorWidth, frameThick, frameDepth), frameMat);
        // Bottom Frame
        CreateBox("Frame_Bottom", leaf.transform, new Vector3(0f, -(doorHeight - frameThick) * 0.5f, 0f), new Vector3(doorWidth, frameThick, frameDepth), frameMat);
        // Left Frame
        CreateBox("Frame_Left", leaf.transform, new Vector3(-(doorWidth - frameThick) * 0.5f, 0f, 0f), new Vector3(frameThick, doorHeight, frameDepth), frameMat);
        // Right Frame
        CreateBox("Frame_Right", leaf.transform, new Vector3((doorWidth - frameThick) * 0.5f, 0f, 0f), new Vector3(frameThick, doorHeight, frameDepth), frameMat);

        // Transparent Glass Pane
        CreateBox("Glass_Pane", leaf.transform, Vector3.zero, new Vector3(doorWidth - frameThick * 2f, doorHeight - frameThick * 2f, 0.015f), glassMat);

        // Vertical Handle: placed on the side OPPOSITE to the hinge!
        // If left hinge: handle on right side (+X in leaf)
        // If right hinge: handle on left side (-X in leaf)
        float handleX = isLeftHinge ? (doorWidth * 0.5f - 0.12f) : (-doorWidth * 0.5f + 0.12f);
        CreateBox("Handle_Outer", leaf.transform, new Vector3(handleX, 0f, frameDepth * 0.5f + 0.025f), new Vector3(0.035f, 0.70f, 0.04f), handleMat);
        CreateBox("Handle_Inner", leaf.transform, new Vector3(handleX, 0f, -frameDepth * 0.5f - 0.025f), new Vector3(0.035f, 0.70f, 0.04f), handleMat);
    }

    private static void CreateFridgeLight(string name, Transform parent, Vector3 localPos, Material glowMat)
    {
        GameObject fixture = CreateBox(name, parent, localPos, new Vector3(0.80f, 0.03f, 0.08f), glowMat, addCollider: false);
        GameObject lightObj = new GameObject($"{name}_Source");
        lightObj.transform.SetParent(fixture.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        Light pt = lightObj.AddComponent<Light>();
        pt.type = LightType.Point;
        pt.color = new Color(0.86f, 0.94f, 1.0f); // Clean cool white refrigerator lighting
        pt.intensity = 4.5f;
        pt.range = 3.5f;
        pt.shadows = LightShadows.None;
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

    private static Material GetOrCreateColorMaterial(string name, Color color, float smoothness = 0.5f, float metallic = 0f)
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
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Cull", 0f);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }
}

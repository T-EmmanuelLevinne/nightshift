using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Non-destructive runtime performance & shadow optimizer across Night Shift & HouseScene:
/// 1. Uncaps frame rate (disables VSync, sets targetFrameRate = 120).
/// 2. Clamps shadow distance to 30m to prevent distant off-screen trees from drawing into shadow maps.
/// 3. On GasScene2: Keeps soft shadows on MainStore/Stockroom ceiling keys and disables shadow clutter.
/// 4. On HouseScene (Act 1 / 3 / 5): Keeps soft shadows ONLY on primary overhead room key lights + moonlight
///    and restricts shadow casting strictly to architecture, doors, furniture, hero props, and nearby trees.
/// 5. Enables GPU Instancing on tree/foliage/shelf materials for SRP Batcher efficiency.
/// </summary>
public class NightShiftPerformanceOptimizer : MonoBehaviour
{
    public static NightShiftPerformanceOptimizer Instance { get; private set; }
    private static bool sceneLoadedHookRegistered = false;

    private int pointLightsOptimizedCount = 0;
    private int clutterShadowsDisabledCount = 0;
    private int instancedMaterialsCount = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (!sceneLoadedHookRegistered)
        {
            sceneLoadedHookRegistered = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        EnsureExists();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    public static NightShiftPerformanceOptimizer EnsureExists()
    {
        if (Instance != null) return Instance;

        NightShiftPerformanceOptimizer found = Object.FindAnyObjectByType<NightShiftPerformanceOptimizer>();
        if (found != null)
        {
            Instance = found;
            return Instance;
        }

        GameObject go = new GameObject("NightShift_PerformanceOptimizer");
        Instance = go.AddComponent<NightShiftPerformanceOptimizer>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;

        ApplyFrameRateAndQualitySettings();

        bool isHouseScene = string.Equals(gameObject.scene.name, "HouseScene", System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(SceneManager.GetActiveScene().name, "HouseScene", System.StringComparison.OrdinalIgnoreCase);

        if (isHouseScene)
        {
            OptimizeHouseSceneLightsAndShadows();
            OptimizeHouseSceneShadowCasters();
            Debug.Log($"[NightShiftPerformanceOptimizer] HouseScene optimized shadows: {pointLightsOptimizedCount} secondary light shadows disabled, {clutterShadowsDisabledCount} non-essential mesh shadow casters disabled, {instancedMaterialsCount} materials instanced.");
        }
        else
        {
            OptimizeInteriorLights();
            OptimizeShelfMeshes();
            Debug.Log($"[NightShiftPerformanceOptimizer] Applied non-destructive optimizations: {pointLightsOptimizedCount} point light shadows disabled, {clutterShadowsDisabledCount} clutter mesh shadows disabled, {instancedMaterialsCount} materials instanced. Target FPS: 120.");
        }
    }

    public void ApplyFrameRateAndQualitySettings()
    {
        // 1. Uncap frame rate
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;

        // 2. Clamp shadow distance to 30m (reduces exterior shadow geometry passes)
        QualitySettings.shadowDistance = 30f;

        // 3. Optimize Camera Far Clip Plane (matches midnight fog distance, cuts distant culling)
        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].farClipPlane > 90f)
            {
                cams[i].farClipPlane = 85f;
            }
        }

        // 4. Optimize Terrains (LOD and pixel error)
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i] != null)
            {
                terrains[i].heightmapPixelError = 8f;
                terrains[i].basemapDistance = 85f;
                terrains[i].treeDistance = 120f;
                terrains[i].detailObjectDistance = 40f;
            }
        }

        // 5. De-duplicate AudioListeners across any simultaneously loaded scenes
        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
        if (listeners.Length > 1)
        {
            bool keptOne = false;
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] == null) continue;
                bool isPlayerCam = listeners[i].transform.parent != null &&
                                   listeners[i].transform.parent.name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (isPlayerCam && !keptOne)
                {
                    listeners[i].enabled = true;
                    keptOne = true;
                }
                else if (keptOne)
                {
                    listeners[i].enabled = false;
                }
            }
        }
    }

    public void OptimizeInteriorLights()
    {
        // Balanced interior lighting:
        // Keep soft, atmospheric shadows on the Main Store ceiling lights so shelves, counters,
        // and characters cast realistic contact shadows onto the floor, but LIMIT their shadow resolution tier.
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        pointLightsOptimizedCount = 0;

        for (int i = 0; i < allLights.Length; i++)
        {
            Light l = allLights[i];
            if (l == null) continue;

            string name = l.name;

            // 1. MAIN STORE & STOCKROOM CEILING LIGHTS: Keep soft shadows ENABLED & BALANCED!
            bool isKeyStoreLight = name.IndexOf("MainStore", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   name.IndexOf("Stockroom", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isKeyStoreLight)
            {
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.88f;
                l.shadowBias = 0.05f;
                l.shadowNormalBias = 0.4f;
                l.shadowNearPlane = 0.1f;

                var addLightData = l.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
                if (addLightData != null)
                {
                    // Limit resolution tier to Medium (Tier 1 = 512x512) so all faces fit comfortably in 2048x2048 atlas
                    addLightData.additionalLightsShadowResolutionTier = 1;
                }
                continue;
            }

            // 2. DIRECTIONAL MOON & CANOPY SPOTS: Keep shadows
            if (l.type == LightType.Directional || name.IndexOf("Canopy", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            // 3. SECONDARY / ENCLOSED FILL LIGHTS (cooler interiors, small restroom/bay accents):
            // Disable shadows on enclosed fill tubes to prevent shadow atlas overflow
            if (l.type == LightType.Point && l.shadows != LightShadows.None)
            {
                l.shadows = LightShadows.None;
                pointLightsOptimizedCount++;
            }
        }

        // 4. FIX UNNATURAL "X" CEILING SHADOWS:
        // Ensure fluorescent light fixture housing meshes do NOT cast shadows upward onto the ceiling from their own internal emitters.
        // The lights still cast soft, grounded shadows downwards onto shelves, counters, and floors.
        MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            MeshRenderer mr = allRenderers[i];
            if (mr == null) continue;
            string rName = mr.name;

            // STRICT PROTECTION: Never touch Beverage_Service_Counter or any of its children
            if (rName.IndexOf("Beverage_Service_Counter", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (mr.transform.parent != null && mr.transform.parent.name.IndexOf("Beverage_Service_Counter", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            if (rName.StartsWith("Light_MainStore", System.StringComparison.OrdinalIgnoreCase) ||
                rName.StartsWith("Light_Stockroom", System.StringComparison.OrdinalIgnoreCase) ||
                rName.StartsWith("Light_Male_Restroom", System.StringComparison.OrdinalIgnoreCase))
            {
                if (mr.shadowCastingMode != ShadowCastingMode.Off)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }
    }

    public void OptimizeShelfMeshes()
    {
        // CRITICAL PROTECTION: Find and strictly protect Beverage_Service_Counter
        Transform counterTransform = null;
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t != null && t.name.IndexOf("Beverage_Service_Counter", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                counterTransform = t;
                break;
            }
        }

        // Find shelf clutter mesh renderers
        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
        clutterShadowsDisabledCount = 0;
        HashSet<Material> instancedMaterials = new HashSet<Material>();

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer mr = renderers[i];
            if (mr == null) continue;

            // STRICT PROTECTION: Never touch Beverage_Service_Counter or any of its children
            if (counterTransform != null && (mr.transform == counterTransform || mr.transform.IsChildOf(counterTransform)))
            {
                continue;
            }
            if (mr.name.IndexOf("Beverage_Service_Counter", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            // Optimize foliage & bushes (never lag the game with shadow passes on foliage)
            if (mr.name.IndexOf("Bush", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                (mr.transform.parent != null && mr.transform.parent.name.IndexOf("Bush", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (mr.shadowCastingMode != ShadowCastingMode.Off)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                }
                Material bMat = mr.sharedMaterial;
                if (bMat != null && !instancedMaterials.Contains(bMat))
                {
                    bMat.enableInstancing = true;
                    instancedMaterials.Add(bMat);
                }
                continue;
            }

            // Optimize trees (enable GPU instancing across all tree materials)
            if (mr.name.IndexOf("Tree", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                (mr.transform.parent != null && mr.transform.parent.name.IndexOf("Tree", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                Material tMat = mr.sharedMaterial;
                if (tMat != null && !instancedMaterials.Contains(tMat))
                {
                    tMat.enableInstancing = true;
                    instancedMaterials.Add(tMat);
                }
                continue;
            }

            // Target the shelf pack (model.dae / shelves-pack / ID meshes)
            bool isShelfPack = false;
            Transform cur = mr.transform;
            while (cur != null)
            {
                string nm = cur.name;
                if (string.Equals(nm, "model", System.StringComparison.OrdinalIgnoreCase) ||
                    nm.IndexOf("shelves", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    nm.IndexOf("1695", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isShelfPack = true;
                    break;
                }
                cur = cur.parent;
            }

            if (!isShelfPack && mr.transform.name.StartsWith("ID"))
            {
                isShelfPack = true;
            }

            if (isShelfPack)
            {
                // Disable shadow casting on tiny snack clutter (cans, bags, boxes)
                // Bounds magnitude < 1.6f corresponds to individual clutter items on shelves
                if (mr.bounds.size.magnitude < 1.6f && mr.shadowCastingMode != ShadowCastingMode.Off)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    clutterShadowsDisabledCount++;
                }

                // Enable GPU Instancing on shelf materials
                Material mat = mr.sharedMaterial;
                if (mat != null && !instancedMaterials.Contains(mat))
                {
                    mat.enableInstancing = true;
                    instancedMaterials.Add(mat);
                }
            }
        }

        instancedMaterialsCount = instancedMaterials.Count;
    }

    public void OptimizeHouseSceneLightsAndShadows()
    {
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        pointLightsOptimizedCount = 0;

        Light bestLivingRoomKey = null;
        float bestLivingRange = -1f;
        Light secondLivingKey = null;
        float secondLivingRange = -1f;
        Light bestBedroomKey = null;
        float bestBedroomRange = -1f;
        Light bestBathroomKey = null;
        float bestBathroomRange = -1f;

        List<Light> interiorPointLights = new List<Light>(16);

        for (int i = 0; i < allLights.Length; i++)
        {
            Light l = allLights[i];
            if (l == null || !l.enabled || !l.gameObject.activeInHierarchy) continue;

            if (l.type == LightType.Directional)
            {
                // Moon directional light keeps realistic soft shadows within shadowDistance (30m)
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.80f;
                l.shadowBias = 0.04f;
                l.shadowNormalBias = 0.35f;
                continue;
            }

            if (l.type != LightType.Point) continue;

            string lname = l.name;
            if (lname.IndexOf("Fridge", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                lname.IndexOf("Candle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                lname.IndexOf("Bounce", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                lname.IndexOf("Burner", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                lname.IndexOf("Monster", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (l.shadows != LightShadows.None)
                {
                    l.shadows = LightShadows.None;
                    pointLightsOptimizedCount++;
                }
                continue;
            }

            interiorPointLights.Add(l);
            var room = HouseSceneStoryController.GetLightRoomPlace(l.transform);
            switch (room)
            {
                case CabinLightSwitch.RoomPlace.LivingRoom:
                    if (l.range > bestLivingRange)
                    {
                        bestLivingRange = l.range;
                        bestLivingRoomKey = l;
                    }
                    break;
                case CabinLightSwitch.RoomPlace.Bedroom:
                    if (l.range > bestBedroomRange)
                    {
                        bestBedroomRange = l.range;
                        bestBedroomKey = l;
                    }
                    break;
                case CabinLightSwitch.RoomPlace.Bathroom:
                    if (l.range > bestBathroomRange)
                    {
                        bestBathroomRange = l.range;
                        bestBathroomKey = l;
                    }
                    break;
            }
        }

        // Also select a distinct secondary key light on the Kitchen/Dining side of LivingRoom (>= 3.4m from main LivingRoom key)
        if (bestLivingRoomKey != null)
        {
            Vector3 mainLivingPos = bestLivingRoomKey.transform.position;
            for (int i = 0; i < interiorPointLights.Count; i++)
            {
                Light l = interiorPointLights[i];
                if (l == null || l == bestLivingRoomKey) continue;
                if (HouseSceneStoryController.GetLightRoomPlace(l.transform) != CabinLightSwitch.RoomPlace.LivingRoom) continue;
                if (Vector3.Distance(l.transform.position, mainLivingPos) >= 3.4f && l.range > secondLivingRange)
                {
                    secondLivingRange = l.range;
                    secondLivingKey = l;
                }
            }
        }

        for (int i = 0; i < interiorPointLights.Count; i++)
        {
            Light l = interiorPointLights[i];
            if (l == null) continue;

            bool isMainLiving = (l == bestLivingRoomKey);
            bool isSecondaryRoomKey = (l == secondLivingKey || l == bestBedroomKey || l == bestBathroomKey);

            if (isMainLiving || isSecondaryRoomKey)
            {
#if UNITY_EDITOR
                // Baked lights (m_Lightmapping=2) cannot cast realtime URP shadows; ensure key lights are Realtime
                if (l.lightmapBakeType == LightmapBakeType.Baked)
                {
                    l.lightmapBakeType = LightmapBakeType.Realtime;
                }
#endif
                l.shadows = LightShadows.Soft;
                l.shadowStrength = isMainLiving ? 0.92f : 0.85f;
                l.shadowBias = 0.015f;
                l.shadowNormalBias = 0.06f;
                l.shadowNearPlane = 0.12f;
                l.intensity *= 1.18f;

                var addLightData = l.GetComponent<UniversalAdditionalLightData>()
                                   ?? l.gameObject.AddComponent<UniversalAdditionalLightData>();
                if (addLightData != null)
                {
                    // Must disable usePipelineSettings so tight 0.06 normalBias applies instead of RPAsset's 0.5 bias
                    addLightData.usePipelineSettings = false;
                    // Tier 1 (512px) for main living room key, Tier 0 (256px) for other 3 room keys -> fits cleanly inside 2048x2048 atlas
                    addLightData.additionalLightsShadowResolutionTier = isMainLiving ? 1 : 0;
                }
            }
            else
            {
                if (l.shadows != LightShadows.None)
                {
                    l.shadows = LightShadows.None;
                    pointLightsOptimizedCount++;
                }
                // Soften overlapping non-shadow fill lights so cast shadows from overhead key lights read with realistic depth
                l.intensity *= 0.52f;
            }
        }
    }

    public void OptimizeHouseSceneShadowCasters()
    {
        Vector3 cabinCenter = new Vector3(-11.5f, 12.5f, 305.5f);
        GameObject buildingBase = CabinWindowUtility.FindBuildingBaseInScene(gameObject.scene);
        if (buildingBase != null)
        {
            Renderer baseRen = buildingBase.GetComponent<Renderer>();
            if (baseRen != null) cabinCenter = baseRen.bounds.center;
        }

        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
        clutterShadowsDisabledCount = 0;
        HashSet<Material> instancedMaterials = new HashSet<Material>();

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer mr = renderers[i];
            if (mr == null) continue;

            // Strict protection: never touch Beverage_Service_Counter if ever present
            if (HasAncestorNameContains(mr.transform, "Beverage_Service_Counter")) continue;

            string objName = mr.gameObject.name;

            // 1. Light covers & switches: must NOT cast shadows directly around ceiling light bulbs
            if (HasAncestorNameContains(mr.transform, "PFB_LightCover") ||
                HasAncestorNameContains(mr.transform, "PFB_Lightswitch") ||
                HasAncestorNameContains(mr.transform, "PFB_Carpet"))
            {
                if (mr.shadowCastingMode != ShadowCastingMode.Off)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    clutterShadowsDisabledCount++;
                }
                mr.receiveShadows = true;
                continue;
            }

            // 2. Window glass & rain overlays: transparent/no-shadow
            if (objName.IndexOf("WindowGlass", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objName.IndexOf("BrokenWindow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objName.IndexOf("Rain", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (mr.shadowCastingMode != ShadowCastingMode.Off)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    clutterShadowsDisabledCount++;
                }
                mr.receiveShadows = false;
                continue;
            }

            // 3. Bushes & Trees: disable shadow casting on foliage bushes and far trees outside the 32m shadow radius
            bool isBush = HasAncestorNameContains(mr.transform, "Bush");
            bool isTree = !isBush && (HasAncestorNameContains(mr.transform, "Tree") || HasAncestorNameContains(mr.transform, "House_Trees"));
            if (isBush || isTree)
            {
                Material fMat = mr.sharedMaterial;
                if (fMat != null && !instancedMaterials.Contains(fMat))
                {
                    fMat.enableInstancing = true;
                    instancedMaterials.Add(fMat);
                }

                float distSqToCabin = (mr.bounds.center - cabinCenter).sqrMagnitude;
                if (isBush || distSqToCabin > 32f * 32f)
                {
                    if (mr.shadowCastingMode != ShadowCastingMode.Off)
                    {
                        mr.shadowCastingMode = ShadowCastingMode.Off;
                        clutterShadowsDisabledCount++;
                    }
                }
                else
                {
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    mr.receiveShadows = true;
                }
                continue;
            }

            // 4. Tiny non-essential clutter/food props: skip shadow casting to save vertex passes
            bool isTinyClutterName =
                HasAncestorNameContains(mr.transform, "PFB_Egg") ||
                HasAncestorNameContains(mr.transform, "PFB_Bacon") ||
                HasAncestorNameContains(mr.transform, "PFB_Towel") ||
                HasAncestorNameContains(mr.transform, "PFB_Basket") ||
                HasAncestorNameContains(mr.transform, "Remote") ||
                HasAncestorNameContains(mr.transform, "Candle") ||
                HasAncestorNameContains(mr.transform, "Phone") ||
                HasAncestorNameContains(mr.transform, "Pigeon") ||
                HasAncestorNameContains(mr.transform, "Spoon") ||
                HasAncestorNameContains(mr.transform, "Spatula") ||
                HasAncestorNameContains(mr.transform, "Plate") ||
                HasAncestorNameContains(mr.transform, "Bowl");

            bool isEssentialStructureOrFurniture =
                HasAncestorNameContains(mr.transform, "PFB_Building") ||
                HasAncestorNameContains(mr.transform, "BrokenMullion") ||
                HasAncestorNameContains(mr.transform, "PFB_Door") ||
                HasAncestorNameContains(mr.transform, "PFB_CoffeeTable") ||
                HasAncestorNameContains(mr.transform, "PFB_Sofa") ||
                HasAncestorNameContains(mr.transform, "PFB_Dining") ||
                HasAncestorNameContains(mr.transform, "PFB_TV") ||
                HasAncestorNameContains(mr.transform, "PFB_Kitchen") ||
                HasAncestorNameContains(mr.transform, "PFB_Fridge") ||
                HasAncestorNameContains(mr.transform, "PFB_Stove") ||
                HasAncestorNameContains(mr.transform, "PFB_Oven") ||
                HasAncestorNameContains(mr.transform, "PFB_Bed") ||
                HasAncestorNameContains(mr.transform, "PFB_Chest") ||
                HasAncestorNameContains(mr.transform, "PFB_Closet") ||
                HasAncestorNameContains(mr.transform, "PFB_Bathroom") ||
                HasAncestorNameContains(mr.transform, "PFB_Shower") ||
                HasAncestorNameContains(mr.transform, "PFB_Toilet") ||
                HasAncestorNameContains(mr.transform, "PFB_Fireplace") ||
                HasAncestorNameContains(mr.transform, "PFB_Freestand") ||
                HasAncestorNameContains(mr.transform, "PFB_Lamp") ||
                HasAncestorNameContains(mr.transform, "monster");

            if (isEssentialStructureOrFurniture)
            {
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
            else if (isTinyClutterName || mr.bounds.size.magnitude < 0.42f)
            {
                if (mr.shadowCastingMode != ShadowCastingMode.Off)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    clutterShadowsDisabledCount++;
                }
                mr.receiveShadows = true;
            }
        }

        instancedMaterialsCount = instancedMaterials.Count;
    }

    private static bool HasAncestorNameContains(Transform tr, string substring)
    {
        Transform cur = tr;
        while (cur != null)
        {
            if (cur.name.IndexOf(substring, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            cur = cur.parent;
        }
        return false;
    }
}

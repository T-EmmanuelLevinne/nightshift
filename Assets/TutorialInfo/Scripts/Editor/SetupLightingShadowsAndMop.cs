using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// [InitializeOnLoad]
public static class SetupLightingShadowsAndMop
{
    static SetupLightingShadowsAndMop()
    {
        // EditorApplication.delayCall += ApplyLightingAndMopSetup;
    }

    [MenuItem("Tools/Gas Station/Configure Top Lights Shadows, Reflections & Mop")]
    public static void ApplyLightingAndMopSetup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded) return;

        bool isDirty = false;

        // 1. CONFIGURE STORE CEILING LIGHTS (Position just below fixtures, Soft Shadows, Reflected Highlights)
        string[] ceilingLightNames = new string[]
        {
            "Light_MainStore_FrontLeft_PointLight",
            "Light_MainStore_BackLeft_PointLight",
            "Light_MainStore_FrontRight_PointLight",
            "Light_MainStore_BackRight_PointLight",
            "Light_Male_Restroom_PointLight",
            "Light_Female_Restroom_PointLight"
        };

        foreach (var lname in ceilingLightNames)
        {
            GameObject lgo = GameObject.Find(lname);
            if (lgo != null)
            {
                // Position light source 15cm directly below the fluorescent fixture panel
                if (Mathf.Abs(lgo.transform.localPosition.y - (-0.15f)) > 0.05f)
                {
                    lgo.transform.localPosition = new Vector3(0f, -0.15f, 0f);
                    isDirty = true;
                }

                Light light = lgo.GetComponent<Light>();
                if (light != null)
                {
                    if (light.shadows != LightShadows.Soft)
                    {
                        light.shadows = LightShadows.Soft;
                        light.shadowResolution = LightShadowResolution.High;
                        light.shadowBias = 0.05f;
                        light.shadowNormalBias = 0.4f;
                        isDirty = true;
                    }

                    // Ensure warm fluorescent store ambiance with bright downward illumination
                    if (lname.Contains("MainStore") && light.intensity < 4.0f)
                    {
                        light.intensity = 4.5f;
                        light.range = 11.0f;
                        isDirty = true;
                    }
                }
            }
        }

        // 2. CONFIGURE GAS STATION CANOPY DOWNLIGHTS OVER PUMP ISLANDS
        GameObject canopyGroup = GameObject.Find("Canopy_Ceiling_Lighting");
        if (canopyGroup == null)
        {
            canopyGroup = new GameObject("Canopy_Ceiling_Lighting");
            canopyGroup.transform.position = Vector3.zero;
            canopyGroup.transform.rotation = Quaternion.identity;
            isDirty = true;
        }

        Vector3[] canopyLightPositions = new Vector3[]
        {
            new Vector3(2.5f, 5.2f, -4.5f),
            new Vector3(7.5f, 5.2f, -4.5f),
            new Vector3(2.5f, 5.2f, -7.5f),
            new Vector3(7.5f, 5.2f, -7.5f)
        };

        for (int i = 0; i < canopyLightPositions.Length; i++)
        {
            string lightName = $"Canopy_Downlight_{i + 1}";
            Transform lt = canopyGroup.transform.Find(lightName);
            GameObject cLightGo;
            if (lt == null)
            {
                cLightGo = new GameObject(lightName);
                cLightGo.transform.SetParent(canopyGroup.transform, false);
                cLightGo.transform.position = canopyLightPositions[i];
                cLightGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Point straight down

                Light spot = cLightGo.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.spotAngle = 105f;
                spot.innerSpotAngle = 75f;
                spot.color = new Color(0.96f, 0.98f, 1.0f); // Crisp clean commercial canopy white
                spot.intensity = 8.0f;
                spot.range = 10.0f;
                spot.shadows = LightShadows.Soft;
                spot.shadowResolution = LightShadowResolution.High;
                spot.shadowBias = 0.05f;
                spot.shadowNormalBias = 0.4f;

                isDirty = true;
            }
        }

        // 3. CONFIGURE REFLECTION PROBES (Store Interior & Outdoor Canopy)
        GameObject probesGroup = GameObject.Find("Environment_Reflection_Probes");
        if (probesGroup == null)
        {
            probesGroup = new GameObject("Environment_Reflection_Probes");
            probesGroup.transform.position = Vector3.zero;
            isDirty = true;
        }

        // Store Interior Reflection Probe
        Transform storeProbeT = probesGroup.transform.Find("Store_Interior_Reflection_Probe");
        if (storeProbeT == null)
        {
            GameObject spGo = new GameObject("Store_Interior_Reflection_Probe");
            spGo.transform.SetParent(probesGroup.transform, false);
            spGo.transform.position = new Vector3(-8.5f, 1.6f, 0.5f);

            ReflectionProbe rp = spGo.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Realtime;
            rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            rp.boxProjection = true;
            rp.size = new Vector3(13.0f, 3.5f, 9.5f);
            rp.center = Vector3.zero;
            rp.intensity = 1.0f;

            isDirty = true;
        }

        // Canopy Reflection Probe
        Transform canopyProbeT = probesGroup.transform.Find("Canopy_Exterior_Reflection_Probe");
        if (canopyProbeT == null)
        {
            GameObject cpGo = new GameObject("Canopy_Exterior_Reflection_Probe");
            cpGo.transform.SetParent(probesGroup.transform, false);
            cpGo.transform.position = new Vector3(5.0f, 2.8f, -6.0f);

            ReflectionProbe rp = cpGo.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Realtime;
            rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            rp.boxProjection = true;
            rp.size = new Vector3(18.0f, 6.0f, 14.0f);
            rp.center = Vector3.zero;
            rp.intensity = 1.0f;

            isDirty = true;
        }

        // 4. SPAWN EQUIPPABLE CLEANING MOP
        GameObject existingMop = GameObject.Find("Cleaning_Mop");
        if (existingMop == null)
        {
            GameObject mopGo = new GameObject("Cleaning_Mop");
            // Place leaning naturally against the wall near the entrance/restroom area
            mopGo.transform.position = new Vector3(-5.35f, 0.0f, 1.85f);
            mopGo.transform.rotation = Quaternion.Euler(0f, 45f, 6f);
            mopGo.transform.localScale = Vector3.one;

            // MeshFilter & Renderer
            MeshFilter mf = mopGo.AddComponent<MeshFilter>();
            string objPath = "Assets/Mop/Mop.obj";
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(objPath);
            foreach (var a in assets)
            {
                if (a is Mesh mesh)
                {
                    mf.sharedMesh = mesh;
                    break;
                }
            }

            MeshRenderer mr = mopGo.AddComponent<MeshRenderer>();
            string matPath = "Assets/Mop/Mop_Material.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                mr.sharedMaterial = mat;
            }
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;

            // Physics Collider for reticle detection and world physics
            CapsuleCollider col = mopGo.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.72f, 0f);
            col.radius = 0.16f;
            col.height = 1.45f;
            col.direction = 1; // Y-axis

            // Equippable & Interactive Component
            EquippableMop mopScript = mopGo.AddComponent<EquippableMop>();
            mopScript.mopDuration = 2.0f;

            isDirty = true;
            Debug.Log("[SetupLightingShadowsAndMop] Successfully spawned authentic Cleaning Mop in the store!");
        }

        if (isDirty)
        {
            if (!EditorApplication.isPlaying && !Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[SetupLightingShadowsAndMop] Top lights shadows, reflections, and Cleaning Mop successfully configured!");
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Configures authentic 12:00 AM Midnight horror atmosphere (starry skybox, pale full moon,
/// cool lunar directional shadows, midnight mist fog, trilight ambient, and glowing outdoor
/// gas pump canopy downlights) across any scene (SampleScene, Scene2, etc.).
/// </summary>
[ExecuteAlways]
public class MidnightAtmosphereController : MonoBehaviour
{
    public static MidnightAtmosphereController Instance { get; private set; }

    [Header("Midnight Sky & Moon")]
    public Color skyZenithColor = new Color(0.007f, 0.012f, 0.026f, 1f);
    public Color skyHorizonColor = new Color(0.020f, 0.029f, 0.050f, 1f);
    public Color skyGroundColor = new Color(0.008f, 0.011f, 0.018f, 1f);
    public Color moonDiscColor = new Color(0.84f, 0.90f, 1.00f, 1f);
    public Vector3 moonSkyDirection = new Vector3(0.76f, 0.46f, 0.46f);
    [Range(0.01f, 0.08f)]
    public float moonAngularSize = 0.032f;
    [Range(0f, 2f)]
    public float starIntensity = 0.90f;

    [Header("Directional Moonlight")]
    public Color moonlightColor = new Color(0.46f, 0.56f, 0.76f, 1f);
    [Range(0f, 1f)]
    public float moonlightIntensity = 0.22f;

    [Header("Midnight Ambient & Mist Fog")]
    public Color ambientSkyColor = new Color(0.068f, 0.085f, 0.138f, 1f);
    public Color ambientEquatorColor = new Color(0.050f, 0.062f, 0.096f, 1f);
    public Color ambientGroundColor = new Color(0.024f, 0.028f, 0.040f, 1f);
    public bool enableMidnightFog = true;
    public Color midnightFogColor = new Color(0.018f, 0.026f, 0.046f, 1f);
    [Range(0.002f, 0.05f)]
    public float midnightFogDensity = 0.0145f;

    [Header("Outdoor Gas Canopy Illumination")]
    public Color canopyLightColor = new Color(0.95f, 0.97f, 1.00f, 1f);
    public float canopyLightIntensity = 16.0f;
    public float canopyLightRange = 13.5f;

    private Material runtimeSkyboxMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitializeOnPlay()
    {
        EnsureExistsAndApply();
    }

    public static MidnightAtmosphereController EnsureExistsAndApply()
    {
        if (Instance == null || !Instance.gameObject.activeInHierarchy)
        {
            Instance = FindAnyObjectByType<MidnightAtmosphereController>();
        }

        if (Instance == null)
        {
            GameObject go = GameObject.Find("Midnight_Atmosphere");
            if (go == null)
            {
                go = new GameObject("Midnight_Atmosphere");
            }
            Instance = go.GetComponent<MidnightAtmosphereController>();
            if (Instance == null)
            {
                Instance = go.AddComponent<MidnightAtmosphereController>();
            }
        }

        Instance.ApplyMidnightAtmosphere();
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null || !Instance.gameObject.activeInHierarchy)
        {
            Instance = this;
        }
        ApplyMidnightAtmosphere();
    }

    private void OnEnable()
    {
        Instance = this;
        ApplyMidnightAtmosphere();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled)
        {
            ApplyMidnightAtmosphere();
        }
    }

    public void ApplyMidnightAtmosphere()
    {
        Vector3 moonDirNorm = moonSkyDirection.sqrMagnitude > 0.001f
            ? moonSkyDirection.normalized
            : new Vector3(0.76f, 0.46f, 0.46f).normalized;

        // 1. Configure Directional Moonlight & disable duplicate imported daytime suns
        Light moonLight = ConfigureDirectionalMoonlight(moonDirNorm);

        // 2. Configure Starry Midnight Skybox
        ConfigureMidnightSkybox(moonDirNorm, moonLight);

        // 3. Configure Midnight Trilight Ambient, Fog, and Reflections
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSkyColor;
        RenderSettings.ambientEquatorColor = ambientEquatorColor;
        RenderSettings.ambientGroundColor = ambientGroundColor;
        RenderSettings.ambientIntensity = 1.0f;
        RenderSettings.reflectionIntensity = 0.35f;

        RenderSettings.fog = enableMidnightFog;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = midnightFogColor;
        RenderSettings.fogDensity = midnightFogDensity;

        // 4. Ensure Outdoor Gas Pump Canopy Lights illuminate the fuel islands at night
        ConfigureOutdoorCanopyLights();

        // 5. Ensure Active Cameras render the Midnight Skybox cleanly
        ConfigureCamerasForMidnight();

        // 6. Refresh GI & Realtime Reflection Probes
        DynamicGI.UpdateEnvironment();
        RefreshRealtimeReflectionProbes();
    }

    private Light ConfigureDirectionalMoonlight(Vector3 moonDirNorm)
    {
        Light[] allLights = FindObjectsByType<Light>(FindObjectsInactive.Include);
        Light primaryMoonLight = null;

        // Prefer the root scene "Directional Light"
        for (int i = 0; i < allLights.Length; i++)
        {
            Light l = allLights[i];
            if (l == null || IsUnderProtectedObject(l.transform)) continue;
            if (l.type != LightType.Directional) continue;

            if (l.transform.parent == null && string.Equals(l.gameObject.name, "Directional Light", System.StringComparison.OrdinalIgnoreCase))
            {
                primaryMoonLight = l;
                break;
            }
        }

        // Fallback to first available non-protected directional light
        if (primaryMoonLight == null)
        {
            for (int i = 0; i < allLights.Length; i++)
            {
                Light l = allLights[i];
                if (l == null || IsUnderProtectedObject(l.transform)) continue;
                if (l.type == LightType.Directional)
                {
                    primaryMoonLight = l;
                    break;
                }
            }
        }

        // Disable any duplicate directional lights (e.g. imported Blender/SketchUp sun inside stacja)
        for (int i = 0; i < allLights.Length; i++)
        {
            Light l = allLights[i];
            if (l == null || IsUnderProtectedObject(l.transform)) continue;
            if (l.type == LightType.Directional && l != primaryMoonLight)
            {
                l.enabled = false;
            }
        }

        if (primaryMoonLight != null)
        {
            primaryMoonLight.gameObject.SetActive(true);
            primaryMoonLight.enabled = true;
            primaryMoonLight.transform.rotation = Quaternion.LookRotation(-moonDirNorm, Vector3.up);
            primaryMoonLight.color = moonlightColor;
            primaryMoonLight.useColorTemperature = false;
            primaryMoonLight.intensity = moonlightIntensity;
            primaryMoonLight.shadows = LightShadows.Soft;
            primaryMoonLight.shadowStrength = 0.85f;
            RenderSettings.sun = primaryMoonLight;
        }

        return primaryMoonLight;
    }

    private void ConfigureMidnightSkybox(Vector3 moonDirNorm, Light moonLight)
    {
        Material skyMat = null;

        Material resMat = Resources.Load<Material>("MidnightHorrorSkybox");
        if (resMat != null)
        {
            if (Application.isPlaying)
            {
                if (runtimeSkyboxMaterial == null || runtimeSkyboxMaterial.shader != resMat.shader)
                {
                    runtimeSkyboxMaterial = new Material(resMat);
                    runtimeSkyboxMaterial.name = "MidnightHorrorSkybox_Runtime";
                }
                skyMat = runtimeSkyboxMaterial;
            }
            else
            {
                skyMat = resMat;
            }
        }

        if (skyMat == null)
        {
            Shader customSkyShader = Shader.Find("Skybox/MidnightHorrorSkybox");
            if (customSkyShader != null)
            {
                if (runtimeSkyboxMaterial == null || runtimeSkyboxMaterial.shader != customSkyShader)
                {
                    runtimeSkyboxMaterial = new Material(customSkyShader);
                    runtimeSkyboxMaterial.name = "MidnightHorrorSkybox_Generated";
                }
                skyMat = runtimeSkyboxMaterial;
            }
        }

        if (skyMat != null && skyMat.shader != null && skyMat.shader.name == "Skybox/MidnightHorrorSkybox")
        {
            skyMat.SetColor("_ZenithColor", skyZenithColor);
            skyMat.SetColor("_HorizonColor", skyHorizonColor);
            skyMat.SetColor("_GroundColor", skyGroundColor);
            skyMat.SetColor("_MoonColor", moonDiscColor);
            skyMat.SetVector("_MoonDir", new Vector4(moonDirNorm.x, moonDirNorm.y, moonDirNorm.z, 0f));
            skyMat.SetFloat("_MoonSize", moonAngularSize);
            skyMat.SetFloat("_StarIntensity", starIntensity);
            RenderSettings.skybox = skyMat;
            return;
        }

        // Fallback to Unity's built-in Procedural Skybox tuned to pitch-dark midnight
        Shader procShader = Shader.Find("Skybox/Procedural");
        if (procShader != null)
        {
            if (runtimeSkyboxMaterial == null || runtimeSkyboxMaterial.shader != procShader)
            {
                runtimeSkyboxMaterial = new Material(procShader);
                runtimeSkyboxMaterial.name = "MidnightProceduralSkybox_Fallback";
            }

            if (runtimeSkyboxMaterial.HasProperty("_SunSize")) runtimeSkyboxMaterial.SetFloat("_SunSize", 0.028f);
            if (runtimeSkyboxMaterial.HasProperty("_SunSizeConvergence")) runtimeSkyboxMaterial.SetFloat("_SunSizeConvergence", 8f);
            if (runtimeSkyboxMaterial.HasProperty("_AtmosphereThickness")) runtimeSkyboxMaterial.SetFloat("_AtmosphereThickness", 0.18f);
            if (runtimeSkyboxMaterial.HasProperty("_SkyTint")) runtimeSkyboxMaterial.SetColor("_SkyTint", new Color(0.02f, 0.03f, 0.06f, 1f));
            if (runtimeSkyboxMaterial.HasProperty("_GroundColor")) runtimeSkyboxMaterial.SetColor("_GroundColor", skyGroundColor);
            if (runtimeSkyboxMaterial.HasProperty("_Exposure")) runtimeSkyboxMaterial.SetFloat("_Exposure", 0.14f);

            RenderSettings.skybox = runtimeSkyboxMaterial;
        }
    }

    private void ConfigureOutdoorCanopyLights()
    {
        GameObject stacja = GameObject.Find("stacja");
        if (stacja == null) return;

        // Detect exact outdoor canopy light fixture positions from stacja's imported Area.* ceiling lights
        List<Vector3> outdoorCeilingLampPts = new List<Vector3>();
        Transform[] stacjaChildren = stacja.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < stacjaChildren.Length; i++)
        {
            Transform tr = stacjaChildren[i];
            if (tr == null || IsUnderProtectedObject(tr)) continue;

            if (tr.name.StartsWith("Area", System.StringComparison.OrdinalIgnoreCase))
            {
                Vector3 wp = tr.position;
                // Outdoor pump canopy is in front of store (x < -2.2f) and elevated (y > 3.4f)
                if (wp.x < -2.2f && wp.y > 3.4f)
                {
                    outdoorCeilingLampPts.Add(wp);
                }
            }
        }

        Vector3[] targetLightPositions = new Vector3[4];
        Vector3 canopyCenter;

        if (outdoorCeilingLampPts.Count >= 2)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            float sumY = 0f;

            for (int i = 0; i < outdoorCeilingLampPts.Count; i++)
            {
                Vector3 p = outdoorCeilingLampPts[i];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
                sumY += p.y;
            }

            float lampY = (sumY / outdoorCeilingLampPts.Count) - 0.16f;
            float xA = Mathf.Lerp(minX, maxX, 0.22f);
            float xB = Mathf.Lerp(minX, maxX, 0.78f);
            float zA = Mathf.Lerp(minZ, maxZ, 0.22f);
            float zB = Mathf.Lerp(minZ, maxZ, 0.78f);

            if (Mathf.Abs(maxX - minX) < 1.2f) { xA = minX - 2.2f; xB = maxX + 2.2f; }
            if (Mathf.Abs(maxZ - minZ) < 1.2f) { zA = minZ - 2.2f; zB = maxZ + 2.2f; }

            targetLightPositions[0] = new Vector3(xA, lampY, zA);
            targetLightPositions[1] = new Vector3(xB, lampY, zA);
            targetLightPositions[2] = new Vector3(xA, lampY, zB);
            targetLightPositions[3] = new Vector3(xB, lampY, zB);
            canopyCenter = new Vector3((minX + maxX) * 0.5f, 2.4f, (minZ + maxZ) * 0.5f);
        }
        else
        {
            // Fallback coordinates directly beneath the gas pump canopy ceiling in front of the store
            float lampY = 4.45f;
            targetLightPositions[0] = new Vector3(-13.5f, lampY, -4.5f);
            targetLightPositions[1] = new Vector3(-7.5f, lampY, -4.5f);
            targetLightPositions[2] = new Vector3(-13.5f, lampY, 0.8f);
            targetLightPositions[3] = new Vector3(-7.5f, lampY, 0.8f);
            canopyCenter = new Vector3(-10.5f, 2.4f, -1.85f);
        }

        GameObject canopyGroup = GameObject.Find("Canopy_Ceiling_Lighting");
        if (canopyGroup == null)
        {
            canopyGroup = new GameObject("Canopy_Ceiling_Lighting");
            canopyGroup.transform.position = Vector3.zero;
            canopyGroup.transform.rotation = Quaternion.identity;
        }

        for (int i = 0; i < 4; i++)
        {
            string lightName = $"Canopy_Downlight_{i + 1}";
            Transform lt = canopyGroup.transform.Find(lightName);
            GameObject lightGo;
            if (lt == null)
            {
                lightGo = new GameObject(lightName);
                lightGo.transform.SetParent(canopyGroup.transform, false);
            }
            else
            {
                lightGo = lt.gameObject;
            }

            lightGo.transform.position = targetLightPositions[i];
            lightGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Light spot = lightGo.GetComponent<Light>();
            if (spot == null)
            {
                spot = lightGo.AddComponent<Light>();
            }

            spot.enabled = true;
            spot.type = LightType.Spot;
            spot.spotAngle = 124f;
            spot.innerSpotAngle = 82f;
            spot.color = canopyLightColor;
            spot.intensity = canopyLightIntensity;
            spot.range = canopyLightRange;
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.75f;
            spot.shadowBias = 0.05f;
            spot.shadowNormalBias = 0.4f;
        }

        GameObject canopyProbeGo = GameObject.Find("Canopy_Exterior_Reflection_Probe");
        if (canopyProbeGo != null && !IsUnderProtectedObject(canopyProbeGo.transform))
        {
            canopyProbeGo.transform.position = canopyCenter;
        }
    }

    private void ConfigureCamerasForMidnight()
    {
        Camera[] cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
        for (int i = 0; i < cams.Length; i++)
        {
            Camera c = cams[i];
            if (c == null) continue;
            c.clearFlags = CameraClearFlags.Skybox;
            c.backgroundColor = new Color(0.010f, 0.015f, 0.030f, 1f);
        }
    }

    private void RefreshRealtimeReflectionProbes()
    {
        if (!Application.isPlaying) return;

        ReflectionProbe[] probes = FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude);
        for (int i = 0; i < probes.Length; i++)
        {
            ReflectionProbe rp = probes[i];
            if (rp != null && rp.mode == ReflectionProbeMode.Realtime)
            {
                rp.RenderProbe();
            }
        }
    }

    private static bool IsUnderProtectedObject(Transform tr)
    {
        Transform cur = tr;
        while (cur != null)
        {
            if (string.Equals(cur.name, "Beverage_Service_Counter", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            cur = cur.parent;
        }
        return false;
    }
}


using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Automatically converts Built-in Standard materials from imported asset packs
/// (such as FurnishedCabin and BathroomPack) to Universal Render Pipeline/Lit,
/// fixing the missing shader "hot pink / purple" appearance in URP.
/// </summary>
[InitializeOnLoad]
public static class ConvertCabinMaterialsToURP
{
    private static readonly string[] FoldersToConvert = new string[]
    {
        "Assets/FurnishedCabin/Meshes/Materials",
        "Assets/BathroomPack/Materials"
    };

    static ConvertCabinMaterialsToURP()
    {
        EditorApplication.delayCall += AutoConvertOnLoad;
    }

    private static void AutoConvertOnLoad()
    {
        ConvertMaterials(false);
    }

    [MenuItem("Tools/Cabin/Convert Cabin Materials to URP (Fix Purple Pink)")]
    public static void MenuConvertMaterials()
    {
        ConvertMaterials(true);
    }

    public static void ConvertMaterials(bool showLog)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogWarning("[ConvertCabinMaterialsToURP] Could not find 'Universal Render Pipeline/Lit' shader!");
            return;
        }

        int converted = 0;

        foreach (string folder in FoldersToConvert)
        {
            if (!Directory.Exists(folder)) continue;

            string[] matFiles = Directory.GetFiles(folder, "*.mat", SearchOption.AllDirectories);
            foreach (string file in matFiles)
            {
                string assetPath = file.Replace("\\", "/");

                // STRICT PROTECTION: Never touch Beverage_Service_Counter or user-placed scene objects
                if (assetPath.IndexOf("Beverage_Service_Counter", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (mat == null) continue;

                // Check if already URP Lit
                if (mat.shader != null && mat.shader.name == "Universal Render Pipeline/Lit")
                {
                    continue;
                }

                // Read existing properties from Standard / Built-in shader
                Texture mainTex = mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null
                    ? mat.GetTexture("_MainTex")
                    : (mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null);

                Color baseColor = mat.HasProperty("_Color")
                    ? mat.GetColor("_Color")
                    : (mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white);

                Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;

                Texture metallicMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
                float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                float smoothness = mat.HasProperty("_Glossiness")
                    ? mat.GetFloat("_Glossiness")
                    : (mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f);

                Texture occlusionMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
                float occlusionStrength = mat.HasProperty("_OcclusionStrength") ? mat.GetFloat("_OcclusionStrength") : 1f;

                Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
                Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

                float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;
                bool isCutout = mat.IsKeywordEnabled("_ALPHATEST_ON") ||
                                (mat.HasProperty("_Mode") && Mathf.Approximately(mat.GetFloat("_Mode"), 1f));

                // Switch to URP Lit shader
                mat.shader = urpLit;

                // Re-apply properties to URP Lit slots
                if (mainTex != null)
                {
                    mat.SetTexture("_BaseMap", mainTex);
                    mat.SetTexture("_MainTex", mainTex);
                }
                mat.SetColor("_BaseColor", baseColor);
                mat.SetColor("_Color", baseColor);

                if (bumpMap != null)
                {
                    mat.SetTexture("_BumpMap", bumpMap);
                    mat.SetFloat("_BumpScale", bumpScale);
                    mat.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    mat.DisableKeyword("_NORMALMAP");
                }

                if (metallicMap != null)
                {
                    mat.SetTexture("_MetallicGlossMap", metallicMap);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                else
                {
                    mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                }
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);

                if (occlusionMap != null)
                {
                    mat.SetTexture("_OcclusionMap", occlusionMap);
                    mat.SetFloat("_OcclusionStrength", occlusionStrength);
                }

                bool hasEmission = emissionMap != null || (emissionColor.r > 0.01f || emissionColor.g > 0.01f || emissionColor.b > 0.01f);
                if (hasEmission)
                {
                    if (emissionMap != null) mat.SetTexture("_EmissionMap", emissionMap);
                    mat.SetColor("_EmissionColor", emissionColor);
                    mat.EnableKeyword("_EMISSION");
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                }

                if (isCutout)
                {
                    mat.SetFloat("_Surface", 0f); // Opaque surface with alpha cutout
                    mat.SetFloat("_AlphaClip", 1f); // Cutout ON
                    mat.SetFloat("_Cutoff", cutoff);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int)RenderQueue.AlphaTest;
                }
                else
                {
                    mat.SetFloat("_Surface", 0f);
                    mat.SetFloat("_AlphaClip", 0f);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int)RenderQueue.Geometry;
                }

                EditorUtility.SetDirty(mat);
                converted++;
            }
        }

        if (converted > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ConvertCabinMaterialsToURP] Successfully upgraded {converted} materials to Universal Render Pipeline/Lit!");
        }
        else if (showLog)
        {
            Debug.Log("[ConvertCabinMaterialsToURP] All materials are already up-to-date URP Lit materials.");
        }
    }
}


using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FixShelvesMaterials
{
    private const string ModelPath = "Assets/shelves-pack/source/1695/model.dae";
    private const string MaterialsFolder = "Assets/shelves-pack/Materials";
    private const string TexturesFolder1 = "Assets/shelves-pack/source/1695/model";
    private const string TexturesFolder2 = "Assets/shelves-pack/textures";

    static FixShelvesMaterials()
    {
        EditorApplication.delayCall += AutoFixOnce;
    }

    private static void AutoFixOnce()
    {
        if (!Directory.Exists(MaterialsFolder) || Directory.GetFiles(MaterialsFolder, "*.mat").Length == 0)
        {
            FixMaterials(false);
        }
    }

    [MenuItem("Tools/Gas Station/Fix Shelves Pack Pink Materials (URP)")]
    public static void MenuFixMaterials()
    {
        FixMaterials(true);
    }

    public static void FixMaterials(bool showDialog)
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[FixShelves] Could not find ModelImporter at '{ModelPath}'");
            return;
        }

        if (!Directory.Exists(MaterialsFolder))
        {
            Directory.CreateDirectory(MaterialsFolder);
            AssetDatabase.Refresh();
        }

        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null) urpLitShader = Shader.Find("Standard");

        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
        int convertedCount = 0;
        var matMap = new System.Collections.Generic.Dictionary<string, Material>();

        foreach (Object obj in subAssets)
        {
            if (obj is Material embeddedMat)
            {
                string matName = embeddedMat.name;
                string matAssetPath = Path.Combine(MaterialsFolder, matName + ".mat").Replace("\\", "/");

                Material externalMat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);
                if (externalMat == null)
                {
                    externalMat = new Material(urpLitShader);
                    externalMat.name = matName;

                    Texture2D tex = FindTextureForMaterial(matName);
                    if (tex != null)
                    {
                        externalMat.SetTexture("_BaseMap", tex);
                        externalMat.SetTexture("_MainTex", tex);
                        externalMat.SetColor("_BaseColor", Color.white);
                        externalMat.SetColor("_Color", Color.white);
                    }
                    else
                    {
                        if (matName.ToLower().Contains("steel") || matName.ToLower().Contains("metal") || matName.ToLower().Contains("wire"))
                        {
                            externalMat.SetColor("_BaseColor", new Color(0.75f, 0.75f, 0.78f, 1f));
                            externalMat.SetFloat("_Metallic", 0.85f);
                            externalMat.SetFloat("_Smoothness", 0.6f);
                        }
                        else if (matName.ToLower().Contains("rubber") || matName.ToLower().Contains("wheel"))
                        {
                            externalMat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.12f, 1f));
                            externalMat.SetFloat("_Metallic", 0.0f);
                            externalMat.SetFloat("_Smoothness", 0.2f);
                        }
                        else if (matName.ToLower().Contains("tray"))
                        {
                            externalMat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f, 1f));
                            externalMat.SetFloat("_Metallic", 0.1f);
                            externalMat.SetFloat("_Smoothness", 0.4f);
                        }
                        else if (matName.ToLower().Contains("edge"))
                        {
                            externalMat.SetColor("_BaseColor", new Color(0.2f, 0.2f, 0.2f, 1f));
                        }
                        else if (embeddedMat.HasProperty("_Color"))
                        {
                            externalMat.SetColor("_BaseColor", embeddedMat.color);
                            externalMat.SetColor("_Color", embeddedMat.color);
                        }
                    }

                    externalMat.SetFloat("_Smoothness", 0.35f);
                    AssetDatabase.CreateAsset(externalMat, matAssetPath);
                }
                else
                {
                    externalMat.shader = urpLitShader;
                    Texture2D tex = FindTextureForMaterial(matName);
                    if (tex != null)
                    {
                        externalMat.SetTexture("_BaseMap", tex);
                        externalMat.SetTexture("_MainTex", tex);
                    }
                    EditorUtility.SetDirty(externalMat);
                }

                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), externalMat);
                matMap[matName] = externalMat;
                convertedCount++;
            }
        }

        importer.SaveAndReimport();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Also update any MeshRenderers in open scene that have matching material names
        MeshRenderer[] sceneRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
        int sceneUpdated = 0;
        foreach (MeshRenderer mr in sceneRenderers)
        {
            Material[] sharedMats = mr.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < sharedMats.Length; i++)
            {
                if (sharedMats[i] != null && matMap.TryGetValue(sharedMats[i].name, out Material newMat))
                {
                    sharedMats[i] = newMat;
                    changed = true;
                }
            }
            if (changed)
            {
                Undo.RecordObject(mr, "Update Shelves URP Materials");
                mr.sharedMaterials = sharedMats;
                EditorUtility.SetDirty(mr);
                sceneUpdated++;
            }
        }

        Debug.Log($"[FixShelves] Successfully converted {convertedCount} materials to Universal Render Pipeline (URP/Lit)! Scene renderers updated: {sceneUpdated}");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Shelves Materials Converted", $"Successfully converted {convertedCount} materials to Universal Render Pipeline (URP/Lit) with textures applied!\nUpdated {sceneUpdated} objects in the scene.", "OK");
        }
    }

    private static Texture2D FindTextureForMaterial(string matName)
    {
        string[] searchFolders = new string[] { TexturesFolder1, TexturesFolder2 };

        foreach (string folder in searchFolders)
        {
            if (!Directory.Exists(folder)) continue;

            string[] extensions = new string[] { ".jpg", ".jpeg", ".png", ".tga" };
            foreach (string ext in extensions)
            {
                string candidate = Path.Combine(folder, matName + ext).Replace("\\", "/");
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(candidate);
                if (tex != null) return tex;
            }
        }

        if (matName.ToLower().Contains("header"))
        {
            string candidate = Path.Combine(TexturesFolder1, "header.jpg").Replace("\\", "/");
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(candidate);
            if (tex != null) return tex;
        }

        return null;
    }
}

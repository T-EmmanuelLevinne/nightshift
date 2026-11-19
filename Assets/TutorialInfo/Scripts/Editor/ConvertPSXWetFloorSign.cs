using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ConvertPSXWetFloorSign
{
    private const string FbxPath = "Assets/psx-wet-floor-sign/source/model.fbx";
    private const string TexturePath = "Assets/psx-wet-floor-sign/textures/gltf_embedded_0.png";
    private const string MaterialsFolder = "Assets/psx-wet-floor-sign/Materials";
    private const string MatPath = "Assets/psx-wet-floor-sign/Materials/Wet_Floor_Sign_Mat.mat";
    private const string LegacyMatPath = "Assets/psx-wet-floor-sign/Materials/gltf_embedded_0.mat";
    private const string PrefabsFolder = "Assets/psx-wet-floor-sign/Prefabs";
    private const string PrefabPath = "Assets/psx-wet-floor-sign/Prefabs/PSX_Wet_Floor_Sign.prefab";
    private const string SetupKey = "PSX_WetFloorSign_V2_Setup";

    static ConvertPSXWetFloorSign()
    {
        EditorApplication.delayCall += AutoSetupOnce;
    }

    private static void AutoSetupOnce()
    {
        if (!SessionState.GetBool(SetupKey, false))
        {
            SessionState.SetBool(SetupKey, true);
            SetupWetFloorSign(false);
        }
    }

    [MenuItem("Tools/Gas Station/Convert & Setup PSX Wet Floor Sign")]
    public static void MenuSetup()
    {
        SetupWetFloorSign(true);
    }

    [MenuItem("Tools/Gas Station/Spawn PSX Wet Floor Sign")]
    public static void SpawnWetFloorSign()
    {
        SetupWetFloorSign(false);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[ConvertPSXWetFloorSign] Could not load prefab at " + PrefabPath);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "PSX_Wet_Floor_Sign";

        // Place near the store entrance floor / cleaning mop spot
        GameObject refObj = GameObject.Find("Cleaning_Mop") ?? GameObject.Find("Test_FPS_Player");
        if (refObj != null)
        {
            instance.transform.position = refObj.transform.position + new Vector3(0.8f, 0f, 0.5f);
        }
        else
        {
            instance.transform.position = new Vector3(-6.5f, 0.05f, 0f);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Spawn PSX Wet Floor Sign");
        Selection.activeGameObject = instance;
        SceneView.FrameLastActiveSceneView();
        Debug.Log("[ConvertPSXWetFloorSign] Spawned PSX Wet Floor Sign in the scene!");
    }

    public static void SetupWetFloorSign(bool logToConsole)
    {
        // 1. Force re-import of the correctly oriented texture with Point filtering
        if (File.Exists(TexturePath))
        {
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
            TextureImporter texImporter = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (texImporter != null)
            {
                bool texChanged = false;
                if (texImporter.filterMode != FilterMode.Point)
                {
                    texImporter.filterMode = FilterMode.Point;
                    texChanged = true;
                }
                if (texImporter.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    texImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    texChanged = true;
                }
                if (!texImporter.alphaIsTransparency)
                {
                    texImporter.alphaIsTransparency = true;
                    texChanged = true;
                }
                if (texChanged)
                {
                    texImporter.SaveAndReimport();
                }
            }
        }

        // 2. Ensure Materials Folder and URP Material
        if (!Directory.Exists(MaterialsFolder))
        {
            Directory.CreateDirectory(MaterialsFolder);
            AssetDatabase.Refresh();
        }

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        Texture2D signTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

        Material signMat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (signMat == null)
        {
            signMat = new Material(urpLit);
            signMat.name = "Wet_Floor_Sign_Mat";
            AssetDatabase.CreateAsset(signMat, MatPath);
        }
        else
        {
            signMat.shader = urpLit;
        }

        if (signTex != null)
        {
            signMat.SetTexture("_BaseMap", signTex);
            signMat.SetTexture("_MainTex", signTex);
        }
        signMat.SetColor("_BaseColor", Color.white);
        signMat.SetColor("_Color", Color.white);
        signMat.SetFloat("_Metallic", 0.0f);
        signMat.SetFloat("_Smoothness", 0.15f);
        signMat.SetFloat("_Cull", 0f); // Double-sided rendering
        EditorUtility.SetDirty(signMat);

        // Also update legacy material if present
        Material legacyMat = AssetDatabase.LoadAssetAtPath<Material>(LegacyMatPath);
        if (legacyMat != null)
        {
            legacyMat.shader = urpLit;
            if (signTex != null)
            {
                legacyMat.SetTexture("_BaseMap", signTex);
                legacyMat.SetTexture("_MainTex", signTex);
            }
            legacyMat.SetColor("_BaseColor", Color.white);
            legacyMat.SetFloat("_Metallic", 0.0f);
            legacyMat.SetFloat("_Smoothness", 0.15f);
            legacyMat.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(legacyMat);
        }

        // 3. Configure ModelImporter on model.fbx
        if (File.Exists(FbxPath))
        {
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
            ModelImporter modelImporter = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (modelImporter != null)
            {
                bool importerChanged = false;
                if (modelImporter.importCameras)
                {
                    modelImporter.importCameras = false;
                    importerChanged = true;
                }
                if (modelImporter.importLights)
                {
                    modelImporter.importLights = false;
                    importerChanged = true;
                }
                if (modelImporter.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
                {
                    modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    importerChanged = true;
                }

                modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material_0"), signMat);
                modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "gltf_embedded_0"), signMat);
                importerChanged = true;

                if (importerChanged)
                {
                    modelImporter.SaveAndReimport();
                }
            }
        }

        // 4. Update Prefab
        if (!Directory.Exists(PrefabsFolder))
        {
            Directory.CreateDirectory(PrefabsFolder);
            AssetDatabase.Refresh();
        }

        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxAsset != null)
        {
            GameObject tempInstance = Object.Instantiate(fbxAsset);
            tempInstance.name = "PSX_Wet_Floor_Sign";

            MeshRenderer[] renderers = tempInstance.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sharedMaterial = signMat;
                }
            }

            MeshCollider mc = tempInstance.GetComponent<MeshCollider>();
            if (mc != null) Object.DestroyImmediate(mc);

            BoxCollider col = tempInstance.GetComponent<BoxCollider>();
            if (col == null) col = tempInstance.AddComponent<BoxCollider>();

            Renderer[] allRends = tempInstance.GetComponentsInChildren<Renderer>(true);
            if (allRends.Length > 0)
            {
                Bounds b = allRends[0].bounds;
                for (int i = 1; i < allRends.Length; i++)
                {
                    b.Encapsulate(allRends[i].bounds);
                }
                Vector3 lossy = tempInstance.transform.lossyScale;
                Vector3 localSize = new Vector3(
                    Mathf.Abs(lossy.x) > 0.0001f ? b.size.x / Mathf.Abs(lossy.x) : b.size.x,
                    Mathf.Abs(lossy.y) > 0.0001f ? b.size.y / Mathf.Abs(lossy.y) : b.size.y,
                    Mathf.Abs(lossy.z) > 0.0001f ? b.size.z / Mathf.Abs(lossy.z) : b.size.z
                );
                col.center = tempInstance.transform.InverseTransformPoint(b.center);
                col.size = localSize;
                col.isTrigger = true;
            }

            PrefabUtility.SaveAsPrefabAsset(tempInstance, PrefabPath);
            Object.DestroyImmediate(tempInstance);
        }

        // 5. Update any existing wet floor signs in the open scene so they instantly show the texture and safe trigger collider
        GameObject[] allSceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (GameObject go in allSceneObjects)
        {
            if (go == null) continue;
            bool isWetFloorSign = (go.name == "PSX_Wet_Floor_Sign" ||
                                  go.name.Contains("model") && (go.transform.Find("side1") != null || go.transform.Find("root/side1") != null));
            if (isWetFloorSign)
            {
                MeshRenderer[] mrs = go.GetComponentsInChildren<MeshRenderer>(true);
                foreach (MeshRenderer mr in mrs)
                {
                    if (mr != null)
                    {
                        mr.sharedMaterial = signMat;
                        EditorUtility.SetDirty(mr);
                    }
                }

                // Remove invalid empty MeshColliders
                MeshCollider mc = go.GetComponent<MeshCollider>();
                if (mc != null && mc.sharedMesh == null)
                {
                    Object.DestroyImmediate(mc);
                }

                // Ensure BoxCollider is properly scaled in local space and is a trigger
                BoxCollider bc = go.GetComponent<BoxCollider>();
                if (bc != null)
                {
                    Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
                    if (rends.Length > 0)
                    {
                        Bounds b = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++)
                        {
                            b.Encapsulate(rends[i].bounds);
                        }
                        Vector3 lossy = go.transform.lossyScale;
                        Vector3 localSize = new Vector3(
                            Mathf.Abs(lossy.x) > 0.0001f ? b.size.x / Mathf.Abs(lossy.x) : b.size.x,
                            Mathf.Abs(lossy.y) > 0.0001f ? b.size.y / Mathf.Abs(lossy.y) : b.size.y,
                            Mathf.Abs(lossy.z) > 0.0001f ? b.size.z / Mathf.Abs(lossy.z) : b.size.z
                        );
                        bc.center = go.transform.InverseTransformPoint(b.center);
                        bc.size = localSize;
                    }
                    bc.isTrigger = true;
                    EditorUtility.SetDirty(bc);
                }

                EditorUtility.SetDirty(go);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (logToConsole)
        {
            Debug.Log("[ConvertPSXWetFloorSign] Wet Floor Sign textures and materials refreshed successfully!");
        }
    }
}

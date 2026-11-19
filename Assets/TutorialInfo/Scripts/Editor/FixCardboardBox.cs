using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FixCardboardBox
{
    private const string ModelPath = "Assets/cardboard-box/source/model/model/model.dae";
    private const string MaterialsFolder = "Assets/cardboard-box/Materials";
    private const string PrefabsFolder = "Assets/cardboard-box/Prefabs";
    private const string PrefabPath = "Assets/cardboard-box/Prefabs/Cardboard_Box.prefab";
    private const string CustomMeshAssetPath = "Assets/cardboard-box/Prefabs/Custom_Cardboard_Box_Mesh.asset";
    private const string MatPath = "Assets/cardboard-box/Materials/Cardboard_Box_Mat.mat";
    private const string SetupKey = "CardboardBox_RestockSystem_V3_Setup";

    static FixCardboardBox()
    {
        EditorApplication.delayCall += AutoFixOnce;
    }

    private static void AutoFixOnce()
    {
        if (!SessionState.GetBool(SetupKey, false) || !File.Exists(MatPath) || !File.Exists(PrefabPath) || !File.Exists(CustomMeshAssetPath))
        {
            SessionState.SetBool(SetupKey, true);
            FixMaterials(false);
            EnsureSceneRestockObjects(false);
        }
    }

    [MenuItem("Tools/Gas Station/Fix Cardboard Box Materials (URP)")]
    public static void MenuFixMaterials()
    {
        FixMaterials(true);
        EnsureSceneRestockObjects(true);
    }

    [MenuItem("Tools/Gas Station/Setup Cardboard Box + UserBoxPutter + Shelf Restock (12 Cans)")]
    public static void MenuSetupRestockSystem()
    {
        FixMaterials(false);
        RecreateStoreGondolaShelf.EnsureShelfMaterialsAndPrefabs(false);
        RecreateStoreGondolaShelf.ApplyEmptyCanSlotToSceneShelves();
        EnsureSceneRestockObjects(true);
    }

    [MenuItem("Tools/Gas Station/Spawn Cardboard Box")]
    public static void SpawnCardboardBox()
    {
        FixMaterials(false);
        EnsureSceneRestockObjects(true);
    }

    public static void EnsureSceneRestockObjects(bool logToConsole)
    {
        // Strictly only operate in GasScene2! Never spawn gas station restock box / putter in HouseScene or other scenes.
        UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, "GasScene2", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        GameObject shelf1 = GameObject.Find("Shelf1");
        Vector3 defaultPutterPos = new Vector3(-8.42f, 0.09f, -0.38f);
        Vector3 defaultBoxPos = new Vector3(-7.15f, 0.09f, -0.38f);

        if (shelf1 != null)
        {
            ShelfRestockTarget shelfTarget = shelf1.GetComponent<ShelfRestockTarget>();
            if (shelfTarget == null)
            {
                shelfTarget = shelf1.AddComponent<ShelfRestockTarget>();
                EditorUtility.SetDirty(shelf1);
            }

            Transform slotTr = shelf1.transform.Find("Restock_Cans_Slot");
            if (slotTr != null && shelfTarget.restockCansSlot != slotTr.gameObject)
            {
                shelfTarget.restockCansSlot = slotTr.gameObject;
                EditorUtility.SetDirty(shelfTarget);
            }

            Renderer shelfRenderer = shelf1.GetComponent<Renderer>();
            if (shelfRenderer != null)
            {
                Bounds sb = shelfRenderer.bounds;
                float floorY = sb.min.y;
                Vector3 slotWorld = sb.center;

                if (slotTr != null)
                {
                    MeshFilter slotMf = slotTr.GetComponent<MeshFilter>();
                    if (slotMf != null && slotMf.sharedMesh != null)
                    {
                        slotWorld = slotTr.TransformPoint(slotMf.sharedMesh.bounds.center);
                    }
                }

                float frontZ = (slotWorld.z <= sb.center.z)
                    ? (sb.min.z - 0.42f)
                    : (sb.max.z + 0.42f);

                defaultPutterPos = new Vector3(slotWorld.x, floorY, frontZ);
                defaultBoxPos = new Vector3(slotWorld.x + 1.25f, floorY, frontZ);
            }
        }

        // 1. Ensure UserBoxPutter exists in scene (preserve transform if already placed by user)
        GameObject putterObj = GameObject.Find("UserBoxPutter");
        if (putterObj == null)
        {
            putterObj = new GameObject("UserBoxPutter");
            putterObj.transform.position = defaultPutterPos;
            putterObj.transform.rotation = Quaternion.identity;
            putterObj.AddComponent<UserBoxPutter>();
            EditorUtility.SetDirty(putterObj);
            if (putterObj.scene.IsValid()) EditorSceneManager.MarkSceneDirty(putterObj.scene);
        }
        else if (putterObj.GetComponent<UserBoxPutter>() == null)
        {
            putterObj.AddComponent<UserBoxPutter>();
            EditorUtility.SetDirty(putterObj);
        }

        // 2. Ensure Cardboard_Box exists in scene and uses our custom Cardboard Box design + texture
        Material boxMat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        Mesh customMesh = EnsureSavedCustomBoxMesh();

        GameObject boxObj = GameObject.Find("Cardboard_Box");
        if (boxObj == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                boxObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                boxObj.name = "Cardboard_Box";
                boxObj.transform.position = defaultBoxPos;
                boxObj.transform.rotation = Quaternion.identity;
                boxObj.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            }
        }

        if (boxObj != null)
        {
            ApplyCustomDesignToBoxObject(boxObj, customMesh, boxMat);

            if (boxObj.transform.localScale.x > 0.8f)
            {
                boxObj.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            }

            EditorUtility.SetDirty(boxObj);
            if (boxObj.scene.IsValid()) EditorSceneManager.MarkSceneDirty(boxObj.scene);
        }

        SceneView.RepaintAll();

        if (logToConsole && boxObj != null)
        {
            Selection.activeGameObject = boxObj;
            Debug.Log("[FixCardboardBox] Applied custom Cardboard Box 3D design with cardboard texture to Cardboard_Box!");
        }
    }

    private static Mesh EnsureSavedCustomBoxMesh()
    {
        if (!Directory.Exists(PrefabsFolder))
        {
            Directory.CreateDirectory(PrefabsFolder);
            AssetDatabase.Refresh();
        }

        Mesh customMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CustomMeshAssetPath);
        if (customMesh == null)
        {
            Mesh generated = Object.Instantiate(RestockCardboardBox.GetOrCreateCustomCardboardBoxMesh());
            generated.name = "Custom_Cardboard_Box_Mesh";
            AssetDatabase.CreateAsset(generated, CustomMeshAssetPath);
            AssetDatabase.SaveAssets();
            customMesh = generated;
        }
        return customMesh;
    }

    private static void ApplyCustomDesignToBoxObject(GameObject boxObj, Mesh customMesh, Material boxMat)
    {
        if (boxObj == null || customMesh == null) return;

        MeshFilter[] childFilters = boxObj.GetComponentsInChildren<MeshFilter>(true);
        MeshFilter targetFilter = null;

        for (int i = 0; i < childFilters.Length; i++)
        {
            if (childFilters[i] == null) continue;
            string n = childFilters[i].gameObject.name;
            if (n.StartsWith("BoxCan_") || n == "Held_Stock_Can_Viewmodel") continue;

            if (targetFilter == null)
            {
                targetFilter = childFilters[i];
            }
            else
            {
                Renderer r = childFilters[i].GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }
        }

        if (targetFilter == null)
        {
            targetFilter = boxObj.GetComponent<MeshFilter>();
            if (targetFilter == null) targetFilter = boxObj.AddComponent<MeshFilter>();
        }

        if (targetFilter.transform != boxObj.transform)
        {
            targetFilter.transform.localPosition = Vector3.zero;
            targetFilter.transform.localRotation = Quaternion.identity;
            targetFilter.transform.localScale = Vector3.one;
        }

        targetFilter.sharedMesh = customMesh;

        MeshRenderer mr = targetFilter.GetComponent<MeshRenderer>();
        if (mr == null) mr = targetFilter.gameObject.AddComponent<MeshRenderer>();
        if (boxMat != null)
        {
            mr.sharedMaterials = new Material[] { boxMat };
        }

        BoxCollider col = boxObj.GetComponent<BoxCollider>();
        if (col == null) col = boxObj.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, RestockCardboardBox.LocalBoxHeight * 0.5f, 0f);
        col.size = new Vector3(
            RestockCardboardBox.LocalBoxWidth * 1.15f,
            RestockCardboardBox.LocalBoxHeight * 1.05f,
            RestockCardboardBox.LocalBoxDepth * 1.15f
        );

        if (boxObj.GetComponent<RestockCardboardBox>() == null)
        {
            boxObj.AddComponent<RestockCardboardBox>();
        }
    }

    public static void FixMaterials(bool logToConsole)
    {
        FixNormalMapTexture("Assets/cardboard-box/source/model/model/textures/Material_normal.png");
        FixNormalMapTexture("Assets/cardboard-box/textures/Material_normal.png");

        if (!Directory.Exists(MaterialsFolder))
        {
            Directory.CreateDirectory(MaterialsFolder);
            AssetDatabase.Refresh();
        }

        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null) urpLitShader = Shader.Find("Standard");

        Material boxMat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (boxMat == null)
        {
            boxMat = new Material(urpLitShader);
            boxMat.name = "Cardboard_Box_Mat";
            AssetDatabase.CreateAsset(boxMat, MatPath);
        }
        else
        {
            boxMat.shader = urpLitShader;
        }

        Texture2D albedo = FindTexture("cardboadtexture") ?? FindTexture("Material_albedo");
        Texture2D normal = FindTexture("Material_normal");
        Texture2D ao = FindTexture("Material_AO");

        if (albedo != null)
        {
            boxMat.SetTexture("_BaseMap", albedo);
            boxMat.SetTexture("_MainTex", albedo);
        }
        if (normal != null)
        {
            boxMat.SetTexture("_BumpMap", normal);
            boxMat.SetFloat("_BumpScale", 1.0f);
            boxMat.EnableKeyword("_NORMALMAP");
        }
        if (ao != null)
        {
            boxMat.SetTexture("_OcclusionMap", ao);
            boxMat.SetFloat("_OcclusionStrength", 1.0f);
        }

        boxMat.SetColor("_BaseColor", Color.white);
        boxMat.SetColor("_Color", Color.white);
        boxMat.SetFloat("_Metallic", 0.0f);
        boxMat.SetFloat("_Smoothness", 0.22f);
        EditorUtility.SetDirty(boxMat);

        if (File.Exists(ModelPath))
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer != null)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material"), boxMat);
                importer.SaveAndReimport();
            }
        }

        Mesh customMesh = EnsureSavedCustomBoxMesh();

        GameObject tempInstance = new GameObject("Cardboard_Box");
        tempInstance.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        ApplyCustomDesignToBoxObject(tempInstance, customMesh, boxMat);

        PrefabUtility.SaveAsPrefabAsset(tempInstance, PrefabPath);
        Object.DestroyImmediate(tempInstance);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void FixNormalMapTexture(string path)
    {
        if (!File.Exists(path)) return;

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }
    }

    private static Texture2D FindTexture(string partialName)
    {
        string[] searchFolders = new string[]
        {
            "Assets/cardboard-box/source/model/model/textures",
            "Assets/cardboard-box/textures"
        };

        foreach (string folder in searchFolders)
        {
            if (!Directory.Exists(folder)) continue;
            string[] files = Directory.GetFiles(folder, "*" + partialName + "*.*");
            foreach (string file in files)
            {
                if (file.EndsWith(".meta")) continue;
                string unityPath = file.Replace("\\", "/");
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(unityPath);
                if (tex != null) return tex;
            }
        }
        return null;
    }
}

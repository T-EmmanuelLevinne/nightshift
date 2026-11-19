using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SetupFloorStains
{
    private const string SetupKey = "SetupFloorStains_V4_Photoreal_Done";

    private static readonly string[] ModelPaths = new string[]
    {
        "Assets/FloorStains/Models/Floor_Stain_Model_1.obj",
        "Assets/FloorStains/Models/Floor_Stain_Model_2.obj",
        "Assets/FloorStains/Models/Floor_Stain_Model_3.obj"
    };

    private static readonly string[] TexturePaths = new string[]
    {
        "Assets/FloorStains/Textures/stain_spill_01.png",
        "Assets/FloorStains/Textures/stain_spill_02.png",
        "Assets/FloorStains/Textures/stain_spill_03.png"
    };

    private static readonly string[] NormalPaths = new string[]
    {
        "Assets/FloorStains/Textures/stain_spill_01_norm.png",
        "Assets/FloorStains/Textures/stain_spill_02_norm.png",
        "Assets/FloorStains/Textures/stain_spill_03_norm.png"
    };

    private static readonly string[] GlossPaths = new string[]
    {
        "Assets/FloorStains/Textures/stain_spill_01_gloss.png",
        "Assets/FloorStains/Textures/stain_spill_02_gloss.png",
        "Assets/FloorStains/Textures/stain_spill_03_gloss.png"
    };

    private static readonly string[] MatPaths = new string[]
    {
        "Assets/FloorStains/Materials/Floor_Stain_1_Mat.mat",
        "Assets/FloorStains/Materials/Floor_Stain_2_Mat.mat",
        "Assets/FloorStains/Materials/Floor_Stain_3_Mat.mat"
    };

    private static readonly string[] PrefabPaths = new string[]
    {
        "Assets/FloorStains/Prefabs/Floor_Stain_1.prefab",
        "Assets/FloorStains/Prefabs/Floor_Stain_2.prefab",
        "Assets/FloorStains/Prefabs/Floor_Stain_3.prefab"
    };

    private static readonly string[] StainNames = new string[]
    {
        "Floor_Stain_1",
        "Floor_Stain_2",
        "Floor_Stain_3"
    };

    private static readonly Vector3[] DefaultPositions = new Vector3[]
    {
        new Vector3(-2.2f, 0.015f, -0.8f), // Front aisle near entrance
        new Vector3(-0.6f, 0.015f, 0.4f),  // Between central grocery shelves
        new Vector3(0.9f, 0.015f, 1.4f)    // Back aisle towards stockroom
    };

    private static readonly Quaternion[] DefaultRotations = new Quaternion[]
    {
        Quaternion.Euler(0f, 25f, 0f),
        Quaternion.Euler(0f, 110f, 0f),
        Quaternion.Euler(0f, 205f, 0f)
    };

    static SetupFloorStains()
    {
        EditorApplication.delayCall += AutoSetupOnce;
    }

    private static void AutoSetupOnce()
    {
        if (!SessionState.GetBool(SetupKey, false))
        {
            SessionState.SetBool(SetupKey, true);
            ExecuteSetup(false);
        }
    }

    [MenuItem("Tools/Gas Station/Setup and Spawn 3 Floor Stains")]
    public static void MenuSetup()
    {
        ExecuteSetup(true);
    }

    public static void ExecuteSetup(bool focusSceneView)
    {
        // Strictly only operate in GasScene2! Never spawn gas station floor stains in HouseScene or other scenes.
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, "GasScene2", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Ensure folders exist
        EnsureDirectory("Assets/FloorStains/Materials");
        EnsureDirectory("Assets/FloorStains/Prefabs");

        // Ensure Unity has indexed newly generated texture files
        AssetDatabase.Refresh();

        // 1. Setup Textures (Albedo, Normal Map, Metallic/Gloss)
        for (int i = 0; i < TexturePaths.Length; i++)
        {
            SetupTextureImporter(TexturePaths[i], false);
            SetupTextureImporter(NormalPaths[i], true);
            SetupTextureImporter(GlossPaths[i], false);
        }
        AssetDatabase.Refresh();

        // 2. Setup Materials with authentic wet liquid reflection and normal mapping
        Material[] mats = new Material[3];
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        for (int i = 0; i < 3; i++)
        {
            mats[i] = SetupMaterial(MatPaths[i], TexturePaths[i], NormalPaths[i], GlossPaths[i], urpLit);
        }

        // 3. Setup Prefabs
        GameObject[] prefabs = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            prefabs[i] = SetupPrefab(PrefabPaths[i], ModelPaths[i], mats[i], i + 1);
        }
        AssetDatabase.SaveAssets();

        // 4. Spawn into Scene (Preserving user-placed positions!)
        SpawnStainsInScene(prefabs, focusSceneView);
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private static void SetupTextureImporter(string path, bool isNormal)
    {
        if (!File.Exists(path)) return;

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (isNormal)
            {
                if (importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    changed = true;
                }
            }
            else
            {
                if (importer.textureType != TextureImporterType.Default)
                {
                    importer.textureType = TextureImporterType.Default;
                    changed = true;
                }
                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    changed = true;
                }
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }
            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                changed = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static Material SetupMaterial(string matPath, string texPath, string normPath, string glossPath, Shader shader)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        Texture2D norm = AssetDatabase.LoadAssetAtPath<Texture2D>(normPath);
        Texture2D gloss = AssetDatabase.LoadAssetAtPath<Texture2D>(glossPath);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (mat == null)
        {
            mat = new Material(shader);
            mat.name = Path.GetFileNameWithoutExtension(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            mat.shader = shader;
        }

        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
        }

        if (norm != null)
        {
            mat.SetTexture("_BumpMap", norm);
            mat.SetFloat("_BumpScale", 1.25f);
            mat.EnableKeyword("_NORMALMAP");
        }

        if (gloss != null)
        {
            mat.SetTexture("_MetallicGlossMap", gloss);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        mat.SetColor("_BaseColor", Color.white);
        mat.SetColor("_Color", Color.white);

        // Configure true Alpha Blend
        mat.SetFloat("_Surface", 1); // 1 = Transparent
        mat.SetFloat("_Blend", 0);   // 0 = Alpha blend
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);

        // Smoothness & Wet Reflection settings:
        // Smoothness is driven by _MetallicGlossMap's Alpha channel (0.95 inside puddle, 0 outside on transparent floor)
        mat.SetFloat("_SmoothnessTextureChannel", 0); // 0 = MetallicAlpha
        mat.SetFloat("_Smoothness", 1.0f);            // 1.0 multiplier
        mat.SetFloat("_Metallic", 0.0f);              // Non-metallic liquid
        mat.SetFloat("_SpecularHighlights", 1.0f);    // Enable wet highlights on the liquid surface
        mat.SetFloat("_EnvironmentReflections", 1.0f); // Enable ambient reflections on the liquid surface
        mat.SetFloat("_BlendModePreserveSpecular", 0); // Never add specular to transparent pixels!
        mat.SetFloat("_ReceiveShadows", 0);           // Prevent rectangular shadow on quad

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetOverrideTag("RenderType", "Transparent");

        // Two-sided rendering
        mat.SetFloat("_Cull", 0);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static GameObject SetupPrefab(string prefabPath, string modelPath, Material mat, int index)
    {
        Mesh mesh = null;
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        if (allAssets != null)
        {
            for (int a = 0; a < allAssets.Length; a++)
            {
                if (allAssets[a] is Mesh m)
                {
                    mesh = m;
                    break;
                }
            }
        }

        if (mesh == null)
        {
            GameObject modelGo = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelGo != null)
            {
                MeshFilter mf = modelGo.GetComponentInChildren<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
        }

        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));

        MeshFilter rootMf = root.AddComponent<MeshFilter>();
        rootMf.sharedMesh = mesh;

        MeshRenderer rootMr = root.AddComponent<MeshRenderer>();
        rootMr.sharedMaterial = mat;
        rootMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rootMr.receiveShadows = false;

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0f, 0.06f, 0f);
        col.size = new Vector3(1.3f, 0.20f, 1.3f);

        FloorStain stainScript = root.AddComponent<FloorStain>();
        stainScript.stainIndex = index;
        stainScript.stainRenderer = rootMr;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        return prefab;
    }

    private static void SpawnStainsInScene(GameObject[] prefabs, bool focusSceneView)
    {
        // CRITICAL SCENE OBJECT PROTECTION:
        // NEVER touch, move, modify, or destroy Beverage_Service_Counter or any props on it!

        bool sceneDirty = false;
        GameObject lastSpawned = null;

        for (int i = 0; i < 3; i++)
        {
            string stainName = StainNames[i];
            GameObject existing = GameObject.Find(stainName);

            // If stain already exists in the scene, DO NOT MOVE IT (preserve user placement)!
            // Update its renderer settings to ensure shadows & materials are clean.
            if (existing != null)
            {
                FloorStain stain = existing.GetComponent<FloorStain>();
                if (stain == null)
                {
                    stain = existing.AddComponent<FloorStain>();
                    stain.stainIndex = i + 1;
                }
                MeshRenderer mr = existing.GetComponentInChildren<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    if (prefabs[i] != null)
                    {
                        var pMr = prefabs[i].GetComponent<MeshRenderer>();
                        if (pMr != null && pMr.sharedMaterial != null)
                        {
                            mr.sharedMaterial = pMr.sharedMaterial;
                        }
                    }
                    EditorUtility.SetDirty(mr);
                }

                MeshFilter mf = existing.GetComponentInChildren<MeshFilter>();
                if (mf != null && prefabs[i] != null)
                {
                    var pMf = prefabs[i].GetComponent<MeshFilter>();
                    if (pMf != null && pMf.sharedMesh != null)
                    {
                        mf.sharedMesh = pMf.sharedMesh;
                        EditorUtility.SetDirty(mf);
                    }
                }
                stain.stainRenderer = mr;
                EditorUtility.SetDirty(stain);
                EditorUtility.SetDirty(existing);
                lastSpawned = existing;
                sceneDirty = true;
                continue;
            }

            // Spawn fresh instance from prefab
            GameObject newStain = null;
            if (prefabs[i] != null)
            {
                newStain = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
            }
            else
            {
                newStain = new GameObject(stainName);
                newStain.AddComponent<FloorStain>().stainIndex = i + 1;
            }

            newStain.name = stainName;
            newStain.transform.position = DefaultPositions[i];
            newStain.transform.rotation = DefaultRotations[i];
            newStain.transform.localScale = Vector3.one;

            Undo.RegisterCreatedObjectUndo(newStain, "Spawn " + stainName);
            lastSpawned = newStain;
            sceneDirty = true;
        }

        if (sceneDirty)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[SetupFloorStains] 3 Floor Stains spawned successfully in the scene! They can be moved freely around the floor.");
        }

        if (focusSceneView && lastSpawned != null)
        {
            Selection.activeGameObject = lastSpawned;
            SceneView.FrameLastActiveSceneView();
        }
    }
}

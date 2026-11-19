using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// [InitializeOnLoad]
public static class SetupGrassTerrains
{
    private const string SetupKey = "SetupGrassTerrains_V2_Mountains_Done";
    private const string TerrainDataDir = "Assets/TerrainData";
    private const string TexturesDir = "Assets/Textures/Terrain";

    private const string GrassAlbedoPath = "Assets/Textures/Terrain/grass_albedo.png";
    private const string GrassNormalPath = "Assets/Textures/Terrain/grass_normal.png";
    private const string GrassLayerPath = "Assets/Textures/Terrain/Grass_TerrainLayer.terrainlayer";

    private const string RockAlbedoPath = "Assets/Textures/Terrain/rock_albedo.png";
    private const string RockNormalPath = "Assets/Textures/Terrain/rock_normal.png";
    private const string RockLayerPath = "Assets/Textures/Terrain/Rock_TerrainLayer.terrainlayer";

    // Compact terrain size: 60m x 60m per tile (down from 150m x 150m)
    private const float TerrainWidth = 60f;
    private const float TerrainLength = 60f;
    private const float TerrainHeight = 35f;
    private const float BaseY = -0.05f;

    private const int HeightmapRes = 257;
    private const int AlphamapRes = 256;

    // Disabled auto-setup so mountains are not re-created
    // static SetupGrassTerrains()
    // {
    //     EditorApplication.delayCall += AutoSetupOnce;
    // }

    private static void AutoSetupOnce()
    {
        if (!SessionState.GetBool(SetupKey, false))
        {
            SessionState.SetBool(SetupKey, true);
            Execute();
        }
    }

    [MenuItem("Tools/Gas Station/Create 3 Grass Terrains (with Mountains)")]
    public static void Execute()
    {
        // CRITICAL SCENE OBJECT PROTECTION:
        // NEVER touch, move, modify, or destroy Beverage_Service_Counter or any props on it!

        EnsureFolder(TerrainDataDir);
        EnsureFolder(TexturesDir);
        AssetDatabase.Refresh();

        // 1. Setup Texture Importers
        SetupTextureImporter(GrassAlbedoPath, false);
        SetupTextureImporter(GrassNormalPath, true);
        SetupTextureImporter(RockAlbedoPath, false);
        SetupTextureImporter(RockNormalPath, true);
        AssetDatabase.Refresh();

        // 2. Setup TerrainLayers (Grass & Mountain Rock)
        TerrainLayer grassLayer = GetOrCreateLayer(GrassLayerPath, "Grass_TerrainLayer", GrassAlbedoPath, GrassNormalPath, 5f, 0.08f);
        TerrainLayer rockLayer = GetOrCreateLayer(RockLayerPath, "Rock_TerrainLayer", RockAlbedoPath, RockNormalPath, 6f, 0.12f);
        TerrainLayer[] layers = new TerrainLayer[] { grassLayer, rockLayer };

        // 3. World bounds for the 3 compact terrains:
        // Spanning total X from -90 to +90 (180m total), Z from -30 to +30 (60m total)
        Vector3[] positions = new Vector3[]
        {
            new Vector3(-90f, BaseY, -30f), // Terrain_1 (West Ridge)
            new Vector3(-30f, BaseY, -30f), // Terrain_2 (Center / North Mountain behind store)
            new Vector3(30f, BaseY, -30f)   // Terrain_3 (East Ridge)
        };

        // 4. Create or load 3 TerrainData assets with mountain heightmaps and rock blends
        TerrainData[] tData = new TerrainData[3];
        for (int i = 0; i < 3; i++)
        {
            string tDataPath = $"{TerrainDataDir}/TerrainData_{i + 1}.asset";
            tData[i] = AssetDatabase.LoadAssetAtPath<TerrainData>(tDataPath);
            if (tData[i] == null)
            {
                tData[i] = new TerrainData();
                tData[i].heightmapResolution = HeightmapRes;
                tData[i].size = new Vector3(TerrainWidth, TerrainHeight, TerrainLength);
                AssetDatabase.CreateAsset(tData[i], tDataPath);
            }
            else
            {
                tData[i].heightmapResolution = HeightmapRes;
                tData[i].size = new Vector3(TerrainWidth, TerrainHeight, TerrainLength);
            }

            tData[i].terrainLayers = layers;

            // Generate sculpted mountain heights
            GenerateMountainHeightmap(tData[i], positions[i]);

            // Generate texture splatmaps (grass on valley/hills, rock cliffs on steep slopes)
            GenerateSplatmap(tData[i]);

            EditorUtility.SetDirty(tData[i]);
        }
        AssetDatabase.SaveAssets();

        // 5. Update or spawn the 3 Terrains in the Scene
        Terrain[] terrains = new Terrain[3];
        bool sceneModified = false;

        for (int i = 0; i < 3; i++)
        {
            string terrainName = $"Terrain_{i + 1}";
            GameObject go = GameObject.Find(terrainName);

            if (go == null)
            {
                go = Terrain.CreateTerrainGameObject(tData[i]);
                go.name = terrainName;
                go.transform.position = positions[i];
                Undo.RegisterCreatedObjectUndo(go, $"Create {terrainName}");
                sceneModified = true;
            }
            else
            {
                go.transform.position = positions[i];

                Terrain t = go.GetComponent<Terrain>();
                if (t != null && t.terrainData != tData[i])
                {
                    t.terrainData = tData[i];
                    EditorUtility.SetDirty(t);
                    sceneModified = true;
                }
                TerrainCollider tc = go.GetComponent<TerrainCollider>();
                if (tc != null && tc.terrainData != tData[i])
                {
                    tc.terrainData = tData[i];
                    EditorUtility.SetDirty(tc);
                    sceneModified = true;
                }
            }

            Terrain terrainComp = go.GetComponent<Terrain>();
            if (terrainComp != null)
            {
                terrainComp.drawInstanced = true;
                terrainComp.heightmapPixelError = 4f;
                terrainComp.basemapDistance = 300f;
                terrainComp.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                EditorUtility.SetDirty(terrainComp);
            }

            terrains[i] = terrainComp;
        }

        // 6. Connect neighbors for seamless sculpting across all 3 terrains
        if (terrains[0] != null && terrains[1] != null && terrains[2] != null)
        {
            terrains[0].SetNeighbors(null, null, terrains[1], null);
            terrains[1].SetNeighbors(terrains[0], null, terrains[2], null);
            terrains[2].SetNeighbors(terrains[1], null, null, null);
        }

        if (sceneModified)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        Debug.Log("[SetupGrassTerrains] 3 Compact Grass Terrains with mountain ridges created successfully! (60m x 60m each, seamless mountain ranges surrounding the gas station).");
    }

    private static void GenerateMountainHeightmap(TerrainData tData, Vector3 origin)
    {
        int res = tData.heightmapResolution;
        float[,] heights = new float[res, res];

        for (int z = 0; z < res; z++)
        {
            float normZ = (float)z / (res - 1);
            float worldZ = origin.z + normZ * TerrainLength;

            for (int x = 0; x < res; x++)
            {
                float normX = (float)x / (res - 1);
                float worldX = origin.x + normX * TerrainWidth;

                heights[z, x] = CalculateWorldHeight(worldX, worldZ);
            }
        }

        tData.SetHeights(0, 0, heights);
    }

    private static float CalculateWorldHeight(float wx, float wz)
    {
        // Gas station valley floor is flat near wx: [-18, 18], wz: [-20, 8]
        float dx = Mathf.Max(0.0f, Mathf.Abs(wx) - 18.0f);
        float dzBack = Mathf.Max(0.0f, wz - 8.0f);

        // Combined distance into mountain rise
        float dist = Mathf.Sqrt(dx * dx + (dzBack * 1.5f) * (dzBack * 1.5f));

        if (dist <= 0.001f)
        {
            return 0.0f; // Completely flat around the gas station lot and road
        }

        // Smooth elevation ramp
        float ramp = Mathf.Clamp01((dist - 2.0f) / 25.0f);
        ramp = ramp * ramp * (3.0f - 2.0f * ramp); // Smoothstep curve

        // Natural continuous mountain ridgelines
        float ridge1 = Mathf.Sin(wx * 0.07f + 1.2f) * Mathf.Cos(wz * 0.08f + 0.5f);
        float ridge2 = Mathf.Sin(wx * 0.15f - wz * 0.12f) * 0.5f;
        float ridge3 = Mathf.Cos((wx + wz) * 0.22f) * 0.25f;

        float mountainShape = 0.55f + 0.30f * ridge1 + 0.15f * ridge2 + 0.08f * ridge3;
        mountainShape = Mathf.Pow(Mathf.Max(0.1f, mountainShape), 1.3f);

        // Prominent mountain peak directly behind the gas station
        float dxPeak = wx - 0.0f;
        float dzPeak = wz - 28.0f;
        float peak = Mathf.Exp(-(dxPeak * dxPeak / 400.0f + dzPeak * dzPeak / 200.0f)) * 0.40f;

        float totalH = ramp * mountainShape + peak * ramp;
        return Mathf.Clamp01(totalH);
    }

    private static void GenerateSplatmap(TerrainData tData)
    {
        int alphaRes = tData.alphamapResolution;
        float[,,] alphaMaps = new float[alphaRes, alphaRes, 2];

        int hRes = tData.heightmapResolution;
        float[,] heights = tData.GetHeights(0, 0, hRes, hRes);

        for (int z = 0; z < alphaRes; z++)
        {
            float normZ = (float)z / (alphaRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(normZ * (hRes - 1)), 0, hRes - 1);

            for (int x = 0; x < alphaRes; x++)
            {
                float normX = (float)x / (alphaRes - 1);
                int hx = Mathf.Clamp(Mathf.RoundToInt(normX * (hRes - 1)), 0, hRes - 1);

                // Calculate slope gradient
                int hxPrev = Mathf.Max(0, hx - 1);
                int hxNext = Mathf.Min(hRes - 1, hx + 1);
                int hzPrev = Mathf.Max(0, hz - 1);
                int hzNext = Mathf.Min(hRes - 1, hz + 1);

                float dhdx = (heights[hz, hxNext] - heights[hz, hxPrev]) * TerrainHeight / (2.0f * (TerrainWidth / (hRes - 1)));
                float dhdz = (heights[hzNext, hx] - heights[hzPrev, hx]) * TerrainHeight / (2.0f * (TerrainLength / (hRes - 1)));
                float slope = Mathf.Sqrt(dhdx * dhdx + dhdz * dhdz);

                // If slope > 0.5 (steep slope / cliffs), blend in mountain rock
                float rockWeight = Mathf.Clamp01((slope - 0.45f) / 0.40f);
                float grassWeight = 1.0f - rockWeight;

                alphaMaps[z, x, 0] = grassWeight;
                alphaMaps[z, x, 1] = rockWeight;
            }
        }

        tData.SetAlphamaps(0, 0, alphaMaps);
    }

    private static TerrainLayer GetOrCreateLayer(string path, string name, string albedoPath, string normPath, float tileSize, float smoothness)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normPath);

        if (layer == null)
        {
            layer = new TerrainLayer();
            layer.name = name;
            layer.diffuseTexture = albedo;
            layer.normalMapTexture = normal;
            layer.tileSize = new Vector2(tileSize, tileSize);
            layer.smoothness = smoothness;
            layer.metallic = 0.0f;
            layer.normalScale = 1.0f;
            AssetDatabase.CreateAsset(layer, path);
        }
        else
        {
            if (albedo != null) layer.diffuseTexture = albedo;
            if (normal != null) layer.normalMapTexture = normal;
            layer.tileSize = new Vector2(tileSize, tileSize);
            layer.smoothness = smoothness;
            layer.metallic = 0.0f;
            EditorUtility.SetDirty(layer);
        }

        return layer;
    }

    private static void EnsureFolder(string path)
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
            TextureImporterType targetType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != targetType)
            {
                importer.textureType = targetType;
                changed = true;
            }
            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }
            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }
}

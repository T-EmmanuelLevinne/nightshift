using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class FixGasStation
{
    private const string FbxPath = "Assets/small-red-gas-station/source/stacja.fbx";
    private const string MaterialsFolder = "Assets/small-red-gas-station/Materials";
    private const string TexturesFolder = "Assets/small-red-gas-station/textures";

    [MenuItem("Tools/Fix Gas Station (Two-Sided & Textures & Glass & Doors)")]
    public static void Execute()
    {
        Debug.Log("[FixGasStation] Starting Gas Station fix (materials, two-sided, textures, glass, doors)...");

        if (!Directory.Exists(MaterialsFolder))
        {
            Directory.CreateDirectory(MaterialsFolder);
            AssetDatabase.Refresh();
        }

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        var textureMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "95fuel", "95fuel.png" },
            { "fuel", "95+fuel.png" },
            { "disel", "disel.png" },
            { "LPG", "LPG.png" },
            { "ac", "ac.jpg" },
            { "ac2", "ac2.jpg" },
            { "floor", "Tiles082_2K_Color.jpg" },
            { "toilet doors", "toilet_doors.jpg" },
            { "milesad", "milesad.png" },
            { "Material.029", "Bez_nazwy.png" },
            { "Material.008", "supermarket-g75415e3d2_1920.jpg" },
            { "Material.018", "supermarket-g75415e3d2_1920.jpg" },
            { "Material.002", "franki-chamaki-wkvKZR4e2OI-unsplash.jpg" },
            { "Material.006", "franki-chamaki-wkvKZR4e2OI-unsplash.jpg" },
            { "Material.007", "franki-chamaki-wkvKZR4e2OI-unsplash.jpg" },
            { "Material.014", "franki-chamaki-wkvKZR4e2OI-unsplash.jpg" },
            { "Material.016", "franki-chamaki-wkvKZR4e2OI-unsplash.jpg" },
            { "Material.017", "franki-chamaki-wkvKZR4e2OI-unsplash.jpg" },
            { "Material.003", "chuttersnap-ktdmaxlkKtI-unsplash.jpg" },
            { "Material.011", "chuttersnap-ktdmaxlkKtI-unsplash.jpg" },
            { "Material.013", "chuttersnap-ktdmaxlkKtI-unsplash.jpg" },
            { "Material.028", "chuttersnap-ktdmaxlkKtI-unsplash.jpg" },
            { "Material.030", "chuttersnap-ktdmaxlkKtI-unsplash.jpg" },
            { "Material.015", "phil-aicken-xATMkkPIL24-unsplash.jpg" },
            { "Material.019", "phil-aicken-xATMkkPIL24-unsplash.jpg" }
        };

        var colorMap = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "asphalt", new Color(0.233f, 0.233f, 0.233f) },
            { "walls", new Color(0.801f, 0.057f, 0.038f) },
            { "gray walls", new Color(0.514f, 0.514f, 0.514f) },
            { "obudowa", new Color(0.587f, 0.587f, 0.587f) },
            { "obudowa.001", new Color(0.587f, 0.587f, 0.587f) },
            { "concrete", new Color(0.34f, 0.34f, 0.34f) },
            { "barierki", new Color(0.802f, 0.019f, 0.0f) },
            { "czarny mat", new Color(0.116f, 0.116f, 0.116f) },
            { "chrome", new Color(0.7f, 0.7f, 0.7f) },
            { "Material.001", new Color(0.73f, 0.8f, 0.223f) },
            { "Material.022", new Color(0.053f, 0.053f, 0.053f) },
            { "Material.023", new Color(0.038f, 0.038f, 0.038f) },
            { "Material.024", new Color(0.214f, 0.214f, 0.214f) },
            { "Material.025", new Color(0.074f, 0.074f, 0.074f) },
            { "Material.026", new Color(0.285f, 0.288f, 0.132f) },
            { "Material.027", new Color(0.8f, 0.057f, 0.068f) },
            { "Material.033", new Color(0.102f, 0.222f, 0.458f) },
            { "Material.034", new Color(0.161f, 0.151f, 0.037f) },
            { "Material.035", new Color(0.273f, 0.8f, 0.251f) },
            { "Material.037", new Color(0.302f, 0.302f, 0.302f) },
            { "Material.040", new Color(0.295f, 0.295f, 0.295f) },
            { "Material.041", new Color(0.549f, 0.335f, 0.161f) },
            { "glowin", new Color(0.95f, 0.95f, 0.9f) },
            { "glowin.001", new Color(0.95f, 0.95f, 0.9f) }
        };

        var createdMaterials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        foreach (var sub in subAssets)
        {
            if (sub is Material internalMat)
            {
                string matName = internalMat.name;
                string matAssetPath = $"{MaterialsFolder}/{matName}.mat";
                Material targetMat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);

                bool isNew = (targetMat == null);
                if (isNew)
                {
                    targetMat = new Material(litShader);
                    targetMat.name = matName;
                }

                // 1. Force Double-Sided (Fixes see-through roof and single-sided walls)
                targetMat.SetFloat("_Cull", 0f);
                targetMat.doubleSidedGI = true;

                // Check for Glass (Material.032 = storefront windows, Material.031 = cooler display doors)
                bool isGlass = matName.Equals("Material.032", StringComparison.OrdinalIgnoreCase) ||
                               matName.Equals("Material.031", StringComparison.OrdinalIgnoreCase);

                if (isGlass)
                {
                    bool isStorefrontGlass = matName.Equals("Material.032", StringComparison.OrdinalIgnoreCase);
                    targetMat.SetFloat("_Surface", 1f); // Transparent
                    targetMat.SetFloat("_Blend", 0f);   // Alpha
                    targetMat.SetFloat("_BlendModePreserveSpecular", 0f);
                    targetMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    targetMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    targetMat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                    targetMat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    targetMat.SetFloat("_ZWrite", 0f);
                    targetMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    targetMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    targetMat.renderQueue = (int)RenderQueue.Transparent;
                    targetMat.SetOverrideTag("RenderType", "Transparent");

                    if (isStorefrontGlass)
                    {
                        targetMat.SetFloat("_Cull", 2f); // Single-pass back-face culling avoids double frosted haze on Cube panes
                        targetMat.SetColor("_BaseColor", new Color(0.70f, 0.84f, 0.96f, 0.035f));
                        targetMat.SetColor("_Color", new Color(0.70f, 0.84f, 0.96f, 0.035f));
                        targetMat.SetFloat("_Smoothness", 0.25f);
                        targetMat.SetFloat("_Metallic", 0f);
                        targetMat.SetFloat("_EnvironmentReflections", 0f);
                        targetMat.SetFloat("_SpecularHighlights", 0f);
                        targetMat.SetFloat("_ReceiveShadows", 0f);
                        targetMat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                        targetMat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                        targetMat.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                    }
                    else
                    {
                        targetMat.SetColor("_BaseColor", new Color(0.82f, 0.92f, 1f, 0.25f));
                        targetMat.SetColor("_Color", new Color(0.82f, 0.92f, 1f, 0.25f));
                        targetMat.SetFloat("_Smoothness", 0.95f);
                        targetMat.SetFloat("_Metallic", 0f);
                    }
                }
                else
                {
                    targetMat.SetFloat("_Surface", 0f); // Opaque
                    targetMat.SetFloat("_ZWrite", 1f);
                    targetMat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    targetMat.renderQueue = (int)RenderQueue.Geometry;
                    targetMat.SetOverrideTag("RenderType", "Opaque");

                    // 2. Base Color & Textures
                    if (textureMap.TryGetValue(matName, out string texFileName))
                    {
                        string texPath = $"{TexturesFolder}/{texFileName}";
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                        if (tex != null)
                        {
                            targetMat.SetTexture("_BaseMap", tex);
                            targetMat.SetTexture("_MainTex", tex);
                            targetMat.SetColor("_BaseColor", Color.white);
                            targetMat.SetColor("_Color", Color.white);
                        }
                    }
                    else if (colorMap.TryGetValue(matName, out Color col))
                    {
                        targetMat.SetColor("_BaseColor", col);
                        targetMat.SetColor("_Color", col);
                    }
                    else
                    {
                        Color internalCol = internalMat.HasProperty("_BaseColor") ? internalMat.GetColor("_BaseColor") : (internalMat.HasProperty("_Color") ? internalMat.color : Color.white);
                        targetMat.SetColor("_BaseColor", internalCol);
                        targetMat.SetColor("_Color", internalCol);
                    }

                    // 3. Material specific tweaks
                    if (matName.Equals("chrome", StringComparison.OrdinalIgnoreCase))
                    {
                        targetMat.SetFloat("_Metallic", 0.9f);
                        targetMat.SetFloat("_Smoothness", 0.85f);
                    }
                    else if (matName.StartsWith("glowin", StringComparison.OrdinalIgnoreCase))
                    {
                        targetMat.EnableKeyword("_EMISSION");
                        targetMat.SetColor("_EmissionColor", new Color(1.5f, 1.5f, 1.4f));
                    }
                    else if (matName.Equals("floor", StringComparison.OrdinalIgnoreCase))
                    {
                        targetMat.SetFloat("_Smoothness", 0.4f);
                    }
                    else
                    {
                        targetMat.SetFloat("_Smoothness", 0.3f);
                    }
                }

                if (isNew)
                {
                    AssetDatabase.CreateAsset(targetMat, matAssetPath);
                }
                else
                {
                    EditorUtility.SetDirty(targetMat);
                }

                createdMaterials[matName] = targetMat;
            }
        }

        // Remap ModelImporter
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer != null)
        {
            foreach (var sub in subAssets)
            {
                if (sub is Material internalMat && createdMaterials.TryGetValue(internalMat.name, out Material targetMat))
                {
                    var id = new AssetImporter.SourceAssetIdentifier(internalMat);
                    importer.AddRemap(id, targetMat);
                    var nameId = new AssetImporter.SourceAssetIdentifier(typeof(Material), internalMat.name);
                    importer.AddRemap(nameId, targetMat);
                }
            }

            AssetDatabase.WriteImportSettingsIfDirty(FbxPath);
            importer.SaveAndReimport();
        }

        // Update Scene instances
        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = activeScene.GetRootGameObjects();

        foreach (var root in rootObjects)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in renderers)
            {
                Material[] sharedMats = mr.sharedMaterials;
                bool modified = false;

                for (int i = 0; i < sharedMats.Length; i++)
                {
                    if (sharedMats[i] != null && createdMaterials.TryGetValue(sharedMats[i].name, out Material targetMat))
                    {
                        sharedMats[i] = targetMat;
                        modified = true;
                    }
                }

                if (modified)
                {
                    mr.sharedMaterials = sharedMats;
                    EditorUtility.SetDirty(mr);
                }
            }
        }

        // Create the physical door and clear the doorway opening
        CreateEntranceDoors();

        if (!EditorApplication.isPlaying && !Application.isPlaying)
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[FixGasStation] Gas station fix and door installation completed successfully!");
    }

    [MenuItem("Tools/Put Entrance Doors on Gas Station")]
    public static void CreateEntranceDoors()
    {
        var stacja = GameObject.Find("stacja");
        if (stacja == null)
        {
            Debug.LogWarning("[FixGasStation] 'stacja' GameObject not found in active scene.");
            return;
        }

        Transform plane1 = null;
        foreach (Transform t in stacja.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Plane.001") { plane1 = t; break; }
        }

        if (plane1 == null)
        {
            Debug.LogWarning("[FixGasStation] Plane.001 not found inside stacja.");
            return;
        }

        // 1. Clear the static glass doorway in Plane.001
        MeshFilter mf = plane1.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Mesh origMesh = mf.sharedMesh;
            Mesh mesh = UnityEngine.Object.Instantiate(origMesh);
            mesh.name = "Plane.001_DoorwayOpen";

            if (mesh.subMeshCount > 2)
            {
                int[] glassTris = mesh.GetTriangles(2);
                Vector3[] verts = mesh.vertices;
                List<int> newGlassTris = new List<int>();

                for (int i = 0; i < glassTris.Length; i += 3)
                {
                    Vector3 center = (verts[glassTris[i]] + verts[glassTris[i + 1]] + verts[glassTris[i + 2]]) / 3f;
                    // Doorway glass faces are in Y [6.7, 9.5] and Z <= 0.76 in Plane.001 local mesh coords
                    bool isDoorwayGlass = (center.y >= 6.7f && center.y <= 9.5f && center.z <= 0.76f);
                    if (!isDoorwayGlass)
                    {
                        newGlassTris.Add(glassTris[i]);
                        newGlassTris.Add(glassTris[i + 1]);
                        newGlassTris.Add(glassTris[i + 2]);
                    }
                }

                mesh.SetTriangles(newGlassTris.ToArray(), 2);
                mf.sharedMesh = mesh;
                EditorUtility.SetDirty(plane1.gameObject);
                Debug.Log("[FixGasStation] Cleared static glass faces from doorway opening.");
            }
        }

        // 2. Remove existing doors if already present
        Transform existing = stacja.transform.Find("Store_Entrance_Doors");
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        // 3. Calculate Doorway placement in World Space
        Vector3 localDoorCenter = new Vector3(4.46f, 8.115f, -0.385f);
        Vector3 worldCenter = plane1.TransformPoint(localDoorCenter);
        Vector3 dirNormal = plane1.TransformDirection(Vector3.right);
        Vector3 dirHeight = plane1.TransformDirection(Vector3.forward);
        Quaternion worldRot = Quaternion.LookRotation(dirNormal, dirHeight);

        // Materials
        Material frameMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/czarny mat.mat");
        Material glassMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Material.032.mat");
        Material handleMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/chrome.mat");

        // 4. Create Root Doors GameObject
        GameObject doorsRoot = new GameObject("Store_Entrance_Doors");
        doorsRoot.transform.SetParent(stacja.transform, true);
        doorsRoot.transform.position = worldCenter;
        doorsRoot.transform.rotation = worldRot;
        doorsRoot.transform.localScale = Vector3.one;

        // Header Track Beam
        GameObject header = GameObject.CreatePrimitive(PrimitiveType.Cube);
        header.name = "Door_Header_Track";
        header.transform.SetParent(doorsRoot.transform, false);
        header.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        header.transform.localRotation = Quaternion.identity;
        header.transform.localScale = new Vector3(2.80f, 0.10f, 0.12f);
        if (frameMat != null) header.GetComponent<MeshRenderer>().sharedMaterial = frameMat;

        // Motion Sensor Box
        GameObject sensor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sensor.name = "Motion_Sensor";
        sensor.transform.SetParent(header.transform, false);
        sensor.transform.localPosition = new Vector3(0f, 0.2f, 0.7f);
        sensor.transform.localScale = new Vector3(0.12f, 0.6f, 0.5f);
        if (handleMat != null) sensor.GetComponent<MeshRenderer>().sharedMaterial = handleMat;

        // Left Sliding Door
        GameObject leftDoor = CreateSlidingDoorPanel("Left_Sliding_Door", doorsRoot.transform, new Vector3(-0.68f, 0f, 0f), frameMat, glassMat, handleMat, true);

        // Right Sliding Door
        GameObject rightDoor = CreateSlidingDoorPanel("Right_Sliding_Door", doorsRoot.transform, new Vector3(0.68f, 0f, 0f), frameMat, glassMat, handleMat, false);

        // Automatic Sliding Door Controller
        var controller = doorsRoot.AddComponent<AutomaticSlidingDoors>();
        controller.leftDoor = leftDoor.transform;
        controller.rightDoor = rightDoor.transform;
        controller.openDistance = 1.15f;
        controller.isOpen = true; // Started open so the user/player can walk right in!
        controller.autoTrigger = true;

        // Trigger Collider for Auto Opening
        BoxCollider trigger = doorsRoot.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0f, 0f);
        trigger.size = new Vector3(3.2f, 2.4f, 4.0f);

        EditorUtility.SetDirty(doorsRoot);
        Debug.Log("[FixGasStation] Successfully created Store_Entrance_Doors at entrance!");
    }

    private static GameObject CreateSlidingDoorPanel(string name, Transform parent, Vector3 localPos, Material frameMat, Material glassMat, Material handleMat, bool isLeft)
    {
        GameObject panelRoot = new GameObject(name);
        panelRoot.transform.SetParent(parent, false);
        panelRoot.transform.localPosition = localPos;
        panelRoot.transform.localRotation = Quaternion.identity;
        panelRoot.transform.localScale = Vector3.one;

        float width = 1.32f;
        float height = 2.16f;
        float thickness = 0.045f;
        float frameThick = 0.07f;

        // Top frame
        CreateBox("Frame_Top", panelRoot.transform, new Vector3(0f, (height - frameThick) / 2f, 0f), new Vector3(width, frameThick, thickness), frameMat);
        // Bottom frame
        CreateBox("Frame_Bottom", panelRoot.transform, new Vector3(0f, -(height - frameThick) / 2f, 0f), new Vector3(width, frameThick, thickness), frameMat);
        // Left frame
        CreateBox("Frame_Left", panelRoot.transform, new Vector3(-(width - frameThick) / 2f, 0f, 0f), new Vector3(frameThick, height, thickness), frameMat);
        // Right frame
        CreateBox("Frame_Right", panelRoot.transform, new Vector3((width - frameThick) / 2f, 0f, 0f), new Vector3(frameThick, height, thickness), frameMat);

        // Glass Pane
        CreateBox("Glass_Pane", panelRoot.transform, Vector3.zero, new Vector3(width - frameThick * 2f, height - frameThick * 2f, 0.015f), glassMat);

        // Handle
        float handleX = isLeft ? (width / 2f - 0.15f) : -(width / 2f - 0.15f);
        // Exterior Handle
        CreateBox("Handle_Exterior", panelRoot.transform, new Vector3(handleX, 0f, 0.045f), new Vector3(0.035f, 0.90f, 0.035f), handleMat);
        // Interior Handle
        CreateBox("Handle_Interior", panelRoot.transform, new Vector3(handleX, 0f, -0.045f), new Vector3(0.035f, 0.90f, 0.035f), handleMat);

        return panelRoot;
    }

    private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = localScale;
        if (mat != null) box.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return box;
    }
}
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SetupMidnightAtmosphere
{
    private const string SkyboxMatAssetPath = "Assets/Resources/MidnightHorrorSkybox.mat";

    static SetupMidnightAtmosphere()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += () =>
        {
            EnsureMidnightSkyboxMaterialAsset();
            ApplyMidnightToActiveSceneInEditor(markDirty: true);
        };
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += () =>
        {
            EnsureMidnightSkyboxMaterialAsset();
            ApplyMidnightToActiveSceneInEditor(markDirty: false);
        };
    }

    private static void EnsureMidnightSkyboxMaterialAsset()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Shader skyShader = Shader.Find("Skybox/MidnightHorrorSkybox");
        if (skyShader == null) return;

        if (!Directory.Exists("Assets/Resources"))
        {
            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.Refresh();
        }

        Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMatAssetPath);
        if (existingMat == null)
        {
            Material mat = new Material(skyShader)
            {
                name = "MidnightHorrorSkybox"
            };
            mat.SetColor("_ZenithColor", new Color(0.007f, 0.012f, 0.026f, 1f));
            mat.SetColor("_HorizonColor", new Color(0.020f, 0.029f, 0.050f, 1f));
            mat.SetColor("_GroundColor", new Color(0.008f, 0.011f, 0.018f, 1f));
            mat.SetColor("_MoonColor", new Color(0.84f, 0.90f, 1.00f, 1f));
            mat.SetVector("_MoonDir", new Vector4(0.76f, 0.46f, 0.46f, 0f));
            mat.SetFloat("_MoonSize", 0.032f);
            mat.SetFloat("_StarIntensity", 0.90f);

            AssetDatabase.CreateAsset(mat, SkyboxMatAssetPath);
            AssetDatabase.SaveAssets();
        }
        else if (existingMat.shader != skyShader)
        {
            existingMat.shader = skyShader;
            EditorUtility.SetDirty(existingMat);
            AssetDatabase.SaveAssets();
        }
    }

    [MenuItem("Tools/Night Shift/Apply Midnight Atmosphere")]
    public static void MenuApplyMidnight()
    {
        EnsureMidnightSkyboxMaterialAsset();
        ApplyMidnightToActiveSceneInEditor(markDirty: true);
        Debug.Log("[SetupMidnightAtmosphere] Midnight time atmosphere applied to active scene.");
    }

    private static void ApplyMidnightToActiveSceneInEditor(bool markDirty)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded) return;

        MidnightAtmosphereController controller = MidnightAtmosphereController.EnsureExistsAndApply();
        if (controller != null && !EditorApplication.isPlaying && markDirty)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }

        SceneView.RepaintAll();
    }
}


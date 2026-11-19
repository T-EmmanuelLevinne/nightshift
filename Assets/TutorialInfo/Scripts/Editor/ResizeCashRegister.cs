using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ResizeCashRegister
{
    private const string AssetPath = "Assets/cash-register/source/cash_register/cash_register/kasa_fbx.fbx";
    private const string KeyDone = "ResizeCashRegister_V1_Done";

    static ResizeCashRegister()
    {
        EditorApplication.delayCall += AutoExecuteOnce;
    }

    private static void AutoExecuteOnce()
    {
        if (!SessionState.GetBool(KeyDone, false))
        {
            SessionState.SetBool(KeyDone, true);
            Execute();
        }
    }

    [MenuItem("Tools/Gas Station/Resize Cash Register to Normal Size")]
    public static void Execute()
    {
        // 1. Set FBX ModelImporter scale factor to 0.01
        // The original FBX was modeled in centimeters (46.1cm x 55.3cm x 32.6cm).
        // Importing with globalScale = 0.01 converts it to accurate real-world meters (0.46m x 0.55m x 0.33m).
        ModelImporter importer = AssetImporter.GetAtPath(AssetPath) as ModelImporter;
        if (importer != null)
        {
            if (Mathf.Abs(importer.globalScale - 0.01f) > 0.0001f)
            {
                importer.globalScale = 0.01f;
                importer.SaveAndReimport();
                Debug.Log($"[ResizeCashRegister] Set ModelImporter.globalScale to 0.01 for {AssetPath}");
            }
        }

        // 2. Check scene instance(s)
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        bool sceneModified = false;

        foreach (GameObject root in rootObjects)
        {
            // CRITICAL SCENE OBJECT PROTECTION:
            // NEVER touch or modify Beverage_Service_Counter
            if (root.name.Contains("Beverage_Service_Counter")) continue;

            CheckAndResetScale(root.transform, ref sceneModified);
        }

        if (sceneModified)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        // Verify and log final bounds
        GameObject go = GameObject.Find("kasa_fbx");
        if (go != null)
        {
            Bounds b = CalculateTotalBounds(go);
            Debug.Log($"[ResizeCashRegister] Cash register 'kasa_fbx' resized successfully! Normal size: {b.size.x * 100f:F1} cm (W) x {b.size.y * 100f:F1} cm (H) x {b.size.z * 100f:F1} cm (D) [{b.size.x:F3}m x {b.size.y:F3}m x {b.size.z:F3}m]");
        }
    }

    private static void CheckAndResetScale(Transform t, ref bool modified)
    {
        if (t.name == "kasa_fbx")
        {
            // If scale was previously set to something else or double-scaled, reset to Vector3.one
            if (t.localScale != Vector3.one)
            {
                Undo.RecordObject(t, "Reset Cash Register Scale to 1");
                t.localScale = Vector3.one;
                modified = true;
            }
        }

        for (int i = 0; i < t.childCount; i++)
        {
            CheckAndResetScale(t.GetChild(i), ref modified);
        }
    }

    private static Bounds CalculateTotalBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            b.Encapsulate(renderers[i].bounds);
        }
        return b;
    }
}


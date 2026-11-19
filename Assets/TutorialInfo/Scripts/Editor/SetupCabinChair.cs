using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor setup utility for PFB_DiningChairUserInteract in HouseScene.
/// Ensures the chair has CabinChairInteractable, BoxCollider (trigger), and isStatic = false.
/// </summary>
[InitializeOnLoad]
public static class SetupCabinChair
{
    private const string SessionKey = "CabinChair_Configured_V1";

    static SetupCabinChair()
    {
        EditorApplication.delayCall += AutoRunOnLoad;
        EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            if (string.Equals(scene.name, "HouseScene", System.StringComparison.OrdinalIgnoreCase))
            {
                EditorApplication.delayCall += () => ConfigureDiningChair(markDirty: true);
            }
        };
    }

    private static void AutoRunOnLoad()
    {
        if (!SessionState.GetBool(SessionKey, false))
        {
            SessionState.SetBool(SessionKey, true);
            ConfigureDiningChair(markDirty: true);
        }
    }

    [MenuItem("Tools/House Scene/Setup Seating (Dining Chair & Sofa)")]
    public static void SetupDiningChairMenu()
    {
        ConfigureDiningChair(markDirty: true);
        ConfigureSofaSmall(markDirty: true);
    }

    public static void ConfigureDiningChair(bool markDirty = true)
    {
        Scene houseScene = SceneManager.GetSceneByName("HouseScene");
        if (!houseScene.IsValid() || !houseScene.isLoaded)
        {
            return;
        }

        GameObject chairObj = GameObject.Find("PFB_DiningChairUserInteract");
        if (chairObj == null)
        {
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < allObjects.Length; i++)
            {
                if (allObjects[i].name.IndexOf("DiningChairUserInteract", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    chairObj = allObjects[i];
                    break;
                }
            }
        }

        if (chairObj == null)
        {
            Debug.LogWarning("[SetupCabinChair] PFB_DiningChairUserInteract not found in scene!");
            return;
        }

        Undo.RecordObject(chairObj, "Configure Dining Chair");
        chairObj.isStatic = false;

        CabinChairInteractable cci = chairObj.GetComponent<CabinChairInteractable>();
        if (cci == null)
        {
            cci = Undo.AddComponent<CabinChairInteractable>(chairObj);
        }

        BoxCollider box = chairObj.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = Undo.AddComponent<BoxCollider>(chairObj);
        }

        MeshFilter mf = chairObj.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            box.center = b.center;
            box.size = new Vector3(
                Mathf.Max(b.size.x, 0.48f),
                Mathf.Max(b.size.y, 0.95f),
                Mathf.Max(b.size.z, 0.48f)
            );
        }
        else
        {
            box.center = new Vector3(0f, 0.48f, 0f);
            box.size = new Vector3(0.50f, 0.96f, 0.50f);
        }
        box.isTrigger = true;

        if (markDirty && !EditorApplication.isPlaying)
        {
            EditorUtility.SetDirty(chairObj);
            EditorSceneManager.MarkSceneDirty(houseScene);
        }

        Debug.Log("[SetupCabinChair] Successfully configured PFB_DiningChairUserInteract with CabinChairInteractable.");

        ConfigureSofaSmall(markDirty);
    }

    public static void ConfigureSofaSmall(bool markDirty = true)
    {
        Scene houseScene = SceneManager.GetSceneByName("HouseScene");
        if (!houseScene.IsValid() || !houseScene.isLoaded) return;

        GameObject sofaObj = GameObject.Find("PFB_SofaSmall");
        if (sofaObj == null)
        {
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < allObjects.Length; i++)
            {
                if (allObjects[i].name.Equals("PFB_SofaSmall", System.StringComparison.OrdinalIgnoreCase))
                {
                    sofaObj = allObjects[i];
                    break;
                }
            }
        }

        if (sofaObj == null) return;

        Undo.RecordObject(sofaObj, "Configure SofaSmall");
        sofaObj.isStatic = false;

        CabinChairInteractable cci = sofaObj.GetComponent<CabinChairInteractable>();
        if (cci == null)
        {
            cci = Undo.AddComponent<CabinChairInteractable>(sofaObj);
        }

        cci.requiresMoveFirst = false;
        cci.standUpInFront = true;
        cci.sittingEyeHeight = 1.08f;

        BoxCollider box = sofaObj.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = Undo.AddComponent<BoxCollider>(sofaObj);
        }

        MeshFilter mf = sofaObj.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            box.center = b.center;
            box.size = new Vector3(
                Mathf.Max(b.size.x, 0.70f),
                Mathf.Max(b.size.y, 0.85f),
                Mathf.Max(b.size.z, 0.70f)
            );
        }
        else
        {
            box.center = new Vector3(0f, 0.42f, 0f);
            box.size = new Vector3(0.85f, 0.85f, 0.85f);
        }
        box.isTrigger = true;

        if (markDirty && !EditorApplication.isPlaying)
        {
            EditorUtility.SetDirty(sofaObj);
            EditorSceneManager.MarkSceneDirty(houseScene);
        }

        Debug.Log("[SetupCabinChair] Successfully configured PFB_SofaSmall with CabinChairInteractable.");
    }
}

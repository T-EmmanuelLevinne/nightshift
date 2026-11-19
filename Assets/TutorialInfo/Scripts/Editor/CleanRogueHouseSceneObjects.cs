using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CleanRogueHouseSceneObjects
{
    static CleanRogueHouseSceneObjects()
    {
        EditorApplication.delayCall += CleanRogueObjectsIfHouseScene;
    }

    [MenuItem("Tools/House Scene/Clean Rogue Gas Station Objects from HouseScene")]
    public static void CleanRogueObjectsIfHouseScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, "HouseScene", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string[] rogueNames = new string[]
        {
            "UserBoxPutter",
            "Cardboard_Box",
            "Floor_Stain_1",
            "Floor_Stain_2",
            "Floor_Stain_3"
        };

        bool cleanedAny = false;
        for (int i = 0; i < rogueNames.Length; i++)
        {
            GameObject go = GameObject.Find(rogueNames[i]);
            if (go != null)
            {
                Undo.DestroyObjectImmediate(go);
                cleanedAny = true;
                Debug.Log($"[CleanRogueHouseSceneObjects] Removed rogue gas station object '{rogueNames[i]}' from HouseScene.");
            }
        }

        if (cleanedAny)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }
    }
}


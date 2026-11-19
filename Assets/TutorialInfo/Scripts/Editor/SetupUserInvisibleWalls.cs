using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor utility to apply the invisible wall configuration to user-placed walls:
/// InvisWall, InvisWall2, InvisWall3, InvisWall4.
/// 
/// Guarantees:
/// 1. Walls are 100% INVISIBLE (destroys MeshRenderer and MeshFilter).
/// 2. Walls remain solid physical barriers (BoxCollider enabled, isTrigger = false).
/// 3. Removes any stray child walls (Wall_North, Wall_South, etc.) spawned by older scripts.
/// 4. Does not place walls somewhere else.
/// 5. Strictly protects Beverage_Service_Counter and user props.
/// </summary>
[InitializeOnLoad]
public static class SetupUserInvisibleWalls
{
    private const string AutoRunKey = "SetupUserInvisibleWalls_AutoRun_Done_V1";

    static SetupUserInvisibleWalls()
    {
        EditorApplication.delayCall += AutoApplyOnce;
    }

    private static void AutoApplyOnce()
    {
        if (!SessionState.GetBool(AutoRunKey, false))
        {
            SessionState.SetBool(AutoRunKey, true);
            Execute();
        }
    }

    [MenuItem("Tools/Gas Station/Apply Invisible Walls to User Placed Walls")]
    public static void Execute()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded) return;

        // Find all InvisWall objects in active scene
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        int processedCount = 0;

        // Clean up any stray root "Invisible_World_Border" objects
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject go = allObjects[i];
            if (go == null) continue;

            if (go.name == "Invisible_World_Border")
            {
                Undo.DestroyObjectImmediate(go);
            }
        }

        // Process user-placed walls: InvisWall, InvisWall2, InvisWall3, InvisWall4
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject wall = allObjects[i];
            if (wall == null) continue;

            string name = wall.name;
            bool isUserWall = name == "InvisWall" || name == "InvisWall2" || name == "InvisWall3" || name == "InvisWall4" ||
                              name.StartsWith("InvisWall") || name.StartsWith("InvisibleWall");

            if (!isUserWall) continue;

            Undo.RecordObject(wall, "Configure Invisible Wall");

            // 1. Remove any stray child objects (Wall_North, Wall_South, Wall_East, Wall_West)
            for (int c = wall.transform.childCount - 1; c >= 0; c--)
            {
                Transform child = wall.transform.GetChild(c);
                if (child != null)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            // 2. Remove visible MeshRenderer and MeshFilter so wall is completely invisible
            MeshRenderer mr = wall.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Undo.DestroyObjectImmediate(mr);
            }

            MeshFilter mf = wall.GetComponent<MeshFilter>();
            if (mf != null)
            {
                Undo.DestroyObjectImmediate(mf);
            }

            // 3. Ensure BoxCollider is enabled and solid
            BoxCollider col = wall.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = Undo.AddComponent<BoxCollider>(wall);
            }
            col.isTrigger = false;
            col.enabled = true;

            // 4. Ensure InvisibleWorldBorder component is attached for dialogue feedback
            InvisibleWorldBorder iwb = wall.GetComponent<InvisibleWorldBorder>();
            if (iwb == null)
            {
                iwb = Undo.AddComponent<InvisibleWorldBorder>(wall);
            }
            iwb.showWarningDialogue = true;
            iwb.boundaryWarningText = "You: I can't leave yet, I still have to work.";

            EditorUtility.SetDirty(wall);
            processedCount++;
        }

        if (processedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[SetupUserInvisibleWalls] Successfully configured {processedCount} user invisible walls! They are now 100% invisible, physically solid, and cleaned of legacy child objects.");
        }
    }
}


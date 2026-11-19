using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// [InitializeOnLoad]
public static class FixGroundCollision
{
    static FixGroundCollision()
    {
        // EditorApplication.delayCall += ApplyGroundFix;
    }

    [MenuItem("Tools/Gas Station/Fix Ground Collision (Solid BoxCollider)")]
    public static void ApplyGroundFix()
    {
        GameObject stacja = GameObject.Find("stacja");
        if (stacja == null)
        {
            Debug.LogWarning("[FixGroundCollision] 'stacja' not found in active scene.");
            return;
        }

        Transform ground = null;
        foreach (Transform t in stacja.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Ground" || t.name == "Plane")
            {
                ground = t;
                break;
            }
        }

        if (ground == null)
        {
            Debug.LogWarning("[FixGroundCollision] Ground/Plane not found under stacja.");
            return;
        }

        // 1. Remove or disable paper-thin 2D MeshCollider which causes tunneling / backface pass-through
        MeshCollider mc = ground.GetComponent<MeshCollider>();
        if (mc != null)
        {
            mc.enabled = false;
        }

        // 2. Add or configure a solid 3D BoxCollider with 2-meter downward depth
        // The FBX Ground Plane in stacja has local scale [100, 100, 100] and local rotation [-90, 0, 0]
        // In Plane local space:
        // Width (X): ~41.32m -> local X size ~0.4132
        // Length (Y in FBX): ~36.18m -> local Y size ~0.3618
        // Downward depth: in FBX with rot -90 around X, local -Z extends DOWNWARD into the earth in world space!
        BoxCollider bc = ground.GetComponent<BoxCollider>();
        if (bc == null)
        {
            bc = ground.gameObject.AddComponent<BoxCollider>();
        }

        bc.enabled = true;
        bc.isTrigger = false;

        // In Ground local space (scale is 100 on X, Y, Z):
        // X = 0.4132 (41.32 meters wide)
        // Y = 0.3618 (36.18 meters long)
        // Z thickness = 0.02 (2.0 meters deep downward)
        // Z center = -0.01 (-1.0 meter center, so top surface aligns exactly at Z=0 / Y=0 in world space)
        bc.size = new Vector3(0.4132f, 0.3618f, 0.02f);
        bc.center = new Vector3(0f, 0f, -0.01f);

        EditorUtility.SetDirty(ground.gameObject);

        // 3. Also verify Test_FPS_Player ground clearance
        GameObject player = GameObject.Find("Test_FPS_Player");
        if (player != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.stepOffset = 0.35f;
                cc.skinWidth = 0.05f;
            }
            EditorUtility.SetDirty(player);
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!EditorApplication.isPlaying && !Application.isPlaying)
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log("[FixGroundCollision] Successfully fixed Ground! Replaced paper-thin 2D mesh collider with a solid 2-meter deep 3D BoxCollider. Player will no longer fall through!");
    }
}

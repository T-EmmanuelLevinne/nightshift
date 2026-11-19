using System.IO;
using UnityEditor;
using UnityEngine;

// [InitializeOnLoad]
public static class InspectSceneAllSixCans
{
    static InspectSceneAllSixCans()
    {
        // EditorApplication.delayCall += InspectSceneCans;
    }

    public static void InspectSceneCans()
    {
        GameObject go = GameObject.Find("All Six Soda Cans");
        if (go == null)
        {
            Debug.LogWarning("All Six Soda Cans not found in scene");
            return;
        }

        string outPath = Path.Combine(Application.dataPath, "../scene_cans.txt");
        using (StreamWriter sw = new StreamWriter(outPath))
        {
            sw.WriteLine($"Root: {go.name}, Pos: {go.transform.position}, Rot: {go.transform.eulerAngles}");
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
            {
                if (child != go.transform)
                {
                    sw.WriteLine($"  Child: '{child.name}', LocalPos: {child.localPosition}, LocalRot: {child.localEulerAngles}, WorldRot: {child.eulerAngles}, Scale: {child.localScale}");
                }
            }
        }
    }
}

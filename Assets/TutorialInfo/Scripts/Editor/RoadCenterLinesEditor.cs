using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoadCenterLines))]
public class RoadCenterLinesCustomEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RoadCenterLines script = (RoadCenterLines)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Dashed White"))
        {
            Undo.RecordObject(script, "Set Dashed White");
            script.style = RoadCenterLines.LineStyle.DashedWhite;
            script.RebuildLines();
            EditorUtility.SetDirty(script);
        }
        if (GUILayout.Button("Dashed Yellow"))
        {
            Undo.RecordObject(script, "Set Dashed Yellow");
            script.style = RoadCenterLines.LineStyle.DashedYellow;
            script.RebuildLines();
            EditorUtility.SetDirty(script);
        }
        if (GUILayout.Button("Double Yellow"))
        {
            Undo.RecordObject(script, "Set Double Yellow");
            script.style = RoadCenterLines.LineStyle.DoubleSolidYellow;
            script.RebuildLines();
            EditorUtility.SetDirty(script);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        if (GUILayout.Button("Rebuild Lines Now", GUILayout.Height(30)))
        {
            Undo.RecordObject(script, "Rebuild Road Lines");
            script.RebuildLines();
            EditorUtility.SetDirty(script);
        }
    }
}

public static class RoadCenterLinesMenu
{
    [MenuItem("Tools/Gas Station/Attach Center Lines To Road")]
    public static void AttachToRoad()
    {
        GameObject target = Selection.activeGameObject;

        if (target == null || target.GetComponent<MeshFilter>() == null)
        {
            target = GameObject.Find("Road");
            if (target == null)
            {
                GameObject stacja = GameObject.Find("stacja");
                if (stacja != null)
                {
                    Transform t = stacja.transform.Find("Road");
                    if (t != null) target = t.gameObject;
                }
            }
        }

        if (target == null)
        {
            EditorUtility.DisplayDialog("Road Not Found", "Could not find a GameObject named 'Road' with a MeshFilter. Please select your Road object in the Hierarchy and run this tool again.", "OK");
            return;
        }

        RoadCenterLines rcl = target.GetComponent<RoadCenterLines>();
        if (rcl == null)
        {
            rcl = Undo.AddComponent<RoadCenterLines>(target);
        }

        rcl.RebuildLines();
        EditorUtility.SetDirty(target);
        Selection.activeGameObject = target;
        Debug.Log($"[RoadCenterLines] Successfully attached and generated center road lines on '{target.name}'!");
    }
}

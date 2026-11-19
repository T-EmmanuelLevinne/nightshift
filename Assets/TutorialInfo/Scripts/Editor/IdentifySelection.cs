    using UnityEditor;
using UnityEngine;

// [InitializeOnLoad]
public static class IdentifySelection
{
    static IdentifySelection()
    {
        // EditorApplication.delayCall += ReportSelection;
        // Selection.selectionChanged += ReportSelection;
    }

    [MenuItem("Tools/Debug/Report Selected Object")]
    public static void ReportSelection()
    {
        if (Selection.activeGameObject != null)
        {
            GameObject go = Selection.activeGameObject;
            Debug.Log($"[SELECTION_REPORT] Name: '{go.name}', Path: '{GetFullPath(go)}', Pos: {go.transform.position}, LocalPos: {go.transform.localPosition}, Scale: {go.transform.localScale}");
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp != null)
                {
                    Debug.Log($"[SELECTION_REPORT] Component: {comp.GetType().Name}");
                    if (comp is BoxCollider bc)
                    {
                        Debug.Log($"[SELECTION_REPORT] BoxCollider: center={bc.center}, size={bc.size}, isTrigger={bc.isTrigger}");
                    }
                    if (comp is MeshCollider mc)
                    {
                        Debug.Log($"[SELECTION_REPORT] MeshCollider: mesh={(mc.sharedMesh != null ? mc.sharedMesh.name : "null")}, isTrigger={mc.isTrigger}");
                    }
                }
            }
        }
        else
        {
            Debug.Log("[SELECTION_REPORT] No active GameObject selected.");
        }
    }

    private static string GetFullPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UnpackShelvesTool
{
    private const string ModelPath = "Assets/shelves-pack/source/1695/model.dae";

    static UnpackShelvesTool()
    {
        EditorApplication.delayCall += AutoScaleModelMetaOnce;
    }

    private static void AutoScaleModelMetaOnce()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer != null && Mathf.Approximately(importer.globalScale, 1f))
        {
            importer.globalScale = 0.04f;
            importer.SaveAndReimport();
            Debug.Log("[ShelvesScale] Updated model.dae globalScale to 0.04 for realistic store sizing.");
        }
    }

    [MenuItem("Tools/Gas Station/Resize Shelves To Fit Store (Human Scale 1.8m)")]
    public static void ResizeShelvesInScene()
    {
        // 1. Update importer globalScale
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer != null)
        {
            importer.globalScale = 0.04f;
            importer.SaveAndReimport();
        }

        // 2. Find any shelf objects in the active scene
        List<GameObject> targets = new List<GameObject>();

        GameObject model = GameObject.Find("model");
        if (model != null) targets.Add(model);

        GameObject rootShelves = GameObject.Find("Convenience_Store_Shelves");
        if (rootShelves != null) targets.Add(rootShelves);

        // Also search for individual separated shelves
        string[] names = new string[] { "Snack_Cart_With_Wheels", "Wire_Snack_Stand", "Large_Snack_Display_Shelf" };
        foreach (string n in names)
        {
            GameObject obj = GameObject.Find(n);
            if (obj != null && !targets.Contains(obj)) targets.Add(obj);
        }

        if (targets.Count == 0)
        {
            if (Selection.activeGameObject != null)
            {
                targets.Add(Selection.activeGameObject);
            }
        }

        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("Not Found", "Could not find 'model' or shelves in the scene. Please select your shelf object in the Hierarchy and run this tool again.", "OK");
            return;
        }

        int count = 0;
        foreach (GameObject t in targets)
        {
            Undo.RecordObject(t.transform, "Resize Shelves To Store Scale");
            
            // Check current renderer height to scale to ~1.8m
            Renderer[] rends = t.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds();
            bool hasB = false;
            foreach (var r in rends)
            {
                if (!hasB) { b = r.bounds; hasB = true; }
                else b.Encapsulate(r.bounds);
            }

            if (hasB && b.size.y > 5.0f)
            {
                // It's still gigantic (e.g. 45m tall)
                float factor = 1.8f / b.size.y;
                t.transform.localScale *= factor;
            }
            else if (t.transform.localScale == Vector3.one)
            {
                t.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
            }

            // Ground it so feet touch Y=0
            Vector3 pos = t.transform.position;
            pos.y = 0f;
            t.transform.position = pos;

            EditorUtility.SetDirty(t);
            count++;
        }

        Debug.Log($"[ShelvesScale] Resized {count} shelf object(s) to realistic store scale (height ~1.8m)!");
        EditorUtility.DisplayDialog("Shelves Resized", 
            $"Successfully resized shelves to human store scale (~1.8m height)!\n\n" +
            "They will now fit comfortably through the doors and under the store ceiling.", "OK");
    }

    [MenuItem("Tools/Gas Station/Unpack Shelves Prefab")]
    public static void UnpackOnly()
    {
        GameObject target = Selection.activeGameObject;
        if (target == null) target = GameObject.Find("model");

        if (target == null)
        {
            EditorUtility.DisplayDialog("Not Found", "Please select 'model' in the Hierarchy.", "OK");
            return;
        }

        if (PrefabUtility.IsPartOfPrefabInstance(target))
        {
            Undo.RegisterFullObjectHierarchyUndo(target, "Unpack Shelves Prefab");
            PrefabUtility.UnpackPrefabInstance(target, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            Debug.Log($"[UnpackShelves] Successfully unpacked '{target.name}'!");
            EditorUtility.DisplayDialog("Unpacked Successfully", $"'{target.name}' is now fully unpacked! You can freely move and edit all parts in the Hierarchy.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Already Unpacked", $"'{target.name}' is already a standard GameObject (not a prefab instance).", "OK");
        }
    }

    [MenuItem("Tools/Gas Station/Unpack & Separate Into 3 Standalone Shelves")]
    public static void UnpackAndSeparate()
    {
        GameObject model = Selection.activeGameObject;
        if (model == null) model = GameObject.Find("model");

        if (model == null)
        {
            EditorUtility.DisplayDialog("Not Found", "Please select 'model' in the Hierarchy.", "OK");
            return;
        }

        // 1. Unpack first if it's a prefab instance
        if (PrefabUtility.IsPartOfPrefabInstance(model))
        {
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.UserAction);
        }

        Transform group0 = model.transform.Find("group_0");
        if (group0 == null) group0 = model.transform;

        int totalChildren = group0.childCount;
        if (totalChildren == 0)
        {
            EditorUtility.DisplayDialog("Empty", "No child objects found in 'group_0'.", "OK");
            return;
        }

        List<Transform> allChildren = new List<Transform>();
        for (int i = 0; i < totalChildren; i++)
        {
            allChildren.Add(group0.GetChild(i));
        }

        Vector3 minPos = Vector3.positiveInfinity;
        Vector3 maxPos = Vector3.negativeInfinity;

        foreach (Transform t in allChildren)
        {
            Renderer r = t.GetComponent<Renderer>();
            Vector3 center = r != null ? r.bounds.center : t.position;
            minPos = Vector3.Min(minPos, center);
            maxPos = Vector3.Max(maxPos, center);
        }

        Vector3 span = maxPos - minPos;
        int spreadAxis = 0; // 0=X, 1=Y, 2=Z
        if (span.y > span.x && span.y > span.z) spreadAxis = 1;
        else if (span.z > span.x && span.z > span.y) spreadAxis = 2;

        allChildren.Sort((a, b) =>
        {
            Renderer ra = a.GetComponent<Renderer>();
            Renderer rb = b.GetComponent<Renderer>();
            float ca = ra != null ? ra.bounds.center[spreadAxis] : a.position[spreadAxis];
            float cb = rb != null ? rb.bounds.center[spreadAxis] : b.position[spreadAxis];
            return ca.CompareTo(cb);
        });

        float totalSpan = span[spreadAxis];
        float split1 = minPos[spreadAxis] + totalSpan * 0.35f;
        float split2 = minPos[spreadAxis] + totalSpan * 0.65f;

        List<Transform> groupLeft = new List<Transform>();
        List<Transform> groupMid = new List<Transform>();
        List<Transform> groupRight = new List<Transform>();

        foreach (Transform t in allChildren)
        {
            Renderer r = t.GetComponent<Renderer>();
            float val = r != null ? r.bounds.center[spreadAxis] : t.position[spreadAxis];
            if (val < split1) groupLeft.Add(t);
            else if (val < split2) groupMid.Add(t);
            else groupRight.Add(t);
        }

        GameObject rootShelves = new GameObject("Convenience_Store_Shelves");
        rootShelves.transform.position = model.transform.position;
        rootShelves.transform.rotation = model.transform.rotation;
        Undo.RegisterCreatedObjectUndo(rootShelves, "Create Separated Shelves");

        CreateShelfGroup("Snack_Cart_With_Wheels", groupLeft, rootShelves.transform);
        CreateShelfGroup("Wire_Snack_Stand", groupMid, rootShelves.transform);
        CreateShelfGroup("Large_Snack_Display_Shelf", groupRight, rootShelves.transform);

        Undo.DestroyObjectImmediate(model);

        // Scale to store-fitting scale
        rootShelves.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);

        Selection.activeGameObject = rootShelves;
        EditorUtility.DisplayDialog("Separated Successfully", 
            $"Successfully separated into 3 independent shelves under 'Convenience_Store_Shelves':\n\n" +
            $"1. Snack_Cart_With_Wheels ({groupLeft.Count} parts)\n" +
            $"2. Wire_Snack_Stand ({groupMid.Count} parts)\n" +
            $"3. Large_Snack_Display_Shelf ({groupRight.Count} parts)\n\n" +
            $"Scaled to realistic human store size (~1.8m height)!\nYou can now move each shelf independently anywhere inside your store.", "OK");
    }

    private static void CreateShelfGroup(string name, List<Transform> parts, Transform parent)
    {
        if (parts.Count == 0) return;

        Bounds b = new Bounds();
        bool hasBounds = false;
        foreach (Transform t in parts)
        {
            Renderer r = t.GetComponent<Renderer>();
            if (r != null)
            {
                if (!hasBounds) { b = r.bounds; hasBounds = true; }
                else b.Encapsulate(r.bounds);
            }
        }

        Vector3 pivotPos = hasBounds ? new Vector3(b.center.x, b.min.y, b.center.z) : parts[0].position;

        GameObject shelfObj = new GameObject(name);
        shelfObj.transform.position = pivotPos;
        shelfObj.transform.SetParent(parent, true);

        foreach (Transform t in parts)
        {
            t.SetParent(shelfObj.transform, true);
        }

        Undo.RegisterCreatedObjectUndo(shelfObj, "Create " + name);
    }
}

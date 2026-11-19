using System.IO;
using UnityEditor;
using UnityEngine;

// [InitializeOnLoad]
public static class CheckCanOrientation
{
    static CheckCanOrientation()
    {
        // EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Debug/Check Can Orientation")]
    public static void Run()
    {
        string fbxPath = "Assets/soda-cans-classic-355ml-free-download/source/All Six Soda Cans.fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null) return;

        // Instantiate a temporary instance to check transformed vertices
        GameObject inst = UnityEngine.Object.Instantiate(prefab);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        string outPath = Path.Combine(Application.dataPath, "../can_orientation.txt");
        using (StreamWriter sw = new StreamWriter(outPath))
        {
            foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t.parent == inst.transform)
                {
                    MeshFilter mf = t.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        Mesh m = mf.sharedMesh;
                        Vector3[] verts = m.vertices;
                        // Transform first 10 vertices and calculate world bounds of this can
                        Bounds wb = new Bounds(t.TransformPoint(verts[0]), Vector3.zero);
                        foreach (var v in verts)
                        {
                            wb.Encapsulate(t.TransformPoint(v));
                        }
                        sw.WriteLine($"Can: {t.name}");
                        sw.WriteLine($"  World Bounds Size: {wb.size} (X={wb.size.x}, Y={wb.size.y}, Z={wb.size.z})");
                        sw.WriteLine($"  World Bounds Center: {wb.center}");
                        sw.WriteLine($"  Min={wb.min}, Max={wb.max}");
                    }
                }
            }
        }
        UnityEngine.Object.DestroyImmediate(inst);
    }
}

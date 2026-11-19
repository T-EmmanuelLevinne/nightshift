using System.IO;
using UnityEditor;
using UnityEngine;

// [InitializeOnLoad]
public static class InspectCanBounds
{
    static InspectCanBounds()
    {
        // EditorApplication.delayCall += RunInspect;
    }

    [MenuItem("Tools/Debug/Inspect Can Bounds")]
    public static void RunInspect()
    {
        string fbxPath = "Assets/soda-cans-classic-355ml-free-download/source/All Six Soda Cans.fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null)
        {
            Debug.LogError("Could not find All Six Soda Cans.fbx");
            return;
        }

        string outPath = Path.Combine(Application.dataPath, "../can_bounds.txt");
        using (StreamWriter sw = new StreamWriter(outPath))
        {
            foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (child.parent == prefab.transform)
                {
                    MeshFilter mf = child.GetComponent<MeshFilter>();
                    MeshRenderer mr = child.GetComponent<MeshRenderer>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        Bounds b = mf.sharedMesh.bounds;
                        sw.WriteLine($"Can Brand Node: '{child.name}'");
                        sw.WriteLine($"  LocalScale: {child.localScale}, LocalRot: {child.localEulerAngles}");
                        sw.WriteLine($"  Mesh '{mf.sharedMesh.name}': Vertices={mf.sharedMesh.vertexCount}, Triangles={mf.sharedMesh.triangles.Length/3}");
                        sw.WriteLine($"  Mesh Bounds: Center={b.center}, Size={b.size}, Min={b.min}, Max={b.max}");
                        
                        // Check child pulltab
                        if (child.childCount > 0)
                        {
                            Transform tab = child.GetChild(0);
                            MeshFilter tmf = tab.GetComponent<MeshFilter>();
                            if (tmf != null && tmf.sharedMesh != null)
                            {
                                Bounds tb = tmf.sharedMesh.bounds;
                                sw.WriteLine($"  PullTab Node: '{tab.name}' Vertices={tmf.sharedMesh.vertexCount}, Mesh Bounds: Center={tb.center}, Size={tb.size}");
                            }
                        }
                    }
                }
            }
        }
    }
}
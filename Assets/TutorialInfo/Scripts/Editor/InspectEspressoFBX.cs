using System.IO;
using UnityEditor;
using UnityEngine;

// [InitializeOnLoad]
public static class InspectEspressoFBX
{
    static InspectEspressoFBX()
    {
        // EditorApplication.delayCall += Inspect;
    }

    [MenuItem("Tools/Debug/Inspect Espresso FBX")]
    public static void Inspect()
    {
        string fbxPath = "Assets/Espresso Coffee Machine/Espresso Coffee Machine.fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null)
        {
            Debug.LogError($"[InspectEspressoFBX] Failed to load {fbxPath}");
            return;
        }

        using (StreamWriter sw = new StreamWriter("espresso_info.txt"))
        {
            sw.WriteLine($"Prefab: {prefab.name}");
            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                Renderer r = temp.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    var b = r.bounds;
                    sw.WriteLine($"Instantiated Bounds: Center={b.center}, Size={b.size}, Min={b.min}, Max={b.max}");
                }
                foreach (Transform t in temp.GetComponentsInChildren<Transform>(true))
                {
                    MeshFilter mf = t.GetComponent<MeshFilter>();
                    MeshRenderer mr = t.GetComponent<MeshRenderer>();
                    string meshInfo = "none";
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var b = mf.sharedMesh.bounds;
                        meshInfo = $"Mesh: '{mf.sharedMesh.name}', Bounds center={b.center}, size={b.size}, min={b.min}, max={b.max}";
                    }
                    string matInfo = "none";
                    if (mr != null)
                    {
                        matInfo = $"Mats ({mr.sharedMaterials.Length}): ";
                        foreach (var m in mr.sharedMaterials)
                        {
                            matInfo += (m != null ? m.name : "null") + ", ";
                        }
                    }
                    sw.WriteLine($"Child: '{t.name}', Pos: {t.localPosition}, Rot: {t.localEulerAngles}, Scale: {t.localScale}\n  {meshInfo}\n  {matInfo}");
                }
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }
        Debug.Log("[InspectEspressoFBX] Done writing espresso_info.txt");
    }
}

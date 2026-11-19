using System.IO;
using UnityEditor;
using UnityEngine;

// [InitializeOnLoad]
public static class CheckCanFrontVector
{
    static CheckCanFrontVector()
    {
        // EditorApplication.delayCall += Check;
    }

    public static void Check()
    {
        string fbxPath = "Assets/soda-cans-classic-355ml-free-download/source/All Six Soda Cans.fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null) return;

        GameObject inst = UnityEngine.Object.Instantiate(prefab);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        string outPath = Path.Combine(Application.dataPath, "../can_front.txt");
        using (StreamWriter sw = new StreamWriter(outPath))
        {
            Transform coke = inst.transform.Find("Coca-Cola.001");
            if (coke != null)
            {
                MeshFilter mf = coke.GetComponent<MeshFilter>();
                Mesh m = mf.sharedMesh;
                Vector3[] verts = m.vertices;
                Vector2[] uvs = m.uv;
                Vector3[] normals = m.normals;

                // In standard soda can textures:
                // U goes 0 to 1 around the cylinder.
                // The brand logo is typically around U = 0.5 (or U = 0.25 / 0.75).
                // Let's find vertices with U around 0.5 vs U around 0.0 vs U around 0.25 vs U around 0.75
                Vector3 avgAtU0 = Vector3.zero; int c0 = 0;
                Vector3 avgAtU025 = Vector3.zero; int c025 = 0;
                Vector3 avgAtU05 = Vector3.zero; int c05 = 0;
                Vector3 avgAtU075 = Vector3.zero; int c075 = 0;

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 wp = coke.TransformPoint(verts[i]);
                    // Only consider the middle cylinder body (Y between 0.03 and 0.09)
                    if (wp.y > 0.03f && wp.y < 0.09f)
                    {
                        float u = uvs[i].x;
                        if (Mathf.Abs(u - 0.5f) < 0.05f) { avgAtU05 += wp; c05++; }
                        else if (Mathf.Abs(u - 0.25f) < 0.05f) { avgAtU025 += wp; c025++; }
                        else if (Mathf.Abs(u - 0.75f) < 0.05f) { avgAtU075 += wp; c075++; }
                        else if (u < 0.05f || u > 0.95f) { avgAtU0 += wp; c0++; }
                    }
                }

                if (c0 > 0) avgAtU0 /= c0;
                if (c025 > 0) avgAtU025 /= c025;
                if (c05 > 0) avgAtU05 /= c05;
                if (c075 > 0) avgAtU075 /= c075;

                Vector3 center = new Vector3(0.04f, 0.06f, 0f);
                sw.WriteLine($"U=0.0 direction: {avgAtU0 - center}");
                sw.WriteLine($"U=0.25 direction: {avgAtU025 - center}");
                sw.WriteLine($"U=0.5 direction: {avgAtU05 - center}");
                sw.WriteLine($"U=0.75 direction: {avgAtU075 - center}");
            }
        }
        UnityEngine.Object.DestroyImmediate(inst);
    }
}

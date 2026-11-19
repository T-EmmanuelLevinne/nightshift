using System.IO;
using UnityEditor;
using UnityEngine;

// [InitializeOnLoad]
public static class FindCanLogoDirection
{
    static FindCanLogoDirection()
    {
        // EditorApplication.delayCall += Execute;
    }

    public static void Execute()
    {
        string fbxPath = "Assets/soda-cans-classic-355ml-free-download/source/All Six Soda Cans.fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null) return;

        GameObject inst = UnityEngine.Object.Instantiate(prefab);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        string outPath = Path.Combine(Application.dataPath, "../logo_direction.txt");
        using (StreamWriter sw = new StreamWriter(outPath))
        {
            Transform coke = inst.transform.Find("Coca-Cola.001");
            if (coke != null)
            {
                MeshFilter mf = coke.GetComponent<MeshFilter>();
                Mesh m = mf.sharedMesh;
                Vector3[] verts = m.vertices;
                Vector2[] uvs = m.uv;

                // Let's find vertices where U is near 0.25 (Main Coca-Cola Logo)
                // and U is near 0.90 (Nutrition Facts / Barcode)
                Vector3 logoSum = Vector3.zero;
                int logoCount = 0;
                Vector3 nutritionSum = Vector3.zero;
                int nutritionCount = 0;

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 wp = coke.TransformPoint(verts[i]);
                    // Only consider the middle cylinder body height (Y between 0.03 and 0.09)
                    if (wp.y > 0.03f && wp.y < 0.09f)
                    {
                        float u = uvs[i].x;
                        if (Mathf.Abs(u - 0.25f) < 0.05f)
                        {
                            logoSum += wp;
                            logoCount++;
                        }
                        else if (Mathf.Abs(u - 0.92f) < 0.05f)
                        {
                            nutritionSum += wp;
                            nutritionCount++;
                        }
                    }
                }

                Vector3 canCenter = new Vector3(coke.position.x, 0.06f, coke.position.z);
                Vector3 logoDir = (logoSum / logoCount) - canCenter;
                Vector3 nutritionDir = (nutritionSum / nutritionCount) - canCenter;

                sw.WriteLine($"Can Position: {coke.position}");
                sw.WriteLine($"Logo Direction (from center): X={logoDir.x:F4}, Y={logoDir.y:F4}, Z={logoDir.z:F4}");
                sw.WriteLine($"Nutrition Facts Direction: X={nutritionDir.x:F4}, Y={nutritionDir.y:F4}, Z={nutritionDir.z:F4}");
                sw.WriteLine($"Normalized Logo Dir: {logoDir.normalized}");
                sw.WriteLine($"Normalized Nutrition Dir: {nutritionDir.normalized}");
            }
        }

        UnityEngine.Object.DestroyImmediate(inst);
    }
}

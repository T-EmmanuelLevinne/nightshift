using System.IO;
using UnityEditor;
using UnityEngine;

// [InitializeOnLoad]
public static class TestCanTransform
{
    static TestCanTransform()
    {
        // EditorApplication.delayCall += Test;
    }

    public static void Test()
    {
        string fbxPath = "Assets/soda-cans-classic-355ml-free-download/source/All Six Soda Cans.fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null) return;

        Transform coke = prefab.transform.Find("Coca-Cola.001");
        if (coke == null) return;

        MeshFilter mf = coke.GetComponent<MeshFilter>();
        Mesh m = mf.sharedMesh;
        Vector3[] verts = m.vertices;
        Vector2[] uvs = m.uv;

        // In Coca-Cola texture, U=0.25 is center of main Coca-Cola logo
        // U=0.92 is nutrition facts
        // Test with Matrix4x4 using rotation Euler(270f, 0f, 0f) and scale (100, 100, 100)
        Matrix4x4 mat = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(270.02f, 0f, 0f), Vector3.one * 100f);

        Vector3 logoAvg = Vector3.zero; int lc = 0;
        Vector3 nutrAvg = Vector3.zero; int nc = 0;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 worldV = mat.MultiplyPoint3x4(verts[i]);
            if (worldV.y > 0.03f && worldV.y < 0.09f)
            {
                if (Mathf.Abs(uvs[i].x - 0.25f) < 0.05f) { logoAvg += worldV; lc++; }
                else if (Mathf.Abs(uvs[i].x - 0.92f) < 0.05f) { nutrAvg += worldV; nc++; }
            }
        }

        if (lc > 0) logoAvg /= lc;
        if (nc > 0) nutrAvg /= nc;

        string outPath = Path.Combine(Application.dataPath, "../transform_test.txt");
        using (StreamWriter sw = new StreamWriter(outPath))
        {
            sw.WriteLine($"With Euler(270.02, 0, 0):");
            sw.WriteLine($"  Logo Pos: X={logoAvg.x:F4}, Y={logoAvg.y:F4}, Z={logoAvg.z:F4}");
            sw.WriteLine($"  Nutrition Pos: X={nutrAvg.x:F4}, Y={nutrAvg.y:F4}, Z={nutrAvg.z:F4}");
            if (logoAvg.z > nutrAvg.z)
            {
                sw.WriteLine("  RESULT: LOGO FACES +Z (FORWARD TOWARDS THE GLASS DOORS)!");
            }
            else
            {
                sw.WriteLine("  RESULT: LOGO FACES -Z (BACKWARDS AWAY FROM GLASS)!");
            }
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;

// [InitializeOnLoad]
public static class InspectAllSixCansFBX
{
    static InspectAllSixCansFBX()
    {
        // EditorApplication.delayCall += Inspect;
    }

    [MenuItem("Tools/Debug/Inspect All Six Cans FBX")]
    public static void Inspect()
    {
        string fbxPath = "Assets/soda-cans-classic-355ml-free-download/source/All Six Soda Cans.fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null) return;

        using (StreamWriter sw = new StreamWriter("fbx_structure.txt"))
        {
            sw.WriteLine($"Prefab: {prefab.name}");
            foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
            {
                MeshFilter mf = child.GetComponent<MeshFilter>();
                MeshRenderer mr = child.GetComponent<MeshRenderer>();
                string mName = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "none";
                string matName = mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "none";
                sw.WriteLine($"  Name: '{child.name}', Parent: '{child.parent?.name}', Pos: {child.localPosition}, Rot: {child.localEulerAngles}, Scale: {child.localScale}, Mesh: {mName}, Mat: {matName}");
            }

            sw.WriteLine("--- Sub Assets ---");
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var a in assets)
            {
                if (a != null)
                {
                    sw.WriteLine($"  SubAsset: '{a.name}', Type: {a.GetType().Name}");
                }
            }
        }
    }
}
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// [InitializeOnLoad]
public static class MeshIDChecker
{
    static MeshIDChecker()
    {
        // EditorApplication.delayCall += CheckMeshIDs;
    }

    [MenuItem("Tools/Debug/Check Can Mesh IDs")]
    public static void CheckMeshIDs()
    {
        string[] brands = new string[] {
            "Cans_DrPepper", "Cans_Sprite", "Cans_CocaCola",
            "Cans_CocaColaZero", "Cans_Pepsi", "Cans_PepsiZero"
        };

        foreach (var b in brands)
        {
            string path = $"Assets/soda-cans-classic-355ml-free-download/source/{b}.obj";
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            Debug.Log($"[MeshIDChecker] Path: {path}, Sub-assets count: {assets.Length}");
            foreach (var a in assets)
            {
                if (a != null)
                {
                    string guid;
                    long fileId;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out guid, out fileId))
                    {
                        Debug.Log($"[MeshIDChecker] Asset: '{a.name}' Type: '{a.GetType().Name}', GUID: {guid}, LocalFileIdentifier: {fileId}");
                    }
                }
            }
        }
    }
}

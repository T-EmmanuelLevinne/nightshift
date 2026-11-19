using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CheckBoxStatus
{
    static CheckBoxStatus()
    {
        EditorApplication.delayCall += RunCheck;
    }

    private static void RunCheck()
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/cardboard-box/source/model/model/model.dae");
        if (modelAsset != null)
        {
            MeshRenderer mr = modelAsset.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                Material m = mr.sharedMaterial;
                Debug.Log($"[CheckBoxStatus] model.dae Renderer material: {(m != null ? m.name : "null")}, shader: {(m != null && m.shader != null ? m.shader.name : "null")}");
            }
        }
    }
}


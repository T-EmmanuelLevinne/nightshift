using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Separates the opaque black-alpha window glass panes in FurnishedCabin's Building mesh
/// into a dedicated non-shadow-casting child renderer with a clear URP Transparent glass material,
/// preserving the original white window frames, grid mullions, curtains, and wall colliders.
/// </summary>
public static class CabinWindowUtility
{
    public const string BuildingBaseObjectName = "PFB_Building_Base";
    public const string GlassChildObjectName = "Cabin_WindowGlass";
    public const string StructureMeshName = "Building_WindowsRemoved";
    public const string GlassMeshName = "Building_WindowGlass";
    public const string GlassMaterialName = "CabinWindowGlass_MAT";

    // In BuildingB_MAT_AlbedoTransparency.png, all 36 window glass quads (72 tris across 6 windows)
    // map exclusively to U [0.756..0.903], V [0.005..0.240]. Surrounding geometry and white frames
    // sit outside (U <= 0.730, U >= 0.923, or V >= 0.253).
    private const float GlassUvMinU = 0.742f;
    private const float GlassUvMaxU = 0.912f;
    private const float GlassUvMinV = 0.000f;
    private const float GlassUvMaxV = 0.248f;

    public static bool IsWindowGlassUv(Vector2 uv)
    {
        return uv.x >= GlassUvMinU && uv.x <= GlassUvMaxU &&
               uv.y >= GlassUvMinV && uv.y <= GlassUvMaxV;
    }

    public static bool IsWindowGlassTriangle(Vector2 uv0, Vector2 uv1, Vector2 uv2)
    {
        return IsWindowGlassUv(uv0) && IsWindowGlassUv(uv1) && IsWindowGlassUv(uv2);
    }

    public static GameObject FindBuildingBaseInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;

        Transform[] allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null || t.gameObject.scene != scene) continue;

            // Strict protection: never touch user beverage counter
            if (t.name.IndexOf("Beverage_Service_Counter", StringComparison.OrdinalIgnoreCase) >= 0) continue;

            if (string.Equals(t.name, BuildingBaseObjectName, StringComparison.Ordinal))
            {
                return t.gameObject;
            }
        }

        return null;
    }

    public static int ResolveBuildingBSubMeshIndex(Mesh sourceMesh, MeshRenderer renderer)
    {
        if (sourceMesh == null || sourceMesh.subMeshCount <= 1) return 0;

        if (renderer != null)
        {
            Material[] mats = renderer.sharedMaterials;
            int limit = Mathf.Min(mats != null ? mats.Length : 0, sourceMesh.subMeshCount);
            for (int i = 0; i < limit; i++)
            {
                Material m = mats[i];
                if (m != null && m.name.IndexOf("BuildingB", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }
        }

        return 1;
    }

    public static int CountWindowGlassTriangles(Mesh mesh, int subMeshIndex)
    {
        if (mesh == null || !mesh.isReadable) return 0;
        if (subMeshIndex < 0 || subMeshIndex >= mesh.subMeshCount) return 0;

        Vector2[] uvs = mesh.uv;
        if (uvs == null || uvs.Length == 0) return 0;

        int[] tris = mesh.GetTriangles(subMeshIndex);
        int count = 0;
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            int i0 = tris[i];
            int i1 = tris[i + 1];
            int i2 = tris[i + 2];
            if (IsWindowGlassTriangle(uvs[i0], uvs[i1], uvs[i2]))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Splits the Building mesh into structureMesh (walls, trim, white frames/mullions without glass panes)
    /// and glassMesh (the 72 extracted window glass triangles). Returns extracted triangle count (0 if already split).
    /// </summary>
    public static int SplitBuildingWindows(Mesh sourceMesh, int buildingBSubMeshIndex, out Mesh structureMesh, out Mesh glassMesh)
    {
        structureMesh = null;
        glassMesh = null;

        if (sourceMesh == null || !sourceMesh.isReadable) return 0;
        if (buildingBSubMeshIndex < 0 || buildingBSubMeshIndex >= sourceMesh.subMeshCount) return 0;

        Vector2[] uvs = sourceMesh.uv;
        if (uvs == null || uvs.Length == 0) return 0;

        int[] srcTris = sourceMesh.GetTriangles(buildingBSubMeshIndex);
        List<int> keptTris = new List<int>(srcTris.Length);
        List<int> glassTris = new List<int>(216);

        for (int i = 0; i + 2 < srcTris.Length; i += 3)
        {
            int i0 = srcTris[i];
            int i1 = srcTris[i + 1];
            int i2 = srcTris[i + 2];

            if (IsWindowGlassTriangle(uvs[i0], uvs[i1], uvs[i2]))
            {
                glassTris.Add(i0);
                glassTris.Add(i1);
                glassTris.Add(i2);
            }
            else
            {
                keptTris.Add(i0);
                keptTris.Add(i1);
                keptTris.Add(i2);
            }
        }

        if (glassTris.Count == 0) return 0;

        structureMesh = UnityEngine.Object.Instantiate(sourceMesh);
        structureMesh.name = StructureMeshName;
        structureMesh.SetTriangles(keptTris, buildingBSubMeshIndex, false);
        structureMesh.RecalculateBounds();

        glassMesh = BuildCompactedMesh(sourceMesh, glassTris, GlassMeshName);
        return glassTris.Count / 3;
    }

    private static Mesh BuildCompactedMesh(Mesh source, List<int> triIndices, string meshName)
    {
        Vector3[] srcVerts = source.vertices;
        Vector3[] srcNormals = source.normals;
        Vector2[] srcUv = source.uv;
        Vector4[] srcTangents = source.tangents;

        bool hasNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;
        bool hasUv = srcUv != null && srcUv.Length == srcVerts.Length;
        bool hasTangents = srcTangents != null && srcTangents.Length == srcVerts.Length;

        Dictionary<int, int> remap = new Dictionary<int, int>(triIndices.Count);
        List<Vector3> dstVerts = new List<Vector3>(triIndices.Count);
        List<Vector3> dstNormals = hasNormals ? new List<Vector3>(triIndices.Count) : null;
        List<Vector2> dstUv = hasUv ? new List<Vector2>(triIndices.Count) : null;
        List<Vector4> dstTangents = hasTangents ? new List<Vector4>(triIndices.Count) : null;
        List<int> dstTris = new List<int>(triIndices.Count);

        for (int i = 0; i < triIndices.Count; i++)
        {
            int oldIdx = triIndices[i];
            if (!remap.TryGetValue(oldIdx, out int newIdx))
            {
                newIdx = dstVerts.Count;
                remap[oldIdx] = newIdx;
                dstVerts.Add(srcVerts[oldIdx]);
                if (hasNormals) dstNormals.Add(srcNormals[oldIdx]);
                if (hasUv) dstUv.Add(srcUv[oldIdx]);
                if (hasTangents) dstTangents.Add(srcTangents[oldIdx]);
            }
            dstTris.Add(newIdx);
        }

        Mesh mesh = new Mesh();
        mesh.name = meshName;
        mesh.SetVertices(dstVerts);
        if (hasNormals) mesh.SetNormals(dstNormals);
        if (hasUv) mesh.SetUVs(0, dstUv);
        if (hasTangents) mesh.SetTangents(dstTangents);
        mesh.SetTriangles(dstTris, 0, true);
        if (!hasNormals) mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Material CreateTransparentGlassMaterial(Shader fallbackShader = null)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null || !shader.isSupported)
        {
            shader = fallbackShader != null ? fallbackShader : Shader.Find("Standard");
        }

        Material mat = new Material(shader);
        mat.name = GlassMaterialName;
        ApplyGlassMaterialProperties(mat);
        return mat;
    }

    private static Texture2D cachedWindowPaneSheenTex;

    public static Texture2D GetOrCreateWindowGlassSheenTexture()
    {
        if (cachedWindowPaneSheenTex != null) return cachedWindowPaneSheenTex;

        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "CabinWindowPaneSheen_TEX";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = y / (float)(size - 1);
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);

                // Clear dark see-through glass body (0.04 alpha, dark RGB so _EMISSION never fogs the pane blue)
                // with crisp diagonal silvery reflection streaks matching Act 3 glass shine
                float diag = Mathf.Repeat(u * 0.95f + v * 1.15f, 1.0f);
                float broadStreaks = Mathf.Exp(-Mathf.Pow((diag - 0.30f) / 0.068f, 2f)) * 0.16f;
                float sharpStreaks = Mathf.Exp(-Mathf.Pow((diag - 0.44f) / 0.024f, 2f)) * 0.32f;
                float secondaryStreak = Mathf.Exp(-Mathf.Pow((diag - 0.78f) / 0.034f, 2f)) * 0.17f;
                float edgeDist = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float edgeFrost = Mathf.Exp(-edgeDist * 16f) * 0.08f;

                float sheen = broadStreaks + sharpStreaks + secondaryStreak + edgeFrost;
                float alpha = Mathf.Clamp01(0.040f + sheen);
                float streakStrength = Mathf.Clamp01(sheen * 3.1f);
                Color baseCol = new Color(0.03f, 0.05f, 0.08f, alpha);
                Color highlightCol = new Color(0.88f, 0.95f, 1.00f, alpha);
                pixels[y * size + x] = Color.Lerp(baseCol, highlightCol, streakStrength);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        cachedWindowPaneSheenTex = tex;
        return tex;
    }

    public static void ApplyGlassMaterialProperties(Material mat, Texture2D customSheenTex = null)
    {
        if (mat == null) return;

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit != null && mat.shader != urpLit)
        {
            mat.shader = urpLit;
        }

        Texture2D sheenTex = customSheenTex != null ? customSheenTex : GetOrCreateWindowGlassSheenTexture();

        // Clear window glass with visible Act-3 diagonal reflection streaks and zero white PointLight glare orbs in lit Act 1
        Color glassColor = new Color(0.80f, 0.90f, 1.00f, 0.96f);
        mat.SetColor("_BaseColor", glassColor);
        mat.SetColor("_Color", glassColor);
        mat.SetTexture("_BaseMap", sheenTex);
        mat.SetTexture("_MainTex", sheenTex);
        mat.mainTextureScale = new Vector2(0.72f, 0.72f);
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTextureScale("_BaseMap", new Vector2(0.72f, 0.72f));
        }

        mat.SetFloat("_WorkflowMode", 1f);
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 0f);   // Alpha
        mat.SetFloat("_BlendModePreserveSpecular", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.25f);
        mat.SetFloat("_SpecularHighlights", 0f);
        if (mat.HasProperty("_SpecColor")) mat.SetColor("_SpecColor", Color.black);
        mat.SetFloat("_EnvironmentReflections", 0f);
        mat.SetFloat("_ReceiveShadows", 0f);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        mat.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");

        // Subtle emission masked by sheenTex highlights the crisp diagonal glass streaks across both Act 1 and Act 3
        // while keeping the glass body (alpha 0.035) clear & non-foggy
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", sheenTex);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", new Color(0.16f, 0.22f, 0.30f));

        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetShaderPassEnabled("ShadowCaster", false);
        mat.SetShaderPassEnabled("DepthOnly", false);
        mat.SetShaderPassEnabled("MotionVectors", false);
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    public static bool ConfigureGlassChildRenderer(Transform buildingBaseTransform, Mesh glassMesh, Material glassMaterial)
    {
        if (buildingBaseTransform == null) return false;

        bool changed = false;
        Transform child = buildingBaseTransform.Find(GlassChildObjectName);
        GameObject glassGo;

        if (child == null)
        {
            glassGo = new GameObject(GlassChildObjectName);
            glassGo.transform.SetParent(buildingBaseTransform, false);
            changed = true;
        }
        else
        {
            glassGo = child.gameObject;
        }

        if (glassGo.layer != buildingBaseTransform.gameObject.layer)
        {
            glassGo.layer = buildingBaseTransform.gameObject.layer;
            changed = true;
        }

        if (!glassGo.activeSelf)
        {
            glassGo.SetActive(true);
            changed = true;
        }

        if (glassGo.transform.localPosition != Vector3.zero)
        {
            glassGo.transform.localPosition = Vector3.zero;
            changed = true;
        }
        if (glassGo.transform.localRotation != Quaternion.identity)
        {
            glassGo.transform.localRotation = Quaternion.identity;
            changed = true;
        }
        if (glassGo.transform.localScale != Vector3.one)
        {
            glassGo.transform.localScale = Vector3.one;
            changed = true;
        }

        MeshFilter mf = glassGo.GetComponent<MeshFilter>();
        if (mf == null)
        {
            mf = glassGo.AddComponent<MeshFilter>();
            changed = true;
        }
        if (glassMesh != null && mf.sharedMesh != glassMesh)
        {
            mf.sharedMesh = glassMesh;
            changed = true;
        }
        if (Application.isPlaying && mf.sharedMesh != null && mf.sharedMesh.isReadable && !mf.sharedMesh.name.EndsWith("_PlanarUV"))
        {
            Mesh planarMesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
            planarMesh.name = mf.sharedMesh.name + "_PlanarUV";
            Vector3[] verts = planarMesh.vertices;
            Vector3[] norms = planarMesh.normals;
            bool hasNorms = norms != null && norms.Length == verts.Length;
            Vector2[] uvs = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = verts[i];
                float u = (hasNorms && Mathf.Abs(norms[i].x) > Mathf.Abs(norms[i].z)) ? p.z : p.x;
                uvs[i] = new Vector2(u, p.y);
            }
            planarMesh.SetUVs(0, uvs);
            mf.sharedMesh = planarMesh;
        }

        MeshRenderer mr = glassGo.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            mr = glassGo.AddComponent<MeshRenderer>();
            changed = true;
        }
        if (glassMaterial != null && mr.sharedMaterial != glassMaterial)
        {
            mr.sharedMaterial = glassMaterial;
            changed = true;
        }
        if (mr.shadowCastingMode != ShadowCastingMode.Off)
        {
            mr.shadowCastingMode = ShadowCastingMode.Off;
            changed = true;
        }
        if (mr.receiveShadows)
        {
            mr.receiveShadows = false;
            changed = true;
        }
        if (mr.lightProbeUsage != LightProbeUsage.BlendProbes)
        {
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;
            changed = true;
        }

        return changed;
    }

    public static void EnsureRuntimeCabinWindowsConfigured(Material existingGlassMaterial = null)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, "HouseScene", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        GameObject buildingBase = FindBuildingBaseInScene(activeScene);
        if (buildingBase == null) return;

        MeshFilter mf = buildingBase.GetComponent<MeshFilter>();
        MeshRenderer mr = buildingBase.GetComponent<MeshRenderer>();
        if (mf == null || mf.sharedMesh == null || mr == null) return;

        int bSubMesh = ResolveBuildingBSubMeshIndex(mf.sharedMesh, mr);
        int glassTriCount = CountWindowGlassTriangles(mf.sharedMesh, bSubMesh);

        Shader fallbackShader = null;
        if (bSubMesh >= 0 && bSubMesh < mr.sharedMaterials.Length && mr.sharedMaterials[bSubMesh] != null)
        {
            fallbackShader = mr.sharedMaterials[bSubMesh].shader;
        }

        if (glassTriCount > 0)
        {
            if (SplitBuildingWindows(mf.sharedMesh, bSubMesh, out Mesh structureMesh, out Mesh glassMesh) > 0)
            {
                mf.sharedMesh = structureMesh;
                Material glassMat = existingGlassMaterial != null
                    ? existingGlassMaterial
                    : CreateTransparentGlassMaterial(fallbackShader);
                ConfigureGlassChildRenderer(buildingBase.transform, glassMesh, glassMat);
            }
        }
        else
        {
            Transform glassChild = buildingBase.transform.Find(GlassChildObjectName);
            if (glassChild != null)
            {
                MeshRenderer glassMr = glassChild.GetComponent<MeshRenderer>();
                Material mat = existingGlassMaterial ?? (glassMr != null ? glassMr.sharedMaterial : null);
                if (mat == null)
                {
                    mat = CreateTransparentGlassMaterial(fallbackShader);
                }
                else
                {
                    ApplyGlassMaterialProperties(mat);
                }
                ConfigureGlassChildRenderer(buildingBase.transform, null, mat);
            }
        }
    }
}

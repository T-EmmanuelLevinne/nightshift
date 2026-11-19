using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to SM_Dumpster (and SM_Dumpster2).
/// Splits the top garbage lid islands from SM_Dumpster's mesh into a hinged pivot so the lid
/// physically swings open, allows opening ONLY when the player is empty-handed, and after
/// opening allows putting the stockroom Trashbag inside to complete the task.
/// </summary>
public class InteractableDumpster : MonoBehaviour, IInteractable
{
    public static InteractableDumpster PrimaryInstance { get; private set; }
    public static readonly List<InteractableDumpster> AllDumpsters = new List<InteractableDumpster>();

    [Header("Dumpster Lid State")]
    public bool isOpen = false;
    public float openAngleDeg = 105f;
    public float lidSwingSpeed = 6.2f;

    private Transform lidPivotTransform;
    private Quaternion closedLidLocalRot = Quaternion.identity;
    private Quaternion openLidLocalRot = Quaternion.Euler(-105f, 0f, 0f);

    private static Mesh s_cachedBodyMesh;
    private static Mesh s_cachedLidMesh;
    private static Vector3 s_cachedHingeLocalPos = new Vector3(0f, 0.368f, 0.957f);

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void EnsureDumpsterModelReadableInEditor()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            EnsureFbxReadable("Assets/dumpster/source/Dumpster/SM_Dumpster.FBX");
            EnsureFbxReadable("Assets/weathered-trashbag-gameready/source/Trashbag_LOW.fbx");
        };
    }

    private static void EnsureFbxReadable(string assetPath)
    {
        UnityEditor.ModelImporter importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.ModelImporter;
        if (importer != null)
        {
            Mesh loadedMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (!importer.isReadable || (loadedMesh != null && !loadedMesh.isReadable))
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }
    }
#endif

    private BoxCollider interactionTrigger;
    private Bounds worldBounds;
    private AudioSource audioSource;
    private AudioClip lidOpenClip;
    private AudioClip trashThrowClip;

    public Vector3 IndicatorWorldPosition =>
        worldBounds.size.sqrMagnitude > 0.01f
            ? worldBounds.center + Vector3.up * (worldBounds.extents.y + 0.35f)
            : transform.position + Vector3.up * 1.35f;

    public static bool IsAnyDumpsterOpen
    {
        get
        {
            for (int i = 0; i < AllDumpsters.Count; i++)
            {
                if (AllDumpsters[i] != null && AllDumpsters[i].isOpen) return true;
            }
            return false;
        }
    }

    public static InteractableDumpster EnsureAttachedToSceneDumpsters()
    {
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;

            // Only attach to root SM_Dumpster / SM_Dumpster2 objects (not internal child mesh nodes)
            string nm = t.name.Trim();
            if (nm.StartsWith("SM_Dumpster", System.StringComparison.OrdinalIgnoreCase))
            {
                InteractableDumpster comp = t.gameObject.GetComponent<InteractableDumpster>();
                if (comp == null)
                {
                    comp = t.gameObject.AddComponent<InteractableDumpster>();
                }
                if (string.Equals(nm, "SM_Dumpster", System.StringComparison.OrdinalIgnoreCase) || PrimaryInstance == null)
                {
                    PrimaryInstance = comp;
                }
            }
        }

        return PrimaryInstance;
    }

    private void Awake()
    {
        if (!AllDumpsters.Contains(this))
        {
            AllDumpsters.Add(this);
        }
        if (PrimaryInstance == null || string.Equals(gameObject.name.Trim(), "SM_Dumpster", System.StringComparison.OrdinalIgnoreCase))
        {
            PrimaryInstance = this;
        }

        ComputeDumpsterBounds();
        SetupHingedDumpsterLid();
        EnsureDumpsterColliders();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.78f;
        audioSource.volume = 0.85f;

        GenerateProceduralAudio();
    }

    private void OnDestroy()
    {
        AllDumpsters.Remove(this);
    }

    private void ComputeDumpsterBounds()
    {
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        if (rends != null && rends.Length > 0)
        {
            worldBounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null) worldBounds.Encapsulate(rends[i].bounds);
            }
        }
        else
        {
            worldBounds = new Bounds(transform.position + Vector3.up * 0.6f, new Vector3(1.4f, 1.25f, 1.0f));
        }
    }

    private void EnsureDumpsterColliders()
    {
        // Solid body collider so player/NPCs cannot walk through the dumpster
        BoxCollider[] existingCols = GetComponents<BoxCollider>();
        BoxCollider solidCol = null;
        for (int i = 0; i < existingCols.Length; i++)
        {
            if (!existingCols[i].isTrigger) solidCol = existingCols[i];
            else interactionTrigger = existingCols[i];
        }

        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);  
        Vector3 localSize = new Vector3(1.36f, 0.76f, 0.96f);

        if (solidCol == null)
        {
            solidCol = gameObject.AddComponent<BoxCollider>();
            solidCol.isTrigger = false;
            solidCol.center = localCenter;
            solidCol.size = localSize;
        }

        if (interactionTrigger == null)
        {
            interactionTrigger = gameObject.AddComponent<BoxCollider>();
        }
        interactionTrigger.isTrigger = true;
        interactionTrigger.center = localCenter;
        interactionTrigger.size = localSize;
    }

    /// <summary>
    /// Extracts the two top plastic lid islands (Island 0 and Island 1) from SM_Dumpster's
    /// mesh into a hinged child transform at the back hinge bar so the lid physically swings open!
    /// </summary>
    private void SetupHingedDumpsterLid()
    {
        Transform existingPivot = transform.Find("Dumpster_Lid_Pivot");
        if (existingPivot != null)
        {
            lidPivotTransform = existingPivot;
            return;
        }

        MeshFilter mf = GetComponentInChildren<MeshFilter>();
        MeshRenderer mr = GetComponentInChildren<MeshRenderer>();
        if (mf == null || mr == null || mf.sharedMesh == null) return;

        Mesh srcMesh = mf.sharedMesh;

#if UNITY_EDITOR
        if (!srcMesh.isReadable)
        {
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(srcMesh);
            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = "Assets/dumpster/source/Dumpster/SM_Dumpster.FBX";
            }
            EnsureFbxReadable(assetPath);
            srcMesh = mf.sharedMesh;
        }
#endif

        if (s_cachedBodyMesh == null || s_cachedLidMesh == null)
        {
            if (srcMesh != null && srcMesh.isReadable)
            {
                TrySplitDumpsterLidAndBodyMeshes(
                    srcMesh,
                    out s_cachedHingeLocalPos,
                    out s_cachedBodyMesh,
                    out s_cachedLidMesh);
            }
        }

        GameObject pivotGo = new GameObject("Dumpster_Lid_Pivot");
        pivotGo.transform.SetParent(mf.transform, false);
        pivotGo.transform.localPosition = s_cachedHingeLocalPos;
        pivotGo.transform.localRotation = Quaternion.identity;
        pivotGo.transform.localScale = Vector3.one;
        lidPivotTransform = pivotGo.transform;

        if (s_cachedBodyMesh != null && s_cachedLidMesh != null)
        {
            mf.sharedMesh = s_cachedBodyMesh;

            MeshCollider existingMc = mf.GetComponent<MeshCollider>();
            if (existingMc != null)
            {
                existingMc.sharedMesh = s_cachedBodyMesh;
            }

            MeshFilter lidMf = pivotGo.AddComponent<MeshFilter>();
            lidMf.sharedMesh = s_cachedLidMesh;
            MeshRenderer lidMr = pivotGo.AddComponent<MeshRenderer>();
            lidMr.sharedMaterials = mr.sharedMaterials;

            BoxCollider lidCol = pivotGo.AddComponent<BoxCollider>();
            lidCol.center = s_cachedLidMesh.bounds.center;
            lidCol.size = s_cachedLidMesh.bounds.size;
        }

        // Determine which rotation sign around local X lifts the front edge UP in +Z
        Vector3 sampleFrontLipOffset = new Vector3(0f, -0.72f, -0.21f);
        Vector3 rotatedPlus = Quaternion.AngleAxis(45f, Vector3.right) * sampleFrontLipOffset;
        Vector3 rotatedMinus = Quaternion.AngleAxis(-45f, Vector3.right) * sampleFrontLipOffset;
        float sign = (rotatedPlus.z > rotatedMinus.z) ? 1f : -1f;

        closedLidLocalRot = Quaternion.identity;
        openLidLocalRot = Quaternion.AngleAxis(sign * openAngleDeg, Vector3.right);
    }

    private static bool TrySplitDumpsterLidAndBodyMeshes(
        Mesh src,
        out Vector3 hingeLocalPos,
        out Mesh bodyMesh,
        out Mesh lidMesh)
    {
        bodyMesh = null;
        lidMesh = null;

        Bounds b = src.bounds;
        float sizeZ = Mathf.Max(0.001f, b.size.z);
        float sizeY = Mathf.Max(0.001f, b.size.y);
        hingeLocalPos = new Vector3(
            b.center.x,
            b.min.y + sizeY * 0.9827f,
            b.min.z + sizeZ * 0.9825f);

        Vector3[] verts = src.vertices;
        Vector3[] norms = src.normals;
        Vector2[] uvs = src.uv;
        int[] tris = src.triangles;
        int vCount = verts.Length;

        if (vCount < 100 || tris.Length < 60) return false;

        // Weld vertices by quantized 3D position so hard normal edges & UV seams on the corrugated
        // dumpster lid stay connected as the two complete 380-vertex lid islands
        float invWeldStep = 2000f / sizeZ;
        Dictionary<Vector3Int, int> quantToWeldId = new Dictionary<Vector3Int, int>(vCount);
        int[] vertToWeld = new int[vCount];
        List<Vector3> weldPositions = new List<Vector3>(vCount / 2);

        for (int i = 0; i < vCount; i++)
        {
            Vector3 p = verts[i] - b.min;
            Vector3Int qKey = new Vector3Int(
                Mathf.RoundToInt(p.x * invWeldStep),
                Mathf.RoundToInt(p.y * invWeldStep),
                Mathf.RoundToInt(p.z * invWeldStep));

            if (!quantToWeldId.TryGetValue(qKey, out int wId))
            {
                wId = weldPositions.Count;
                quantToWeldId[qKey] = wId;
                weldPositions.Add(verts[i]);
            }
            vertToWeld[i] = wId;
        }

        int weldCount = weldPositions.Count;
        List<int>[] weldAdj = new List<int>[weldCount];
        for (int i = 0; i < weldCount; i++) weldAdj[i] = new List<int>(6);

        for (int i = 0; i < tris.Length; i += 3)
        {
            int wa = vertToWeld[tris[i]];
            int wb = vertToWeld[tris[i + 1]];
            int wc = vertToWeld[tris[i + 2]];
            weldAdj[wa].Add(wb); weldAdj[wa].Add(wc);
            weldAdj[wb].Add(wa); weldAdj[wb].Add(wc);
            weldAdj[wc].Add(wa); weldAdj[wc].Add(wb);
        }

        bool[] visitedWeld = new bool[weldCount];
        bool[] isLidWeld = new bool[weldCount];
        Queue<int> queue = new Queue<int>();
        List<int> comp = new List<int>();
        int lidWeldsFound = 0;

        for (int i = 0; i < weldCount; i++)
        {
            if (visitedWeld[i]) continue;
            comp.Clear();
            queue.Clear();

            visitedWeld[i] = true;
            queue.Enqueue(i);

            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                comp.Add(u);
                Vector3 p = weldPositions[u];
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;

                List<int> neighbors = weldAdj[u];
                for (int n = 0; n < neighbors.Count; n++)
                {
                    int w = neighbors[n];
                    if (!visitedWeld[w])
                    {
                        visitedWeld[w] = true;
                        queue.Enqueue(w);
                    }
                }
            }

            float minNormZ = (minZ - b.min.z) / sizeZ;
            float minNormY = (minY - b.min.y) / sizeY;
            float maxNormY = (maxY - b.min.y) / sizeY;

            // Top garbage lids sit at the top of the bin (minNormZ > 0.71) and span front-to-back
            // (minNormY < 0.35 to maxNormY > 0.80), excluding both the bin body (minNormZ ~ 0.05)
            // and the rear hinge brackets (minNormY ~ 0.97)
            bool isLidIsland = (minNormZ > 0.71f && minNormY < 0.35f && maxNormY > 0.80f);
            if (isLidIsland)
            {
                for (int k = 0; k < comp.Count; k++)
                {
                    isLidWeld[comp[k]] = true;
                }
                lidWeldsFound += comp.Count;
            }
        }

        if (lidWeldsFound == 0) return false;

        List<int> bodyTris = new List<int>(tris.Length);
        List<int> lidTris = new List<int>(tris.Length / 2);

        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = tris[i];
            int bIdx = tris[i + 1];
            int c = tris[i + 2];
            if (isLidWeld[vertToWeld[a]] || isLidWeld[vertToWeld[bIdx]] || isLidWeld[vertToWeld[c]])
            {
                lidTris.Add(a);
                lidTris.Add(bIdx);
                lidTris.Add(c);
            }
            else
            {
                bodyTris.Add(a);
                bodyTris.Add(bIdx);
                bodyTris.Add(c);
            }
        }

        bodyMesh = new Mesh();
        bodyMesh.name = "SM_Dumpster_Body_Runtime";
        bodyMesh.vertices = verts;
        if (norms != null && norms.Length == vCount) bodyMesh.normals = norms;
        if (uvs != null && uvs.Length == vCount) bodyMesh.uv = uvs;
        bodyMesh.triangles = bodyTris.ToArray();
        bodyMesh.RecalculateBounds();

        Vector3[] lidVerts = new Vector3[vCount];
        for (int i = 0; i < vCount; i++)
        {
            lidVerts[i] = verts[i] - hingeLocalPos;
        }

        lidMesh = new Mesh();
        lidMesh.name = "SM_Dumpster_Lids_Runtime";
        lidMesh.vertices = lidVerts;
        if (norms != null && norms.Length == vCount) lidMesh.normals = norms;
        if (uvs != null && uvs.Length == vCount) lidMesh.uv = uvs;
        lidMesh.triangles = lidTris.ToArray();
        lidMesh.RecalculateBounds();

        return true;
    }

    private void Update()
    {
        if (lidPivotTransform != null)
        {
            Quaternion targetRot = isOpen ? openLidLocalRot : closedLidLocalRot;
            lidPivotTransform.localRotation = Quaternion.Slerp(
                lidPivotTransform.localRotation,
                targetRot,
                Time.deltaTime * lidSwingSpeed);
        }
    }

    /// <summary>
    /// Returns true strictly when the player's currently active hand / hotbar slot is empty.
    /// </summary>
    public static bool IsPlayerEmptyHanded()
    {
        if (InteractableTrashbag.IsHoldingTrashbagInHand) return false;

        if (EquippableMop.Instance != null && EquippableMop.Instance.IsEquipped)
        {
            if (HorrorHotbar.Instance == null || HorrorHotbar.Instance.IsSlotActive(0))
            {
                return false;
            }
        }

        if (HorrorHotbar.Instance != null && !HorrorHotbar.Instance.IsActiveSlotEmpty)
        {
            return false;
        }

        return true;
    }

    public bool IsPlayerWithinDistance(float maxDist = 2.25f)
    {
        Camera cam = HorrorInteractionReticle.CurrentPlayerCamera;
        if (cam == null) cam = HorrorInteractionReticle.GetPlayerCamera();
        if (cam == null) return false;

        Vector3 camPos = cam.transform.position;
        Vector3 closest = interactionTrigger != null
            ? interactionTrigger.ClosestPoint(camPos)
            : worldBounds.ClosestPoint(camPos);

        if (Vector3.Distance(camPos, closest) > maxDist)
        {
            return false;
        }

        // Ensure no solid wall or building geometry blocks line-of-sight between the player and the dumpster
        Vector3 targetCheck = worldBounds.center + Vector3.up * 0.2f;
        Vector3 toTarget = targetCheck - camPos;
        float distToTarget = toTarget.magnitude;
        if (distToTarget > 0.05f)
        {
            RaycastHit[] hits = Physics.RaycastAll(camPos, toTarget.normalized, distToTarget, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                if (c.GetComponentInParent<CharacterController>() != null) continue;
                if (c.GetComponentInParent<InteractableDumpster>() != null) continue;
                if (c.GetComponentInParent<InteractableTrashbag>() != null) continue;
                return false;
            }
        }

        return true;
    }

    public string GetPromptText()
    {
        if (NightShiftStoryController.IsDialogueActive) return string.Empty;
        if (!IsPlayerWithinDistance(2.25f)) return string.Empty;

        // 1. When dumpster is CLOSED: strictly require empty hands to open
        if (!isOpen)
        {
            if (IsPlayerEmptyHanded())
            {
                return "E to open";
            }
            return "Empty hands required to open (Press 2 or 3)";
        }

        // 2. When dumpster is OPEN: allow putting the trashbag inside to complete the task
        if (NightShiftStoryController.IsTrashTaskActive &&
            !NightShiftStoryController.HasDisposedTrash &&
            InteractableTrashbag.IsTrashPickedUp)
        {
            if (InteractableTrashbag.IsHoldingTrashbagInHand)
            {
                return "E to put trash inside";
            }
            return "E to put trash inside";
        }

        if (IsPlayerEmptyHanded())
        {
            return "E to close";
        }

        return string.Empty;
    }

    public void Interact()
    {
        if (NightShiftStoryController.IsDialogueActive) return;
        if (!IsPlayerWithinDistance(2.25f)) return;

        // 1. Opening when closed (strictly only when empty-handed!)
        if (!isOpen)
        {
            if (!IsPlayerEmptyHanded())
            {
                return;
            }

            isOpen = true;
            PlayLidSound(1.0f);

            if (NightShiftStoryController.Instance != null)
            {
                NightShiftStoryController.Instance.NotifyDumpsterOpened();
            }
            return;
        }

        // 2. Putting trash inside when open
        if (NightShiftStoryController.IsTrashTaskActive &&
            !NightShiftStoryController.HasDisposedTrash &&
            InteractableTrashbag.IsTrashPickedUp &&
            InteractableTrashbag.Instance != null)
        {
            Vector3 binInteriorPos = worldBounds.center + Vector3.up * 0.15f;
            InteractableTrashbag.Instance.PlaceInsideDumpster(binInteriorPos);
            PlayTrashThrowSound();

            if (NightShiftStoryController.Instance != null)
            {
                NightShiftStoryController.Instance.NotifyTrashDisposed();
            }

            StartCoroutine(CloseLidAfterDelayRoutine(0.75f));
            return;
        }

        // 3. Otherwise toggle closed if empty-handed
        if (IsPlayerEmptyHanded())
        {
            isOpen = false;
            PlayLidSound(0.88f);
        }
    }

    private IEnumerator CloseLidAfterDelayRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        isOpen = false;
        PlayLidSound(0.88f);
    }

    private void PlayLidSound(float pitch)
    {
        if (audioSource != null && lidOpenClip != null)
        {
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(lidOpenClip, 0.78f);
        }
    }

    private void PlayTrashThrowSound()
    {
        if (audioSource != null && trashThrowClip != null)
        {
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(trashThrowClip, 0.88f);
        }
    }

    private void GenerateProceduralAudio()
    {
        int sr = 44100;

        // 1. Heavy plastic/metal dumpster lid creak & clunk
        int lidSamples = (int)(sr * 0.24f);
        float[] lidBuf = new float[lidSamples];
        for (int i = 0; i < lidSamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 14f);
            float creak = Mathf.Sin(2f * Mathf.PI * (240f + 90f * Mathf.Sin(t * 28f)) * t) * 0.38f;
            float thud = Mathf.Sin(2f * Mathf.PI * 82f * t) * Mathf.Exp(-t * 32f) * 0.55f;
            float scrape = (Random.value * 2f - 1f) * 0.22f;
            lidBuf[i] = (creak + thud + scrape) * env * 0.75f;
        }
        lidOpenClip = AudioClip.Create("DumpsterLidOpen", lidSamples, 1, sr, false);
        lidOpenClip.SetData(lidBuf, 0);

        // 2. Heavy trashbag landing inside dumpster bin
        int throwSamples = (int)(sr * 0.28f);
        float[] throwBuf = new float[throwSamples];
        for (int i = 0; i < throwSamples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 12f);
            float impact = Mathf.Sin(2f * Mathf.PI * 68f * t) * Mathf.Exp(-t * 22f) * 0.7f;
            float rustle = (Random.value * 2f - 1f) * 0.45f;
            throwBuf[i] = (impact + rustle) * env * 0.8f;
        }
        trashThrowClip = AudioClip.Create("DumpsterTrashThrow", throwSamples, 1, sr, false);
        trashThrowClip.SetData(throwBuf, 0);
    }
}


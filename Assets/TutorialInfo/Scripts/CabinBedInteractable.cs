using System.Collections;
using UnityEngine;

/// <summary>
/// Attached at runtime to PFB_Bed in HouseScene.
/// Lets the player go to sleep once all bedtime prerequisites are met,
/// playing a smooth bedside-to-pillow lie-down animation while eyelids slowly close.
/// </summary>
public class CabinBedInteractable : MonoBehaviour, IInteractable
{
    private Transform pillow01;
    private Transform pillow02;
    private Transform blanketTransform;
    private AudioSource bedAudioSource;
    private AudioClip clothRustleClip;

    public static CabinBedInteractable Instance { get; private set; }

    public static void ConfigureBedroomBed()
    {
        GameObject bedObj = GameObject.Find("PFB_Bed");
        if (bedObj == null)
        {
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < allObjects.Length; i++)
            {
                if (allObjects[i] != null &&
                    allObjects[i].name.StartsWith("PFB_Bed", System.StringComparison.OrdinalIgnoreCase))
                {
                    bedObj = allObjects[i];
                    break;
                }
            }
        }

        if (bedObj == null)
        {
            Debug.LogWarning("[CabinBedInteractable] PFB_Bed was not found in HouseScene.");
            return;
        }

        bedObj.SetActive(true);
        CabinBedInteractable bed = bedObj.GetComponent<CabinBedInteractable>();
        if (bed == null)
        {
            bed = bedObj.AddComponent<CabinBedInteractable>();
        }

        bed.EnsureColliderAndChildren();
        Instance = bed;
    }

    private void Awake()
    {
        Instance = this;
        EnsureColliderAndChildren();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void EnsureColliderAndChildren()
    {
        if (pillow01 == null) pillow01 = transform.Find("BedPillow01");
        if (pillow02 == null) pillow02 = transform.Find("BedPillow02");
        if (blanketTransform == null) blanketTransform = transform.Find("BedBlanket");

        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = GetComponentInChildren<Collider>();
        }
        if (col == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.45f, 0f);
            box.size = new Vector3(1.9f, 0.75f, 2.2f);
        }
        else
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        if (bedAudioSource == null)
        {
            bedAudioSource = gameObject.GetComponent<AudioSource>();
            if (bedAudioSource == null)
            {
                bedAudioSource = gameObject.AddComponent<AudioSource>();
            }
            bedAudioSource.playOnAwake = false;
            bedAudioSource.spatialBlend = 0.7f;
            bedAudioSource.volume = 0.42f;
        }

        if (clothRustleClip == null)
        {
            clothRustleClip = CreateProceduralClothRustleClip();
        }
    }

    public string GetPromptText()
    {
        if (HouseSceneStoryController.IsIntroActive ||
            HouseSceneStoryController.IsDialogueActive ||
            HouseSceneStoryController.IsSleepingSequenceActive ||
            CabinChairInteractable.IsPlayerSitting)
        {
            return string.Empty;
        }

        if (!HouseSceneStoryController.IsBedSleepUnlocked)
        {
            return string.Empty;
        }

        return "E to Sleep";
    }

    public void Interact()
    {
        if (HouseSceneStoryController.IsIntroActive ||
            HouseSceneStoryController.IsDialogueActive ||
            HouseSceneStoryController.IsSleepingSequenceActive ||
            CabinChairInteractable.IsPlayerSitting ||
            !HouseSceneStoryController.IsBedSleepUnlocked)
        {
            return;
        }

        if (HouseSceneStoryController.IsFrontDoubleDoorOpen())
        {
            HouseSceneStoryController.NotifyTriedSleepWithOpenDoor();
            return;
        }

        HouseSceneStoryController.NotifyBedSleepStarted(this);
    }

    /// <summary>
    /// Animates the player walking to the clearest bedside, sitting on the mattress,
    /// reclining onto the pillows, and closing their eyes smoothly toward full blackness.
    /// </summary>
    public IEnumerator AnimateLieDownAndSleepRoutine(
        Transform playerTransform,
        Camera playerCam,
        System.Action<float> onEyelidProgressChanged)
    {
        EnsureColliderAndChildren();

        if (playerTransform == null || playerCam == null)
        {
            onEyelidProgressChanged?.Invoke(1f);
            yield break;
        }

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Vector3 pillowCenter = ComputePillowWorldPosition();
        Vector3 blanketCenter = blanketTransform != null
            ? blanketTransform.position
            : transform.TransformPoint(new Vector3(0f, 0.55f, 0.28f));

        Vector3 bedForward = Vector3.ProjectOnPlane(blanketCenter - pillowCenter, Vector3.up);
        if (bedForward.sqrMagnitude < 0.001f)
        {
            bedForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }
        bedForward.Normalize();

        Vector3 bedRight = Vector3.Cross(Vector3.up, bedForward).normalized;
        Vector3 bedMidWorld = Vector3.Lerp(pillowCenter, blanketCenter, 0.42f);

        float sideSign = ChooseClearBedSide(playerTransform, bedMidWorld, bedRight, cc);

        Vector3 startCamPos = playerCam.transform.position;
        Quaternion startCamRot = playerCam.transform.rotation;
        float standingCamY = startCamPos.y;

        // 1. Step to the clear edge of the bed
        Vector3 bedsideStandPos = bedMidWorld + bedRight * (sideSign * 1.18f);
        bedsideStandPos.y = standingCamY;

        Vector3 lookAtBedEdge = bedMidWorld + bedRight * (sideSign * 0.15f);
        lookAtBedEdge.y = pillowCenter.y + 0.05f;
        Quaternion standLookRot = Quaternion.LookRotation((lookAtBedEdge - bedsideStandPos).normalized, Vector3.up);

        float stepDuration = 0.75f;
        float elapsed = 0f;
        while (elapsed < stepDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / stepDuration);
            float smooth = t * t * (3f - 2f * t);

            Vector3 pos = Vector3.Lerp(startCamPos, bedsideStandPos, smooth);
            // Gentle walking footstep bob
            pos.y += Mathf.Sin(t * Mathf.PI * 3f) * 0.022f * (1f - smooth * 0.4f);

            playerCam.transform.position = pos;
            playerCam.transform.rotation = Quaternion.Slerp(startCamRot, standLookRot, smooth);
            yield return null;
        }

        // 2. Sit on the edge of the mattress
        PlayClothSound(0.32f, 0.94f);
        Vector3 sitCamPos = pillowCenter + bedForward * 0.52f + bedRight * (sideSign * 0.58f);
        sitCamPos.y = pillowCenter.y + 0.54f;

        Vector3 sitLookTarget = pillowCenter + bedForward * 0.28f - bedRight * (sideSign * 0.25f);
        sitLookTarget.y = pillowCenter.y + 0.10f;
        Quaternion sitCamRot = Quaternion.LookRotation((sitLookTarget - sitCamPos).normalized, Vector3.up);

        float sitDuration = 0.65f;
        elapsed = 0f;
        while (elapsed < sitDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / sitDuration);
            float smooth = t * t * (3f - 2f * t);

            playerCam.transform.position = Vector3.Lerp(bedsideStandPos, sitCamPos, smooth);
            playerCam.transform.rotation = Quaternion.Slerp(standLookRot, sitCamRot, smooth);
            yield return null;
        }

        // 3. Recline smoothly onto the pillows (favoring the side the player entered from)
        PlayClothSound(0.45f, 0.85f);
        Vector3 headRestPos = pillowCenter + bedRight * (sideSign * 0.26f) + bedForward * 0.12f;
        headRestPos.y = pillowCenter.y + 0.20f;

        Vector3 restLookDir = (bedForward * 0.68f + Vector3.up * 0.72f - bedRight * (sideSign * 0.10f)).normalized;
        Quaternion restBaseRot = Quaternion.LookRotation(restLookDir, Vector3.up) *
                                 Quaternion.Euler(0f, 0f, sideSign * -4.5f);

        float reclineDuration = 1.25f;
        elapsed = 0f;
        while (elapsed < reclineDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / reclineDuration);
            float smooth = t * t * (3f - 2f * t);

            // Slight natural sink onto the pillow before settling
            Vector3 pos = Vector3.Lerp(sitCamPos, headRestPos, smooth);
            pos.y -= Mathf.Sin(t * Mathf.PI) * 0.03f;

            playerCam.transform.position = pos;
            playerCam.transform.rotation = Quaternion.Slerp(sitCamRot, restBaseRot, smooth);
            yield return null;
        }

        playerCam.transform.position = headRestPos;
        playerCam.transform.rotation = restBaseRot;

        // 4. Slow, realistic eyelid droop -> brief sleep-fighting flutter -> complete shut
        yield return StartCoroutine(AnimateRealisticEyelidsRoutine(playerCam, headRestPos, restBaseRot, sideSign, onEyelidProgressChanged));
    }

    private IEnumerator AnimateRealisticEyelidsRoutine(
        Camera playerCam,
        Vector3 basePos,
        Quaternion baseRot,
        float sideSign,
        System.Action<float> onEyelidProgressChanged)
    {
        float totalCloseTime = 3.85f;
        float elapsed = 0f;

        while (elapsed < totalCloseTime)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / totalCloseTime);

            float eyeProgress;
            if (normalized < 0.34f)
            {
                // Stage A: Heavy initial droop (0 -> 0.56)
                float k = normalized / 0.34f;
                float ease = 1f - Mathf.Pow(1f - k, 2.2f);
                eyeProgress = Mathf.Lerp(0f, 0.56f, ease);
            }
            else if (normalized < 0.49f)
            {
                // Stage B: Brief heavy-lidded flutter fighting sleep (0.56 -> 0.36 -> 0.50)
                float k = (normalized - 0.34f) / 0.15f;
                float pulse = Mathf.Sin(k * Mathf.PI);
                eyeProgress = Mathf.Lerp(0.56f, 0.50f, k) - pulse * 0.20f;
            }
            else
            {
                // Stage C: Final heavy slow close to 100% shut (0.50 -> 1.0)
                float k = (normalized - 0.49f) / 0.51f;
                float ease = k * k * (3f - 2f * k);
                eyeProgress = Mathf.Lerp(0.50f, 1.0f, ease);
            }

            onEyelidProgressChanged?.Invoke(Mathf.Clamp01(eyeProgress));

            // Subtle breathing rise/fall & drowsy head tilt settle while lying on pillow
            float breath = Mathf.Sin(elapsed * 1.45f) * 0.007f;
            float settlePitch = Mathf.SmoothStep(0f, -3.5f, normalized);
            float settleRoll = Mathf.SmoothStep(0f, sideSign * -2.2f, normalized);
            playerCam.transform.position = basePos + Vector3.up * breath;
            playerCam.transform.rotation = baseRot * Quaternion.Euler(settlePitch, 0f, settleRoll);

            yield return null;
        }

        onEyelidProgressChanged?.Invoke(1.0f);
    }

    private Vector3 ComputePillowWorldPosition()
    {
        if (pillow01 != null && pillow02 != null)
        {
            return (pillow01.position + pillow02.position) * 0.5f;
        }
        if (pillow01 != null) return pillow01.position;
        if (pillow02 != null) return pillow02.position;
        return transform.TransformPoint(new Vector3(0f, 0.63f, -0.90f));
    }

    private float ChooseClearBedSide(Transform playerTransform, Vector3 bedMidWorld, Vector3 bedRight, Collider playerCollider)
    {
        float sideFromPlayer = Vector3.Dot(playerTransform.position - bedMidWorld, bedRight) >= 0f ? 1f : -1f;

        bool preferredClear = IsStandSpotClear(bedMidWorld + bedRight * (sideFromPlayer * 1.18f), playerCollider);
        if (preferredClear) return sideFromPlayer;

        float alternateSide = -sideFromPlayer;
        if (IsStandSpotClear(bedMidWorld + bedRight * (alternateSide * 1.18f), playerCollider))
        {
            return alternateSide;
        }

        return sideFromPlayer;
    }

    private bool IsStandSpotClear(Vector3 candidatePos, Collider playerCollider)
    {
        Vector3 checkPos = candidatePos;
        checkPos.y = transform.position.y + 0.95f;

        Collider[] hits = Physics.OverlapSphere(checkPos, 0.28f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit == playerCollider) continue;
            if (hit.transform.IsChildOf(transform) || hit.transform == transform) continue;
            return false;
        }

        return true;
    }

    private void PlayClothSound(float volumeScale, float pitch)
    {
        if (bedAudioSource == null || clothRustleClip == null) return;
        bedAudioSource.pitch = pitch;
        bedAudioSource.PlayOneShot(clothRustleClip, volumeScale);
    }

    private AudioClip CreateProceduralClothRustleClip()
    {
        const int sampleRate = 22050;
        const float duration = 0.42f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        System.Random rng = new System.Random(1942);
        float lowpass = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float env = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.45f);
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lowpass = Mathf.Lerp(lowpass, white, 0.14f);
            float creak = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(95f, 62f, t) * (i / (float)sampleRate)) * 0.18f;
            samples[i] = (lowpass * 0.65f + creak) * env;
        }

        AudioClip clip = AudioClip.Create("BedClothRustle", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}


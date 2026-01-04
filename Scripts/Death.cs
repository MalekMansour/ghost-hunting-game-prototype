using UnityEngine;
using System.Collections;

public class Death : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Your player cylinder root (the object that has all the scripts). If empty, auto uses transform.root.")]
    public GameObject playerCylinder;

    [Tooltip("Main camera (usually Camera.main). If empty, auto found.")]
    public Camera mainCamera;

    [Tooltip("AudioSource to play the death sound. If empty, tries to find one on the cylinder.")]
    public AudioSource deathAudioSource;

    [Header("Animator / Model")]
    [Tooltip("Animator on the spawned character model. If empty, auto-found in children at runtime.")]
    public Animator animator;

    [Tooltip("Root of the visual model (mesh/armature root). If empty, uses animator.transform.")]
    public Transform modelRoot;

    [Tooltip("Optional: a bone/transform near the torso/hips (used for blood raycast origin). If empty, uses modelRoot.")]
    public Transform bloodOrigin;

    [Header("Death Animation Params")]
    public string dieParamName = "Die";
    public enum DieParamMode { Trigger, Bool }
    public DieParamMode dieParamMode = DieParamMode.Trigger;
    public bool dieBoolValue = true;

    [Header("Death Sound")]
    public AudioClip deathClip;
    [Range(0f, 1f)] public float deathVolume = 0.8f;

    [Header("Wait For Animation End")]
    [Tooltip("Animator layer index to read the death state from (usually 0).")]
    public int animatorLayerIndex = 0;

    [Tooltip("If set, we will wait until THIS state is playing and finished. Leave empty to wait for whichever state is active after Die.")]
    public string deathStateName = "";

    [Tooltip("Extra time after animation finishes before we remove the body (seconds).")]
    public float afterAnimExtraDelay = 0.0f;

    [Header("Body Removal")]
    [Tooltip("If true, disable all renderers under modelRoot when the animation ends.")]
    public bool disableRenderersOnDeathEnd = true;

    [Tooltip("If true, also disable all Colliders under modelRoot (so you can't bump the invisible body).")]
    public bool disableCollidersOnDeathEnd = true;

    [Tooltip("If true, set modelRoot gameObject inactive after disabling things (strongest 'remove body').")]
    public bool deactivateModelRoot = true;

    [Header("Blood Splatter (NO particles)")]
    [Tooltip("Prefab for a static blood splatter (Quad/Plane with transparent URP material).")]
    public GameObject bloodSplatterPrefab;

    [Tooltip("Layers considered 'ground' for placing blood.")]
    public LayerMask groundMask = ~0;

    [Tooltip("How far down we raycast to find the floor.")]
    public float groundRayDistance = 10f;

    [Tooltip("Small offset up to avoid z-fighting.")]
    public float bloodUpOffset = 0.02f;

    [Tooltip("Randomize size of the blood splatter.")]
    public Vector2 bloodScaleRange = new Vector2(0.9f, 1.25f);

    [Tooltip("Random rotation around the surface normal.")]
    public bool randomYaw = true;

    [Header("Spectator Mode")]
    [Tooltip("Delay AFTER body removal before spectator starts (0 = instant).")]
    public float spectatorDelay = 0.0f;

    public string spectatorLayerName = "Spectator";

    [Header("Disable Scripts on Cylinder")]
    [Tooltip("If true, disables every MonoBehaviour on the player cylinder once spectator begins (except this script).")]
    public bool disableAllScriptsOnCylinder = true;

    [Tooltip("Also disable Light components on the camera (flashlight).")]
    public bool disableFlashlightForever = true;

    [Header("Spectator Movement")]
    public float spectatorControllerHeight = 1.8f;
    public float spectatorControllerRadius = 0.35f;
    public float spectatorMoveSpeed = 5f;
    public float spectatorSprintMultiplier = 1.75f;
    public float spectatorLookSensitivity = 2.2f;
    public bool lockCursor = true;
    public bool keepCameraWorldY = true;

    [Header("Debug")]
    public bool debugLogs = true;

    private bool deathStarted = false;
    public bool IsDead => deathStarted;
    private bool soundPlayed = false;

    private void Awake()
    {
        if (playerCylinder == null) playerCylinder = transform.root.gameObject;
        if (mainCamera == null) mainCamera = Camera.main;
        if (deathAudioSource == null && playerCylinder != null)
            deathAudioSource = playerCylinder.GetComponentInChildren<AudioSource>(true);
    }

    public void BeginDeath()
    {
        if (deathStarted) return;
        deathStarted = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        EnsureRuntimeRefs();

        // Drop inventory immediately when death starts (throw forward)
        DropInventoryNow();

        // Play animation immediately
        if (animator == null)
        {
            Log("[ERROR] Animator not found. Assign it or ensure it exists in children of the spawned model.");
            yield break;
        }

        animator.enabled = true;
        animator.speed = 1f;

        if (dieParamMode == DieParamMode.Trigger)
        {
            animator.ResetTrigger(dieParamName);
            animator.SetTrigger(dieParamName);
        }
        else
        {
            animator.SetBool(dieParamName, dieBoolValue);
        }

        // Play sound immediately (ONCE)
        PlayDeathSoundOnce();

        // Wait until animation ends (real end)
        yield return WaitForDeathAnimationToFinish();

        if (afterAnimExtraDelay > 0f)
            yield return new WaitForSeconds(afterAnimExtraDelay);

        // Spawn blood splatter on the floor (static)
        SpawnBloodSplatter();

        // Remove body entirely
        RemoveBody();

        if (spectatorDelay > 0f)
            yield return new WaitForSeconds(spectatorDelay);

        ForceToSpectatorLayer();
        EnterSpectator();
    }

    private void EnsureRuntimeRefs()
    {
        if (playerCylinder == null) playerCylinder = transform.root.gameObject;
        if (mainCamera == null) mainCamera = Camera.main;

        if (animator == null)
            animator = playerCylinder != null
                ? playerCylinder.GetComponentInChildren<Animator>(true)
                : GetComponentInChildren<Animator>(true);

        if (modelRoot == null && animator != null)
            modelRoot = animator.transform;

        if (bloodOrigin == null)
            bloodOrigin = (modelRoot != null) ? modelRoot : transform;
    }

    private void DropInventoryNow()
    {
        if (playerCylinder == null) return;

        PlayerInventory inv = playerCylinder.GetComponentInChildren<PlayerInventory>(true);
        if (inv == null) return;

        Transform throwFrom = (mainCamera != null) ? mainCamera.transform : transform;
        inv.DropAllItemsOnDeath(throwFrom);
    }

    private void PlayDeathSoundOnce()
    {
        if (soundPlayed) return;
        soundPlayed = true;

        if (deathAudioSource != null && deathClip != null)
        {
            deathAudioSource.spatialBlend = 0f;
            deathAudioSource.PlayOneShot(deathClip, Mathf.Clamp01(deathVolume));
        }
    }

    private IEnumerator WaitForDeathAnimationToFinish()
    {
        yield return null;

        if (animator == null) yield break;

        if (!string.IsNullOrEmpty(deathStateName))
        {
            while (true)
            {
                AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(animatorLayerIndex);
                if (st.IsName(deathStateName)) break;
                yield return null;
            }

            while (true)
            {
                AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(animatorLayerIndex);
                if (!st.IsName(deathStateName)) break;
                if (st.normalizedTime >= 1f) break;
                yield return null;
            }

            yield break;
        }

        AnimatorStateInfo startState = animator.GetCurrentAnimatorStateInfo(animatorLayerIndex);
        int startHash = startState.fullPathHash;

        if (startState.length <= 0.0001f)
        {
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        while (true)
        {
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(animatorLayerIndex);
            if (st.fullPathHash != startHash) break;
            if (st.normalizedTime >= 1f) break;
            yield return null;
        }
    }

    private void SpawnBloodSplatter()
    {
        if (bloodSplatterPrefab == null || bloodOrigin == null) return;

        Vector3 origin = bloodOrigin.position + Vector3.up * 0.75f;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            return;

        Vector3 pos = hit.point + hit.normal * Mathf.Max(0.02f, bloodUpOffset);

        // Quad faces forward (Z+). If using Plane, change Vector3.forward to Vector3.up.
        Quaternion rot = Quaternion.FromToRotation(Vector3.forward, hit.normal);

        if (randomYaw)
        {
            float yaw = Random.Range(0f, 360f);
            rot = Quaternion.AngleAxis(yaw, hit.normal) * rot;
        }

        GameObject blood = Instantiate(bloodSplatterPrefab, pos, rot);
        blood.transform.SetParent(null, true);

        float s = Random.Range(bloodScaleRange.x, bloodScaleRange.y);
        blood.transform.localScale = blood.transform.localScale * s;
    }

    private void RemoveBody()
    {
        if (modelRoot == null) return;

        if (disableRenderersOnDeathEnd)
        {
            var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = false;
        }

        if (disableCollidersOnDeathEnd)
        {
            var cols = modelRoot.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) c.enabled = false;
        }

        if (animator != null)
            animator.enabled = false;

        if (deactivateModelRoot)
            modelRoot.gameObject.SetActive(false);
    }

    private void ForceToSpectatorLayer()
    {
        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer < 0)
        {
            Log($"[ERROR] Layer '{spectatorLayerName}' not found.");
            return;
        }

        if (playerCylinder != null)
            SetLayerRecursively(playerCylinder, specLayer);
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void EnterSpectator()
    {
        if (mainCamera == null) return;

        if (disableFlashlightForever)
            DisableFlashlight(mainCamera);

        var pv = mainCamera.GetComponent<PlayerView>();
        if (pv != null)
            pv.SetSpectatorMode(true);

        if (disableAllScriptsOnCylinder && playerCylinder != null)
        {
            var scripts = playerCylinder.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in scripts)
            {
                if (mb == null) continue;
                if (mb == this) continue;
                mb.enabled = false;
            }
        }

        Transform camT = mainCamera.transform;
        Vector3 camPos = camT.position;
        Quaternion camRot = camT.rotation;

        GameObject spec = new GameObject("SpectatorController");
        spec.transform.position = camPos;
        spec.transform.rotation = Quaternion.Euler(0f, camRot.eulerAngles.y, 0f);

        int specLayer = LayerMask.NameToLayer(spectatorLayerName);
        if (specLayer >= 0)
            SetLayerRecursively(spec, specLayer);

        CharacterController cc = spec.AddComponent<CharacterController>();
        cc.height = spectatorControllerHeight;
        cc.radius = spectatorControllerRadius;
        cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
        cc.stepOffset = 0.25f;

        camT.SetParent(spec.transform, true);
        camT.position = camPos;
        camT.rotation = camRot;

        var controller = spec.AddComponent<SpectatorController>();
        controller.cc = cc;
        controller.cameraTransform = camT;
        controller.moveSpeed = spectatorMoveSpeed;
        controller.sprintMultiplier = spectatorSprintMultiplier;
        controller.lookSensitivity = spectatorLookSensitivity;
        controller.lockCursor = lockCursor;
        controller.keepWorldY = keepCameraWorldY;
        controller.fixedWorldY = camPos.y;
    }

    private void DisableFlashlight(Camera cam)
    {
        if (cam == null) return;

        var lights = cam.GetComponentsInChildren<Light>(true);
        foreach (var l in lights) l.enabled = false;

        var behaviours = cam.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var b in behaviours)
        {
            if (b == null) continue;
            if (b.GetType().Name == "Flashlight")
                b.enabled = false;
        }
    }

    private void Log(string msg)
    {
        if (debugLogs) Debug.Log("[Death] " + msg, this);
    }
}

public class SpectatorController : MonoBehaviour
{
    [HideInInspector] public CharacterController cc;
    [HideInInspector] public Transform cameraTransform;

    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.75f;
    public float lookSensitivity = 2.2f;
    public bool lockCursor = true;

    public bool keepWorldY = true;
    public float fixedWorldY;

    private float yaw;
    private float pitch;

    private void Start()
    {
        if (lockCursor)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (cc == null || cameraTransform == null) return;

        float mx = Input.GetAxis("Mouse X") * lookSensitivity;
        float my = Input.GetAxis("Mouse Y") * lookSensitivity;

        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, -85f, 85f);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 input = new Vector3(h, 0f, v);
        input = Vector3.ClampMagnitude(input, 1f);

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
        Vector3 move = (transform.right * input.x + transform.forward * input.z) * speed;

        cc.Move(move * Time.deltaTime);

        if (keepWorldY)
        {
            Vector3 p = cameraTransform.position;
            cameraTransform.position = new Vector3(p.x, fixedWorldY, p.z);
        }
    }
}

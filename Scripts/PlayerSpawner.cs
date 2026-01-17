using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [Header("References (auto if empty)")]
    public GameObject cylinder;                 // can be auto-found
    public Transform modelRoot;                 // where the character model should be parented

    [Header("Character Prefabs")]
    public GameObject[] characterPrefabs;

    private void Start()
    {
        // If you forgot to assign in inspector, we try to find them safely.
        if (cylinder == null)
        {
            // Try common names first
            Transform t = transform.Find("cylinder");
            if (t == null) t = transform.Find("Cylinder");
            if (t != null) cylinder = t.gameObject;
        }

        if (modelRoot == null)
        {
            // Recommended: create an empty child called "ModelRoot" under the player
            Transform mr = transform.Find("ModelRoot");
            if (mr != null) modelRoot = mr;
        }

        // Fallback: if you didn't make ModelRoot, we parent to cylinder (instance only)
        if (modelRoot == null && cylinder != null)
            modelRoot = cylinder.transform;

        SpawnCharacter();
    }

    public void SpawnCharacter()
    {
        if (cylinder == null)
        {
            Debug.LogError("[PlayerSpawner] Cylinder reference missing. Make sure the player prefab has a Cylinder child or assign it.");
            return;
        }

        if (characterPrefabs == null || characterPrefabs.Length == 0)
        {
            Debug.LogError("[PlayerSpawner] No characterPrefabs assigned.");
            return;
        }

        int index = PlayerPrefs.GetInt("SelectedCharacter", 0);

        if (index < 0 || index >= characterPrefabs.Length)
        {
            Debug.LogError("[PlayerSpawner] Invalid character index!");
            return;
        }

        // Clear old model if any (prevents duplicates when reloading/testing)
        if (modelRoot != null)
        {
            for (int i = modelRoot.childCount - 1; i >= 0; i--)
                Destroy(modelRoot.GetChild(i).gameObject);
        }

        // Spawn at the player's cylinder position, then parent under ModelRoot
        GameObject character = Instantiate(
            characterPrefabs[index],
            cylinder.transform.position,
            cylinder.transform.rotation
        );

        // ✅ This will only succeed if modelRoot is a SCENE INSTANCE (which it will be)
        character.transform.SetParent(modelRoot, true);

        // Lock transforms nicely under the player
        character.transform.localPosition = Vector3.zero;
        character.transform.localRotation = Quaternion.identity;

        // Optional scaling system you already had
        ModelScaler scaler = character.GetComponent<ModelScaler>();
        if (scaler != null)
        {
            scaler.ApplyScale();
        }

        Animator anim = character.GetComponentInChildren<Animator>();
        if (anim == null)
        {
            Debug.LogError("[PlayerSpawner] Animator missing on character prefab!");
            return;
        }

        // Hook movement animation script (on cylinder)
        MovementAnimation animScript = cylinder.GetComponent<MovementAnimation>();
        if (animScript != null)
        {
            animScript.SetAnimator(anim);
        }

        // Find HoldPoint inside the model
        Transform holdPoint = null;
        foreach (Transform t in character.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "HoldPoint")
            {
                holdPoint = t;
                break;
            }
        }

        if (holdPoint == null)
        {
            Debug.LogError("[PlayerSpawner] ❌ HoldPoint NOT FOUND on spawned character!");
            return;
        }

        // Hook interaction hold point
        Interaction interaction = cylinder.GetComponent<Interaction>();
        if (interaction != null)
        {
            interaction.SetHoldPoint(holdPoint);
        }

        // Hook inventory hand point
        PlayerInventory inventory = cylinder.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            inventory.handPoint = holdPoint;
        }
        else
        {
            Debug.LogWarning("[PlayerSpawner] PlayerInventory missing on cylinder!");
        }
    }
}

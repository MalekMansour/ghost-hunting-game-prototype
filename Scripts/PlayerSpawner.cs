using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject cylinder;
    public GameObject[] characterPrefabs;

    void Start()
    {
        SpawnCharacter();
    }

    void SpawnCharacter()
    {
        int index = PlayerPrefs.GetInt("SelectedCharacter", 0);

        if (index < 0 || index >= characterPrefabs.Length)
        {
            Debug.LogError("Invalid character index!");
            return;
        }

        GameObject character = Instantiate(
            characterPrefabs[index],
            cylinder.transform.position,
            cylinder.transform.rotation
        );

        character.transform.SetParent(cylinder.transform, false);
        character.transform.localPosition = Vector3.zero;
        character.transform.localRotation = Quaternion.identity;

        ModelScaler scaler = character.GetComponent<ModelScaler>();
        if (scaler != null)
        {
            scaler.ApplyScale();
        }

        Animator anim = character.GetComponentInChildren<Animator>();
        if (anim == null)
        {
            Debug.LogError("Animator missing on character prefab!");
            return;
        }

        MovementAnimation animScript = cylinder.GetComponent<MovementAnimation>();
        if (animScript != null)
        {
            animScript.SetAnimator(anim);
        }

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
            Debug.LogError("❌ HoldPoint NOT FOUND on spawned character!");
            return;
        }

        Interaction interaction = cylinder.GetComponent<Interaction>();
        if (interaction != null)
        {
            interaction.SetHoldPoint(holdPoint);
        }

        PlayerInventory inventory = cylinder.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            inventory.handPoint = holdPoint;
        }
        else
        {
            Debug.LogWarning("PlayerInventory missing on cylinder!");
        }
    }
}


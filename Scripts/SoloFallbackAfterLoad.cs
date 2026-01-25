using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class SoloFallbackAfterLoad : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float checkAfterSeconds = 10f;

    [Header("Offline Prefab (NO NetworkObject)")]
    [SerializeField] private GameObject offlinePlayerPrefab;

    private bool switched;

    private IEnumerator Start()
    {
        Debug.Log($"[SoloFallback] Waiting {checkAfterSeconds}s before solo check...");
        yield return new WaitForSecondsRealtime(checkAfterSeconds);

        if (switched) yield break;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            yield break;

        int count = NetworkManager.Singleton.ConnectedClients.Count;
        Debug.Log($"[SoloFallback] ConnectedClients={count}");

        if (count >= 2)
        {
            Debug.Log("[SoloFallback] Multiplayer active. No fallback needed.");
            yield break;
        }

        Debug.LogWarning("[SoloFallback] SOLO detected → switching to OFFLINE mode");

        // Get network player
        NetworkObject netPlayerNO = NetworkManager.Singleton.LocalClient?.PlayerObject;
        GameObject netPlayer = netPlayerNO != null ? netPlayerNO.gameObject : null;

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        // ✅ NEW: cache chosen character index BEFORE disabling anything
        int chosenIndex = 0;
        bool gotChosenIndex = false;

        if (netPlayer != null)
        {
            spawnPos = netPlayer.transform.position;
            spawnRot = netPlayer.transform.rotation;

            // Try read chosen index from NetworkCharacterAppearance (best)
            NetworkCharacterAppearance appearance = netPlayer.GetComponent<NetworkCharacterAppearance>();
            if (appearance != null)
            {
                // We can't access private NetworkVariable directly, so we infer from the current model if needed.
                // BUT: your appearance script renames player as Player(owner)-CharX or similar.
                // We'll try to parse from the name first:
                // Example: "Player(0)-Char0" or "Player(0)-Char0/..." etc
                chosenIndex = TryParseCharIndexFromName(netPlayer.name, out gotChosenIndex);

                // If name parse fails, attempt to infer from spawned model prefab name under ModelRoot
                if (!gotChosenIndex)
                {
                    chosenIndex = InferIndexFromSpawnedModel(netPlayer, out gotChosenIndex);
                }
            }
            else
            {
                // If no appearance script, infer from model
                chosenIndex = InferIndexFromSpawnedModel(netPlayer, out gotChosenIndex);
            }

            if (!gotChosenIndex)
            {
                // Last fallback: LocalSelection (won't break)
                chosenIndex = LocalSelection.SelectedCharacterIndex;
                gotChosenIndex = true;
            }

            Debug.Log($"[SoloFallback] ChosenIndex={chosenIndex} (gotChosenIndex={gotChosenIndex})");

            // 🔥 IMPORTANT: disable ALL gameplay scripts on net player
            foreach (var mb in netPlayer.GetComponentsInChildren<MonoBehaviour>())
            {
                mb.enabled = false;
            }

            // Disable camera + audio explicitly
            Camera cam = netPlayer.GetComponentInChildren<Camera>(true);
            if (cam != null) cam.enabled = false;

            AudioListener al = netPlayer.GetComponentInChildren<AudioListener>(true);
            if (al != null) al.enabled = false;

            Debug.Log("[SoloFallback] Disabled network player scripts & camera.");
        }

        // Spawn offline player
        if (offlinePlayerPrefab == null)
        {
            Debug.LogError("[SoloFallback] offlinePlayerPrefab NOT assigned!");
            yield break;
        }

        GameObject offline = Instantiate(offlinePlayerPrefab, spawnPos, spawnRot);
        Debug.Log("[SoloFallback] ✅ Offline player spawned.");

        // ✅ NEW: apply the chosen character model to the offline player
        PlayerSpawner offlineSpawner = offline.GetComponent<PlayerSpawner>();
        if (offlineSpawner == null)
            offlineSpawner = offline.GetComponentInChildren<PlayerSpawner>(true);

        if (offlineSpawner != null)
        {
            // Ensure it has the prefab list in inspector on the OFFLINE prefab too.
            offlineSpawner.SpawnOrSwapModel(chosenIndex, "SoloFallback copy chosen character");
            Debug.Log($"[SoloFallback] ✅ Applied chosen character idx={chosenIndex} to offline player.");
        }
        else
        {
            Debug.LogWarning("[SoloFallback] Offline player has no PlayerSpawner, cannot apply chosen character.");
        }

        // Now it is SAFE to shut down Netcode
        switched = true;
        Debug.LogWarning("[SoloFallback] Shutting down NetworkManager (server stops, game continues).");
        NetworkManager.Singleton.Shutdown();
    }

    // ----------------------------
    // Helpers
    // ----------------------------

    private int TryParseCharIndexFromName(string n, out bool ok)
    {
        ok = false;
        if (string.IsNullOrEmpty(n)) return 0;

        // expected patterns:
        // "Player(0)-Char3"
        // "Player(0)-Vivi" (if you changed naming)
        int i = n.IndexOf("Char", System.StringComparison.OrdinalIgnoreCase);
        if (i < 0) return 0;

        i += 4; // after "Char"
        // read digits
        int start = i;
        while (i < n.Length && char.IsDigit(n[i])) i++;

        if (i <= start) return 0;

        string digits = n.Substring(start, i - start);
        if (int.TryParse(digits, out int idx))
        {
            ok = true;
            return idx;
        }

        return 0;
    }

    private int InferIndexFromSpawnedModel(GameObject netPlayer, out bool ok)
    {
        ok = false;
        if (netPlayer == null) return 0;

        PlayerSpawner spawner = netPlayer.GetComponent<PlayerSpawner>();
        if (spawner == null)
            spawner = netPlayer.GetComponentInChildren<PlayerSpawner>(true);

        if (spawner == null) return 0;

        // Find the ModelRoot and the spawned model
        Transform mr = spawner.modelRoot;
        if (mr == null)
        {
            // attempt to find by name under netPlayer
            foreach (Transform t in netPlayer.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "ModelRoot") { mr = t; break; }
            }
        }

        if (mr == null || mr.childCount == 0) return 0;

        string modelName = mr.GetChild(0).name; // "Model(idx 3 | owner 0)" or "Model(Vivi)" etc

        // Try parse "idx X"
        int idxPos = modelName.IndexOf("idx", System.StringComparison.OrdinalIgnoreCase);
        if (idxPos >= 0)
        {
            idxPos += 3;
            while (idxPos < modelName.Length && modelName[idxPos] == ' ') idxPos++;
            int start = idxPos;
            while (idxPos < modelName.Length && char.IsDigit(modelName[idxPos])) idxPos++;
            if (idxPos > start)
            {
                string digits = modelName.Substring(start, idxPos - start);
                if (int.TryParse(digits, out int idx))
                {
                    ok = true;
                    return idx;
                }
            }
        }

        // If your spawner list exists, match prefab name inside the instance name
        if (spawner.characterPrefabs != null && spawner.characterPrefabs.Length > 0)
        {
            for (int i = 0; i < spawner.characterPrefabs.Length; i++)
            {
                if (spawner.characterPrefabs[i] == null) continue;
                if (modelName.Contains(spawner.characterPrefabs[i].name))
                {
                    ok = true;
                    return i;
                }
            }
        }

        return 0;
    }
}

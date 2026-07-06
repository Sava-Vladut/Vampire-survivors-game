using Lexone.UnityTwitchChat;
using NaughtyAttributes;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class TwitchListener : MonoBehaviour
{
    [System.Serializable]
    public class ChatterSpawnEntry
    {
        [ShowAssetPreview] public GameObject prefab;

        [Min(0f)] public float weight = 1f;

        [Header("Power Cost")]
        [Tooltip("How much of the chatter's power budget this prefab consumes per spawn.")]
        [Min(1)] public int power = 1;

        [Header("Spawn Gate")]
        [Tooltip("Seconds since start before this prefab can be selected to spawn.")]
        [MinMaxSlider(0, 600f)] public Vector2 timeSpawn = new Vector2(0, 600); // eligibility time
    }

    [Header("Spawn Setup")]
    [SerializeField] private List<ChatterSpawnEntry> chatterPrefabs = new();
    [SerializeField] private Transform player;
    [SerializeField] private float minSpawnDistance = 1.5f;
    [SerializeField] private float maxSpawnDistance = 3.5f;

    [Header("Power Progression")]
    [Tooltip("How often to attempt increasing global min power (seconds)")]
    [SerializeField, Min(0f)] public float spawnIncreaseInterval = 60f;

    public int minPower = 0; // Minimum power level (also drives global cap)
    [Tooltip("Global max active spawns = minPower * ratio")]
    [Min(1)] public int maxSpawnPerPowerRatio = 3;
    public float chanceToUpgradeMinPower = 0.6f; // Chance to upgrade chatter power on spawn
    [SerializeField] private bool alwaysSpawnMaxEnemies = false;

    // Track time for next power increase attempt
    private float nextSpawnIncreaseTime = 0f;

    [Header("Collision Check")]
    [Tooltip("Radius used for checking if spawn position is ON these layers (e.g., Ground).")]
    [SerializeField] private float spawnCheckRadius = 0.5f;

    [Tooltip("Layers the spawn position MUST overlap (e.g., Ground/Walkable).")]
    [SerializeField] private LayerMask spawnOnLayers;
    [SerializeField]
    private LayerMask linecastBlockLayers;


    [Header("Repositioning")]
    [Tooltip("If a chatter drifts farther than this from the player, it will be teleported back near the player.")]
    [SerializeField] private float maxDistanceFromPlayer = 12f;

    [Header("UI")]
    [Tooltip("Optional: displays stopwatch time (MM:SS)")]
    [SerializeField] private TextMeshProUGUI stopwatchText;

    // Stopwatch time
    private float elapsedSeconds = 0f;
    // Track spawned chatters
    [SerializeField] public List<GameObject> spawnedChatters = new();
    [SerializeField] public List<Chatter> chatters = new();
    private void Start()
    {
        if (player == null) player = transform;
        if (minSpawnDistance > maxSpawnDistance)
        {
            float t = minSpawnDistance;
            minSpawnDistance = maxSpawnDistance;
            maxSpawnDistance = t;
        }

        if (IRC.Instance != null)
            IRC.Instance.OnChatMessage += OnChatMessage;
    }

    private void Update()
    {

        for (int i = spawnedChatters.Count - 1; i >= 0; i--)
            if (spawnedChatters[i] == null)
                spawnedChatters.RemoveAt(i);

        if (player == null) return; // ✅ Prevents MissingReferenceException
        // Only update stopwatch if the game isn't paused
        if (Time.timeScale > 0f)
        {
            elapsedSeconds += Time.deltaTime;

            // Periodically attempt to increase global min power
            if (spawnIncreaseInterval > 0f && elapsedSeconds >= nextSpawnIncreaseTime)
            {
                if (UnityEngine.Random.value < chanceToUpgradeMinPower)
                {
                    minPower++;
                }
                nextSpawnIncreaseTime = elapsedSeconds + spawnIncreaseInterval;
                //  Debug.Log($"[TwitchListener] Min power increased to {minPower}");
            }


            // Reposition chatters if they drift too far
            for (int i = spawnedChatters.Count - 1; i >= 0; i--)
            {
                GameObject chatterObj = spawnedChatters[i];
                if (chatterObj == null) continue;

                float dist = Vector3.Distance(player.position, chatterObj.transform.position);
                if (dist > maxDistanceFromPlayer)
                {
                    Vector3? newPos = FindValidSpawnPosition();
                    if (newPos.HasValue)
                    {
                        // Teleport chatter safely
                        Rigidbody2D rb = chatterObj.GetComponent<Rigidbody2D>();
                        if (rb != null)
                            rb.position = newPos.Value; // physics-safe teleport
                        else
                            chatterObj.transform.position = newPos.Value;

                        Debug.Log($"[TwitchListener] Repositioned {chatterObj.name} to stay near player.");
                    }
                }
            }

            if (alwaysSpawnMaxEnemies)
                EnsureMaxSpawns();

        }

        // Update stopwatch UI
        if (stopwatchText != null)
            stopwatchText.text = FormatTime(elapsedSeconds);
    }

    private void OnDestroy()
    {
        if (IRC.Instance != null)
            IRC.Instance.OnChatMessage -= OnChatMessage;
    }


    private Vector2? FindValidSpawnPosition()
    {
        if (player == null) return null;

        Vector3 spawnPos = player.position;
        int safetyCounter = 0;
        const int maxAttempts = 20;

        while (safetyCounter < maxAttempts)
        {
            safetyCounter++;

            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Mathf.Lerp(minSpawnDistance * minSpawnDistance,
                                            maxSpawnDistance * maxSpawnDistance,
                                            UnityEngine.Random.value));
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * r;
            spawnPos = player.position + offset;

            // Must be on a valid ground/walkable surface
            if (Physics2D.OverlapCircle(spawnPos, spawnCheckRadius, spawnOnLayers) == null)
                continue;

            // NEW: must have line-of-sight from player to spawn (no blockers between)
            RaycastHit2D hit = Physics2D.Linecast(player.position, spawnPos, linecastBlockLayers);

            // If we hit a solid blocker (non-trigger) on the blocking layers, reject this point
            if (hit.collider != null && !hit.collider.isTrigger)
                continue;

            // All checks passed
            return spawnPos;
        }

        Debug.LogWarning("[TwitchListener] Could not find valid spawn position.");
        return null;
    }




    private void OnChatMessage(Chatter chatter)
    {
        if (player == null) return;
        chatters.Add(chatter);

        // EnsureMaxSpawns() (driven from Update) handles spawning/naming in this mode;
        // we still needed to record the chatter above so it has a real name to draw from.
        if (alwaysSpawnMaxEnemies) return;

        var entry = PickWeightedEntry();
        if (entry == null || entry.prefab == null) return;

        int budget = GetChatterPowerBudget(chatter);
        int cost = Mathf.Max(1, entry.power);
        int unitsByBudget = budget / cost;

        // Enforce GLOBAL max active cap = minPower * ratio
        int globalMaxAllowed = Mathf.Max(0, minPower * Mathf.Max(1, maxSpawnPerPowerRatio));
        int globalRemaining = Mathf.Max(0, globalMaxAllowed - spawnedChatters.Count);

        int unitsToSpawn = alwaysSpawnMaxEnemies ? globalRemaining : Mathf.Min(unitsByBudget, globalRemaining);
        for (int i = 0; i < unitsToSpawn; i++)
        {
            string nameOverride = i == 0 ? null : $"{chatter.tags.displayName} ({i + 1})";
            TrySpawnChatter(chatter, entry.prefab, nameOverride);
        }
    }


    private int GetChatterPowerBudget(Chatter chatter)
    {
        int budget = minPower;
        foreach (ChatterBadge b in chatter.tags.badges)
        {
            if (b.id == "subscriber" && int.TryParse(b.version, out int months) && months < 100)
            {
                budget += months;
            }
        }
        return Mathf.Max(0, budget);
    }

    private bool TrySpawnChatter(Chatter chatter, GameObject prefab, string displayNameOverride = null)
    {
        if (player == null) return false;
        if (prefab == null) return false;
        // Use displayName as the base name
        string baseName = chatter?.tags?.displayName ?? string.Empty;

        // Find a valid spawn position
        Vector3? spawnPosNullable = FindValidSpawnPosition();
        if (!spawnPosNullable.HasValue) return false;
        Vector3 spawnPos = spawnPosNullable.Value;

        string finalName = string.IsNullOrEmpty(displayNameOverride) ? baseName : displayNameOverride;
        if (string.IsNullOrEmpty(finalName)) return false;

        GameObject instantiatedChatter = Instantiate(prefab, spawnPos, Quaternion.identity);
        instantiatedChatter.transform.name = finalName;
        spawnedChatters.Add(instantiatedChatter);

        var stats = instantiatedChatter.GetComponent<ChatterStats>();
        if (stats != null)
        {
            if (stats.nameGUI != null)
            {
                stats.nameGUI.text = finalName;
                stats.nameGUI.color = chatter != null ? chatter.GetNameColor() : Color.white;
                // Only hide the tag for generic filler spawns that have no real chatter behind them.
                stats.nameGUI.enabled = chatter != null;
            }
            if (chatter != null)
            {
                foreach (ChatterBadge b in chatter.tags.badges)
                {
                    if (b.id == "subscriber" && int.Parse(b.version) < 100)
                    {
                        stats.power += int.Parse(b.version);
                    }
                }
            }
            stats.power += minPower;
        }

        //var chatterMessage = instantiatedChatter.GetComponent<ChatterMessagePopups>();
        //if (chatterMessage != null)
        //    chatterMessage.ShowMessage(chatter.message);

        Debug.Log($"<color=#fef83e><b>[MESSAGE]</b></color> Spawned ({prefab.name}) for {finalName} at {spawnPos}");
        return true;
    }

    private void EnsureMaxSpawns()
    {
        int globalMaxAllowed = Mathf.Max(0, minPower * Mathf.Max(1, maxSpawnPerPowerRatio));
        int globalMissing = Mathf.Max(0, globalMaxAllowed - spawnedChatters.Count);
        if (globalMissing <= 0) return;

        for (int i = 0; i < globalMissing; i++)
        {
            var entry = PickWeightedEntry();
            if (entry == null || entry.prefab == null) break;

            // Prefer a real Twitch chatter's display name over a generic placeholder.
            Chatter chatter = chatters.Count > 0 ? chatters[(spawnedChatters.Count + i) % chatters.Count] : null;
            string nameOverride = chatter != null ? null : $"Enemy ({spawnedChatters.Count + 1})";
            TrySpawnChatter(chatter, entry.prefab, nameOverride);
        }
    }


    private ChatterSpawnEntry PickWeightedEntry()
    {
        float now = elapsedSeconds; // stopwatch time

        // 1) Sum weights only for entries eligible in [min..max] window
        float total = 0f;
        foreach (var e in chatterPrefabs)
        {
            if (e != null && e.prefab != null && e.weight > 0f && IsEligibleByTime(now, e.timeSpawn))
                total += e.weight;
        }
        if (total <= 0f) return null; // nothing eligible at this time

        // 2) Weighted roll among only eligible entries
        float roll = UnityEngine.Random.value * total;
        float acc = 0f;

        foreach (var e in chatterPrefabs)
        {
            if (e == null || e.prefab == null || e.weight <= 0f) continue;
            if (!IsEligibleByTime(now, e.timeSpawn)) continue;

            acc += e.weight;
            if (roll <= acc)
                return e;
        }

        // 3) Fallback (shouldn't happen if total>0, but safe)
        foreach (var e in chatterPrefabs)
        {
            if (e?.prefab != null && IsEligibleByTime(now, e.timeSpawn))
                return e;
        }
        return null;
    }

    private static bool IsEligibleByTime(float now, Vector2 window)
    {
        // window.x = earliest allowed time, window.y = latest allowed time
        return now >= window.x && now <= window.y;
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int mins = (int)(seconds / 60f);
        int secs = (int)(seconds % 60f);
        return $"{mins:00}:{secs:00}";
    }
}

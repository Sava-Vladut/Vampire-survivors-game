using Lexone.UnityTwitchChat;
using NaughtyAttributes;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Unlock Gate")]
        [Tooltip("Seconds since start before this prefab can join the rotating active type pool.")]
        [Min(0f)] public float unlockTime = 0f;

        [Header("Boss")]
        [Tooltip("If true, this chatter prefab can be selected for timed boss spawns.")]
        public bool bossable = true;
    }

    [Header("Spawn Setup")]
    [SerializeField] private List<ChatterSpawnEntry> chatterPrefabs = new();
    [SerializeField] private Transform player;
    [SerializeField] private float minSpawnDistance = 1.5f;
    [SerializeField] private float maxSpawnDistance = 3.5f;

    [Header("Power Progression")]
    [Tooltip("How often to attempt increasing global min power (seconds)")]
    [SerializeField, Min(0f)] public float spawnIncreaseInterval = 60f;

    [Header("Active Chatter Types")]
    [Tooltip("How often the active chatter prefab types rotate.")]
    [SerializeField, Min(0f)] private float activeTypeRotationInterval = 60f;
    [Tooltip("Minimum number of active chatter prefab types per rotation.")]
    [SerializeField, Min(1)] private int minActiveTypes = 2;
    [Tooltip("Maximum number of active chatter prefab types per rotation.")]
    [SerializeField, Min(1)] private int maxActiveTypes = 3;

    public int minPower = 0; // Minimum power level (also drives global cap)
    [Tooltip("Global max active spawns = minPower * ratio")]
    [Min(1)] public int maxSpawnPerPowerRatio = 3;
    public float chanceToUpgradeMinPower = 0.6f; // Chance to upgrade chatter power on spawn
    [SerializeField] private bool alwaysSpawnMaxEnemies = false;

    [Header("Curse Power Ups")]
    [Tooltip("How much global chatter min power increases when the player chooses a cursed power-up.")]
    [SerializeField, Min(0)] private int cursePowerIncrease = 5;

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

    [Header("Boss Spawns")]
    [Tooltip("How often a boss spawn is queued. If a previous boss is alive, the spawn waits until it dies.")]
    [SerializeField, Min(0f)] private float bossSpawnInterval = 300f;
    [Tooltip("Multiplier applied to the boss prefab's local scale.")]
    [SerializeField, Min(0.01f)] private float bossScaleMultiplier = 1.5f;
    [Tooltip("Multiplier applied to final rolled boss max health.")]
    [SerializeField, Min(1f)] private float bossHealthMultiplier = 10f;
    [Tooltip("Multiplier applied to the boss loot table's final rolled amount.")]
    [SerializeField, Min(1)] private int bossRewardMultiplier = 10;
    [Tooltip("Boss-only outline color applied after MonsterRarity finishes its legendary visual pass.")]
    [SerializeField] private Color bossOutlineColor = new Color(0.95f, 0.1f, 1f, 1f);
    [SerializeField, Min(0f)] private float bossOutlineThickness = 0.08f;
    [SerializeField] private int bossOutlineSortingOrderOffset = -2;

    [Header("Boss UI")]
    [SerializeField] private Slider bossHealthSlider;
    [SerializeField] private TextMeshProUGUI bossHealthText;
    [Tooltip("Optional root to hide/show for the boss health UI. If empty, the slider object is used.")]
    [SerializeField] private GameObject bossHealthBarRoot;
    [Tooltip("Optional camera used to decide whether a boss is visible on screen. If empty, Camera.main is used.")]
    [SerializeField] private Camera bossVisibilityCamera;

    // Stopwatch time
    private float elapsedSeconds = 0f;
    private float nextActiveTypeRotationTime;
    private float nextBossSpawnTime;
    private bool bossSpawnPending;
    private PlayerSafeZoneStatus playerSafeZoneStatus;
    private Transform cachedSafeZonePlayer;
    // Track spawned chatters
    [SerializeField] public List<GameObject> spawnedChatters = new();
    [SerializeField] public List<Chatter> chatters = new();
    private readonly List<ChatterBoss> spawnedBosses = new();
    private readonly List<int> activeChatterEntryIndices = new();
    private readonly List<int> eligibleChatterEntryIndices = new();
    private void Start()
    {
        if (player == null) player = transform;
        if (minSpawnDistance > maxSpawnDistance)
        {
            float t = minSpawnDistance;
            minSpawnDistance = maxSpawnDistance;
            maxSpawnDistance = t;
        }
        if (minActiveTypes > maxActiveTypes)
        {
            int t = minActiveTypes;
            minActiveTypes = maxActiveTypes;
            maxActiveTypes = t;
        }

        if (IRC.Instance != null)
            IRC.Instance.OnChatMessage += OnChatMessage;

        RefreshActiveChatterTypes();
        nextActiveTypeRotationTime = activeTypeRotationInterval > 0f ? activeTypeRotationInterval : float.PositiveInfinity;
        nextBossSpawnTime = bossSpawnInterval > 0f ? bossSpawnInterval : float.PositiveInfinity;
        SetBossHealthBarVisible(false);
    }

    private void Update()
    {

        for (int i = spawnedChatters.Count - 1; i >= 0; i--)
            if (spawnedChatters[i] == null)
                spawnedChatters.RemoveAt(i);

        CleanBossList();

        if (player == null)
        {
            SetBossHealthBarVisible(false);
            return; // ✅ Prevents MissingReferenceException
        }

        bool gameRunning = Time.timeScale > 0f;
        bool timerPausedBySafeZone = IsTimerPausedBySafeZone();

        // Only update stopwatch and timer-gated progression if the game isn't paused
        // and the player is not resting inside a safe zone.
        if (gameRunning && !timerPausedBySafeZone)
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


            if (alwaysSpawnMaxEnemies)
                EnsureMaxSpawns();

            HandleActiveChatterTypeRotation();
            HandleBossSpawning();
        }

        if (gameRunning)
            RepositionFarChatters();

        // Update stopwatch UI
        if (stopwatchText != null)
            stopwatchText.text = FormatTime(elapsedSeconds);

        UpdateBossHealthUI();
    }

    private bool IsTimerPausedBySafeZone()
    {
        PlayerSafeZoneStatus status = GetPlayerSafeZoneStatus();
        return status != null && status.IsSafeZoneActive;
    }

    private PlayerSafeZoneStatus GetPlayerSafeZoneStatus()
    {
        if (player == null)
            return null;

        if (cachedSafeZonePlayer != player)
        {
            cachedSafeZonePlayer = player;
            playerSafeZoneStatus = null;
        }

        if (playerSafeZoneStatus == null)
            playerSafeZoneStatus = player.GetComponentInParent<PlayerSafeZoneStatus>();

        return playerSafeZoneStatus;
    }

    private void RepositionFarChatters()
    {
        for (int i = spawnedChatters.Count - 1; i >= 0; i--)
        {
            GameObject chatterObj = spawnedChatters[i];
            if (chatterObj == null) continue;

            float dist = Vector3.Distance(player.position, chatterObj.transform.position);
            if (dist <= maxDistanceFromPlayer) continue;

            Vector3? newPos = FindValidSpawnPosition();
            if (!newPos.HasValue) continue;

            Rigidbody2D rb = chatterObj.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.position = newPos.Value;
            else
                chatterObj.transform.position = newPos.Value;

            Debug.Log($"[TwitchListener] Repositioned {chatterObj.name} to stay near player.");
        }
    }

    private void OnDestroy()
    {
        if (IRC.Instance != null)
            IRC.Instance.OnChatMessage -= OnChatMessage;
    }

    public void ApplyCursePowerUpPenalty()
    {
        IncreaseOverallChatterPower(cursePowerIncrease);
    }

    public void IncreaseOverallChatterPower(int amount)
    {
        int delta = Mathf.Max(0, amount);
        if (delta <= 0)
            return;

        minPower += delta;
        Debug.Log($"[TwitchListener] Curse increased global chatter min power by {delta}. Min power is now {minPower}.");
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

    private GameObject TrySpawnChatter(Chatter chatter, GameObject prefab, string displayNameOverride = null, bool forceNameVisible = false)
    {
        if (player == null) return null;
        if (prefab == null) return null;
        // Use displayName as the base name
        string baseName = chatter?.tags?.displayName ?? string.Empty;

        // Find a valid spawn position
        Vector3? spawnPosNullable = FindValidSpawnPosition();
        if (!spawnPosNullable.HasValue) return null;
        Vector3 spawnPos = spawnPosNullable.Value;

        string finalName = string.IsNullOrEmpty(displayNameOverride) ? baseName : displayNameOverride;
        if (string.IsNullOrEmpty(finalName)) return null;

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
                stats.nameGUI.enabled = chatter != null || forceNameVisible;
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
        return instantiatedChatter;
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
        EnsureActiveChatterTypesAvailable();

        float total = 0f;
        foreach (int entryIndex in activeChatterEntryIndices)
        {
            var e = GetSpawnEntry(entryIndex);
            if (IsEntryEligible(e))
                total += e.weight;
        }
        if (total <= 0f) return null;

        float roll = UnityEngine.Random.value * total;
        float acc = 0f;

        foreach (int entryIndex in activeChatterEntryIndices)
        {
            var e = GetSpawnEntry(entryIndex);
            if (!IsEntryEligible(e)) continue;

            acc += e.weight;
            if (roll <= acc)
                return e;
        }

        foreach (int entryIndex in activeChatterEntryIndices)
        {
            var e = GetSpawnEntry(entryIndex);
            if (IsEntryEligible(e))
                return e;
        }

        return null;
    }

    private void HandleActiveChatterTypeRotation()
    {
        if (activeTypeRotationInterval <= 0f)
            return;

        while (elapsedSeconds >= nextActiveTypeRotationTime)
        {
            RefreshActiveChatterTypes();
            nextActiveTypeRotationTime += activeTypeRotationInterval;
        }
    }

    private void EnsureActiveChatterTypesAvailable()
    {
        if (activeChatterEntryIndices.Count <= 0)
            RefreshActiveChatterTypes();
    }

    private void RefreshActiveChatterTypes()
    {
        activeChatterEntryIndices.Clear();
        eligibleChatterEntryIndices.Clear();

        if (chatterPrefabs == null || chatterPrefabs.Count == 0)
            return;

        for (int i = 0; i < chatterPrefabs.Count; i++)
        {
            if (IsEntryEligible(chatterPrefabs[i]))
                eligibleChatterEntryIndices.Add(i);
        }

        if (eligibleChatterEntryIndices.Count == 0)
            return;

        int targetCount = UnityEngine.Random.Range(minActiveTypes, maxActiveTypes + 1);
        if (eligibleChatterEntryIndices.Count < minActiveTypes)
            targetCount = eligibleChatterEntryIndices.Count;
        else
            targetCount = Mathf.Min(targetCount, eligibleChatterEntryIndices.Count);

        for (int i = 0; i < targetCount; i++)
        {
            int candidateListIndex = PickWeightedCandidateIndex(eligibleChatterEntryIndices);
            if (candidateListIndex < 0)
                break;

            activeChatterEntryIndices.Add(eligibleChatterEntryIndices[candidateListIndex]);
            eligibleChatterEntryIndices.RemoveAt(candidateListIndex);
        }
    }

    private int PickWeightedCandidateIndex(List<int> candidateIndices)
    {
        if (candidateIndices == null || candidateIndices.Count == 0)
            return -1;

        float total = 0f;
        foreach (int entryIndex in candidateIndices)
        {
            var entry = GetSpawnEntry(entryIndex);
            if (IsEntryEligible(entry))
                total += entry.weight;
        }

        if (total <= 0f)
            return -1;

        float roll = UnityEngine.Random.value * total;
        float acc = 0f;

        for (int i = 0; i < candidateIndices.Count; i++)
        {
            var entry = GetSpawnEntry(candidateIndices[i]);
            if (!IsEntryEligible(entry))
                continue;

            acc += entry.weight;
            if (roll <= acc)
                return i;
        }

        for (int i = candidateIndices.Count - 1; i >= 0; i--)
        {
            if (IsEntryEligible(GetSpawnEntry(candidateIndices[i])))
                return i;
        }

        return -1;
    }

    private void HandleBossSpawning()
    {
        if (bossSpawnInterval <= 0f)
            return;

        while (elapsedSeconds >= nextBossSpawnTime)
        {
            bossSpawnPending = true;
            nextBossSpawnTime += bossSpawnInterval;
        }

        if (!bossSpawnPending || HasLivingBoss())
            return;

        var entry = PickWeightedBossEntry();
        if (entry == null || entry.prefab == null)
            return;

        Chatter chatter = chatters.Count > 0 ? chatters[UnityEngine.Random.Range(0, chatters.Count)] : null;
        string bossDisplayName = chatter != null && chatter.tags != null && !string.IsNullOrWhiteSpace(chatter.tags.displayName)
            ? chatter.tags.displayName
            : "Boss";
        string displayName = bossDisplayName == "Boss" ? "Boss" : $"{bossDisplayName} (Boss)";

        GameObject bossObject = TrySpawnChatter(chatter, entry.prefab, displayName, true);
        if (bossObject == null)
            return;

        ConfigureBoss(bossObject, bossDisplayName);
        bossSpawnPending = false;
    }

    private ChatterSpawnEntry PickWeightedBossEntry()
    {
        EnsureActiveChatterTypesAvailable();

        float total = 0f;
        foreach (int entryIndex in activeChatterEntryIndices)
        {
            var e = GetSpawnEntry(entryIndex);
            if (IsBossEntryEligible(e))
                total += e.weight;
        }

        if (total <= 0f)
            return null;

        float roll = UnityEngine.Random.value * total;
        float acc = 0f;

        foreach (int entryIndex in activeChatterEntryIndices)
        {
            var e = GetSpawnEntry(entryIndex);
            if (!IsBossEntryEligible(e))
                continue;

            acc += e.weight;
            if (roll <= acc)
                return e;
        }

        foreach (int entryIndex in activeChatterEntryIndices)
        {
            var e = GetSpawnEntry(entryIndex);
            if (IsBossEntryEligible(e))
                return e;
        }

        return null;
    }

    private bool IsBossEntryEligible(ChatterSpawnEntry entry)
    {
        return IsEntryEligible(entry)
            && entry.bossable
            && IsEntryActive(entry);
    }

    private bool IsEntryEligible(ChatterSpawnEntry entry)
    {
        return entry != null
            && entry.prefab != null
            && entry.weight > 0f
            && elapsedSeconds >= entry.unlockTime;
    }

    private bool IsEntryActive(ChatterSpawnEntry entry)
    {
        if (entry == null || chatterPrefabs == null)
            return false;

        for (int i = 0; i < activeChatterEntryIndices.Count; i++)
        {
            if (GetSpawnEntry(activeChatterEntryIndices[i]) == entry)
                return true;
        }

        return false;
    }

    private ChatterSpawnEntry GetSpawnEntry(int index)
    {
        if (chatterPrefabs == null || index < 0 || index >= chatterPrefabs.Count)
            return null;

        return chatterPrefabs[index];
    }

    private void ConfigureBoss(GameObject bossObject, string bossDisplayName)
    {
        if (bossObject == null)
            return;

        SimpleHealth health = bossObject.GetComponentInChildren<SimpleHealth>();
        ChatterBoss boss = bossObject.GetComponent<ChatterBoss>();
        if (boss == null)
            boss = bossObject.AddComponent<ChatterBoss>();

        boss.Initialize(health, bossDisplayName);
        spawnedBosses.Add(boss);

        LootTable2D loot = bossObject.GetComponentInChildren<LootTable2D>();
        if (loot != null)
            loot.SetDropMultiplier(bossRewardMultiplier);

        if (health != null)
            StartCoroutine(ApplyBossSetupAfterRarity(health, boss));
    }

    private System.Collections.IEnumerator ApplyBossSetupAfterRarity(SimpleHealth health, ChatterBoss boss)
    {
        yield return null;

        if (health == null)
            yield break;

        GameObject bossObject = boss != null ? boss.gameObject : health.gameObject;
        MonsterRarity rarity = bossObject.GetComponentInChildren<MonsterRarity>();
        if (rarity != null && (boss == null || !boss.LegendaryApplied))
        {
            rarity.ForceRarity(MonsterRarity.Rarity.Legendary);
            boss?.MarkLegendaryApplied();
        }

        ApplyBossScale(bossObject);
        ApplyBossOutline(bossObject);

        int bossMaxHealth = Mathf.Max(1, Mathf.RoundToInt(health.maxHealth * bossHealthMultiplier));
        health.maxHealth = bossMaxHealth;
        health.currentHealth = bossMaxHealth;
        health.SyncSlider();
        health.UpdateStatsText();

        if (boss != null)
            boss.Initialize(health, boss.DisplayName);

        UpdateBossHealthUI();
    }

    private void ApplyBossScale(GameObject bossObject)
    {
        if (bossObject == null || bossScaleMultiplier <= 0f)
            return;

        bossObject.transform.localScale *= bossScaleMultiplier;
    }

    private void ApplyBossOutline(GameObject bossObject)
    {
        if (bossObject == null || bossOutlineThickness <= 0f || bossOutlineColor.a <= 0f)
            return;

        SpriteRenderer[] renderers = bossObject.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || IsGeneratedRarityOutline(renderer))
                continue;

            RaritySpriteOutline2D outline = renderer.GetComponent<RaritySpriteOutline2D>();
            if (outline == null)
                outline = renderer.gameObject.AddComponent<RaritySpriteOutline2D>();

            outline.Configure(renderer, bossOutlineColor, bossOutlineThickness, bossOutlineSortingOrderOffset);
        }
    }

    private static bool IsGeneratedRarityOutline(SpriteRenderer renderer)
    {
        return renderer.transform.parent != null
            && renderer.name.StartsWith(RaritySpriteOutline2D.OutlineChildName, System.StringComparison.Ordinal);
    }

    private bool HasLivingBoss()
    {
        CleanBossList();

        foreach (var boss in spawnedBosses)
        {
            if (IsLivingBoss(boss))
                return true;
        }

        return false;
    }

    private void CleanBossList()
    {
        for (int i = spawnedBosses.Count - 1; i >= 0; i--)
        {
            if (spawnedBosses[i] == null || !IsLivingBoss(spawnedBosses[i]))
                spawnedBosses.RemoveAt(i);
        }
    }

    private static bool IsLivingBoss(ChatterBoss boss)
    {
        return boss != null
            && boss.gameObject != null
            && boss.gameObject.activeInHierarchy
            && boss.Health != null
            && boss.Health.IsAlive;
    }

    private void UpdateBossHealthUI()
    {
        ChatterBoss visibleBoss = GetVisibleBoss();
        if (visibleBoss == null || visibleBoss.Health == null)
        {
            SetBossHealthBarVisible(false);
            return;
        }

        SimpleHealth visibleBossHealth = visibleBoss.Health;
        if (bossHealthSlider != null)
        {
            bossHealthSlider.minValue = 0f;
            bossHealthSlider.maxValue = visibleBossHealth.maxHealth;
            bossHealthSlider.value = Mathf.Clamp(visibleBossHealth.currentHealth, 0f, bossHealthSlider.maxValue);
        }

        if (bossHealthText != null)
            bossHealthText.text = $"{EscapeRichText(visibleBoss.DisplayName)} {Mathf.RoundToInt(visibleBossHealth.currentHealth)}/{visibleBossHealth.maxHealth}";

        SetBossHealthBarVisible(true);
    }

    private ChatterBoss GetVisibleBoss()
    {
        Camera cam = bossVisibilityCamera != null ? bossVisibilityCamera : Camera.main;
        if (cam == null)
            return null;

        foreach (var boss in spawnedBosses)
        {
            if (!IsLivingBoss(boss))
                continue;

            if (IsBossVisibleOnCamera(boss.gameObject, cam))
                return boss;
        }

        return null;
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private static bool IsBossVisibleOnCamera(GameObject bossObject, Camera cam)
    {
        if (bossObject == null || cam == null)
            return false;

        Renderer[] renderers = bossObject.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return IsWorldPointVisibleOnCamera(bossObject.transform.position, cam);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (IsBoundsVisibleOnCamera(renderer.bounds, cam))
                return true;
        }

        return false;
    }

    private static bool IsBoundsVisibleOnCamera(Bounds bounds, Camera cam)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        return IsWorldPointVisibleOnCamera(bounds.center, cam)
            || IsWorldPointVisibleOnCamera(new Vector3(min.x, min.y, min.z), cam)
            || IsWorldPointVisibleOnCamera(new Vector3(min.x, min.y, max.z), cam)
            || IsWorldPointVisibleOnCamera(new Vector3(min.x, max.y, min.z), cam)
            || IsWorldPointVisibleOnCamera(new Vector3(min.x, max.y, max.z), cam)
            || IsWorldPointVisibleOnCamera(new Vector3(max.x, min.y, min.z), cam)
            || IsWorldPointVisibleOnCamera(new Vector3(max.x, min.y, max.z), cam)
            || IsWorldPointVisibleOnCamera(new Vector3(max.x, max.y, min.z), cam)
            || IsWorldPointVisibleOnCamera(new Vector3(max.x, max.y, max.z), cam);
    }

    private static bool IsWorldPointVisibleOnCamera(Vector3 worldPoint, Camera cam)
    {
        Vector3 viewportPoint = cam.WorldToViewportPoint(worldPoint);
        return viewportPoint.z >= cam.nearClipPlane
            && viewportPoint.z <= cam.farClipPlane
            && viewportPoint.x >= 0f
            && viewportPoint.x <= 1f
            && viewportPoint.y >= 0f
            && viewportPoint.y <= 1f;
    }

    private void SetBossHealthBarVisible(bool visible)
    {
        GameObject target = bossHealthBarRoot;
        if (target == null && bossHealthSlider != null)
            target = bossHealthSlider.gameObject;
        if (target == null && bossHealthText != null)
            target = bossHealthText.gameObject;

        if (target != null && target.activeSelf != visible)
            target.SetActive(visible);
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int mins = (int)(seconds / 60f);
        int secs = (int)(seconds % 60f);
        return $"{mins:00}:{secs:00}";
    }
}

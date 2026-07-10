using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight status effect system with add/remove/has, events, stacking, and ticking.
/// Re-adding the same effect adds a stack up to its cap and refreshes the shared duration.
/// Includes built-in Bleeding that damages a SimpleHealth component each tick.
/// </summary>
[AddComponentMenu("Gameplay/Status Effect System")]
public class StatusEffectSystem : MonoBehaviour
{
    // ---- Public API ----
    public enum StatusType
    {
        Bleeding = 0,
        Stun = 1,
        Speed = 2,
        Rush = 3,
        Ignite = 4,
        Shock = 5,
        Poison = 6,
        Frozen = 7,
        Regeneration = 8,
        XpBoost = 9,
        Slow = 10,
        Fear = 11,
        Cursed = 12,
        Onslaught = 13,
        // Add more: Poison, Stunned, Shielded, etc.
    }

    /// <summary>Events for effect lifecycle and ticks.</summary>
    public event Action<StatusType> OnStart;
    public event Action<StatusType> OnEnd;
    public event Action<StatusType, int> OnTick; // tick index (1-based) for that effect
    public event Action<StatusType, int> OnStatusUpdated; // current stack count after add/refresh

    [Serializable]
    public class StackLimit
    {
        public StatusType type;
        [Min(1)] public int maxStacks = 5;
    }

    [Header("Time")]
    [Tooltip("If true, uses unscaled time (ignores slow-mo/pauses).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Bleeding")]
    [Tooltip("If true, Bleeding ticks will call SimpleHealth.TakeDamage().")]
    [SerializeField] private bool enableBleeding = true;

    [Tooltip("Damage applied per tick while Bleeding is active (rounded to int).")]
    [SerializeField] public float bleedingDamagePerTick = 5f;

    [Header("Ignite")]
    [Tooltip("If true, Ignite ticks will call SimpleHealth.TakeDamage().")]
    [SerializeField] private bool enableIgnite = true;

    [Tooltip("Damage applied per tick while Ignite is active (rounded to int).")]
    [SerializeField] public float igniteDamagePerTick = 5f;

    [Header("Poison")]
    [Tooltip("If true, Poison ticks will call SimpleHealth.TakeDamage().")]
    [SerializeField] private bool enablePoison = true;

    [Tooltip("Damage applied per tick while Poison is active (rounded to int).")]
    [SerializeField] public float poisonDamagePerTick = 5f;

    [Tooltip("Optional: target health. If not set, auto-finds on this GameObject.")]
    [SerializeField] private SimpleHealth health; // your health system

    [Header("Regeneration")]
    [Tooltip("If true, Regeneration ticks will call SimpleHealth.Heal().")]
    [SerializeField] private bool enableRegeneration = true;

    [Tooltip("Healing applied per tick while Regeneration is active (rounded to int).")]
    [SerializeField] public float regenerationPerTick = 5f;

    [Header("Shock")]
    [Tooltip("Incoming damage multiplier at one Shock stack.")]
    [SerializeField, Min(0f)] private float shockDamageTakenMultiplier = 2f;

    [Header("XP Boost")]
    [Tooltip("If true, applying the XpBoost status adjusts XP rewards while active.")]
    [SerializeField] private bool enableXpBoost = true;
    [Tooltip("Multiplier applied to XP gain while XpBoost is active.")]
    [SerializeField, Min(0f)] private float xpBoostMultiplier = 2f;

    [Header("Movement Modifiers")]
    [Tooltip("Movement multiplier while Speed is active.")]
    [SerializeField, Min(0f)] private float speedMoveMultiplier = 2f;
    [Tooltip("Movement multiplier while Slow is active.")]
    [SerializeField, Min(0f)] private float slowMoveMultiplier = 0.5f;

    [Header("Onslaught")]
    [Tooltip("Attack speed multiplier while Onslaught is active.")]
    [SerializeField, Min(0f)] private float onslaughtAttackSpeedMultiplier = 1.50f;
    [Tooltip("Movement multiplier while Onslaught is active.")]
    [SerializeField, Min(0f)] private float onslaughtMoveMultiplier = 1.10f;

    [Header("Curse")]
    [Tooltip("Healing received multiplier while Cursed is active.")]
    [SerializeField, Min(0f)] private float cursedHealingReceivedMultiplier = 0.5f;
    [Tooltip("Incoming status duration multiplier while Cursed is active.")]
    [SerializeField, Min(0f)] private float cursedStatusDurationMultiplier = 1.5f;

    [Header("Stack Limits")]
    [Tooltip("Per-status stack caps. Missing entries use 1 for hard control and 5 for other statuses.")]
    [SerializeField] private List<StackLimit> stackLimits = new List<StackLimit>();

    [Header("UnityEvent Defaults")]
    [SerializeField, Min(0.01f)] private float defaultDuration = 5f;
    [SerializeField, Min(0f)] private float defaultTickInterval = 1f;


    // ---- Internal model ----
    private class Effect
    {
        public StatusType type;
        public float remaining;        // seconds left
        public float tickInterval;     // seconds between ticks
        public float tickTimer;        // accumulates until tick
        public int tickCount;          // number of ticks fired for this activation
        public int stackCount;         // stacks sharing this activation's duration and cadence
    }

    private readonly Dictionary<StatusType, Effect> _active = new Dictionary<StatusType, Effect>(8);
    private readonly Dictionary<StatusType, GameObject> _sources = new Dictionary<StatusType, GameObject>(8);
    private static readonly List<StatusType> _keysCache = new List<StatusType>(8);
    private enum BuiltInEvent { Started, Refreshed, Tick, Ended }

    public float CurrentXpMultiplier => enableXpBoost ? GetScaledMultiplier(StatusType.XpBoost, xpBoostMultiplier) : 1f;
    public float HealingReceivedMultiplier => GetScaledMultiplier(StatusType.Cursed, cursedHealingReceivedMultiplier);
    public float StatusDurationReceivedMultiplier => GetScaledMultiplier(StatusType.Cursed, cursedStatusDurationMultiplier);
    public float AttackSpeedMultiplier => GetScaledMultiplier(StatusType.Onslaught, onslaughtAttackSpeedMultiplier);
    public float IncomingDamageMultiplier => GetScaledMultiplier(StatusType.Shock, shockDamageTakenMultiplier);

    public float MovementSpeedMultiplier
    {
        get
        {
            if (HasStatus(StatusType.Stun) || HasStatus(StatusType.Frozen))
                return 0f;

            float multiplier = 1f;
            if (HasStatus(StatusType.Speed))
                multiplier *= GetScaledMultiplier(StatusType.Speed, speedMoveMultiplier);
            if (HasStatus(StatusType.Slow))
                multiplier *= GetScaledMultiplier(StatusType.Slow, slowMoveMultiplier);
            if (HasStatus(StatusType.Onslaught))
                multiplier *= GetScaledMultiplier(StatusType.Onslaught, onslaughtMoveMultiplier);

            return multiplier;
        }
    }

    private void Awake()
    {
        if (health == null)
            health = GetComponent<SimpleHealth>();
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        AdvanceEffects(dt);
    }

    private void AdvanceEffects(float dt)
    {
        if (dt <= 0f || _active.Count == 0) return;

        _keysCache.Clear();
        _keysCache.AddRange(_active.Keys);

        foreach (var type in _keysCache)
        {
            var e = _active[type];
            e.remaining -= dt;
            e.tickTimer += dt;

            // Fire all ticks that fit this frame
            while (e.tickInterval > 0f && e.tickTimer >= e.tickInterval && e.remaining > 0f)
            {
                e.tickTimer -= e.tickInterval;
                e.tickCount++;
                OnTick?.Invoke(type, e.tickCount);
                HandleBuiltIn(type, BuiltInEvent.Tick);
            }

            if (e.remaining <= 0f)
            {
                _active.Remove(type);
                _sources.Remove(type);
                HandleBuiltIn(type, BuiltInEvent.Ended);
                OnEnd?.Invoke(type);
            }
        }
    }

    /// <summary>
    /// UnityEvent-friendly: apply by enum int (cast). 
    /// In the Inspector you can pass an int for the enum (0=Bleeding, 1=Stun, 2=Speed...).
    /// Uses defaultDuration and defaultTickInterval.
    /// </summary>
    public void ApplyStatusEffect_Int(int statusTypeInt)
    {
        var type = (StatusType)Mathf.Clamp(statusTypeInt, 0, Enum.GetNames(typeof(StatusType)).Length - 1);
        AddStatus(type, defaultDuration, defaultTickInterval);
    }

    public void ApplyStatusEffect_Int(int statusTypeInt, float time)
    {
        var type = (StatusType)Mathf.Clamp(statusTypeInt, 0, Enum.GetNames(typeof(StatusType)).Length - 1);
        AddStatus(type, time, defaultTickInterval);
    }


    /// <summary>
    /// Adds or stacks a status effect.
    /// If already active, adds a stack up to its cap and resets remaining time to 'duration'.
    /// Tick cadence is preserved and OnStart does not fire again on refresh.
    /// </summary>

    public void AddStatus(StatusType type, float duration, float tickInterval = 1f, GameObject sourceObject = null)
    {
        duration = Mathf.Max(0f, duration * GetIncomingDurationMultiplier(type));
        if (duration <= 0f) return;

        if (_active.TryGetValue(type, out var e))
        {
            e.stackCount = Mathf.Min(e.stackCount + 1, GetMaxStacks(type));
            e.remaining = duration;
            // Keep tickInterval, tickTimer, and tickCount from the first application.
            if (sourceObject != null)
                _sources[type] = sourceObject;
            HandleBuiltIn(type, BuiltInEvent.Refreshed);
            OnStatusUpdated?.Invoke(type, e.stackCount);
        }
        else
        {
            e = new Effect
            {
                type = type,
                remaining = duration,
                tickInterval = Mathf.Max(0f, tickInterval),
                tickTimer = 0f,
                tickCount = 0,
                stackCount = 1
            };
            _active.Add(type, e);
            if (sourceObject != null)
                _sources[type] = sourceObject;
            OnStart?.Invoke(type);
            HandleBuiltIn(type, BuiltInEvent.Started);
            OnStatusUpdated?.Invoke(type, e.stackCount);
        }
    }


    /// <summary>Remove a status immediately (fires OnEnd).</summary>
    public void RemoveStatus(StatusType type)
    {
        if (_active.Remove(type))
        {
            _sources.Remove(type);
            HandleBuiltIn(type, BuiltInEvent.Ended);
            OnEnd?.Invoke(type);
        }
    }

    /// <summary>True if the status is currently active.</summary>
    public bool HasStatus(StatusType type) => _active.ContainsKey(type);

    /// <summary>Remaining seconds for a status (0 if not active).</summary>
    public float GetRemainingTime(StatusType type)
    {
        return _active.TryGetValue(type, out var e) ? Mathf.Max(0f, e.remaining) : 0f;
    }

    /// <summary>Clears all statuses (fires OnEnd for each).</summary>
    public void ClearAll()
    {
        _keysCache.Clear();
        _keysCache.AddRange(_active.Keys);
        foreach (var k in _keysCache)
        {
            HandleBuiltIn(k, BuiltInEvent.Ended);
            OnEnd?.Invoke(k);
        }
        _active.Clear();
        _sources.Clear();
    }

    /// <summary>Current stack count for a status (0 if not active).</summary>
    public int GetStackCount(StatusType type)
    {
        return _active.TryGetValue(type, out var e) ? Mathf.Max(0, e.stackCount) : 0;
    }

    /// <summary>Configured stack cap for a status.</summary>
    public int GetMaxStacks(StatusType type)
    {
        for (int i = 0; stackLimits != null && i < stackLimits.Count; i++)
        {
            StackLimit limit = stackLimits[i];
            if (limit != null && limit.type == type)
                return Mathf.Max(1, limit.maxStacks);
        }

        return IsHardControl(type) ? 1 : 5;
    }


    /// <summary>
    /// Sets bleeding damage per tick. 
    /// If the given amount is higher than the current bleed damage, it replaces it.
    /// </summary>
    public void SetBleedDamage(float amount)
    {
        if (amount <= 0f) return;

        if (amount > bleedingDamagePerTick)
            bleedingDamagePerTick = amount;
    }

    /// <summary>
    /// Sets ignite damage per tick.
    /// If the given amount is higher than the current ignite damage, it replaces it.
    /// </summary>
    public void SetIgniteDamage(float amount)
    {
        if (amount <= 0f) return;

        if (amount > igniteDamagePerTick)
            igniteDamagePerTick = amount;
    }

    /// <summary>
    /// Sets poison damage per tick.
    /// If the given amount is higher than the current poison damage, it replaces it.
    /// </summary>
    public void SetPoisonDamage(float amount)
    {
        if (amount <= 0f) return;

        if (amount > poisonDamagePerTick)
            poisonDamagePerTick = amount;
    }

    /// <summary>
    /// Sets regeneration heal per tick.
    /// If the given amount is higher than the current regeneration amount, it replaces it.
    /// </summary>
    public void SetRegenerationAmount(float amount)
    {
        if (amount <= 0f) return;

        if (amount > regenerationPerTick)
            regenerationPerTick = amount;
    }

    // ---- Built-in handlers ----
    private void HandleBuiltIn(StatusType type, BuiltInEvent evt)
    {
        if (evt != BuiltInEvent.Tick)
            return;

        int stacks = GetStackCount(type);

        if (type == StatusType.Bleeding && enableBleeding && bleedingDamagePerTick > 0f)
        {
            if (health != null)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(bleedingDamagePerTick * stacks));
                // Uses your health system's public API:
                // SimpleHealth.TakeDamage(int amount)
                health.TakeDamage(dmg, SimpleHealth.DamageType.Physical, false, false, GetSource(type), "Bleeding"); // will handle armor, invuln, popup, etc.
            }
            // If no health found, we silently skip (no debug spam).
        }

        if (type == StatusType.Ignite && enableIgnite && igniteDamagePerTick > 0f)
        {
            if (health != null)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(igniteDamagePerTick * stacks));
                // Uses your health system's public API:
                // SimpleHealth.TakeDamage(int amount)
                health.TakeDamage(dmg, SimpleHealth.DamageType.Fire, false, false, GetSource(type), "Ignite");
            }
            // If no health found, we silently skip (no debug spam).
        }

        if (type == StatusType.Poison && enablePoison && poisonDamagePerTick > 0f)
        {
            if (health != null)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(poisonDamagePerTick * stacks));
                // Uses your health system's public API:
                // SimpleHealth.TakeDamage(int amount)
                health.TakeDamage(dmg, SimpleHealth.DamageType.Poison, false, false, GetSource(type), "Poison");
            }
            // If no health found, we silently skip (no debug spam).
        }

        if (type == StatusType.Regeneration && enableRegeneration && regenerationPerTick > 0f)
        {
            if (health != null)
            {
                int heal = Mathf.Max(1, Mathf.RoundToInt(regenerationPerTick * stacks));
                // Uses your health system's public API:
                // SimpleHealth.Heal(int amount)
                health.Heal(heal);
            }
            // If no health found, we silently skip (no debug spam).
        }
    }

    private GameObject GetSource(StatusType type)
    {
        return _sources.TryGetValue(type, out GameObject source) ? source : null;
    }

    private float GetScaledMultiplier(StatusType type, float configuredMultiplier)
    {
        int stacks = GetStackCount(type);
        if (stacks <= 0)
            return 1f;

        return Mathf.Max(0f, 1f + (configuredMultiplier - 1f) * stacks);
    }

    private static bool IsHardControl(StatusType type)
    {
        return type == StatusType.Stun || type == StatusType.Frozen || type == StatusType.Fear;
    }

    private float GetIncomingDurationMultiplier(StatusType type)
    {
        if (type == StatusType.Cursed)
            return 1f;

        return StatusDurationReceivedMultiplier;
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Add Bleeding (5s, 1s tick)")]
    private void _TestAddBleeding() => AddStatus(StatusType.Bleeding, 5f, 1f);

    [ContextMenu("Test: Add Ignite (5s, 1s tick)")]
    private void _TestAddIgnite() => AddStatus(StatusType.Ignite, 5f, 1f);

    [ContextMenu("Test: Add Poison (5s, 1s tick)")]
    private void _TestAddPoison() => AddStatus(StatusType.Poison, 5f, 1f);

    [ContextMenu("Test: Add Regeneration (5s, 1s tick)")]
    private void _TestAddRegen() => AddStatus(StatusType.Regeneration, 5f, 1f);

    [ContextMenu("Test: Add Onslaught (5s)")]
    private void _TestAddOnslaught() => AddStatus(StatusType.Onslaught, 5f, 1f);
#endif
}

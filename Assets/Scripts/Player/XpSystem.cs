using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class XpSystem : MonoBehaviour
{
    public enum CurveType { Linear, Quadratic, Exponential, CustomPerLevel }

    [Header("Level State")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int currentXpInLevel = 0;
    [SerializeField] private int maxLevel = 100;

    [Header("Curve Settings")]
    [SerializeField] private CurveType curve = CurveType.Exponential;
    [SerializeField] private int baseNextLevelXp = 100;
    [SerializeField] private int linearStep = 25;
    [SerializeField] private int quadA = 15;
    [SerializeField] private float expGrowth = 1.25f;
    [SerializeField] private AnimationCurve customNextLevelXp = AnimationCurve.Linear(1, 100, 100, 1000);

    [Header("UI Display")]
    [Tooltip("Optional: slider showing current XP progress in this level.")]
    [SerializeField] private Slider xpSliderUI;
    [SerializeField] private TextMeshProUGUI levelText;

    [Serializable] public class LevelUpEvent : UnityEvent<int> { }
    public LevelUpEvent OnLevelUp;

    [Serializable]
    public class LevelUnityEvent
    {
        [Min(1)] public int level = 1;
        public UnityEvent onReached;
        public bool fireOnce = true;

        [HideInInspector] public bool fired;
    }

    [Header("Level Events")]
    [Tooltip("Events that trigger when reaching specific levels.")]
    public List<LevelUnityEvent> levelEvents = new List<LevelUnityEvent>();

    private PowerUpSelectionUI PUSUI;
    [SerializeField] private StatusEffectSystem statusEffects;
    private PlayerAccessoryStats accessoryStats;

    // Selection queue machinery
    private int pendingSelections = 0;
    private Coroutine selectionRunner;

    public int CurrentLevel => currentLevel;
    public int CurrentXpInLevel => currentXpInLevel;
    public int XpNeededThisLevel => GetXpRequiredForNextLevel(currentLevel);
    public float Progress01 => XpNeededThisLevel > 0 ? (float)currentXpInLevel / XpNeededThisLevel : 1f;
    public bool IsMaxLevel => currentLevel >= maxLevel;
    public int RemainingXpThisLevel => Mathf.Max(0, GetXpRequiredForNextLevel(currentLevel) - currentXpInLevel);

    private void Awake()
    {
        var gc = GameObject.FindGameObjectWithTag("GameController");
        if (gc != null) PUSUI = gc.GetComponent<PowerUpSelectionUI>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (statusEffects == null && player != null) statusEffects = player.GetComponent<StatusEffectSystem>();
        if (player != null) accessoryStats = PlayerAccessoryStats.Find(player.transform);
    }

    private void Update()
    {
        UpdateXpUI();
    }

    /// <summary>
    /// Your per-level reward. Still called once per level gained.
    /// </summary>
    public void LevelUp()
    {
        // Put custom reward logic here
    }

    /// <summary>
    /// Adds XP, handles multi-level gains, and queues one selection per level.
    /// Returns how many levels were gained.
    /// </summary>
    public int AddExperience(int amount)
    {
        if (amount <= 0 || IsMaxLevel) return 0;

        amount = ApplyXpBoost(amount);
        if (amount <= 0) return 0;

        int levelsGained = 0;
        int safety = 1000;
        int prevLevel = currentLevel;

        while (amount > 0 && !IsMaxLevel && safety-- > 0)
        {
            int needed = GetXpRequiredForNextLevel(currentLevel);
            int remaining = needed - currentXpInLevel;

            if (amount < remaining)
            {
                currentXpInLevel += amount;
                amount = 0;
                break;
            }

            // consume enough XP to finish this level
            amount -= remaining;

            // level up
            currentLevel = Mathf.Min(currentLevel + 1, maxLevel);
            currentXpInLevel = 0;

            levelsGained++;
            OnLevelUp?.Invoke(currentLevel);
            LevelUp();

            InvokeLevelEvents(currentLevel);
        }

        if (IsMaxLevel) currentXpInLevel = 0;

        // Queue exactly one selection per level gained
        if (levelsGained > 0)
        {
            pendingSelections += levelsGained;
            if (selectionRunner == null)
                selectionRunner = StartCoroutine(RunSelectionQueue());
        }

        UpdateXpUI();
        return levelsGained;
    }

    public void SetLevel(int level, bool resetXpInLevel = true)
    {
        int oldLevel = currentLevel;

        currentLevel = Mathf.Clamp(level, 1, maxLevel);
        if (resetXpInLevel)
            currentXpInLevel = 0;
        else
            currentXpInLevel = Mathf.Clamp(currentXpInLevel, 0, GetXpRequiredForNextLevel(currentLevel));

        // Fire events for levels crossed
        if (currentLevel > oldLevel)
        {
            for (int L = oldLevel + 1; L <= currentLevel; L++)
                InvokeLevelEvents(L);
        }

        UpdateXpUI();
    }

    public int GetXpRequiredForNextLevel(int level)
    {
        level = Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));

        switch (curve)
        {
            case CurveType.Linear:
                return Mathf.Max(1, baseNextLevelXp + linearStep * (level - 1));

            case CurveType.Quadratic:
                return Mathf.Max(1, baseNextLevelXp + quadA * (level - 1) * (level - 1));

            case CurveType.Exponential:
                return Mathf.Max(1, Mathf.RoundToInt(baseNextLevelXp * Mathf.Pow(expGrowth, level - 1)));

            case CurveType.CustomPerLevel:
                return Mathf.Max(1, Mathf.RoundToInt(customNextLevelXp.Evaluate(level)));

            default:
                return baseNextLevelXp;
        }
    }

    public int GetTotalXpToReachLevel(int targetLevel)
    {
        targetLevel = Mathf.Clamp(targetLevel, 1, maxLevel);
        int sum = 0;
        for (int L = 1; L < targetLevel; L++)
            sum += GetXpRequiredForNextLevel(L);
        return sum;
    }

    private void UpdateXpUI()
    {
        if (levelText != null)
        {
            if (IsMaxLevel)
            {
                levelText.text = $"Level {currentLevel} (MAX)";
            }
            else
            {
                int needed = XpNeededThisLevel;
                int have = currentXpInLevel;
                int remain = RemainingXpThisLevel;
                float pct = needed > 0 ? (have / (float)needed) : 1f;

                levelText.text = $"Level {currentLevel} - {have}/{needed} XP ({pct:P0}) - {remain} XP to next";
            }
        }

        if (xpSliderUI != null)
        {
            xpSliderUI.maxValue = XpNeededThisLevel;
            xpSliderUI.value = currentXpInLevel;
        }
    }

    private void InvokeLevelEvents(int reachedLevel)
    {
        if (levelEvents == null) return;

        foreach (var ev in levelEvents)
        {
            if (ev == null) continue;
            if (ev.level == reachedLevel)
            {
                if (ev.fireOnce && ev.fired) continue;

                ev.onReached?.Invoke();
                ev.fired = true;
            }
        }
    }

    /// <summary>
    /// Sequentially opens PowerUp selection exactly once per pending level-up.
    /// Waits for the panel to close (Time.timeScale restored) between opens.
    /// </summary>
    private IEnumerator RunSelectionQueue()
    {
        // If selection is already open (paused), wait until it closes
        while (Time.timeScale == 0f) yield return null;

        while (pendingSelections > 0)
        {
            if (PUSUI != null)
            {
                PUSUI.ShowSelection();

                // Wait while open (paused)
                while (Time.timeScale == 0f)
                    yield return null;

                // Slight delay to avoid same-frame reopen issues
                yield return null;
            }

            pendingSelections--;
        }

        selectionRunner = null;
    }

    private int ApplyXpBoost(int amount)
    {
        if (accessoryStats == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) accessoryStats = PlayerAccessoryStats.Find(player.transform);
        }

        float multiplier = statusEffects != null ? statusEffects.CurrentXpMultiplier : 1f;
        if (accessoryStats != null) multiplier *= accessoryStats.XpGainMultiplier;
        if (Mathf.Approximately(multiplier, 1f)) return amount;

        int adjusted = Mathf.RoundToInt(amount * multiplier);
        return Mathf.Max(0, adjusted);
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterRarity : MonoBehaviour
{
    public enum Rarity { Common, Uncommon, Rare, Legendary }

    [Header("Auto Roll")]
    [SerializeField] private bool rollOnStart = true;
    [SerializeField] private bool rerollWeaponDamageType = true;

    [Header("Current")]
    [SerializeField] private Rarity rarity = Rarity.Common;

    [Header("Rarity Weights")]
    [SerializeField] public float weightCommon = 60f;
    [SerializeField] public float weightUncommon = 25f;
    [SerializeField] public float weightRare = 12f;
    [SerializeField] public float weightLegendary = 3f;

    // === Enemy (SimpleHealth) roll ranges ===
    [Header("Enemy Health & Defense Rolls")]
    [SerializeField] public Vector2Int hpFlatAdd = new Vector2Int(15, 60);
    [SerializeField] public Vector2 hpMult = new Vector2(1.10f, 1.35f);
    [SerializeField] public Vector2 regenAdd = new Vector2(0.2f, 2.0f);
    [SerializeField] public Vector2 armorAdd = new Vector2(1f, 6f);
    [SerializeField] public Vector2 evasionAdd = new Vector2(1f, 6f);

    // (Removed) Movement speed upgrade

    // === Global cadence (WeaponTick) ===
    [Header("Global Attack Cadence (WeaponTick)")]
    [SerializeField] public Vector2 atkSpeedFracAll = new Vector2(0.08f, 0.25f);

    // === Knife rolls (public fields) ===
    [Header("Knife Rolls")]
    [SerializeField] public Vector2Int KnifeDamageFlat = new Vector2Int(2, 12);
    [SerializeField] public Vector2 KnifeDamageMult = new Vector2(1.08f, 1.30f);
    [SerializeField] public Vector2 KnifeLifestealAdd = new Vector2(0.03f, 0.15f);
    // NOTE: removed KnifeMaxTargetsAdd (per request)
    [SerializeField] public Vector2 KnifeCritChanceAdd = new Vector2(0.05f, 0.20f);
    [SerializeField] public Vector2 KnifeCritMultAdd = new Vector2(0.20f, 0.80f);

    // === Shooter rolls (public fields) ===
    [Header("Shooter Rolls")]
    [SerializeField] public Vector2Int shooterDamageFlat = new Vector2Int(2, 12);
    [SerializeField] public Vector2 shooterDamageMult = new Vector2(1.08f, 1.30f);
    [SerializeField] public Vector2Int shooterProjectilesAdd = new Vector2Int(1, 2);

    // Cached refs
    private SimpleHealth health;        // needs public: int maxHealth, int currentHealth, float regenRate, float armor;
                                        // public UnityEngine.UI.Slider healthSlider; public TMPro.TextMeshProUGUI healthText;
                                        // public string extraTextField; public void Heal(int amt); public void UpdateStatsText();
                                        // (Removed) private EnemyChaser chaser;  // movement speed upgrade removed
    private Knife[] knives;             // must have public fields used below
    private SimpleShooter[] shooters;   // must have public fields used below
    private WeaponTick[] ticks;         // needs public: float interval; public void ResetAndStart();

    // Visible notes (pre-styled lines)
    private readonly List<string> notesEnemy = new();
    private readonly List<string> notesWeapons = new();

    // ===== UI Colors (TMP rich text) =====
    private const string C_HEADER = "#8BD3FF";   // headers
    private const string C_LABEL = "#EAEAEA";   // label text
    private const string C_VALUE = "#FFD24D";   // numbers
    private const string C_TEXT = "#D8E6F2";   // base text
    private const string C_RARE = "#3AA0FF";
    private const string C_UNC = "#3EC46D";
    private const string C_COM = "#B0B0B0";
    private const string C_LEG = "#FFB347";

    // Damage Type Weights to make Physical more common
    private const float WEIGHT_PHYSICAL = 90f;
    private const float WEIGHT_FIRE = 15f;
    private const float WEIGHT_COLD = 15f;
    private const float WEIGHT_LIGHTNING = 15f;
    private const float WEIGHT_POISON = 15f;

    private void Awake() => RefreshCachedRefs();

    private void Start()
    {
        // If a ChatterStats component is present, it will handle the initial roll
        // to ensure power scaling is applied first.
        if (rollOnStart && GetComponent<ChatterStats>() == null)
        {
            RerollRarity();
        }
    }

    private void OnTransformChildrenChanged() => RefreshCachedRefs();

    private void RefreshCachedRefs()
    {
        health = GetComponent<SimpleHealth>();
        knives = GetComponentsInChildren<Knife>(true);
        shooters = GetComponentsInChildren<SimpleShooter>(true);
        ticks = GetComponentsInChildren<WeaponTick>(true);
    }

    // ===== Public / Context API =====
    [ContextMenu("Monster Rarity / Refresh & Reroll")]
    private void RefreshAndReroll()
    {
        RefreshCachedRefs();
        RerollRarity();
    }

    [ContextMenu("Monster Rarity / Reroll Rarity + Stats")]
    public void RerollRarity()
    {
        RefreshCachedRefs();
        rarity = RollWeightedRarity();
        RerollStats();
    }

    [ContextMenu("Monster Rarity / Reroll Stats Only")]
    public void RerollStats()
    {
        RefreshCachedRefs();
        notesEnemy.Clear();
        notesWeapons.Clear();

        int rolls = rarity switch
        {
            Rarity.Common => 1,
            Rarity.Uncommon => 2,
            Rarity.Rare => 3,
            Rarity.Legendary => 4,
            _ => 1
        };

        var candidates = BuildCandidates();
        Shuffle(candidates);
        for (int i = 0; i < Mathf.Min(rolls, candidates.Count); i++)
            candidates[i].Invoke();

        // Always apply a randomized damage type to weapons if present
        bool hasWeapons = (knives != null && knives.Length > 0) || (shooters != null && shooters.Length > 0);
        if (rerollWeaponDamageType && hasWeapons)
            Up_Weapons_DamageType_Reroll();

        // Optional extra: cadence tweak across all weapons (60% chance)
        if (ticks != null && ticks.Length > 0 && UnityEngine.Random.value < 0.6f)
            Upgrade_AllWeaponAttackSpeed();

        // Restart WeaponTick safely
        foreach (var t in ticks)
            if (t && t.isActiveAndEnabled)
                t.ResetAndStart();

        // === Write EVERYTHING to parent entity’s UI ===
        WriteIntoParentUI();
    }

    // ===== Candidate pool =====
    private List<Action> BuildCandidates()
    {
        var list = new List<Action>();

        if (health)
        {
            list.Add(Up_HP_Flat);
            list.Add(Up_HP_Mult);
            list.Add(Up_Regen_Add);
            list.Add(Up_Armor_Add);
            list.Add(Up_Evasion_Add);
        }

        // Movement speed upgrade removed

        bool hasKnives = knives != null && knives.Length > 0;
        if (hasKnives)
        {
            list.Add(Up_Knife_Dmg_Flat);
            list.Add(Up_Knife_Dmg_Mult);
            list.Add(Up_Knife_Lifesteal_Add);
            list.Add(Up_Knife_Crit_Both);
        }

        bool hasShooters = shooters != null && shooters.Length > 0;
        if (hasShooters)
        {
            list.Add(Up_Shooter_Dmg_Flat);
            list.Add(Up_Shooter_Dmg_Mult);
            list.Add(Up_Shooter_Projectiles_Add);
        }

        // Damage type reroll is now always applied in RerollStats(),
        // so we do not include it in the random candidate pool.

        return list;
    }

    // ===== Enemy (SimpleHealth) upgrades =====
    private void Up_HP_Flat()
    {
        if (!health) return;

        int add = UnityEngine.Random.Range(hpFlatAdd.x, hpFlatAdd.y + 1);
        health.maxHealth += add;
        health.currentHealth = health.maxHealth; // full heal

        EN("Max Health", $"+{add}");
        health.UpdateStatsText();
    }

    private void Up_HP_Mult()
    {
        if (!health) return;

        float mult = UnityEngine.Random.Range(hpMult.x, hpMult.y);
        health.maxHealth = Mathf.RoundToInt(health.maxHealth * mult);
        health.currentHealth = health.maxHealth; // full heal

        EN("Max Health", $"+{(mult - 1f) * 100f:F0}%");
        health.UpdateStatsText();
    }

    private void Up_Regen_Add()
    {
        if (!health) return;
        float add = UnityEngine.Random.Range(regenAdd.x, regenAdd.y);
        health.regenRate = Mathf.Max(0f, health.regenRate + add);
        EN("Regen", $"+{add:F2}/s");
        health.UpdateStatsText();
    }

    private void Up_Armor_Add()
    {
        if (!health) return;
        float add = UnityEngine.Random.Range(armorAdd.x, armorAdd.y);
        health.armor = Mathf.Max(0f, health.armor + add);
        EN("Armor", $"+{add:F1}");
    }

    private void Up_Evasion_Add()
    {
        if (!health) return;
        float add = UnityEngine.Random.Range(evasionAdd.x, evasionAdd.y);
        health.evasion = Mathf.Max(0f, health.evasion + add);
        EN("Evasion", $"+{add:F1}");
        health.UpdateStatsText();
    }

    // (Removed) Movement speed upgrade implementation

    // ===== Global cadence =====
    private void Upgrade_AllWeaponAttackSpeed()
    {
        float frac = UnityEngine.Random.Range(atkSpeedFracAll.x, atkSpeedFracAll.y);
        if (ticks == null) return;

        foreach (var t in ticks)
        {
            if (!t) continue;
            float before = t.interval;
            float after = Mathf.Max(0.01f, before * (1f - frac));
            t.interval = after;
            if (t.isActiveAndEnabled) t.ResetAndStart();
        }

        // Gamey name & positive stat wording
        WN("Attack Speed ", $"+{frac * 100f:F0}%");
    }


    // ===== Knife (public fields) =====
    private void Up_Knife_Dmg_Flat()
    {
        int add = UnityEngine.Random.Range(KnifeDamageFlat.x, KnifeDamageFlat.y + 1);
        foreach (var k in knives)
        {
            if (!k) continue;
            k.minDamage = Mathf.Max(0, k.minDamage + add);
            k.damage = Mathf.Max(k.minDamage, k.damage + add);
        }
        WN("Melee Damage", $"+{add}");
    }

    private void Up_Knife_Dmg_Mult()
    {
        float m = UnityEngine.Random.Range(KnifeDamageMult.x, KnifeDamageMult.y);
        foreach (var k in knives)
        {
            if (!k) continue;
            k.minDamage = Mathf.Max(0, Mathf.RoundToInt(k.minDamage * m));
            k.damage = Mathf.Max(k.minDamage, Mathf.RoundToInt(k.damage * m));
        }
        WN("Melee Damage", $"+{(m - 1f) * 100f:F0}%");
    }

    private void Up_Knife_Lifesteal_Add()
    {
        float add = UnityEngine.Random.Range(KnifeLifestealAdd.x, KnifeLifestealAdd.y);
        foreach (var k in knives) if (k) k.lifestealPercent = Mathf.Clamp01(k.lifestealPercent + add);
        WN("Melee Lifesteal", $"+{add * 100f:F0}%");
    }

    private void Up_Knife_Crit_Both()
    {
        float addChance = UnityEngine.Random.Range(KnifeCritChanceAdd.x, KnifeCritChanceAdd.y);
        float addMult = UnityEngine.Random.Range(KnifeCritMultAdd.x, KnifeCritMultAdd.y);
        foreach (var k in knives)
        {
            if (!k) continue;
            k.critChance = Mathf.Clamp01(k.critChance + addChance);
            k.critMultiplier = Mathf.Max(1f, k.critMultiplier + addMult);
        }
        WN("Melee Crit", $"+{addChance * 100f:F0}% / +{addMult * 100f:F0}% dmg");
    }

    // ===== Shooter (public fields) =====
    private void Up_Shooter_Dmg_Flat()
    {
        int add = UnityEngine.Random.Range(shooterDamageFlat.x, shooterDamageFlat.y + 1);
        foreach (var s in shooters)
        {
            if (!s) continue;
            s.minDamage = Mathf.Max(0, s.minDamage + add);
            s.damage = Mathf.Max(s.minDamage, s.damage + add);
        }
        WN("Ranged Damage", $"+{add}");
    }

    private void Up_Shooter_Dmg_Mult()
    {
        float m = UnityEngine.Random.Range(shooterDamageMult.x, shooterDamageMult.y);
        foreach (var s in shooters)
        {
            if (!s) continue;
            s.minDamage = Mathf.Max(0, Mathf.RoundToInt(s.minDamage * m));
            s.damage = Mathf.Max(s.minDamage, Mathf.RoundToInt(s.damage * m));
        }
        WN("Ranged Damage", $"+{(m - 1f) * 100f:F0}%");
    }

    private void Up_Shooter_Projectiles_Add()
    {
        int add = UnityEngine.Random.Range(shooterProjectilesAdd.x, shooterProjectilesAdd.y + 1);
        foreach (var s in shooters) if (s) s.projectileCount = Mathf.Max(1, s.projectileCount + add);
        WN("Ranged Projectiles", $"+{add}");
    }

    // ===== New: Weapon Damage Type Reroll =====
    private void Up_Weapons_DamageType_Reroll()
    {
        // Weighted roll for damage type
        float total = WEIGHT_PHYSICAL + WEIGHT_FIRE + WEIGHT_COLD + WEIGHT_LIGHTNING + WEIGHT_POISON;
        float r = UnityEngine.Random.value * Mathf.Max(0.0001f, total);

        SimpleHealth.DamageType chosen;
        if ((r -= WEIGHT_PHYSICAL) < 0) chosen = SimpleHealth.DamageType.Physical;
        else if ((r -= WEIGHT_FIRE) < 0) chosen = SimpleHealth.DamageType.Fire;
        else if ((r -= WEIGHT_COLD) < 0) chosen = SimpleHealth.DamageType.Cold;
        else if ((r -= WEIGHT_LIGHTNING) < 0) chosen = SimpleHealth.DamageType.Lightning;
        else chosen = SimpleHealth.DamageType.Poison;

        if (knives != null)
            foreach (var k in knives)
                if (k) k.damageType = chosen;

        if (shooters != null)
            foreach (var s in shooters)
                if (s) s.damageType = chosen;
    }

    // ===== Rarity & UI =====
    private Rarity RollWeightedRarity()
    {
        float total = weightCommon + weightUncommon + weightRare + weightLegendary;
        float r = UnityEngine.Random.value * Mathf.Max(0.0001f, total);
        if ((r -= weightCommon) < 0) return Rarity.Common;
        if ((r -= weightUncommon) < 0) return Rarity.Uncommon;
        if ((r -= weightRare) < 0) return Rarity.Rare;
        return Rarity.Legendary;
    }

    private void WriteIntoParentUI()
    {
        if (!health) return;

        var sb = new StringBuilder();

        // Header
        // sb.AppendLine($"<b>{C(C_LABEL, "Rarity:")} {FormatRarity(rarity)}</b>");

        // Enemy section
        if (notesEnemy.Count > 0)
        {
            foreach (var line in notesEnemy) sb.AppendLine(line);
        }

        // Weapon section (append independently so it shows even if no enemy notes)
        if (notesWeapons.Count > 0)
        {
            foreach (var line in notesWeapons) sb.AppendLine(line);
        }

        // Wrap with base text color and slightly smaller size for compactness
        string block = $"{C(C_TEXT, $"<size=85%>{sb}</size>")}";

        // Replace previous rarity block in SimpleHealth.extraTextField
        string cur = health.extraTextField ?? "";
        string cleaned = RemoveRaritySection(cur);
        string combined = string.IsNullOrWhiteSpace(cleaned) ? block : $"{cleaned}\n{block}";
        health.extraTextField = combined;

        // Update UI (method assumed public)
        try { health.UpdateStatsText(); } catch { /* ignore if not present */ }
    }

    // ===== Formatting helpers =====
    private static string C(string hex, string text) => $"<color={hex}>{text}</color>";
    private static string Bullet(string label, string value)
        => $"{C(C_LABEL, label)}: {C(C_VALUE, value)}";

    private void EN(string label, string value) => notesEnemy.Add(Bullet(label, value));
    private void WN(string label, string value) => notesWeapons.Add(Bullet(label, value));

    private static string RemoveRaritySection(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int idx = s.IndexOf("Rarity:", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? s[..idx].TrimEnd() : s;
    }

    private static string FormatRarity(Rarity r) => r switch
    {
        Rarity.Common => C(C_COM, "Weak"),
        Rarity.Uncommon => C(C_UNC, "Normal"),
        Rarity.Rare => C(C_RARE, "Strong"),
        Rarity.Legendary => C(C_LEG, "Elite"),
        _ => "Weak"
    };

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

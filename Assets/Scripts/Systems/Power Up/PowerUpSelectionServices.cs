using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public static class PowerUpWeightedSelector
{
    public static List<PowerUp> Pick(IReadOnlyList<PowerUp> candidates, int count)
    {
        List<PowerUp> available = CopyCandidates(candidates);

        int resultCount = Mathf.Min(Mathf.Max(0, count), available.Count);
        var result = new List<PowerUp>(resultCount);

        while (result.Count < resultCount)
            result.Add(TakeWeighted(available));

        return result;
    }

    /// <summary>
    /// Picks a build-aware card set. A valid owned-item upgrade receives the first
    /// slot, all unique candidates are exhausted before any duplicate is used,
    /// and duplicate fallback can fill the configured card count from a small pool.
    /// </summary>
    public static List<PowerUp> PickContextual(
        IReadOnlyList<PowerUp> candidates,
        int count,
        bool guaranteeUpgrade = true,
        bool allowDuplicateFallback = true)
    {
        List<PowerUp> source = CopyCandidates(candidates);
        int desired = Mathf.Max(0, count);
        var result = new List<PowerUp>(desired);
        if (desired == 0 || source.Count == 0)
            return result;

        var available = new List<PowerUp>(source);
        if (guaranteeUpgrade)
        {
            var upgrades = new List<PowerUp>();
            for (int i = 0; i < available.Count; i++)
                if (available[i].IsUpgrade)
                    upgrades.Add(available[i]);

            if (upgrades.Count > 0)
            {
                PowerUp guaranteed = TakeWeighted(upgrades);
                result.Add(guaranteed);
                available.Remove(guaranteed);
            }
        }

        while (result.Count < desired && available.Count > 0)
            result.Add(TakeWeighted(available));

        while (allowDuplicateFallback && result.Count < desired)
            result.Add(source[ChooseWeightedIndex(source)]);

        return result;
    }

    private static List<PowerUp> CopyCandidates(IReadOnlyList<PowerUp> candidates)
    {
        var result = new List<PowerUp>();
        if (candidates == null) return result;

        for (int i = 0; i < candidates.Count; i++)
            if (candidates[i] != null)
                result.Add(candidates[i]);
        return result;
    }

    private static PowerUp TakeWeighted(List<PowerUp> candidates)
    {
        int index = ChooseWeightedIndex(candidates);
        PowerUp selected = candidates[index];
        candidates.RemoveAt(index);
        return selected;
    }

    private static int ChooseWeightedIndex(IReadOnlyList<PowerUp> candidates)
    {
        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += Mathf.Max(0f, candidates[i].weight);

        if (totalWeight <= 0f)
            return Random.Range(0, candidates.Count);

        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += Mathf.Max(0f, candidates[i].weight);
            if (roll <= cumulative)
                return i;
        }

        return candidates.Count - 1;
    }
}

public static class PowerUpCardFormatter
{
    private const int MaxBaseStatRows = 2;

    public static string BuildDescription(PowerUp offer)
    {
        if (offer == null) return string.Empty;

        var prefix = new StringBuilder();
        if (offer.IsUpgrade)
        {
            string rarityName = PowerUp.GetRarityDisplayName(offer.rarity).ToUpperInvariant();
            string rarityColor = PowerUp.GetRarityColor(offer.rarity);
            prefix.Append($"<b><color={rarityColor}>[{rarityName}]</color></b> ");
        }

        if (offer.IsWeapon) prefix.Append("<b>[WEAPON] </b>");
        if (offer.IsAccessory) prefix.Append("<b>[ACCESSORY] </b>");

        if (prefix.Length > 0)
            prefix.AppendLine();

        bool isBaseItem = !offer.IsUpgrade && (offer.IsWeapon || offer.IsAccessory);
        prefix.Append(isBaseItem
            ? BuildBaseItemDescription(offer)
            : offer.powerUpDescription ?? string.Empty);
        return prefix.ToString();
    }

    private static string BuildBaseItemDescription(PowerUp offer)
    {
        var result = new StringBuilder(256);
        string rpgDescription = GetRpgDescription(offer);
        if (!string.IsNullOrWhiteSpace(rpgDescription))
            result.Append($"<color=#E7D7B1><i>{rpgDescription}</i></color>");

        string stats = offer.IsWeapon
            ? BuildWeaponStats(offer.powerUpObject)
            : BuildAccessoryStats(offer.powerUpObject);

        if (!string.IsNullOrWhiteSpace(stats))
        {
            if (result.Length > 0) result.AppendLine().AppendLine();
            result.AppendLine("<b><color=#C9A86A>BASE STATS</color></b>");
            result.Append(stats);
        }

        return result.ToString();
    }

    private static string BuildWeaponStats(GameObject weaponObject)
    {
        if (weaponObject == null) return string.Empty;

        WeaponTick tick = weaponObject.GetComponentInChildren<WeaponTick>(true);
        Knife knife = weaponObject.GetComponentInChildren<Knife>(true);
        if (knife != null)
            return BuildKnifeStats(knife, tick);

        SimpleShooter shooter = weaponObject.GetComponentInChildren<SimpleShooter>(true);
        return shooter != null ? BuildShooterStats(shooter, tick) : string.Empty;
    }

    private static string BuildKnifeStats(Knife knife, WeaponTick tick)
    {
        var stats = new StringBuilder(160);
        AppendStat(stats, "Damage", FormatDamage(knife.minDamage, knife.damage, knife.damageType));
        if (tick != null)
            AppendStat(stats, "Cooldown", $"{FormatNumber(Mathf.Max(0f, tick.interval))}s");
        else
            AppendStat(stats, "Reach", FormatNumber(knife.radius));
        return stats.ToString();
    }

    private static string BuildShooterStats(SimpleShooter shooter, WeaponTick tick)
    {
        var stats = new StringBuilder(160);
        AppendStat(stats, "Damage", FormatDamage(shooter.minDamage, shooter.damage, shooter.damageType));
        if (tick != null)
            AppendStat(stats, "Cooldown", $"{FormatNumber(Mathf.Max(0f, tick.interval))}s");
        else
            AppendStat(stats, "Projectiles", Mathf.Max(1, shooter.projectileCount).ToString(CultureInfo.InvariantCulture));
        return stats.ToString();
    }

    private static string BuildAccessoryStats(GameObject accessoryObject)
    {
        if (accessoryObject == null) return string.Empty;

        var stats = new StringBuilder(128);
        AccessoryStatEffect[] effects = accessoryObject.GetComponentsInChildren<AccessoryStatEffect>(true);
        for (int i = 0; i < effects.Length; i++)
        {
            IReadOnlyList<AccessoryStatModifier> modifiers = effects[i].Modifiers;
            for (int j = 0; j < modifiers.Count; j++)
            {
                string line = AccessoryStatApplicator.FormatDescription(modifiers[j].type, modifiers[j].value);
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (stats.Length > 0) stats.AppendLine();
                stats.Append(line);
            }
        }

        string combined = AccessoryDescriptionFormatter.CombineStatLines(stats.ToString());
        return TakeFirstStatRows(combined);
    }

    private static string GetRpgDescription(PowerUp offer)
    {
        string name = NormalizeName(offer.powerUpName);
        return name switch
        {
            "burstfire bow" => "A ranger's war bow tuned for disciplined volleys. It looses three arrows before the next draw.",
            "demonic aura" or "demon aura" => "A profane halo bound to your soul. It scourges every foe reckless enough to enter its reach.",
            "longshot crossbow" => "A siege-bow for patient hunters. Its heavy bolts carry brutal force across the battlefield.",
            "blowgun" => "A hunter's reed made for swift skirmishing, delivering deadly darts before the horde can close in.",
            "phantom aegis" => "A spectral guardian sworn to orbit its bearer, hunting nearby foes with unseen force.",
            "eye of devastion" or "eye of devastation" => "A forbidden eye chained to your orbit. It lashes nearby enemies with relentless eldritch power.",
            "knife" => "A duelist's blade made for rapid executions, seeking the nearest foe with ruthless precision.",
            "axe" => "A headsman's cleaver with a slow, crushing arc, built to break clustered ranks in a single swing.",
            "sword" => "A knight's balanced blade, dependable in speed, reach, and killing power.",

            "arcane splitter bandolier" => "A spellwoven harness that conjures an additional spectral projectile whenever your weapons fire.",
            "armor" => "Armor doubles for 3s after taking damage.",
            "bloodied banner" => "A war standard that drinks in the press of battle, granting +10% damage per nearby enemy, up to +100%.",
            "boots" => "Road-worn boots of a royal outrider, granting their bearer speed and a lighter step.",
            "coat" => "Evasion doubles for 3s after taking damage.",
            "grave pact" => "A funerary covenant that turns suffering into strength, granting +2% damage per 10% missing health, up to +20%.",
            "ice ring" => "A frostbound signet whose pale ward steels flesh against both winter and flame.",
            "life ring" => "A living-gold band that fortifies the body and steadily knits ruined flesh.",
            "lightning ring" => "A stormglass signet that guards its bearer from lightning and venom alike.",
            "reaper's ledger" => "A cursed account that records every death, granting +0.1% damage per kill, up to +150%.",
            "sanguine chalice" => "A crimson relic that restores 5% maximum health after every 10 enemies slain.",
            "witching hourglass" => "A witch's timepiece that grants +1% damage each untouched second, up to +50%; taking damage empties it.",
            "fire ring" => "An ember-bound signet that wraps its bearer in the ancient ward of the first flame.",
            "poison ring" => "A venom-carved signet whose alchemy turns mortal toxins aside.",
            _ => offer.powerUpDescription ?? string.Empty,
        };
    }

    private static string NormalizeName(string value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        int qualifier = normalized.IndexOf(" (");
        return qualifier > 0 ? normalized.Substring(0, qualifier) : normalized;
    }

    private static string FormatDamage(int minDamage, int maxDamage, SimpleHealth.DamageType damageType)
    {
        int min = Mathf.Max(0, Mathf.Min(minDamage, maxDamage));
        int max = Mathf.Max(0, Mathf.Max(minDamage, maxDamage));
        string range = min == max ? min.ToString(CultureInfo.InvariantCulture) : $"{min}-{max}";
        return damageType == SimpleHealth.DamageType.Physical ? range : $"{range} {damageType}";
    }

    private static string FormatNumber(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string TakeFirstStatRows(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var result = new StringBuilder(96);
        string[] rows = value.Split('\n');
        int added = 0;
        for (int i = 0; i < rows.Length && added < MaxBaseStatRows; i++)
        {
            string row = rows[i].Trim();
            if (row.Length == 0) continue;
            if (result.Length > 0) result.AppendLine();
            result.Append(row);
            added++;
        }
        return result.ToString();
    }

    private static void AppendStat(StringBuilder stats, string label, string value)
    {
        if (stats.Length > 0) stats.AppendLine();
        stats.Append($"<color=#AAAAAA>{label}:</color> <color=#F2D492>{value}</color>");
    }
}

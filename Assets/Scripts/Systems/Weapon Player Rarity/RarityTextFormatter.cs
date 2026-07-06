using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class RarityTextFormatter
{
    public const string TagColorOpen = "<color=#00AEEF>";
    public const string TagColorClose = "</color>";

    public static string BuildRangesSummaryText(
        Rarity current,
        IReadOnlyList<IUpgrade> applied,
        TierSystem tiers,
        UpgradeRanges ranges)
    {
        var lines = new List<string>
        {
            "<b>Roll Ranges</b>",
            $"<b>Rarity:</b> {WeaponContext.FormatRarity(current)}"
        };

        if (applied == null || applied.Count == 0)
        {
            lines.Add("<i>No selected rolls.</i>");
            return string.Join("\n", lines);
        }

        for (int i = 0; i < applied.Count; i++)
            AddSelectedRangeLine(lines, applied[i], tiers, ranges);

        return string.Join("\n", lines);
    }

    public static string BuildStatsTextWithRanges(
        string normalStatsText,
        Rarity current,
        IReadOnlyList<IUpgrade> applied,
        TierSystem tiers,
        UpgradeRanges ranges)
    {
        string baseStats = RemoveLastRaritySection(normalStatsText ?? string.Empty).TrimEnd();
        string rangesBlock = TagColorOpen + BuildRangesSummaryText(current, applied, tiers, ranges) + TagColorClose;
        return string.IsNullOrWhiteSpace(baseStats)
            ? rangesBlock
            : baseStats + "\n" + rangesBlock;
    }

    public static string MergeRarityBlock(string currentText, IReadOnlyList<string> lines)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            string clean = StripWrappers(lines[i]);
            if (!string.IsNullOrWhiteSpace(clean))
                sb.AppendLine(clean);
        }

        string inner = sb.ToString().TrimEnd();
        string block = TagColorOpen + inner + TagColorClose;
        string withoutLast = RemoveLastRaritySection(currentText ?? string.Empty);

        string merged = string.IsNullOrWhiteSpace(withoutLast)
            ? block
            : withoutLast.TrimEnd() + "\n" + block;

        return DedupeColorTags(NormalizeWhitespace(merged));
    }

    public static string RemoveLastRaritySection(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        int rarityIdx = s.LastIndexOf("<b>Rarity:</b>", StringComparison.OrdinalIgnoreCase);
        if (rarityIdx < 0)
        {
            int colorIdx = s.LastIndexOf(TagColorOpen, StringComparison.OrdinalIgnoreCase);
            return colorIdx >= 0 ? s[..colorIdx].TrimEnd() : s;
        }

        int colorStart = s.LastIndexOf(TagColorOpen, rarityIdx, StringComparison.OrdinalIgnoreCase);
        int startIdx = colorStart >= 0 ? colorStart : rarityIdx;
        return s[..startIdx].TrimEnd();
    }

    private static void AddSelectedRangeLine(List<string> lines, IUpgrade upgrade, TierSystem tiers, UpgradeRanges ranges)
    {
        if (upgrade is DamageFlatUpgrade)
        {
            var r = tiers.Scale(ranges.damageFlatAdd, tiers.damageFlat, 0);
            AddRangeLine(lines, "Damage", r.x, r.y, "", tiers.damageFlat);
        }
        else if (upgrade is DamagePercentAsFlatUpgrade)
        {
            var r = tiers.ScaleMultiplierLike(ranges.damageMult, tiers.damagePercent);
            AddPercentRangeLine(lines, "Damage", r.x - 1f, r.y - 1f, tiers.damagePercent);
        }
        else if (upgrade is AttackSpeedUpgrade)
        {
            var r = tiers.Scale(ranges.atkSpeedFrac, tiers.attackSpeed);
            AddPercentRangeLine(lines, "Attack Speed", r.x, r.y, tiers.attackSpeed);
        }
        else if (upgrade is CritUpgrade)
        {
            var chance = tiers.Scale(ranges.critChanceAdd, tiers.critChance);
            var mult = tiers.Scale(ranges.critMultAdd, tiers.critMultiplier);
            AddPercentRangeLine(lines, "Crit Chance", chance.x, chance.y, tiers.critChance);
            AddRangeLine(lines, "Crit Mult", mult.x, mult.y, "", tiers.critMultiplier, "F2");
        }
        else if (upgrade is HpFlatUpgrade)
        {
            var r = tiers.Scale(ranges.hpFlatAdd, tiers.hpFlat, 0);
            AddRangeLine(lines, "Max Health", r.x, r.y, "", tiers.hpFlat);
        }
        else if (upgrade is HpPercentUpgrade)
        {
            var r = tiers.ScaleMultiplierLike(ranges.hpMult, tiers.hpPercent);
            AddPercentRangeLine(lines, "Max Health", r.x - 1f, r.y - 1f, tiers.hpPercent);
        }
        else if (upgrade is RegenUpgrade)
        {
            var r = tiers.Scale(ranges.regenAdd, tiers.regen);
            AddRangeLine(lines, "Regen", r.x, r.y, "/s", tiers.regen, "F2");
        }
        else if (upgrade is ArmorUpgrade)
        {
            var r = tiers.Scale(ranges.armorAdd, tiers.armor);
            AddRangeLine(lines, "Armor", r.x, r.y, "", tiers.armor);
        }
        else if (upgrade is EvasionUpgrade)
        {
            var r = tiers.Scale(ranges.evasionAdd, tiers.evasion);
            AddRangeLine(lines, "Evasion", r.x, r.y, "", tiers.evasion);
        }
        else if (upgrade is ArmorPercentUpgrade)
        {
            var r = tiers.ScaleMultiplierLike(ranges.armorMult, tiers.armorPercent);
            AddPercentRangeLine(lines, "Armor", r.x - 1f, r.y - 1f, tiers.armorPercent);
        }
        else if (upgrade is EvasionPercentUpgrade)
        {
            var r = tiers.ScaleMultiplierLike(ranges.evasionMult, tiers.evasionPercent);
            AddPercentRangeLine(lines, "Evasion", r.x - 1f, r.y - 1f, tiers.evasionPercent);
        }
        else if (upgrade is FireResistUpgrade || upgrade is ColdResistUpgrade || upgrade is LightningResistUpgrade || upgrade is PoisonResistUpgrade)
        {
            var r = tiers.Scale(ranges.resistAdd, tiers.resist);
            AddPercentRangeLine(lines, "Resist", r.x, r.y, tiers.resist);
        }
        else if (upgrade is KnifeRadiusUpgrade)
        {
            var r = tiers.ScaleMultiplierLike(ranges.knifeRadiusMult, tiers.knifeRadius);
            AddPercentRangeLine(lines, "Range", r.x - 1f, r.y - 1f, tiers.knifeRadius);
        }
        else if (upgrade is KnifeSplashUpgrade)
        {
            var r = tiers.ScaleMultiplierLike(ranges.knifeSplashRadiusMult, tiers.knifeSplashRadius);
            AddPercentRangeLine(lines, "AOE", r.x - 1f, r.y - 1f, tiers.knifeSplashRadius);
        }
        else if (upgrade is ShooterRangeUpgrade)
        {
            var r = tiers.Scale(ranges.shooterForceAdd, tiers.shooterForce);
            AddRangeLine(lines, "Projectile Speed", r.x, r.y, "", tiers.shooterForce, "F1");
        }
        else if (upgrade is ShooterAccuracyUpgrade)
        {
            var r = tiers.Scale(ranges.shooterSpreadReduceFrac, tiers.shooterAccuracy);
            AddPercentRangeLine(lines, "Accuracy", r.x, r.y, tiers.shooterAccuracy);
        }
    }

    private static void AddRangeLine(List<string> lines, string label, int min, int max, string suffix, int tier)
    {
        lines.Add($"+{min}-{max}{suffix} {label} ({RomanStatic(tier)})");
    }

    private static void AddRangeLine(List<string> lines, string label, float min, float max, string suffix, int tier, string format = "F0")
    {
        if (min > max) (min, max) = (max, min);
        lines.Add($"+{min.ToString(format)}-{max.ToString(format)}{suffix} {label} ({RomanStatic(tier)})");
    }

    private static void AddPercentRangeLine(List<string> lines, string label, float min, float max, int tier)
    {
        if (min > max) (min, max) = (max, min);
        lines.Add($"+{min * 100f:F0}-{max * 100f:F0}% {label} ({RomanStatic(tier)})");
    }

    private static string RomanStatic(int n)
    {
        n = Mathf.Clamp(n, 1, 5);
        return n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => "V" };
    }

    private static string StripWrappers(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace("<size=80%>", string.Empty).Replace("</size>", string.Empty);
        s = s.Replace(TagColorOpen, string.Empty).Replace(TagColorClose, string.Empty);
        return s.Trim();
    }

    private static string DedupeColorTags(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace(TagColorOpen + TagColorOpen, TagColorOpen);
        s = s.Replace(TagColorClose + TagColorClose, TagColorClose);
        return s;
    }

    private static string NormalizeWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        var sb = new StringBuilder(s.Length);
        int nlRun = 0;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];

            if (c == '\r') continue;

            if (c == '\n')
            {
                nlRun++;
                if (nlRun <= 2) sb.Append('\n');
                continue;
            }

            nlRun = 0;

            if ((c == ' ' || c == '\t') && sb.Length > 0 && sb[^1] == '\n') continue;

            sb.Append(c);
        }

        return sb.ToString().TrimEnd();
    }
}

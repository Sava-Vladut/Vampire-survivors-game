using System.Collections.Generic;
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

        prefix.Append(offer.powerUpDescription ?? string.Empty);
        return prefix.ToString();
    }
}

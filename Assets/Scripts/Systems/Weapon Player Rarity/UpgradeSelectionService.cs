using System;
using System.Collections.Generic;
using UnityEngine;

public static class UpgradeSelectionService
{
    public static List<IUpgrade> Pick(
        IList<UpgradeWeightProvider.Candidate> candidates,
        int picks,
        UpgradeWeightProvider upgradeWeights,
        System.Random rng)
    {
        if (upgradeWeights != null)
            return upgradeWeights.PickWeighted(candidates, picks, rng);

        var bag = new List<UpgradeWeightProvider.Candidate>(candidates);
        Shuffle(bag, rng);

        int count = Mathf.Min(picks, bag.Count);
        var result = new List<IUpgrade>(count);
        for (int i = 0; i < count; i++)
            result.Add(bag[i].upgrade);

        return result;
    }

    public static IUpgrade PickUnique(
        IList<UpgradeWeightProvider.Candidate> candidates,
        IEnumerable<IUpgrade> applied,
        WeaponContext ctx,
        UpgradeWeightProvider upgradeWeights,
        System.Random rng,
        out bool hasEligibleUnique)
    {
        hasEligibleUnique = false;
        var existing = new HashSet<Type>();
        foreach (var upgrade in applied)
        {
            if (upgrade != null) existing.Add(upgrade.GetType());
        }

        var bag = new List<UpgradeWeightProvider.Candidate>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            var up = candidates[i].upgrade;
            if (up == null) continue;
            if (!up.IsApplicable(ctx)) continue;
            if (existing.Contains(up.GetType())) continue;
            bag.Add(candidates[i]);
        }

        hasEligibleUnique = bag.Count > 0;
        return PickOne(bag, upgradeWeights, rng);
    }

    public static IUpgrade PickReplacement(
        IList<UpgradeWeightProvider.Candidate> candidates,
        Type currentType,
        IEnumerable<IUpgrade> occupiedUpgrades,
        WeaponContext ctx,
        UpgradeWeightProvider upgradeWeights,
        System.Random rng)
    {
        if (currentType == null) return null;

        var occupied = new HashSet<Type>();
        foreach (var upgrade in occupiedUpgrades)
        {
            if (upgrade != null) occupied.Add(upgrade.GetType());
        }

        var bag = new List<UpgradeWeightProvider.Candidate>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            var up = candidates[i].upgrade;
            if (up == null) continue;
            Type type = up.GetType();
            if (type == currentType) continue;
            if (occupied.Contains(type)) continue;
            if (!up.IsApplicable(ctx)) continue;
            bag.Add(candidates[i]);
        }

        return PickOne(bag, upgradeWeights, rng);
    }

    private static IUpgrade PickOne(
        IList<UpgradeWeightProvider.Candidate> candidates,
        UpgradeWeightProvider upgradeWeights,
        System.Random rng)
    {
        if (candidates == null || candidates.Count == 0) return null;

        if (upgradeWeights != null)
        {
            var picked = upgradeWeights.PickWeighted(candidates, 1, rng);
            return picked != null && picked.Count > 0 ? picked[0] : null;
        }

        int index = rng.Next(0, candidates.Count);
        return candidates[index].upgrade;
    }

    public static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = rng.Next(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

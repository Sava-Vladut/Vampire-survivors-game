using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class PowerUpWeightedSelector
{
    public static List<PowerUp> Pick(IReadOnlyList<PowerUp> candidates, int count)
    {
        var available = new List<PowerUp>();
        if (candidates != null)
        {
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i] != null)
                    available.Add(candidates[i]);
        }

        int resultCount = Mathf.Min(Mathf.Max(0, count), available.Count);
        var result = new List<PowerUp>(resultCount);

        while (result.Count < resultCount)
        {
            float totalWeight = 0f;
            for (int i = 0; i < available.Count; i++)
                totalWeight += Mathf.Max(0f, available[i].weight);

            int chosenIndex;
            if (totalWeight <= 0f)
            {
                chosenIndex = Random.Range(0, available.Count);
            }
            else
            {
                float roll = Random.value * totalWeight;
                float cumulative = 0f;
                chosenIndex = available.Count - 1;

                for (int i = 0; i < available.Count; i++)
                {
                    cumulative += Mathf.Max(0f, available[i].weight);
                    if (roll <= cumulative)
                    {
                        chosenIndex = i;
                        break;
                    }
                }
            }

            result.Add(available[chosenIndex]);
            available.RemoveAt(chosenIndex);
        }

        return result;
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

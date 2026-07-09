using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerDamageModifierRegistry : MonoBehaviour
{
    private readonly List<IPlayerDamageMultiplierProvider> providers = new();

    public void Register(IPlayerDamageMultiplierProvider provider)
    {
        if (provider != null && !providers.Contains(provider)) providers.Add(provider);
    }

    public void Unregister(IPlayerDamageMultiplierProvider provider)
    {
        if (provider != null) providers.Remove(provider);
    }

    public int Apply(int damage)
    {
        if (damage <= 0) return damage;
        float multiplier = 1f;
        for (int i = providers.Count - 1; i >= 0; i--)
        {
            if (providers[i] is not MonoBehaviour behaviour || behaviour == null)
            {
                providers.RemoveAt(i);
                continue;
            }
            if (behaviour.isActiveAndEnabled)
                multiplier *= Mathf.Max(0f, providers[i].DamageMultiplier);
        }
        return Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
    }
}

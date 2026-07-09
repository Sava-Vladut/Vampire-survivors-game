using System;
using UnityEngine;

[Serializable]
public struct AccessoryStatModifier
{
    public AccessoriesUpgrades.StatUpgradeType type;
    public float value;
}

public static class AccessoryStatApplicator
{
    public static bool CanApply(AccessoriesUpgrades.StatUpgradeType type, AccessoryEquipContext context)
    {
        if (type == AccessoriesUpgrades.StatUpgradeType.None) return false;
        if (type is AccessoriesUpgrades.StatUpgradeType.MoveSpeedFlat or AccessoriesUpgrades.StatUpgradeType.DashDistanceFlat)
            return context.Movement != null;
        if (type == AccessoriesUpgrades.StatUpgradeType.ProjectileCountFlat)
            return context.PlayerRoot != null;
        if (PlayerAccessoryStats.IsRuntimeModifier(type))
            return context.PlayerRoot != null;
        return context.Health != null;
    }

    public static void Apply(AccessoriesUpgrades.StatUpgradeType type, float value, AccessoryEquipContext context)
    {
        SimpleHealth health = context.Health;
        switch (type)
        {
            case AccessoriesUpgrades.StatUpgradeType.MaxHealthFlat:
                health.IncreaseMaxHealth(Mathf.RoundToInt(value)); break;
            case AccessoriesUpgrades.StatUpgradeType.MaxHealthPercent:
                health.IncreaseMaxHealth(Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * value))); break;
            case AccessoriesUpgrades.StatUpgradeType.RegenFlat:
                health.regenRate += value; break;
            case AccessoriesUpgrades.StatUpgradeType.ArmorFlat:
                health.GiveArmor(value); break;
            case AccessoriesUpgrades.StatUpgradeType.ArmorPercent:
                health.GiveArmor(health.armor * value); break;
            case AccessoriesUpgrades.StatUpgradeType.EvasionFlat:
                health.GiveEvasion(value); break;
            case AccessoriesUpgrades.StatUpgradeType.EvasionPercent:
                health.GiveEvasion(health.evasion * value); break;
            case AccessoriesUpgrades.StatUpgradeType.FireResist:
                health.AddFireResist(value); break;
            case AccessoriesUpgrades.StatUpgradeType.ColdResist:
                health.AddColdResist(value); break;
            case AccessoriesUpgrades.StatUpgradeType.LightningResist:
                health.AddLightningResist(value); break;
            case AccessoriesUpgrades.StatUpgradeType.PoisonResist:
                health.AddPoisonResist(value); break;
            case AccessoriesUpgrades.StatUpgradeType.MoveSpeedFlat:
                context.Movement.IncreaseMoveSpeed(value); break;
            case AccessoriesUpgrades.StatUpgradeType.DashDistanceFlat:
                context.Movement.IncreaseDashDistance(value); break;
            case AccessoriesUpgrades.StatUpgradeType.ThornsFlat:
                health.GiveThorns(value); break;
            case AccessoriesUpgrades.StatUpgradeType.ProjectileCountFlat:
                SimpleShooter[] shooters = context.PlayerRoot.GetComponentsInChildren<SimpleShooter>(true);
                int amount = Mathf.RoundToInt(value);
                for (int i = 0; i < shooters.Length; i++) shooters[i].projectileCount += amount;
                break;
            default:
                PlayerAccessoryStats.GetOrAdd(context.PlayerRoot)?.AddModifier(type, value);
                break;
        }
    }

    public static string FormatDescription(AccessoriesUpgrades.StatUpgradeType type, float value)
    {
        string number = Mathf.Abs(value - Mathf.Round(value)) < 0.0001f ? Mathf.RoundToInt(value).ToString() : value.ToString("0.##");
        string percent = $"{value * 100f:0.#}%";
        return type switch
        {
            AccessoriesUpgrades.StatUpgradeType.MaxHealthFlat => $"+{number} Max Health",
            AccessoriesUpgrades.StatUpgradeType.MaxHealthPercent => $"+{percent} Max Health",
            AccessoriesUpgrades.StatUpgradeType.RegenFlat => $"+{number} Health Regen",
            AccessoriesUpgrades.StatUpgradeType.ArmorFlat => $"+{number} Armor",
            AccessoriesUpgrades.StatUpgradeType.ArmorPercent => $"+{percent} Armor",
            AccessoriesUpgrades.StatUpgradeType.EvasionFlat => $"+{number} Evasion",
            AccessoriesUpgrades.StatUpgradeType.EvasionPercent => $"+{percent} Evasion",
            AccessoriesUpgrades.StatUpgradeType.FireResist => $"+{percent} Fire Resist",
            AccessoriesUpgrades.StatUpgradeType.ColdResist => $"+{percent} Cold Resist",
            AccessoriesUpgrades.StatUpgradeType.LightningResist => $"+{percent} Lightning Resist",
            AccessoriesUpgrades.StatUpgradeType.PoisonResist => $"+{percent} Poison Resist",
            AccessoriesUpgrades.StatUpgradeType.MoveSpeedFlat => $"+{number} Move Speed",
            AccessoriesUpgrades.StatUpgradeType.DashDistanceFlat => $"+{number} Dash Distance",
            AccessoriesUpgrades.StatUpgradeType.ThornsFlat => $"+{number} Thorns",
            AccessoriesUpgrades.StatUpgradeType.ProjectileCountFlat => $"+{number} Projectile Count",
            AccessoriesUpgrades.StatUpgradeType.CooldownReduction => $"+{percent} Cooldown Reduction",
            AccessoriesUpgrades.StatUpgradeType.AttackSpeedPercent => $"+{percent} Attack Speed",
            AccessoriesUpgrades.StatUpgradeType.GlobalDamagePercent => $"+{percent} Global Damage",
            AccessoriesUpgrades.StatUpgradeType.CriticalChanceFlat => $"+{percent} Critical Chance",
            AccessoriesUpgrades.StatUpgradeType.CriticalDamageFlat => $"+{percent} Critical Damage",
            AccessoriesUpgrades.StatUpgradeType.WeaponAreaPercent => $"+{percent} Weapon Area",
            AccessoriesUpgrades.StatUpgradeType.ProjectileSpeedPercent => $"+{percent} Projectile Speed",
            AccessoriesUpgrades.StatUpgradeType.ProjectileLifetimePercent => $"+{percent} Projectile Lifetime",
            AccessoriesUpgrades.StatUpgradeType.ProjectilePenetrationFlat => $"+{number} Projectile Penetration",
            AccessoriesUpgrades.StatUpgradeType.KnockbackStrengthFlat => $"+{number} Knockback Strength",
            AccessoriesUpgrades.StatUpgradeType.PickupRadiusFlat => $"+{number} Pickup Radius",
            AccessoriesUpgrades.StatUpgradeType.XpGainPercent => $"+{percent} XP Gain",
            AccessoriesUpgrades.StatUpgradeType.HealingReceivedPercent => $"+{percent} Healing Received",
            AccessoriesUpgrades.StatUpgradeType.StatusDurationPercent => $"+{percent} Status Duration",
            AccessoriesUpgrades.StatUpgradeType.StatusApplicationChanceFlat => $"+{percent} Status Chance",
            AccessoriesUpgrades.StatUpgradeType.DashCooldownReduction => $"+{percent} Dash Cooldown Reduction",
            AccessoriesUpgrades.StatUpgradeType.AdditionalDashChargeFlat => $"+{number} Dash Charge",
            AccessoriesUpgrades.StatUpgradeType.DashInvulnerabilityFlat => $"+{number}s Dash Invulnerability",
            AccessoriesUpgrades.StatUpgradeType.ContactDamageReduction => $"+{percent} Contact Damage Reduction",
            AccessoriesUpgrades.StatUpgradeType.EnemySlowAura => $"+{percent} Enemy Slow Aura",
            _ => string.Empty,
        };
    }
}

[DisallowMultipleComponent]
public sealed class PlayerAccessoryStats : MonoBehaviour
{
    private const float MaximumReduction = 0.80f;
    public const float SlowAuraRadius = 5f;

    private float cooldownReduction;
    private float attackSpeedBonus;
    private float globalDamageBonus;
    private float criticalChanceBonus;
    private float criticalDamageBonus;
    private float weaponAreaBonus;
    private float projectileSpeedBonus;
    private float projectileLifetimeBonus;
    private int projectilePenetrationBonus;
    private float knockbackStrengthBonus;
    private float pickupRadiusBonus;
    private float xpGainBonus;
    private float healingReceivedBonus;
    private float statusDurationBonus;
    private float statusApplicationChanceBonus;
    private float dashCooldownReduction;
    private int additionalDashCharges;
    private float dashInvulnerabilityDuration;
    private float contactDamageReduction;
    private float enemySlowAura;

    public float CooldownMultiplier => 1f - Mathf.Clamp(cooldownReduction, 0f, MaximumReduction);
    public float AttackSpeedMultiplier => 1f + Mathf.Max(0f, attackSpeedBonus);
    public float GlobalDamageMultiplier => 1f + Mathf.Max(0f, globalDamageBonus);
    public float CriticalChanceBonus => Mathf.Max(0f, criticalChanceBonus);
    public float CriticalDamageBonus => Mathf.Max(0f, criticalDamageBonus);
    public float WeaponAreaMultiplier => 1f + Mathf.Max(0f, weaponAreaBonus);
    public float ProjectileSpeedMultiplier => 1f + Mathf.Max(0f, projectileSpeedBonus);
    public float ProjectileLifetimeMultiplier => 1f + Mathf.Max(0f, projectileLifetimeBonus);
    public int ProjectilePenetrationBonus => Mathf.Max(0, projectilePenetrationBonus);
    public float KnockbackStrengthBonus => Mathf.Max(0f, knockbackStrengthBonus);
    public float PickupRadiusBonus => Mathf.Max(0f, pickupRadiusBonus);
    public float XpGainMultiplier => 1f + Mathf.Max(0f, xpGainBonus);
    public float HealingReceivedMultiplier => 1f + Mathf.Max(0f, healingReceivedBonus);
    public float StatusDurationMultiplier => 1f + Mathf.Max(0f, statusDurationBonus);
    public float StatusApplicationChanceBonus => Mathf.Max(0f, statusApplicationChanceBonus);
    public float DashCooldownMultiplier => 1f - Mathf.Clamp(dashCooldownReduction, 0f, MaximumReduction);
    public int AdditionalDashCharges => Mathf.Max(0, additionalDashCharges);
    public float DashInvulnerabilityDuration => Mathf.Max(0f, dashInvulnerabilityDuration);
    public float ContactDamageMultiplier => 1f - Mathf.Clamp(contactDamageReduction, 0f, MaximumReduction);
    public float EnemySlowMultiplier => 1f - Mathf.Clamp(enemySlowAura, 0f, MaximumReduction);
    public bool HasEnemySlowAura => enemySlowAura > 0f;

    public static bool IsRuntimeModifier(AccessoriesUpgrades.StatUpgradeType type) =>
        type >= AccessoriesUpgrades.StatUpgradeType.CooldownReduction;

    public void AddModifier(AccessoriesUpgrades.StatUpgradeType type, float value)
    {
        float positive = Mathf.Max(0f, value);
        switch (type)
        {
            case AccessoriesUpgrades.StatUpgradeType.CooldownReduction: cooldownReduction += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.AttackSpeedPercent: attackSpeedBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.GlobalDamagePercent: globalDamageBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.CriticalChanceFlat: criticalChanceBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.CriticalDamageFlat: criticalDamageBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.WeaponAreaPercent: weaponAreaBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.ProjectileSpeedPercent: projectileSpeedBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.ProjectileLifetimePercent: projectileLifetimeBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.ProjectilePenetrationFlat: projectilePenetrationBonus += Mathf.Max(0, Mathf.RoundToInt(value)); break;
            case AccessoriesUpgrades.StatUpgradeType.KnockbackStrengthFlat: knockbackStrengthBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.PickupRadiusFlat: pickupRadiusBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.XpGainPercent: xpGainBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.HealingReceivedPercent: healingReceivedBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.StatusDurationPercent: statusDurationBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.StatusApplicationChanceFlat: statusApplicationChanceBonus += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.DashCooldownReduction: dashCooldownReduction += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.AdditionalDashChargeFlat: additionalDashCharges += Mathf.Max(0, Mathf.RoundToInt(value)); break;
            case AccessoriesUpgrades.StatUpgradeType.DashInvulnerabilityFlat: dashInvulnerabilityDuration += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.ContactDamageReduction: contactDamageReduction += positive; break;
            case AccessoriesUpgrades.StatUpgradeType.EnemySlowAura: enemySlowAura += positive; break;
        }
    }

    public int ApplyGlobalDamage(int damage)
    {
        if (damage <= 0) return damage;
        return Mathf.Max(1, Mathf.RoundToInt(damage * GlobalDamageMultiplier));
    }

    public static PlayerAccessoryStats GetOrAdd(Transform playerRoot)
    {
        if (playerRoot == null) return null;
        PlayerAccessoryStats stats = playerRoot.GetComponentInChildren<PlayerAccessoryStats>(true);
        return stats != null ? stats : playerRoot.gameObject.AddComponent<PlayerAccessoryStats>();
    }

    public static PlayerAccessoryStats Find(Transform source)
    {
        if (source == null) return null;
        PlayerAccessoryStats stats = source.GetComponentInParent<PlayerAccessoryStats>(true);
        if (stats != null) return stats;
        return source.root != null ? source.root.GetComponentInChildren<PlayerAccessoryStats>(true) : null;
    }

    public static PlayerAccessoryStats FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? Find(player.transform) : null;
    }
}

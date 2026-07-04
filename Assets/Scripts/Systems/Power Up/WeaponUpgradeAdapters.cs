using UnityEngine;

// Common surface shared by Knife and SimpleShooter for the stats that behave
// identically regardless of weapon type (damage, crit, on-hit status, damage type).
// Letting one upgrade concept target both weapon types through this interface is
// what lets WeaponUpgrades define each concept's name/description/effect once
// instead of once per weapon type.
public interface IWeaponStatTarget
{
    int Damage { get; set; }
    SimpleHealth.DamageType DamageType { get; set; }
    float CritChance { get; set; }
    float CritMultiplier { get; set; }
    bool ApplyStatusEffectOnHit { get; set; }
    float StatusApplyChance { get; set; }
    float StatusEffectDuration { get; set; }
    void EnableOnHitEffectByIndex(int effectIndex);
}

public interface IKnifeStatTarget : IWeaponStatTarget
{
    float Radius { get; set; }
    int MaxTargetsPerTick { get; set; }
    float LifestealPercent { get; set; }
    float SplashRadius { get; set; }
    float SplashDamagePercent { get; set; }
}

public interface IShooterStatTarget : IWeaponStatTarget
{
    int ProjectileCount { get; set; }
    float SpreadAngle { get; set; }
    float ShootForce { get; set; }
    float BulletLifetime { get; set; }
}

public interface ITickStatTarget
{
    float Interval { get; set; }
    int BurstCount { get; set; }
    float BurstSpacing { get; set; }
}

public sealed class KnifeUpgradeAdapter : IKnifeStatTarget
{
    private readonly Knife k;
    public KnifeUpgradeAdapter(Knife k) { this.k = k; }

    public int Damage { get => k.damage; set => k.damage = value; }
    public SimpleHealth.DamageType DamageType { get => k.damageType; set => k.damageType = value; }
    public float CritChance { get => k.critChance; set => k.critChance = value; }
    public float CritMultiplier { get => k.critMultiplier; set => k.critMultiplier = value; }
    public bool ApplyStatusEffectOnHit { get => k.applyStatusEffectOnHit; set => k.applyStatusEffectOnHit = value; }
    public float StatusApplyChance { get => k.statusApplyChance; set => k.statusApplyChance = Mathf.Clamp01(value); }
    public float StatusEffectDuration { get => k.statusEffectDuration; set => k.statusEffectDuration = value; }
    public void EnableOnHitEffectByIndex(int effectIndex) => k.EnableOnHitEffectByIndex(effectIndex);

    public float Radius { get => k.radius; set => k.radius = value; }
    public int MaxTargetsPerTick { get => k.maxTargetsPerTick; set => k.maxTargetsPerTick = value; }
    public float LifestealPercent { get => k.lifestealPercent; set => k.lifestealPercent = value; }
    public float SplashRadius { get => k.splashRadius; set => k.splashRadius = value; }
    public float SplashDamagePercent { get => k.splashDamagePercent; set => k.splashDamagePercent = value; }
}

public sealed class ShooterUpgradeAdapter : IShooterStatTarget
{
    private readonly SimpleShooter s;
    public ShooterUpgradeAdapter(SimpleShooter s) { this.s = s; }

    public int Damage { get => s.damage; set => s.damage = value; }
    public SimpleHealth.DamageType DamageType { get => s.damageType; set => s.damageType = value; }
    public float CritChance { get => s.critChance; set => s.critChance = value; }
    public float CritMultiplier { get => s.critMultiplier; set => s.critMultiplier = value; }
    public bool ApplyStatusEffectOnHit { get => s.applyStatusEffectOnHit; set => s.applyStatusEffectOnHit = value; }
    public float StatusApplyChance { get => s.statusApplyChance; set => s.statusApplyChance = Mathf.Clamp01(value); }
    public float StatusEffectDuration { get => s.statusEffectDuration; set => s.statusEffectDuration = value; }
    public void EnableOnHitEffectByIndex(int effectIndex) => s.EnableOnHitEffectByIndex(effectIndex);

    public int ProjectileCount { get => s.projectileCount; set => s.projectileCount = value; }
    public float SpreadAngle { get => s.spreadAngle; set => s.spreadAngle = value; }
    public float ShootForce { get => s.shootForce; set => s.shootForce = value; }
    public float BulletLifetime { get => s.bulletLifetime; set => s.bulletLifetime = value; }
}

public sealed class TickUpgradeAdapter : ITickStatTarget
{
    private readonly WeaponTick t;
    public TickUpgradeAdapter(WeaponTick t) { this.t = t; }

    public float Interval { get => t.interval; set => t.interval = value; }
    public int BurstCount { get => t.burstCount; set => t.burstCount = value; }
    public float BurstSpacing { get => t.burstSpacing; set => t.burstSpacing = value; }
}

using UnityEngine;

public interface IDamageModule
{
    int Damage { get; set; }
    int MinDamage { get; set; }
    int MaxDamage { get; set; }
}
public interface ICritModule { float CritChance { get; set; } float CritMultiplier { get; set; } }
public interface IAttackSpeedModule { float Interval { get; set; } }
public interface IKnifeModule
{
    float LifestealPercent { get; set; }
    float Radius { get; set; }
    float SplashRadius { get; set; }
    int MaxTargetsPerTick { get; set; }
}
public interface IShooterModule
{
    float BulletLifetime { get; set; }
    float ShootForce { get; set; }
    int ProjectileCount { get; set; }
    float SpreadAngle { get; set; }
}
public interface IUITextSink
{
    string Text { get; set; }          // current full text
    void SetText(string s);
}

public interface IHealthModule
{
    int MaxHealth { get; set; }
    float RegenRate { get; set; }
    float Armor { get; set; }
    float Evasion { get; set; }

    float FireResist { get; set; }
    float ColdResist { get; set; }
    float LightningResist { get; set; }
    float PoisonResist { get; set; }

    void IncreaseMaxHealth(int delta);
}

// ===== Adapters (no reflection) =====
public sealed class KnifeAdapter : IDamageModule, ICritModule, IKnifeModule, IUITextSink
{
    private readonly Knife k;
    public KnifeAdapter(Knife k) { this.k = k; }
    public int Damage { get => k.damage; set => MaxDamage = value; }
    public int MinDamage
    {
        get => k.minDamage;
        set
        {
            k.minDamage = Mathf.Max(0, value);
            k.damage = Mathf.Max(k.minDamage, k.damage);
        }
    }
    public int MaxDamage { get => k.damage; set => k.damage = Mathf.Max(k.minDamage, value); }
    public float CritChance { get => k.critChance; set => k.critChance = Mathf.Clamp01(value); }
    public float CritMultiplier { get => k.critMultiplier; set => k.critMultiplier = value; }
    public float LifestealPercent { get => k.lifestealPercent; set => k.lifestealPercent = Mathf.Clamp01(value); }
    public float Radius { get => k.radius; set => k.radius = value; }
    public float SplashRadius { get => k.splashRadius; set => k.splashRadius = value; }
    public int MaxTargetsPerTick { get => k.maxTargetsPerTick; set => k.maxTargetsPerTick = value; }
    public string Text { get => k.extraTextField ?? ""; set => k.extraTextField = value; }
    public void SetText(string s) => k.extraTextField = s;
}

public sealed class ShooterAdapter : IDamageModule, ICritModule, IShooterModule, IUITextSink
{
    private readonly SimpleShooter s;
    public ShooterAdapter(SimpleShooter s) { this.s = s; }
    public int Damage { get => s.damage; set => MaxDamage = value; }
    public int MinDamage
    {
        get => s.minDamage;
        set
        {
            s.minDamage = Mathf.Max(0, value);
            s.damage = Mathf.Max(s.minDamage, s.damage);
        }
    }
    public int MaxDamage { get => s.damage; set => s.damage = Mathf.Max(s.minDamage, value); }
    public float CritChance { get => s.critChance; set => s.critChance = Mathf.Clamp01(value); }
    public float CritMultiplier { get => s.critMultiplier; set => s.critMultiplier = value; }
    public float BulletLifetime { get => s.bulletLifetime; set => s.bulletLifetime = value; }
    public float ShootForce { get => s.shootForce; set => s.shootForce = value; }
    public int ProjectileCount { get => s.projectileCount; set => s.projectileCount = value; }
    public float SpreadAngle { get => s.spreadAngle; set => s.spreadAngle = Mathf.Max(0f, value); }
    public string Text { get => s.extraTextField ?? ""; set => s.extraTextField = value; }
    public void SetText(string t) => s.extraTextField = t;
}

public sealed class TickAdapter : IAttackSpeedModule
{
    private readonly WeaponTick t;
    public TickAdapter(WeaponTick t) { this.t = t; }
    public float Interval { get => t.interval; set => t.interval = value; }
    public void ResetAndStartIfPlaying()
    {
        if (Application.isPlaying) t.ResetAndStart();
    }
}

public sealed class HealthAdapter : IHealthModule
{
    private readonly SimpleHealth h;
    public HealthAdapter(SimpleHealth h) { this.h = h; }

    public int MaxHealth { get => h.maxHealth; set => h.maxHealth = Mathf.Max(1, value); }
    public float RegenRate { get => h.regenRate; set => h.regenRate = Mathf.Max(0f, value); }
    public float Armor { get => h.armor; set => h.armor = Mathf.Max(0f, value); }
    public float Evasion { get => h.evasion; set => h.evasion = Mathf.Max(0f, value); }

    public float FireResist { get => h.fireResist; set => h.fireResist = Mathf.Clamp(value, 0f, 0.95f); }
    public float ColdResist { get => h.coldResist; set => h.coldResist = Mathf.Clamp(value, 0f, 0.95f); }
    public float LightningResist { get => h.lightningResist; set => h.lightningResist = Mathf.Clamp(value, 0f, 0.95f); }
    public float PoisonResist { get => h.poisonResist; set => h.poisonResist = Mathf.Clamp(value, 0f, 0.95f); }

    public void IncreaseMaxHealth(int delta) => h.IncreaseMaxHealth(delta);
}

public sealed class AccessoryAdapter : IUITextSink
{
    private readonly Accessory a;
    public AccessoryAdapter(Accessory a) { this.a = a; }
    public string Text
    {
        get => a != null ? (a.AccesoryDescription ?? string.Empty) : string.Empty;
        set { if (a != null) a.SetDescription(value ?? string.Empty); }
    }
    public void SetText(string s)
    {
        if (a != null) a.SetDescription(s ?? string.Empty);
    }
}

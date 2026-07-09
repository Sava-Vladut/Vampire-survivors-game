using System;
using UnityEngine;

[Flags]
public enum WeaponUpgradeTraits
{
    None = 0,
    Generated = 1 << 0,
    ScalesWithRarity = 1 << 1,
    StatusRelated = 1 << 2,
    FirstOnHit = 1 << 3
}

public enum WeaponUpgradeTarget
{
    Knife,
    Shooter,
    WeaponTick
}

public enum WeaponUpgradeValueFormat
{
    None,
    Integer,
    Percent,
    Decimal1,
    Decimal2,
    Seconds1,
    Seconds2,
    Multiplier,
    Degrees,
    DamageType,
    StatusType
}

public readonly struct WeaponUpgradeRange
{
    public WeaponUpgradeRange(float min, float max, bool wholeNumbers = false)
    {
        Min = min;
        Max = max;
        WholeNumbers = wholeNumbers;
    }

    public float Min { get; }
    public float Max { get; }
    public bool WholeNumbers { get; }

    public float Roll()
    {
        float min = Mathf.Min(Min, Max);
        float max = Mathf.Max(Min, Max);
        float value = Mathf.Approximately(min, max) ? min : UnityEngine.Random.Range(min, max);
        return WholeNumbers ? Mathf.Round(value) : value;
    }
}

public readonly struct WeaponUpgradeApplication
{
    public WeaponUpgradeApplication(Transform target, float value, bool seedStatusChance, float seededStatusChance)
    {
        Target = target;
        Value = value;
        SeedStatusChance = seedStatusChance;
        SeededStatusChance = seededStatusChance;
    }

    public Transform Target { get; }
    public float Value { get; }
    public bool SeedStatusChance { get; }
    public float SeededStatusChance { get; }
}

/// <summary>Immutable behavior and presentation for one weapon upgrade type.</summary>
public sealed class WeaponUpgradeDefinition
{
    private readonly Func<Transform, bool> eligibility;
    private readonly Func<Transform, float> specialRoll;
    private readonly Func<WeaponUpgradeApplication, bool> apply;
    private readonly Func<Transform, Sprite> icon;

    public WeaponUpgradeDefinition(
        WeaponUpgrades.UpgradeType type,
        WeaponUpgradeTarget target,
        string titleTemplate,
        string descriptionTemplate,
        WeaponUpgradeValueFormat valueFormat,
        WeaponUpgradeTraits traits,
        Func<WeaponUpgradeApplication, bool> apply,
        WeaponUpgradeRange defaultRange = default,
        bool hasConfigurableRange = true,
        Func<Transform, bool> eligibility = null,
        Func<Transform, float> specialRoll = null,
        Func<Transform, Sprite> icon = null)
    {
        Type = type;
        Target = target;
        TitleTemplate = titleTemplate;
        DescriptionTemplate = descriptionTemplate;
        ValueFormat = valueFormat;
        Traits = traits;
        DefaultRange = defaultRange;
        HasConfigurableRange = hasConfigurableRange;
        this.eligibility = eligibility;
        this.specialRoll = specialRoll;
        this.apply = apply;
        this.icon = icon;
    }

    public WeaponUpgrades.UpgradeType Type { get; }
    public WeaponUpgradeTarget Target { get; }
    public string TitleTemplate { get; }
    public string DescriptionTemplate { get; }
    public WeaponUpgradeValueFormat ValueFormat { get; }
    public WeaponUpgradeTraits Traits { get; }
    public WeaponUpgradeRange DefaultRange { get; }
    public bool HasConfigurableRange { get; }

    public bool IsGenerated => (Traits & WeaponUpgradeTraits.Generated) != 0;
    public bool ScalesWithRarity => (Traits & WeaponUpgradeTraits.ScalesWithRarity) != 0;
    public bool IsStatusRelated => (Traits & WeaponUpgradeTraits.StatusRelated) != 0;
    public bool IsFirstOnHit => (Traits & WeaponUpgradeTraits.FirstOnHit) != 0;

    public bool Supports(Transform target)
    {
        if (target == null) return false;
        return Target switch
        {
            WeaponUpgradeTarget.Knife => target.TryGetComponent<Knife>(out _),
            WeaponUpgradeTarget.Shooter => target.TryGetComponent<SimpleShooter>(out _),
            WeaponUpgradeTarget.WeaponTick => target.TryGetComponent<WeaponTick>(out _),
            _ => false
        };
    }

    public bool CanOffer(Transform target) => Supports(target) && (eligibility == null || eligibility(target));

    public float RollBaseValue(Transform target)
    {
        if (specialRoll != null)
            return specialRoll(target);

        if (HasConfigurableRange && GeneratedUpgradeSettings.TryRollWeapon(Type, out float configured))
            return configured;

        return DefaultRange.Roll();
    }

    public bool TryApply(WeaponUpgradeApplication application)
    {
        return Supports(application.Target) && apply != null && apply(application);
    }

    public Sprite GetIcon(Transform target) => icon?.Invoke(target);

    public void BuildText(Transform target, float value, out string title, out string description)
    {
        string formatted = WeaponUpgradeTextFormatter.Format(value, ValueFormat);
        title = string.Format(TitleTemplate, formatted);
        description = string.Format(DescriptionTemplate, formatted);

        if (target != null && !string.IsNullOrWhiteSpace(target.name))
            title = $"{target.name} - {title}";
    }
}

public static class WeaponUpgradeTextFormatter
{
    private const string NumberColor = "#8888FF";

    public static string Format(float value, WeaponUpgradeValueFormat format)
    {
        string Color(string text) => $"<color={NumberColor}>{text}</color>";

        return format switch
        {
            WeaponUpgradeValueFormat.Integer => Color(Mathf.RoundToInt(value).ToString()),
            WeaponUpgradeValueFormat.Percent => Color((value * 100f).ToString("F0")) + "%",
            WeaponUpgradeValueFormat.Decimal1 => Color(value.ToString("F1")),
            WeaponUpgradeValueFormat.Decimal2 => Color(value.ToString("F2")),
            WeaponUpgradeValueFormat.Seconds1 => Color(value.ToString("F1")) + "s",
            WeaponUpgradeValueFormat.Seconds2 => Color(value.ToString("F2")) + "s",
            WeaponUpgradeValueFormat.Multiplier => Color(value.ToString("F2")) + "x",
            WeaponUpgradeValueFormat.Degrees => Color(value.ToString("F1")) + "°",
            WeaponUpgradeValueFormat.DamageType => WeaponUpgradeRollUtility.ClampDamageType(value).ToString(),
            WeaponUpgradeValueFormat.StatusType => WeaponUpgradeRollUtility.ClampStatusType(value).ToString(),
            _ => string.Empty
        };
    }
}

public static class WeaponUpgradeRollUtility
{
    private static readonly StatusEffectSystem.StatusType[] NegativeStatusEffects =
    {
        StatusEffectSystem.StatusType.Bleeding,
        StatusEffectSystem.StatusType.Stun,
        StatusEffectSystem.StatusType.Ignite,
        StatusEffectSystem.StatusType.Shock,
        StatusEffectSystem.StatusType.Poison,
        StatusEffectSystem.StatusType.Frozen,
        StatusEffectSystem.StatusType.Slow,
        StatusEffectSystem.StatusType.Fear,
        StatusEffectSystem.StatusType.Cursed
    };

    public static int RandomNegativeStatusEffectIndex()
    {
        return (int)NegativeStatusEffects[UnityEngine.Random.Range(0, NegativeStatusEffects.Length)];
    }

    public static SimpleHealth.DamageType ClampDamageType(float value)
    {
        int count = Enum.GetValues(typeof(SimpleHealth.DamageType)).Length;
        return (SimpleHealth.DamageType)Mathf.Clamp(Mathf.RoundToInt(value), 0, count - 1);
    }

    public static StatusEffectSystem.StatusType ClampStatusType(float value)
    {
        int count = Enum.GetValues(typeof(StatusEffectSystem.StatusType)).Length;
        return (StatusEffectSystem.StatusType)Mathf.Clamp(Mathf.RoundToInt(value), 0, count - 1);
    }

    public static float RollDifferentDamageType(SimpleHealth.DamageType current)
    {
        int count = Enum.GetValues(typeof(SimpleHealth.DamageType)).Length;
        if (count <= 1) return 0f;

        int rolled = UnityEngine.Random.Range(0, count - 1);
        if (rolled >= (int)current) rolled++;
        return rolled;
    }

    public static SimpleHealth.DamageType ResolveDifferentDamageType(SimpleHealth.DamageType current, float value)
    {
        SimpleHealth.DamageType rolled = ClampDamageType(value);
        if (rolled != current) return rolled;

        int count = Enum.GetValues(typeof(SimpleHealth.DamageType)).Length;
        return (SimpleHealth.DamageType)(((int)current + 1) % count);
    }
}

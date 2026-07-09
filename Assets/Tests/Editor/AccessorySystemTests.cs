using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class AccessorySystemTests
{
    private readonly List<Object> cleanup = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        cleanup.Clear();
    }

    [Test]
    public void DescriptionFormatter_CombinesMatchingStats_AndPreservesText()
    {
        string result = AccessoryDescriptionFormatter.CombineStatLines("+5 armor\n+2 Armor\nA special rule");
        Assert.That(result, Does.Contain("+7 Armor"));
        Assert.That(result, Does.Contain("A special rule"));
    }

    [Test]
    public void DamageRegistry_StacksRegisteredProvidersMultiplicatively()
    {
        GameObject player = Track(new GameObject("Player"));
        PlayerDamageModifierRegistry registry = player.AddComponent<PlayerDamageModifierRegistry>();
        TestDamageProvider first = player.AddComponent<TestDamageProvider>();
        TestDamageProvider second = player.AddComponent<TestDamageProvider>();
        first.Multiplier = 1.5f;
        second.Multiplier = 2f;
        registry.Register(first);
        registry.Register(second);

        Assert.That(registry.Apply(10), Is.EqualTo(30));
        registry.Unregister(second);
        Assert.That(registry.Apply(10), Is.EqualTo(15));
    }

    [Test]
    public void StaticAccessoryEffect_AppliesOnlyOnceThroughExplicitSelection()
    {
        GameObject player = Track(new GameObject("Player"));
        player.tag = "Player";
        SimpleHealth health = player.AddComponent<SimpleHealth>();
        player.AddComponent<AccessoryInventory>();
        player.AddComponent<PlayerDamageModifierRegistry>();

        GameObject item = Track(new GameObject("Life Ring"));
        item.transform.SetParent(player.transform, false);
        Accessory accessory = item.AddComponent<Accessory>();
        AccessoryStatEffect effect = item.AddComponent<AccessoryStatEffect>();
        SetModifiers(effect, new AccessoryStatModifier
        {
            type = AccessoriesUpgrades.StatUpgradeType.MaxHealthFlat,
            value = 50f,
        });

        var offer = new PowerUp
        {
            powerUpName = "Life Ring",
            powerUpDescription = "A sturdy ring.",
            powerUpObject = item,
            IsAccessory = true,
        };
        var selection = new PowerUpSelectionContext(null, offer, item, player.transform);

        Assert.That(health.MaxHealth, Is.EqualTo(100));
        Assert.That(accessory.TryApply(selection), Is.True);
        Assert.That(health.MaxHealth, Is.EqualTo(150));
        Assert.That(accessory.BuildDisplayText(), Does.Not.Contain("A sturdy ring."));
        Assert.That(accessory.BuildDisplayText(), Does.Contain("+50 Max Health"));
        Assert.That(accessory.TryApply(selection), Is.True);
        Assert.That(health.MaxHealth, Is.EqualTo(150));
    }

    [Test]
    public void DefaultUpgradeProfile_ExcludesOwnerSpecificStats()
    {
        Assert.That(AccessoryUpgradeProfile.IsDefaultType(AccessoriesUpgrades.StatUpgradeType.ArmorFlat), Is.True);
        Assert.That(AccessoryUpgradeProfile.IsDefaultType(AccessoriesUpgrades.StatUpgradeType.MoveSpeedFlat), Is.False);
        Assert.That(AccessoryUpgradeProfile.IsDefaultType(AccessoriesUpgrades.StatUpgradeType.ThornsFlat), Is.False);
        Assert.That(AccessoryUpgradeProfile.IsDefaultType(AccessoriesUpgrades.StatUpgradeType.ProjectileCountFlat), Is.False);
    }

    [Test]
    public void EveryAccessoryStatType_HasDescriptionAndGeneratedRange()
    {
        foreach (AccessoriesUpgrades.StatUpgradeType type in System.Enum.GetValues(typeof(AccessoriesUpgrades.StatUpgradeType)))
        {
            if (type == AccessoriesUpgrades.StatUpgradeType.None) continue;

            Assert.That(AccessoryStatApplicator.FormatDescription(type, 0.1f), Is.Not.Empty, type.ToString());
            Assert.That(
                GeneratedUpgradeSettings.TryGetDefaultAccessoryRange(type, out float min, out float max, out _),
                Is.True,
                type.ToString());
            Assert.That(max, Is.GreaterThanOrEqualTo(min), type.ToString());
        }
    }

    [Test]
    public void RuntimeAccessoryStats_StackBonusesAndCapReductions()
    {
        GameObject player = Track(new GameObject("Player"));
        PlayerAccessoryStats stats = player.AddComponent<PlayerAccessoryStats>();

        stats.AddModifier(AccessoriesUpgrades.StatUpgradeType.GlobalDamagePercent, 0.25f);
        stats.AddModifier(AccessoriesUpgrades.StatUpgradeType.GlobalDamagePercent, 0.15f);
        stats.AddModifier(AccessoriesUpgrades.StatUpgradeType.CooldownReduction, 2f);
        stats.AddModifier(AccessoriesUpgrades.StatUpgradeType.AdditionalDashChargeFlat, 1f);

        Assert.That(stats.ApplyGlobalDamage(100), Is.EqualTo(140));
        Assert.That(stats.CooldownMultiplier, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(stats.AdditionalDashCharges, Is.EqualTo(1));
    }

    [Test]
    public void GlobalDamageStat_AppliesThroughWeaponDamageUtility()
    {
        GameObject player = Track(new GameObject("Player"));
        player.AddComponent<PlayerDamageModifierRegistry>();
        PlayerAccessoryStats stats = player.AddComponent<PlayerAccessoryStats>();
        stats.AddModifier(AccessoriesUpgrades.StatUpgradeType.GlobalDamagePercent, 0.5f);

        GameObject weapon = Track(new GameObject("Weapon"));
        weapon.transform.SetParent(player.transform, false);

        Assert.That(PlayerDamageMultiplierUtility.Apply(weapon, 20), Is.EqualTo(30));
    }

    private T Track<T>(T value) where T : Object
    {
        cleanup.Add(value);
        return value;
    }

    private static void SetModifiers(AccessoryStatEffect effect, params AccessoryStatModifier[] modifiers)
    {
        var serialized = new SerializedObject(effect);
        SerializedProperty list = serialized.FindProperty("modifiers");
        list.arraySize = modifiers.Length;
        for (int i = 0; i < modifiers.Length; i++)
        {
            SerializedProperty item = list.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("type").enumValueIndex = (int)modifiers[i].type;
            item.FindPropertyRelative("value").floatValue = modifiers[i].value;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

public sealed class TestDamageProvider : MonoBehaviour, IPlayerDamageMultiplierProvider
{
    public float Multiplier = 1f;
    public float DamageMultiplier => Multiplier;
}

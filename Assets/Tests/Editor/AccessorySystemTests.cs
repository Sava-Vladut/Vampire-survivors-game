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

    [Test]
    public void PowerUpCardFormatter_BaseWeaponUsesRpgCopyAndLiveBaseStats()
    {
        GameObject weapon = Track(new GameObject("Knife"));
        Knife knife = weapon.AddComponent<Knife>();
        knife.minDamage = 7;
        knife.damage = 23;
        knife.radius = 2f;
        knife.maxTargetsPerTick = 1;
        knife.critChance = 0.5f;
        knife.critMultiplier = 1.5f;
        WeaponTick tick = weapon.AddComponent<WeaponTick>();
        tick.interval = 0.5f;

        string description = PowerUpCardFormatter.BuildDescription(new PowerUp
        {
            powerUpName = "Knife",
            powerUpDescription = "Old non-RPG copy",
            powerUpObject = weapon,
            IsWeapon = true,
        });

        Assert.That(description, Does.Contain("duelist's blade"));
        Assert.That(description, Does.Contain("BASE STATS"));
        Assert.That(description, Does.Contain("7-23"));
        Assert.That(description, Does.Contain("0.5s"));
        Assert.That(description, Does.Not.Contain("Reach:"));
        Assert.That(description, Does.Not.Contain("Critical:"));
        Assert.That(description, Does.Not.Contain("Old non-RPG copy"));
    }

    [Test]
    public void PowerUpCardFormatter_BaseAccessoryUsesRpgCopyAndSerializedStats()
    {
        GameObject accessory = Track(new GameObject("Ice Ring"));
        AccessoryStatEffect effect = accessory.AddComponent<AccessoryStatEffect>();
        SetModifiers(effect,
            new AccessoryStatModifier { type = AccessoriesUpgrades.StatUpgradeType.MaxHealthFlat, value = 20f },
            new AccessoryStatModifier { type = AccessoriesUpgrades.StatUpgradeType.ColdResist, value = 0.3f },
            new AccessoryStatModifier { type = AccessoriesUpgrades.StatUpgradeType.FireResist, value = 0.3f });

        string description = PowerUpCardFormatter.BuildDescription(new PowerUp
        {
            powerUpName = "Ice Ring",
            powerUpDescription = "Old non-RPG copy",
            powerUpObject = accessory,
            IsAccessory = true,
        });

        Assert.That(description, Does.Contain("frostbound signet"));
        Assert.That(description, Does.Contain("+20 Max Health"));
        Assert.That(description, Does.Contain("+30% Cold Resist"));
        Assert.That(description, Does.Not.Contain("+30% Fire Resist"));
        Assert.That(description, Does.Not.Contain("Old non-RPG copy"));
    }

    [TestCase(RecentlyHitDefenseStat.Armor)]
    [TestCase(RecentlyHitDefenseStat.Evasion)]
    public void RecentlyHitDefense_DoublesTheCurrentTotalStatForThreeSeconds(RecentlyHitDefenseStat stat)
    {
        GameObject player = Track(new GameObject("Player"));
        SimpleHealth health = player.AddComponent<SimpleHealth>();
        health.armor = 20f;
        health.evasion = 20f;
        SetFloat(health, "maxEvasion", 0f);
        SetFloat(health, "invulnerabilityDuration", 0f);
        health.ResetHealth();

        GameObject item = Track(new GameObject(stat.ToString()));
        item.transform.SetParent(player.transform, false);
        item.AddComponent<Accessory>();
        RecentlyHitDefenseAccessory effect = item.AddComponent<RecentlyHitDefenseAccessory>();
        SetEnum(effect, "stat", (int)stat);
        InvokeAccessoryEnable(effect);

        Assert.That(health.EffectiveArmor, Is.EqualTo(20f));
        Assert.That(health.EffectiveEvasion, Is.EqualTo(20f));

        health.TakeDamage(10, SimpleHealth.DamageType.Physical, true, false, null, "Test Hit");

        Assert.That(effect.IsActive, Is.True);
        Assert.That(effect.RemainingDuration, Is.GreaterThan(2.9f).And.LessThanOrEqualTo(3f));
        Assert.That(
            stat == RecentlyHitDefenseStat.Armor ? health.EffectiveArmor : health.EffectiveEvasion,
            Is.EqualTo(40f));
        Assert.That(effect.GetAccessoryDescriptionLine(), Does.Contain("Active:"));
    }

    [Test]
    public void RecentlyHitDefense_IgnoresUnmitigatableDamage()
    {
        GameObject player = Track(new GameObject("Player"));
        SimpleHealth health = player.AddComponent<SimpleHealth>();
        health.armor = 20f;
        SetFloat(health, "invulnerabilityDuration", 0f);
        health.ResetHealth();

        GameObject item = Track(new GameObject("Armor"));
        item.transform.SetParent(player.transform, false);
        item.AddComponent<Accessory>();
        RecentlyHitDefenseAccessory effect = item.AddComponent<RecentlyHitDefenseAccessory>();
        InvokeAccessoryEnable(effect);

        health.TakeDamage(10, SimpleHealth.DamageType.Fire, false, false, null, "Ignite");

        Assert.That(effect.IsActive, Is.False);
        Assert.That(health.EffectiveArmor, Is.EqualTo(20f));
    }

    [Test]
    public void PowerUpChooser_RejectsNoOpOffersAndNewItemsPastTheirCap()
    {
        PowerUpChooser chooser = Track(new GameObject("Chooser")).AddComponent<PowerUpChooser>();
        GameObject candidateObject = Track(new GameObject("Candidate Weapon"));
        var candidate = new PowerUp { powerUpObject = candidateObject, IsWeapon = true };

        Assert.That(chooser.CanSelect(candidate), Is.True);
        Assert.That(chooser.CanSelect(new PowerUp { IsWeapon = true }), Is.False);

        chooser.selectedPowerUps.Add(new PowerUp
        {
            powerUpObject = Track(new GameObject("Equipped Weapon")),
            IsWeapon = true,
        });
        Assert.That(chooser.CanSelect(candidate), Is.False);
    }

    [Test]
    public void WeaponUpgradeOffer_RequiresAnActivePlayerOwnedTarget()
    {
        GameObject player = Track(new GameObject("Player"));
        player.tag = "Player";
        PowerUpChooser chooser = Track(new GameObject("Chooser")).AddComponent<PowerUpChooser>();

        GameObject weaponObject = Track(new GameObject("Knife"));
        weaponObject.transform.SetParent(player.transform, false);
        weaponObject.AddComponent<Knife>();

        GameObject offerObject = Track(new GameObject("Upgrade"));
        offerObject.SetActive(false);
        offerObject.transform.SetParent(weaponObject.transform, false);
        WeaponUpgrades upgrade = offerObject.AddComponent<WeaponUpgrades>();
        Assert.That(upgrade.ConfigureAsOffer(
            WeaponUpgrades.UpgradeType.KnifeDamageFlat,
            5f,
            PowerUpRarity.Common), Is.True);

        Assert.That(chooser.CanSelect(upgrade.Upgrade), Is.True);

        weaponObject.SetActive(false);
        Assert.That(chooser.CanSelect(upgrade.Upgrade), Is.False);

        weaponObject.SetActive(true);
        weaponObject.transform.SetParent(null);
        Assert.That(chooser.CanSelect(upgrade.Upgrade), Is.False);
    }

    [Test]
    public void FirstStatusUnlock_RemainsEligibleWithoutAnExistingStatusSource()
    {
        GameObject player = Track(new GameObject("Player"));
        player.tag = "Player";
        PowerUpChooser chooser = Track(new GameObject("Chooser")).AddComponent<PowerUpChooser>();

        GameObject weaponObject = Track(new GameObject("Knife"));
        weaponObject.transform.SetParent(player.transform, false);
        Knife knife = weaponObject.AddComponent<Knife>();
        knife.applyStatusEffectOnHit = false;

        GameObject offerObject = Track(new GameObject("First Status Upgrade"));
        offerObject.SetActive(false);
        offerObject.transform.SetParent(weaponObject.transform, false);
        WeaponUpgrades upgrade = offerObject.AddComponent<WeaponUpgrades>();
        Assert.That(upgrade.ConfigureAsOffer(
            WeaponUpgrades.UpgradeType.KnifeStatusEffectIndex,
            (int)StatusEffectSystem.StatusType.Poison,
            PowerUpRarity.Common,
            applyRarityMultiplier: false), Is.True);
        upgrade.ConfigureStatusChanceSeed(0.15f);

        Assert.That(chooser.CanSelect(upgrade.Upgrade), Is.True);
    }

    [Test]
    public void AccessoryStatusUpgrade_RequiresEquippedOwnerAndStatusSource()
    {
        GameObject player = Track(new GameObject("Player"));
        player.tag = "Player";
        player.AddComponent<SimpleHealth>();
        player.AddComponent<AccessoryInventory>();
        PowerUpChooser chooser = Track(new GameObject("Chooser")).AddComponent<PowerUpChooser>();

        GameObject accessoryObject = Track(new GameObject("Status Ring"));
        accessoryObject.transform.SetParent(player.transform, false);
        Accessory accessory = accessoryObject.AddComponent<Accessory>();

        GameObject offerObject = Track(new GameObject("Status Duration Upgrade"));
        offerObject.SetActive(false);
        offerObject.transform.SetParent(accessoryObject.transform, false);
        AccessoriesUpgrades upgrade = offerObject.AddComponent<AccessoriesUpgrades>();
        upgrade.upgradeType = AccessoriesUpgrades.StatUpgradeType.StatusDurationPercent;
        upgrade.Upgrade = new PowerUp
        {
            powerUpObject = offerObject,
            IsAccessory = true,
            IsUpgrade = true,
        };

        Assert.That(chooser.CanSelect(upgrade.Upgrade), Is.False);

        var accessoryOffer = new PowerUp
        {
            powerUpName = "Status Ring",
            powerUpObject = accessoryObject,
            IsAccessory = true,
        };
        Assert.That(accessory.TryApply(new PowerUpSelectionContext(
            chooser,
            accessoryOffer,
            accessoryObject,
            player.transform)), Is.True);
        Assert.That(chooser.CanSelect(upgrade.Upgrade), Is.False);

        GameObject weaponObject = Track(new GameObject("Status Knife"));
        weaponObject.transform.SetParent(player.transform, false);
        Knife knife = weaponObject.AddComponent<Knife>();
        knife.applyStatusEffectOnHit = true;

        Assert.That(chooser.CanSelect(upgrade.Upgrade), Is.True);
    }

    [Test]
    public void AccessoryUpgradeCapabilities_TrackProjectileTimingMovementAndStatusSystems()
    {
        GameObject player = Track(new GameObject("Player"));
        player.tag = "Player";
        PowerUpChooser chooser = Track(new GameObject("Chooser")).AddComponent<PowerUpChooser>();

        Assert.That(chooser.CanBenefitFromAccessoryUpgrade(
            AccessoriesUpgrades.StatUpgradeType.ProjectileSpeedPercent), Is.False);
        Assert.That(chooser.CanBenefitFromAccessoryUpgrade(
            AccessoriesUpgrades.StatUpgradeType.CooldownReduction), Is.False);
        Assert.That(chooser.CanBenefitFromAccessoryUpgrade(
            AccessoriesUpgrades.StatUpgradeType.DashCooldownReduction), Is.False);
        Assert.That(chooser.CanBenefitFromAccessoryUpgrade(
            AccessoriesUpgrades.StatUpgradeType.StatusApplicationChanceFlat), Is.False);

        GameObject weaponObject = Track(new GameObject("Shooter"));
        weaponObject.SetActive(false);
        weaponObject.transform.SetParent(player.transform, false);
        SimpleShooter shooter = weaponObject.AddComponent<SimpleShooter>();
        shooter.applyStatusEffectOnHit = true;
        WeaponTick tick = weaponObject.AddComponent<WeaponTick>();
        var tickData = new SerializedObject(tick);
        tickData.FindProperty("startOnAwake").boolValue = false;
        tickData.ApplyModifiedPropertiesWithoutUndo();
        weaponObject.SetActive(true);

        player.AddComponent<Rigidbody2D>();
        player.AddComponent<Snappy2DController>();

        Assert.That(chooser.CanBenefitFromAccessoryUpgrade(
            AccessoriesUpgrades.StatUpgradeType.ProjectileSpeedPercent), Is.True);
        Assert.That(chooser.CanBenefitFromAccessoryUpgrade(
            AccessoriesUpgrades.StatUpgradeType.CriticalChanceFlat), Is.True);
        Assert.That(chooser.CanBenefitFromAccessoryUpgrade(
            AccessoriesUpgrades.StatUpgradeType.CooldownReduction), Is.True);
        Assert.That(chooser.CanBenefitFromAccessoryUpgrade(
            AccessoriesUpgrades.StatUpgradeType.DashCooldownReduction), Is.True);
        Assert.That(chooser.CanBenefitFromAccessoryUpgrade(
            AccessoriesUpgrades.StatUpgradeType.StatusApplicationChanceFlat), Is.True);
    }

    [Test]
    public void ContextualSelector_GuaranteesUpgradeThenUsesDuplicatesOnlyAsFallback()
    {
        var weapon = new PowerUp { powerUpName = "Weapon", weight = 100f };
        var accessory = new PowerUp { powerUpName = "Accessory", weight = 100f };
        var upgrade = new PowerUp { powerUpName = "Upgrade", IsUpgrade = true, weight = 0.01f };

        List<PowerUp> result = PowerUpWeightedSelector.PickContextual(
            new[] { weapon, accessory, upgrade },
            3);

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result, Does.Contain(upgrade));
        Assert.That(result, Does.Contain(weapon));
        Assert.That(result, Does.Contain(accessory));

        result = PowerUpWeightedSelector.PickContextual(new[] { upgrade }, 3);
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Is.SameAs(upgrade));
        Assert.That(result[1], Is.SameAs(upgrade));
        Assert.That(result[2], Is.SameAs(upgrade));
    }

    [Test]
    public void GeneratedUpgradeWeights_ZeroWeightDisablesCandidate()
    {
        GeneratedUpgradeSettings settings = Track(ScriptableObject.CreateInstance<GeneratedUpgradeSettings>());

        Assert.That(WeaponUpgradeCatalog.TryGet(
            WeaponUpgrades.UpgradeType.KnifeDamageFlat,
            out WeaponUpgradeDefinition disabledWeapon), Is.True);
        Assert.That(WeaponUpgradeCatalog.TryGet(
            WeaponUpgrades.UpgradeType.KnifeDamagePercent,
            out WeaponUpgradeDefinition enabledWeapon), Is.True);

        settings.weaponWeights.Add(new GeneratedUpgradeSettings.WeaponWeight
        {
            type = disabledWeapon.Type,
            weight = 0f,
        });
        settings.weaponWeights.Add(new GeneratedUpgradeSettings.WeaponWeight
        {
            type = enabledWeapon.Type,
            weight = 1f,
        });

        Assert.That(
            settings.PickWeaponUpgrade(new[] { disabledWeapon, enabledWeapon }),
            Is.SameAs(enabledWeapon));

        settings.FindWeaponWeight(enabledWeapon.Type).weight = 0f;
        Assert.That(
            settings.PickWeaponUpgrade(new[] { disabledWeapon, enabledWeapon }),
            Is.Null);

        var disabledAccessory = AccessoriesUpgrades.StatUpgradeType.MaxHealthFlat;
        var enabledAccessory = AccessoriesUpgrades.StatUpgradeType.ArmorFlat;
        settings.accessoryWeights.Add(new GeneratedUpgradeSettings.AccessoryWeight
        {
            type = disabledAccessory,
            weight = 0f,
        });
        settings.accessoryWeights.Add(new GeneratedUpgradeSettings.AccessoryWeight
        {
            type = enabledAccessory,
            weight = 1f,
        });

        Assert.That(
            settings.PickAccessoryUpgrade(new[] { disabledAccessory, enabledAccessory }),
            Is.EqualTo(enabledAccessory));

        settings.FindAccessoryWeight(enabledAccessory).weight = 0f;
        Assert.That(
            settings.PickAccessoryUpgrade(new[] { disabledAccessory, enabledAccessory }),
            Is.EqualTo(AccessoriesUpgrades.StatUpgradeType.None));
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

    private static void SetFloat(Object target, string propertyName, float value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnum(Object target, string propertyName, int value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).enumValueIndex = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void InvokeAccessoryEnable(AccessoryBehaviour behaviour)
    {
        typeof(AccessoryBehaviour)
            .GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(behaviour, null);
    }
}

public sealed class TestDamageProvider : MonoBehaviour, IPlayerDamageMultiplierProvider
{
    public float Multiplier = 1f;
    public float DamageMultiplier => Multiplier;
}

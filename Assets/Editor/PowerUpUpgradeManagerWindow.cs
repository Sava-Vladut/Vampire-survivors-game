using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PowerUpUpgradeManagerWindow : EditorWindow
{
    private const string DefaultPlayerPrefab = "Assets/Prefabs/Player.prefab";
    private const string SettingsDirectory = "Assets/Resources";
    private const string SettingsPath = SettingsDirectory + "/GeneratedUpgradeSettings.asset";

    private GameObject prefabAsset;
    private GameObject prefabRoot;
    private string loadedPath;
    private Vector2 scroll;
    private bool showWeapons = true;
    private bool showAccessories = true;
    private bool showCatalog = true;
    private readonly Dictionary<EntityId, bool> foldouts = new();
    private GeneratedUpgradeSettings settings;

    [MenuItem("Tools/Power Ups/Upgrade Manager")]
    private static void Open() =>
        GetWindow<PowerUpUpgradeManagerWindow>("Upgrade Manager");

    private void OnEnable()
    {
        prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefab);
        settings = LoadOrCreateSettings();
    }

    private void OnDisable() => UnloadPrefab();

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Generated Upgrade Manager", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Controls which owned items may generate upgrades and shows every possible runtime-generated roll. " +
            "Legacy pre-authored upgrade chains are no longer used.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            prefabAsset = (GameObject)EditorGUILayout.ObjectField(
                "Player Prefab", prefabAsset, typeof(GameObject), false);

            GUI.enabled = prefabAsset != null;
            if (GUILayout.Button("Load", GUILayout.Width(70f)))
                LoadPrefab(prefabAsset);
            GUI.enabled = true;
        }

        if (prefabRoot == null)
        {
            EditorGUILayout.Space();
            DrawGeneratedCatalog();
            return;
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Editing: {loadedPath}", EditorStyles.miniLabel);
            if (GUILayout.Button("Save Prefab", GUILayout.Width(100f)))
                SavePrefab();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        showWeapons = EditorGUILayout.Foldout(showWeapons, "Upgradeable Weapons", true);
        if (showWeapons) DrawWeapons();

        showAccessories = EditorGUILayout.Foldout(showAccessories, "Upgradeable Accessories", true);
        if (showAccessories) DrawAccessories();

        showCatalog = EditorGUILayout.Foldout(showCatalog, "All Generated Upgrades", true);
        if (showCatalog) DrawGeneratedCatalogContents();

        EditorGUILayout.EndScrollView();
    }

    private void LoadPrefab(GameObject asset)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path) ||
            PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.NotAPrefab)
        {
            EditorUtility.DisplayDialog("Upgrade Manager", "Select a prefab asset.", "OK");
            return;
        }

        UnloadPrefab();
        loadedPath = path;
        prefabRoot = PrefabUtility.LoadPrefabContents(path);
        foldouts.Clear();
    }

    private void SavePrefab()
    {
        if (prefabRoot == null || string.IsNullOrEmpty(loadedPath)) return;
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, loadedPath);
        AssetDatabase.SaveAssets();
        ShowNotification(new GUIContent("Prefab saved"));
    }

    private void UnloadPrefab()
    {
        if (prefabRoot != null)
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        prefabRoot = null;
        loadedPath = null;
        foldouts.Clear();
    }

    private void DrawWeapons()
    {
        var owners = new HashSet<GameObject>();
        foreach (var knife in prefabRoot.GetComponentsInChildren<Knife>(true))
            owners.Add(knife.gameObject);
        foreach (var shooter in prefabRoot.GetComponentsInChildren<SimpleShooter>(true))
            owners.Add(shooter.gameObject);

        foreach (var owner in owners)
        {
            bool knife = owner.GetComponent<Knife>() != null;
            bool tick = owner.GetComponent<WeaponTick>() != null;
            DrawOwner(owner, knife ? "Knife" : "Shooter",
                $"{(knife ? 20 : 18)} weapon types" + (tick ? " + 6 timing types" : ""));
        }
    }

    private void DrawAccessories()
    {
        foreach (var accessory in prefabRoot.GetComponentsInChildren<Accessory>(true))
        {
            if (accessory.transform.parent != null &&
                accessory.transform.parent.GetComponentInParent<Accessory>(true) != null)
                continue;

            string name = string.IsNullOrWhiteSpace(accessory.AccesoryName)
                ? accessory.name
                : accessory.AccesoryName.Trim();
            bool boots = name.Equals("Boots", StringComparison.OrdinalIgnoreCase);
            DrawOwner(accessory.gameObject, name, boots ? "14 types (includes movement)" : "12 stat types");
        }
    }

    private void DrawOwner(GameObject owner, string label, string detail)
    {
        EntityId id = owner.GetEntityId();
        foldouts.TryGetValue(id, out bool expanded);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            foldouts[id] = EditorGUILayout.Foldout(expanded, $"{owner.name}  [{label}]", true);
            if (!foldouts[id]) return;

            DrawEligibility(owner);
            EditorGUILayout.LabelField("Generated pool", detail);
        }
    }

    private static void DrawEligibility(GameObject owner)
    {
        var eligibility = owner.GetComponent<GeneratedUpgradeEligibility>();
        bool current = eligibility == null || eligibility.allowGeneratedUpgrades;
        bool next = EditorGUILayout.ToggleLeft("Allow generated upgrade drops", current);
        if (next == current) return;

        if (eligibility == null)
            eligibility = Undo.AddComponent<GeneratedUpgradeEligibility>(owner);

        Undo.RecordObject(eligibility, "Change Upgrade Eligibility");
        eligibility.allowGeneratedUpgrades = next;
        EditorUtility.SetDirty(eligibility);
    }

    private void DrawGeneratedCatalog()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawGeneratedCatalogContents();
        EditorGUILayout.EndScrollView();
    }

    private void DrawGeneratedCatalogContents()
    {
        DrawRaritySettings();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Weapon upgrades", EditorStyles.boldLabel);
        DrawHeader();
        foreach (WeaponUpgrades.UpgradeType type in Enum.GetValues(typeof(WeaponUpgrades.UpgradeType)))
        {
            if (!WeaponUpgrades.IsGeneratedType(type))
                continue;
            DrawWeaponCatalogRow(type);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Accessory upgrades", EditorStyles.boldLabel);
        DrawHeader();
        foreach (AccessoriesUpgrades.StatUpgradeType type in Enum.GetValues(typeof(AccessoriesUpgrades.StatUpgradeType)))
        {
            if (type == AccessoriesUpgrades.StatUpgradeType.None) continue;
            bool bootsOnly =
                type == AccessoriesUpgrades.StatUpgradeType.MoveSpeedFlat ||
                type == AccessoriesUpgrades.StatUpgradeType.DashDistanceFlat;
            bool armorOnly = type == AccessoriesUpgrades.StatUpgradeType.ThornsFlat;
            DrawAccessoryCatalogRow(type,
                bootsOnly ? "Boots only" : armorOnly ? "Armor only" : "Any root accessory");
        }
    }

    private void DrawRaritySettings()
    {
        EditorGUILayout.LabelField("Rarity odds and strength", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Frequency is a relative weight. Strength multiplies generated upgrade values after the stat is rolled.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Rarity", EditorStyles.miniBoldLabel, GUILayout.MinWidth(140f));
            EditorGUILayout.LabelField("Frequency", EditorStyles.miniBoldLabel, GUILayout.Width(80f));
            EditorGUILayout.LabelField("Chance", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
            EditorGUILayout.LabelField("Strength", EditorStyles.miniBoldLabel, GUILayout.Width(95f));
        }

        float totalFrequency = 0f;
        if (settings != null && settings.raritySettings != null)
        {
            foreach (var entry in settings.raritySettings)
                if (entry != null)
                    totalFrequency += Mathf.Max(0f, entry.frequency);
        }

        foreach (PowerUpRarity rarity in Enum.GetValues(typeof(PowerUpRarity)))
        {
            var entry = settings != null ? settings.FindRaritySetting(rarity) : null;
            if (entry == null) continue;

            DrawRarityRow(entry, totalFrequency);
        }
    }

    private void DrawRarityRow(GeneratedUpgradeSettings.PowerUpRaritySetting entry, float totalFrequency)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var rarityStyle = new GUIStyle(EditorStyles.miniLabel);
            if (ColorUtility.TryParseHtmlString(PowerUp.GetRarityColor(entry.rarity), out Color rarityColor))
                rarityStyle.normal.textColor = rarityColor;

            EditorGUILayout.LabelField(PowerUp.GetRarityDisplayName(entry.rarity), rarityStyle, GUILayout.MinWidth(140f));

            EditorGUI.BeginChangeCheck();
            float nextFrequency = Mathf.Max(0f, EditorGUILayout.FloatField(entry.frequency, GUILayout.Width(80f)));
            float chance = totalFrequency > 0f ? Mathf.Max(0f, entry.frequency) / totalFrequency : 0f;
            EditorGUILayout.LabelField($"{chance * 100f:F0}%", EditorStyles.miniLabel, GUILayout.Width(70f));

            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(95f)))
            {
                float nextStrength = Mathf.Max(0f, EditorGUILayout.FloatField(entry.strengthMultiplier, GUILayout.Width(65f)));
                EditorGUILayout.LabelField("x", EditorStyles.miniLabel, GUILayout.Width(15f));

                if (!EditorGUI.EndChangeCheck()) return;

                Undo.RecordObject(settings, "Change Power-Up Rarity Settings");
                entry.frequency = nextFrequency;
                entry.strengthMultiplier = nextStrength;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }
    }

    private static void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Upgrade", EditorStyles.miniBoldLabel, GUILayout.MinWidth(220f));
            EditorGUILayout.LabelField("Available for", EditorStyles.miniBoldLabel, GUILayout.Width(130f));
            EditorGUILayout.LabelField("Min", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
            EditorGUILayout.LabelField("Max", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
        }
    }

    private static void DrawCatalogRow(string name, string target, string range, bool header = false)
    {
        GUIStyle style = header ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(name, style, GUILayout.MinWidth(220f));
            EditorGUILayout.LabelField(target, style, GUILayout.Width(130f));
            EditorGUILayout.LabelField(range, style, GUILayout.Width(120f));
        }
    }

    private void DrawWeaponCatalogRow(WeaponUpgrades.UpgradeType type)
    {
        var range = settings != null ? settings.FindWeaponRange(type) : null;
        string typeName = type.ToString();
        bool percentage = typeName.Contains("Percent") ||
                          typeName.Contains("CritChance") ||
                          typeName.Contains("StatusApplyChanceFlat") ||
                          typeName.Contains("CullThreshold") ||
                          typeName.Contains("EchoStrikeChance") ||
                          typeName.Contains("ForkShotChance") ||
                          type == WeaponUpgrades.UpgradeType.KnifeLifestealFlat;
        DrawEditableRow(ObjectNames.NicifyVariableName(typeName), WeaponCompatibility(type), range, percentage);
    }

    private void DrawAccessoryCatalogRow(AccessoriesUpgrades.StatUpgradeType type, string target)
    {
        var range = settings != null ? settings.FindAccessoryRange(type) : null;
        bool percentage = type.ToString().Contains("Percent") ||
                          type == AccessoriesUpgrades.StatUpgradeType.FireResist ||
                          type == AccessoriesUpgrades.StatUpgradeType.ColdResist ||
                          type == AccessoriesUpgrades.StatUpgradeType.LightningResist ||
                          type == AccessoriesUpgrades.StatUpgradeType.PoisonResist;
        DrawEditableRow(ObjectNames.NicifyVariableName(type.ToString()), target, range, percentage);
    }

    private void DrawEditableRow(string name, string target, object rangeObject, bool percentage)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(name, EditorStyles.miniLabel, GUILayout.MinWidth(220f));
            EditorGUILayout.LabelField(target, EditorStyles.miniLabel, GUILayout.Width(130f));

            float min;
            float max;
            bool wholeNumbers;
            if (rangeObject is GeneratedUpgradeSettings.WeaponRange weapon)
            {
                min = weapon.min;
                max = weapon.max;
                wholeNumbers = weapon.wholeNumbers;
            }
            else if (rangeObject is GeneratedUpgradeSettings.AccessoryRange accessory)
            {
                min = accessory.min;
                max = accessory.max;
                wholeNumbers = accessory.wholeNumbers;
            }
            else
            {
                EditorGUILayout.LabelField("Random enum value", EditorStyles.miniLabel, GUILayout.Width(140f));
                return;
            }

            float displayScale = percentage ? 100f : 1f;
            EditorGUI.BeginChangeCheck();
            float nextMin = wholeNumbers
                ? EditorGUILayout.IntField(Mathf.RoundToInt(min), GUILayout.Width(70f))
                : EditorGUILayout.FloatField(min * displayScale, GUILayout.Width(70f)) / displayScale;
            float nextMax = wholeNumbers
                ? EditorGUILayout.IntField(Mathf.RoundToInt(max), GUILayout.Width(70f))
                : EditorGUILayout.FloatField(max * displayScale, GUILayout.Width(70f)) / displayScale;
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(settings, "Change Generated Upgrade Range");
            nextMax = Mathf.Max(nextMin, nextMax);
            if (rangeObject is GeneratedUpgradeSettings.WeaponRange changedWeapon)
            {
                changedWeapon.min = nextMin;
                changedWeapon.max = nextMax;
            }
            else if (rangeObject is GeneratedUpgradeSettings.AccessoryRange changedAccessory)
            {
                changedAccessory.min = nextMin;
                changedAccessory.max = nextMax;
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }

    private static GeneratedUpgradeSettings LoadOrCreateSettings()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GeneratedUpgradeSettings>(SettingsPath);
        if (asset == null)
        {
            if (!AssetDatabase.IsValidFolder(SettingsDirectory))
                AssetDatabase.CreateFolder("Assets", "Resources");
            asset = CreateInstance<GeneratedUpgradeSettings>();
            AssetDatabase.CreateAsset(asset, SettingsPath);
        }

        int weaponCount = asset.weaponRanges.Count;
        int accessoryCount = asset.accessoryRanges.Count;
        int rarityCount = asset.raritySettings?.Count ?? 0;
        asset.EnsureAllRanges();
        if (weaponCount != asset.weaponRanges.Count ||
            accessoryCount != asset.accessoryRanges.Count ||
            rarityCount != asset.raritySettings.Count)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
        return asset;
    }

    private static string WeaponCompatibility(WeaponUpgrades.UpgradeType type)
    {
        if (type >= WeaponUpgrades.UpgradeType.KnifeDamageFlat &&
            type <= WeaponUpgrades.UpgradeType.KnifeCullThreshold) return "Knife";
        if (type >= WeaponUpgrades.UpgradeType.ShooterDamageFlat &&
            type <= WeaponUpgrades.UpgradeType.ShooterChainHits) return "Shooter";
        if (type == WeaponUpgrades.UpgradeType.KnifeEchoStrikeChance) return "Knife";
        if (type == WeaponUpgrades.UpgradeType.ShooterForkShotChance ||
            type == WeaponUpgrades.UpgradeType.ShooterPenetrationFlat) return "Shooter";
        return "WeaponTick";
    }

    private static string WeaponRange(WeaponUpgrades.UpgradeType type)
    {
        string name = type.ToString();
        if (name.Contains("DamageFlat")) return "+1 to +6";
        if (name.Contains("DamagePercent")) return "+3% to +12%";
        if (name.Contains("CritChance")) return "+2% to +10%";
        if (name.Contains("CritMultiplier")) return "+0.05 to +0.30";
        if (name.Contains("StatusApplyChanceFlat")) return "+3% to +15%";
        if (name.Contains("StatusApplyChancePercent")) return "+5% to +25%";
        if (name.Contains("StatusDurationFlat")) return "+0.25s to +1.5s";
        if (name.Contains("StatusDurationPercent")) return "+5% to +25%";
        if (name.Contains("EnableStatusEffect")) return "Enable";
        if (name.Contains("StatusEffectIndex")) return "Random status";
        if (name.Contains("DamageTypeIndex")) return "Damage type";
        if (name.Contains("Knockback")) return "+0.25 to +1.5";
        if (name.Contains("CullThreshold")) return "+1% to +3%";
        if (name.Contains("PenetrationFlat")) return "+1";
        if (name.Contains("ChainHits")) return "+1";
        if (name.Contains("MaxTargets") || name.Contains("ProjectileCount")) return "+1 to +2";
        if (type == WeaponUpgrades.UpgradeType.KnifeRadiusFlat) return "+0.05 to +0.50";
        if (type == WeaponUpgrades.UpgradeType.KnifeRadiusPercent) return "+3% to +15%";
        if (type == WeaponUpgrades.UpgradeType.KnifeLifestealFlat) return "+1% to +8%";
        if (type == WeaponUpgrades.UpgradeType.KnifeLifestealPercent) return "+5% to +20%";
        if (type == WeaponUpgrades.UpgradeType.KnifeSplashRadiusFlat) return "+0.10 to +0.75";
        if (type == WeaponUpgrades.UpgradeType.KnifeSplashRadiusPercent) return "+5% to +25%";
        if (type == WeaponUpgrades.UpgradeType.KnifeSplashDamagePercentFlat) return "+3% to +15%";
        if (type == WeaponUpgrades.UpgradeType.KnifeSplashDamagePercentPercent) return "+5% to +25%";
        if (type == WeaponUpgrades.UpgradeType.ShooterSpreadAngleFlat) return "+1° to +10°";
        if (type == WeaponUpgrades.UpgradeType.ShooterSpreadAnglePercent) return "+5% to +25%";
        if (type == WeaponUpgrades.UpgradeType.ShooterProjectileSpeedFlat) return "+0.25 to +2.5";
        if (type == WeaponUpgrades.UpgradeType.ShooterProjectileSpeedPercent) return "+5% to +25%";
        if (type == WeaponUpgrades.UpgradeType.ShooterLifetimeFlat) return "+0.15s to +1s";
        if (type == WeaponUpgrades.UpgradeType.ShooterLifetimePercent) return "+5% to +25%";
        if (type == WeaponUpgrades.UpgradeType.TickRateFlat) return "0.03s to 0.25s";
        if (type == WeaponUpgrades.UpgradeType.TickRatePercent) return "3% to 15%";
        if (type == WeaponUpgrades.UpgradeType.KnifeEchoStrikeChance) return "+8% to +20%";
        if (type == WeaponUpgrades.UpgradeType.ShooterForkShotChance) return "+8% to +20%";
        return "—";
    }

    private static string AccessoryRange(AccessoriesUpgrades.StatUpgradeType type)
    {
        switch (type)
        {
            case AccessoriesUpgrades.StatUpgradeType.MaxHealthFlat: return "+15 to +60";
            case AccessoriesUpgrades.StatUpgradeType.MaxHealthPercent: return "+5% to +25%";
            case AccessoriesUpgrades.StatUpgradeType.RegenFlat: return "+0.10 to +1.50/s";
            case AccessoriesUpgrades.StatUpgradeType.ArmorFlat: return "+1 to +6";
            case AccessoriesUpgrades.StatUpgradeType.ArmorPercent: return "+5% to +25%";
            case AccessoriesUpgrades.StatUpgradeType.EvasionFlat: return "+2 to +12";
            case AccessoriesUpgrades.StatUpgradeType.EvasionPercent: return "+5% to +25%";
            case AccessoriesUpgrades.StatUpgradeType.FireResist:
            case AccessoriesUpgrades.StatUpgradeType.ColdResist:
            case AccessoriesUpgrades.StatUpgradeType.LightningResist:
            case AccessoriesUpgrades.StatUpgradeType.PoisonResist: return "+5% to +20%";
            case AccessoriesUpgrades.StatUpgradeType.MoveSpeedFlat: return "+0.15 to +0.75";
            case AccessoriesUpgrades.StatUpgradeType.DashDistanceFlat: return "+0.25 to +1.50";
            case AccessoriesUpgrades.StatUpgradeType.ThornsFlat: return "+2 to +12";
            default: return "—";
        }
    }

}

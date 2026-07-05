using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PowerUpUpgradeManagerWindow : EditorWindow
{
    private const string DefaultPlayerPrefab = "Assets/Prefabs/Player.prefab";

    private GameObject prefabAsset;
    private GameObject prefabRoot;
    private string loadedPath;
    private Vector2 scroll;
    private bool showWeapons = true;
    private bool showAccessories = true;
    private bool showCatalog = true;
    private readonly Dictionary<int, bool> foldouts = new();

    [MenuItem("Tools/Power Ups/Upgrade Manager")]
    private static void Open() =>
        GetWindow<PowerUpUpgradeManagerWindow>("Upgrade Manager");

    private void OnEnable()
    {
        prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefab);
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
        int id = owner.GetEntityId();
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

    private static void DrawGeneratedCatalogContents()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Weapon upgrades", EditorStyles.boldLabel);
        DrawHeader();
        foreach (WeaponUpgrades.UpgradeType type in Enum.GetValues(typeof(WeaponUpgrades.UpgradeType)))
        {
            if (!WeaponUpgrades.IsGeneratedType(type))
                continue;
            DrawCatalogRow(ObjectNames.NicifyVariableName(type.ToString()), WeaponCompatibility(type), WeaponRange(type));
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
            DrawCatalogRow(ObjectNames.NicifyVariableName(type.ToString()),
                bootsOnly ? "Boots only" : armorOnly ? "Armor only" : "Any root accessory",
                AccessoryRange(type));
        }
    }

    private static void DrawHeader() =>
        DrawCatalogRow("Upgrade", "Available for", "Generated value", true);

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

    private static string WeaponCompatibility(WeaponUpgrades.UpgradeType type)
    {
        if (type >= WeaponUpgrades.UpgradeType.KnifeDamageFlat &&
            type <= WeaponUpgrades.UpgradeType.KnifeExecuteChance) return "Knife";
        if (type >= WeaponUpgrades.UpgradeType.ShooterDamageFlat &&
            type <= WeaponUpgrades.UpgradeType.ShooterChainHits) return "Shooter";
        return "WeaponTick";
    }

    private static string WeaponRange(WeaponUpgrades.UpgradeType type)
    {
        string name = type.ToString();
        if (name.Contains("DamageFlat")) return "+2 to +12";
        if (name.Contains("DamagePercent")) return "+5% to +25%";
        if (name.Contains("CritChance")) return "+3% to +20%";
        if (name.Contains("CritMultiplier")) return "+0.10 to +0.60";
        if (name.Contains("StatusApplyChanceFlat")) return "+5% to +30%";
        if (name.Contains("StatusApplyChancePercent")) return "+10% to +50%";
        if (name.Contains("StatusDurationFlat")) return "+0.5s to +3s";
        if (name.Contains("StatusDurationPercent")) return "+10% to +50%";
        if (name.Contains("EnableStatusEffect")) return "Enable";
        if (name.Contains("StatusEffectIndex")) return "Random status";
        if (name.Contains("DamageTypeIndex")) return "Damage type";
        if (name.Contains("Knockback")) return "+0.5 to +3";
        if (name.Contains("ExecuteChance")) return "+1% to +5%";
        if (name.Contains("ChainHits")) return "+1 to +2";
        if (name.Contains("MaxTargets") || name.Contains("ProjectileCount") || name.Contains("BurstCountFlat")) return "+1 to +3";
        if (type == WeaponUpgrades.UpgradeType.KnifeRadiusFlat) return "+0.10 to +1.00";
        if (type == WeaponUpgrades.UpgradeType.KnifeRadiusPercent) return "+5% to +30%";
        if (type == WeaponUpgrades.UpgradeType.KnifeLifestealFlat) return "+2% to +15%";
        if (type == WeaponUpgrades.UpgradeType.KnifeLifestealPercent) return "+10% to +40%";
        if (type == WeaponUpgrades.UpgradeType.KnifeSplashRadiusFlat) return "+0.20 to +1.50";
        if (type == WeaponUpgrades.UpgradeType.KnifeSplashRadiusPercent) return "+10% to +50%";
        if (type == WeaponUpgrades.UpgradeType.KnifeSplashDamagePercentFlat) return "+5% to +30%";
        if (type == WeaponUpgrades.UpgradeType.KnifeSplashDamagePercentPercent) return "+10% to +50%";
        if (type == WeaponUpgrades.UpgradeType.ShooterSpreadAngleFlat) return "+2° to +20°";
        if (type == WeaponUpgrades.UpgradeType.ShooterSpreadAnglePercent) return "+10% to +50%";
        if (type == WeaponUpgrades.UpgradeType.ShooterProjectileSpeedFlat) return "+0.5 to +5";
        if (type == WeaponUpgrades.UpgradeType.ShooterProjectileSpeedPercent) return "+10% to +50%";
        if (type == WeaponUpgrades.UpgradeType.ShooterLifetimeFlat) return "+0.3s to +2s";
        if (type == WeaponUpgrades.UpgradeType.ShooterLifetimePercent) return "+10% to +50%";
        if (type == WeaponUpgrades.UpgradeType.TickRateFlat) return "0.05s to 0.50s";
        if (type == WeaponUpgrades.UpgradeType.TickRatePercent) return "5% to 30%";
        if (type == WeaponUpgrades.UpgradeType.BurstCountPercent) return "+10% to +50%";
        if (type == WeaponUpgrades.UpgradeType.BurstSpacingFlat) return "0.02s to 0.30s";
        if (type == WeaponUpgrades.UpgradeType.BurstSpacingPercent) return "10% to 50%";
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

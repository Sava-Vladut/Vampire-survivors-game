using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PowerUpUpgradeManagerWindow : EditorWindow
{
    private const string DefaultPlayerPrefab = "Assets/Prefabs/Player.prefab";
    private const string SettingsDirectory = "Assets/Resources";
    private const string SettingsPath = SettingsDirectory + "/GeneratedUpgradeSettings.asset";

    private static readonly string[] Tabs = { "Setup", "Items", "Upgrade Ranges" };
    private static readonly string[] RangeFilters = { "All", "Knife", "Shooter", "WeaponTick", "Accessory" };

    private GameObject prefabAsset;
    private GameObject prefabRoot;
    private string loadedPath;
    private Vector2 scroll;
    private int selectedTab;
    private int rangeFilter;
    private string rangeSearch = "";
    private bool showRarity = true;
    private readonly Dictionary<string, bool> groupFoldouts = new();
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
        DrawHeader();
        DrawToolbar();

        selectedTab = GUILayout.Toolbar(selectedTab, Tabs);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(4f);

        switch (selectedTab)
        {
            case 0:
                DrawSetupTab();
                break;
            case 1:
                DrawItemsTab();
                break;
            default:
                DrawRangesTab();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Generated Upgrade Manager", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Tune generated upgrade offers, item eligibility, rarity strength, and roll ranges. " +
            "This tool edits the settings asset and loaded prefab contents; runtime roll rules are unchanged.",
            MessageType.Info);
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUILayout.LabelField("Player Prefab", GUILayout.Width(82f));
            prefabAsset = (GameObject)EditorGUILayout.ObjectField(
                prefabAsset, typeof(GameObject), false, GUILayout.MinWidth(170f));

            using (new EditorGUI.DisabledScope(prefabAsset == null))
            {
                if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                    LoadPrefab(prefabAsset);
            }

            using (new EditorGUI.DisabledScope(prefabRoot == null))
            {
                if (GUILayout.Button("Unload", EditorStyles.toolbarButton, GUILayout.Width(62f)))
                    UnloadPrefab();
                if (GUILayout.Button("Save Prefab", EditorStyles.toolbarButton, GUILayout.Width(84f)))
                    SavePrefab();
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(settings == null))
            {
                if (GUILayout.Button("Ping Settings", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                    EditorGUIUtility.PingObject(settings);
                if (GUILayout.Button("Select Settings", EditorStyles.toolbarButton, GUILayout.Width(104f)))
                    Selection.activeObject = settings;
            }
        }

        if (prefabRoot != null)
            EditorGUILayout.LabelField($"Editing: {loadedPath}", EditorStyles.miniLabel);
        else
            EditorGUILayout.LabelField("No prefab loaded. Setup and Items need a loaded prefab; Upgrade Ranges edits the shared settings asset.", EditorStyles.miniLabel);
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
        groupFoldouts.Clear();
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
        groupFoldouts.Clear();
    }

    private void DrawSetupTab()
    {
        if (!DrawRequiresLoadedPrefab())
            return;

        DrawSectionTitle("Selection Limits");
        var chooser = prefabRoot.GetComponentInChildren<PowerUpChooser>(true);
        if (chooser == null)
        {
            EditorGUILayout.HelpBox("No PowerUpChooser was found in the loaded prefab.", MessageType.Warning);
        }
        else
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawObjectLine("PowerUpChooser", chooser.gameObject);
                DrawSerializedInt(chooser, "maxWeapons", "Max Weapons", 0);
                DrawSerializedInt(chooser, "maxAccessories", "Max Accessories", 0);
            }
        }

        EditorGUILayout.Space(6f);
        DrawSectionTitle("Selection UI");
        var selectionUI = prefabRoot.GetComponentInChildren<PowerUpSelectionUI>(true);
        if (selectionUI == null)
        {
            EditorGUILayout.HelpBox("No PowerUpSelectionUI was found in the loaded prefab.", MessageType.Warning);
        }
        else
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawObjectLine("PowerUpSelectionUI", selectionUI.gameObject);
                DrawSerializedInt(selectionUI, "refreshesPerGame", "Rerolls Per Game", 0);
                DrawSerializedBool(selectionUI, "firstSelectionWeaponsOnly", "First Selection Weapons Only");
                DrawSerializedInt(selectionUI, "firstSelectionCount", "First Selection Choice Count", 1);
                DrawSerializedFloat(selectionUI, "firstOnHitBaseChance", "First On-Hit Status Chance", 0f, 1f, true);
            }
        }

        EditorGUILayout.Space(6f);
        DrawSectionTitle("Generated Offers");
        var generator = prefabRoot.GetComponentInChildren<RandomUpgradeGenerator>(true);
        if (generator == null)
        {
            EditorGUILayout.HelpBox(
                "No RandomUpgradeGenerator is saved on this prefab. The selection UI can add one at runtime, " +
                "but saving it here makes the offer counts visible and editable.",
                MessageType.Warning);

            using (new EditorGUI.DisabledScope(chooser == null))
            {
                if (GUILayout.Button("Add Generator Settings", GUILayout.Width(170f)))
                {
                    var target = chooser != null ? chooser.gameObject : prefabRoot;
                    generator = Undo.AddComponent<RandomUpgradeGenerator>(target);
                    WireSelectionGenerator(selectionUI, generator);
                    EditorUtility.SetDirty(target);
                    ShowNotification(new GUIContent("Generator settings added"));
                }
            }
        }
        else
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawObjectLine("RandomUpgradeGenerator", generator.gameObject);
                DrawSerializedInt(generator, "offersPerWeapon", "Offers Per Weapon", 1);
                DrawSerializedInt(generator, "offersPerAccessory", "Offers Per Accessory", 1);

                if (selectionUI != null && GUILayout.Button("Wire Selection UI Reference", GUILayout.Width(190f)))
                    WireSelectionGenerator(selectionUI, generator);
            }
        }
    }

    private void DrawItemsTab()
    {
        if (!DrawRequiresLoadedPrefab())
            return;

        DrawSectionTitle("Weapons");
        var weapons = CollectWeaponOwners();
        if (weapons.Count == 0)
            EditorGUILayout.HelpBox("No Knife or SimpleShooter components were found in the loaded prefab.", MessageType.Info);
        else
            DrawItemGroup("Weapons", weapons);

        EditorGUILayout.Space(6f);
        DrawSectionTitle("Accessories");
        var accessories = CollectAccessoryOwners();
        if (accessories.Count == 0)
            EditorGUILayout.HelpBox("No root Accessory components were found in the loaded prefab.", MessageType.Info);
        else
            DrawItemGroup("Accessories", accessories);
    }

    private void DrawRangesTab()
    {
        if (settings == null)
        {
            EditorGUILayout.HelpBox("GeneratedUpgradeSettings could not be loaded or created.", MessageType.Error);
            return;
        }

        showRarity = EditorGUILayout.Foldout(showRarity, "Rarity Odds and Strength", true);
        if (showRarity)
            DrawRaritySettings();

        EditorGUILayout.Space(8f);
        DrawSectionTitle("Roll Ranges");
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Search", GUILayout.Width(48f));
            rangeSearch = EditorGUILayout.TextField(rangeSearch);
            rangeFilter = EditorGUILayout.Popup(rangeFilter, RangeFilters, GUILayout.Width(120f));
        }

        DrawWeaponRangeGroup("Knife", IsKnifeType);
        DrawWeaponRangeGroup("Shooter", IsShooterType);
        DrawWeaponRangeGroup("WeaponTick", IsTickType);
        DrawAccessoryRangeGroup();
    }

    private bool DrawRequiresLoadedPrefab()
    {
        if (prefabRoot != null)
            return true;

        EditorGUILayout.HelpBox("Load a player prefab to edit this section.", MessageType.Info);
        if (prefabAsset != null && GUILayout.Button("Load Player Prefab", GUILayout.Width(150f)))
            LoadPrefab(prefabAsset);
        return false;
    }

    private void DrawSectionTitle(string title)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private void DrawObjectLine(string label, GameObject target)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(160f));
            EditorGUILayout.LabelField(target != null ? target.name : "Missing", EditorStyles.miniLabel);
            DrawSelectButtons(target);
        }
    }

    private void DrawSelectButtons(UnityEngine.Object target)
    {
        using (new EditorGUI.DisabledScope(target == null))
        {
            if (GUILayout.Button("Ping", EditorStyles.miniButtonLeft, GUILayout.Width(42f)))
                EditorGUIUtility.PingObject(target);
            if (GUILayout.Button("Select", EditorStyles.miniButtonRight, GUILayout.Width(52f)))
                Selection.activeObject = target;
        }
    }

    private static void DrawSerializedInt(Component component, string propertyName, string label, int min)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            DrawMissingProperty(label, propertyName);
            return;
        }

        serialized.Update();
        EditorGUI.BeginChangeCheck();
        int next = Mathf.Max(min, EditorGUILayout.IntField(label, property.intValue));
        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = next;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
        }
    }

    private static void DrawSerializedFloat(Component component, string propertyName, string label, float min, float max, bool percentage)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            DrawMissingProperty(label, propertyName);
            return;
        }

        float scale = percentage ? 100f : 1f;
        string suffix = percentage ? "%" : "";

        serialized.Update();
        EditorGUI.BeginChangeCheck();
        float shown = EditorGUILayout.FloatField(label, property.floatValue * scale);
        if (EditorGUI.EndChangeCheck())
        {
            property.floatValue = Mathf.Clamp(shown / scale, min, max);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
        }

        if (!string.IsNullOrEmpty(suffix))
            EditorGUILayout.LabelField($"Stored as {property.floatValue:0.###} ({property.floatValue * 100f:0.#}{suffix})", EditorStyles.miniLabel);
    }

    private static void DrawSerializedBool(Component component, string propertyName, string label)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            DrawMissingProperty(label, propertyName);
            return;
        }

        serialized.Update();
        EditorGUI.BeginChangeCheck();
        bool next = EditorGUILayout.Toggle(label, property.boolValue);
        if (EditorGUI.EndChangeCheck())
        {
            property.boolValue = next;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
        }
    }

    private static void DrawMissingProperty(string label, string propertyName)
    {
        EditorGUILayout.LabelField(label, $"Missing serialized field: {propertyName}", EditorStyles.miniLabel);
    }

    private static void WireSelectionGenerator(PowerUpSelectionUI selectionUI, RandomUpgradeGenerator generator)
    {
        if (selectionUI == null || generator == null)
            return;

        var serialized = new SerializedObject(selectionUI);
        var property = serialized.FindProperty("upgradeGenerator");
        if (property == null)
            return;

        Undo.RecordObject(selectionUI, "Wire Upgrade Generator");
        serialized.Update();
        property.objectReferenceValue = generator;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(selectionUI);
    }

    private List<ItemRow> CollectWeaponOwners()
    {
        var owners = new HashSet<GameObject>();
        foreach (var knife in prefabRoot.GetComponentsInChildren<Knife>(true))
            owners.Add(knife.gameObject);
        foreach (var shooter in prefabRoot.GetComponentsInChildren<SimpleShooter>(true))
            owners.Add(shooter.gameObject);

        var rows = new List<ItemRow>();
        foreach (var owner in owners)
        {
            bool hasKnife = owner.GetComponent<Knife>() != null;
            bool hasShooter = owner.GetComponent<SimpleShooter>() != null;
            bool hasTick = owner.GetComponent<WeaponTick>() != null;
            string kind = hasKnife ? "Knife" : hasShooter ? "Shooter" : "Weapon";
            string pool = GetWeaponPoolSummary(hasKnife, hasShooter, hasTick);
            rows.Add(new ItemRow(owner, owner.name, kind, pool));
        }

        rows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    private List<ItemRow> CollectAccessoryOwners()
    {
        var rows = new List<ItemRow>();
        foreach (var accessory in prefabRoot.GetComponentsInChildren<Accessory>(true))
        {
            if (accessory.transform.parent != null &&
                accessory.transform.parent.GetComponentInParent<Accessory>(true) != null)
                continue;

            string name = string.IsNullOrWhiteSpace(accessory.AccesoryName)
                ? accessory.name
                : accessory.AccesoryName.Trim();
            bool boots = name.Equals("Boots", StringComparison.OrdinalIgnoreCase);
            bool armor = name.Equals("Armor", StringComparison.OrdinalIgnoreCase);
            string pool = boots ? "14 accessory types incl. movement" :
                armor ? "13 accessory types incl. thorns" :
                "12 general accessory types";
            rows.Add(new ItemRow(accessory.gameObject, name, "Accessory", pool));
        }

        rows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    private static string GetWeaponPoolSummary(bool hasKnife, bool hasShooter, bool hasTick)
    {
        int count = 0;
        if (hasKnife) count += 20;
        if (hasShooter) count += 18;
        if (hasTick) count += 2;

        string baseName = hasKnife ? "knife" : hasShooter ? "shooter" : "weapon";
        return hasTick ? $"{count} {baseName} + timing types" : $"{count} {baseName} types";
    }

    private void DrawItemGroup(string key, List<ItemRow> rows)
    {
        bool expanded = GetFoldout(key, true);
        expanded = EditorGUILayout.Foldout(expanded, $"{key} ({rows.Count})", true);
        SetFoldout(key, expanded);
        if (!expanded)
            return;

        DrawItemHeader();
        foreach (var row in rows)
            DrawItemRow(row);
    }

    private static void DrawItemHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Item", EditorStyles.miniBoldLabel, GUILayout.MinWidth(160f));
            EditorGUILayout.LabelField("Kind", EditorStyles.miniBoldLabel, GUILayout.Width(82f));
            EditorGUILayout.LabelField("Generated Pool", EditorStyles.miniBoldLabel, GUILayout.MinWidth(190f));
            EditorGUILayout.LabelField("Drops", EditorStyles.miniBoldLabel, GUILayout.Width(58f));
            GUILayout.Space(100f);
        }
    }

    private void DrawItemRow(ItemRow row)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(row.Name, EditorStyles.miniLabel, GUILayout.MinWidth(160f));
            EditorGUILayout.LabelField(row.Kind, EditorStyles.miniLabel, GUILayout.Width(82f));
            EditorGUILayout.LabelField(row.Pool, EditorStyles.miniLabel, GUILayout.MinWidth(190f));
            DrawEligibilityToggle(row.Target, GUILayout.Width(58f));
            DrawSelectButtons(row.Target);
        }
    }

    private void DrawEligibilityToggle(GameObject owner, params GUILayoutOption[] options)
    {
        var eligibility = owner.GetComponent<GeneratedUpgradeEligibility>();
        bool current = eligibility == null || eligibility.allowGeneratedUpgrades;

        EditorGUI.BeginChangeCheck();
        bool next = EditorGUILayout.Toggle(current, options);
        if (!EditorGUI.EndChangeCheck())
            return;

        if (eligibility == null)
            eligibility = Undo.AddComponent<GeneratedUpgradeEligibility>(owner);

        Undo.RecordObject(eligibility, "Change Upgrade Eligibility");
        eligibility.allowGeneratedUpgrades = next;
        EditorUtility.SetDirty(eligibility);
    }

    private void DrawRaritySettings()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "Relative weight controls how often a rarity appears. Value multiplier scales generated upgrade values after the base stat roll.",
                EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Rarity", EditorStyles.miniBoldLabel, GUILayout.MinWidth(150f));
                EditorGUILayout.LabelField("Relative Weight", EditorStyles.miniBoldLabel, GUILayout.Width(110f));
                EditorGUILayout.LabelField("Chance", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
                EditorGUILayout.LabelField("Value Multiplier", EditorStyles.miniBoldLabel, GUILayout.Width(120f));
            }

            float totalFrequency = GetTotalRarityFrequency();
            foreach (PowerUpRarity rarity in Enum.GetValues(typeof(PowerUpRarity)))
            {
                var entry = settings.FindRaritySetting(rarity);
                if (entry != null)
                    DrawRarityRow(entry, totalFrequency);
            }
        }
    }

    private float GetTotalRarityFrequency()
    {
        float total = 0f;
        if (settings?.raritySettings == null)
            return total;

        foreach (var entry in settings.raritySettings)
            if (entry != null)
                total += Mathf.Max(0f, entry.frequency);
        return total;
    }

    private void DrawRarityRow(GeneratedUpgradeSettings.PowerUpRaritySetting entry, float totalFrequency)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var rarityStyle = new GUIStyle(EditorStyles.miniLabel);
            if (ColorUtility.TryParseHtmlString(PowerUp.GetRarityColor(entry.rarity), out Color rarityColor))
                rarityStyle.normal.textColor = rarityColor;

            EditorGUILayout.LabelField(PowerUp.GetRarityDisplayName(entry.rarity), rarityStyle, GUILayout.MinWidth(150f));

            EditorGUI.BeginChangeCheck();
            float nextFrequency = Mathf.Max(0f, EditorGUILayout.FloatField(entry.frequency, GUILayout.Width(110f)));
            float chance = totalFrequency > 0f ? Mathf.Max(0f, entry.frequency) / totalFrequency : 0f;
            EditorGUILayout.LabelField($"{chance * 100f:F0}%", EditorStyles.miniLabel, GUILayout.Width(70f));
            float nextMultiplier = Mathf.Max(0f, EditorGUILayout.FloatField(entry.strengthMultiplier, GUILayout.Width(86f)));
            EditorGUILayout.LabelField("x", EditorStyles.miniLabel, GUILayout.Width(20f));

            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(settings, "Change Power-Up Rarity Settings");
            entry.frequency = nextFrequency;
            entry.strengthMultiplier = nextMultiplier;
            SaveSettingsChange();
        }
    }

    private void DrawWeaponRangeGroup(string label, Func<WeaponUpgrades.UpgradeType, bool> predicate)
    {
        if (!ShouldDrawFilter(label))
            return;

        bool expanded = GetFoldout("Range:" + label, true);
        expanded = EditorGUILayout.Foldout(expanded, label, true);
        SetFoldout("Range:" + label, expanded);
        if (!expanded)
            return;

        DrawRangeHeader(false);
        foreach (WeaponUpgrades.UpgradeType type in Enum.GetValues(typeof(WeaponUpgrades.UpgradeType)))
        {
            if (!WeaponUpgrades.IsGeneratedType(type) || !predicate(type))
                continue;

            string displayName = ObjectNames.NicifyVariableName(type.ToString());
            if (!MatchesSearch(displayName))
                continue;

            DrawWeaponRangeRow(type, displayName);
        }
    }

    private void DrawAccessoryRangeGroup()
    {
        if (!ShouldDrawFilter("Accessory"))
            return;

        bool expanded = GetFoldout("Range:Accessory", true);
        expanded = EditorGUILayout.Foldout(expanded, "Accessory", true);
        SetFoldout("Range:Accessory", expanded);
        if (!expanded)
            return;

        DrawRangeHeader(false);
        foreach (AccessoriesUpgrades.StatUpgradeType type in Enum.GetValues(typeof(AccessoriesUpgrades.StatUpgradeType)))
        {
            if (type == AccessoriesUpgrades.StatUpgradeType.None)
                continue;

            string displayName = ObjectNames.NicifyVariableName(type.ToString());
            if (!MatchesSearch(displayName))
                continue;

            DrawAccessoryRangeRow(type, displayName);
        }
    }

    private bool ShouldDrawFilter(string group) =>
        rangeFilter == 0 || RangeFilters[rangeFilter] == group;

    private bool MatchesSearch(string value) =>
        string.IsNullOrWhiteSpace(rangeSearch) ||
        value.IndexOf(rangeSearch.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

    private static void DrawRangeHeader(bool includeTarget)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Upgrade", EditorStyles.miniBoldLabel, GUILayout.MinWidth(220f));
            if (includeTarget)
                EditorGUILayout.LabelField("Available For", EditorStyles.miniBoldLabel, GUILayout.Width(120f));
            EditorGUILayout.LabelField("Min", EditorStyles.miniBoldLabel, GUILayout.Width(72f));
            EditorGUILayout.LabelField("Max", EditorStyles.miniBoldLabel, GUILayout.Width(72f));
            EditorGUILayout.LabelField("Whole", EditorStyles.miniBoldLabel, GUILayout.Width(48f));
            GUILayout.Space(58f);
        }
    }

    private void DrawWeaponRangeRow(WeaponUpgrades.UpgradeType type, string displayName)
    {
        var range = settings.FindWeaponRange(type);
        bool percentage = IsPercentageWeapon(type);

        if (range == null)
        {
            DrawRangeFallback(displayName, "Random enum value");
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(displayName, EditorStyles.miniLabel, GUILayout.MinWidth(220f));
            DrawEditableRange(range, percentage);

            if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(52f)) &&
                GeneratedUpgradeSettings.TryGetDefaultWeaponRange(type, out float min, out float max, out bool whole))
            {
                Undo.RecordObject(settings, "Reset Generated Upgrade Range");
                range.min = min;
                range.max = max;
                range.wholeNumbers = whole;
                SaveSettingsChange();
            }
        }
    }

    private void DrawAccessoryRangeRow(AccessoriesUpgrades.StatUpgradeType type, string displayName)
    {
        var range = settings.FindAccessoryRange(type);
        bool percentage = IsPercentageAccessory(type);

        if (range == null)
        {
            DrawRangeFallback(displayName, "No configured range");
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(displayName, EditorStyles.miniLabel, GUILayout.MinWidth(220f));
            DrawEditableRange(range, percentage);

            if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(52f)) &&
                GeneratedUpgradeSettings.TryGetDefaultAccessoryRange(type, out float min, out float max, out bool whole))
            {
                Undo.RecordObject(settings, "Reset Generated Upgrade Range");
                range.min = min;
                range.max = max;
                range.wholeNumbers = whole;
                SaveSettingsChange();
            }
        }
    }

    private static void DrawRangeFallback(string name, string message)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(name, EditorStyles.miniLabel, GUILayout.MinWidth(220f));
            EditorGUILayout.LabelField(message, EditorStyles.miniLabel, GUILayout.MinWidth(190f));
        }
    }

    private void DrawEditableRange(GeneratedUpgradeSettings.WeaponRange range, bool percentage)
    {
        EditorGUI.BeginChangeCheck();
        float nextMin = DrawRangeValue(range.min, range.wholeNumbers, percentage, GUILayout.Width(72f));
        float nextMax = DrawRangeValue(range.max, range.wholeNumbers, percentage, GUILayout.Width(72f));
        bool nextWhole = EditorGUILayout.Toggle(range.wholeNumbers, GUILayout.Width(48f));
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(settings, "Change Generated Upgrade Range");
        range.min = nextMin;
        range.max = Mathf.Max(nextMin, nextMax);
        range.wholeNumbers = nextWhole;
        SaveSettingsChange();
    }

    private void DrawEditableRange(GeneratedUpgradeSettings.AccessoryRange range, bool percentage)
    {
        EditorGUI.BeginChangeCheck();
        float nextMin = DrawRangeValue(range.min, range.wholeNumbers, percentage, GUILayout.Width(72f));
        float nextMax = DrawRangeValue(range.max, range.wholeNumbers, percentage, GUILayout.Width(72f));
        bool nextWhole = EditorGUILayout.Toggle(range.wholeNumbers, GUILayout.Width(48f));
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(settings, "Change Generated Upgrade Range");
        range.min = nextMin;
        range.max = Mathf.Max(nextMin, nextMax);
        range.wholeNumbers = nextWhole;
        SaveSettingsChange();
    }

    private static float DrawRangeValue(float value, bool wholeNumbers, bool percentage, params GUILayoutOption[] options)
    {
        if (wholeNumbers)
            return EditorGUILayout.IntField(Mathf.RoundToInt(value), options);

        float scale = percentage ? 100f : 1f;
        return EditorGUILayout.FloatField(value * scale, options) / scale;
    }

    private void SaveSettingsChange()
    {
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private bool GetFoldout(string key, bool defaultValue)
    {
        if (!groupFoldouts.TryGetValue(key, out bool value))
            value = defaultValue;
        return value;
    }

    private void SetFoldout(string key, bool value)
    {
        groupFoldouts[key] = value;
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

    private static bool IsKnifeType(WeaponUpgrades.UpgradeType type) =>
        (type >= WeaponUpgrades.UpgradeType.KnifeDamageFlat &&
         type <= WeaponUpgrades.UpgradeType.KnifeCullThreshold) ||
        type == WeaponUpgrades.UpgradeType.KnifeEchoStrikeChance;

    private static bool IsShooterType(WeaponUpgrades.UpgradeType type) =>
        (type >= WeaponUpgrades.UpgradeType.ShooterDamageFlat &&
         type <= WeaponUpgrades.UpgradeType.ShooterChainHits) ||
        type == WeaponUpgrades.UpgradeType.ShooterForkShotChance ||
        type == WeaponUpgrades.UpgradeType.ShooterPenetrationFlat;

    private static bool IsTickType(WeaponUpgrades.UpgradeType type) =>
        type == WeaponUpgrades.UpgradeType.TickRateFlat ||
        type == WeaponUpgrades.UpgradeType.TickRatePercent;

    private static bool IsPercentageWeapon(WeaponUpgrades.UpgradeType type)
    {
        string name = type.ToString();
        return name.Contains("Percent") ||
               name.Contains("CritChance") ||
               name.Contains("StatusApplyChanceFlat") ||
               name.Contains("CullThreshold") ||
               name.Contains("EchoStrikeChance") ||
               name.Contains("ForkShotChance") ||
               type == WeaponUpgrades.UpgradeType.KnifeLifestealFlat;
    }

    private static bool IsPercentageAccessory(AccessoriesUpgrades.StatUpgradeType type) =>
        type.ToString().Contains("Percent") ||
        type == AccessoriesUpgrades.StatUpgradeType.FireResist ||
        type == AccessoriesUpgrades.StatUpgradeType.ColdResist ||
        type == AccessoriesUpgrades.StatUpgradeType.LightningResist ||
        type == AccessoriesUpgrades.StatUpgradeType.PoisonResist;

    private readonly struct ItemRow
    {
        public ItemRow(GameObject target, string name, string kind, string pool)
        {
            Target = target;
            Name = name;
            Kind = kind;
            Pool = pool;
        }

        public GameObject Target { get; }
        public string Name { get; }
        public string Kind { get; }
        public string Pool { get; }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PowerUpUpgradeManagerWindow : EditorWindow
{
    private const string SettingsDirectory = "Assets/Resources";
    private const string SettingsPath = SettingsDirectory + "/GeneratedUpgradeSettings.asset";

    private const float RowHeight = 24f;
    private const float CompactRowHeight = 68f;
    private const float CompactLayoutBreakpoint = 1080f;
    private const float ColumnGap = 6f;
    private const float UpgradeWidth = 220f;
    private const float WeightWidth = 92f;
    private const float ChanceWidth = 112f;
    private const float ValueWidth = 78f;
    private const float IntegerWidth = 62f;
    private const float ResetWidth = 52f;

    private static readonly string[] RangeFilters = { "All", "Knife", "Shooter", "WeaponTick", "Accessory" };

    private readonly Dictionary<string, bool> groupFoldouts = new();
    private GeneratedUpgradeSettings settings;
    private Vector2 scroll;
    private int rangeFilter;
    private string rangeSearch = "";
    private bool showRarity = true;

    private GUIStyle pageTitleStyle;
    private GUIStyle pageSubtitleStyle;
    private GUIStyle statusStyle;
    private GUIStyle sectionTitleStyle;
    private GUIStyle sectionDescriptionStyle;
    private GUIStyle rowLabelStyle;
    private GUIStyle rowDescriptionStyle;
    private GUIStyle columnLabelStyle;

    [MenuItem("Tools/Power Ups/Upgrade Manager")]
    private static void Open() =>
        GetWindow<PowerUpUpgradeManagerWindow>("Upgrade Ranges");

    private void OnEnable()
    {
        titleContent = new GUIContent("Upgrade Ranges");
        minSize = new Vector2(820f, 520f);
        settings = LoadOrCreateSettings();
    }

    private void OnGUI()
    {
        EnsureStyles();
        DrawPageHeader();
        DrawSettingsToolbar();

        if (settings == null)
        {
            DrawMissingSettingsState();
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(10f);

        DrawRaritySection();
        EditorGUILayout.Space(10f);
        DrawRangesSection();

        EditorGUILayout.Space(16f);
        EditorGUILayout.EndScrollView();
    }

    private void EnsureStyles()
    {
        if (pageTitleStyle != null)
            return;

        pageTitleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 22,
            alignment = TextAnchor.MiddleLeft
        };
        pageTitleStyle.normal.textColor = EditorGUIUtility.isProSkin
            ? new Color(0.94f, 0.96f, 0.98f)
            : new Color(0.10f, 0.13f, 0.16f);

        pageSubtitleStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft
        };
        pageSubtitleStyle.normal.textColor = EditorGUIUtility.isProSkin
            ? new Color(0.67f, 0.72f, 0.76f)
            : new Color(0.34f, 0.38f, 0.42f);

        statusStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9
        };
        statusStyle.normal.textColor = EditorGUIUtility.isProSkin
            ? new Color(0.43f, 0.84f, 0.76f)
            : new Color(0.08f, 0.43f, 0.37f);

        sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13
        };

        sectionDescriptionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            fontSize = 10
        };

        rowLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip
        };

        rowDescriptionStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip
        };
        rowDescriptionStyle.normal.textColor = EditorGUIUtility.isProSkin
            ? new Color(0.68f, 0.71f, 0.74f)
            : new Color(0.34f, 0.37f, 0.40f);

        columnLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleLeft
        };
    }

    private void DrawPageHeader()
    {
        Rect header = EditorGUILayout.GetControlRect(false, 72f);
        Color background = EditorGUIUtility.isProSkin
            ? new Color(0.105f, 0.13f, 0.15f)
            : new Color(0.88f, 0.92f, 0.93f);
        Color accent = new Color(0.18f, 0.67f, 0.62f);

        EditorGUI.DrawRect(header, background);
        EditorGUI.DrawRect(new Rect(header.x, header.y, 4f, header.height), accent);

        Rect titleRect = new Rect(header.x + 20f, header.y + 10f, header.width - 220f, 28f);
        Rect subtitleRect = new Rect(header.x + 20f, header.y + 38f, header.width - 220f, 20f);
        GUI.Label(titleRect, "Upgrade Ranges", pageTitleStyle);
        GUI.Label(subtitleRect, "Shape generated offers, rarity odds, and every rollable value from one place.", pageSubtitleStyle);

        Rect badge = new Rect(header.xMax - 164f, header.y + 23f, 140f, 24f);
        EditorGUI.DrawRect(badge, EditorGUIUtility.isProSkin
            ? new Color(0.12f, 0.27f, 0.25f)
            : new Color(0.72f, 0.88f, 0.84f));
        GUI.Label(badge, "SAVED AUTOMATICALLY", statusStyle);
    }

    private void DrawSettingsToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Settings", EditorStyles.miniBoldLabel, GUILayout.Width(52f));
            EditorGUILayout.LabelField(SettingsPath, EditorStyles.miniLabel, GUILayout.MinWidth(260f));
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(settings == null))
            {
                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(46f)))
                    EditorGUIUtility.PingObject(settings);
                if (GUILayout.Button("Select asset", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                    Selection.activeObject = settings;
            }
            GUILayout.Space(4f);
        }
    }

    private void DrawMissingSettingsState()
    {
        EditorGUILayout.Space(18f);
        EditorGUILayout.HelpBox("GeneratedUpgradeSettings could not be loaded or created.", MessageType.Error);
        if (GUILayout.Button("Try again", GUILayout.Width(100f)))
            settings = LoadOrCreateSettings();
    }

    private void DrawRaritySection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                showRarity = EditorGUILayout.Foldout(showRarity, "Rarity odds and strength", true, sectionTitleStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Final chance is calculated from all relative weights.", EditorStyles.miniLabel, GUILayout.Width(288f));
            }

            if (!showRarity)
                return;

            EditorGUILayout.LabelField(
                "Weight decides how often each rarity appears. Multiplier scales the rolled upgrade value.",
                sectionDescriptionStyle);
            EditorGUILayout.Space(5f);

            DrawRarityHeader();
            float totalFrequency = GetTotalRarityFrequency();
            int rowIndex = 0;
            foreach (PowerUpRarity rarity in Enum.GetValues(typeof(PowerUpRarity)))
            {
                var entry = settings.FindRaritySetting(rarity);
                if (entry != null)
                    DrawRarityRow(entry, totalFrequency, rowIndex++);
            }
            EditorGUILayout.Space(2f);
        }
    }

    private void DrawRarityHeader()
    {
        Rect row = EditorGUILayout.GetControlRect(false, 20f);
        GetRarityColumns(row, out Rect rarityRect, out Rect weightRect, out Rect chanceRect, out Rect multiplierRect);
        GUI.Label(rarityRect, "RARITY", columnLabelStyle);
        GUI.Label(weightRect, "RELATIVE WEIGHT", columnLabelStyle);
        GUI.Label(chanceRect, "RESULTING CHANCE", columnLabelStyle);
        GUI.Label(multiplierRect, "VALUE MULTIPLIER", columnLabelStyle);
    }

    private void DrawRarityRow(GeneratedUpgradeSettings.PowerUpRaritySetting entry, float totalFrequency, int rowIndex)
    {
        Rect row = EditorGUILayout.GetControlRect(false, RowHeight);
        DrawAlternatingRow(row, rowIndex);
        GetRarityColumns(row, out Rect rarityRect, out Rect weightRect, out Rect chanceRect, out Rect multiplierRect);

        Color rarityColor = GetRarityColor(entry.rarity);
        EditorGUI.DrawRect(new Rect(row.x, row.y + 4f, 3f, row.height - 8f), rarityColor);

        var rarityLabelStyle = new GUIStyle(rowLabelStyle);
        rarityLabelStyle.normal.textColor = rarityColor;
        GUI.Label(rarityRect, PowerUp.GetRarityDisplayName(entry.rarity), rarityLabelStyle);

        float chance = totalFrequency > 0f ? Mathf.Max(0f, entry.frequency) / totalFrequency : 0f;
        DrawChanceRail(chanceRect, chance, rarityColor);

        EditorGUI.BeginChangeCheck();
        float nextFrequency = Mathf.Max(0f, EditorGUI.FloatField(weightRect, entry.frequency));
        float nextMultiplier = Mathf.Max(0f, EditorGUI.FloatField(multiplierRect, entry.strengthMultiplier));
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(settings, "Change Power-Up Rarity Settings");
        entry.frequency = nextFrequency;
        entry.strengthMultiplier = nextMultiplier;
        SaveSettingsChange();
    }

    private static void DrawChanceRail(Rect rect, float chance, Color color)
    {
        Rect rail = new Rect(rect.x, rect.y + 4f, rect.width, rect.height - 8f);
        Color track = EditorGUIUtility.isProSkin
            ? new Color(0.08f, 0.09f, 0.10f)
            : new Color(0.75f, 0.77f, 0.78f);
        EditorGUI.DrawRect(rail, track);
        EditorGUI.DrawRect(new Rect(rail.x, rail.y, rail.width * Mathf.Clamp01(chance), rail.height),
            new Color(color.r, color.g, color.b, EditorGUIUtility.isProSkin ? 0.72f : 0.58f));

        var chanceStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
        chanceStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.08f, 0.09f, 0.10f);
        GUI.Label(rail, $"{chance * 100f:0.#}%", chanceStyle);
    }

    private static void GetRarityColumns(
        Rect row,
        out Rect rarityRect,
        out Rect weightRect,
        out Rect chanceRect,
        out Rect multiplierRect)
    {
        const float weightWidth = 112f;
        const float chanceWidth = 176f;
        const float multiplierWidth = 112f;
        float rarityWidth = Mathf.Max(150f, row.width - weightWidth - chanceWidth - multiplierWidth - ColumnGap * 3f - 12f);
        float x = row.x + 6f;

        rarityRect = new Rect(x, row.y + 2f, rarityWidth, row.height - 4f);
        x += rarityWidth + ColumnGap;
        weightRect = new Rect(x, row.y + 3f, weightWidth, row.height - 6f);
        x += weightWidth + ColumnGap;
        chanceRect = new Rect(x, row.y, chanceWidth, row.height);
        x += chanceWidth + ColumnGap;
        multiplierRect = new Rect(x, row.y + 3f, multiplierWidth, row.height - 6f);
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

    private void DrawRangesSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Upgrade weights and roll ranges", sectionTitleStyle);
            EditorGUILayout.LabelField(
                "Weight 1 is the baseline; 0 disables an upgrade. Resulting chance is its share of the category pool before runtime eligibility removes unavailable upgrades.",
                sectionDescriptionStyle);
            EditorGUILayout.Space(6f);

            DrawRangeControls();
            EditorGUILayout.Space(7f);

            int shown = CountVisibleRows();
            if (shown == 0)
            {
                EditorGUILayout.HelpBox("No upgrades match the current search and filter.", MessageType.Info);
                return;
            }

            DrawWeaponRangeGroup("Knife", IsKnifeType, new Color(0.87f, 0.33f, 0.31f));
            DrawWeaponRangeGroup("Shooter", IsShooterType, new Color(0.25f, 0.66f, 0.84f));
            DrawWeaponRangeGroup("WeaponTick", IsTickType, new Color(0.92f, 0.67f, 0.24f));
            DrawAccessoryRangeGroup(new Color(0.66f, 0.45f, 0.88f));
        }
    }

    private void DrawRangeControls()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField;
            rangeSearch = EditorGUILayout.TextField(rangeSearch, searchStyle, GUILayout.MinWidth(190f));
            if (!string.IsNullOrEmpty(rangeSearch) &&
                GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(42f)))
                rangeSearch = "";

            GUILayout.Space(8f);
            rangeFilter = GUILayout.Toolbar(rangeFilter, RangeFilters, EditorStyles.toolbarButton, GUILayout.Width(360f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{CountVisibleRows()} shown", EditorStyles.miniLabel, GUILayout.Width(58f));
        }
    }

    private void DrawWeaponRangeGroup(
        string label,
        Func<WeaponUpgrades.UpgradeType, bool> predicate,
        Color accent)
    {
        if (!ShouldDrawFilter(label))
            return;

        int visibleCount = CountWeaponRows(predicate);
        if (visibleCount == 0)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool expanded = DrawGroupHeader(label, visibleCount, accent);
            SetFoldout("Range:" + label, expanded);
            if (!expanded)
                return;

            DrawRangeHeader();
            float totalWeight = GetTotalWeaponWeight(predicate);
            int rowIndex = 0;
            foreach (WeaponUpgrades.UpgradeType type in Enum.GetValues(typeof(WeaponUpgrades.UpgradeType)))
            {
                if (!WeaponUpgrades.IsGeneratedType(type) || !predicate(type))
                    continue;

                string displayName = ObjectNames.NicifyVariableName(type.ToString());
                if (MatchesSearch(displayName))
                    DrawWeaponRangeRow(type, displayName, totalWeight, accent, rowIndex++);
            }
            EditorGUILayout.Space(2f);
        }
        EditorGUILayout.Space(5f);
    }

    private void DrawAccessoryRangeGroup(Color accent)
    {
        const string label = "Accessory";
        if (!ShouldDrawFilter(label))
            return;

        int visibleCount = CountAccessoryRows();
        if (visibleCount == 0)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool expanded = DrawGroupHeader(label, visibleCount, accent);
            SetFoldout("Range:" + label, expanded);
            if (!expanded)
                return;

            DrawRangeHeader();
            float totalWeight = GetTotalAccessoryWeight();
            int rowIndex = 0;
            foreach (AccessoriesUpgrades.StatUpgradeType type in Enum.GetValues(typeof(AccessoriesUpgrades.StatUpgradeType)))
            {
                if (type == AccessoriesUpgrades.StatUpgradeType.None)
                    continue;

                string displayName = ObjectNames.NicifyVariableName(type.ToString());
                if (MatchesSearch(displayName))
                    DrawAccessoryRangeRow(type, displayName, totalWeight, accent, rowIndex++);
            }
            EditorGUILayout.Space(2f);
        }
        EditorGUILayout.Space(5f);
    }

    private bool DrawGroupHeader(string label, int count, Color accent)
    {
        Rect header = EditorGUILayout.GetControlRect(false, 28f);
        EditorGUI.DrawRect(new Rect(header.x, header.y + 2f, 3f, header.height - 4f), accent);

        bool expanded = GetFoldout("Range:" + label, true);
        expanded = EditorGUI.Foldout(new Rect(header.x + 9f, header.y, 18f, header.height), expanded, GUIContent.none, true);

        var groupStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleLeft
        };
        groupStyle.normal.textColor = accent;
        GUI.Label(new Rect(header.x + 28f, header.y, 180f, header.height), label, groupStyle);

        var countStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight
        };
        GUI.Label(new Rect(header.xMax - 90f, header.y, 84f, header.height), $"{count} upgrades", countStyle);
        return expanded;
    }

    private static void DrawRangeHeader()
    {
        bool compact = EditorGUIUtility.currentViewWidth < CompactLayoutBreakpoint;
        Rect row = EditorGUILayout.GetControlRect(false, compact ? 62f : 20f);
        GetRangeColumns(row, out Rect nameRect, out Rect descriptionRect, out Rect weightRect, out Rect chanceRect, out Rect minRect,
            out Rect maxRect, out Rect integerRect, out Rect resetRect);

        GUI.Label(nameRect, "UPGRADE", EditorStyles.miniBoldLabel);
        GUI.Label(descriptionRect, "DESCRIPTION", EditorStyles.miniBoldLabel);
        GUI.Label(weightRect, "WEIGHT", EditorStyles.miniBoldLabel);
        GUI.Label(chanceRect, new GUIContent(compact ? "CHANCE" : "RESULTING CHANCE", "Share of the category's configured weight before runtime eligibility changes the candidate pool."), EditorStyles.miniBoldLabel);
        GUI.Label(minRect, "MIN", EditorStyles.miniBoldLabel);
        GUI.Label(maxRect, "MAX", EditorStyles.miniBoldLabel);
        GUI.Label(integerRect, compact ? "INT" : "INTEGER", EditorStyles.miniBoldLabel);
        GUI.Label(resetRect, "", EditorStyles.miniBoldLabel);
    }

    private void DrawWeaponRangeRow(
        WeaponUpgrades.UpgradeType type,
        string displayName,
        float totalWeight,
        Color accent,
        int rowIndex)
    {
        var range = settings.FindWeaponRange(type);
        var weight = settings.FindWeaponWeight(type);
        bool percentage = IsPercentageWeapon(type);

        Rect row = EditorGUILayout.GetControlRect(false, GetRangeRowHeight());
        DrawAlternatingRow(row, rowIndex);
        GetRangeColumns(row, out Rect nameRect, out Rect descriptionRect, out Rect weightRect, out Rect chanceRect, out Rect minRect,
            out Rect maxRect, out Rect integerRect, out Rect resetRect);

        GUI.Label(nameRect, new GUIContent(displayName, percentage ? "Min and max are displayed as percentages." : displayName), rowLabelStyle);
        string description = GetWeaponDescription(type, range);
        GUI.Label(descriptionRect, new GUIContent(description, description), rowDescriptionStyle);
        DrawEditableWeight(weight, weightRect);
        float chance = totalWeight > 0f ? settings.GetWeaponWeight(type) / totalWeight : 0f;
        DrawChanceRail(chanceRect, chance, accent);

        if (range == null)
        {
            GUI.Label(new Rect(minRect.x, minRect.y, maxRect.xMax - minRect.x, minRect.height), "Random enum value", EditorStyles.miniLabel);
            DrawWeightResetButton(resetRect, weight);
            return;
        }

        EditorGUI.BeginChangeCheck();
        float nextMin = DrawRangeValue(minRect, range.min, range.wholeNumbers, percentage);
        float nextMax = DrawRangeValue(maxRect, range.max, range.wholeNumbers, percentage);
        bool nextWhole = EditorGUI.Toggle(integerRect, range.wholeNumbers);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(settings, "Change Generated Upgrade Range");
            range.min = nextMin;
            range.max = Mathf.Max(nextMin, nextMax);
            range.wholeNumbers = nextWhole;
            SaveSettingsChange();
        }

        if (GUI.Button(resetRect, "Reset", EditorStyles.miniButton) &&
            GeneratedUpgradeSettings.TryGetDefaultWeaponRange(type, out float min, out float max, out bool whole))
        {
            Undo.RecordObject(settings, "Reset Generated Upgrade Range");
            range.min = min;
            range.max = max;
            range.wholeNumbers = whole;
            if (weight != null)
                weight.weight = GeneratedUpgradeSettings.DefaultUpgradeWeight;
            SaveSettingsChange();
        }
    }

    private void DrawAccessoryRangeRow(
        AccessoriesUpgrades.StatUpgradeType type,
        string displayName,
        float totalWeight,
        Color accent,
        int rowIndex)
    {
        var range = settings.FindAccessoryRange(type);
        var weight = settings.FindAccessoryWeight(type);
        bool percentage = IsPercentageAccessory(type);

        Rect row = EditorGUILayout.GetControlRect(false, GetRangeRowHeight());
        DrawAlternatingRow(row, rowIndex);
        GetRangeColumns(row, out Rect nameRect, out Rect descriptionRect, out Rect weightRect, out Rect chanceRect, out Rect minRect,
            out Rect maxRect, out Rect integerRect, out Rect resetRect);

        GUI.Label(nameRect, new GUIContent(displayName, percentage ? "Min and max are displayed as percentages." : displayName), rowLabelStyle);
        string description = GetAccessoryDescription(type, range);
        GUI.Label(descriptionRect, new GUIContent(description, description), rowDescriptionStyle);
        DrawEditableWeight(weight, weightRect);
        float chance = totalWeight > 0f ? settings.GetAccessoryWeight(type) / totalWeight : 0f;
        DrawChanceRail(chanceRect, chance, accent);

        if (range == null)
        {
            GUI.Label(new Rect(minRect.x, minRect.y, maxRect.xMax - minRect.x, minRect.height), "No configured range", EditorStyles.miniLabel);
            DrawWeightResetButton(resetRect, weight);
            return;
        }

        EditorGUI.BeginChangeCheck();
        float nextMin = DrawRangeValue(minRect, range.min, range.wholeNumbers, percentage);
        float nextMax = DrawRangeValue(maxRect, range.max, range.wholeNumbers, percentage);
        bool nextWhole = EditorGUI.Toggle(integerRect, range.wholeNumbers);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(settings, "Change Generated Upgrade Range");
            range.min = nextMin;
            range.max = Mathf.Max(nextMin, nextMax);
            range.wholeNumbers = nextWhole;
            SaveSettingsChange();
        }

        if (GUI.Button(resetRect, "Reset", EditorStyles.miniButton) &&
            GeneratedUpgradeSettings.TryGetDefaultAccessoryRange(type, out float min, out float max, out bool whole))
        {
            Undo.RecordObject(settings, "Reset Generated Upgrade Range");
            range.min = min;
            range.max = max;
            range.wholeNumbers = whole;
            if (weight != null)
                weight.weight = GeneratedUpgradeSettings.DefaultUpgradeWeight;
            SaveSettingsChange();
        }
    }

    private void DrawEditableWeight(GeneratedUpgradeSettings.WeaponWeight weight, Rect rect)
    {
        if (weight == null)
        {
            GUI.Label(rect, "Missing", EditorStyles.miniLabel);
            return;
        }

        EditorGUI.BeginChangeCheck();
        float next = Mathf.Clamp(
            EditorGUI.FloatField(rect, weight.weight),
            GeneratedUpgradeSettings.MinUpgradeWeight,
            GeneratedUpgradeSettings.MaxUpgradeWeight);
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(settings, "Change Generated Upgrade Weight");
        weight.weight = next;
        SaveSettingsChange();
    }

    private void DrawEditableWeight(GeneratedUpgradeSettings.AccessoryWeight weight, Rect rect)
    {
        if (weight == null)
        {
            GUI.Label(rect, "Missing", EditorStyles.miniLabel);
            return;
        }

        EditorGUI.BeginChangeCheck();
        float next = Mathf.Clamp(
            EditorGUI.FloatField(rect, weight.weight),
            GeneratedUpgradeSettings.MinUpgradeWeight,
            GeneratedUpgradeSettings.MaxUpgradeWeight);
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(settings, "Change Generated Upgrade Weight");
        weight.weight = next;
        SaveSettingsChange();
    }

    private void DrawWeightResetButton(Rect rect, GeneratedUpgradeSettings.WeaponWeight weight)
    {
        if (!GUI.Button(rect, "Reset", EditorStyles.miniButton) || weight == null)
            return;

        Undo.RecordObject(settings, "Reset Generated Upgrade Weight");
        weight.weight = GeneratedUpgradeSettings.DefaultUpgradeWeight;
        SaveSettingsChange();
    }

    private void DrawWeightResetButton(Rect rect, GeneratedUpgradeSettings.AccessoryWeight weight)
    {
        if (!GUI.Button(rect, "Reset", EditorStyles.miniButton) || weight == null)
            return;

        Undo.RecordObject(settings, "Reset Generated Upgrade Weight");
        weight.weight = GeneratedUpgradeSettings.DefaultUpgradeWeight;
        SaveSettingsChange();
    }

    private static float DrawRangeValue(Rect rect, float value, bool wholeNumbers, bool percentage)
    {
        if (wholeNumbers)
            return EditorGUI.IntField(rect, Mathf.RoundToInt(value));

        float scale = percentage ? 100f : 1f;
        return EditorGUI.FloatField(rect, value * scale) / scale;
    }

    private static string GetWeaponDescription(
        WeaponUpgrades.UpgradeType type,
        GeneratedUpgradeSettings.WeaponRange range)
    {
        if (!WeaponUpgradeCatalog.TryGet(type, out WeaponUpgradeDefinition definition))
            return string.Empty;

        if (definition.HasConfigurableRange && range != null)
        {
            definition.BuildText(null, range.min, out _, out string minDescription);
            definition.BuildText(null, range.max, out _, out string maxDescription);
            return MergeDescriptionRange(minDescription, maxDescription);
        }

        string previewValue = definition.ValueFormat switch
        {
            WeaponUpgradeValueFormat.DamageType => "a different type of",
            WeaponUpgradeValueFormat.StatusType => "a random status effect",
            _ => string.Empty
        };
        return StripRuntimeMarkup(string.Format(definition.DescriptionTemplate, previewValue));
    }

    private static string GetAccessoryDescription(
        AccessoriesUpgrades.StatUpgradeType type,
        GeneratedUpgradeSettings.AccessoryRange range)
    {
        if (range == null)
            return string.Empty;

        AccessoriesUpgrades.BuildSelectionText(type, range.min, out _, out string minDescription);
        AccessoriesUpgrades.BuildSelectionText(type, range.max, out _, out string maxDescription);
        return MergeDescriptionRange(minDescription, maxDescription);
    }

    private static string MergeDescriptionRange(string minDescription, string maxDescription)
    {
        string min = StripRuntimeMarkup(minDescription);
        string max = StripRuntimeMarkup(maxDescription);
        if (string.Equals(min, max, StringComparison.Ordinal))
            return min;

        if (TrySplitRuntimeValue(minDescription, out string minPrefix, out string minValue, out string minSuffix) &&
            TrySplitRuntimeValue(maxDescription, out string maxPrefix, out string maxValue, out string maxSuffix) &&
            string.Equals(minPrefix, maxPrefix, StringComparison.Ordinal) &&
            string.Equals(minSuffix, maxSuffix, StringComparison.Ordinal))
        {
            return StripRuntimeMarkup(minPrefix) + minValue + " - " + maxValue +
                   StripRuntimeMarkup(minSuffix);
        }

        return min + " / " + max;
    }

    private static bool TrySplitRuntimeValue(
        string description,
        out string prefix,
        out string value,
        out string suffix)
    {
        const string openingTag = "<color=#8888FF>";
        const string closingTag = "</color>";
        int valueStart = description?.IndexOf(openingTag, StringComparison.Ordinal) ?? -1;
        if (valueStart < 0)
        {
            prefix = value = suffix = string.Empty;
            return false;
        }

        int contentStart = valueStart + openingTag.Length;
        int valueEnd = description.IndexOf(closingTag, contentStart, StringComparison.Ordinal);
        if (valueEnd < 0)
        {
            prefix = value = suffix = string.Empty;
            return false;
        }

        prefix = description.Substring(0, valueStart);
        value = description.Substring(contentStart, valueEnd - contentStart);
        suffix = description.Substring(valueEnd + closingTag.Length);
        return true;
    }

    private static string StripRuntimeMarkup(string value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("<color=#8888FF>", string.Empty).Replace("</color>", string.Empty);

    private static float GetRangeRowHeight() =>
        EditorGUIUtility.currentViewWidth < CompactLayoutBreakpoint ? CompactRowHeight : RowHeight;

    private static void GetRangeColumns(
        Rect row,
        out Rect nameRect,
        out Rect descriptionRect,
        out Rect weightRect,
        out Rect chanceRect,
        out Rect minRect,
        out Rect maxRect,
        out Rect integerRect,
        out Rect resetRect)
    {
        if (row.height > RowHeight)
        {
            const float compactWeightWidth = 64f;
            const float compactValueWidth = 58f;
            const float compactIntegerWidth = 44f;

            float topWidth = Mathf.Max(80f, row.width - ChanceWidth - ColumnGap - 12f);
            float compactX = row.x + 6f;
            nameRect = new Rect(compactX, row.y + 1f, topWidth, 18f);
            chanceRect = new Rect(compactX + topWidth + ColumnGap, row.y, ChanceWidth, 20f);
            descriptionRect = new Rect(compactX, row.y + 22f, row.width - 12f, 18f);

            float controlsWidth = compactWeightWidth + compactValueWidth * 2f +
                                  compactIntegerWidth + ResetWidth + ColumnGap * 4f;
            float controlsX = row.x + Mathf.Max(6f, (row.width - controlsWidth) * 0.5f);
            float controlsY = row.y + 46f;

            weightRect = new Rect(controlsX, controlsY, compactWeightWidth, 18f);
            controlsX += compactWeightWidth + ColumnGap;
            minRect = new Rect(controlsX, controlsY, compactValueWidth, 18f);
            controlsX += compactValueWidth + ColumnGap;
            maxRect = new Rect(controlsX, controlsY, compactValueWidth, 18f);
            controlsX += compactValueWidth + ColumnGap;
            integerRect = new Rect(controlsX + 12f, controlsY, compactIntegerWidth - 12f, 18f);
            controlsX += compactIntegerWidth + ColumnGap;
            resetRect = new Rect(controlsX, controlsY, ResetWidth, 18f);
            return;
        }

        float fixedWidth = UpgradeWidth + WeightWidth + ChanceWidth + ValueWidth * 2f +
                           IntegerWidth + ResetWidth + ColumnGap * 7f;
        float descriptionWidth = Mathf.Max(240f, row.width - fixedWidth - 12f);
        float x = row.x + 6f;

        nameRect = new Rect(x, row.y + 2f, UpgradeWidth, row.height - 4f);
        x += UpgradeWidth + ColumnGap;
        descriptionRect = new Rect(x, row.y + 2f, descriptionWidth, row.height - 4f);
        x += descriptionWidth + ColumnGap;
        weightRect = new Rect(x, row.y + 3f, WeightWidth, row.height - 6f);
        x += WeightWidth + ColumnGap;
        chanceRect = new Rect(x, row.y, ChanceWidth, row.height);
        x += ChanceWidth + ColumnGap;
        minRect = new Rect(x, row.y + 3f, ValueWidth, row.height - 6f);
        x += ValueWidth + ColumnGap;
        maxRect = new Rect(x, row.y + 3f, ValueWidth, row.height - 6f);
        x += ValueWidth + ColumnGap;
        integerRect = new Rect(x + 20f, row.y + 3f, IntegerWidth - 20f, row.height - 6f);
        x += IntegerWidth + ColumnGap;
        resetRect = new Rect(x, row.y + 3f, ResetWidth, row.height - 6f);
    }

    private static void DrawAlternatingRow(Rect row, int rowIndex)
    {
        if ((rowIndex & 1) == 0)
        {
            Color color = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.025f)
                : new Color(0f, 0f, 0f, 0.035f);
            EditorGUI.DrawRect(row, color);
        }
    }

    private float GetTotalWeaponWeight(Func<WeaponUpgrades.UpgradeType, bool> predicate)
    {
        float total = 0f;
        foreach (WeaponUpgrades.UpgradeType type in Enum.GetValues(typeof(WeaponUpgrades.UpgradeType)))
        {
            if (WeaponUpgrades.IsGeneratedType(type) && predicate(type))
                total += settings.GetWeaponWeight(type);
        }
        return total;
    }

    private float GetTotalAccessoryWeight()
    {
        float total = 0f;
        foreach (AccessoriesUpgrades.StatUpgradeType type in Enum.GetValues(typeof(AccessoriesUpgrades.StatUpgradeType)))
        {
            if (type != AccessoriesUpgrades.StatUpgradeType.None)
                total += settings.GetAccessoryWeight(type);
        }
        return total;
    }

    private int CountVisibleRows()
    {
        int count = 0;
        if (ShouldDrawFilter("Knife")) count += CountWeaponRows(IsKnifeType);
        if (ShouldDrawFilter("Shooter")) count += CountWeaponRows(IsShooterType);
        if (ShouldDrawFilter("WeaponTick")) count += CountWeaponRows(IsTickType);
        if (ShouldDrawFilter("Accessory")) count += CountAccessoryRows();
        return count;
    }

    private int CountWeaponRows(Func<WeaponUpgrades.UpgradeType, bool> predicate)
    {
        int count = 0;
        foreach (WeaponUpgrades.UpgradeType type in Enum.GetValues(typeof(WeaponUpgrades.UpgradeType)))
        {
            if (!WeaponUpgrades.IsGeneratedType(type) || !predicate(type))
                continue;
            if (MatchesSearch(ObjectNames.NicifyVariableName(type.ToString())))
                count++;
        }
        return count;
    }

    private int CountAccessoryRows()
    {
        int count = 0;
        foreach (AccessoriesUpgrades.StatUpgradeType type in Enum.GetValues(typeof(AccessoriesUpgrades.StatUpgradeType)))
        {
            if (type == AccessoriesUpgrades.StatUpgradeType.None)
                continue;
            if (MatchesSearch(ObjectNames.NicifyVariableName(type.ToString())))
                count++;
        }
        return count;
    }

    private bool ShouldDrawFilter(string group) =>
        rangeFilter == 0 || RangeFilters[rangeFilter] == group;

    private bool MatchesSearch(string value) =>
        string.IsNullOrWhiteSpace(rangeSearch) ||
        value.IndexOf(rangeSearch.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

    private static Color GetRarityColor(PowerUpRarity rarity)
    {
        if (ColorUtility.TryParseHtmlString(PowerUp.GetRarityColor(rarity), out Color color))
            return color;
        return EditorGUIUtility.isProSkin ? Color.white : Color.black;
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

        int weaponCount = asset.weaponRanges?.Count ?? 0;
        int accessoryCount = asset.accessoryRanges?.Count ?? 0;
        int weaponWeightCount = asset.weaponWeights?.Count ?? 0;
        int accessoryWeightCount = asset.accessoryWeights?.Count ?? 0;
        int rarityCount = asset.raritySettings?.Count ?? 0;
        asset.EnsureAllRanges();
        if (weaponCount != asset.weaponRanges.Count ||
            accessoryCount != asset.accessoryRanges.Count ||
            weaponWeightCount != asset.weaponWeights.Count ||
            accessoryWeightCount != asset.accessoryWeights.Count ||
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
        type == AccessoriesUpgrades.StatUpgradeType.PoisonResist ||
        type == AccessoriesUpgrades.StatUpgradeType.CooldownReduction ||
        type == AccessoriesUpgrades.StatUpgradeType.CriticalChanceFlat ||
        type == AccessoriesUpgrades.StatUpgradeType.CriticalDamageFlat ||
        type == AccessoriesUpgrades.StatUpgradeType.StatusApplicationChanceFlat ||
        type == AccessoriesUpgrades.StatUpgradeType.DashCooldownReduction ||
        type == AccessoriesUpgrades.StatUpgradeType.ContactDamageReduction ||
        type == AccessoriesUpgrades.StatUpgradeType.EnemySlowAura;
}

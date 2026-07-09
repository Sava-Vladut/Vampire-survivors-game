#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AccessorySystemMigration
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string PrefabFolder = "Assets/Prefabs/Accessories";
    private const string DataFolder = "Assets/PowerUps/Accessories";
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Grasslands.unity",
        "Assets/Scenes/Castle.unity",
    };

    private sealed class OfferData
    {
        public string key;
        public string name;
        public string description;
        public Sprite icon;
        public PowerUpRarity rarity;
        public float weight;
    }

    [MenuItem("Tools/Power Ups/Migrate Accessory System")]
    public static void RunFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before migrating accessories.");
        Run();
    }

    public static void RunBatch()
    {
        try
        {
            Run();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    private static void Run()
    {
        if (!HasEmbeddedAccessories())
        {
            Debug.Log("[AccessorySystemMigration] Player prefab is already migrated; no changes were made.");
            return;
        }

        EnsureFolder(PrefabFolder);
        EnsureFolder(DataFolder);

        Dictionary<string, OfferData> presentation = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<string>> sceneAccessoryKeys = new(StringComparer.OrdinalIgnoreCase);
        CollectSceneOffers(presentation, sceneAccessoryKeys);

        AccessoryUpgradeProfile defaultProfile = CreateProfile("DefaultAccessoryUpgradeProfile", includeMovement: false, includeThorns: false);
        AccessoryUpgradeProfile bootsProfile = CreateProfile("BootsAccessoryUpgradeProfile", includeMovement: true, includeThorns: false);
        AccessoryUpgradeProfile armorProfile = CreateProfile("ArmorAccessoryUpgradeProfile", includeMovement: false, includeThorns: true);

        Dictionary<string, PowerUpDefinition> definitions = CreateAccessoryAssets(presentation, defaultProfile, bootsProfile, armorProfile);
        PowerUpCatalog masterCatalog = CreateCatalog("AccessoryCatalog_All", definitions.Values.OrderBy(d => d.DisplayName));

        UpdateScenes(definitions, sceneAccessoryKeys, masterCatalog);
        UpdatePlayerPrefab(defaultProfile, bootsProfile, armorProfile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AccessorySystemMigration] Migrated {definitions.Count} accessories and {ScenePaths.Length} scenes.");
    }

    private static bool HasEmbeddedAccessories()
    {
        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try { return player.GetComponentsInChildren<Accessory>(true).Length > 0; }
        finally { PrefabUtility.UnloadPrefabContents(player); }
    }

    private static void CollectSceneOffers(Dictionary<string, OfferData> presentation, Dictionary<string, List<string>> sceneAccessoryKeys)
    {
        for (int sceneIndex = 0; sceneIndex < ScenePaths.Length; sceneIndex++)
        {
            string scenePath = ScenePaths[sceneIndex];
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var keys = new List<string>();
            sceneAccessoryKeys[scenePath] = keys;

            PowerUpChooser[] choosers = UnityEngine.Object.FindObjectsByType<PowerUpChooser>(FindObjectsInactive.Include);
            for (int chooserIndex = 0; chooserIndex < choosers.Length; chooserIndex++)
            {
                PowerUpChooser chooser = choosers[chooserIndex];
                for (int i = 0; i < chooser.powerUps.Count; i++)
                {
                    PowerUp offer = chooser.powerUps[i];
                    Accessory accessory = offer?.powerUpObject != null ? offer.powerUpObject.GetComponent<Accessory>() : null;
                    if (accessory == null || !offer.IsAccessory || offer.IsUpgrade) continue;

                    string key = accessory.gameObject.name.Trim();
                    if (!keys.Contains(key, StringComparer.OrdinalIgnoreCase)) keys.Add(key);
                    if (presentation.ContainsKey(key)) continue;

                    presentation[key] = new OfferData
                    {
                        key = key,
                        name = string.IsNullOrWhiteSpace(offer.powerUpName) ? accessory.DisplayName : offer.powerUpName.Trim(),
                        description = offer.powerUpDescription ?? string.Empty,
                        icon = offer.powerUpIcon != null ? offer.powerUpIcon : accessory.Icon,
                        rarity = offer.rarity,
                        weight = Mathf.Max(0f, offer.weight),
                    };
                }
            }
            if (!scene.IsValid()) throw new InvalidOperationException($"Failed to inspect {scenePath}.");
        }
    }

    private static Dictionary<string, PowerUpDefinition> CreateAccessoryAssets(
        Dictionary<string, OfferData> presentation,
        AccessoryUpgradeProfile defaultProfile,
        AccessoryUpgradeProfile bootsProfile,
        AccessoryUpgradeProfile armorProfile)
    {
        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Accessory[] accessories = player.GetComponentsInChildren<Accessory>(true);
            var definitions = new Dictionary<string, PowerUpDefinition>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < accessories.Length; i++)
            {
                Accessory source = accessories[i];
                string key = source.gameObject.name.Trim();
                presentation.TryGetValue(key, out OfferData offer);
                offer ??= new OfferData
                {
                    key = key,
                    name = source.DisplayName,
                    description = source.BaseDescription,
                    icon = source.Icon,
                    rarity = PowerUpRarity.Common,
                    weight = 1f,
                };

                GameObject clone = UnityEngine.Object.Instantiate(source.gameObject);
                clone.name = key;
                clone.transform.SetParent(null, false);
                clone.SetActive(false);
                Accessory cloneAccessory = clone.GetComponent<Accessory>();

                AccessoryUpgradeProfile profile = IsBoots(cloneAccessory, key) ? bootsProfile : IsArmor(cloneAccessory, key) ? armorProfile : defaultProfile;
                SetObjectReference(cloneAccessory, "upgradeProfile", profile);
                ConfigureBaseStatEffect(clone, cloneAccessory, key);

                string safeName = SafeFileName(offer.name);
                string prefabPath = $"{PrefabFolder}/{safeName}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
                UnityEngine.Object.DestroyImmediate(clone);
                if (prefab == null) throw new InvalidOperationException($"Failed to create {prefabPath}.");

                string definitionPath = $"{DataFolder}/{safeName}.asset";
                PowerUpDefinition definition = AssetDatabase.LoadAssetAtPath<PowerUpDefinition>(definitionPath);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<PowerUpDefinition>();
                    AssetDatabase.CreateAsset(definition, definitionPath);
                }
                ConfigureDefinition(definition, offer, prefab);
                definitions[key] = definition;
            }
            return definitions;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }
    }

    private static void UpdateScenes(
        Dictionary<string, PowerUpDefinition> definitions,
        Dictionary<string, List<string>> sceneAccessoryKeys,
        PowerUpCatalog fallbackCatalog)
    {
        for (int sceneIndex = 0; sceneIndex < ScenePaths.Length; sceneIndex++)
        {
            string scenePath = ScenePaths[sceneIndex];
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            sceneAccessoryKeys.TryGetValue(scenePath, out List<string> keys);
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            IEnumerable<PowerUpDefinition> entries;
            if (keys != null && keys.Count > 0)
            {
                entries = keys.Where(definitions.ContainsKey).Select(key => definitions[key]);
            }
            else
            {
                // Castle has two legacy elemental offers that are accessory-tagged
                // but are not Accessory components. Keep those in the scene list and
                // seed the catalog with its prior accessory set (all except Coat).
                entries = sceneName.Equals("Castle", StringComparison.OrdinalIgnoreCase)
                    ? definitions.Where(pair => pair.Key.IndexOf("Coat", StringComparison.OrdinalIgnoreCase) < 0).Select(pair => pair.Value)
                    : definitions.Values;
            }
            PowerUpCatalog catalog = CreateCatalog($"AccessoryCatalog_{sceneName}", entries);
            if (catalog == null) catalog = fallbackCatalog;

            PowerUpChooser[] choosers = UnityEngine.Object.FindObjectsByType<PowerUpChooser>(FindObjectsInactive.Include);
            for (int chooserIndex = 0; chooserIndex < choosers.Length; chooserIndex++)
            {
                PowerUpChooser chooser = choosers[chooserIndex];
                chooser.powerUps.RemoveAll(offer =>
                    offer?.powerUpObject != null &&
                    offer.IsAccessory &&
                    !offer.IsUpgrade &&
                    offer.powerUpObject.GetComponent<Accessory>() != null);

                var serialized = new SerializedObject(chooser);
                serialized.FindProperty("initialCatalog").objectReferenceValue = catalog;
                serialized.FindProperty("loadCatalogOnAwake").boolValue = true;
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                serialized.FindProperty("playerRoot").objectReferenceValue = player != null ? player.transform : null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(chooser);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void UpdatePlayerPrefab(
        AccessoryUpgradeProfile defaultProfile,
        AccessoryUpgradeProfile bootsProfile,
        AccessoryUpgradeProfile armorProfile)
    {
        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Accessory[] accessories = player.GetComponentsInChildren<Accessory>(true);
            GameObject entryPrefab = accessories.FirstOrDefault()?.LegacyStatsTextPrefab;
            Transform contentParent = accessories.FirstOrDefault()?.LegacyUiParent;

            AccessoryInventory inventory = player.GetComponent<AccessoryInventory>();
            if (inventory == null) inventory = player.AddComponent<AccessoryInventory>();
            if (player.GetComponent<PlayerDamageModifierRegistry>() == null)
                player.AddComponent<PlayerDamageModifierRegistry>();

            Transform runtime = player.transform.Find("Accessory Runtime");
            if (runtime == null)
            {
                var runtimeObject = new GameObject("Accessory Runtime");
                runtimeObject.transform.SetParent(player.transform, false);
                runtime = runtimeObject.transform;
            }

            AccessoryInventoryPresenter presenter = player.GetComponent<AccessoryInventoryPresenter>();
            if (presenter == null) presenter = player.AddComponent<AccessoryInventoryPresenter>();
            var presenterSerialized = new SerializedObject(presenter);
            presenterSerialized.FindProperty("inventory").objectReferenceValue = inventory;
            presenterSerialized.FindProperty("entryPrefab").objectReferenceValue = entryPrefab;
            presenterSerialized.FindProperty("contentParent").objectReferenceValue = contentParent;
            presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

            for (int i = 0; i < accessories.Length; i++)
                UnityEngine.Object.DestroyImmediate(accessories[i].gameObject);

            PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }
    }

    private static void ConfigureBaseStatEffect(GameObject root, Accessory accessory, string key)
    {
        List<AccessoryStatModifier> modifiers = BuildBaseModifiers(accessory, key);
        AccessoryStatEffect effect = root.GetComponent<AccessoryStatEffect>();
        if (modifiers.Count == 0)
        {
            if (effect != null) UnityEngine.Object.DestroyImmediate(effect);
            return;
        }
        if (effect == null) effect = root.AddComponent<AccessoryStatEffect>();
        var serialized = new SerializedObject(effect);
        SerializedProperty list = serialized.FindProperty("modifiers");
        list.arraySize = modifiers.Count;
        for (int i = 0; i < modifiers.Count; i++)
        {
            SerializedProperty item = list.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("type").enumValueIndex = (int)modifiers[i].type;
            item.FindPropertyRelative("value").floatValue = modifiers[i].value;
        }
        serialized.FindProperty("applied").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<AccessoryStatModifier> BuildBaseModifiers(Accessory accessory, string key)
    {
        string identity = $"{key} {accessory.DisplayName}".ToLowerInvariant();
        var result = new List<AccessoryStatModifier>();
        void Add(AccessoriesUpgrades.StatUpgradeType type, float value) => result.Add(new AccessoryStatModifier { type = type, value = value });

        if (identity.Contains("life ring")) Add(AccessoriesUpgrades.StatUpgradeType.MaxHealthFlat, 50f);
        else if (identity.Contains("boots"))
        {
            Add(AccessoriesUpgrades.StatUpgradeType.MoveSpeedFlat, 0.3f);
            Add(AccessoriesUpgrades.StatUpgradeType.EvasionFlat, 5f);
        }
        else if (identity.Contains("projectile") || identity.Contains("splitter")) Add(AccessoriesUpgrades.StatUpgradeType.ProjectileCountFlat, 1f);
        else if (identity.Contains("armor")) Add(AccessoriesUpgrades.StatUpgradeType.ArmorFlat, 20f);
        else if (identity.Contains("coat")) Add(AccessoriesUpgrades.StatUpgradeType.EvasionFlat, 20f);
        else if (identity.Contains("lighting/poison") || identity.Contains("lightning/poison"))
        {
            Add(AccessoriesUpgrades.StatUpgradeType.LightningResist, 0.3f);
            Add(AccessoriesUpgrades.StatUpgradeType.PoisonResist, 0.3f);
        }
        else if (identity.Contains("cold/fire"))
        {
            Add(AccessoriesUpgrades.StatUpgradeType.ColdResist, 0.3f);
            Add(AccessoriesUpgrades.StatUpgradeType.FireResist, 0.3f);
        }
        return result;
    }

    private static AccessoryUpgradeProfile CreateProfile(string fileName, bool includeMovement, bool includeThorns)
    {
        string path = $"{DataFolder}/{fileName}.asset";
        AccessoryUpgradeProfile profile = AssetDatabase.LoadAssetAtPath<AccessoryUpgradeProfile>(path);
        if (profile == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
            profile = ScriptableObject.CreateInstance<AccessoryUpgradeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        var allowed = Enum.GetValues(typeof(AccessoriesUpgrades.StatUpgradeType))
            .Cast<AccessoriesUpgrades.StatUpgradeType>()
            .Where(type => type != AccessoriesUpgrades.StatUpgradeType.None && type != AccessoriesUpgrades.StatUpgradeType.ProjectileCountFlat)
            .Where(type => includeMovement || (type != AccessoriesUpgrades.StatUpgradeType.MoveSpeedFlat && type != AccessoriesUpgrades.StatUpgradeType.DashDistanceFlat))
            .Where(type => includeThorns || type != AccessoriesUpgrades.StatUpgradeType.ThornsFlat)
            .ToArray();

        var serialized = new SerializedObject(profile);
        serialized.FindProperty("maxUpgrades").intValue = AccessoriesUpgrades.MaxUpgrades;
        SerializedProperty types = serialized.FindProperty("allowedTypes");
        types.arraySize = allowed.Length;
        for (int i = 0; i < allowed.Length; i++) types.GetArrayElementAtIndex(i).enumValueIndex = (int)allowed[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void ConfigureDefinition(PowerUpDefinition definition, OfferData offer, GameObject prefab)
    {
        var serialized = new SerializedObject(definition);
        SerializedProperty stableId = serialized.FindProperty("stableId");
        if (string.IsNullOrWhiteSpace(stableId.stringValue)) stableId.stringValue = Guid.NewGuid().ToString("N");
        serialized.FindProperty("displayName").stringValue = offer.name;
        serialized.FindProperty("description").stringValue = offer.description;
        serialized.FindProperty("icon").objectReferenceValue = offer.icon;
        serialized.FindProperty("tags").intValue = (int)PowerUpTags.Accessory;
        serialized.FindProperty("rarity").enumValueIndex = (int)offer.rarity;
        serialized.FindProperty("selectionWeight").floatValue = offer.weight <= 0f ? 1f : offer.weight;
        serialized.FindProperty("activationObject").objectReferenceValue = prefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private static PowerUpCatalog CreateCatalog(string fileName, IEnumerable<PowerUpDefinition> entries)
    {
        string path = $"{DataFolder}/{fileName}.asset";
        PowerUpCatalog catalog = AssetDatabase.LoadAssetAtPath<PowerUpCatalog>(path);
        if (catalog == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
            catalog = ScriptableObject.CreateInstance<PowerUpCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
        }
        PowerUpDefinition[] distinct = entries.Where(entry => entry != null).Distinct().ToArray();
        var serialized = new SerializedObject(catalog);
        SerializedProperty array = serialized.FindProperty("entries");
        array.arraySize = distinct.Length;
        for (int i = 0; i < distinct.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = distinct[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static bool IsBoots(Accessory accessory, string key) =>
        $"{key} {accessory.DisplayName}".IndexOf("boots", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool IsArmor(Accessory accessory, string key) =>
        $"{key} {accessory.DisplayName}".IndexOf("armor", StringComparison.OrdinalIgnoreCase) >= 0;

    private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string SafeFileName(string value)
    {
        string result = string.IsNullOrWhiteSpace(value) ? "Accessory" : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars()) result = result.Replace(invalid, '_');
        return result;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif

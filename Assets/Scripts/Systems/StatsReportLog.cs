using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatsReportLog : MonoBehaviour
{
    private const string ReportCanvasName = "Stats Report Canvas";
    private const string ReportTextName = "Stats Report Text";

    private static readonly SimpleHealth.DamageType[] DamageTypes =
    {
        SimpleHealth.DamageType.Physical,
        SimpleHealth.DamageType.Fire,
        SimpleHealth.DamageType.Cold,
        SimpleHealth.DamageType.Lightning,
        SimpleHealth.DamageType.Poison
    };

    [Header("Target")]
    [SerializeField] private SimpleHealth targetHealth;
    [SerializeField] private bool playerOnly = true;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI reportText;

    private readonly int[] damageReceivedByType = new int[DamageTypes.Length];
    private readonly int[] damageDealtByType = new int[DamageTypes.Length];
    private readonly Dictionary<string, WeaponDamageTotals> weaponDamage = new Dictionary<string, WeaponDamageTotals>();
    private readonly List<KeyValuePair<string, WeaponDamageTotals>> sortedWeaponDamage = new List<KeyValuePair<string, WeaponDamageTotals>>();
    private readonly StringBuilder builder = new StringBuilder(512);
    private int enemiesKilled;
    private int damageReceivedTotal;
    private int damageDealtTotal;
    private int damageMitigatedByArmour;
    private int damageDodgedByEvasion;
    private bool isBound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateFallbackCanvas()
    {
        if (FindAnyObjectByType<StatsReportLog>() != null)
            return;

        GameObject canvasObject = CreateReportCanvas();
        StatsReportLog report = canvasObject.AddComponent<StatsReportLog>();
        if (report.reportText == null)
            report.reportText = CreateReportText(canvasObject.transform);
    }

    private void Awake()
    {
        if (targetHealth == null)
            TryFindPlayerHealth();

        EnsureReportTextOnReportCanvas();

        if (reportText != null)
            reportText.text = string.Empty;
    }

    private void OnEnable()
    {
        SimpleHealth.AnyDamageTaken += OnAnyDamageTaken;
        SimpleHealth.AnyDied += OnAnyDied;
        BindToHealth();
    }

    private void Start()
    {
        if (!isBound)
            BindToHealth();
    }

    private void OnDisable()
    {
        SimpleHealth.AnyDamageTaken -= OnAnyDamageTaken;
        SimpleHealth.AnyDied -= OnAnyDied;
        UnbindFromHealth();
    }

    private void Update()
    {
        if (targetHealth == null)
        {
            TryFindPlayerHealth();
            BindToHealth();
        }
    }

    private void BindToHealth()
    {
        if (isBound || targetHealth == null)
            return;

        targetHealth.Died += OnTargetDied;
        targetHealth.HealthReset += OnHealthReset;
        isBound = true;
    }

    private void UnbindFromHealth()
    {
        if (!isBound || targetHealth == null)
            return;

        targetHealth.Died -= OnTargetDied;
        targetHealth.HealthReset -= OnHealthReset;
        isBound = false;
    }

    private void TryFindPlayerHealth()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            targetHealth = player.GetComponentInParent<SimpleHealth>();
    }

    private void OnAnyDamageTaken(SimpleHealth.DamageReportEntry entry)
    {
        if (entry.Target == null)
            return;

        int typeIndex = GetTypeIndex(entry.Type);
        if (typeIndex < 0)
            return;

        if (IsTargetPlayer(entry.Target))
        {
            damageMitigatedByArmour += entry.ArmorMitigatedAmount;
            damageDodgedByEvasion += entry.EvasionDodgedAmount;

            if (entry.Amount <= 0)
                return;

            damageReceivedTotal += entry.Amount;
            damageReceivedByType[typeIndex] += entry.Amount;
            return;
        }

        if (entry.Amount <= 0)
            return;

        if (IsPlayerOwnedSource(entry.SourceObject))
        {
            damageDealtTotal += entry.Amount;
            damageDealtByType[typeIndex] += entry.Amount;

            string weaponName = ResolveWeaponName(entry);
            if (!weaponDamage.TryGetValue(weaponName, out WeaponDamageTotals totals))
            {
                totals = new WeaponDamageTotals();
                weaponDamage[weaponName] = totals;
            }

            totals.Total += entry.Amount;
            totals.ByType[typeIndex] += entry.Amount;
        }
    }

    private void OnAnyDied(SimpleHealth health)
    {
        if (health == null || IsTargetPlayer(health))
            return;

        if (health.GetComponent<EnemyChaser>() != null)
            enemiesKilled++;
    }

    private void OnTargetDied(SimpleHealth health)
    {
        if (playerOnly && !IsTargetPlayer(health))
            return;

        EnsureReportTextOnReportCanvas();

        if (reportText == null)
            return;

        reportText.text = BuildReport();
    }

    private void OnHealthReset(SimpleHealth health)
    {
        ClearReport();
    }

    private bool IsTargetPlayer(SimpleHealth health)
    {
        if (health == null)
            return false;

        if (targetHealth != null)
            return health == targetHealth;

        return health.CompareTag("Player");
    }

    private bool IsPlayerOwnedSource(GameObject sourceObject)
    {
        if (sourceObject == null)
            return false;

        SimpleHealth sourceHealth = sourceObject.GetComponentInParent<SimpleHealth>();
        if (sourceHealth != null && IsTargetPlayer(sourceHealth))
            return true;

        Transform cursor = sourceObject.transform;
        while (cursor != null)
        {
            if (cursor.CompareTag("Player"))
                return true;

            cursor = cursor.parent;
        }

        return false;
    }

    private string BuildReport()
    {
        builder.Clear();
        builder.AppendLine("<b>Run Stats</b>");
        builder.AppendLine($"Enemies Killed: <color=#FFD166>{enemiesKilled}</color>");
        builder.AppendLine($"Damage Received: <color=#FF6666>{damageReceivedTotal}</color>");
        builder.AppendLine($"Damage Mitigated by Armour: <color=#66D9EF>{damageMitigatedByArmour}</color>");
        builder.AppendLine($"Damage Dodged by Evasion: <color=#66D9EF>{damageDodgedByEvasion}</color>");
        builder.AppendLine($"Damage Dealt: <color=#80FF80>{damageDealtTotal}</color>");

        builder.AppendLine();
        builder.AppendLine("<b>Damage Received</b>");
        AppendTypeBreakdown(damageReceivedByType, "#FF9999");

        builder.AppendLine();
        builder.AppendLine("<b>Damage Dealt</b>");
        AppendTypeBreakdown(damageDealtByType, "#99FF99");

        builder.AppendLine();
        builder.AppendLine("<b>Damage By Weapon</b>");
        AppendWeaponBreakdown();

        return builder.ToString();
    }

    private void AppendTypeBreakdown(int[] values, string valueColor)
    {
        for (int i = 0; i < DamageTypes.Length; i++)
            builder.AppendLine($"{ColorType(DamageTypes[i])}: <color={valueColor}>{values[i]}</color>");
    }

    private void AppendWeaponBreakdown()
    {
        if (weaponDamage.Count == 0)
        {
            builder.AppendLine("No weapon damage recorded.");
            return;
        }

        sortedWeaponDamage.Clear();
        foreach (KeyValuePair<string, WeaponDamageTotals> pair in weaponDamage)
            sortedWeaponDamage.Add(pair);

        sortedWeaponDamage.Sort((a, b) =>
        {
            int totalCompare = b.Value.Total.CompareTo(a.Value.Total);
            return totalCompare != 0
                ? totalCompare
                : string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
        });

        foreach (KeyValuePair<string, WeaponDamageTotals> pair in sortedWeaponDamage)
            AppendWeaponLine(pair.Key, pair.Value);
    }

    private void AppendWeaponLine(string weaponName, WeaponDamageTotals totals)
    {
        builder.Append("<b>");
        builder.Append(EscapeRichText(weaponName));
        builder.Append("</b>: <color=#80FF80>");
        builder.Append(totals.Total);
        builder.Append("</color>");

        bool hasBreakdown = false;
        for (int i = 0; i < DamageTypes.Length; i++)
        {
            int amount = totals.ByType[i];
            if (amount <= 0)
                continue;

            if (!hasBreakdown)
            {
                builder.Append("  <size=80%>");
                hasBreakdown = true;
            }
            else
            {
                builder.Append("  ");
            }

            builder.Append(ColorShortType(DamageTypes[i], amount));
        }

        if (hasBreakdown)
            builder.Append("</size>");

        builder.AppendLine();
    }

    private void ClearReport()
    {
        for (int i = 0; i < DamageTypes.Length; i++)
        {
            damageReceivedByType[i] = 0;
            damageDealtByType[i] = 0;
        }

        enemiesKilled = 0;
        damageReceivedTotal = 0;
        damageDealtTotal = 0;
        damageMitigatedByArmour = 0;
        damageDodgedByEvasion = 0;
        weaponDamage.Clear();

        if (reportText != null)
            reportText.text = string.Empty;
    }

    private void EnsureReportTextOnReportCanvas()
    {
        GameObject canvasObject = GetOrCreateReportCanvas();
        if (canvasObject == null)
            return;

        if (reportText == null)
        {
            reportText = CreateReportText(canvasObject.transform);
            return;
        }

        Canvas currentCanvas = reportText.GetComponentInParent<Canvas>(true);
        if (currentCanvas != null && currentCanvas.gameObject == canvasObject)
            return;

        reportText.transform.SetParent(canvasObject.transform, false);
        ConfigureReportText(reportText);
        PlaceReportText(reportText.rectTransform);
    }

    private static GameObject GetOrCreateReportCanvas()
    {
        GameObject existing = GameObject.Find(ReportCanvasName);
        return existing != null ? existing : CreateReportCanvas();
    }

    private static GameObject CreateReportCanvas()
    {
        GameObject canvasObject = new GameObject(ReportCanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        return canvasObject;
    }

    private static TextMeshProUGUI CreateReportText(Transform parent)
    {
        GameObject textObject = new GameObject(ReportTextName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        ConfigureReportText(text);
        PlaceReportText(text.rectTransform);
        return text;
    }

    private static void ConfigureReportText(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.alignment = TextAlignmentOptions.TopRight;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.fontSize = 26f;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private static void PlaceReportText(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.55f, 0f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(0f, 32f);
        rect.offsetMax = new Vector2(-36f, -32f);
    }

    private static int GetTypeIndex(SimpleHealth.DamageType type)
    {
        for (int i = 0; i < DamageTypes.Length; i++)
        {
            if (DamageTypes[i] == type)
                return i;
        }

        return -1;
    }

    private static string ResolveWeaponName(SimpleHealth.DamageReportEntry entry)
    {
        GameObject sourceObject = entry.SourceObject;
        if (sourceObject == null)
            return string.IsNullOrWhiteSpace(entry.SourceDetail) ? "Unknown" : entry.SourceDetail;

        if (sourceObject.CompareTag("Player"))
            return string.IsNullOrWhiteSpace(entry.SourceDetail) ? "Player" : entry.SourceDetail;

        if (sourceObject.TryGetComponent(out SimpleShooter shooter))
            return CleanObjectName(shooter.transform.name);

        if (sourceObject.TryGetComponent(out Knife knife))
            return CleanObjectName(knife.transform.name);

        SimpleShooter parentShooter = sourceObject.GetComponentInParent<SimpleShooter>();
        if (parentShooter != null)
            return CleanObjectName(parentShooter.transform.name);

        Knife parentKnife = sourceObject.GetComponentInParent<Knife>();
        if (parentKnife != null)
            return CleanObjectName(parentKnife.transform.name);

        return CleanObjectName(sourceObject.name);
    }

    private static string ColorType(SimpleHealth.DamageType type)
    {
        string hex = type switch
        {
            SimpleHealth.DamageType.Fire => "#FF6600",
            SimpleHealth.DamageType.Cold => "#4DB2FF",
            SimpleHealth.DamageType.Lightning => "#FFFF4D",
            SimpleHealth.DamageType.Poison => "#80FF80",
            _ => "#D9D9D9"
        };

        return $"<color={hex}>{type}</color>";
    }

    private static string ShortType(SimpleHealth.DamageType type)
    {
        return type switch
        {
            SimpleHealth.DamageType.Physical => "P",
            SimpleHealth.DamageType.Fire => "F",
            SimpleHealth.DamageType.Cold => "C",
            SimpleHealth.DamageType.Lightning => "L",
            SimpleHealth.DamageType.Poison => "Po",
            _ => "?"
        };
    }

    private static string ColorShortType(SimpleHealth.DamageType type, int amount)
    {
        string hex = type switch
        {
            SimpleHealth.DamageType.Fire => "#FF6600",
            SimpleHealth.DamageType.Cold => "#4DB2FF",
            SimpleHealth.DamageType.Lightning => "#FFFF4D",
            SimpleHealth.DamageType.Poison => "#80FF80",
            _ => "#D9D9D9"
        };

        return $"<color={hex}>{ShortType(type)} {amount}</color>";
    }

    private static string CleanObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unknown";

        return value.Replace("(Clone)", string.Empty).Trim();
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private sealed class WeaponDamageTotals
    {
        public int Total;
        public readonly int[] ByType = new int[DamageTypes.Length];
    }
}

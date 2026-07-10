using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DamageReportLog : MonoBehaviour
{
    private const string ReportCanvasName = "Damage Report Canvas";
    private const string ReportTextName = "Damage Report Text";

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
    [SerializeField, Min(1)] private int maxRecentHits = 12;

    private readonly int[] totalByType = new int[DamageTypes.Length];
    private readonly Dictionary<string, SourceTotals> sourceTotals = new Dictionary<string, SourceTotals>(StringComparer.OrdinalIgnoreCase);
    private readonly List<KeyValuePair<string, SourceTotals>> sortedSourceTotals = new List<KeyValuePair<string, SourceTotals>>();
    private readonly List<HitLine> recentHits = new List<HitLine>();
    private readonly StringBuilder builder = new StringBuilder(1024);
    private bool isBound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateFallbackCanvas()
    {
        if (FindAnyObjectByType<DamageReportLog>() != null)
            return;

        GameObject canvasObject = CreateReportCanvas();
        DamageReportLog report = canvasObject.AddComponent<DamageReportLog>();
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
        BindToHealth();
    }

    private void Start()
    {
        if (!isBound)
            BindToHealth();
    }

    private void OnDisable()
    {
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

    public void BindToHealth()
    {
        if (isBound || targetHealth == null)
            return;

        targetHealth.DamageTaken += OnDamageTaken;
        targetHealth.Died += OnTargetDied;
        targetHealth.HealthReset += OnHealthReset;
        isBound = true;
    }

    private void UnbindFromHealth()
    {
        if (!isBound || targetHealth == null)
            return;

        targetHealth.DamageTaken -= OnDamageTaken;
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

    private void OnDamageTaken(SimpleHealth.DamageReportEntry entry)
    {
        if (entry.Target == null || entry.Amount <= 0)
            return;

        if (playerOnly && !entry.Target.CompareTag("Player"))
            return;

        int typeIndex = GetTypeIndex(entry.Type);
        if (typeIndex < 0)
            return;

        totalByType[typeIndex] += entry.Amount;

        string sourceName = string.IsNullOrWhiteSpace(entry.SourceName) ? "Unknown" : entry.SourceName.Trim();
        string chatterName = GetChatterGroupName(sourceName);
        if (!sourceTotals.TryGetValue(chatterName, out SourceTotals totals))
        {
            totals = new SourceTotals();
            sourceTotals[chatterName] = totals;
        }

        totals.Total += entry.Amount;
        totals.ByType[typeIndex] += entry.Amount;

        recentHits.Add(new HitLine(sourceName, entry.SourceDetail, entry.Type, entry.Amount, entry.HealthAfter));
        while (recentHits.Count > maxRecentHits)
            recentHits.RemoveAt(0);
    }

    private void OnTargetDied(SimpleHealth health)
    {
        if (playerOnly && (health == null || !health.CompareTag("Player")))
            return;

        EnsureReportTextOnReportCanvas();

        if (reportText == null)
            return;

        reportText.text = BuildReport();
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
        StretchReportText(reportText.rectTransform);
    }

    private void OnHealthReset(SimpleHealth health)
    {
        ClearReport();
    }

    private string BuildReport()
    {
        builder.Clear();
        builder.AppendLine("<b>Damage Report</b>");
        builder.AppendLine($"Total Received: <color=#FF6666>{GetTotalDamage()}</color>");
        builder.AppendLine();

        builder.AppendLine("<b>By Type</b>");
        for (int i = 0; i < DamageTypes.Length; i++)
            builder.AppendLine($"{ColorType(DamageTypes[i])}: <color=#FFFFFF>{totalByType[i]}</color>");

        builder.AppendLine();
        builder.AppendLine("<b>By Chatter</b>");
        if (sourceTotals.Count == 0)
        {
            builder.AppendLine("No damage recorded.");
        }
        else
        {
            sortedSourceTotals.Clear();
            foreach (KeyValuePair<string, SourceTotals> pair in sourceTotals)
                sortedSourceTotals.Add(pair);

            sortedSourceTotals.Sort((a, b) =>
            {
                int totalCompare = b.Value.Total.CompareTo(a.Value.Total);
                return totalCompare != 0
                    ? totalCompare
                    : string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
            });

            foreach (KeyValuePair<string, SourceTotals> pair in sortedSourceTotals)
                AppendChatterSummary(pair.Key, pair.Value);
        }

        if (recentHits.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("<b>Recent Hits</b>");
            int start = Mathf.Max(0, recentHits.Count - maxRecentHits);
            for (int i = start; i < recentHits.Count; i++)
            {
                HitLine hit = recentHits[i];
                builder.AppendLine($"{EscapeRichText(hit.SourceName)} - {hit.Amount} {ColorType(hit.Type)} ({EscapeRichText(hit.Detail)}, HP {Mathf.CeilToInt(hit.HealthAfter)})");
            }
        }

        return builder.ToString();
    }

    private void AppendChatterSummary(string sourceName, SourceTotals totals)
    {
        builder.Append("<b>");
        builder.Append(EscapeRichText(sourceName));
        builder.Append("</b>  <color=#FF6666>");
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
        for (int i = 0; i < totalByType.Length; i++)
            totalByType[i] = 0;

        sourceTotals.Clear();
        recentHits.Clear();

        if (reportText != null)
            reportText.text = string.Empty;
    }

    private int GetTotalDamage()
    {
        int total = 0;
        for (int i = 0; i < totalByType.Length; i++)
            total += totalByType[i];
        return total;
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
        StretchReportText(text.rectTransform);
        return text;
    }

    private static void ConfigureReportText(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.fontSize = 26f;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private static void StretchReportText(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(36f, 32f);
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

    private static string GetChatterGroupName(string sourceName)
    {
        int closingParenthesis = sourceName.Length - 1;
        if (closingParenthesis < 3 || sourceName[closingParenthesis] != ')')
            return sourceName;

        int openingParenthesis = sourceName.LastIndexOf(" (", StringComparison.Ordinal);
        if (openingParenthesis <= 0)
            return sourceName;

        string suffix = sourceName.Substring(openingParenthesis + 2, closingParenthesis - openingParenthesis - 2);
        bool isSpawnOrdinal = int.TryParse(suffix, out int ordinal) && ordinal > 0;
        bool isBossLabel = string.Equals(suffix, "Boss", StringComparison.OrdinalIgnoreCase);
        return isSpawnOrdinal || isBossLabel
            ? sourceName.Substring(0, openingParenthesis)
            : sourceName;
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

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private sealed class SourceTotals
    {
        public int Total;
        public readonly int[] ByType = new int[DamageTypes.Length];
    }

    private readonly struct HitLine
    {
        public readonly string SourceName;
        public readonly string Detail;
        public readonly SimpleHealth.DamageType Type;
        public readonly int Amount;
        public readonly float HealthAfter;

        public HitLine(string sourceName, string detail, SimpleHealth.DamageType type, int amount, float healthAfter)
        {
            SourceName = sourceName;
            Detail = string.IsNullOrWhiteSpace(detail) ? type.ToString() : detail;
            Type = type;
            Amount = amount;
            HealthAfter = healthAfter;
        }
    }
}

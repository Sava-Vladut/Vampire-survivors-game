using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class TwitchSpawnMiniDisplay : MonoBehaviour
{
    [System.Serializable]
    public class ChatCommandEntry
    {
        [Tooltip("The exact text chatters can type.")]
        public string command = "";

        [Tooltip("Short text shown after the command. Leave empty to show only the command.")]
        public string description = "";

        [Tooltip("If false, this command stays configured but hidden from the mini display.")]
        public bool visible = true;
    }

    [Header("Sources")]
    [Tooltip("Drag your TwitchListener here.")]
    public TwitchListener listener;

    [Tooltip("TMP text target where the info will be rendered.")]
    public TextMeshProUGUI targetText;

    [Header("Update")]
    [Min(0.05f)] public float refreshInterval = 0.25f;

    [Header("Spawn Queue")]
    [SerializeField] private bool showSpawnQueue = true;
    [SerializeField, Min(1)] private int maxQueuedNames = 3;

    [Header("Style (hex codes without #)")]
    public string headingHex = "FFD166"; // yellow
    public string valueHex = "00AEEF"; // cyan
    public string noteHex = "9E9E9E"; // gray

    [Header("Chat Commands")]
    [SerializeField] private bool showChatCommands = true;
    [SerializeField] private ChatCommandEntry[] chatCommands =
    {
        new ChatCommandEntry
        {
            command = "LOLW",
            description = "trigger your chatter action",
            visible = true
        }
    };

    private float nextRefresh;
    private readonly List<string> queuedNameBuffer = new(4);

    private void Reset()
    {
        if (!targetText) targetText = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void Update()
    {
        if (!listener || !targetText) return;
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;

        targetText.richText = true;
        targetText.text = BuildText();
    }

    private string BuildText()
    {
        var sb = new StringBuilder(256);

        string h = ColorTag(headingHex);
        string v = ColorTag(valueHex);
        string n = ColorTag(noteHex);

        int cur = Mathf.Max(0, listener.spawnedChatters.Count);
        float interval = Mathf.Max(0f, listener.spawnIncreaseInterval);
        int cap = Mathf.Max(0, listener.minPower * Mathf.Max(1, listener.maxSpawnPerPowerRatio));
        int totalEnemyPower = GetTotalEnemyPower();

        // --- Current spawns vs global cap ---
        sb.AppendLine($"{h}<b>Spawns:</b></color> {v}{cur}</color> / {v}{cap}</color>");
        AppendSpawnQueue(sb, h, v, n);

        sb.AppendLine();

        // --- Min power and upgrade chance ---
        float chance = Mathf.Clamp01(listener.chanceToUpgradeMinPower);
        sb.AppendLine($"{h}<b>Min Power:</b></color> {v}{listener.minPower}</color>");
        sb.AppendLine($"{h}<b>Enemy Power:</b></color> {v}{totalEnemyPower}</color>");
        if (interval > 0f)
            sb.AppendLine($"{n}Chance to +1 every {interval:0}s:</color> {v}{chance * 100f:0}%</color>");
        else
            sb.AppendLine($"{n}Power growth:</color> {v}disabled</color>");

        AppendChatCommands(sb, h, v, n);

        return sb.ToString();
    }

    private void AppendSpawnQueue(StringBuilder sb, string h, string v, string n)
    {
        int queuedCount = listener.QueuedChatterCount;
        if (!showSpawnQueue || queuedCount <= 0)
            return;

        int shown = listener.CopyQueuedChatterLabels(queuedNameBuffer, Mathf.Max(1, maxQueuedNames));
        sb.Append($"{h}<b>Queue:</b></color> {v}{queuedCount}</color>");

        if (shown > 0)
        {
            sb.Append($" {n}");
            for (int i = 0; i < shown; i++)
            {
                if (i > 0) sb.Append(" • ");
                sb.Append(EscapeRichText(queuedNameBuffer[i]));
            }

            if (queuedCount > shown)
                sb.Append($" +{queuedCount - shown}");
            sb.Append("</color>");
        }

        sb.AppendLine();
    }

    private void AppendChatCommands(StringBuilder sb, string h, string v, string n)
    {
        if (!showChatCommands)
            return;

        sb.AppendLine();
        sb.AppendLine($"{h}<b>Chat Commands:</b></color>");

        bool anyVisible = false;
        if (chatCommands != null)
        {
            for (int i = 0; i < chatCommands.Length; i++)
            {
                ChatCommandEntry entry = chatCommands[i];
                if (entry == null || !entry.visible || string.IsNullOrWhiteSpace(entry.command))
                    continue;

                anyVisible = true;
                string command = entry.command.Trim();
                string description = entry.description?.Trim();

                if (string.IsNullOrEmpty(description))
                    sb.AppendLine($"{v}{command}</color>");
                else
                    sb.AppendLine($"{v}{command}</color> {n}- {description}</color>");
            }
        }

        if (!anyVisible)
            sb.AppendLine($"{n}No commands configured</color>");
    }

    private int GetTotalEnemyPower()
    {
        int total = 0;

        for (int i = 0; i < listener.spawnedChatters.Count; i++)
        {
            GameObject enemy = listener.spawnedChatters[i];
            if (!enemy) continue;

            ChatterStats stats = enemy.GetComponent<ChatterStats>();
            if (stats != null)
                total += Mathf.Max(0, stats.power);
        }

        return total;
    }

    private static string ColorTag(string hexNoHash) => $"<color=#{hexNoHash}>";

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}

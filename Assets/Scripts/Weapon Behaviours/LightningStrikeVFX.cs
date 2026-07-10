using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightningStrikeVFX : MonoBehaviour
{
    [Header("Bolt")]
    [SerializeField, Min(0.01f)] private float duration = 0.28f;
    [SerializeField, Min(0.1f)] private float strikeHeight = 5.5f;
    [SerializeField, Range(4, 20)] private int segments = 10;
    [SerializeField, Min(0f)] private float jaggedness = 0.38f;
    [SerializeField, Min(0.01f)] private float coreWidth = 0.075f;
    [SerializeField, Min(0.01f)] private float glowWidth = 0.24f;
    [SerializeField] private Color coreColor = new Color(1f, 1f, 0.82f, 1f);
    [SerializeField] private Color glowColor = new Color(0.2f, 0.78f, 1f, 0.7f);

    [Header("Impact")]
    [SerializeField, Min(0f)] private float impactRadius = 0.72f;
    [SerializeField, Range(8, 48)] private int impactSegments = 28;
    [SerializeField] private Color impactColor = new Color(0.35f, 0.9f, 1f, 0.9f);
    [SerializeField] private int sortingOrder = 20;

    private static Material sharedMaterial;
    private LineRenderer glow;
    private LineRenderer core;
    private LineRenderer impact;
    private Vector3[] boltPoints;

    private void Awake()
    {
        EnsureMaterial();
        glow = CreateLine("Lightning Glow", glowWidth, glowColor, false, sortingOrder);
        core = CreateLine("Lightning Core", coreWidth, coreColor, false, sortingOrder + 1);
        impact = CreateLine("Lightning Impact", 0.055f, impactColor, true, sortingOrder + 1);

        BuildImpactRing();
        RebuildBolt();
        StartCoroutine(AnimateStrike());
    }

    private static void EnsureMaterial()
    {
        if (sharedMaterial != null)
            return;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader != null)
        {
            sharedMaterial = new Material(shader)
            {
                name = "Runtime Lightning Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }

    private LineRenderer CreateLine(string objectName, float width, Color color, bool loop, int order)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.widthMultiplier = width;
        line.startColor = color;
        line.endColor = color;
        line.sortingOrder = order;
        if (sharedMaterial != null)
            line.sharedMaterial = sharedMaterial;
        return line;
    }

    private void RebuildBolt()
    {
        int pointCount = Mathf.Max(4, segments) + 1;
        if (boltPoints == null || boltPoints.Length != pointCount)
            boltPoints = new Vector3[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            float edgeFade = Mathf.Sin(t * Mathf.PI);
            float x = Random.Range(-jaggedness, jaggedness) * edgeFade;
            boltPoints[i] = new Vector3(x, Mathf.Lerp(strikeHeight, 0f, t), 0f);
        }

        glow.positionCount = pointCount;
        core.positionCount = pointCount;
        glow.SetPositions(boltPoints);
        core.SetPositions(boltPoints);
    }

    private void BuildImpactRing()
    {
        int count = Mathf.Max(8, impactSegments);
        impact.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            float unevenRadius = impactRadius * Random.Range(0.82f, 1.08f);
            impact.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * unevenRadius);
        }
    }

    private IEnumerator AnimateStrike()
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        float nextJitter = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / safeDuration);

            if (elapsed >= nextJitter && normalized < 0.55f)
            {
                RebuildBolt();
                nextJitter = elapsed + 0.035f;
            }

            float alpha = 1f - normalized;
            SetAlpha(glow, glowColor, alpha * alpha);
            SetAlpha(core, coreColor, alpha);
            SetAlpha(impact, impactColor, alpha);
            impact.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.35f, normalized);
            yield return null;
        }

        Destroy(gameObject);
    }

    private static void SetAlpha(LineRenderer line, Color baseColor, float alpha)
    {
        Color color = baseColor;
        color.a *= Mathf.Clamp01(alpha);
        line.startColor = color;
        line.endColor = color;
    }
}

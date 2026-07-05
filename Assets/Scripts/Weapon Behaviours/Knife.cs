using TMPro;
using UnityEngine;
using UnityEngine.UI; // For Image
using System.Collections.Generic;

// Targeting preference options for selecting enemies.
public enum TargetingMode
{
    Closest,
    Furthest,
    MoreHP,
    LessHP,
    Random
}

public class Knife : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField, Tooltip("How to prioritize targets within the radius.")]
    public TargetingMode targetingMode = TargetingMode.Closest;
    [Header("AOE Damage")]
    [SerializeField, Tooltip("Main hit radius for selecting enemies.")]
    public float radius = 1f;
    [SerializeField, Tooltip("Base damage dealt to main target.")]
    public int damage = 10;
    [SerializeField, Tooltip("Damage category used for resistances, weaknesses, and damage coloring.")]
    public SimpleHealth.DamageType damageType;
    [SerializeField, Tooltip("Which layers are considered valid targets.")]
    private LayerMask targetMask = ~0;
    [SerializeField, Tooltip("Maximum number of targets per tick (0 = unlimited).")]
    public int maxTargetsPerTick = 0;
    [Header("Hit Origins")]
    [Tooltip("Optional extra origins to check. If empty, uses this transform.")]
    [SerializeField] private Transform[] hitOrigins;



    [Header("AOE Splash Damage")]
    [SerializeField, Tooltip("Radius around the main target for splash damage. 0 disables splash.")]
    public float splashRadius = 0;
    [SerializeField, Tooltip("Damage dealt to enemies inside splashRadius (percentage of main damage).")]
    [Range(0f, 1f)] public float splashDamagePercent = 0.5f;

    [Header("On Hit Effects")]
    [Tooltip("Whether successful hits can apply a status effect.")]
    public bool applyStatusEffectOnHit = false;
    [Tooltip("Chance for each successful hit to apply the selected status effect.")]
    [Range(0f, 1f)] public float statusApplyChance = 1f;    // optional: chance to apply on hit (0..1)
    [Tooltip("Status effect applied when the on-hit chance succeeds.")]
    public StatusEffectSystem.StatusType statusEffectOnHit = StatusEffectSystem.StatusType.Bleeding;
    [Tooltip("Duration of the applied status effect, in seconds.")]
    public float statusEffectDuration = 3f; // Duration in seconds for the status effect

    [Header("Knockback")]
    [SerializeField, Tooltip("Initial push speed applied to hit enemies, in units/sec. 0 disables knockback.")]
    public float knockbackForce = 0f;
    [Range(0f, 1f)] public float executeChance = 0f;

    [Header("Lifesteal")]
    [Tooltip("Fraction of damage dealt that is restored as health.")]
    [Range(0f, 1f)][SerializeField] public float lifestealPercent = 0.25f;

    [Header("Criticals")]
    [Tooltip("Chance for a hit to deal critical damage.")]
    [Range(0f, 1f)] public float critChance = 0f;
    [Tooltip("Damage multiplier applied when a critical hit occurs.")]
    [Min(1f)] public float critMultiplier = 2f;

    [Header("SFX")]
    [Tooltip("Audio clip played whenever the knife attacks.")]
    [SerializeField] private AudioClip shootClip;
    [Tooltip("Audio clip played when the knife hits at least one target.")]
    [SerializeField] private AudioClip stabClip;
    [Tooltip("Visual effect spawned at each hit target, or near the weapon when the attack misses.")]
    [SerializeField] private GameObject slashEffect;
    [Tooltip("Extra SFX GameObject spawned directly on top of the Knife.")]
    [SerializeField] private GameObject selfSfxObject;

    [Header("UI")]
    [Tooltip("Prefab root GameObject that contains a TextMeshProUGUI somewhere in its children.")]
    [SerializeField] public GameObject statsTextPrefab;
    [Tooltip("Transform under which the weapon stats UI is instantiated.")]
    [SerializeField] private Transform uiParent;
    [Tooltip("Optional custom text appended to the generated weapon stats.")]
    [TextArea][SerializeField] public string extraTextField;
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] public Sprite weaponSprite;

    [Header("Range Visual")]
    [Tooltip("Child SpriteRenderer that should visually match the AOE radius.")]
    [SerializeField] private SpriteRenderer rangeRenderer;
    [Tooltip("Extra world-units padding added to the visual radius (optional).")]
    [SerializeField] private float visualPadding = 0f;
    [Tooltip("If true, auto-scales the rangeRenderer to match 'radius'.")]
    [SerializeField] private bool autoScaleRangeVisual = true;


    [HideInInspector] public TextMeshProUGUI statsTextInstance;
    private GameObject statsGameobjectInstance;
    private Image iconImage;
    private AudioSource shootSource;
    private SimpleHealth parentHealth;
    private WeaponTick wt;
    private WeaponSwingAnimator swingAnimator;

    private void Awake()
    {
        shootSource = GetComponent<AudioSource>();

        if (transform.parent != null && transform.parent.parent != null)
            parentHealth = transform.parent.parent.GetComponent<SimpleHealth>();
        else
            parentHealth = GetComponentInParent<SimpleHealth>();

        if (statsTextPrefab != null && uiParent != null)
        {
            // Instantiate the prefab root
            var go = Instantiate(statsTextPrefab, uiParent);
            // Find the TMP text anywhere under it
            statsTextInstance = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (statsTextInstance != null) statsTextInstance.text = "";
            statsGameobjectInstance = go;
            // Find Image in children and assign sprite
            // Find child GameObject named "Icon" and get its Image
            var iconObj = go.transform.Find("Icon");
            if (iconObj != null)
                iconImage = iconObj.GetComponent<Image>();

            if (iconImage != null && weaponSprite != null)
                iconImage.sprite = weaponSprite;

        }

        wt = GetComponent<WeaponTick>();
        swingAnimator = GetComponent<WeaponSwingAnimator>();
        UpdateStatsText();
        UpdateRangeVisual();
    }

    private void Update()
    {
        UpdateStatsText();

        if (autoScaleRangeVisual)
            UpdateRangeVisual();
    }

    public void UpdateStatsText()
    {
        if (statsTextInstance != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            const string numColor = "#8888FF";
            WeaponUpgrades[] wu = GetComponentsInChildren<WeaponUpgrades>(true);

            sb.AppendLine($"<b>{transform.name} Stats</b>");

            // ✅ Count enabled upgrades vs total
            int enabledCount = 0;
            foreach (var upgrade in wu)
            {
                if (upgrade != null && upgrade.gameObject.activeInHierarchy && upgrade.enabled)
                    enabledCount++;
            }
            sb.AppendLine($"Upgrades: <color={numColor}>{enabledCount}</color>/<color={numColor}>{WeaponUpgrades.MaxUpgrades}</color>");

            sb.AppendLine($"Damage: <color={numColor}>{damage}</color>");
            string dtColor = GetDamageTypeHex(damageType);
            sb.AppendLine($"Damage Type: <color={dtColor}>{damageType}</color>");
            sb.AppendLine($"Radius: <color={numColor}>{radius:F2}</color>");
            sb.AppendLine($"Splash: <color={numColor}>{splashRadius:F2}</color> (<color={numColor}>{splashDamagePercent * 100f:F0}</color>% dmg)");

            if (wt != null)
                sb.AppendLine($"Attack Delay: <color={numColor}>{wt.interval:F1}</color>s");

            sb.AppendLine($"Lifesteal: <color={numColor}>{(lifestealPercent * 100f):F0}</color>%");
            sb.AppendLine($"Crit: <color={numColor}>{(critChance * 100f):F0}</color>% x<color={numColor}>{critMultiplier:F2}</color>");
            if (knockbackForce > 0f)
                sb.AppendLine($"Knockback: <color={numColor}>{knockbackForce:F1}</color>");
            sb.AppendLine($"Max Targets: <color={numColor}>{maxTargetsPerTick}</color>");

            if (applyStatusEffectOnHit)
            {
                sb.AppendLine($"Status Effect Chance: <color={numColor}>{statusApplyChance * 100f:F0}</color>%");
                sb.AppendLine($"On Hit: {statusEffectOnHit} (<color={numColor}>{statusEffectDuration:F1}</color>s)");
            }



            if (!string.IsNullOrWhiteSpace(extraTextField))
                sb.AppendLine(extraTextField);

            statsTextInstance.text = sb.ToString();
        }
    }


    public void RemoveStatsText()
    {
        if (statsTextInstance != null)
        {
            Destroy(statsTextInstance.gameObject.transform.root.gameObject);
            statsTextInstance = null;
        }
    }
    private void OnDisable()
    {
        Destroy(statsGameobjectInstance);
    }
    public void OnKnifeTick()
    {
        if (swingAnimator != null)
        {
            swingAnimator.Swing();
        }


        if (selfSfxObject != null)
            Instantiate(selfSfxObject, transform.position, Quaternion.identity);

        // choose origins (fallback to self)
        Transform[] origins = (hitOrigins != null && hitOrigins.Length > 0) ? hitOrigins : new Transform[] { transform };

        if (shootClip != null) shootSource?.PlayOneShot(shootClip);

        bool anyHit = false;
        int targetsHit = 0;
        int targetCap = (maxTargetsPerTick > 0) ? maxTargetsPerTick : int.MaxValue;
        HashSet<Collider2D> processed = new HashSet<Collider2D>();

        for (int oi = 0; oi < origins.Length; oi++)
        {
            var origin = origins[oi];
            if (origin == null) continue;

            Collider2D[] hits = Physics2D.OverlapCircleAll(origin.position, radius, targetMask);

            // Order/select targets based on targeting mode and remaining capacity
            List<Collider2D> selected = OrderTargets(hits, origin, targetingMode, processed, targetCap - targetsHit);

            if (selected.Count > 0 && !anyHit)
            {
                anyHit = true;
                if (stabClip != null) shootSource?.PlayOneShot(stabClip);
            }

            for (int hi = 0; hi < selected.Count; hi++)
            {
                var col = selected[hi];
                if (col == null) continue;

                processed.Add(col);

                if (slashEffect != null)
                    Instantiate(slashEffect, col.transform.position, Quaternion.identity);

                SimpleHealth health = col.GetComponent<SimpleHealth>();
                StatusEffectSystem splashStatus = col.GetComponent<StatusEffectSystem>();

                if (health != null && health.IsAlive && !health.IsInvulnerable)
                {
                    // status on hit
                    if (splashStatus != null && applyStatusEffectOnHit && Random.Range(0f, 1f) <= statusApplyChance)
                    {
                        splashStatus.AddStatus(statusEffectOnHit, statusEffectDuration, 1f);
                    }

                    // main hit
                    bool isCrit = Random.value < Mathf.Clamp01(critChance);
                    float mult = isCrit ? Mathf.Max(1f, critMultiplier) : 1f;
                    int dealt = Mathf.RoundToInt(damage * mult);

                    if (executeChance > 0f && Random.value < executeChance)
                        health.TakeDamage(health.CurrentHealth, damageType, false, false);
                    else
                        health.TakeDamage(dealt, damageType);

                    // knockback (away from the hit origin)
                    if (knockbackForce > 0f)
                    {
                        EnemyChaser chaser = col.GetComponent<EnemyChaser>();
                        if (chaser != null)
                        {
                            Vector2 dir = (Vector2)col.transform.position - (Vector2)origin.position;
                            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Random.insideUnitCircle.normalized;
                            chaser.ApplyKnockback(dir * knockbackForce);
                        }
                    }

                    // lifesteal
                    if (lifestealPercent > 0f && parentHealth != null && parentHealth.IsAlive)
                    {
                        int healAmount = Mathf.RoundToInt(dealt * lifestealPercent);
                        parentHealth.Heal(healAmount);
                    }

                    // splash
                    if (splashRadius > 0f && splashDamagePercent > 0f)
                    {
                        Collider2D[] splashHits = Physics2D.OverlapCircleAll(col.transform.position, splashRadius, targetMask);
                        for (int si = 0; si < splashHits.Length; si++)
                        {
                            var splashCol = splashHits[si];
                            if (splashCol == null || splashCol == col || splashCol.gameObject == gameObject) continue;

                            SimpleHealth splashHealth = splashCol.GetComponent<SimpleHealth>();
                            if (splashHealth != null && splashHealth.IsAlive && !splashHealth.IsInvulnerable)
                            {
                                int splashDamage = Mathf.RoundToInt(dealt * splashDamagePercent);
                                splashHealth.TakeDamage(splashDamage, damageType);
                            }
                        }
                    }

                    targetsHit++;
                    if (targetsHit >= targetCap)
                        return; // stop after cap reached
                }
            }
        }

        // no targets anywhere → fling a slash VFX near first origin (or self)
        if (!anyHit && slashEffect != null)
        {
            Transform baseOrigin = (hitOrigins != null && hitOrigins.Length > 0 && hitOrigins[0] != null) ? hitOrigins[0] : transform;
            Vector3 fxPos = baseOrigin.position + (Vector3)(Random.insideUnitCircle * 1f);
            Instantiate(slashEffect, fxPos, Quaternion.identity);
        }
    }


    [ContextMenu("Sync Range Visual Now")]
    private void UpdateRangeVisual()
    {
        if (!autoScaleRangeVisual || rangeRenderer == null || rangeRenderer.sprite == null)
            return;

        float desiredDiameter = Mathf.Max(0f, 2f * (radius + visualPadding));
        var sprite = rangeRenderer.sprite;
        Vector2 spriteSizeWorld = sprite.bounds.size;

        float parentScaleX = rangeRenderer.transform.parent ? rangeRenderer.transform.parent.lossyScale.x : 1f;
        float parentScaleY = rangeRenderer.transform.parent ? rangeRenderer.transform.parent.lossyScale.y : 1f;

        float baseW = Mathf.Max(0.0001f, spriteSizeWorld.x * parentScaleX);
        float baseH = Mathf.Max(0.0001f, spriteSizeWorld.y * parentScaleY);

        float scaleX = desiredDiameter / baseW;
        float scaleY = desiredDiameter / baseH;

        rangeRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        rangeRenderer.enabled = desiredDiameter > 0.0001f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isEditor && !Application.isPlaying)
            UpdateRangeVisual();
    }
#endif

    public void EnableOnHitEffect(StatusEffectSystem.StatusType effectType)
    {
        applyStatusEffectOnHit = true;
        statusEffectOnHit = effectType;
    }

    public void SetOnHitEffectDuration(float duration)
    {
        statusEffectDuration = duration;
    }
    public void EnableOnHitEffectByIndex(int effectIndex)
    {
        applyStatusEffectOnHit = true;
        statusEffectOnHit = (StatusEffectSystem.StatusType)effectIndex;
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);

        if (splashRadius > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, splashRadius);
        }
    }

    private static string GetDamageTypeHex(SimpleHealth.DamageType type)
    {
        switch (type)
        {
            case SimpleHealth.DamageType.Fire: return "#FF6600";       // orange-red
            case SimpleHealth.DamageType.Cold: return "#4DB2FF";       // icy blue
            case SimpleHealth.DamageType.Lightning: return "#FFFF4D";  // yellow
            case SimpleHealth.DamageType.Poison: return "#80FF80";     // green
            case SimpleHealth.DamageType.Physical:
            default: return "#FFFFFF";                                // white
        }
    }

    // Orders and selects targets from hits based on the chosen mode.
    private List<Collider2D> OrderTargets(Collider2D[] hits, Transform origin, TargetingMode mode, HashSet<Collider2D> alreadyChosen, int takeCount)
    {
        List<(Collider2D col, SimpleHealth hp, float dist)> candidates = new List<(Collider2D, SimpleHealth, float)>();
        Vector3 o = origin != null ? origin.position : transform.position;

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null || c.gameObject == gameObject) continue;
            if (alreadyChosen != null && alreadyChosen.Contains(c)) continue;

            SimpleHealth h = c.GetComponent<SimpleHealth>();
            if (h == null || !h.IsAlive || h.IsInvulnerable) continue;

            float d = Vector2.Distance(o, c.transform.position);
            candidates.Add((c, h, d));
        }

        // Sort by mode
        switch (mode)
        {
            case TargetingMode.Closest:
                candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
                break;
            case TargetingMode.Furthest:
                candidates.Sort((a, b) => b.dist.CompareTo(a.dist));
                break;
            case TargetingMode.MoreHP:
                candidates.Sort((a, b) =>
                {
                    int cmp = b.hp.CurrentHealth.CompareTo(a.hp.CurrentHealth);
                    if (cmp != 0) return cmp;
                    return a.dist.CompareTo(b.dist);
                });
                break;
            case TargetingMode.LessHP:
                candidates.Sort((a, b) =>
                {
                    int cmp = a.hp.CurrentHealth.CompareTo(b.hp.CurrentHealth);
                    if (cmp != 0) return cmp;
                    return a.dist.CompareTo(b.dist);
                });
                break;
            case TargetingMode.Random:
                // Fisher-Yates shuffle
                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    var tmp = candidates[i];
                    candidates[i] = candidates[j];
                    candidates[j] = tmp;
                }
                break;
        }

        int toTake = Mathf.Clamp(takeCount <= 0 ? int.MaxValue : takeCount, 0, candidates.Count);
        List<Collider2D> result = new List<Collider2D>(toTake);
        for (int i = 0; i < toTake; i++)
            result.Add(candidates[i].col);
        return result;
    }
}

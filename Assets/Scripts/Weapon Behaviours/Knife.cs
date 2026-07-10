using TMPro;
using UnityEngine;
using UnityEngine.UI; // For Image
using UnityEngine.Serialization;
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
    private const float DamageOutputMultiplier = 0.5f;

    [Header("Targeting")]
    [SerializeField, Tooltip("How to prioritize targets within the radius.")]
    public TargetingMode targetingMode = TargetingMode.Closest;
    [Header("AOE Damage")]
    [SerializeField, Tooltip("Main hit radius for selecting enemies.")]
    public float radius = 1f;
    [SerializeField, Min(0), Tooltip("Minimum base damage rolled for each main hit before crits.")]
    public int minDamage = 10;
    [SerializeField, Min(0), Tooltip("Maximum base damage rolled for each main hit before crits.")]
    public int damage = 10;
    [SerializeField, Tooltip("Damage category used for resistances, weaknesses, and damage coloring.")]
    public SimpleHealth.DamageType damageType;
    [SerializeField, Tooltip("Name credited in combat logs for direct and splash hits.")]
    private string damageSourceName = "Knife";
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

    [Header("Splash Visual")]
    [SerializeField, Tooltip("Draw a temporary transparent circle when splash damage triggers.")]
    private bool showSplashCircle = true;
    [SerializeField, Tooltip("Color and opacity of the runtime splash circle.")]
    private Color splashCircleColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField, Min(0.01f), Tooltip("How long the splash circle remains visible.")]
    private float splashCircleDuration = 0.18f;
    [SerializeField, Tooltip("SpriteRenderer order for the splash circle.")]
    private int splashCircleSortingOrder = 5;

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
    [FormerlySerializedAs("executeChance")]
    [Tooltip("Enemies at or below this fraction of their max health are instantly slain by this weapon's hits.")]
    [Range(0f, 1f)] public float cullThreshold = 0f;

    [Header("Lifesteal")]
    [Tooltip("Fraction of damage dealt that is restored as health.")]
    [Range(0f, 1f)][SerializeField] public float lifestealPercent = 0.25f;

    [Header("Criticals")]
    [Tooltip("Chance for a hit to deal critical damage.")]
    [Range(0f, 1f)] public float critChance = 0f;
    [Tooltip("Damage multiplier applied when a critical hit occurs.")]
    [Min(1f)] public float critMultiplier = 2f;

    [Header("Unique Effects")]
    [Tooltip("Chance for a successful hit to repeat for partial damage.")]
    [Range(0f, 1f)] public float echoStrikeChance = 0f;
    [Tooltip("Fraction of the original hit damage dealt by Echo Strike.")]
    [Range(0f, 1f)] public float echoStrikeDamagePercent = 0.5f;

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
    private PlayerAccessoryStats accessoryStats;
    private Transform[] fallbackOrigins;
    private readonly List<Collider2D> hitsBuffer = new List<Collider2D>(32);
    private readonly List<Collider2D> splashHitsBuffer = new List<Collider2D>(32);
    private readonly List<TargetCandidate> candidates = new List<TargetCandidate>(32);
    private readonly HashSet<Collider2D> processed = new HashSet<Collider2D>();
    private readonly System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
    private ContactFilter2D contactFilter;
    private WeaponUpgrades[] wu = new WeaponUpgrades[0];
    private bool weaponUpgradesDirty = true;
    private StatsSnapshot statsSnapshot;
    private bool hasStatsSnapshot;

    private static readonly System.Comparison<TargetCandidate> closestComparison = CompareClosest;
    private static readonly System.Comparison<TargetCandidate> furthestComparison = CompareFurthest;
    private static readonly System.Comparison<TargetCandidate> moreHpComparison = CompareMoreHp;
    private static readonly System.Comparison<TargetCandidate> lessHpComparison = CompareLessHp;
    private static readonly Stack<SplashCircleFade> splashCirclePool = new Stack<SplashCircleFade>();

    private readonly struct TargetCandidate
    {
        public readonly Collider2D col;
        public readonly SimpleHealth hp;
        public readonly float dist;

        public TargetCandidate(Collider2D col, SimpleHealth hp, float dist)
        {
            this.col = col;
            this.hp = hp;
            this.dist = dist;
        }
    }

    private readonly struct EffectiveValues
    {
        public readonly float effectiveRadius;
        public readonly float effectiveSplashRadius;
        public readonly float effectiveCritChance;
        public readonly float effectiveCritMultiplier;
        public readonly float effectiveKnockback;
        public readonly float effectiveStatusChance;
        public readonly float effectiveStatusDuration;

        public EffectiveValues(
            float effectiveRadius,
            float effectiveSplashRadius,
            float effectiveCritChance,
            float effectiveCritMultiplier,
            float effectiveKnockback,
            float effectiveStatusChance,
            float effectiveStatusDuration)
        {
            this.effectiveRadius = effectiveRadius;
            this.effectiveSplashRadius = effectiveSplashRadius;
            this.effectiveCritChance = effectiveCritChance;
            this.effectiveCritMultiplier = effectiveCritMultiplier;
            this.effectiveKnockback = effectiveKnockback;
            this.effectiveStatusChance = effectiveStatusChance;
            this.effectiveStatusDuration = effectiveStatusDuration;
        }
    }

    private struct StatsSnapshot
    {
        public string weaponName;
        public int enabledCount;
        public int minDamage;
        public int damage;
        public SimpleHealth.DamageType damageType;
        public float effectiveRadius;
        public float effectiveSplashRadius;
        public float splashDamagePercent;
        public bool hasDelay;
        public float delay;
        public float manaCostPerTick;
        public float lifestealPercent;
        public float effectiveCritChance;
        public float effectiveCritMultiplier;
        public float echoStrikeChance;
        public float echoStrikeDamagePercent;
        public float effectiveKnockback;
        public int maxTargetsPerTick;
        public bool applyStatusEffectOnHit;
        public float effectiveStatusChance;
        public StatusEffectSystem.StatusType statusEffectOnHit;
        public float effectiveStatusDuration;
        public string extraTextField;

        public bool Matches(StatsSnapshot other)
        {
            return weaponName == other.weaponName &&
                   enabledCount == other.enabledCount &&
                   minDamage == other.minDamage &&
                   damage == other.damage &&
                   damageType == other.damageType &&
                   effectiveRadius == other.effectiveRadius &&
                   effectiveSplashRadius == other.effectiveSplashRadius &&
                   splashDamagePercent == other.splashDamagePercent &&
                   hasDelay == other.hasDelay &&
                   delay == other.delay &&
                   manaCostPerTick == other.manaCostPerTick &&
                   lifestealPercent == other.lifestealPercent &&
                   effectiveCritChance == other.effectiveCritChance &&
                   effectiveCritMultiplier == other.effectiveCritMultiplier &&
                   echoStrikeChance == other.echoStrikeChance &&
                   echoStrikeDamagePercent == other.echoStrikeDamagePercent &&
                   effectiveKnockback == other.effectiveKnockback &&
                   maxTargetsPerTick == other.maxTargetsPerTick &&
                   applyStatusEffectOnHit == other.applyStatusEffectOnHit &&
                   effectiveStatusChance == other.effectiveStatusChance &&
                   statusEffectOnHit == other.statusEffectOnHit &&
                   effectiveStatusDuration == other.effectiveStatusDuration &&
                   extraTextField == other.extraTextField;
        }
    }

    private void Awake()
    {
        shootSource = GetComponent<AudioSource>();
        fallbackOrigins = new[] { transform };

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

            ConfigureAppliedUpgradeTooltip(go);
        }

        wt = GetComponent<WeaponTick>();
        swingAnimator = GetComponent<WeaponSwingAnimator>();
        accessoryStats = PlayerAccessoryStats.Find(transform);
        UpdateStatsText();
        UpdateRangeVisual();
    }

    private void ConfigureAppliedUpgradeTooltip(GameObject statsObject)
    {
        if (statsObject == null) return;

        if (!statsObject.TryGetComponent(out TooltipTarget _))
            statsObject.AddComponent<TooltipTarget>();

        var provider = statsObject.GetComponent<AppliedUpgradeTooltipProvider>();
        if (provider == null)
            provider = statsObject.AddComponent<AppliedUpgradeTooltipProvider>();

        provider.Configure(transform, transform.name);
    }

    private void Update()
    {
        RefreshStatsText(false);

        if (autoScaleRangeVisual)
            UpdateRangeVisual();
    }

    public void UpdateStatsText()
    {
        RefreshWeaponUpgrades();
        RefreshStatsText(true);
    }

    private void RefreshStatsText(bool force)
    {
        if (statsTextInstance == null)
            return;

        if (weaponUpgradesDirty)
            RefreshWeaponUpgrades();

        int enabledCount = 0;
        for (int i = 0; i < wu.Length; i++)
        {
            var upgrade = wu[i];
            if (upgrade != null && upgrade.gameObject.activeInHierarchy && upgrade.enabled)
                enabledCount++;
        }

        EffectiveValues effectiveValues = GetEffectiveValues(GetAccessoryStats());
        StatsSnapshot currentSnapshot = new StatsSnapshot
        {
            weaponName = transform.name,
            enabledCount = enabledCount,
            minDamage = minDamage,
            damage = damage,
            damageType = damageType,
            effectiveRadius = effectiveValues.effectiveRadius,
            effectiveSplashRadius = effectiveValues.effectiveSplashRadius,
            splashDamagePercent = splashDamagePercent,
            hasDelay = wt != null,
            delay = wt != null ? wt.EffectiveInterval : 0f,
            manaCostPerTick = wt != null ? wt.ManaCostPerTick : 0f,
            lifestealPercent = lifestealPercent,
            effectiveCritChance = effectiveValues.effectiveCritChance,
            effectiveCritMultiplier = effectiveValues.effectiveCritMultiplier,
            echoStrikeChance = echoStrikeChance,
            echoStrikeDamagePercent = echoStrikeDamagePercent,
            effectiveKnockback = effectiveValues.effectiveKnockback,
            maxTargetsPerTick = maxTargetsPerTick,
            applyStatusEffectOnHit = applyStatusEffectOnHit,
            effectiveStatusChance = effectiveValues.effectiveStatusChance,
            statusEffectOnHit = statusEffectOnHit,
            effectiveStatusDuration = effectiveValues.effectiveStatusDuration,
            extraTextField = extraTextField
        };

        if (!force && hasStatsSnapshot && statsSnapshot.Matches(currentSnapshot))
            return;

        const string numColor = "#8888FF";
        sb.Clear();
        sb.AppendLine($"<b>{currentSnapshot.weaponName}</b>");

        if (currentSnapshot.enabledCount > 0)
            sb.AppendLine($"Upg: <color={numColor}>{currentSnapshot.enabledCount}</color>/<color={numColor}>{WeaponUpgrades.MaxUpgrades}</color>");

        string damageColor = GetDamageTypeHex(currentSnapshot.damageType);
        sb.AppendLine($"DMG: <color={damageColor}>{GetDamageRangeText()}</color>");
        if (currentSnapshot.damageType != SimpleHealth.DamageType.Physical)
            sb.AppendLine($"Type: <color={damageColor}>{currentSnapshot.damageType}</color>");
        sb.AppendLine($"Range: <color={numColor}>{currentSnapshot.effectiveRadius:F2}</color>");
        if (currentSnapshot.effectiveSplashRadius > 0f && currentSnapshot.splashDamagePercent > 0f)
            sb.AppendLine($"AOE: <color={numColor}>{currentSnapshot.effectiveSplashRadius:F2}</color> (<color={numColor}>{currentSnapshot.splashDamagePercent * 100f:F0}</color>% dmg)");

        if (currentSnapshot.hasDelay)
            sb.AppendLine($"Delay: <color={numColor}>{currentSnapshot.delay:F1}</color>s");
        if (currentSnapshot.manaCostPerTick > 0f)
            sb.AppendLine($"Mana: <color={numColor}>{currentSnapshot.manaCostPerTick:F1}</color>/tick");

        if (currentSnapshot.lifestealPercent > 0f)
            sb.AppendLine($"Steal: <color={numColor}>{currentSnapshot.lifestealPercent * 100f:F0}</color>%");
        if (currentSnapshot.effectiveCritChance > 0f)
            sb.AppendLine($"Crit: <color={numColor}>{currentSnapshot.effectiveCritChance * 100f:F0}</color>% x<color={numColor}>{currentSnapshot.effectiveCritMultiplier:F2}</color>");
        if (currentSnapshot.echoStrikeChance > 0f)
            sb.AppendLine($"Echo: <color={numColor}>{currentSnapshot.echoStrikeChance * 100f:F0}</color>% for <color={numColor}>{currentSnapshot.echoStrikeDamagePercent * 100f:F0}</color>% dmg");
        if (currentSnapshot.effectiveKnockback > 0f)
            sb.AppendLine($"KB: <color={numColor}>{currentSnapshot.effectiveKnockback:F1}</color>");
        if (currentSnapshot.maxTargetsPerTick > 0)
            sb.AppendLine($"Targets: <color={numColor}>{currentSnapshot.maxTargetsPerTick}</color>");

        if (currentSnapshot.applyStatusEffectOnHit)
        {
            sb.AppendLine($"Proc: <color={numColor}>{currentSnapshot.effectiveStatusChance * 100f:F0}</color>%");
            sb.AppendLine($"Hit: {currentSnapshot.statusEffectOnHit} (<color={numColor}>{currentSnapshot.effectiveStatusDuration:F1}</color>s)");
        }

        if (!string.IsNullOrWhiteSpace(currentSnapshot.extraTextField))
            sb.AppendLine(currentSnapshot.extraTextField);

        statsTextInstance.text = sb.ToString();
        statsSnapshot = currentSnapshot;
        hasStatsSnapshot = true;
    }

    private void RefreshWeaponUpgrades()
    {
        wu = GetComponentsInChildren<WeaponUpgrades>(true);
        weaponUpgradesDirty = false;
    }

    private void OnTransformChildrenChanged()
    {
        weaponUpgradesDirty = true;
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
        if (selfSfxObject != null)
            Instantiate(selfSfxObject, transform.position, Quaternion.identity);

        // choose origins (fallback to self)
        if (fallbackOrigins == null)
            fallbackOrigins = new[] { transform };
        Transform[] origins = (hitOrigins != null && hitOrigins.Length > 0) ? hitOrigins : fallbackOrigins;

        if (shootClip != null) shootSource?.PlayOneShot(shootClip);

        bool anyHit = false;
        bool swingStarted = false;
        int targetsHit = 0;
        int targetCap = (maxTargetsPerTick > 0) ? maxTargetsPerTick : int.MaxValue;
        processed.Clear();
        contactFilter.SetLayerMask(targetMask);
        contactFilter.useTriggers = Physics2D.queriesHitTriggers;

        EffectiveValues effectiveValues = GetEffectiveValues(GetAccessoryStats());
        float effectiveRadius = effectiveValues.effectiveRadius;
        float effectiveSplashRadius = effectiveValues.effectiveSplashRadius;
        float effectiveStatusChance = effectiveValues.effectiveStatusChance;
        float effectiveStatusDuration = effectiveValues.effectiveStatusDuration;
        float effectiveKnockback = effectiveValues.effectiveKnockback;
        float effectiveEchoStrikeChance = Mathf.Clamp01(echoStrikeChance);
        float effectiveEchoStrikeDamagePercent = Mathf.Clamp01(echoStrikeDamagePercent);

        for (int oi = 0; oi < origins.Length; oi++)
        {
            var origin = origins[oi];
            if (origin == null) continue;

            hitsBuffer.Clear();
            Physics2D.OverlapCircle(origin.position, effectiveRadius, contactFilter, hitsBuffer);

            // Order/select targets based on targeting mode and remaining capacity
            int selected = OrderTargets(hitsBuffer, origin, targetingMode, processed, targetCap - targetsHit);

            if (selected > 0 && !anyHit)
            {
                anyHit = true;
                if (stabClip != null) shootSource?.PlayOneShot(stabClip);
            }

            for (int hi = 0; hi < selected; hi++)
            {
                TargetCandidate candidate = candidates[hi];
                var col = candidate.col;
                if (col == null) continue;

                if (!swingStarted && swingAnimator != null)
                {
                    swingAnimator.SwingTowards(col.transform.position, origin.position);
                    swingStarted = true;
                }

                processed.Add(col);

                if (slashEffect != null)
                    Instantiate(slashEffect, col.transform.position, Quaternion.identity);

                SimpleHealth health = candidate.hp;

                if (health != null && health.IsAlive && !health.IsInvulnerable)
                {
                    // status on hit
                    if (applyStatusEffectOnHit)
                    {
                        StatusEffectSystem splashStatus = col.GetComponent<StatusEffectSystem>();
                        if (splashStatus != null && Random.Range(0f, 1f) <= effectiveStatusChance)
                            splashStatus.AddStatus(statusEffectOnHit, effectiveStatusDuration, 1f, gameObject);
                    }

                    // main hit
                    int dealt = RollHitDamage(effectiveValues.effectiveCritChance, effectiveValues.effectiveCritMultiplier, out bool isCritical);

                    bool cull = cullThreshold > 0f && health.CurrentHealth <= health.MaxHealth * cullThreshold;
                    if (cull)
                        health.TakeDamage(health.CurrentHealth, damageType, false, false, gameObject, "Cull");
                    else
                        health.TakeDamage(dealt, damageType, true, true, gameObject, GetDamageSourceName(), isCritical);

                    if (!cull && echoStrikeChance > 0f && health.IsAlive && Random.value <= effectiveEchoStrikeChance)
                    {
                        int echoDamage = Mathf.Max(1, Mathf.RoundToInt(dealt * effectiveEchoStrikeDamagePercent));
                        health.TakeDamage(echoDamage, damageType, true, true, gameObject, "Echo Strike", isCritical);
                        if (slashEffect != null)
                            Instantiate(slashEffect, col.transform.position, Quaternion.identity);
                    }

                    // knockback (away from the hit origin)
                    if (effectiveKnockback > 0f)
                        ApplyKnockback(col, origin.position, effectiveKnockback);

                    // lifesteal
                    if (lifestealPercent > 0f && parentHealth != null && parentHealth.IsAlive)
                    {
                        int healAmount = Mathf.RoundToInt(dealt * lifestealPercent);
                        parentHealth.Heal(healAmount);
                    }

                    // splash
                    if (effectiveSplashRadius > 0f && splashDamagePercent > 0f)
                    {
                        SpawnSplashCircle(col.transform.position, effectiveSplashRadius);
                        int splashDamage = Mathf.RoundToInt(dealt * splashDamagePercent);
                        splashHitsBuffer.Clear();
                        Physics2D.OverlapCircle(col.transform.position, effectiveSplashRadius, contactFilter, splashHitsBuffer);
                        for (int si = 0; si < splashHitsBuffer.Count; si++)
                        {
                            var splashCol = splashHitsBuffer[si];
                            if (splashCol == null || splashCol == col || splashCol.gameObject == gameObject) continue;

                            SimpleHealth splashHealth = splashCol.GetComponent<SimpleHealth>();
                            if (splashHealth != null && splashHealth.IsAlive && !splashHealth.IsInvulnerable)
                                splashHealth.TakeDamage(splashDamage, damageType, true, true, gameObject, GetSplashDamageSourceName(), isCritical);
                        }
                    }

                    targetsHit++;
                    if (targetsHit >= targetCap)
                        return; // stop after cap reached
                }
            }
        }

        if (!swingStarted && swingAnimator != null)
        {
            swingAnimator.Swing();
        }

        // no targets anywhere → fling a slash VFX near first origin (or self)
        if (!anyHit && slashEffect != null)
        {
            Transform baseOrigin = (hitOrigins != null && hitOrigins.Length > 0 && hitOrigins[0] != null) ? hitOrigins[0] : transform;
            Vector3 fxPos = baseOrigin.position + (Vector3)(Random.insideUnitCircle * 1f);
            Instantiate(slashEffect, fxPos, Quaternion.identity);
        }
    }

    private string GetDamageSourceName()
    {
        return string.IsNullOrWhiteSpace(damageSourceName) ? "Knife" : damageSourceName.Trim();
    }

    private string GetSplashDamageSourceName()
    {
        return $"{GetDamageSourceName()} Splash";
    }

    private void SpawnSplashCircle(Vector3 center, float effectRadius)
    {
        if (!showSplashCircle || effectRadius <= 0f || splashCircleColor.a <= 0f)
            return;

        SplashCircleFade splashCircle = GetSplashCircle();
        var go = splashCircle.gameObject;
        go.name = "Knife Splash Circle";
        go.transform.position = center;
        go.transform.localScale = Vector3.one * (effectRadius * 2f);

        var spriteRenderer = splashCircle.Renderer;
        spriteRenderer.sprite = GetSplashCircleSprite();
        spriteRenderer.color = splashCircleColor;
        spriteRenderer.sortingOrder = splashCircleSortingOrder;
        spriteRenderer.sortingLayerID = rangeRenderer != null ? rangeRenderer.sortingLayerID : 0;
        spriteRenderer.enabled = true;

        splashCircle.Init(spriteRenderer, splashCircleColor, splashCircleDuration);
    }

    private static SplashCircleFade GetSplashCircle()
    {
        while (splashCirclePool.Count > 0)
        {
            SplashCircleFade splashCircle = splashCirclePool.Pop();
            if (splashCircle != null)
                return splashCircle;
        }

        var go = new GameObject("Knife Splash Circle");
        var spriteRenderer = go.AddComponent<SpriteRenderer>();
        SplashCircleFade created = go.AddComponent<SplashCircleFade>();
        created.SetRenderer(spriteRenderer);
        return created;
    }

    private static void ReturnSplashCircle(SplashCircleFade splashCircle)
    {
        if (splashCircle != null)
            splashCirclePool.Push(splashCircle);
    }

    private void ApplyKnockback(Collider2D col, Vector3 originPosition, float strength)
    {
        Vector2 direction = (Vector2)col.bounds.center - (Vector2)originPosition;
        if (direction.sqrMagnitude <= 1e-6f)
            direction = (Vector2)col.transform.position - (Vector2)originPosition;
        if (direction.sqrMagnitude <= 1e-6f)
            direction = Random.insideUnitCircle;
        if (direction.sqrMagnitude <= 1e-6f)
            direction = Vector2.right;

        Vector2 impulse = direction.normalized * strength;
        if (col.GetComponentInParent<EnemyChaser>() is EnemyChaser chaser)
        {
            chaser.ApplyKnockback(impulse);
            return;
        }

        if (col.GetComponentInParent<Snappy2DController>() is Snappy2DController playerMovement)
            playerMovement.ApplyKnockback(impulse);
    }

    public int RollBaseDamage()
    {
        int min = Mathf.Max(0, Mathf.Min(minDamage, damage));
        int max = Mathf.Max(0, Mathf.Max(minDamage, damage));
        return Random.Range(min, max + 1);
    }

    public int RollHitDamage()
    {
        EffectiveValues effectiveValues = GetEffectiveValues(GetAccessoryStats());
        return RollHitDamage(effectiveValues.effectiveCritChance, effectiveValues.effectiveCritMultiplier, out _);
    }

    private int RollHitDamage(float effectiveCritChance, float effectiveCritMultiplier, out bool isCritical)
    {
        int baseDamage = RollBaseDamage();
        int finalDamage;
        isCritical = Random.value < effectiveCritChance;
        if (isCritical)
            finalDamage = PlayerDamageMultiplierUtility.Apply(gameObject, Mathf.RoundToInt(baseDamage * effectiveCritMultiplier));
        else
            finalDamage = PlayerDamageMultiplierUtility.Apply(gameObject, baseDamage);

        return Mathf.Max(1, Mathf.RoundToInt(finalDamage * DamageOutputMultiplier));
    }

    private PlayerAccessoryStats GetAccessoryStats()
    {
        if (accessoryStats == null)
            accessoryStats = PlayerAccessoryStats.Find(transform);
        return accessoryStats;
    }

    private EffectiveValues GetEffectiveValues(PlayerAccessoryStats stats)
    {
        float weaponAreaMultiplier = stats != null ? stats.WeaponAreaMultiplier : 1f;
        return new EffectiveValues(
            radius * weaponAreaMultiplier,
            splashRadius * weaponAreaMultiplier,
            Mathf.Clamp01(critChance + (stats != null ? stats.CriticalChanceBonus : 0f)),
            Mathf.Max(1f, critMultiplier + (stats != null ? stats.CriticalDamageBonus : 0f)),
            Mathf.Max(0f, knockbackForce + (stats != null ? stats.KnockbackStrengthBonus : 0f)),
            Mathf.Clamp01(statusApplyChance + (stats != null ? stats.StatusApplicationChanceBonus : 0f)),
            Mathf.Max(0f, statusEffectDuration * (stats != null ? stats.StatusDurationMultiplier : 1f)));
    }

    private float GetEffectiveRadius() => GetEffectiveValues(GetAccessoryStats()).effectiveRadius;
    private float GetEffectiveSplashRadius() => GetEffectiveValues(GetAccessoryStats()).effectiveSplashRadius;
    private float GetEffectiveCritChance() => GetEffectiveValues(GetAccessoryStats()).effectiveCritChance;
    private float GetEffectiveCritMultiplier() => GetEffectiveValues(GetAccessoryStats()).effectiveCritMultiplier;
    private float GetEffectiveKnockback() => GetEffectiveValues(GetAccessoryStats()).effectiveKnockback;
    private float GetEffectiveStatusChance() => GetEffectiveValues(GetAccessoryStats()).effectiveStatusChance;
    private float GetEffectiveStatusDuration() => GetEffectiveValues(GetAccessoryStats()).effectiveStatusDuration;

    private string GetDamageRangeText()
    {
        int min = Mathf.Max(0, Mathf.Min(minDamage, damage));
        int max = Mathf.Max(0, Mathf.Max(minDamage, damage));
        return min == max ? max.ToString() : $"{min}-{max}";
    }

    private static Sprite splashCircleSprite;

    private static Sprite GetSplashCircleSprite()
    {
        if (splashCircleSprite != null)
            return splashCircleSprite;

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(0.82f, 1f, distance));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        splashCircleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        splashCircleSprite.hideFlags = HideFlags.HideAndDontSave;
        return splashCircleSprite;
    }

    private sealed class SplashCircleFade : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Color startColor;
        private float duration;
        private float elapsed;

        public SpriteRenderer Renderer => spriteRenderer;

        public void SetRenderer(SpriteRenderer renderer)
        {
            spriteRenderer = renderer;
        }

        public void Init(SpriteRenderer renderer, Color color, float lifetime)
        {
            spriteRenderer = renderer;
            startColor = color;
            duration = Mathf.Max(0.01f, lifetime);
            elapsed = 0f;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (spriteRenderer != null)
            {
                Color color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, t);
                spriteRenderer.color = color;
            }

            if (t >= 1f)
            {
                gameObject.SetActive(false);
                ReturnSplashCircle(this);
            }
        }
    }


    [ContextMenu("Sync Range Visual Now")]
    private void UpdateRangeVisual()
    {
        if (!autoScaleRangeVisual || rangeRenderer == null || rangeRenderer.sprite == null)
            return;

        float desiredDiameter = Mathf.Max(0f, 2f * (GetEffectiveRadius() + visualPadding));
        var sprite = rangeRenderer.sprite;
        Vector2 spriteSizeWorld = sprite.bounds.size;

        float parentScaleX = rangeRenderer.transform.parent ? rangeRenderer.transform.parent.lossyScale.x : 1f;
        float parentScaleY = rangeRenderer.transform.parent ? rangeRenderer.transform.parent.lossyScale.y : 1f;

        float baseW = Mathf.Max(0.0001f, spriteSizeWorld.x * parentScaleX);
        float baseH = Mathf.Max(0.0001f, spriteSizeWorld.y * parentScaleY);

        float scaleX = desiredDiameter / baseW;
        float scaleY = desiredDiameter / baseH;

        Vector3 currentScale = rangeRenderer.transform.localScale;
        if (currentScale.x != scaleX || currentScale.y != scaleY || currentScale.z != 1f)
            rangeRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);

        bool shouldBeEnabled = desiredDiameter > 0.0001f;
        if (rangeRenderer.enabled != shouldBeEnabled)
            rangeRenderer.enabled = shouldBeEnabled;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minDamage = Mathf.Max(0, minDamage);
        damage = Mathf.Max(minDamage, damage);

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

    private static int CompareClosest(TargetCandidate a, TargetCandidate b)
    {
        return a.dist.CompareTo(b.dist);
    }

    private static int CompareFurthest(TargetCandidate a, TargetCandidate b)
    {
        return b.dist.CompareTo(a.dist);
    }

    private static int CompareMoreHp(TargetCandidate a, TargetCandidate b)
    {
        int cmp = b.hp.CurrentHealth.CompareTo(a.hp.CurrentHealth);
        return cmp != 0 ? cmp : a.dist.CompareTo(b.dist);
    }

    private static int CompareLessHp(TargetCandidate a, TargetCandidate b)
    {
        int cmp = a.hp.CurrentHealth.CompareTo(b.hp.CurrentHealth);
        return cmp != 0 ? cmp : a.dist.CompareTo(b.dist);
    }

    // Orders and selects targets from hits based on the chosen mode.
    private int OrderTargets(List<Collider2D> hits, Transform origin, TargetingMode mode, HashSet<Collider2D> alreadyChosen, int takeCount)
    {
        candidates.Clear();
        Vector3 o = origin != null ? origin.position : transform.position;

        for (int i = 0; i < hits.Count; i++)
        {
            var c = hits[i];
            if (c == null || c.gameObject == gameObject) continue;
            if (alreadyChosen != null && alreadyChosen.Contains(c)) continue;

            SimpleHealth h = c.GetComponent<SimpleHealth>();
            if (h == null || !h.IsAlive || h.IsInvulnerable) continue;

            float d = Vector2.Distance(o, c.transform.position);
            candidates.Add(new TargetCandidate(c, h, d));
        }

        // Sort by mode
        switch (mode)
        {
            case TargetingMode.Closest:
                candidates.Sort(closestComparison);
                break;
            case TargetingMode.Furthest:
                candidates.Sort(furthestComparison);
                break;
            case TargetingMode.MoreHP:
                candidates.Sort(moreHpComparison);
                break;
            case TargetingMode.LessHP:
                candidates.Sort(lessHpComparison);
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
        return toTake;
    }
}

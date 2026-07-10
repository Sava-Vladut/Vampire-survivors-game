using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using System.Collections.Generic;

public class SimpleHealth : MonoBehaviour
{
    public static event System.Action<DamageReportEntry> AnyDamageTaken;
    public static event System.Action<SimpleHealth> AnyDied;
    public event System.Action<DamageReportEntry> DamageTaken;
    public event System.Action<SimpleHealth> Died;
    public event System.Action<SimpleHealth> HealthReset;

    public readonly struct DamageReportEntry
    {
        public readonly SimpleHealth Target;
        public readonly int Amount;
        public readonly DamageType Type;
        public readonly GameObject SourceObject;
        public readonly string SourceName;
        public readonly string SourceDetail;
        public readonly float HealthAfter;
        public readonly bool WasLethal;
        public readonly int ArmorMitigatedAmount;
        public readonly int EvasionDodgedAmount;
        public readonly bool WasMitigatable;

        public DamageReportEntry(
            SimpleHealth target,
            int amount,
            DamageType type,
            GameObject sourceObject,
            string sourceName,
            string sourceDetail,
            float healthAfter,
            bool wasLethal,
            int armorMitigatedAmount = 0,
            int evasionDodgedAmount = 0,
            bool wasMitigatable = false)
        {
            Target = target;
            Amount = amount;
            Type = type;
            SourceObject = sourceObject;
            SourceName = sourceName;
            SourceDetail = sourceDetail;
            HealthAfter = healthAfter;
            WasLethal = wasLethal;
            ArmorMitigatedAmount = Mathf.Max(0, armorMitigatedAmount);
            EvasionDodgedAmount = Mathf.Max(0, evasionDodgedAmount);
            WasMitigatable = wasMitigatable;
        }
    }

    // NEW: Damage types
    public enum DamageType
    {
        Physical = 0,
        Fire = 1,
        Cold = 2,
        Lightning = 3,
        Poison = 4
    }

    private static readonly DamageType[] DamageTypeOrder =
    {
        DamageType.Physical,
        DamageType.Fire,
        DamageType.Cold,
        DamageType.Lightning,
        DamageType.Poison
    };

    [Header("Health")]
    [SerializeField] public int maxHealth = 100;
    [Tooltip("If <=0, starts at maxHealth.")]
    [SerializeField] private int startingHealth = 100;

    [Header("Damage Immunity")]
    [Tooltip("Seconds of invulnerability after taking damage.")]
    [SerializeField] private float invulnerabilityDuration = 1f;

    [Header("Regeneration")]
    [Tooltip("Health regenerated per second. Can be fractional. Does not grant temporary health.")]
    [SerializeField] public float regenRate = 0f;

    [Header("Temporary Health")]
    [Tooltip("If true, healing past max health grants a temporary, decaying health shield.")]
    [SerializeField] private bool enableTemporaryHealth = true;
    [Tooltip("How fast temporary health depletes per second.")]
    [SerializeField] private float tempHealthDecayRate = 5f;
    [Tooltip("How long to wait after gaining temp health before it starts decaying.")]
    [SerializeField] private float tempHealthDecayDelay = 1.5f;

    [Header("Armor")]
    [Tooltip("Flat armor rating. More armor = more mitigation on small hits.")]
    [SerializeField] public float armor = 0f;
    [Tooltip("How quickly mitigation falls off as the hit gets bigger. Higher = big hits bypass sooner.")]
    [SerializeField] private float armorScaling = 10f;
    [Tooltip("Cap the maximum mitigation fraction (0..0.95). 0.8 = up to 80% reduction on tiny hits.")]
    [Range(0f, 0.95f)][SerializeField] private float maxMitigation = 0.8f;

    [Header("Evasion")]
    [SerializeField] public float evasion = 0f;
    [SerializeField] private float evasionScaling = 10f;
    [SerializeField, Range(0f, 0.95f)] private float maxEvasion = 0.8f;

    [Header("Counter Damage")]
    public int thornsDamage;

    [Header("Resistances")]
    [Tooltip("0..0.95 fraction of damage reduced for each type.")]
    [Range(0f, 0.95f)] public float fireResist = 0f;
    [Range(0f, 0.95f)] public float coldResist = 0f;
    [Range(0f, 0.95f)] public float lightningResist = 0f;
    [Range(0f, 0.95f)] public float poisonResist = 0f;

    [Header("Health UI")]
    [Tooltip("Optional slider to show current health.")]
    [SerializeField] public Slider healthSlider;
    [SerializeField] public TextMeshProUGUI healthText;

    [Header("Stats UI")]
    [Tooltip("Prefab root GameObject that contains a TextMeshProUGUI somewhere in its children.")]
    [SerializeField] private GameObject statsTextPrefab;
    [SerializeField] private Transform uiParent;
    [TextArea][SerializeField] public string extraTextField;
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] private Sprite iconSprite;

    [Header("Effects and Audio")]
    [SerializeField] private Volume playerVolume;
    [SerializeField] private GameObject[] deathObjects;
    [SerializeField] private AudioClip[] damageClip;
    [SerializeField] private AudioClip[] deathClip;
    [SerializeField] private GameObject bloodSFX;

    [Header("Loot")]
    [Tooltip("Weighted loot table. On death we roll once and spawn the result (if any).")]
    [SerializeField] private LootTable2D loot;

    [Header("Hit Flash")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitColor = new Color(1f, 0.5f, 0.5f, 1f);
    [SerializeField] private float hitFlashDuration = 0.1f;

    [Header("Damage Popup")]
    [Tooltip("Prefab with a TextMeshPro or TextMeshProUGUI to display damage taken.")]
    [SerializeField] private GameObject damagePopupPrefab;
    [Tooltip("Offset from entity position when spawning damage popup.")]
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 1f, 0f);
    [Tooltip("If true, starting a negative status effect shows text like Shocked! or Ignited!.")]
    [SerializeField] private bool showStatusAfflictionPopups = true;
    [Tooltip("Extra offset added to the normal damage popup offset for affliction text.")]
    [SerializeField] private Vector3 afflictionPopupExtraOffset = new Vector3(0f, 0.35f, 0f);

    private AudioSource soundSource;
    [HideInInspector] public float currentHealth;
    [HideInInspector] public float currentTemporaryHealth;
    private float tempHealthDecayTimer;
    private bool isInvulnerable;

    private Color _originalColor;
    private bool _hasOriginalColor;
    private Coroutine _flashRoutine;
    private Coroutine _invulnerabilityRoutine;
    private int lastDamageTaken = 0;
    private DamageType lastDamageType = DamageType.Physical; // remember last type
    private readonly int[] runDamageTakenByType = new int[DamageTypeOrder.Length];
    private int runDamageTakenTotal = 0;
    private Snappy2DController movementController;
    private readonly System.Text.StringBuilder _statsBuilder = new System.Text.StringBuilder(256);
    private readonly List<IPlayerDefenseIncreaseProvider> defenseIncreaseProviders = new();

    // Cached components for performance
    private DPSChecker _dpsChecker;
    private StatusEffectSystem _statusEffectSystem;
    private PlayerAccessoryStats _accessoryStats;
    private bool _subscribedToStatusPopups;

    // Stats UI (matches Knife.cs pattern)
    [HideInInspector] public TextMeshProUGUI healthStatsText;
    [HideInInspector] public TextMeshProUGUI defenseStatsText;
    [HideInInspector] public TextMeshProUGUI movementStatsText;

    private Image iconImage;
    private AudioLowPassFilter filter;
    private PlayerSafeZoneStatus safeZoneStatus;
    // Active damage popups per damage type for accumulation
    private readonly Dictionary<DamageType, TMP_Text> _activeDamagePopups = new Dictionary<DamageType, TMP_Text>();
    private bool hasDied;
    private bool statsDirty = true;
    private bool showRunDamageStats;
    private bool hasMovementSnapshot;
    private float lastMoveSpeed;
    private float lastDashSpeed;
    private float lastDashDuration;
    private float lastDashCooldown;
    private bool healthSliderInitialActive;
    private bool healthTextInitialActive;
    private bool capturedHealthBarInitialState;
    private static bool healthBarsVisible = true;
    private static EnemyChaser[] cachedThornsTargets = System.Array.Empty<EnemyChaser>();
    private static float nextThornsTargetRefreshTime;
    private const float ThornsTargetRefreshInterval = 0.2f;

    public bool IsAlive => currentHealth > 0f || (enableTemporaryHealth && currentTemporaryHealth > 0f);
    public bool IsInvulnerable => isInvulnerable;
    public int CurrentHealth => Mathf.RoundToInt(currentHealth);
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        CaptureHealthBarInitialState();

        if (startingHealth <= 0) startingHealth = maxHealth;
        currentHealth = Mathf.Clamp(startingHealth, 0, maxHealth);
        SyncSlider();

        // Cache components
        filter = GetComponent<AudioLowPassFilter>();
        movementController = GetComponent<Snappy2DController>();
        soundSource = GetComponent<AudioSource>();
        _dpsChecker = GetComponent<DPSChecker>();
        _statusEffectSystem = GetComponent<StatusEffectSystem>();
        _accessoryStats = PlayerAccessoryStats.Find(transform);
        SubscribeToStatusPopups();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            _originalColor = spriteRenderer.color;
            _hasOriginalColor = true;
        }

        // Instantiate prefab root (like Knife.cs) and wire up text + icon
        if (statsTextPrefab != null && uiParent != null)
        {
            // Health section
            var go1 = Instantiate(statsTextPrefab, uiParent);
            healthStatsText = go1.GetComponentInChildren<TextMeshProUGUI>(true);

            // Defense section
            var go2 = Instantiate(statsTextPrefab, uiParent);
            defenseStatsText = go2.GetComponentInChildren<TextMeshProUGUI>(true);

            // Movement section
            var go3 = Instantiate(statsTextPrefab, uiParent);
            movementStatsText = go3.GetComponentInChildren<TextMeshProUGUI>(true);

            // Clear initial
            if (healthStatsText != null) healthStatsText.text = string.Empty;
            if (defenseStatsText != null) defenseStatsText.text = string.Empty;
            if (movementStatsText != null) movementStatsText.text = string.Empty;

            // Icon optional (taken from the first card if it has an "Icon" child)
            var iconObj = go1.transform.Find("Icon");
            if (iconObj != null)
                iconImage = iconObj.GetComponent<Image>();
            if (iconImage != null && iconSprite != null)
                iconImage.sprite = iconSprite;
        }

        SetHealthBarVisible(healthBarsVisible);
        UpdateVolume();
        UpdateStatsText();
    }

    private void UpdateVolume()
    {
        if (playerVolume != null)
        {
            float hpFraction = currentHealth / Mathf.Max(1f, maxHealth);
            playerVolume.weight = 1f - hpFraction;
        }

        if (filter != null)
        {
            float hpFraction = currentHealth / Mathf.Max(1f, maxHealth);
            float minCutoff = 200f;
            float maxCutoff = 22000f;
            filter.cutoffFrequency = Mathf.Lerp(minCutoff, maxCutoff, hpFraction);
        }
    }

    private void OnEnable()
    {
        if (_hasOriginalColor && spriteRenderer != null)
            spriteRenderer.color = _originalColor;

        SubscribeToStatusPopups();
    }

    private void OnDisable()
    {
        if (_hasOriginalColor && spriteRenderer != null)
            spriteRenderer.color = _originalColor;
        _flashRoutine = null;
        _invulnerabilityRoutine = null;
        isInvulnerable = false;
        UnsubscribeFromStatusPopups();
    }

    private void Update()
    {
        bool healthChanged = false;

        // Health Regeneration (does NOT grant temporary health)
        if (regenRate > 0f && currentHealth > 0f && currentHealth < maxHealth)
        {
            currentHealth = Mathf.Min(currentHealth + regenRate * Time.deltaTime, maxHealth);
            healthChanged = true;
        }

        // Temporary Health Decay Logic
        if (enableTemporaryHealth && currentTemporaryHealth > 0)
        {
            if (tempHealthDecayTimer > 0)
            {
                tempHealthDecayTimer -= Time.deltaTime;
            }
            else
            {
                currentTemporaryHealth -= tempHealthDecayRate * Time.deltaTime;
                if (currentTemporaryHealth < 0)
                {
                    currentTemporaryHealth = 0;
                }
                healthChanged = true;
            }
        }

        if (currentHealth <= 0 && (!enableTemporaryHealth || currentTemporaryHealth <= 0))
        {
            Die();
        }

        if (healthChanged)
        {
            SyncSlider();
            UpdateVolume();
            MarkStatsDirty();
        }

        bool shouldShowRunDamage = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (showRunDamageStats != shouldShowRunDamage)
        {
            showRunDamageStats = shouldShowRunDamage;
            MarkStatsDirty();
        }

        if (HasMovementStatsChanged())
            MarkStatsDirty();

        if (statsDirty)
            UpdateStatsText();
    }

    private void MarkStatsDirty()
    {
        statsDirty = true;
    }

    private bool HasMovementStatsChanged()
    {
        if (movementStatsText == null || movementController == null)
            return false;

        if (!hasMovementSnapshot ||
            !Mathf.Approximately(lastMoveSpeed, movementController.MoveSpeed) ||
            !Mathf.Approximately(lastDashSpeed, movementController.DashSpeed) ||
            !Mathf.Approximately(lastDashDuration, movementController.DashDuration) ||
            !Mathf.Approximately(lastDashCooldown, movementController.DashCooldown))
        {
            hasMovementSnapshot = true;
            lastMoveSpeed = movementController.MoveSpeed;
            lastDashSpeed = movementController.DashSpeed;
            lastDashDuration = movementController.DashDuration;
            lastDashCooldown = movementController.DashCooldown;
            return true;
        }

        return false;
    }

    public void UpdateStatsText()
    {
        statsDirty = false;
        float referenceDamage = Mathf.Max(1, lastDamageTaken);
        float effectiveArmor = EffectiveArmor;
        float effectiveEvasion = EffectiveEvasion;
        const string statColor = "#8888FF";
        const string healthColor = "#FF6666";
        const string currentHealthColor = "#80FF80";

        // HEALTH
        if (healthStatsText != null)
        {
            _statsBuilder.Clear();
            _statsBuilder.AppendLine($"<b><color={healthColor}>Health</color></b>");
            _statsBuilder.AppendLine($"Max Health: <color={healthColor}>{maxHealth}</color>");
            _statsBuilder.Append($"Current: <color={currentHealthColor}>{CurrentHealth}</color>");
            if (enableTemporaryHealth && currentTemporaryHealth > 0)
            {
                _statsBuilder.Append($" <color=#64C8FF>+{Mathf.RoundToInt(currentTemporaryHealth)}</color>");
            }
            _statsBuilder.AppendLine(); // New line
            _statsBuilder.AppendLine($"Regen: <color={currentHealthColor}>{regenRate:F2}</color>/s");
            healthStatsText.text = _statsBuilder.ToString();
        }

        // DEFENSE & RESISTANCES
        if (defenseStatsText != null)
        {
            float mitigation = (effectiveArmor > 0f && armorScaling > 0f)
                ? Mathf.Min(effectiveArmor / (effectiveArmor + armorScaling * referenceDamage), maxMitigation)
                : 0f;
            float evasionChance = (effectiveEvasion > 0f && evasionScaling > 0f)
                ? Mathf.Min(effectiveEvasion / (effectiveEvasion + evasionScaling * referenceDamage), maxEvasion)
                : 0f;

            _statsBuilder.Clear();
            if (showRunDamageStats)
            {
                AppendRunDamageStats(statColor);
            }
            else
            {
                _statsBuilder.AppendLine($"<b><color={statColor}>Defense</color></b>");
                _statsBuilder.AppendLine($"Armor: <color={statColor}>{Mathf.RoundToInt(effectiveArmor)}</color> (Mitigation: <color={statColor}>{mitigation * 100f:F1}%</color>)");
                _statsBuilder.AppendLine($"Evasion: <color={statColor}>{Mathf.RoundToInt(effectiveEvasion)}</color> (Chance: <color={statColor}>{evasionChance * 100f:F1}%</color>)");
                _statsBuilder.AppendLine($"Thorns: <color={statColor}>{thornsDamage}</color>");
                _statsBuilder.AppendLine($"<color=#FF6600>Fire Res: {fireResist * 100f:F0}%</color>");
                _statsBuilder.AppendLine($"<color=#4DB2FF>Cold Res: {coldResist * 100f:F0}%</color>");
                _statsBuilder.AppendLine($"<color=#FFFF4D>Lightning Res: {lightningResist * 100f:F0}%</color>");
                _statsBuilder.AppendLine($"<color=#80FF80>Poison Res: {poisonResist * 100f:F0}%</color>");
                _statsBuilder.AppendLine($"Last Hit: <color={statColor}>{lastDamageTaken}</color> ({ColorDamageType(lastDamageType)})");
            }
            defenseStatsText.text = _statsBuilder.ToString();
        }

        // MOVEMENT
        if (movementStatsText != null && movementController != null)
        {
            _statsBuilder.Clear();
            _statsBuilder.AppendLine($"<b><color={statColor}>Movement</color></b>");
            _statsBuilder.AppendLine($"Move Speed: <color={statColor}>{movementController.MoveSpeed:F2}</color>");
            _statsBuilder.AppendLine($"Dash Speed: <color={statColor}>{movementController.DashSpeed:F2}</color>");
            _statsBuilder.AppendLine($"Dash Duration: <color={statColor}>{movementController.DashDuration:F2}</color>s");
            _statsBuilder.AppendLine($"Dash Cooldown: <color={statColor}>{movementController.DashCooldown:F2}</color>s");
            movementStatsText.text = _statsBuilder.ToString();
        }
    }

    private static string ColorDamageType(DamageType type)
    {
        string hex = type switch
        {
            DamageType.Fire => "#FF6600",
            DamageType.Cold => "#4DB2FF",
            DamageType.Lightning => "#FFFF4D",
            DamageType.Poison => "#80FF80",
            _ => "#D9D9D9"
        };
        return $"<color={hex}>{type}</color>";
    }

    private void AppendRunDamageStats(string statColor)
    {
        _statsBuilder.AppendLine($"<b><color={statColor}>Run Damage</color></b>");
        _statsBuilder.AppendLine($"Total: <color={statColor}>{runDamageTakenTotal}</color>");

        foreach (DamageType type in DamageTypeOrder)
        {
            int amount = runDamageTakenByType[(int)type];
            _statsBuilder.AppendLine($"{ColorDamageType(type)}: <color={statColor}>{amount}</color>");
        }

        _statsBuilder.AppendLine($"Last Hit: <color={statColor}>{lastDamageTaken}</color> ({ColorDamageType(lastDamageType)})");
    }

    private void RegisterRunDamage(int amount, DamageType type)
    {
        if (amount <= 0) return;

        int index = (int)type;
        if (index < 0 || index >= runDamageTakenByType.Length) return;

        runDamageTakenByType[index] += amount;
        runDamageTakenTotal += amount;
    }

    private void ResetRunDamage()
    {
        for (int i = 0; i < runDamageTakenByType.Length; i++)
            runDamageTakenByType[i] = 0;

        runDamageTakenTotal = 0;
        lastDamageTaken = 0;
        lastDamageType = DamageType.Physical;
    }

    private void TryApplyAilments(StatusEffectSystem ses, DamageType type, int dmg, GameObject source)
    {
        if (ses == null || dmg <= 0) return;

        float dmgFrac = Mathf.Clamp01((float)dmg / Mathf.Max(1, maxHealth));
        int dotDamage = Mathf.Max(1, Mathf.RoundToInt(dmg * 0.20f));
        PlayerAccessoryStats sourceStats = PlayerAccessoryStats.Find(source != null ? source.transform : null);
        float statusChance = Mathf.Clamp01(dmgFrac + (sourceStats != null ? sourceStats.StatusApplicationChanceBonus : 0f));
        float durationMultiplier = sourceStats != null ? sourceStats.StatusDurationMultiplier : 1f;

        float roll = Random.value;

        switch (type)
        {
            case DamageType.Lightning:
                {
                    if (roll < statusChance)
                        ses.AddStatus(StatusEffectSystem.StatusType.Shock, 5f * durationMultiplier, 1f, source);
                    break;
                }
            case DamageType.Fire:
                {
                    if (roll < statusChance)
                    {
                        ses.AddStatus(StatusEffectSystem.StatusType.Ignite, 5f * durationMultiplier, 1f, source);
                        ses.igniteDamagePerTick = dotDamage;
                    }
                    break;
                }
            case DamageType.Cold:
                {
                    if (roll < statusChance)
                    {
                        ses.AddStatus(StatusEffectSystem.StatusType.Frozen, 3f * durationMultiplier, 1f, source);
                    }
                    break;
                }
            case DamageType.Poison:
                {
                    if (roll < statusChance)
                    {
                        ses.AddStatus(StatusEffectSystem.StatusType.Poison, 15f * durationMultiplier, 0.5f, source);
                        ses.poisonDamagePerTick = dotDamage;
                    }
                    break;
                }
            case DamageType.Physical:
                {
                    if (roll < statusChance)
                    {
                        ses.AddStatus(StatusEffectSystem.StatusType.Bleeding, 5f * durationMultiplier, 1f, source);
                        ses.bleedingDamagePerTick = dotDamage;
                    }
                    break;
                }
        }
    }

    // BACK-COMPAT: original signature forwards to Physical damage type
    public void TakeDamage(int amount, bool mitigatable = true)
    {
        TakeDamage(amount, DamageType.Physical, mitigatable);
    }

    public void TakeContactDamage(int amount)
    {
        TakeDamage(amount, DamageType.Physical, true, true, null, "Contact");
    }

    // NEW: main overload with type
    public void TakeDamage(
        int amount,
        DamageType type = DamageType.Physical,
        bool mitigatable = true,
        bool applyAilments = true,
        GameObject sourceObject = null,
        string sourceDetail = null,
        bool isCritical = false)
    {
        if (amount <= 0 || IsProtectedBySafeZone() || isInvulnerable || !IsAlive) return;

        float incomingDamage = amount;
        if (IsContactDamage(sourceObject, sourceDetail))
        {
            PlayerAccessoryStats stats = GetAccessoryStats();
            if (stats != null) incomingDamage *= stats.ContactDamageMultiplier;
        }

        // Absorb damage with temporary health first
        if (enableTemporaryHealth && currentTemporaryHealth > 0)
        {
            float damageAbsorbedByTemp = Mathf.Min(incomingDamage, currentTemporaryHealth);
            currentTemporaryHealth -= damageAbsorbedByTemp;
            incomingDamage -= damageAbsorbedByTemp;

            if (incomingDamage <= 0)
            {
                SyncSlider();
                MarkStatsDirty();
                // Optional: You could show a different colored damage popup for absorbed damage here.
                return;
            }
        }

        if (currentHealth <= 0)
        {
            SyncSlider();
            MarkStatsDirty();
            if (!IsAlive) Die();
            return;
        }

        float dmg = incomingDamage;
        int armorMitigatedAmount = 0;

        if (mitigatable)
        {
            // Evasion check BEFORE armor/resistance
            if (TryEvade(incomingDamage))
            {
                lastDamageTaken = 0;
                lastDamageType = type;
                MarkStatsDirty();

                // Show "Dodged" popup
                if (damagePopupPrefab != null)
                {
                    GameObject popup = Instantiate(damagePopupPrefab, transform);
                    popup.transform.localPosition = popupOffset;
                    var tmpUI = popup.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmpUI != null)
                    {
                        tmpUI.color = Color.white;
                        tmpUI.text = "Dodged";
                    }
                }
                RaiseDamageTaken(0, type, sourceObject, sourceDetail, 0, Mathf.RoundToInt(incomingDamage), mitigatable);
                return;
            }

            if (type == DamageType.Physical)
            {
                // Armor (small-hit mitigation) first
                float damageBeforeArmor = dmg;
                dmg = ApplyArmor(dmg);
                armorMitigatedAmount = Mathf.Max(0, Mathf.RoundToInt(damageBeforeArmor - dmg));
            }

            // Then elemental/type resistance
            dmg = ApplyResistance(dmg, type);
        }

        if (dmg <= 0) return;

        // ailments
        if (_statusEffectSystem != null)
            dmg *= _statusEffectSystem.IncomingDamageMultiplier;

        int displayedDamage = Mathf.Max(1, Mathf.RoundToInt(dmg));
        lastDamageTaken = displayedDamage;
        lastDamageType = type;
        _dpsChecker?.RegisterDamage(displayedDamage);

        if (_statusEffectSystem != null && applyAilments)
            TryApplyAilments(_statusEffectSystem, type, displayedDamage, sourceObject);

        RegisterRunDamage(displayedDamage, type);
        currentHealth = Mathf.Clamp(currentHealth - dmg, 0, maxHealth);
        RaiseDamageTaken(displayedDamage, type, sourceObject, sourceDetail, armorMitigatedAmount, 0, mitigatable);
        ApplyThorns();
        SyncSlider();
        UpdateVolume();

        // Damage popup (accumulate if an existing one is active) per damage type
        if (damagePopupPrefab != null)
        {
            Color popupColor = GetDamageColor(type);

            _activeDamagePopups.TryGetValue(type, out var activeText);
            if (activeText != null)
            {
                DamagePopup2D damagePopup = activeText.GetComponentInParent<DamagePopup2D>();
                if (damagePopup != null)
                {
                    damagePopup.AddDamage(displayedDamage, popupColor, isCritical);
                }
                else
                {
                    bool containsCriticalHit = isCritical || activeText.text.EndsWith("!");
                    string currentText = activeText.text.TrimEnd('!');
                    if (!int.TryParse(currentText, out int currentVal)) currentVal = 0;
                    currentVal += displayedDamage;
                    activeText.text = containsCriticalHit ? $"{currentVal}!" : currentVal.ToString();
                    activeText.color = popupColor;
                }
            }
            else
            {
                GameObject popup = Instantiate(damagePopupPrefab, transform);
                popup.transform.localPosition = popupOffset;
                TMP_Text tmp = null;
                if (!popup.TryGetComponent<TMP_Text>(out tmp))
                {
                    tmp = popup.GetComponentInChildren<TMP_Text>();
                }
                if (tmp == null)
                {
                    if (popup.TryGetComponent<TextMeshPro>(out var tmpWorld)) tmp = tmpWorld;
                    else if (popup.TryGetComponent<TextMeshProUGUI>(out var tmpUI)) tmp = tmpUI;
                }
                if (tmp != null)
                {
                    DamagePopup2D damagePopup = tmp.GetComponentInParent<DamagePopup2D>();
                    if (damagePopup != null)
                        damagePopup.SetDamage(displayedDamage, popupColor, isCritical);
                    else
                    {
                        tmp.text = isCritical ? $"{displayedDamage}!" : displayedDamage.ToString();
                        tmp.color = popupColor;
                    }
                    _activeDamagePopups[type] = tmp;
                }
            }
        }

        if (bloodSFX != null)
        {
            Quaternion randomRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            Instantiate(bloodSFX, transform.position, randomRotation);
        }

        if (soundSource != null && damageClip != null && damageClip.Length > 0)
        {
            soundSource.pitch = Random.Range(0.9f, 1.1f);
            soundSource.PlayOneShot(damageClip[Random.Range(0, damageClip.Length)]);
            soundSource.pitch = 1f;
        }

        if (spriteRenderer != null)
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRedCoroutine());
        }

        MarkStatsDirty();

        if (!IsAlive) Die();
        else if (invulnerabilityDuration > 0) StartInvulnerability(invulnerabilityDuration);
    }

    private bool IsProtectedBySafeZone()
    {
        if (!CompareTag("Player"))
            return false;

        if (safeZoneStatus == null)
            TryGetComponent(out safeZoneStatus);

        return safeZoneStatus != null && safeZoneStatus.IsSafeZoneActive;
    }

    private void SubscribeToStatusPopups()
    {
        if (_subscribedToStatusPopups)
            return;

        if (_statusEffectSystem == null)
            _statusEffectSystem = GetComponent<StatusEffectSystem>();

        if (_statusEffectSystem == null)
            return;

        _statusEffectSystem.OnStart += HandleStatusEffectStarted;
        _subscribedToStatusPopups = true;
    }

    private void UnsubscribeFromStatusPopups()
    {
        if (!_subscribedToStatusPopups || _statusEffectSystem == null)
            return;

        _statusEffectSystem.OnStart -= HandleStatusEffectStarted;
        _subscribedToStatusPopups = false;
    }

    private void HandleStatusEffectStarted(StatusEffectSystem.StatusType statusType)
    {
        if (!showStatusAfflictionPopups || damagePopupPrefab == null)
            return;

        if (!DamagePopup2D.TryGetAfflictionPopup(statusType, out _, out _))
            return;

        GameObject popup = Instantiate(damagePopupPrefab, transform);
        popup.transform.localPosition = popupOffset + afflictionPopupExtraOffset;

        if (popup.TryGetComponent(out DamagePopup2D damagePopup))
        {
            damagePopup.SetStatusAffliction(statusType);
            return;
        }

        DamagePopup2D childPopup = popup.GetComponentInChildren<DamagePopup2D>();
        if (childPopup != null)
            childPopup.SetStatusAffliction(statusType);
    }

    private void ApplyThorns()
    {
        if (thornsDamage <= 0 || !CompareTag("Player")) return;

        EnemyChaser nearest = null;
        float nearestSqr = float.PositiveInfinity;

        if (Time.time >= nextThornsTargetRefreshTime || cachedThornsTargets.Length == 0)
        {
            cachedThornsTargets = FindObjectsByType<EnemyChaser>();
            nextThornsTargetRefreshTime = Time.time + ThornsTargetRefreshInterval;
        }

        foreach (var enemy in cachedThornsTargets)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            if (!enemy.TryGetComponent(out SimpleHealth enemyHealth) || !enemyHealth.IsAlive) continue;

            float sqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = enemy;
            }
        }

        if (nearest != null && nearest.TryGetComponent(out SimpleHealth thornTargetHealth))
            thornTargetHealth.TakeDamage(thornsDamage, DamageType.Physical, false, false, gameObject, "Thorns");
    }

    private void RaiseDamageTaken(
        int amount,
        DamageType type,
        GameObject sourceObject,
        string sourceDetail,
        int armorMitigatedAmount = 0,
        int evasionDodgedAmount = 0,
        bool wasMitigatable = false)
    {
        DamageReportEntry entry = new DamageReportEntry(
            this,
            amount,
            type,
            sourceObject,
            ResolveDamageSourceName(sourceObject),
            string.IsNullOrWhiteSpace(sourceDetail) ? type.ToString() : sourceDetail,
            currentHealth,
            !IsAlive,
            armorMitigatedAmount,
            evasionDodgedAmount,
            wasMitigatable);

        DamageTaken?.Invoke(entry);
        AnyDamageTaken?.Invoke(entry);
    }

    public static string ResolveDamageSourceName(GameObject sourceObject)
    {
        if (sourceObject == null)
            return "Unknown";

        ChatterStats chatterStats = sourceObject.GetComponentInParent<ChatterStats>();
        if (chatterStats == null && sourceObject.transform.root != null)
            chatterStats = sourceObject.transform.root.GetComponentInChildren<ChatterStats>();

        if (chatterStats != null)
        {
            string chatterName = chatterStats.transform.name;
            if (!string.IsNullOrWhiteSpace(chatterName))
                return chatterName;
        }

        SimpleHealth sourceHealth = sourceObject.GetComponentInParent<SimpleHealth>();
        if (sourceHealth != null && !string.IsNullOrWhiteSpace(sourceHealth.name))
            return sourceHealth.name;

        return string.IsNullOrWhiteSpace(sourceObject.name) ? "Unknown" : sourceObject.name;
    }

    private bool TryEvade(float rawDamage)
    {
        float effectiveEvasion = EffectiveEvasion;
        if (rawDamage <= 0 || effectiveEvasion <= 0f || evasionScaling <= 0f) return false;
        float chance = effectiveEvasion / (effectiveEvasion + evasionScaling * rawDamage);
        chance = Mathf.Min(chance, maxEvasion);
        return Random.value < chance;
    }

    private float ApplyArmor(float rawDamage)
    {
        float effectiveArmor = EffectiveArmor;
        if (rawDamage <= 0 || effectiveArmor <= 0f || armorScaling <= 0f) return rawDamage;
        float m = effectiveArmor / (effectiveArmor + armorScaling * rawDamage);
        if (maxMitigation > 0f) m = Mathf.Min(m, maxMitigation);
        return Mathf.Max(0f, rawDamage * (1f - m));
    }

    // per-type resistance after armor
    private float ApplyResistance(float rawDamage, DamageType type)
    {
        if (rawDamage <= 0) return 0f;

        float resist = 0f;
        switch (type)
        {
            case DamageType.Fire: resist = fireResist; break;
            case DamageType.Cold: resist = coldResist; break;
            case DamageType.Lightning: resist = lightningResist; break;
            case DamageType.Poison: resist = poisonResist; break;
        }

        resist = Mathf.Clamp(resist, 0f, 0.95f);
        return Mathf.Max(0f, rawDamage * (1f - resist));
    }

    // Map damage types to popup colors
    private Color GetDamageColor(DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire:
                return new Color(1f, 0.4f, 0f);        // orange-red
            case DamageType.Cold:
                return new Color(0.3f, 0.7f, 1f);      // icy blue
            case DamageType.Lightning:
                return new Color(1f, 1f, 0.3f);        // yellow
            case DamageType.Poison:
                return new Color(0.5f, 1f, 0.5f);      // green
            case DamageType.Physical:
            default:
                return Color.white;
        }
    }

    private System.Collections.IEnumerator FlashRedCoroutine()
    {
        if (spriteRenderer == null) yield break;
        var c = spriteRenderer.color;
        var target = new Color(hitColor.r, hitColor.g, hitColor.b, c.a);
        spriteRenderer.color = target;
        yield return new WaitForSecondsRealtime(hitFlashDuration);
        if (_hasOriginalColor) spriteRenderer.color = _originalColor;
        _flashRoutine = null;
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || currentHealth <= 0) return;
        amount = GetModifiedHealingAmount(amount);
        if (amount <= 0) return;

        if (enableTemporaryHealth)
        {
            float oldHealth = currentHealth;
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

            float healedAmount = currentHealth - oldHealth;
            float overHeal = amount - healedAmount;

            if (overHeal > 0)
            {
                currentTemporaryHealth += overHeal;
                tempHealthDecayTimer = tempHealthDecayDelay;
            }
        }
        else
        {
            // If feature is disabled, just heal to max and stop.
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        }

        SyncSlider();
        UpdateVolume();
        UpdateStatsText();
    }

    private int GetModifiedHealingAmount(int amount)
    {
        float multiplier = _statusEffectSystem != null ? _statusEffectSystem.HealingReceivedMultiplier : 1f;
        PlayerAccessoryStats stats = GetAccessoryStats();
        if (stats != null) multiplier *= stats.HealingReceivedMultiplier;
        if (multiplier <= 0f)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));
    }

    private PlayerAccessoryStats GetAccessoryStats()
    {
        if (_accessoryStats == null)
            _accessoryStats = PlayerAccessoryStats.Find(transform);
        return _accessoryStats;
    }

    private static bool IsContactDamage(GameObject sourceObject, string sourceDetail)
    {
        if (!string.IsNullOrWhiteSpace(sourceDetail))
        {
            if (sourceDetail.IndexOf("contact", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (sourceDetail.IndexOf("projectile", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceDetail.IndexOf("explosion", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceDetail.IndexOf("bleeding", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceDetail.IndexOf("ignite", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceDetail.IndexOf("poison", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceDetail.IndexOf("cull", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
        }

        return sourceObject != null && sourceObject.GetComponentInParent<EnemyChaser>() != null;
    }

    public void Kill()
    {
        if (hasDied || !IsAlive) return;
        currentHealth = 0;
        if (enableTemporaryHealth) currentTemporaryHealth = 0;
        SyncSlider();
        UpdateVolume();
        UpdateStatsText();
        Die();
    }

    public void ResetHealth()
    {
        hasDied = false;
        isInvulnerable = false;
        if (_invulnerabilityRoutine != null)
        {
            StopCoroutine(_invulnerabilityRoutine);
            _invulnerabilityRoutine = null;
        }
        currentHealth = maxHealth;
        if (enableTemporaryHealth) currentTemporaryHealth = 0;
        tempHealthDecayTimer = 0f;
        _activeDamagePopups.Clear();
        ResetRunDamage();
        SyncSlider();
        UpdateVolume();
        UpdateStatsText();
        HealthReset?.Invoke(this);
    }

    private void Die()
    {
        if (hasDied) return;
        hasDied = true;
        AnyDied?.Invoke(this);
        Died?.Invoke(this);

        if (deathObjects != null && deathObjects.Length > 0)
        {
            foreach (var obj in deathObjects)
            {
                if (obj != null)
                {
                    Instantiate(obj, transform.position, Quaternion.identity);
                }
            }
        }

        if (deathClip != null && deathClip.Length > 0)
        {
            GameObject tempAudio = new GameObject("DeathSound");
            var tempSource = tempAudio.AddComponent<AudioSource>();
            var deathClipSelected = deathClip[Random.Range(0, deathClip.Length)];
            tempSource.clip = deathClipSelected;
            tempSource.Play();
            Object.Destroy(tempAudio, deathClipSelected.length);
        }

        if (loot != null)
        {
            try { loot.RollAndSpawn(); }
            catch (System.Exception e) { Debug.LogWarning($"[SimpleHealth] Loot roll failed on {name}: {e.Message}"); }
        }

        if (!gameObject.CompareTag("Player"))
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void SyncSlider()
    {
        if (healthSlider != null)
        {
            healthSlider.minValue = 0;
            healthSlider.maxValue = maxHealth;
            healthSlider.value = Mathf.Clamp(currentHealth, 0, healthSlider.maxValue);
        }

        if (healthText != null)
        {
            if (enableTemporaryHealth && currentTemporaryHealth > 0)
            {
                healthText.text = $"{Mathf.RoundToInt(currentHealth)}<color=#64C8FF>+{Mathf.RoundToInt(currentTemporaryHealth)}</color>/{maxHealth}";
            }
            else
            {
                healthText.text = $"{Mathf.RoundToInt(currentHealth)}/{maxHealth}";
            }
        }
    }

    public void SetHealthBarVisible(bool visible)
    {
        CaptureHealthBarInitialState();

        if (healthSlider != null)
            healthSlider.gameObject.SetActive(visible && healthSliderInitialActive);

        if (healthText != null)
            healthText.gameObject.SetActive(visible && healthTextInitialActive);
    }

    private void CaptureHealthBarInitialState()
    {
        if (capturedHealthBarInitialState)
            return;

        healthSliderInitialActive = healthSlider != null && healthSlider.gameObject.activeSelf;
        healthTextInitialActive = healthText != null && healthText.gameObject.activeSelf;
        capturedHealthBarInitialState = true;
    }

    public static int SetAllHealthBarsVisible(bool visible)
    {
        healthBarsVisible = visible;
        SimpleHealth[] healthSystems = FindObjectsByType<SimpleHealth>();

        foreach (SimpleHealth health in healthSystems)
        {
            if (health != null)
                health.SetHealthBarVisible(visible);
        }

        return healthSystems.Length;
    }

    public void IncreaseMaxHealth(int amount)
    {
        maxHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        SyncSlider();
        UpdateVolume();
        UpdateStatsText();
    }

    public void GiveArmor(float amount)
    {
        if (amount == 0f) return;
        armor = Mathf.Max(0f, armor + amount);
        UpdateStatsText();
    }

    public void GiveEvasion(float amount)
    {
        if (amount == 0f) return;
        evasion = Mathf.Max(0f, evasion + amount);
        UpdateStatsText();
    }

    public float EffectiveArmor => armor * (1f + GetDefenseIncrease(useArmor: true));
    public float EffectiveEvasion => evasion * (1f + GetDefenseIncrease(useArmor: false));

    public void RegisterDefenseIncreaseProvider(IPlayerDefenseIncreaseProvider provider)
    {
        if (provider == null || defenseIncreaseProviders.Contains(provider)) return;
        defenseIncreaseProviders.Add(provider);
        MarkStatsDirty();
    }

    public void UnregisterDefenseIncreaseProvider(IPlayerDefenseIncreaseProvider provider)
    {
        if (provider == null) return;
        defenseIncreaseProviders.Remove(provider);
        MarkStatsDirty();
    }

    public void NotifyDefenseModifiersChanged()
    {
        MarkStatsDirty();
    }

    private float GetDefenseIncrease(bool useArmor)
    {
        float increase = 0f;
        for (int i = defenseIncreaseProviders.Count - 1; i >= 0; i--)
        {
            IPlayerDefenseIncreaseProvider provider = defenseIncreaseProviders[i];
            if (provider is Object unityObject && unityObject == null)
            {
                defenseIncreaseProviders.RemoveAt(i);
                continue;
            }

            if (provider is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                continue;

            increase += Mathf.Max(0f, useArmor ? provider.ArmorIncrease : provider.EvasionIncrease);
        }
        return increase;
    }

    public void GiveThorns(float amount)
    {
        int roundedAmount = Mathf.RoundToInt(amount);
        if (roundedAmount == 0) return;
        thornsDamage = Mathf.Max(0, thornsDamage + roundedAmount);
        UpdateStatsText();
    }

    // OPTIONAL HELPERS: adjust resistances at runtime
    public void GiveResistance(DamageType type, float amount)
    {
        if (Mathf.Approximately(amount, 0f)) return;
        switch (type)
        {
            case DamageType.Fire: fireResist = Mathf.Clamp(fireResist + amount, 0f, 0.95f); break;
            case DamageType.Cold: coldResist = Mathf.Clamp(coldResist + amount, 0f, 0.95f); break;
            case DamageType.Lightning: lightningResist = Mathf.Clamp(lightningResist + amount, 0f, 0.95f); break;
            case DamageType.Poison: poisonResist = Mathf.Clamp(poisonResist + amount, 0f, 0.95f); break;
        }
        UpdateStatsText();
    }

    #region Public Unity Event Helpers

    // --- Health ---
    public void SetMaxHealth(int value)
    {
        maxHealth = Mathf.Max(1, value);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        SyncSlider();
        UpdateVolume();
        UpdateStatsText();
    }

    public void AddMaxHealth(int amount)
    {
        IncreaseMaxHealth(amount);
    }

    public void SetHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        if (currentHealth > 0f) hasDied = false;
        SyncSlider();
        UpdateVolume();
        UpdateStatsText();
    }

    public void HealByPercentage(float percentage)
    {
        if (percentage <= 0) return;
        int amountToHeal = Mathf.RoundToInt(maxHealth * percentage / 100f);
        Heal(amountToHeal);
    }

    public void TakeDamageByPercentage(float percentage)
    {
        if (percentage <= 0) return;
        int amountToTake = Mathf.RoundToInt(maxHealth * percentage / 100f);
        TakeDamage(amountToTake, DamageType.Physical, false); // Percentage damage is unmitigatable
    }

    public void SetHealthByPercentage(float percentage)
    {
        currentHealth = Mathf.Clamp(maxHealth * percentage / 100f, 0, maxHealth);
        if (currentHealth > 0f) hasDied = false;
        SyncSlider();
        UpdateVolume();
        UpdateStatsText();
    }

    // --- Regeneration ---
    public void AddRegen(float amount)
    {
        regenRate += amount;
        UpdateStatsText();
    }

    public void SetRegen(float value)
    {
        regenRate = value;
        UpdateStatsText();
    }

    // --- Defense ---
    public void SetArmor(float value)
    {
        armor = Mathf.Max(0f, value);
        UpdateStatsText();
    }

    public void SetEvasion(float value)
    {
        evasion = Mathf.Max(0f, value);
        UpdateStatsText();
    }

    public void AddThorns(float amount) => GiveThorns(amount);

    public void SetThorns(int value)
    {
        thornsDamage = Mathf.Max(0, value);
        UpdateStatsText();
    }

    // --- Resistances ---
    public void AddFireResist(float amount) => GiveResistance(DamageType.Fire, amount);
    public void SetFireResist(float value) { fireResist = Mathf.Clamp(value, 0f, 0.95f); UpdateStatsText(); }
    public void AddColdResist(float amount) => GiveResistance(DamageType.Cold, amount);
    public void SetColdResist(float value) { coldResist = Mathf.Clamp(value, 0f, 0.95f); UpdateStatsText(); }
    public void AddLightningResist(float amount) => GiveResistance(DamageType.Lightning, amount);
    public void SetLightningResist(float value) { lightningResist = Mathf.Clamp(value, 0f, 0.95f); UpdateStatsText(); }
    public void AddPoisonResist(float amount) => GiveResistance(DamageType.Poison, amount);
    public void SetPoisonResist(float value) { poisonResist = Mathf.Clamp(value, 0f, 0.95f); UpdateStatsText(); }

    // --- Invulnerability ---
    public void SetInvulnerable(float duration)
    {
        if (duration > 0)
        {
            StartInvulnerability(duration);
        }
    }

    private void StartInvulnerability(float duration)
    {
        if (_invulnerabilityRoutine != null)
            StopCoroutine(_invulnerabilityRoutine);

        _invulnerabilityRoutine = StartCoroutine(InvulnerabilityCoroutine(duration));
    }

    private System.Collections.IEnumerator InvulnerabilityCoroutine(float duration)
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(duration);
        isInvulnerable = false;
        _invulnerabilityRoutine = null;
    }

    #endregion

}

public interface IPlayerDefenseIncreaseProvider
{
    float ArmorIncrease { get; }
    float EvasionIncrease { get; }
}





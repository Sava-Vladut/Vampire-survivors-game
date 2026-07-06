using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using System.Collections.Generic;
public class SimpleHealth : MonoBehaviour
{
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

    [BoxGroup("Health")][SerializeField] public int maxHealth = 100;
    [BoxGroup("Health")]
    [Tooltip("If <=0, starts at maxHealth.")]
    [SerializeField] private int startingHealth = 100;

    [BoxGroup("Invulnerability")]
    [Tooltip("Seconds of invulnerability after taking damage.")]
    [SerializeField] private float invulnerabilityDuration = 1f;

    [BoxGroup("Regeneration")]
    [Tooltip("Health regenerated per second. Can be fractional. Does not grant temporary health.")]
    [SerializeField] public float regenRate = 0f;

    [BoxGroup("Temporary Health")]
    [Tooltip("If true, healing past max health grants a temporary, decaying health shield.")]
    [SerializeField] private bool enableTemporaryHealth = true;
    [BoxGroup("Temporary Health")]
    [Tooltip("How fast temporary health depletes per second.")]
    [SerializeField] private float tempHealthDecayRate = 5f;
    [BoxGroup("Temporary Health")]
    [Tooltip("How long to wait after gaining temp health before it starts decaying.")]
    [SerializeField] private float tempHealthDecayDelay = 1.5f;

    [BoxGroup("Armor (Small-hit mitigation)")]
    [Tooltip("Flat armor rating. More armor = more mitigation on small hits.")]
    [SerializeField] public float armor = 0f;
    [BoxGroup("Armor (Small-hit mitigation)")]
    [Tooltip("How quickly mitigation falls off as the hit gets bigger. Higher = big hits bypass sooner.")]
    [SerializeField] private float armorScaling = 10f;
    [BoxGroup("Armor (Small-hit mitigation)")]
    [Tooltip("Cap the maximum mitigation fraction (0..0.95). 0.8 = up to 80% reduction on tiny hits.")]
    [Range(0f, 0.95f)][SerializeField] private float maxMitigation = 0.8f;

    [BoxGroup("Evasion (Chance to completely dodge small hits)")]
    [SerializeField] public float evasion = 0f;
    [BoxGroup("Evasion (Chance to completely dodge small hits)")]
    [SerializeField] private float evasionScaling = 10f;
    [BoxGroup("Evasion (Chance to completely dodge small hits)")]
    [SerializeField, Range(0f, 0.95f)] private float maxEvasion = 0.8f;

    [BoxGroup("Resistances (% damage reduced AFTER armor)")]
    [Tooltip("0..0.95 fraction of damage reduced for each type.")]
    [Range(0f, 0.95f)] public float fireResist = 0f;
    [BoxGroup("Resistances (% damage reduced AFTER armor)")]
    [Range(0f, 0.95f)] public float coldResist = 0f;
    [BoxGroup("Resistances (% damage reduced AFTER armor)")]
    [Range(0f, 0.95f)] public float lightningResist = 0f;
    [BoxGroup("Resistances (% damage reduced AFTER armor)")]
    [Range(0f, 0.95f)] public float poisonResist = 0f;

    [BoxGroup("UI")]
    [Tooltip("Optional slider to show current health.")]
    [SerializeField] public Slider healthSlider;
    [BoxGroup("UI")][SerializeField] public TextMeshProUGUI healthText;

    [BoxGroup("Stats Display")]
    [Tooltip("Prefab root GameObject that contains a TextMeshProUGUI somewhere in its children.")]
    [SerializeField] private GameObject statsTextPrefab;
    [BoxGroup("Stats Display")][SerializeField] private Transform uiParent;
    [BoxGroup("Stats Display")][TextArea][SerializeField] public string extraTextField;
    [BoxGroup("Stats Display")]
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] private Sprite iconSprite;

    [BoxGroup("SFX")][SerializeField] private Volume playerVolume;
    [BoxGroup("SFX")][SerializeField] private GameObject[] deathObjects;
    [BoxGroup("SFX")][SerializeField] private AudioClip[] damageClip;
    [BoxGroup("SFX")][SerializeField] private AudioClip[] deathClip;
    [BoxGroup("SFX")][SerializeField] private GameObject bloodSFX;

    [BoxGroup("Loot")]
    [Tooltip("Weighted loot table. On death we roll once and spawn the result (if any).")]
    [SerializeField] private LootTable2D loot;

    [BoxGroup("Hit Flash")][SerializeField] private SpriteRenderer spriteRenderer;
    [BoxGroup("Hit Flash")][SerializeField] private Color hitColor = new Color(1f, 0.5f, 0.5f, 1f);
    [BoxGroup("Hit Flash")][SerializeField] private float hitFlashDuration = 0.1f;

    [BoxGroup("Damage Popup")]
    [Tooltip("Prefab with a TextMeshPro or TextMeshProUGUI to display damage taken.")]
    [SerializeField] private GameObject damagePopupPrefab;
    [BoxGroup("Damage Popup")]
    [Tooltip("Offset from entity position when spawning damage popup.")]
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 1f, 0f);

    private AudioSource soundSource;
    [HideInInspector] public float currentHealth;
    [HideInInspector] public float currentTemporaryHealth;
    private float tempHealthDecayTimer;
    private bool isInvulnerable;

    private Color _originalColor;
    private bool _hasOriginalColor;
    private Coroutine _flashRoutine;
    private int lastDamageTaken = 0;
    private DamageType lastDamageType = DamageType.Physical; // remember last type
    private readonly int[] runDamageTakenByType = new int[DamageTypeOrder.Length];
    private int runDamageTakenTotal = 0;
    private Snappy2DController movementController;
    private readonly System.Text.StringBuilder _statsBuilder = new System.Text.StringBuilder(256);

    // Cached components for performance
    private DPSChecker _dpsChecker;
    private StatusEffectSystem _statusEffectSystem;
    private OrthoScrollZoom _orthoScrollZoom;

    // Stats UI (matches Knife.cs pattern)
    [HideInInspector] public TextMeshProUGUI healthStatsText;
    [HideInInspector] public TextMeshProUGUI defenseStatsText;
    [HideInInspector] public TextMeshProUGUI movementStatsText;

    private Image iconImage;
    private AudioLowPassFilter filter;
    // Cache the currently active damage popup's text to accumulate values
    // Active damage popups per damage type for accumulation
    private readonly Dictionary<DamageType, TMP_Text> _activeDamagePopups = new Dictionary<DamageType, TMP_Text>();

    public bool IsAlive => currentHealth > 0f || (enableTemporaryHealth && currentTemporaryHealth > 0f);
    public bool IsInvulnerable => isInvulnerable;
    public int CurrentHealth => Mathf.RoundToInt(currentHealth);
    public int MaxHealth => maxHealth;
    public int thornsDamage;

    private void Awake()
    {
        if (startingHealth <= 0) startingHealth = maxHealth;
        currentHealth = Mathf.Clamp(startingHealth, 0, maxHealth);
        SyncSlider();

        // Cache components
        filter = GetComponent<AudioLowPassFilter>();
        movementController = GetComponent<Snappy2DController>();
        soundSource = GetComponent<AudioSource>();
        _dpsChecker = GetComponent<DPSChecker>();
        _statusEffectSystem = GetComponent<StatusEffectSystem>();
        _orthoScrollZoom = FindAnyObjectByType<OrthoScrollZoom>();

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
    }

    private void Start()
    {
        ResetHealth();
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
    }

    private void OnDisable()
    {
        if (_hasOriginalColor && spriteRenderer != null)
            spriteRenderer.color = _originalColor;
        _flashRoutine = null;
    }

    private void Update()
    {
        // Health Regeneration (does NOT grant temporary health)
        if (regenRate > 0f && currentHealth > 0f && currentHealth < maxHealth)
        {
            currentHealth = Mathf.Min(currentHealth + regenRate * Time.deltaTime, maxHealth);
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
            }
        }

        if (currentHealth <= 0 && (!enableTemporaryHealth || currentTemporaryHealth <= 0))
        {
            Die();
        }

        UpdateVolume();
        UpdateStatsText();
        SyncSlider(); // Sync slider every frame to show decay
    }

    public void UpdateStatsText()
    {
        float referenceDamage = Mathf.Max(1, lastDamageTaken);
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
            float mitigation = (armor > 0f && armorScaling > 0f)
                ? Mathf.Min(armor / (armor + armorScaling * referenceDamage), maxMitigation)
                : 0f;
            float evasionChance = (evasion > 0f && evasionScaling > 0f)
                ? Mathf.Min(evasion / (evasion + evasionScaling * referenceDamage), maxEvasion)
                : 0f;

            _statsBuilder.Clear();
            bool showRunDamage = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (showRunDamage)
            {
                AppendRunDamageStats(statColor);
            }
            else
            {
                _statsBuilder.AppendLine($"<b><color={statColor}>Defense</color></b>");
                _statsBuilder.AppendLine($"Armor: <color={statColor}>{(int)armor}</color> (Mitigation: <color={statColor}>{mitigation * 100f:F1}%</color>)");
                _statsBuilder.AppendLine($"Evasion: <color={statColor}>{(int)evasion}</color> (Chance: <color={statColor}>{evasionChance * 100f:F1}%</color>)");
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

    private void TryApplyAilments(StatusEffectSystem ses, DamageType type, int dmg)
    {
        if (ses == null || dmg <= 0) return;

        float dmgFrac = Mathf.Clamp01((float)dmg / Mathf.Max(1, maxHealth));
        int dotDamage = Mathf.Max(1, Mathf.RoundToInt(dmg * 0.20f));

        const float shockMult = 1;
        const float igniteMult = 1;
        const float poisonMult = 1;
        const float bleedMult = 1;

        float roll = Random.value;

        switch (type)
        {
            case DamageType.Lightning:
                {
                    float chance = Mathf.Clamp01(dmgFrac * shockMult);
                    if (roll < chance)
                        ses.AddStatus(StatusEffectSystem.StatusType.Shock, 5f, 1f);
                    break;
                }
            case DamageType.Fire:
                {
                    float chance = Mathf.Clamp01(dmgFrac * igniteMult);
                    if (roll < chance)
                    {
                        ses.AddStatus(StatusEffectSystem.StatusType.Ignite, 5f, 1f);
                        ses.igniteDamagePerTick = dotDamage;
                    }
                    break;
                }
            case DamageType.Cold:
                {
                    float chance = Mathf.Clamp01(dmgFrac * igniteMult);
                    if (roll < chance)
                    {
                        ses.AddStatus(StatusEffectSystem.StatusType.Frozen, 3f, 1f);
                    }
                    break;
                }
            case DamageType.Poison:
                {
                    float chance = Mathf.Clamp01(dmgFrac * poisonMult);
                    if (roll < chance)
                    {
                        ses.AddStatus(StatusEffectSystem.StatusType.Poison, 15f, 0.5f);
                        ses.poisonDamagePerTick = 1;
                    }
                    break;
                }
            case DamageType.Physical:
                {
                    float chance = Mathf.Clamp01(dmgFrac * bleedMult);
                    if (roll < chance)
                    {
                        ses.AddStatus(StatusEffectSystem.StatusType.Bleeding, 5f, 1f);
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

    // NEW: main overload with type
    public void TakeDamage(int amount, DamageType type = DamageType.Physical, bool mitigatable = true, bool applyAilments = true)
    {
        if (amount <= 0 || isInvulnerable || !IsAlive) return;

        int incomingDamage = amount;

        // Absorb damage with temporary health first
        if (enableTemporaryHealth && currentTemporaryHealth > 0)
        {
            float damageAbsorbedByTemp = Mathf.Min(incomingDamage, currentTemporaryHealth);
            currentTemporaryHealth -= damageAbsorbedByTemp;
            incomingDamage -= (int)damageAbsorbedByTemp;

            if (incomingDamage <= 0)
            {
                SyncSlider();
                UpdateStatsText();
                // Optional: You could show a different colored damage popup for absorbed damage here.
                return;
            }
        }

        if (currentHealth <= 0) return; // Don't continue if only temp health was left

        int dmg = incomingDamage;

        if (mitigatable)
        {
            // Evasion check BEFORE armor/resistance
            if (TryEvade(amount))
            {
                lastDamageTaken = 0;
                lastDamageType = type;

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
                return;
            }

            if (type == DamageType.Physical)
            {
                // Armor (small-hit mitigation) first
                dmg = ApplyArmor(dmg);
            }

            // Then elemental/type resistance
            dmg = ApplyResistance(dmg, type);
        }

        if (dmg <= 0) return;

        lastDamageTaken = dmg;
        lastDamageType = type;
        _dpsChecker?.RegisterDamage(dmg);

        // ailments
        if (_statusEffectSystem != null)
        {
            if (_statusEffectSystem.HasStatus(StatusEffectSystem.StatusType.Shock))
                dmg *= 2;
            if (applyAilments)
            {
                TryApplyAilments(_statusEffectSystem, type, dmg);
            }
        }

        RegisterRunDamage(dmg, type);
        currentHealth = Mathf.Clamp(currentHealth - dmg, 0, maxHealth);
        ApplyThorns();
        SyncSlider();

                        // Damage popup (accumulate if an existing one is active) — per damage type
        if (damagePopupPrefab != null)
        {
            Color popupColor = GetDamageColor(type);

            _activeDamagePopups.TryGetValue(type, out var activeText);
            if (activeText != null)
            {
                int currentVal = 0;
                if (!int.TryParse(activeText.text, out currentVal)) currentVal = 0;
                currentVal += dmg;
                activeText.text = currentVal.ToString();
                activeText.color = popupColor;
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
                    tmp.text = dmg.ToString();
                    tmp.color = popupColor;
                    _activeDamagePopups[type] = tmp;
                }
            }
        }if (bloodSFX != null)
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

        UpdateStatsText();

        if (!IsAlive) Die();
        else if (invulnerabilityDuration > 0) StartCoroutine(InvulnerabilityCoroutine());
    }

    private void ApplyThorns()
    {
        if (thornsDamage <= 0 || !CompareTag("Player")) return;

        EnemyChaser nearest = null;
        float nearestSqr = float.PositiveInfinity;
        foreach (var enemy in FindObjectsByType<EnemyChaser>())
        {
            float sqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = enemy;
            }
        }

        if (nearest != null && nearest.TryGetComponent(out SimpleHealth enemyHealth))
            enemyHealth.TakeDamage(thornsDamage, DamageType.Physical, false, false);
    }

    private bool TryEvade(int rawDamage)
    {
        if (rawDamage <= 0 || evasion <= 0f || evasionScaling <= 0f) return false;
        float chance = evasion / (evasion + evasionScaling * rawDamage);
        chance = Mathf.Min(chance, maxEvasion);
        return Random.value < chance;
    }

    private int ApplyArmor(int rawDamage)
    {
        if (rawDamage <= 0 || armor <= 0f || armorScaling <= 0f) return rawDamage;
        float m = armor / (armor + armorScaling * rawDamage);
        if (maxMitigation > 0f) m = Mathf.Min(m, maxMitigation);
        float reduced = rawDamage * (1f - m);
        return Mathf.Max(0, Mathf.RoundToInt(reduced));
    }

    // per-type resistance after armor
    private int ApplyResistance(int rawDamage, DamageType type)
    {
        if (rawDamage <= 0) return 0;

        float resist = 0f;
        switch (type)
        {
            case DamageType.Fire: resist = fireResist; break;
            case DamageType.Cold: resist = coldResist; break;
            case DamageType.Lightning: resist = lightningResist; break;
            case DamageType.Poison: resist = poisonResist; break;
        }

        resist = Mathf.Clamp(resist, 0f, 0.95f);
        float reduced = rawDamage * (1f - resist);
        return Mathf.Max(0, Mathf.RoundToInt(reduced));
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
        UpdateStatsText();
    }

    public void Kill()
    {
        if (!IsAlive) return;
        currentHealth = 0;
        if (enableTemporaryHealth) currentTemporaryHealth = 0;
        SyncSlider();
        UpdateStatsText();
        Die();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        if (enableTemporaryHealth) currentTemporaryHealth = 0;
        ResetRunDamage();
        SyncSlider();
        UpdateStatsText();
    }

    private void Die()
    {
        if (deathObjects.Length > 0)
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

    public void IncreaseMaxHealth(int amount)
    {
        maxHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        SyncSlider();
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
        UpdateStatsText();
    }

    public void AddMaxHealth(int amount)
    {
        IncreaseMaxHealth(amount);
    }

    public void SetHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        SyncSlider();
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
        SyncSlider();
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

    // --- Resistances ---
    public void AddFireResist(float amount) => GiveResistance(DamageType.Fire, amount);
    public void SetFireResist(float value) { fireResist = Mathf.Clamp(value, 0f, 0.95f); UpdateStatsText(); }
    public void AddColdResist(float amount) => GiveResistance(DamageType.Cold, amount);
    public void SetColdResist(float value) { coldResist = Mathf.Clamp(value, 0f, 0.95f); UpdateStatsText(); }
    public void AddLightningResist(float amount) => GiveResistance(DamageType.Lightning, amount);
    public void SetLightningResist(float value) { lightningResist = Mathf.Clamp(value, 0f, 0.95f); UpdateStatsText(); }
    public void AddPoisonResist(float amount) => GiveResistance(DamageType.Poison, amount);
    public void SetPoisonResist(float value) { poisonResist = Mathf.Clamp(poisonResist + value, 0f, 0.95f); UpdateStatsText(); }

    // --- Invulnerability ---
    public void SetInvulnerable(float duration)
    {
        if (duration > 0)
        {
            StartCoroutine(InvulnerabilityCoroutineWithDuration(duration));
        }
    }

    private System.Collections.IEnumerator InvulnerabilityCoroutineWithDuration(float duration)
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(duration);
        isInvulnerable = false;
    }

    #endregion

    private System.Collections.IEnumerator InvulnerabilityCoroutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityDuration);
        isInvulnerable = false;
    }
}






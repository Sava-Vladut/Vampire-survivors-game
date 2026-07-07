using TMPro;
using UnityEngine;
using UnityEngine.UI; // For Image
using UnityEngine.Serialization;

public class SimpleShooter : MonoBehaviour
{
    [Header("Projectile Settings")]
    [Tooltip("Projectile prefab instantiated whenever this weapon fires.")]
    public GameObject bulletPrefab;
    [Tooltip("Projectile spawn points. If empty, this transform is used.")]
    public Transform[] shootTransforms; // Where to spawn bullets; if empty, uses this.transform

    [Tooltip("Initial movement speed applied to spawned projectiles.")]
    public float shootForce = 10f;
    [Min(0), Tooltip("Minimum base damage rolled for each spawned projectile before crits.")]
    public int minDamage = 15;
    [Min(0), Tooltip("Maximum base damage rolled for each spawned projectile before crits.")]
    public int damage = 15;
    [SerializeField, Tooltip("Damage category used for resistances, weaknesses, and damage coloring.")]
    public SimpleHealth.DamageType damageType;
    [Tooltip("Time in seconds before a spawned projectile is destroyed. Set to 0 or less to disable timed destruction.")]
    public float bulletLifetime = 5f;
    [Tooltip("Number of enemies a projectile can pass through before being destroyed.")]
    public int penetration = 1; // How many enemies the bullet can pass through before being destroyed
    [Min(0f)] public float knockbackForce = 0f;
    [FormerlySerializedAs("executeChance")]
    [Tooltip("Enemies at or below this fraction of their max health are instantly slain by this weapon's hits.")]
    [Range(0f, 1f)] public float cullThreshold = 0f;
    [Min(0)] public int chainHits = 0;

    [Header("Criticals")]
    [Tooltip("Chance for a projectile to deal critical damage.")]
    [Range(0f, 1f)] public float critChance = 0f;
    [Tooltip("Damage multiplier applied when a critical hit occurs.")]
    [Min(1f)] public float critMultiplier = 2f;

    [Header("On Hit Effects")]
    [Tooltip("Whether projectile hits can apply a status effect.")]
    public bool applyStatusEffectOnHit = false;
    [Tooltip("Chance for each projectile hit to apply the selected status effect.")]
    public float statusApplyChance = 1f;    // optional: chance to apply on hit (0..1)
    [Tooltip("Status effect applied when the on-hit chance succeeds.")]
    public StatusEffectSystem.StatusType statusEffectOnHit = StatusEffectSystem.StatusType.Bleeding;
    [Tooltip("Duration in seconds for the applied status effect.")]
    public float statusEffectDuration = 3f;

    [Header("Shot Pattern")]
    [Tooltip("Number of projectiles spawned from each shoot transform per attack.")]
    public int projectileCount = 1;
    [Tooltip("Total cone in degrees. Each projectile gets a random angle within [-spread/2, +spread/2].")]
    public float spreadAngle = 0f;

    [Header("Unique Effects")]
    [Tooltip("Chance for each projectile to split into two weaker angled side shots.")]
    [Range(0f, 1f)] public float forkShotChance = 0f;
    [Tooltip("Angle offset used by forked side shots.")]
    [Range(0f, 90f)] public float forkShotAngle = 18f;
    [Tooltip("Damage fraction dealt by each forked side shot.")]
    [Range(0f, 1f)] public float forkShotDamagePercent = 0.5f;

    [Header("SFX")]
    [Tooltip("Audio clip played whenever the weapon fires.")]
    [SerializeField] private AudioClip shootClip;

    [Header("UI")]
    [Tooltip("Prefab root GameObject that contains a TextMeshProUGUI somewhere in its children.")]
    [SerializeField] public GameObject statsTextPrefab;
    [Tooltip("Transform under which the weapon stats UI is instantiated.")]
    [SerializeField] private Transform uiParent;
    [Tooltip("Optional custom text appended to the generated weapon stats.")]
    [TextArea][SerializeField] public string extraTextField = " ";
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] public Sprite weaponSprite;

    private WeaponTick wt;
    [HideInInspector] public TextMeshProUGUI statsTextInstance;
    private Image iconImage;
    private AudioSource shootSource;
    private GameObject statsGameobjectInstance;

    private void Awake()
    {
        shootSource = GetComponent<AudioSource>();

        if (statsTextPrefab != null && uiParent != null)
        {
            // Instantiate the prefab root
            var go = Instantiate(statsTextPrefab, uiParent);
            statsGameobjectInstance = go;
            // Find the TMP text anywhere under it
            statsTextInstance = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (statsTextInstance != null) statsTextInstance.text = "";

            // Find child GameObject named "Icon" and get its Image
            var iconObj = go.transform.Find("Icon");
            if (iconObj != null)
                iconImage = iconObj.GetComponent<Image>();

            if (iconImage != null && weaponSprite != null)
                iconImage.sprite = weaponSprite;

        }

        wt = GetComponent<WeaponTick>();
        UpdateStatsText();
    }

    public void ChangeBullet(GameObject newBullet)
    {
        bulletPrefab = newBullet;
        if (statsTextInstance != null)
        {
            UpdateStatsText();
        }

    }

    private void Update()
    {
        UpdateStatsText();
    }

    public void RemoveStatsText()
    {
        if (statsTextInstance != null)
        {
            Destroy(statsTextInstance.gameObject.transform.root.gameObject);
            statsTextInstance = null;
        }
    }
    public void EnableOnHitEffect(StatusEffectSystem.StatusType effectType)
    {
        applyStatusEffectOnHit = true;
        statusEffectOnHit = effectType;
    }

    public void SetOnHitEffectDuration(float duration)
    {
        statusEffectDuration = duration;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="effectIndex"></param>
    public void EnableOnHitEffectByIndex(int effectIndex)
    {
        applyStatusEffectOnHit = true;
        statusEffectOnHit = (StatusEffectSystem.StatusType)effectIndex;
    }


    public void UpdateStatsText()
    {
        if (statsTextInstance == null) return;

        // Compute dynamic fields
        const string numColor = "#8888FF";
        string delay = wt != null ? $"<color={numColor}>{wt.interval:F1}</color>s" : "N/A";

        // Build text (Knife.cs style)
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{transform.name}</b>");

        // ✅ Upgrades: enabled / total (in children)
        var allUpgrades = GetComponentsInChildren<WeaponUpgrades>(true);
        int enabledUpgrades = 0;
        for (int i = 0; i < allUpgrades.Length; i++)
        {
            var u = allUpgrades[i];
            if (u != null && u.enabled && u.gameObject.activeInHierarchy)
                enabledUpgrades++;
        }
        if (enabledUpgrades > 0)
            sb.AppendLine($"Upg: <color={numColor}>{enabledUpgrades}</color>/<color={numColor}>{WeaponUpgrades.MaxUpgrades}</color>");

        string damageColor = GetDamageTypeHex(damageType);
        sb.AppendLine($"DMG: <color={damageColor}>{GetDamageRangeText()}</color>");
        if (damageType != SimpleHealth.DamageType.Physical)
            sb.AppendLine($"Type: <color={damageColor}>{damageType}</color>");
        sb.AppendLine($"Delay: {delay}");
        if (shootForce > 0f)
            sb.AppendLine($"Speed: <color={numColor}>{shootForce:F1}</color>");
        if (bulletLifetime > 0f)
            sb.AppendLine($"Life: <color={numColor}>{bulletLifetime:F1}</color>s");
        if (projectileCount > 1)
            sb.AppendLine($"Shots: <color={numColor}>{projectileCount}</color>");
        if (penetration > 1)
            sb.AppendLine($"Pierce: <color={numColor}>{penetration}</color>");
        if (critChance > 0f)
            sb.AppendLine($"Crit: <color={numColor}>{(Mathf.Clamp01(critChance) * 100f):F0}</color>% x<color={numColor}>{critMultiplier:F2}</color>");
        if (forkShotChance > 0f)
            sb.AppendLine($"Fork: <color={numColor}>{forkShotChance * 100f:F0}</color>% for <color={numColor}>{forkShotDamagePercent * 100f:F0}</color>% dmg");

        if (applyStatusEffectOnHit)
        {
            sb.AppendLine($"Proc: <color={numColor}>{statusApplyChance * 100f:F0}</color>%");
            sb.AppendLine($"Hit: {statusEffectOnHit} (<color={numColor}>{statusEffectDuration:F1}</color>s)");
        }

        if (bulletPrefab != null && bulletPrefab.TryGetComponent<RB2DChainToTag>(out var RB2D) && RB2D.maxChains > 0)
            sb.AppendLine($"Chain: <color={numColor}>{RB2D.maxChains}</color>");

        if (!string.IsNullOrWhiteSpace(extraTextField))
            sb.AppendLine(extraTextField);

        statsTextInstance.text = sb.ToString();
    }




    private void OnDisable()
    {
        Destroy(statsGameobjectInstance);
    }


    // --- Shooting API ---

    // Call this to shoot at a Transform
    public void ShootTransform(Transform target)
    {
        if (target == null || bulletPrefab == null) return;

        // Per-origin shooting toward the target
        ShootTowards(target.position);
    }

    // Core shooter that handles multiple origins + spread
    public void ShootTowards(Vector3 worldTargetPos)
    {
        if (bulletPrefab == null) return;

        // SFX
        if (shootClip != null && shootSource != null)
            shootSource.PlayOneShot(shootClip);

        float halfSpread = spreadAngle * 0.5f;

        // Use provided origins or fallback to this.transform
        Transform[] origins = (shootTransforms != null && shootTransforms.Length > 0)
            ? shootTransforms
            : new Transform[] { transform };

        for (int oi = 0; oi < origins.Length; oi++)
        {
            var origin = origins[oi];
            if (origin == null) continue;

            // ✅ Direction from THIS origin to target
            Vector2 baseDir = (Vector2)(worldTargetPos - origin.position);
            if (baseDir.sqrMagnitude < 0.0001f) baseDir = (Vector2)origin.right; // per-origin fallback
            baseDir.Normalize();

            int shots = Mathf.Max(1, projectileCount);
            for (int i = 0; i < shots; i++)
            {
                float angle = (spreadAngle > 0f) ? Random.Range(-halfSpread, halfSpread) : 0f;
                float rad = angle * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);

                Vector2 shootDir = new Vector2(
                    baseDir.x * cos - baseDir.y * sin,
                    baseDir.x * sin + baseDir.y * cos
                );

                int finalDamage = RollHitDamage();

                SpawnProjectile(origin.position, shootDir, finalDamage);

                if (forkShotChance > 0f && Random.value <= Mathf.Clamp01(forkShotChance))
                {
                    int forkDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * Mathf.Clamp01(forkShotDamagePercent)));
                    float forkAngle = Mathf.Max(0f, forkShotAngle);
                    SpawnProjectile(origin.position, Rotate(shootDir, -forkAngle), forkDamage);
                    SpawnProjectile(origin.position, Rotate(shootDir, forkAngle), forkDamage);
                }
            }
        }
    }

    private void SpawnProjectile(Vector3 originPosition, Vector2 shootDir, int finalDamage)
    {
        var bullet = Instantiate(bulletPrefab, originPosition, Quaternion.identity);

        float rotDeg = Mathf.Atan2(shootDir.y, shootDir.x) * Mathf.Rad2Deg;
        bullet.transform.rotation = Quaternion.Euler(0, 0, rotDeg);

        if (bullet.TryGetComponent<BulletDamageTrigger>(out var bulletDamage))
        {
            bulletDamage.damageAmount = finalDamage;
            bulletDamage.damageType = damageType;
            bulletDamage.penetration = penetration;
            bulletDamage.knockbackForce = knockbackForce;
            bulletDamage.cullThreshold = cullThreshold;
            bulletDamage.statusApplyChance = statusApplyChance;
            bulletDamage.applyStatusEffectOnHit = applyStatusEffectOnHit;
            bulletDamage.statusEffectOnHit = statusEffectOnHit;
            bulletDamage.statusEffectDuration = statusEffectDuration;
            bulletDamage.sourceObject = gameObject;
        }

        if (bullet.TryGetComponent<ExplosionDamage2D>(out var explosionDamage))
        {
            explosionDamage.baseDamage = finalDamage;
            explosionDamage.damageType = damageType;
            explosionDamage.sourceObject = gameObject;
            explosionDamage.sourceDetail = "Projectile Explosion";
        }

        if (bullet.TryGetComponent<Rigidbody2D>(out var rb))
            rb.linearVelocity = shootDir * shootForce;

        if (bulletLifetime > 0f)
            Destroy(bullet, bulletLifetime);
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos
        );
    }

    public int RollBaseDamage()
    {
        int min = Mathf.Max(0, Mathf.Min(minDamage, damage));
        int max = Mathf.Max(0, Mathf.Max(minDamage, damage));
        return Random.Range(min, max + 1);
    }

    public int RollHitDamage()
    {
        int baseDamage = RollBaseDamage();
        if (Random.value < Mathf.Clamp01(critChance))
            return PlayerDamageMultiplierUtility.Apply(gameObject, Mathf.RoundToInt(baseDamage * Mathf.Max(1f, critMultiplier)));

        return PlayerDamageMultiplierUtility.Apply(gameObject, baseDamage);
    }

    private string GetDamageRangeText()
    {
        int min = Mathf.Max(0, Mathf.Min(minDamage, damage));
        int max = Mathf.Max(0, Mathf.Max(minDamage, damage));
        return min == max ? max.ToString() : $"{min}-{max}";
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

    // --- Debug Helpers ---
    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minDamage = Mathf.Max(0, minDamage);
        damage = Mathf.Max(minDamage, damage);
    }
#endif
}

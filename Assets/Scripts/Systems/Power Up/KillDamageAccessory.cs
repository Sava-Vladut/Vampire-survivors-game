using UnityEngine;

public interface IPlayerDamageMultiplierProvider
{
    float DamageMultiplier { get; }
}

public static class PlayerDamageMultiplierUtility
{
    public static int Apply(GameObject source, int damage)
    {
        if (source == null || damage <= 0)
            return damage;

        float multiplier = 1f;
        var providers = source.transform.root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < providers.Length; i++)
        {
            var behaviour = providers[i];
            if (behaviour == null || !behaviour.enabled || !behaviour.gameObject.activeInHierarchy)
                continue;

            if (behaviour is IPlayerDamageMultiplierProvider provider)
                multiplier *= Mathf.Max(0f, provider.DamageMultiplier);
        }

        return Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
    }
}

public class KillDamageAccessory : MonoBehaviour, IPlayerDamageMultiplierProvider, IAccessoryDescriptionProvider
{
    [SerializeField, Tooltip("Damage bonus gained per enemy killed. 0.001 = 0.1%.")]
    private float bonusPerEnemyKilled = 0.001f;
    [SerializeField, Tooltip("Maximum bonus damage. 1.5 = +150%.")]
    private float maxBonusDamage = 1.5f;

    private int enemyKills;
    private float lastDisplayedBonus = -1f;

    public float CurrentBonusDamage => Mathf.Min(enemyKills * bonusPerEnemyKilled, maxBonusDamage);
    public float DamageMultiplier => 1f + CurrentBonusDamage;

    private void OnEnable()
    {
        SimpleHealth.AnyDied += OnAnyDied;
        RefreshDescriptionIfNeeded(true);
    }

    private void OnDisable()
    {
        SimpleHealth.AnyDied -= OnAnyDied;
    }

    private void OnAnyDied(SimpleHealth health)
    {
        if (health == null || health.CompareTag("Player"))
            return;

        if (health.GetComponent<EnemyChaser>() == null)
            return;

        enemyKills++;
        RefreshDescriptionIfNeeded();
    }

    private void RefreshDescriptionIfNeeded(bool force = false)
    {
        float current = CurrentBonusDamage;
        if (!force && Mathf.Approximately(current, lastDisplayedBonus))
            return;

        lastDisplayedBonus = current;
        GetComponent<Accessory>()?.NotifyRootToRefresh();
    }

    public string GetAccessoryDescriptionLine()
    {
        return $"<color=#FFD166>Damage: +{CurrentBonusDamage * 100f:F1}% ({enemyKills} kills)</color>";
    }
}

using UnityEngine;

public class KillDamageAccessory : AccessoryBehaviour, IPlayerDamageMultiplierProvider
{
    [SerializeField, Tooltip("Damage bonus gained per enemy killed. 0.001 = 0.1%.")]
    private float bonusPerEnemyKilled = 0.001f;
    [SerializeField, Tooltip("Maximum bonus damage. 1.5 = +150%.")]
    private float maxBonusDamage = 1.5f;

    private int enemyKills;
    private float lastDisplayedBonus = -1f;

    public float CurrentBonusDamage => Mathf.Min(enemyKills * bonusPerEnemyKilled, maxBonusDamage);
    public float DamageMultiplier => 1f + CurrentBonusDamage;

    protected override void OnAccessoryEnabled()
    {
        SimpleHealth.AnyDied += OnAnyDied;
        RefreshDescriptionIfNeeded(true);
    }

    protected override void OnAccessoryDisabled()
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
        MarkDescriptionDirty();
    }

    public override string GetAccessoryDescriptionLine()
    {
        return $"<color=#FFD166>Damage: +{CurrentBonusDamage * 100f:F1}% ({enemyKills} kills)</color>";
    }
}

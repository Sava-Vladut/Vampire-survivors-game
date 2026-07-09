using UnityEngine;

public class SanguineChaliceAccessory : AccessoryBehaviour
{
    [SerializeField, Min(1), Tooltip("Enemy kills required to trigger a heal.")]
    private int killsPerHeal = 10;
    [SerializeField, Range(0f, 1f), Tooltip("Fraction of maximum health restored each time. 0.05 = 5%.")]
    private float maxHealthFractionHealed = 0.05f;

    private SimpleHealth ownerHealth;
    private int killsTowardsHeal;

    protected override void OnAccessoryEnabled()
    {
        killsTowardsHeal = 0;
        ownerHealth = GetComponentInParent<SimpleHealth>(true);
        SimpleHealth.AnyDied += OnAnyDied;
        MarkDescriptionDirty();
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

        int requiredKills = Mathf.Max(1, killsPerHeal);
        killsTowardsHeal++;
        if (killsTowardsHeal >= requiredKills)
        {
            killsTowardsHeal -= requiredKills;
            HealOwner();
        }

        MarkDescriptionDirty();
    }

    private void HealOwner()
    {
        if (ownerHealth == null)
            ownerHealth = GetComponentInParent<SimpleHealth>(true);

        if (ownerHealth == null)
            return;

        int amount = Mathf.Max(1, Mathf.RoundToInt(ownerHealth.MaxHealth * maxHealthFractionHealed));
        ownerHealth.Heal(amount);
    }

    public override string GetAccessoryDescriptionLine()
    {
        int requiredKills = Mathf.Max(1, killsPerHeal);
        return $"<color=#FF6B6B>Healing: {maxHealthFractionHealed * 100f:F0}% max HP every {requiredKills} kills ({killsTowardsHeal}/{requiredKills})</color>";
    }
}

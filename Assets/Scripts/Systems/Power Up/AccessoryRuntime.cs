using UnityEngine;

public interface IAccessoryEquipEffect
{
    int Order { get; }
    bool TryEquip(AccessoryEquipContext context);
}

public readonly struct AccessoryEquipContext
{
    public AccessoryEquipContext(PowerUpSelectionContext selection, Accessory accessory, Transform playerRoot)
    {
        Selection = selection;
        Accessory = accessory;
        PlayerRoot = playerRoot;
        Health = playerRoot.GetComponentInChildren<SimpleHealth>(true);
        Movement = playerRoot.GetComponentInChildren<Snappy2DController>(true);
        Inventory = playerRoot.GetComponentInChildren<AccessoryInventory>(true);
        DamageModifiers = playerRoot.GetComponentInChildren<PlayerDamageModifierRegistry>(true);
    }

    public PowerUpSelectionContext Selection { get; }
    public Accessory Accessory { get; }
    public Transform PlayerRoot { get; }
    public SimpleHealth Health { get; }
    public Snappy2DController Movement { get; }
    public AccessoryInventory Inventory { get; }
    public PlayerDamageModifierRegistry DamageModifiers { get; }
}

public abstract class AccessoryBehaviour : MonoBehaviour, IAccessoryDescriptionProvider
{
    private Accessory accessory;
    private PlayerDamageModifierRegistry damageModifiers;

    protected Accessory Owner => accessory != null ? accessory : accessory = GetComponentInParent<Accessory>(true);
    protected Transform PlayerRoot => Owner != null ? Owner.transform.root : transform.root;
    protected SimpleHealth OwnerHealth => PlayerRoot != null ? PlayerRoot.GetComponentInChildren<SimpleHealth>(true) : null;

    protected virtual void OnEnable()
    {
        if (this is IPlayerDamageMultiplierProvider provider)
        {
            damageModifiers = PlayerRoot != null ? PlayerRoot.GetComponentInChildren<PlayerDamageModifierRegistry>(true) : null;
            damageModifiers?.Register(provider);
        }
        OnAccessoryEnabled();
    }

    protected virtual void OnDisable()
    {
        OnAccessoryDisabled();
        if (this is IPlayerDamageMultiplierProvider provider)
            damageModifiers?.Unregister(provider);
        damageModifiers = null;
    }

    protected virtual void OnAccessoryEnabled() { }
    protected virtual void OnAccessoryDisabled() { }
    protected void MarkDescriptionDirty() => Owner?.MarkChanged();
    public abstract string GetAccessoryDescriptionLine();
}

public interface IPlayerDamageMultiplierProvider
{
    float DamageMultiplier { get; }
}

public static class PlayerDamageMultiplierUtility
{
    public static int Apply(GameObject source, int damage)
    {
        if (source == null || damage <= 0) return damage;
        PlayerDamageModifierRegistry registry = source.GetComponentInParent<PlayerDamageModifierRegistry>();
        int modifiedDamage = registry != null ? registry.Apply(damage) : damage;
        PlayerAccessoryStats stats = PlayerAccessoryStats.Find(source.transform);
        return stats != null ? stats.ApplyGlobalDamage(modifiedDamage) : modifiedDamage;
    }
}

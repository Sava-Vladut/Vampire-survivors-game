using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AccessoryInventory : MonoBehaviour
{
    private readonly List<Accessory> equipped = new();

    public event Action<Accessory> Equipped;
    public event Action<Accessory> Removed;
    public event Action<Accessory> AccessoryChanged;
    public IReadOnlyList<Accessory> EquippedAccessories => equipped;

    public bool Register(Accessory accessory)
    {
        if (accessory == null || equipped.Contains(accessory)) return false;
        equipped.Add(accessory);
        Equipped?.Invoke(accessory);
        return true;
    }

    public bool Unregister(Accessory accessory)
    {
        if (accessory == null || !equipped.Remove(accessory)) return false;
        Removed?.Invoke(accessory);
        return true;
    }

    public void NotifyChanged(Accessory accessory)
    {
        if (accessory != null && equipped.Contains(accessory))
            AccessoryChanged?.Invoke(accessory);
    }
}

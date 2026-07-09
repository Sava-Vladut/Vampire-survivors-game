using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AccessoryStatEffect : MonoBehaviour, IAccessoryEquipEffect, IAccessoryDescriptionProvider
{
    [SerializeField] private List<AccessoryStatModifier> modifiers = new();
    [SerializeField, HideInInspector] private bool applied;

    public int Order => 0;
    public IReadOnlyList<AccessoryStatModifier> Modifiers => modifiers;

    public bool TryEquip(AccessoryEquipContext context)
    {
        if (applied) return true;
        for (int i = 0; i < modifiers.Count; i++)
            if (!AccessoryStatApplicator.CanApply(modifiers[i].type, context)) return false;
        for (int i = 0; i < modifiers.Count; i++)
            AccessoryStatApplicator.Apply(modifiers[i].type, modifiers[i].value, context);
        applied = true;
        return true;
    }

    public string GetAccessoryDescriptionLine()
    {
        var lines = new List<string>(modifiers.Count);
        for (int i = 0; i < modifiers.Count; i++)
            lines.Add(AccessoryStatApplicator.FormatDescription(modifiers[i].type, modifiers[i].value));
        return string.Join("\n", lines);
    }
}

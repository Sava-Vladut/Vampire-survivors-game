using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChatterBoss : MonoBehaviour
{
    public SimpleHealth Health { get; private set; }
    public string DisplayName { get; private set; } = "Boss";
    public bool LegendaryApplied { get; private set; }

    public void Initialize(SimpleHealth health, string displayName)
    {
        Health = health;
        if (!string.IsNullOrWhiteSpace(displayName))
            DisplayName = displayName;
    }

    public void MarkLegendaryApplied()
    {
        LegendaryApplied = true;
    }
}

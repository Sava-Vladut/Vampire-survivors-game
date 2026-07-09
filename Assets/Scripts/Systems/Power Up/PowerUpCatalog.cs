using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PowerUpCatalog", menuName = "Power Ups/Power-Up Catalog")]
public sealed class PowerUpCatalog : ScriptableObject
{
    [SerializeField] private PowerUpDefinition[] entries = Array.Empty<PowerUpDefinition>();

    public PowerUpDefinition[] Entries => entries;
}

using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PowerUp", menuName = "Power Ups/Power-Up Definition")]
public sealed class PowerUpDefinition : ScriptableObject
{
    [SerializeField, HideInInspector] private string stableId;

    [Header("Presentation")]
    [SerializeField] private string displayName;
    [TextArea(2, 6)][SerializeField] private string description;
    [SerializeField] private Sprite icon;

    [Header("Selection")]
    [SerializeField] private PowerUpTags tags = PowerUpTags.World;
    [SerializeField] private PowerUpRarity rarity = PowerUpRarity.Common;
    [Min(0f)][SerializeField] private float selectionWeight = 1f;

    [Header("Activation")]
    [Tooltip("Prefer a prefab here. Scene object references should remain on legacy scene entries.")]
    [SerializeField] private GameObject activationObject;

    public string StableId => stableId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public PowerUpTags Tags => tags;
    public PowerUpRarity Rarity => rarity;
    public float SelectionWeight => Mathf.Max(0f, selectionWeight);
    public GameObject ActivationObject => activationObject;

    public PowerUp CreateOffer() => PowerUp.FromDefinition(this);

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(stableId))
            stableId = Guid.NewGuid().ToString("N");

        if (tags == PowerUpTags.None)
            tags = PowerUpTags.World;

        selectionWeight = Mathf.Max(0f, selectionWeight);
    }
}

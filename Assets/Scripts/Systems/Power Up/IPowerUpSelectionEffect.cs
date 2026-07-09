using UnityEngine;

/// <summary>
/// Explicit effect invoked by PowerUpChooser after an offer's object is active.
/// This replaces hidden gameplay mutations from Awake/OnEnable.
/// </summary>
public interface IPowerUpSelectionEffect
{
    bool TryApply(PowerUpSelectionContext context);
}

public readonly struct PowerUpSelectionContext
{
    public PowerUpSelectionContext(PowerUpChooser chooser, PowerUp offer, GameObject instance, Transform playerRoot)
    {
        Chooser = chooser;
        Offer = offer;
        Instance = instance;
        PlayerRoot = playerRoot;
    }

    public PowerUpChooser Chooser { get; }
    public PowerUp Offer { get; }
    public GameObject Instance { get; }
    public Transform PlayerRoot { get; }
}

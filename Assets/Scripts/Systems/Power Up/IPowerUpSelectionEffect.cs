using UnityEngine;

/// <summary>
/// Explicit effect invoked by PowerUpChooser after an offer's object is active.
/// This replaces hidden gameplay mutations from Awake/OnEnable.
/// </summary>
public interface IPowerUpSelectionEffect
{
    bool TryApply(PowerUpSelectionContext context);
}

/// <summary>
/// Optional preview-time validation for offers whose usefulness depends on the
/// player's current build. PowerUpChooser evaluates this before an offer is
/// displayed and again immediately before it is selected.
/// </summary>
public interface IPowerUpOfferEligibility
{
    bool CanOffer(PowerUpSelectionContext context);
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

using UnityEngine;

/// <summary>
/// Inserts a new PowerUp into a target PowerUpChooser's available list when this component is enabled.
/// This is useful for dynamically adding power-ups to the pool at runtime.
/// </summary>
public class PowerUpInserter : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("The PowerUp to add to the chooser's list.")]
    [SerializeField] private PowerUp powerUpToAdd;

    [SerializeField] private PowerUpChooser powerUpChooser;

    private void OnEnable()
    {
        InsertPowerUp();
    }

    /// <summary>
    /// Inserts the configured power-up into the chooser's available list.
    /// It checks to prevent adding duplicates.
    /// </summary>
    private void InsertPowerUp()
    {
        if (powerUpChooser == null)
        {
            Debug.LogError("[PowerUpInserter] PowerUpChooser component not found!", this);
            return;
        }

        if (powerUpToAdd == null || string.IsNullOrEmpty(powerUpToAdd.powerUpName))
        {
            Debug.LogWarning("[PowerUpInserter] No PowerUp has been configured to be added.", this);
            return;
        }

        if (powerUpChooser.TryAddAvailable(powerUpToAdd))
        {
            Debug.Log($"'{powerUpToAdd.powerUpName}' was added to the available power-ups.", this);
        }
    }
}

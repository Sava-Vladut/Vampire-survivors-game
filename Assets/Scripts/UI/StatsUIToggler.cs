using UnityEngine;
using UnityEngine.Rendering;

public class StatsUIToggler : MonoBehaviour
{
    [Tooltip("UI object to show when holding the key.")]
    [SerializeField] private GameObject statsUI;

    [Tooltip("Another UI element to hide when stats UI is shown.")]
    [SerializeField] private GameObject otherUI;

    [Tooltip("Key to hold to show stats.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Tooltip("Time scale when holding the key (1 = normal, 0.5 = half speed, 0 = paused).")]
    [Range(0f, 1f)]
    [SerializeField] private float slowTimeScale = 0.5f;

    [SerializeField] private Volume slowMoVolume;

    private float timeScaleBeforeSlowMo = 1f;
    private bool ownsSlowMo;
    private bool wasOtherUIActive;

    private void Start()
    {
        if (statsUI != null)
            statsUI.SetActive(false);

        if (slowMoVolume)
            slowMoVolume.weight = 0f;
    }

    private void Update()
    {
        bool externallyPaused = Mathf.Approximately(Time.timeScale, 0f);

        if (Input.GetKey(toggleKey))
        {
            if (statsUI && !statsUI.activeSelf)
            {
                statsUI.SetActive(true);

                // Only hide otherUI if it is currently active.
                if (otherUI && otherUI.activeSelf)
                {
                    wasOtherUIActive = true;
                    otherUI.SetActive(false);
                }
                else
                {
                    wasOtherUIActive = false;
                }
            }

            // Only apply slow-mo if not externally paused.
            if (!externallyPaused)
                ApplySlowMo();
        }
        else
        {
            HideStatsUI();
            ReleaseSlowMo();
        }
    }

    private void OnDisable()
    {
        HideStatsUI();
        ReleaseSlowMo();
    }

    private void OnDestroy()
    {
        ReleaseSlowMo();
    }

    private void ApplySlowMo()
    {
        if (!ownsSlowMo)
        {
            timeScaleBeforeSlowMo = Time.timeScale;
            ownsSlowMo = true;
        }

        Time.timeScale = slowTimeScale;
        if (slowMoVolume) slowMoVolume.weight = 1f;
    }

    private void ReleaseSlowMo()
    {
        if (slowMoVolume) slowMoVolume.weight = 0f;

        if (!ownsSlowMo) return;

        ownsSlowMo = false;

        // If another system paused the game while Tab was held, let that pause win.
        if (!Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = Mathf.Approximately(timeScaleBeforeSlowMo, slowTimeScale) ? 1f : timeScaleBeforeSlowMo;
    }

    private void HideStatsUI()
    {
        if (statsUI && statsUI.activeSelf)
            statsUI.SetActive(false);

        // Only reactivate otherUI if we hid it.
        if (otherUI && wasOtherUIActive)
        {
            otherUI.SetActive(true);
            wasOtherUIActive = false;
        }
    }
}
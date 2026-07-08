using UnityEngine;
using UnityEngine.Rendering;

public class StatsUIToggler : MonoBehaviour
{
    [Tooltip("UI object to show when toggled on.")]
    [SerializeField] private GameObject statsUI;

    [Tooltip("Another UI element to hide when stats UI is shown.")]
    [SerializeField] private GameObject otherUI;

    [Tooltip("Key to press to toggle stats.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Tooltip("Time scale while stats are shown (1 = normal, 0.5 = half speed, 0 = paused).")]
    [Range(0f, 1f)]
    [SerializeField] private float slowTimeScale = 0.5f;

    [SerializeField] private Volume slowMoVolume;

    private float timeScaleBeforeSlowMo = 1f;
    private bool ownsSlowMo;
    private bool wasOtherUIActive;
    private bool isStatsVisible;

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

        if (Input.GetKeyDown(toggleKey))
        {
            if (isStatsVisible)
            {
                HideStatsUI();
                ReleaseSlowMo();
            }
            else
            {
                ShowStatsUI();
            }
        }

        if (isStatsVisible)
        {
            if (!externallyPaused)
                ApplySlowMo();
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

        // If another system paused the game while stats were open, let that pause win.
        if (!Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = Mathf.Approximately(timeScaleBeforeSlowMo, slowTimeScale) ? 1f : timeScaleBeforeSlowMo;
    }

    private void ShowStatsUI()
    {
        isStatsVisible = true;

        if (statsUI && !statsUI.activeSelf)
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

    private void HideStatsUI()
    {
        isStatsVisible = false;

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

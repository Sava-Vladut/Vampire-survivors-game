using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleSceneRestarter : MonoBehaviour
{
    public KeyCode restartKey = KeyCode.R; // Key to restart the scene
    [SerializeField, Min(0f)] private float holdDuration = 1f;

    private float heldTime;

    void Update()
    {
        if (!Input.GetKey(restartKey))
        {
            heldTime = 0f;
            return;
        }

        heldTime += Time.unscaledDeltaTime;
        if (heldTime >= holdDuration)
        {
            heldTime = 0f;
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}

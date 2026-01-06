using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuManagerUI : MonoBehaviour
{
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;

    private bool isPaused;

    private void Start()
    {
        if (pauseButton) pauseButton.onClick.AddListener(PauseGame);
        if (continueButton) continueButton.onClick.AddListener(ContinueGame);
        if (restartButton) restartButton.onClick.AddListener(RestartGame);
        if (quitButton) quitButton.onClick.AddListener(QuitGame);
    }

    private void PauseGame()
    {
        Time.timeScale = 0f;
        isPaused = true;
        AudioListener.pause = true;
    }

    private void ContinueGame()
    {
        Time.timeScale = 1f;
        isPaused = false;
        AudioListener.pause = false;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

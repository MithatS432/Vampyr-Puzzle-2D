using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class MenuManagerUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;

    [Header("Animators")]
    [SerializeField] private Animator continueButtonAnimator;
    [SerializeField] private Animator restartButtonAnimator;
    [SerializeField] private Animator exitButtonAnimator;

    private bool isPaused;

    private void Start()
    {
        // Animator'lar pause sırasında çalışabilsin
        SetUnscaled(continueButtonAnimator);
        SetUnscaled(restartButtonAnimator);
        SetUnscaled(exitButtonAnimator);

        if (pauseButton) pauseButton.onClick.AddListener(PauseToggle);
        if (continueButton) continueButton.onClick.AddListener(ContinueGame);
        if (restartButton) restartButton.onClick.AddListener(RestartGame);
        if (quitButton) quitButton.onClick.AddListener(QuitGame);
    }

    private void SetUnscaled(Animator animator)
    {
        if (animator != null)
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }


    private void PauseToggle()
    {
        if (isPaused)
            CloseMenu();
        else
            OpenMenu();
    }

    private void OpenMenu()
    {
        StartCoroutine(OpenMenuSequence());
    }

    private IEnumerator OpenMenuSequence()
    {
        TriggerAll("Show");

        yield return new WaitForSecondsRealtime(0.5f);

        Time.timeScale = 0f;
        AudioListener.pause = true;
        isPaused = true;
    }

    private void CloseMenu()
    {
        TriggerAll("Back");

        Time.timeScale = 1f;
        AudioListener.pause = false;
        isPaused = false;
    }


    private void ContinueGame()
    {
        if (!isPaused) return;
        CloseMenu();
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }


    private void TriggerAll(string trigger)
    {
        Trigger(continueButtonAnimator, trigger);
        Trigger(restartButtonAnimator, trigger);
        Trigger(exitButtonAnimator, trigger);
    }

    private void Trigger(Animator animator, string trigger)
    {
        if (animator == null) return;

        animator.ResetTrigger("Show");
        animator.ResetTrigger("Back");
        animator.SetTrigger(trigger);
    }
}

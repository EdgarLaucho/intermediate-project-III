using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class PauseScreen : MonoBehaviour
{
    [SerializeField] private UIDocument screenUIDocument;

    private VisualElement pausePanel;
    private VisualElement screenOverlays;
    private Button resumeButton;
    private Button restartButton;
    private Button mainMenuButton;

    private void OnEnable()
    {
        GamePauseEvents.OnGameResumed += HandleGameResumed;
    }

    private void OnDisable()
    {
        GamePauseEvents.OnGameResumed -= HandleGameResumed;
    }

    private void Start()
    {
        ResolveScreenUI();
        SetPanelVisible(false);
    }

    private void OnDestroy()
    {
        if (resumeButton != null)
            resumeButton.clicked -= ResumeGame;

        if (restartButton != null)
            restartButton.clicked -= RestartGame;

        if (mainMenuButton != null)
            mainMenuButton.clicked -= GoToMainMenu;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GamePauseEvents.IsPaused && GamePauseEvents.CanResume)
            {
                ResumeGame();
            }
            else if (!GamePauseEvents.IsPaused)
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        if (GamePauseEvents.IsPaused)
            return;

        SetPanelVisible(true);
        GamePauseEvents.PauseGame(true);
    }

    public void ResumeGame()
    {
        if (!GamePauseEvents.CanResume)
            return;

        SetPanelVisible(false);
        GamePauseEvents.ResumeGame();
    }

    public void RestartGame()
    {
        GamePauseEvents.ClearPauseState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        GamePauseEvents.ClearPauseState();
        SceneManager.LoadScene("MainMenu");
    }

    private void HandleGameResumed()
    {
        SetPanelVisible(false);
    }

    private void ResolveScreenUI()
    {
        if (screenUIDocument == null || screenUIDocument.rootVisualElement == null)
        {
            Debug.LogError("[PauseScreen] Screen UIDocument not assigned.", this);
            return;
        }

        var root = screenUIDocument.rootVisualElement;
        screenOverlays = root.Q<VisualElement>("screen-overlays");
        pausePanel = root.Q<VisualElement>("pause-panel");
        resumeButton = root.Q<Button>("pause-resume-button");
        restartButton = root.Q<Button>("pause-restart-button");
        mainMenuButton = root.Q<Button>("pause-main-menu-button");

        if (screenOverlays != null)
            screenOverlays.pickingMode = PickingMode.Ignore;

        if (pausePanel != null)
            pausePanel.pickingMode = PickingMode.Ignore;

        if (resumeButton != null)
        {
            resumeButton.clicked -= ResumeGame;
            resumeButton.clicked += ResumeGame;
            resumeButton.pickingMode = PickingMode.Position;
        }

        if (restartButton != null)
        {
            restartButton.clicked -= RestartGame;
            restartButton.clicked += RestartGame;
            restartButton.pickingMode = PickingMode.Position;
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.clicked -= GoToMainMenu;
            mainMenuButton.clicked += GoToMainMenu;
            mainMenuButton.pickingMode = PickingMode.Position;
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (pausePanel == null)
            return;

        if (screenOverlays != null)
            screenOverlays.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

        pausePanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        pausePanel.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
    }
}

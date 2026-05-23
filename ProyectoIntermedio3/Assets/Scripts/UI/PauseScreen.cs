using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class PauseScreen : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private UIDocument screenUIDocument;

    private VisualElement pauseToolkitPanel;
    private VisualElement screenOverlays;
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
        if (restartButton != null)
            restartButton.clicked -= RestartGame;

        if (mainMenuButton != null)
            mainMenuButton.clicked -= GoToMainMenu;
    }

    void Update()
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
        UIDocument doc = screenUIDocument != null ? screenUIDocument : FindScreenUIDocument();
        if (doc == null || doc.rootVisualElement == null)
            return;

        screenUIDocument = doc;
        screenOverlays = doc.rootVisualElement.Q<VisualElement>("screen-overlays");
        pauseToolkitPanel = doc.rootVisualElement.Q<VisualElement>("pause-panel");
        restartButton = doc.rootVisualElement.Q<Button>("pause-restart-button");
        mainMenuButton = doc.rootVisualElement.Q<Button>("pause-main-menu-button");

        if (screenOverlays != null)
            screenOverlays.pickingMode = PickingMode.Ignore;

        if (pauseToolkitPanel != null)
            pauseToolkitPanel.pickingMode = PickingMode.Ignore;

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
        if (pauseToolkitPanel != null)
        {
            if (screenOverlays != null)
                screenOverlays.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

            pauseToolkitPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            pauseToolkitPanel.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

            if (pausePanel != null)
                pausePanel.SetActive(false);

            return;
        }

        if (pausePanel != null)
            pausePanel.SetActive(visible);
    }

    private static UIDocument FindScreenUIDocument()
    {
        foreach (UIDocument doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
        {
            if (doc?.rootVisualElement?.Q<VisualElement>("pause-panel") != null)
                return doc;
        }

        return null;
    }
}

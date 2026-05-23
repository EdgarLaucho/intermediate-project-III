using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class GameOverScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private UIDocument screenUIDocument;

    private VisualElement gameOverToolkitPanel;
    private VisualElement screenOverlays;
    private Button restartButton;
    private Button mainMenuButton;

    private void Awake()
    {
        GameOverTrigger.ResetGameOverState();
    }

    private void OnEnable()
    {
        GameOverTrigger.OnGameOverRequested += OnGameOverRequested;
    }

    private void OnDisable()
    {
        GameOverTrigger.OnGameOverRequested -= OnGameOverRequested;
    }

    void Start()
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

    private void OnGameOverRequested(GameOverTrigger.GameOverReason reason, GameObject source)
    {
        SetPanelVisible(true);
        GamePauseEvents.PauseGame(false);
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

    private void ResolveScreenUI()
    {
        UIDocument doc = screenUIDocument != null ? screenUIDocument : FindScreenUIDocument();
        if (doc == null || doc.rootVisualElement == null)
            return;

        screenUIDocument = doc;
        screenOverlays = doc.rootVisualElement.Q<VisualElement>("screen-overlays");
        gameOverToolkitPanel = doc.rootVisualElement.Q<VisualElement>("game-over-panel");
        restartButton = doc.rootVisualElement.Q<Button>("game-over-restart-button");
        mainMenuButton = doc.rootVisualElement.Q<Button>("game-over-main-menu-button");

        if (screenOverlays != null)
            screenOverlays.pickingMode = PickingMode.Ignore;

        if (gameOverToolkitPanel != null)
            gameOverToolkitPanel.pickingMode = PickingMode.Ignore;

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
        if (gameOverToolkitPanel != null)
        {
            if (screenOverlays != null)
                screenOverlays.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

            gameOverToolkitPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            gameOverToolkitPanel.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);

            return;
        }

        if (gameOverPanel != null)
            gameOverPanel.SetActive(visible);
    }

    private static UIDocument FindScreenUIDocument()
    {
        foreach (UIDocument doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
        {
            if (doc?.rootVisualElement?.Q<VisualElement>("game-over-panel") != null)
                return doc;
        }

        return null;
    }
}

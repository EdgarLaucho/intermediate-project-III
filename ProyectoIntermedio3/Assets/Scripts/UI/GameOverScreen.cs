using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class GameOverScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument screenUIDocument;

    private VisualElement gameOverPanel;
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
        if (screenUIDocument == null || screenUIDocument.rootVisualElement == null)
        {
            Debug.LogError("[GameOverScreen] Screen UIDocument not assigned.", this);
            return;
        }

        var root = screenUIDocument.rootVisualElement;
        screenOverlays = root.Q<VisualElement>("screen-overlays");
        gameOverPanel = root.Q<VisualElement>("game-over-panel");
        restartButton = root.Q<Button>("game-over-restart-button");
        mainMenuButton = root.Q<Button>("game-over-main-menu-button");

        if (screenOverlays != null)
            screenOverlays.pickingMode = PickingMode.Ignore;

        if (gameOverPanel != null)
            gameOverPanel.pickingMode = PickingMode.Ignore;

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
        if (gameOverPanel == null)
            return;

        if (screenOverlays != null)
            screenOverlays.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

        gameOverPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        gameOverPanel.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
    }
}
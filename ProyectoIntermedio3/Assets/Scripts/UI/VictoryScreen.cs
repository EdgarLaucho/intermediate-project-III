using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class VictoryScreen : MonoBehaviour
{
    [SerializeField] private UIDocument screenUIDocument;

    private VisualElement victoryPanel;
    private VisualElement screenOverlays;
    private Button restartButton;
    private Button mainMenuButton;

    private void OnEnable()
    {
        WaveSystem.OnGameWon += ShowVictoryScreen;
    }

    private void OnDisable()
    {
        WaveSystem.OnGameWon -= ShowVictoryScreen;
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

    private void ShowVictoryScreen()
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
            Debug.LogError("[VictoryScreen] Screen UIDocument not assigned.", this);
            return;
        }

        var root = screenUIDocument.rootVisualElement;
        screenOverlays = root.Q<VisualElement>("screen-overlays");
        victoryPanel = root.Q<VisualElement>("victory-panel");
        restartButton = root.Q<Button>("victory-restart-button");
        mainMenuButton = root.Q<Button>("victory-main-menu-button");

        if (screenOverlays != null)
            screenOverlays.pickingMode = PickingMode.Ignore;

        if (victoryPanel != null)
            victoryPanel.pickingMode = PickingMode.Ignore;

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
        if (victoryPanel == null)
            return;

        if (screenOverlays != null)
            screenOverlays.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

        victoryPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        victoryPanel.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
    }
}

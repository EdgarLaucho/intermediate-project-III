using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class VictoryScreen : MonoBehaviour
{
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private UIDocument screenUIDocument;

    private VisualElement victoryToolkitPanel;
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
        UIDocument doc = screenUIDocument != null ? screenUIDocument : FindScreenUIDocument();
        if (doc == null || doc.rootVisualElement == null)
            return;

        screenUIDocument = doc;
        screenOverlays = doc.rootVisualElement.Q<VisualElement>("screen-overlays");
        victoryToolkitPanel = doc.rootVisualElement.Q<VisualElement>("victory-panel");
        restartButton = doc.rootVisualElement.Q<Button>("victory-restart-button");
        mainMenuButton = doc.rootVisualElement.Q<Button>("victory-main-menu-button");

        if (screenOverlays != null)
            screenOverlays.pickingMode = PickingMode.Ignore;

        if (victoryToolkitPanel != null)
            victoryToolkitPanel.pickingMode = PickingMode.Ignore;

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
        if (victoryToolkitPanel != null)
        {
            if (screenOverlays != null)
                screenOverlays.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

            victoryToolkitPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            victoryToolkitPanel.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

            if (victoryPanel != null)
                victoryPanel.SetActive(false);

            return;
        }

        if (victoryPanel != null)
            victoryPanel.SetActive(visible);
    }

    private static UIDocument FindScreenUIDocument()
    {
        foreach (UIDocument doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
        {
            if (doc?.rootVisualElement?.Q<VisualElement>("victory-panel") != null)
                return doc;
        }

        return null;
    }
}
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class GameOverScreen : MonoBehaviour
{
    [SerializeField] private UIDocument screenUIDocument;

    private VisualElement _panel;
    private VisualElement _screenOverlays;
    private Button _restartButton;
    private Button _mainMenuButton;

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
        if (_restartButton != null)
            _restartButton.clicked -= RestartGame;

        if (_mainMenuButton != null)
            _mainMenuButton.clicked -= GoToMainMenu;
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
        var document = screenUIDocument != null ? screenUIDocument : FindScreenUIDocument();
        if (document == null || document.rootVisualElement == null) return;

        screenUIDocument = document;
        var root = document.rootVisualElement;
        _screenOverlays = root.Q<VisualElement>("screen-overlays");
        _panel = root.Q<VisualElement>("game-over-panel");
        _restartButton = root.Q<Button>("game-over-restart-button");
        _mainMenuButton = root.Q<Button>("game-over-main-menu-button");

        if (_screenOverlays != null)
            _screenOverlays.pickingMode = PickingMode.Ignore;

        if (_panel != null)
            _panel.pickingMode = PickingMode.Ignore;

        BindButton(_restartButton, RestartGame);
        BindButton(_mainMenuButton, GoToMainMenu);
    }

    private void SetPanelVisible(bool visible)
    {
        if (_panel == null) return;

        if (_screenOverlays != null)
            _screenOverlays.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

        _panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        _panel.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
    }

    private static void BindButton(Button button, System.Action action)
    {
        if (button == null) return;

        button.clicked -= action;
        button.clicked += action;
        button.pickingMode = PickingMode.Position;
    }

    private static UIDocument FindScreenUIDocument()
    {
        foreach (var document in FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            if (document?.rootVisualElement?.Q<VisualElement>("game-over-panel") != null)
                return document;

        return null;
    }
}
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UIElements;

public class WaveSystem : MonoBehaviour
{
    public static event Action OnGameWon;

    [Header("References")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private UIDocument screenUIDocument;

    [Header("Wave Settings")]
    public List<Wave> waves;
    public float timeBetweenWaves = 5f;

    [Header("Game Mode Settings")]
    public bool isNormalMode = false;
    
    [Header("Wave UI")]
    [SerializeField] private WaveInfoUI waveInfoUI;
    [SerializeField] private WaveEnemiesUI waveEnemiesUI;

    private int currentWaveIndex = 0;
    private int currentEnemiesAlive = 0;
    private int totalKills = 0;
    private bool startNextWavePressed = false;
    private Button battleButton;
    private Label killsLabel;
    private Label waveMessageLabel;
    private VisualElement waveHud;
    private VisualElement lifePanel;
    private bool reportedMissingToolkitUI;

    void Start()
    {
        isNormalMode = PlayerPrefs.GetInt("NormalModeActive", 0) == 1;
        PhaseEvents.PhaseChanged(GamePhase.Preparation);
        ResolveScreenUI();

        SetBattleButtonVisible(isNormalMode);

        if (enemySpawner == null)
        {
            enemySpawner = FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Exclude);
        }

        SetWaveMessage("");
        UpdateKillsCounter();

        StartCoroutine(SpawnWave());
    }

    private void OnDestroy()
    {
        if (battleButton != null)
            battleButton.clicked -= StartNextWave;
    }

    IEnumerator SpawnWave()
    {
        while (currentWaveIndex < waves.Count)
        {
            Wave currentWave = waves[currentWaveIndex];

            if (waveEnemiesUI != null)
            {
                waveEnemiesUI.ShowEnemies(currentWave.mixedEnemies);
            }
            
            if (isNormalMode)
            {
                if (battleButton == null)
                {
                    ReportMissingToolkitUI();
                    yield break;
                }

                startNextWavePressed = false;
                SetBattleButtonVisible(true);
                SetBattleButtonEnabled(currentEnemiesAlive == 0);

                if (waveInfoUI != null)
                {
                    waveInfoUI.ShowBuildPhaseWithButton(currentWaveIndex, waves.Count);
                }
                
                while (!startNextWavePressed)
                {
                    yield return null;
                }

                SetBattleButtonEnabled(false);
                SetBattleButtonVisible(false);
            }
            if (!isNormalMode)
            {
                float timer = timeBetweenWaves;

                while (timer > 0)
                {
                    if (waveInfoUI != null)
                    {
                        waveInfoUI.ShowBuildPhaseWithTimer(
                            currentWaveIndex,
                            waves.Count,
                            timer);
                    }

                    timer -= Time.deltaTime;

                    yield return null;
                }
            }

            PhaseEvents.PhaseChanged(GamePhase.Combat);

            if (waveInfoUI != null)
            {
                waveInfoUI.ShowCombatPhase(currentWaveIndex, waves.Count);
            }
            
            Debug.Log("LOADING WAVE: " + currentWave.waveName);

            currentEnemiesAlive = 0;

            int groupsFinished = 0;
            foreach (EnemyMix mix in currentWave.mixedEnemies)
            {
                StartCoroutine(SpawnEnemyGroup(mix, () =>
                {
                    groupsFinished++;
                }));
            }

            while (groupsFinished < currentWave.mixedEnemies.Count)
            {
                yield return new WaitForSeconds(0.2f);
            }

            while (currentEnemiesAlive > 0)
            {
                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log($"YOU SURVIVED TO {currentWave.waveName} !");

            if (currentWaveIndex == waves.Count - 1)
            {
                StartCoroutine(ShowFinalWaveMessage("YOU SURVIVE! "));
            }
            else
            {
                StartCoroutine(ShowFinalWaveMessage("WAVE FINISHED! "));
            }

            currentWaveIndex++;
            if (currentWaveIndex < waves.Count)
            {
                PhaseEvents.PhaseChanged(GamePhase.Preparation);
                
            }
        }

        Debug.Log("YOU WON THE GAME!");
        SetBattleButtonVisible(false);
        OnGameWon?.Invoke();
    }

    IEnumerator ShowFinalWaveMessage(string message)
    {
        SetWaveMessage(message);

        yield return new WaitForSeconds(3f);

        SetWaveMessage("");
    }

    public void StartNextWave()
    {
        if (isNormalMode && currentEnemiesAlive == 0)
        {
            startNextWavePressed = true;
            SetBattleButtonEnabled(false);
        }
    }

    IEnumerator SpawnEnemyGroup(EnemyMix mix, Action onGroupComplete)
    {
        if (mix.delayBeforeStart > 0)
        {
            yield return new WaitForSeconds(mix.delayBeforeStart);
        }

        for (int i = 0; i < mix.enemyCount; i++)
        {
            SpawnEnemyFromPool(mix.enemyType);
            yield return new WaitForSeconds(1f / mix.rate);
        }

        onGroupComplete?.Invoke();
    }

    private void SpawnEnemyFromPool(EnemyType type)
    {
        BaseEnemyAI enemy = enemySpawner.SpawnEnemyFromPoint(type);

        if (enemy != null)
        {
            currentEnemiesAlive++;

            enemy.OnDeath -= OnEnemyCharacterDied;
            enemy.OnDeath += OnEnemyCharacterDied;
        }
    }

    private void OnEnemyCharacterDied(IDamageable damageable)
    {
        if (damageable is BaseEnemyAI enemy)
        {
            enemy.OnDeath -= OnEnemyCharacterDied;
        }

        currentEnemiesAlive--;
        totalKills++;
        UpdateKillsCounter();
    }

    private void UpdateKillsCounter()
    {
        string text = "KILLS: " + totalKills;

        if (killsLabel != null)
            killsLabel.text = text;
    }

    private void ResolveScreenUI()
    {
        UIDocument doc = screenUIDocument != null ? screenUIDocument : FindScreenUIDocument();
        if (doc == null || doc.rootVisualElement == null)
            return;

        screenUIDocument = doc;
        VisualElement root = doc.rootVisualElement;

        waveHud = root.Q<VisualElement>("wave-hud");
        lifePanel = root.Q<VisualElement>("life-panel");
        killsLabel = root.Q<Label>("kills-label");
        waveMessageLabel = root.Q<Label>("wave-message-label");
        battleButton = root.Q<Button>("battle-button");

        if (waveHud != null)
            waveHud.pickingMode = PickingMode.Position;

        if (lifePanel != null)
            lifePanel.pickingMode = PickingMode.Ignore;

        if (killsLabel != null)
            killsLabel.pickingMode = PickingMode.Ignore;

        if (waveMessageLabel != null)
            waveMessageLabel.pickingMode = PickingMode.Ignore;

        if (battleButton != null)
        {
            battleButton.clicked -= StartNextWave;
            battleButton.clicked += StartNextWave;
            battleButton.text = "BATTLE !";
            battleButton.focusable = true;
            battleButton.pickingMode = PickingMode.Position;
        }

        if (battleButton == null || killsLabel == null || waveMessageLabel == null)
            ReportMissingToolkitUI();
    }

    private static UIDocument FindScreenUIDocument()
    {
        foreach (UIDocument doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
        {
            if (doc?.rootVisualElement == null)
                continue;

            VisualElement root = doc.rootVisualElement;
            if (root.Q<VisualElement>("wave-hud") != null
                || root.Q<Button>("battle-button") != null
                || root.Q<Label>("kills-label") != null)
                return doc;
        }

        return null;
    }

    private void SetBattleButtonVisible(bool visible)
    {
        if (battleButton == null)
            return;

        battleButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        battleButton.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

        if (visible)
            battleButton.BringToFront();
    }

    private void SetBattleButtonEnabled(bool enabled)
    {
        if (battleButton == null)
            return;

        battleButton.SetEnabled(enabled);
    }

    private void SetWaveMessage(string message)
    {
        if (waveMessageLabel == null)
            return;

        waveMessageLabel.text = message;
        waveMessageLabel.style.display = string.IsNullOrEmpty(message)
            ? DisplayStyle.None
            : DisplayStyle.Flex;
    }

    private void ReportMissingToolkitUI()
    {
        if (reportedMissingToolkitUI)
            return;

        Debug.LogError("[WaveSystem] ScreenUI UIDocument must provide 'battle-button', 'kills-label' and 'wave-message-label' UI Toolkit elements.", this);
        reportedMissingToolkitUI = true;
    }
}

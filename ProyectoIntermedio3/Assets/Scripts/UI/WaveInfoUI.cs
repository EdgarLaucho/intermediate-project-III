using TMPro;
using UnityEngine;

public class WaveInfoUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField]
    private GameObject panel;

    [Header("Texts")]
    [SerializeField]
    private TextMeshProUGUI waveTitleText;

    [SerializeField]
    private TextMeshProUGUI phaseText;

    [SerializeField]
    private TextMeshProUGUI timerText;

    public void ShowTutorial(string tutorialMessage)
    {
        panel.SetActive(true);

        waveTitleText.text = "Tutorial";
        phaseText.text = tutorialMessage;

        timerText.gameObject.SetActive(false);
    }

    public void ShowBuildPhaseWithTimer(int waveIndex, int totalWaves, float timeRemaining)
    {
        panel.SetActive(true);

        waveTitleText.text = $"Wave {waveIndex + 1} / {totalWaves}";
        phaseText.text = "Construction phase";

        timerText.gameObject.SetActive(true);
        timerText.text = $"Next wave: {Mathf.CeilToInt(timeRemaining)}";
    }

    public void ShowBuildPhaseWithButton(int waveIndex, int totalWaves)
    {
        panel.SetActive(true);

        waveTitleText.text = $"Wave {waveIndex + 1} / {totalWaves}";
        phaseText.text = "Construction phase";

        timerText.gameObject.SetActive(false);
    }

    public void ShowCombatPhase(int waveIndex, int totalWaves)
    {
        panel.SetActive(true);

        waveTitleText.text = $"Wave {waveIndex + 1} / {totalWaves}";
        phaseText.text = "Combat phase";

        timerText.gameObject.SetActive(false);
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}
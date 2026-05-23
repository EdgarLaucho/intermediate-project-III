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

        waveTitleText.text = $"Oleada {waveIndex + 1} / {totalWaves}";
        phaseText.text = "Tiempo de construcción";

        timerText.gameObject.SetActive(true);
        timerText.text = $"Siguiente oleada en: {Mathf.CeilToInt(timeRemaining)}";
    }

    public void ShowBuildPhaseWithButton(int waveIndex, int totalWaves)
    {
        panel.SetActive(true);

        waveTitleText.text = $"Oleada {waveIndex + 1} / {totalWaves}";
        phaseText.text = "Tiempo de construcción";

        timerText.gameObject.SetActive(false);
    }

    public void ShowCombatPhase(int waveIndex, int totalWaves)
    {
        panel.SetActive(true);

        waveTitleText.text = $"Oleada {waveIndex + 1} / {totalWaves}";
        phaseText.text = "Combate";

        timerText.gameObject.SetActive(false);
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}
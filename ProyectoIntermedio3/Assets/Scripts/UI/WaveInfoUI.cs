using System;
using TMPro;
using UnityEngine;

public class WaveInfoUI : MonoBehaviour
{
    [Header(("Panel"))] 
    [SerializeField] 
    private GameObject panel;

    [Header("Texts")] 
    [SerializeField] 
    private TextMeshProUGUI waveTitleText;
    [SerializeField]
    private TextMeshProUGUI phaseText;
    [SerializeField]
    private TextMeshProUGUI timerText;
    [SerializeField]
    private TextMeshProUGUI enemiesText;

    public void ShowTutorial(string tutorialMessage)
    {
        panel.SetActive(true);

        waveTitleText.text = "Tutorial";
        phaseText.text = tutorialMessage;
        timerText.text = "";
        enemiesText.text = "";
    }
    
    public void ShowBuildPhaseWithTimer(int waveIndex, int totalWaves, float timeRemaining, string enemiesInfo)
    {
        panel.SetActive(true);

        waveTitleText.text = $"Oleada {waveIndex + 1} / {totalWaves}";
        phaseText.text = "Tiempo de construcción";
        timerText.text = $"Siguiente oleada en: {Mathf.CeilToInt(timeRemaining)}";
        enemiesText.text = enemiesInfo;
    }

    public void ShowCombatPhase(int waveIndex, int totalWaves, string enemiesInfo)
    {
        panel.SetActive(true);
        
        waveTitleText.text = $"Oleada {waveIndex + 1} / {totalWaves}";
        phaseText.text = "¡Defiende la mesa!";
        timerText.text = "";
        enemiesText.text = enemiesInfo;
    }
    
    public void ShowBuildPhaseWithButton(int waveIndex, int totalWaves, string enemiesInfo)
    {
        panel.SetActive(true);

        waveTitleText.text = $"Oleada {waveIndex + 1} / {totalWaves}";
        phaseText.text = "Tiempo de construcción";
        timerText.text = "Pulsa el botón para iniciar la oleada";
        enemiesText.text = enemiesInfo;
    }
    
    public void Hide()
    {
        panel.SetActive(false);
    }
}

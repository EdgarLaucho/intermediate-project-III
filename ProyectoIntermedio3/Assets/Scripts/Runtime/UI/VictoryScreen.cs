using UnityEngine;
using TMPro;

public class VictoryScreen : MonoBehaviour
{
    [SerializeField] private GameObject victoryPanel;

    private void OnEnable()
    {
        WaveSystem.OnGameWon += ShowVictoryScreen;
    }

    private void OnDisable()
    {
        WaveSystem.OnGameWon -= ShowVictoryScreen;
    }

    private void ShowVictoryScreen()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            Time.timeScale = 0f;
        }
    }
}
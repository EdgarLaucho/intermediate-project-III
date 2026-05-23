using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuScreen : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "TowerDefence";

    public void PlayHardcoreGame()
    {
        PlayerPrefs.SetInt("NormalModeActive", 0);
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void PlayNormalGame()
    {
        PlayerPrefs.SetInt("NormalModeActive", 1);
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
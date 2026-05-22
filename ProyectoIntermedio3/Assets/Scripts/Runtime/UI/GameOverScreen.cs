using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject gameOverPanel;

    [Header("Player Reference")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private Health tableHealth;

    void Start()
    {
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerHealth = player.GetComponent<Health>();
            }
        }

        if (tableHealth == null)
        {
            GameObject table = GameObject.FindWithTag("Base");
            if (table != null)
            {
                tableHealth = table.GetComponent<Health>();
            }
        }

        if (playerHealth != null)
        {
            playerHealth.OnDeath += OnTargetDeath;
        }

        if (tableHealth != null)
        {
            tableHealth.OnDeath += OnTargetDeath;
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= OnTargetDeath;
        }

        if (tableHealth != null)
        { 
            tableHealth.OnDeath -= OnTargetDeath;
        }
    }

    private void OnTargetDeath(IDamageable damageable)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            Time.timeScale = 0f;
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}

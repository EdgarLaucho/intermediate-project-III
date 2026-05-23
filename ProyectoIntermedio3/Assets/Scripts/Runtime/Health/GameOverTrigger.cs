using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public class GameOverTrigger : MonoBehaviour
{
    public enum GameOverReason
    {
        PlayerDied,
        NexusDestroyed
    }

    public static event Action<GameOverReason, GameObject> OnGameOverRequested;

    [SerializeField] private GameOverReason reason;
    [SerializeField] private Health health;

    private static bool gameOverRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        gameOverRequested = false;
        OnGameOverRequested = null;
    }

    public static void ResetGameOverState()
    {
        gameOverRequested = false;
    }

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (health != null)
            health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
    }

    private void HandleDeath(IDamageable damageable)
    {
        if (gameOverRequested)
            return;

        gameOverRequested = true;
        OnGameOverRequested?.Invoke(reason, gameObject);
    }
}
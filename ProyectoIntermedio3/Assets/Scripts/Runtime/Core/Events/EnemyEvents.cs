using System;

// Raised by enemies when they die. The EconomyManager listens here to credit the
// gold reward, keeping enemy and economy code fully decoupled.
public static class EnemyEvents
{
    public static event Action<int> OnEnemyDied;

    // goldReward is defined on the enemy's data asset and passed through so the
    // economy system doesn't need a reference back to the enemy.
    public static void EnemyDied(int goldReward) => OnEnemyDied?.Invoke(goldReward);
}

using System;

public static class EnemyEvents
{
    public static event Action<int> OnEnemyDied;
    public static void EnemyDied(int goldReward) => OnEnemyDied?.Invoke(goldReward);
}
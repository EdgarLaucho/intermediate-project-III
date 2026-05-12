using UnityEngine;

public class EconomyManager : MonoBehaviour, IEconomyService
{
    [SerializeField] private int startingGold = 200;

    [field: SerializeField, ReadOnly]
    public int Gold { get; private set; }

    private void Awake()
    {
        Gold = startingGold;
    }

    private void Start()
    {
        EconomyEvents.GoldChanged(Gold);
    }

    private void OnEnable()
    {
        EnemyEvents.OnEnemyDied += OnEnemyDied;
    }

    private void OnDisable()
    {
        EnemyEvents.OnEnemyDied -= OnEnemyDied;
    }

    public bool CanAfford(int cost) => Gold >= cost;

    public bool SpendGold(int cost)
    {
        if (cost <= 0)
        {
            Debug.LogWarning($"[EconomyManager] SpendGold called with invalid cost ({cost}). Ignored.");
            return false;
        }
        if (!CanAfford(cost)) return false;
        Gold -= cost;
        EconomyEvents.GoldChanged(Gold);
        return true;
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        Gold += amount;
        EconomyEvents.GoldChanged(Gold);
    }

    private void OnEnemyDied(int goldReward) => AddGold(goldReward);
}
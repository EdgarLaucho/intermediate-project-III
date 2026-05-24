public interface IEconomyService
{
    int Gold { get; }
    bool CanAfford(int cost);
    bool SpendGold(int cost);
    void AddGold(int amount);
}
// Abstraction over the economy system so callers (UI, BuildManager, etc.) are not
// coupled to the concrete EconomyManager MonoBehaviour.
public interface IEconomyService
{
    // Queries
    int  Gold      { get; }
    bool CanAfford(int cost);
    bool SpendGold(int cost);
    void AddGold(int amount);
}

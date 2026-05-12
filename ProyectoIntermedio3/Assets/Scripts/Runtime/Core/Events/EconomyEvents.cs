using System;

// Broadcast whenever the player's gold balance changes so UI elements and any
// other listeners can refresh without polling the EconomyManager directly.
public static class EconomyEvents
{
    public static event Action<int> OnGoldChanged;

    // newTotal is the complete current balance, not a delta, so listeners never
    // need to track the previous value to compute the new one.
    public static void GoldChanged(int newTotal) => OnGoldChanged?.Invoke(newTotal);
}
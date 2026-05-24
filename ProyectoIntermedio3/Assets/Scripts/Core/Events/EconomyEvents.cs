using System;

public static class EconomyEvents
{
    public static event Action<int> OnGoldChanged;
    public static void GoldChanged(int newTotal) => OnGoldChanged?.Invoke(newTotal);
}
using System;
using UnityEngine;

public static class PhaseEvents
{
    public static event Action<GamePhase> OnPhaseChanged;
    public static GamePhase CurrentPhase { get; private set; } = GamePhase.Preparation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        OnPhaseChanged = null;
        CurrentPhase = GamePhase.Preparation;
    }

    public static void PhaseChanged(GamePhase phase)
    {
        CurrentPhase = phase;
        OnPhaseChanged?.Invoke(phase);
    }
}
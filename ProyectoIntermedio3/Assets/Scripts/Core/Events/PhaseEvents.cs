using System;

// Global phase-change bus. GameManager raises this when switching between
// Preparation and Combat so every system (BuildManager, UI, enemies, etc.)
// can respond without coupling directly to GameManager.
public static class PhaseEvents
{
    public static event Action<GamePhase> OnPhaseChanged;

    public static void PhaseChanged(GamePhase phase) => OnPhaseChanged?.Invoke(phase);
}
// The two top-level states the game alternates between.
// Preparation: the player manages their base (build, upgrade, repair).
// Combat: enemies are active; some actions may be restricted depending on BuildManager settings.
public enum GamePhase { Preparation, Combat }
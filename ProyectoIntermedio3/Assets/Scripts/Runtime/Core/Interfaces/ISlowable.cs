// Implemented by enemies that can have their movement speed reduced.
// Towers and traps check for this interface after dealing damage so the slow
// is applied only to targets that support it, without a type cast.
public interface ISlowable
{
    // Current multiplier applied to base movement speed (1 = full speed, 0 = stopped).
    float MoveSpeedMultiplier { get; }

    // slowPercent: 0–1 fraction by which speed is reduced (0.3 = 30% slower).
    // duration: how many seconds the slow lasts before fading or expiring.
    void ApplySlow(float slowPercent, float duration);
}

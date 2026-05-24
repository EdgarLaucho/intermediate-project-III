public interface ISlowable
{
    float MoveSpeedMultiplier { get; }

    void ApplySlow(float slowPercent, float duration);
}
using System;

// Implemented by anything that can receive damage and die: buildings, enemies, etc.
// Keeping damage logic behind this interface lets towers and traps apply damage
// without knowing the concrete type of their target.
public interface IDamageable
{
    #region Health Properties

    int  CurrentHealth { get; }
    int  MaxHealth     { get; }
    bool IsAlive       { get; }

    #endregion

    #region Events
    event Action<IDamageable> OnDeath;
    event Action OnHealthChanged;

    #endregion

    #region Methods

    void TakeDamage(int amount);
    void Heal(int amount);

    #endregion
}

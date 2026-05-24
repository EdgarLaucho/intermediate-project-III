using System;

public interface IDamageable
{
    #region Health Properties
    int CurrentHealth { get; }
    int MaxHealth { get; }
    bool IsAlive { get; }

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
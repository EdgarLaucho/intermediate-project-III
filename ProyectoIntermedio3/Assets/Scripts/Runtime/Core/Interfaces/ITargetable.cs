// Marker interface that identifies objects towers and traps are allowed to attack.
// Extending IDamageable means every ITargetable already exposes health and damage
// methods; ITargetable exists as a distinct type so non-targetable damageable
// objects (e.g. destructible scenery) are not accidentally picked up by targeting logic.
public interface ITargetable : IDamageable { }

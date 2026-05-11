using UnityEngine;
using UnityEngine.AI;

// Wall is a purely defensive structure that blocks NavMesh paths and reduces
// incoming damage via its Armor stat. ThornsDamage is returned to melee attackers
// but is applied by the enemy's own combat logic, not by the wall directly.
public class Wall : BuildingBase
{
    #region Wall Stats

    public WallRole Role { get; private set; }
    public int Armor { get; private set; }
    public int ThornsDamage { get; private set; }

    #endregion

    #region Lifecycle

    private void Awake()
    {
        EnsureObstacle();
    }

    public override void Initialize(BuildingData data)
    {
        base.Initialize(data);

        if (data is WallData wallData)
        {
            Role = wallData.role;
            Armor = wallData.armor;
            ThornsDamage = wallData.thornsDamage;
        }
    }

    #endregion

    #region Actions

    // Subtracts Armor from the incoming hit before forwarding to the base class.
    // Mathf.Max(1, ...) ensures the wall can always be destroyed.
    public override void TakeDamage(int amount)
    {
        base.TakeDamage(Mathf.Max(1, amount - Armor));
    }

    #endregion

    #region Upgrade Handling

    protected override void ApplyUpgradeStats(UpgradeLevelData upgradeData)
    {
        Armor += upgradeData.bonusArmor;
        ThornsDamage += upgradeData.bonusThornsDamage;
    }

    #endregion

    #region Private Helpers

    // Adds a BoxCollider and NavMeshObstacle at runtime if they are not already
    // present. This lets wall prefabs be created without pre-configured physics
    // components while still guaranteeing correct blocking behaviour.
    private void EnsureObstacle()
    {
        if (!TryGetComponent(out BoxCollider boxCollider))
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(0.92f, 1.25f, 0.92f);
            boxCollider.center = new Vector3(0f, 0.62f, 0f);
        }

        if (!TryGetComponent(out NavMeshObstacle obstacle))
        {
            obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.size = new Vector3(0.92f, 1.25f, 0.92f);
            obstacle.center = new Vector3(0f, 0.62f, 0f);
        }
    }

    #endregion
}
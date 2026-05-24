using UnityEngine;

public class ChickenPenguinEnemyAI : RangedEnemyAI
{
    [Header("Phase Visuals")]
    [SerializeField]
    private GameObject chickenVisual;

    [SerializeField]
    private GameObject mountedPenguinVisual;

    [SerializeField]
    private GameObject standingPenguinVisual;

    [Header("Second Phase")]
    [SerializeField]
    private int secondPhaseHealth = 100;

    [SerializeField]
    private float secondPhaseSpeed = 5f;


    private bool secondPhase = false;

    protected override void Attack()
    {
        if (!secondPhase)
        {
            BaseMeleeAttack();
            return;
        }

        base.Attack();
    }
    protected override void Die()
    {
        if (!secondPhase)
        {
            EnterSecondPhase();
            return;
        }
        base.Die();
    }

    private void EnterSecondPhase()
    {
        secondPhase = true;

        chickenVisual.SetActive(false);

        if (mountedPenguinVisual != null)
            mountedPenguinVisual.SetActive(false);

        if (standingPenguinVisual != null)
            standingPenguinVisual.SetActive(true);

        MaxHealth = secondPhaseHealth;
        CurrentHealth = MaxHealth;

        if (agent != null)
        {
            SetBaseMoveSpeed(secondPhaseSpeed);
        }
        Debug.Log("Penguin entered second phase");
    }

    public override void Initialize()
    {
        base.Initialize();

        secondPhase = false;
        if (agent != null)
        {
            SetBaseMoveSpeed(enemySO.speed);
        }

        chickenVisual.SetActive(true);

        if (mountedPenguinVisual != null)
            mountedPenguinVisual.SetActive(true);

        if (standingPenguinVisual != null)
            standingPenguinVisual.SetActive(false);
    }
}

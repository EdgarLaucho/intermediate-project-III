using UnityEngine;
using UnityEngine.AI;

public class PlayerAnimationController : MonoBehaviour
{
    private Animator _animator;
    private NavMeshAgent _agent;

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        float speed = _agent.velocity.magnitude;
        _animator.SetFloat(SpeedHash, speed);
    }
}
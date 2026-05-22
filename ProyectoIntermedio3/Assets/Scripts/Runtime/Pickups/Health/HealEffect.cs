using UnityEngine;

public class HealEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem healParticles;

    public void PlayHealEffect()
    {
        if (healParticles != null)
        {
            healParticles.Play();
        }
    }
}
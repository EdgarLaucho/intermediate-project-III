using System;
using System.Collections.Generic;
using UnityEngine;

public static class GamePauseEvents
{
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;

    public static bool IsPaused { get; private set; }
    public static bool CanResume { get; private set; } = true;

    private static readonly Dictionary<Animator, float> PausedAnimatorSpeeds = new();
    private static readonly HashSet<ParticleSystem> PausedParticleSystems = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        OnGamePaused = null;
        OnGameResumed = null;
        IsPaused = false;
        CanResume = true;
        PausedAnimatorSpeeds.Clear();
        PausedParticleSystems.Clear();
        Time.timeScale = 1f;
    }

    public static void PauseGame(bool canResume = true)
    {
        if (!canResume)
            CanResume = false;
        else if (!IsPaused)
            CanResume = true;

        Time.timeScale = 0f;
        PauseSceneAnimation();

        if (IsPaused)
            return;

        IsPaused = true;
        OnGamePaused?.Invoke();
    }

    public static void ResumeGame()
    {
        if (!IsPaused || !CanResume)
            return;

        IsPaused = false;
        CanResume = true;
        Time.timeScale = 1f;
        ResumeSceneAnimation();
        OnGameResumed?.Invoke();
    }

    public static void ClearPauseState()
    {
        IsPaused = false;
        CanResume = true;
        Time.timeScale = 1f;
        ResumeSceneAnimation();
    }

    private static void PauseSceneAnimation()
    {
        foreach (Animator animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude))
        {
            if (animator == null || !animator.enabled)
                continue;

            if (!PausedAnimatorSpeeds.ContainsKey(animator))
                PausedAnimatorSpeeds.Add(animator, animator.speed);

            animator.speed = 0f;
        }

        foreach (ParticleSystem particleSystem in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude))
        {
            if (particleSystem == null)
                continue;

            if (particleSystem.isPlaying || particleSystem.isEmitting)
                PausedParticleSystems.Add(particleSystem);

            particleSystem.Pause(true);
        }
    }

    private static void ResumeSceneAnimation()
    {
        foreach (KeyValuePair<Animator, float> animatorSpeed in PausedAnimatorSpeeds)
        {
            if (animatorSpeed.Key != null)
                animatorSpeed.Key.speed = animatorSpeed.Value;
        }

        PausedAnimatorSpeeds.Clear();

        foreach (ParticleSystem particleSystem in PausedParticleSystems)
        {
            if (particleSystem != null)
                particleSystem.Play(true);
        }

        PausedParticleSystems.Clear();
    }
}
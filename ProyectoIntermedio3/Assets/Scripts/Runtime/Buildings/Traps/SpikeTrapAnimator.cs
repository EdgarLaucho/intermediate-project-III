using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SpikeTrapAnimator : MonoBehaviour
{
    [SerializeField] private Transform[] spikes;
    [SerializeField] private float hiddenDepth = 0.35f;
    [SerializeField] private float riseDuration = 0.08f;
    [SerializeField] private float holdDuration = 0.08f;
    [SerializeField] private float hideDuration = 0.18f;

    private readonly List<SpikeState> spikeStates = new();
    private MotionState state = MotionState.Hidden;
    private float timer;
    private bool configured;

    public float HitDelay => riseDuration;
    public float RemainingDurationAfterHit => holdDuration + hideDuration;
    public float TotalDuration => riseDuration + holdDuration + hideDuration;

    public void SyncToAttackRate(float cooldown)
    {
        var duration = Mathf.Max(0.12f, cooldown);
        riseDuration = Mathf.Clamp(duration * 0.28f, 0.05f, 0.12f);
        holdDuration = Mathf.Clamp(duration * 0.18f, 0.04f, 0.12f);
        hideDuration = Mathf.Max(0.05f, duration - riseDuration - holdDuration);
    }

    public void Initialize()
    {
        ConfigureIfNeeded();
        HideImmediate();
    }

    public void Play()
    {
        ConfigureIfNeeded();
        if (spikeStates.Count == 0) return;

        for (int index = 0; index < spikeStates.Count; index++)
        {
            var spikeState = spikeStates[index];
            spikeState.StartPosition = spikeState.Spike.localPosition;
            spikeStates[index] = spikeState;
        }

        timer = 0f;
        state = MotionState.Rising;
    }

    private void Awake()
    {
        ConfigureIfNeeded();
        HideImmediate();
    }

    private void OnEnable()
    {
        ConfigureIfNeeded();
        if (state == MotionState.Hidden)
            HideImmediate();
    }

    private void Update()
    {
        if (GamePauseEvents.IsPaused || state == MotionState.Hidden)
            return;

        timer += Time.deltaTime;

        switch (state)
        {
            case MotionState.Rising:
                AnimateTowardsShown();
                break;
            case MotionState.Holding:
                HoldShown();
                break;
            case MotionState.Hiding:
                AnimateTowardsHidden();
                break;
        }
    }

    private void ConfigureIfNeeded()
    {
        if (configured) return;

        configured = true;
        spikeStates.Clear();

        Transform[] resolvedSpikes = spikes != null && spikes.Length > 0
            ? spikes
            : FindSpikeTransforms();

        foreach (Transform spike in resolvedSpikes)
        {
            if (spike == null) continue;
            Vector3 shownPosition = spike.localPosition;
            spikeStates.Add(new SpikeState(
                spike,
                shownPosition,
                shownPosition + Vector3.down * hiddenDepth));
        }
    }

    private Transform[] FindSpikeTransforms()
    {
        List<Transform> foundSpikes = new();
        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            var candidate = renderer.transform;
            if (candidate == transform) continue;
            if (!candidate.name.ToLowerInvariant().Contains("spike")) continue;
            foundSpikes.Add(candidate);
        }

        return foundSpikes.ToArray();
    }

    private void HideImmediate()
    {
        for (int index = 0; index < spikeStates.Count; index++)
            spikeStates[index].Spike.localPosition = spikeStates[index].HiddenPosition;

        timer = 0f;
        state = MotionState.Hidden;
    }

    private void ShowImmediate()
    {
        for (int index = 0; index < spikeStates.Count; index++)
            spikeStates[index].Spike.localPosition = spikeStates[index].ShownPosition;
    }

    private void AnimateTowardsShown()
    {
        var t = Smooth01(timer / Mathf.Max(0.01f, riseDuration));
        for (int index = 0; index < spikeStates.Count; index++)
        {
            var spikeState = spikeStates[index];
            spikeState.Spike.localPosition = Vector3.Lerp(spikeState.StartPosition, spikeState.ShownPosition, t);
        }

        if (timer < riseDuration) return;

        ShowImmediate();
        timer = 0f;
        state = MotionState.Holding;
    }

    private void HoldShown()
    {
        ShowImmediate();
        if (timer < holdDuration) return;

        for (int index = 0; index < spikeStates.Count; index++)
        {
            var spikeState = spikeStates[index];
            spikeState.StartPosition = spikeState.Spike.localPosition;
            spikeStates[index] = spikeState;
        }

        timer = 0f;
        state = MotionState.Hiding;
    }

    private void AnimateTowardsHidden()
    {
        var t = Smooth01(timer / Mathf.Max(0.01f, hideDuration));
        for (int index = 0; index < spikeStates.Count; index++)
        {
            var spikeState = spikeStates[index];
            spikeState.Spike.localPosition = Vector3.Lerp(spikeState.StartPosition, spikeState.HiddenPosition, t);
        }

        if (timer < hideDuration) return;
        HideImmediate();
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private enum MotionState
    {
        Hidden,
        Rising,
        Holding,
        Hiding
    }

    private struct SpikeState
    {
        public readonly Transform Spike;
        public readonly Vector3 ShownPosition;
        public readonly Vector3 HiddenPosition;
        public Vector3 StartPosition;

        public SpikeState(Transform spike, Vector3 shownPosition, Vector3 hiddenPosition)
        {
            Spike = spike;
            ShownPosition = shownPosition;
            HiddenPosition = hiddenPosition;
            StartPosition = hiddenPosition;
        }
    }
}
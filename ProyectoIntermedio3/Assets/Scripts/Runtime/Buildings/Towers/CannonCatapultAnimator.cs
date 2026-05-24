using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CannonCatapultAnimator : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] private string armName = "CatapultG_Arm02";
    [SerializeField] private string projectileAnchorName = "ShootPoint";
    [SerializeField] private float launchAngleX = 80f;
    [SerializeField] private float windupDuration = 0.25f;
    [SerializeField] private float reloadDuration = 0.25f;
    [SerializeField] private float loadedProjectileScale = 1f;

    #endregion

    #region State

    private Transform arm;
    private Transform projectileAnchor;
    private GameObject loadedProjectile;
    private GameObject loadedProjectilePrefab;
    private Quaternion restRotation;
    private Quaternion launchRotation;
    private MotionState state = MotionState.Ready;
    private float timer;
    private bool releaseInvoked;
    private Action releaseCallback;
    private Action completeCallback;

    #endregion

    #region Properties

    public bool CanAnimate => arm != null && projectileAnchor != null;

    public bool IsPlaying => state != MotionState.Ready;

    #endregion

    #region Public API

    public void Configure(GameObject projectilePrefab, Transform launchPoint, Transform searchRoot)
    {
        var root = searchRoot != null ? searchRoot : transform;

        if (arm == null || !arm.IsChildOf(root))
            arm = TowerLaunchSockets.FindChildRecursive(root, armName);

        projectileAnchor = launchPoint != null ? launchPoint : TowerLaunchSockets.FindChildRecursive(root, projectileAnchorName);

        if (arm != null)
        {
            restRotation = arm.localRotation;
            var restEuler = restRotation.eulerAngles;
            launchRotation = Quaternion.Euler(restEuler.x + launchAngleX, restEuler.y, restEuler.z);
        }

        loadedProjectilePrefab = projectilePrefab;
        EnsureLoadedProjectile();
        SetLoadedProjectileVisible(enabled && state != MotionState.Reloading);
    }

    public bool Play(Action onRelease, Action onComplete)
    {
        if (!CanAnimate || IsPlaying) return false;

        releaseCallback = onRelease;
        completeCallback = onComplete;
        releaseInvoked = false;
        timer = 0f;
        state = MotionState.Windup;
        SetLoadedProjectileVisible(true);
        return true;
    }

    #endregion

    #region Unity Events

    private void OnEnable()
    {
        SetLoadedProjectileVisible(state != MotionState.Reloading);
    }

    private void OnDisable()
    {
        SetLoadedProjectileVisible(false);
    }

    private void Update()
    {
        if (GamePauseEvents.IsPaused || state == MotionState.Ready || arm == null)
            return;

        timer += Time.deltaTime;

        switch (state)
        {
            case MotionState.Windup:
                UpdateWindup();
                break;
            case MotionState.Reloading:
                UpdateReload();
                break;
        }
    }

    #endregion

    #region Motion

    private void UpdateWindup()
    {
        var t = Smooth01(timer / Mathf.Max(0.01f, windupDuration));
        arm.localRotation = Quaternion.Slerp(restRotation, launchRotation, t);

        if (timer < windupDuration) return;

        arm.localRotation = launchRotation;
        SetLoadedProjectileVisible(false);
        InvokeRelease();
        timer = 0f;
        state = MotionState.Reloading;
    }

    private void UpdateReload()
    {
        var t = Smooth01(timer / Mathf.Max(0.01f, reloadDuration));
        arm.localRotation = Quaternion.Slerp(launchRotation, restRotation, t);

        if (timer < reloadDuration) return;

        arm.localRotation = restRotation;
        state = MotionState.Ready;
        timer = 0f;
        SetLoadedProjectileVisible(true);
        InvokeComplete();
    }

    #endregion

    #region Projectile Visual

    private void EnsureLoadedProjectile()
    {
        if (loadedProjectilePrefab == null || projectileAnchor == null)
        {
            SetLoadedProjectileVisible(false);
            return;
        }

        if (loadedProjectile != null && loadedProjectile.name.StartsWith(loadedProjectilePrefab.name, StringComparison.Ordinal))
        {
            loadedProjectile.transform.SetParent(projectileAnchor, false);
            ResetLoadedProjectileTransform();
            return;
        }

        if (loadedProjectile != null)
            Destroy(loadedProjectile);

        loadedProjectile = Instantiate(loadedProjectilePrefab, projectileAnchor);
        loadedProjectile.name = loadedProjectilePrefab.name + "_LoadedVisual";
        PrepareLoadedProjectileVisual(loadedProjectile);
        ResetLoadedProjectileTransform();
    }

    private void ResetLoadedProjectileTransform()
    {
        if (loadedProjectile == null) return;

        var loadedTransform = loadedProjectile.transform;
        loadedTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        loadedTransform.localScale = Vector3.one * loadedProjectileScale;
    }

    private void SetLoadedProjectileVisible(bool visible)
    {
        if (loadedProjectile != null && loadedProjectile.activeSelf != visible)
            loadedProjectile.SetActive(visible);
    }

    private static void PrepareLoadedProjectileVisual(GameObject visual)
    {
        foreach (MonoBehaviour behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;

        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (Rigidbody rigidbody in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
        }

        foreach (ParticleSystem particles in visual.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Renderer particleRenderer = particles.GetComponent<Renderer>();
            if (particleRenderer != null)
                particleRenderer.enabled = false;
        }
    }

    #endregion

    #region Helpers

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    #endregion

    #region Callbacks

    private void InvokeRelease()
    {
        if (releaseInvoked) return;

        releaseInvoked = true;
        Action callback = releaseCallback;
        releaseCallback = null;
        callback?.Invoke();
    }

    private void InvokeComplete()
    {
        Action callback = completeCallback;
        completeCallback = null;
        callback?.Invoke();
    }

    #endregion

    #region Types

    private enum MotionState
    {
        Ready,
        Windup,
        Reloading
    }

    #endregion
}
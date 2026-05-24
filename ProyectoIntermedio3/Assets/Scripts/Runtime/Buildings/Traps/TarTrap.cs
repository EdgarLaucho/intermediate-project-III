using UnityEngine;
using System.Collections.Generic;

public sealed class TarTrap : Trap
{
    #region Inspector Fields

    [Header("Tar Field Feedback")]
    [SerializeField] private ParticleSystem slowFieldVfxPrefab;
    [SerializeField] private TarMudPatchVisual mudPatchPrefab;

    #endregion

    #region Properties

    public int EffectRadius { get; private set; }

    #endregion

    #region Lifecycle

    public override void Initialize(BuildingData data)
    {
        base.Initialize(data);

        if (data is TrapData td)
            EffectRadius = Mathf.Max(0, td.trapStats.effectRadius);
    }

    #endregion

    #region Trap Virtual Overrides

    public override int GetCollectionRadius() => EffectRadius;

    public override void OnTriggered(IEnumerable<ITargetable> targets)
    {
        if (!IsAlive || RemainingUses <= 0) return;

        PlayActivationFeedback();
        SpawnSlowField();
        TryConsumeUse();

        Debug.Log($"[TarTrap] Released slowing field at {transform.position}. Remaining uses: {RemainingUses}");
    }

    #endregion

    #region Upgrade Handling

    protected override void ApplyUpgradeStats(UpgradeLevelData upgradeData)
    {
        base.ApplyUpgradeStats(upgradeData);
        float statMultiplier = upgradeData.statMultiplier > 0f ? upgradeData.statMultiplier : 1f;
        if (SlowPercent > 0f) SlowPercent = Mathf.Clamp01(SlowPercent * statMultiplier);
        if (EffectDuration > 0f) EffectDuration *= statMultiplier;
        EffectRadius = Mathf.Max(0, EffectRadius + upgradeData.bonusRange);
    }

    #endregion

    #region Private Helpers

    private void SpawnSlowField()
    {
        if (EffectDuration <= 0f || SlowPercent <= 0f) return;

        Vector3 center = SnapToGridCenter(transform.position);
        GameObject fieldObject = new GameObject("__TarSlowField");
        fieldObject.transform.position = center;

        TarSlowField field = fieldObject.AddComponent<TarSlowField>();
        field.Initialize(EffectRadius, EffectDuration, SlowPercent, RuntimeTargetMask);

        if (slowFieldVfxPrefab != null)
        {
            ParticleSystem vfx = Instantiate(slowFieldVfxPrefab, center, slowFieldVfxPrefab.transform.rotation, fieldObject.transform);
            float cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
            float worldSize = (EffectRadius * 2 + 1) * cellSize;
            vfx.transform.localScale = slowFieldVfxPrefab.transform.localScale * worldSize;
            vfx.Play(true);
        }
        else
        {
            var mudVisual = mudPatchPrefab != null
                ? Instantiate(mudPatchPrefab, center, Quaternion.identity, fieldObject.transform)
                : fieldObject.AddComponent<TarMudPatchVisual>();

            mudVisual.Initialize(EffectRadius, EffectDuration);
        }
    }

    private static Vector3 SnapToGridCenter(Vector3 worldPosition)
    {
        GridManager grid = GridManager.Instance;
        return grid != null ? grid.GridToWorld(grid.WorldToGrid(worldPosition)) : worldPosition;
    }

    #endregion
}

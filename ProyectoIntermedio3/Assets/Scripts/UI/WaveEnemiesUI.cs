using System.Collections.Generic;
using UnityEngine;

public class WaveEnemiesUI : MonoBehaviour
{
    [SerializeField] private Transform iconsParent;

    [SerializeField] private EnemyWaveIconUI iconPrefab;

    [SerializeField] private WaveTooltipUI tooltip;

    [SerializeField] private List<EnemyIconInfo> enemiesInfo;

    public void ShowEnemies(List<EnemyMix> enemies)
    {
        foreach (Transform child in iconsParent)
        {
            Destroy(child.gameObject);
        }

        foreach (EnemyMix mix in enemies)
        {
            EnemyIconInfo info =
                enemiesInfo.Find(x => x.enemyType == mix.enemyType);

            if (info == null)
            {
                Debug.LogWarning("No icon info for: " + mix.enemyType);
                continue;
            }

            EnemyWaveIconUI icon =
                Instantiate(iconPrefab, iconsParent);

            icon.Setup(info, tooltip);
        }
    }

    public void Clear()
    {
        foreach (Transform child in iconsParent)
        {
            Destroy(child.gameObject);
        }
    }
}
using System;
using UnityEngine;

[Serializable]
public class DropData
{
    public GameObject pickupPrefab;

    [Range(0f, 100f)]
    public float dropChance = 50f;
}
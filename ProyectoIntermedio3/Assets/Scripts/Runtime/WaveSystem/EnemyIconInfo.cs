using UnityEngine;

[CreateAssetMenu(fileName = "EnemyIconInfo", menuName = "Waves/Enemy Icon Info")]
public class EnemyIconInfo : ScriptableObject
{
    public EnemyType enemyType;
    public Sprite icon;
    public string enemyName;
    [TextArea] public string description;
}

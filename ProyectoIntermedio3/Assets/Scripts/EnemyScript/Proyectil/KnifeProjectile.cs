using UnityEngine;

public class KnifeProjectile : EnemyProjectileBase
{
    protected override void Update()
    {
        base.Update();
        
        transform.Rotate(0f,0f,500f * Time.deltaTime);
    }
}

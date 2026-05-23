using UnityEngine;

public class RotatingProjectile : Projectile
{
    [SerializeField]
    private float rotationSpeed = -720f;

    protected override void Move()
    {
        currentTravelDistance += speed * Time.deltaTime;

        float progress = currentTravelDistance / journeyLength;

        progress = Mathf.Clamp01(progress);

        Vector3 targetPosition = target.position;

        Vector3 nextPosition = Vector3.Lerp(
            startPosition,
            targetPosition,
            progress);

        nextPosition.y += Mathf.Sin(progress * Mathf.PI) * arcHeight;

        transform.position = nextPosition;

        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

        if (progress >= 1f)
        {
            HitTarget();
        }
    }
}
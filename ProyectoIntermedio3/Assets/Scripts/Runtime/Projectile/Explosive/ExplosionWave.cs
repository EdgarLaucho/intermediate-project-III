using System.Collections;
using UnityEngine;

public class ExplosionWave : MonoBehaviour
{
    [SerializeField]
    private float duration = 0.4f;

    [SerializeField]
    private float maxScale = 6f;

    private Material material;

    private void Awake()
    {
        material = GetComponent<MeshRenderer>().material;

        transform.localScale = Vector3.zero;

        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        var timer = 0f;
        var startColor = new Color(1f, 1f, 0f, 0.8f);
        var endColor = new Color(1f, 0f, 0f, 0f);

        while (timer < duration)
        {
            if (GamePauseEvents.IsPaused)
            {
                yield return null;
                continue;
            }

            timer += Time.deltaTime;

            var progress = timer / duration;
            var scale = Mathf.Lerp(0f, maxScale, progress);

            transform.localScale = Vector3.one * scale;
            material.color = Color.Lerp(startColor, endColor, progress);

            yield return null;
        }

        Destroy(gameObject);
    }
}
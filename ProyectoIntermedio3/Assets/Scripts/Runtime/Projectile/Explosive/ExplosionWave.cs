using System.Collections;
using UnityEngine;

public class ExplosionWave : MonoBehaviour
{
    [SerializeField]
    private float duration = 0.4f;

    [SerializeField]
    private float maxScale = 6f;

    private Material material;

    private AudioSource audioSource;

    private void Awake()
    {
        material = GetComponent<MeshRenderer>().material;
        audioSource = GetComponent<AudioSource>();
        transform.localScale = Vector3.zero;

        PlayExplosionSound();
        StartCoroutine(Animate());
    }

    private void PlayExplosionSound()
    {
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }

    private IEnumerator Animate()
    {
        float timer = 0f;
        Color startColor = new Color(1f, 1f, 0f, 0.8f);
        Color endColor = new Color(1f, 0f, 0f, 0f);

        while (timer < duration)
        {
            if (GamePauseEvents.IsPaused)
            {
                yield return null;
                continue;
            }

            timer += Time.deltaTime;

            float progress = timer / duration;
            float scale = Mathf.Lerp(0f, maxScale, progress);

            transform.localScale = Vector3.one * scale;
            material.color = Color.Lerp(startColor, endColor, progress);

            yield return null;
        }

        Destroy(gameObject);
    }
}
using UnityEngine;

// Optional audio companion for RadialMenu. Attach to the same GameObject and assign any clips.
// Missing clips are silently ignored — all audio fields are optional.
// Each playback randomizes pitch within pitchJitter for a more natural, organic feel.
[RequireComponent(typeof(AudioSource))]
public class RadialAudio : MonoBehaviour
{
    #region Inspector Fields
    [Header("Clips (all optional)")]
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float openVolume  = 0.55f;
    [SerializeField, Range(0f, 1f)] private float closeVolume = 0.45f;
    [SerializeField, Range(0f, 0.2f)] private float pitchJitter = 0.05f;

    #endregion

    #region Runtime State

    private AudioSource _src;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.playOnAwake = false;
    }

    #endregion

    #region Public API

    public void PlayHover() => Play(hoverClip, hoverVolume);
    public void PlayClick() => Play(clickClip, clickVolume);
    public void PlayOpen()  => Play(openClip,  openVolume);
    public void PlayClose() => Play(closeClip, closeVolume);

    #endregion

    #region Internal Playback

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || _src == null) return;
        _src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        _src.PlayOneShot(clip, volume);
    }

    #endregion
}
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BuildActionAudio : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip constructClip;
    [SerializeField] private AudioClip demolishClip;
    [SerializeField] private AudioClip repairClip;
    [SerializeField] private AudioClip upgradeClip;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float constructVolume = 0.75f;
    [SerializeField, Range(0f, 1f)] private float demolishVolume = 0.75f;
    [SerializeField, Range(0f, 1f)] private float repairVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float upgradeVolume = 0.70f;
    [SerializeField, Range(0f, 0.2f)] private float pitchJitter = 0.04f;

    private AudioSource _source;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
    }

    private void OnEnable()
    {
        ConstructionEvents.OnBuildingPlaced += HandleBuildingPlaced;
        ConstructionEvents.OnBuildingDemolished += HandleBuildingDemolished;
        ConstructionEvents.OnBuildingRepaired += HandleBuildingRepaired;
        ConstructionEvents.OnBuildingUpgraded += HandleBuildingUpgraded;
    }

    private void OnDisable()
    {
        ConstructionEvents.OnBuildingPlaced -= HandleBuildingPlaced;
        ConstructionEvents.OnBuildingDemolished -= HandleBuildingDemolished;
        ConstructionEvents.OnBuildingRepaired -= HandleBuildingRepaired;
        ConstructionEvents.OnBuildingUpgraded -= HandleBuildingUpgraded;
    }

    private void HandleBuildingPlaced(BuildingActionArgs args) => Play(constructClip, constructVolume);
    private void HandleBuildingDemolished(Vector2Int coords) => Play(demolishClip, demolishVolume);
    private void HandleBuildingRepaired(BuildingActionArgs args) => Play(repairClip, repairVolume);
    private void HandleBuildingUpgraded(BuildingActionArgs args) => Play(upgradeClip, upgradeVolume);

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || _source == null) return;

        _source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        _source.PlayOneShot(clip, volume);
    }
}

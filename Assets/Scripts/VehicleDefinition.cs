using UnityEngine;

[CreateAssetMenu(menuName = "Stagebound/Vehicles/Vehicle Definition")]
public class VehicleDefinition : ScriptableObject
{
    [Header("Identity")]
    public string vehicleId;
    public string displayName;
    public string manufacturer;
    public string drivetrainLabel = "RWD";
    [TextArea(2,5)] public string description;
    public bool selectable = true;
    public string unavailableLabel = "COMING SOON / DEVELOPMENT";
    [Header("Playable content")]
    public VehicleConfig config;
    public VehicleVisualSetup visualPrefab;
    public Sprite previewImage;
    [Tooltip("Optional first engine-loop override. Empty preserves the shared rig audio.")]
    public AudioClip engineLoop;
    [Header("Display stats only (never used by physics)")]
    [Range(0,1)] public float power = .55f;
    [Range(0,1)] public float handling = .8f;
    [Range(0,1)] public float drift = .9f;
    [Range(0,1)] public float stability = .6f;

    public bool IsPlayable => selectable && !string.IsNullOrWhiteSpace(vehicleId) &&
        config != null && config.IsValid(out _) && visualPrefab != null && visualPrefab.IsComplete(out _);
}


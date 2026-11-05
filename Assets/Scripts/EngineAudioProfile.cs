using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Stagebound/Audio/Engine Profile")]
public class EngineAudioProfile : ScriptableObject
{
    [Serializable]
    public class Layer
    {
        public AudioClip clip;
        [Tooltip("Phase-matched intake/exhaust loop under load. Optional for older profiles.")]
        public AudioClip loadedClip;
        [Min(100f)] public float recordedRPM = 900f;
        [Range(0f, 1f)] public float gain = 1f;
    }

    [Tooltip("Loops in ascending recorded RPM order. Each loop must have a seamless boundary.")]
    public Layer[] layers = Array.Empty<Layer>();
    [Header("Optional procedural support (silent by default)")]
    public AudioClip supportingLoop;
    [Min(100f)] public float supportingRPM = 4500f;
    [Range(0f, .08f)] public float supportingGain = 0f;
    [Range(0f, 1f)] public float idleVolume = .28f;
    [Range(0f, 1f)] public float loadedVolume = .78f;
    [Min(.01f)] public float rpmSmoothTime = .055f;
    [Min(.01f)] public float volumeSmoothTime = .07f;
    [Header("Intake and exhaust brightness")]
    [Range(80f, 22000f)] public float idleCutoff = 3500f;
    [Range(80f, 22000f)] public float highRPMCutoff = 14000f;
    [Range(.1f, 1f)] public float coastBrightness = .65f;
    [Range(.1f, 3f)] public float minimumPitch = .35f;
    [Range(.1f, 3f)] public float maximumPitch = 3f;
    [Range(0f, .1f)] public float limiterRange = .025f;
    [Min(1f)] public float limiterFrequency = 12f;
    [Range(0f, 1f)] public float limiterDepth = .55f;
    [Range(0f, 1f)] public float spatialBlend = .8f;
    [Min(.1f)] public float minDistance = 2.3f;
    [Min(.1f)] public float maxDistance = 30f;
}

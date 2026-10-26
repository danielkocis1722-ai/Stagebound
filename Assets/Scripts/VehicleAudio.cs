using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(150)]
[RequireComponent(typeof(CarController), typeof(WheelEffects))]
public class VehicleAudio : MonoBehaviour
{
    [Serializable]
    public class EngineLayer
    {
        public AudioClip clip;
        [Tooltip("Optional existing source. Otherwise a child source is created.")]
        public AudioSource source;
        [Range(0f, 1f)] public float gain = 1f;
        [Range(.25f, 2f)] public float pitchMultiplier = 1f;
        [NonSerialized] public bool paused;
    }

    [Header("Engine - add layers here as clips become available")]
    public EngineLayer[] engineLayers = { new EngineLayer() };
    [Range(.1f, 3f)] public float minPitch = .7f;
    [Range(.1f, 3f)] public float maxPitch = 2.2f;
    [Range(0f, 1f)] public float idleVolume = .22f;
    [Range(0f, 1f)] public float loadedVolume = .8f;
    [Min(.1f)] public float pitchResponse = 5f;
    [Min(.1f)] public float volumeResponse = 7f;
    [Header("Tire screech")]
    public AudioClip screechClip;
    public AudioSource screechSource;
    [Min(0f)] public float screechSlipStart = .25f;
    [Min(.01f)] public float screechSlipFull = 1f;
    [Min(0f)] public float screechMinSpeedKmh = 4f;
    [Range(0f, 1f)] public float maxScreechVolume = .75f;
    [Min(.1f)] public float screechFadeResponse = 8f;
    public Vector2 screechPitchRange = new Vector2(.9f, 1.15f);
    [Header("Spatial audio")]
    [Range(0f, 1f)] public float spatialBlend = .8f;
    [Min(.1f)] public float minDistance = 5f;
    [Min(1f)] public float maxDistance = 65f;
    CarController car;
    WheelEffects wheels;
    bool audioPaused, screechPaused;
    readonly List<GameObject> ownedSources = new List<GameObject>();

    void Awake()
    {
        car = GetComponent<CarController>(); wheels = GetComponent<WheelEffects>();
        if (engineLayers == null) engineLayers = Array.Empty<EngineLayer>();
        for (int i = 0; i < engineLayers.Length; i++)
        {
            var layer = engineLayers[i];
            if (layer == null) continue;
            layer.source = PrepareSource(layer.source, layer.clip, "Engine Layer " + (i + 1));
            layer.source.pitch = Mathf.Clamp(minPitch * layer.pitchMultiplier, .1f, 3f);
        }
        screechSource = PrepareSource(screechSource, screechClip, "Tire Screech");
        screechSource.pitch = Mathf.Clamp(screechPitchRange.x, .1f, 3f);
    }

    AudioSource PrepareSource(AudioSource source, AudioClip clip, string title)
    {
        if (source == null)
        {
            var obj = new GameObject(title); obj.transform.SetParent(transform, false);
            source = obj.AddComponent<AudioSource>(); ownedSources.Add(obj);
        }
        source.Stop(); source.playOnAwake = false; source.loop = true;
        if (clip != null) source.clip = clip;
        source.volume = 0; source.spatialBlend = spatialBlend;
        source.dopplerLevel = 0; source.minDistance = minDistance;
        source.maxDistance = Mathf.Max(minDistance, maxDistance);
        return source;
    }

    void Update()
    {
        bool paused = PauseMenu.IsPaused || Time.timeScale <= 0f;
        if (paused != audioPaused) SetPaused(paused);
        if (paused) return;
        float pitchBlend = 1f - Mathf.Exp(-pitchResponse * Time.deltaTime);
        float volumeBlend = 1f - Mathf.Exp(-volumeResponse * Time.deltaTime);
        float rpm = Mathf.InverseLerp(car.idleRPM, Mathf.Max(car.idleRPM + 1, car.redlineRPM), car.EngineRPM);
        float pitch = Mathf.Lerp(minPitch, maxPitch, rpm);
        float volume = Mathf.Lerp(idleVolume, loadedVolume, Mathf.Clamp01(car.ThrottleInput));
        foreach (var layer in engineLayers)
        {
            if (layer == null || layer.source == null) continue;
            // A missing clip is intentionally silent; assigning a clip in Play Mode also works.
            if (layer.clip != null && layer.source.clip != layer.clip) layer.source.clip = layer.clip;
            UpdateLoop(layer.source, volume * layer.gain, pitch * layer.pitchMultiplier, volumeBlend, pitchBlend);
        }

        if (screechSource == null) return;
        if (screechClip != null && screechSource.clip != screechClip) screechSource.clip = screechClip;
        float intensity = wheels.isActiveAndEnabled && wheels.AnyGrounded && wheels.ContactSpeedKmh >= screechMinSpeedKmh
            ? WheelEffects.SlipIntensity(wheels.StrongestSlip, screechSlipStart, screechSlipFull) : 0f;
        float speed = Mathf.Clamp01(wheels.ContactSpeedKmh / 120f);
        float targetVolume = maxScreechVolume * intensity * Mathf.Lerp(.4f, 1f, speed);
        float targetPitch = Mathf.Lerp(screechPitchRange.x, screechPitchRange.y, (intensity + speed) * .5f);
        float fade = 1f - Mathf.Exp(-screechFadeResponse * Time.deltaTime);
        UpdateLoop(screechSource, targetVolume, targetPitch, fade, pitchBlend, false);
    }

    static void UpdateLoop(AudioSource source, float volume, float pitch, float volumeBlend, float pitchBlend, bool playWhenSilent = true)
    {
        if (source.clip == null) { source.volume = 0; return; }
        source.volume = Mathf.Lerp(source.volume, Mathf.Clamp01(volume), volumeBlend);
        if (volume <= 0 && source.volume < .001f) source.volume = 0;
        source.pitch = Mathf.Lerp(source.pitch, Mathf.Clamp(pitch, .1f, 3f), pitchBlend);
        if (!playWhenSilent && volume <= 0 && source.volume == 0)
        {
            if (source.isPlaying) source.Stop();
            return;
        }
        if (!source.isPlaying && source.isActiveAndEnabled) source.Play();
    }

    void SetPaused(bool paused)
    {
        audioPaused = paused;
        foreach (var layer in engineLayers)
        {
            if (layer == null || layer.source == null) continue;
            if (paused) { layer.paused = layer.source.isPlaying; if (layer.paused) layer.source.Pause(); }
            else { if (layer.paused) layer.source.UnPause(); layer.paused = false; }
        }
        if (screechSource == null) return;
        if (paused) { screechPaused = screechSource.isPlaying; if (screechPaused) screechSource.Pause(); }
        else { if (screechPaused) screechSource.UnPause(); screechPaused = false; }
    }

    void OnDisable()
    {
        foreach (var layer in engineLayers)
            if (layer != null && layer.source != null) { layer.source.Stop(); layer.source.volume = 0; layer.paused = false; }
        if (screechSource != null) { screechSource.Stop(); screechSource.volume = 0; }
        audioPaused = screechPaused = false;
    }

    void OnDestroy()
    {
        foreach (var obj in ownedSources) if (obj != null) Destroy(obj);
    }
}

using UnityEngine;

// Reads telemetry only. A vehicle's audio profile owns its loops and tuning.
[DisallowMultipleComponent]
[RequireComponent(typeof(CarController))]
[DefaultExecutionOrder(160)]
public class EngineAudioController : MonoBehaviour
{
    public EngineAudioProfile profile;

    private CarController car;
    private AudioSource[] sources;
    private AudioSource[] loadedSources;
    private AudioLowPassFilter[] filters, loadedFilters;
    private AudioSource supportingSource;
    private AudioLowPassFilter supportingFilter;
    private float supportingEnvelope;
    private float[] envelopes;
    private float[] loadedEnvelopes;
    private float smoothedRPM, smoothedLoad, limiterPhase;
    private bool paused;

    private void Start()
    {
        car = GetComponent<CarController>();
        if (profile == null || profile.layers == null || profile.layers.Length == 0)
        {
            enabled = false;
            return;
        }

        float previousRPM = 0f;
        foreach (var layer in profile.layers)
        {
            if (layer == null || layer.clip == null || layer.recordedRPM <= previousRPM)
            {
                Debug.LogError("Engine audio needs valid loops in ascending recorded RPM order.", this);
                enabled = false;
                return;
            }
            previousRPM = layer.recordedRPM;
        }
        sources = new AudioSource[profile.layers.Length];
        loadedSources = new AudioSource[sources.Length];
        filters = new AudioLowPassFilter[sources.Length];
        loadedFilters = new AudioLowPassFilter[sources.Length];
        envelopes = new float[sources.Length];
        loadedEnvelopes = new float[sources.Length];
        smoothedRPM = Mathf.Max(car.idleRPM, car.EngineRPM);
        for (int i = 0; i < sources.Length; i++)
        {
            var layer = profile.layers[i];
            sources[i] = CreateSource(layer.clip, layer.recordedRPM, out filters[i]);
            if (layer.loadedClip != null)
                loadedSources[i] = CreateSource(layer.loadedClip, layer.recordedRPM, out loadedFilters[i]);
        }
        if (profile.supportingLoop != null)
            supportingSource = CreateSource(profile.supportingLoop, profile.supportingRPM, out supportingFilter);

        // Start together and keep silent layers running throughout crossfades.
        double startTime = AudioSettings.dspTime + .05;
        foreach (var source in sources)
            if (source != null)
                source.PlayScheduled(startTime);
        foreach (var source in loadedSources)
            if (source != null)
                source.PlayScheduled(startTime);
        if (supportingSource != null)
            supportingSource.PlayScheduled(startTime);
    }

    private AudioSource CreateSource(AudioClip clip, float recordedRPM, out AudioLowPassFilter filter)
    {
        var child = new GameObject("Engine Audio " + clip.name);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        source.pitch = Pitch(recordedRPM);
        source.dopplerLevel = 0f;
        source.spatialBlend = profile.spatialBlend;
        source.minDistance = profile.minDistance;
        source.maxDistance = Mathf.Max(profile.minDistance, profile.maxDistance);
        filter = child.AddComponent<AudioLowPassFilter>();
        filter.cutoffFrequency = profile.idleCutoff;
        filter.lowpassResonanceQ = 1f;
        return source;
    }

    private void SetPaused(bool value)
    {
        SetVoicesPaused(sources, value);
        SetVoicesPaused(loadedSources, value);
        if (supportingSource != null)
        {
            if (value) supportingSource.Pause();
            else supportingSource.UnPause();
        }
    }

    private static void SetVoicesPaused(AudioSource[] voices, bool value)
    {
        foreach (var source in voices)
            if (source != null)
            {
                if (value) source.Pause();
                else source.UnPause();
            }
    }

    private float Pitch(float recordedRPM) =>
        Mathf.Clamp(smoothedRPM / Mathf.Max(100f, recordedRPM), profile.minimumPitch, profile.maximumPitch);

    private void Update()
    {
        if (sources == null)
            return;
        bool shouldPause = PauseMenu.IsPaused || Time.timeScale <= 0f;
        if (paused != shouldPause)
        {
            paused = shouldPause;
            SetPaused(paused);
        }
        if (paused)
            return;

        float rpmBlend = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(.01f, profile.rpmSmoothTime));
        float volumeBlend = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(.01f, profile.volumeSmoothTime));
        smoothedRPM = Mathf.Lerp(smoothedRPM, Mathf.Max(0f, car.EngineRPM), rpmBlend);
        smoothedLoad = Mathf.Lerp(smoothedLoad, Mathf.Clamp01(car.ThrottleInput), volumeBlend);
        float volume = Mathf.Lerp(profile.idleVolume, profile.loadedVolume, smoothedLoad);
        volume *= Mathf.SmoothStep(0f, 1f, smoothedRPM / Mathf.Max(1f, car.idleRPM));
        float rpmAmount = Mathf.InverseLerp(car.idleRPM, car.redlineRPM, smoothedRPM);
        float cutoff = Mathf.Clamp(Mathf.Lerp(profile.idleCutoff, profile.highRPMCutoff, rpmAmount) *
            Mathf.Lerp(profile.coastBrightness, 1f, smoothedLoad), 80f, 22000f);

        int lower = 0;
        while (lower < sources.Length - 1 && smoothedRPM >= profile.layers[lower + 1].recordedRPM)
            lower++;
        int upper = Mathf.Min(lower + 1, sources.Length - 1);
        float crossfade = lower == upper ? 0f : Mathf.InverseLerp(profile.layers[lower].recordedRPM,
            profile.layers[upper].recordedRPM, smoothedRPM);

        // Audio-only rounded cuts near the real redline. Never write to engine RPM.
        limiterPhase = Mathf.Repeat(limiterPhase + Time.deltaTime * profile.limiterFrequency, 1f);
        float limiterAmount = Mathf.InverseLerp(car.redlineRPM * (1f - profile.limiterRange),
            car.redlineRPM, smoothedRPM) * Mathf.InverseLerp(.5f, .9f, smoothedLoad);
        float limiter = 1f - profile.limiterDepth * limiterAmount *
            (.5f - .5f * Mathf.Cos(limiterPhase * Mathf.PI * 2f));

        for (int i = 0; i < sources.Length; i++)
        {
            var source = sources[i];
            if (source == null)
                continue;
            float weight = i == lower ? Mathf.Cos(crossfade * Mathf.PI * .5f)
                : i == upper ? Mathf.Sin(crossfade * Mathf.PI * .5f) : 0f;
            source.pitch = Pitch(profile.layers[i].recordedRPM);
            float layerVolume = volume * weight * profile.layers[i].gain;
            // Load pairs share combustion phase, so a linear blend avoids a mid-load boost.
            float loadedWeight = loadedSources[i] != null ? smoothedLoad : 0f;
            float targetVolume = layerVolume * (1f - loadedWeight);
            // Smooth the base envelope while the limiter supplies a continuous modulation.
            envelopes[i] = Mathf.Lerp(envelopes[i], targetVolume, volumeBlend);
            source.volume = envelopes[i] * limiter;
            filters[i].cutoffFrequency = cutoff;
            if (loadedSources[i] != null)
            {
                loadedSources[i].pitch = source.pitch;
                loadedEnvelopes[i] = Mathf.Lerp(loadedEnvelopes[i], layerVolume * loadedWeight, volumeBlend);
                loadedSources[i].volume = loadedEnvelopes[i] * limiter;
                loadedFilters[i].cutoffFrequency = cutoff;
            }
        }
        if (supportingSource != null)
        {
            float gain = Mathf.Clamp(profile.supportingGain, 0f, .08f);
            supportingEnvelope = Mathf.Lerp(supportingEnvelope, volume * gain * smoothedLoad, volumeBlend);
            supportingSource.volume = supportingEnvelope * limiter;
            supportingSource.pitch = Pitch(profile.supportingRPM);
            supportingFilter.cutoffFrequency = Mathf.Min(cutoff, 2500f);
        }
    }

    private void OnDisable()
    {
        if (sources == null)
            return;
        SetPaused(true);
    }

    private void OnEnable()
    {
        if (sources == null || paused)
            return;
        SetPaused(false);
    }

    private void OnDestroy()
    {
        if (sources == null)
            return;
        foreach (var source in sources)
            if (source != null)
                Destroy(source.gameObject);
        foreach (var source in loadedSources)
            if (source != null)
                Destroy(source.gameObject);
        if (supportingSource != null)
            Destroy(supportingSource.gameObject);
    }
}

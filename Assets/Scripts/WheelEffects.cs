using System;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CarController), typeof(SkidmarkSystem))]
public class WheelEffects : MonoBehaviour
{
    [Serializable]
    public class Wheel
    {
        public WheelCollider collider;
        [Tooltip("Optional custom smoke system. Leave empty to create the starter effect.")]
        public ParticleSystem smoke;
        [Min(.05f)] public float markWidth = .32f;
        [Min(0f)] public float smokeMultiplier = 1f;
        [NonSerialized] public float markIntensity;
        [NonSerialized] public float emissionRate;
        [NonSerialized] public bool hasPrevious;
        [NonSerialized] public Vector3 previousPoint, previousNormal;
        [NonSerialized] public float previousIntensity;
        [NonSerialized] public bool ownsSmoke;
    }

    public Wheel[] wheels = Array.Empty<Wheel>();
    [Header("Skidmarks")]
    [Min(0f)] public float markSlipStart = .2f;
    [Min(.01f)] public float markSlipFull = .85f;
    [Min(0f)] public float markMinSpeedKmh = 5f;
    [Min(.1f)] public float markResponse = 10f;
    [Header("Tire smoke")]
    public Material smokeMaterial;
    public bool createSmokeWhenMissing = true;
    [Min(0f)] public float smokeSlipStart = .3f;
    [Min(.01f)] public float smokeSlipFull = 1f;
    [Min(0f)] public float smokeMinSpeedKmh = 2f;
    [Min(.1f)] public float smokeFullSpeedKmh = 60f;
    [Min(0f)] public float maxEmissionRate = 65f;
    [Min(.1f)] public float smokeResponse = 10f;
    [Min(0f)] public float contactOffset = .06f;
    public float StrongestSlip { get; private set; }
    public float ContactSpeedKmh { get; private set; }
    public bool AnyGrounded { get; private set; }
    CarController car;
    SkidmarkSystem marks;
    Material runtimeSmokeMaterial;
    Texture2D smokeTexture;

    void Awake()
    {
        car = GetComponent<CarController>(); marks = GetComponent<SkidmarkSystem>();
        if (wheels == null || wheels.Length == 0)
            wheels = new[] { new Wheel { collider = car.frontLeft }, new Wheel { collider = car.frontRight },
                new Wheel { collider = car.backLeft }, new Wheel { collider = car.backRight } };
        foreach (var wheel in wheels)
        {
            if (wheel == null || wheel.collider == null) continue;
            if (wheel.smoke == null && createSmokeWhenMissing && smokeMaterial != null)
                wheel.smoke = CreateSmoke(wheel);
            if (wheel.smoke == null) continue;
            wheel.smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = wheel.smoke.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false;
            main.loop = true;
            var emission = wheel.smoke.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.burstCount = 0;
        }
    }

    void FixedUpdate()
    {
        StrongestSlip = 0f; ContactSpeedKmh = 0f; AnyGrounded = false;
        foreach (var wheel in wheels)
        {
            if (wheel == null || wheel.collider == null) continue;
            if (!wheel.collider.GetGroundHit(out var hit))
            {
                wheel.hasPrevious = false; wheel.markIntensity = 0f;
                SetSmoke(wheel, 0f); continue;
            }
            AnyGrounded = true;
            float slip = Mathf.Max(Mathf.Abs(hit.sidewaysSlip), Mathf.Abs(hit.forwardSlip));
            float surfaceSpeed = Mathf.Abs(wheel.collider.rpm) * wheel.collider.radius * 2f * Mathf.PI * .06f;
            float speed = Mathf.Max(Mathf.Abs(car.SpeedKmh), surfaceSpeed);
            StrongestSlip = Mathf.Max(StrongestSlip, slip);
            ContactSpeedKmh = Mathf.Max(ContactSpeedKmh, speed);

            float targetMark = Mathf.Abs(car.SpeedKmh) >= markMinSpeedKmh ? SlipIntensity(slip, markSlipStart, markSlipFull) : 0f;
            wheel.markIntensity = Mathf.Lerp(wheel.markIntensity, targetMark, 1f - Mathf.Exp(-markResponse * Time.fixedDeltaTime));
            if (targetMark > 0f && wheel.markIntensity > .01f)
            {
                if (wheel.hasPrevious)
                {
                    if (Vector3.Distance(wheel.previousPoint, hit.point) > marks.maxSegmentLength)
                        wheel.hasPrevious = false;
                    else if (marks.AddSegment(wheel.previousPoint, hit.point, wheel.previousNormal, hit.normal,
                        wheel.previousIntensity, wheel.markIntensity, wheel.markWidth))
                        wheel.hasPrevious = false;
                }
                if (!wheel.hasPrevious)
                {
                    wheel.previousPoint = hit.point; wheel.previousNormal = hit.normal;
                    wheel.previousIntensity = wheel.markIntensity; wheel.hasPrevious = true;
                }
            }
            else wheel.hasPrevious = false;

            if (wheel.smoke != null)
            {
                wheel.smoke.transform.SetPositionAndRotation(hit.point + hit.normal * contactOffset,
                    Quaternion.FromToRotation(Vector3.forward, hit.normal));
                float amount = speed >= smokeMinSpeedKmh ? SlipIntensity(slip, smokeSlipStart, smokeSlipFull) : 0f;
                float speedFactor = Mathf.InverseLerp(smokeMinSpeedKmh, Mathf.Max(smokeMinSpeedKmh + .1f, smokeFullSpeedKmh), speed);
                SetSmoke(wheel, amount * Mathf.Lerp(.25f, 1f, speedFactor) * maxEmissionRate * wheel.smokeMultiplier);
            }
        }
    }

    public static float SlipIntensity(float slip, float start, float full)
    {
        float t = Mathf.InverseLerp(start, Mathf.Max(start + .001f, full), Mathf.Abs(slip));
        return t * t * (3f - 2f * t);
    }

    void SetSmoke(Wheel wheel, float targetRate)
    {
        if (wheel.smoke == null) return;
        // Cut fresh emission under grip/airborne; existing particles fade naturally.
        wheel.emissionRate = targetRate <= 0 ? 0 : Mathf.Lerp(wheel.emissionRate, targetRate,
            1f - Mathf.Exp(-smokeResponse * Time.fixedDeltaTime));
        var emission = wheel.smoke.emission;
        emission.rateOverTime = wheel.emissionRate;
        if (wheel.emissionRate > 0 && !wheel.smoke.isPlaying) wheel.smoke.Play();
    }

    ParticleSystem CreateSmoke(Wheel wheel)
    {
        if (runtimeSmokeMaterial == null)
        {
            runtimeSmokeMaterial = new Material(smokeMaterial);
            smokeTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Soft tire smoke", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[32 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float radius = new Vector2((x + .5f) / 16f - 1f, (y + .5f) / 16f - 1f).magnitude;
                pixels[y * 32 + x] = new Color32(255, 255, 255, (byte)(Mathf.Pow(Mathf.Clamp01(1f - radius), 2f) * 255));
            }
            smokeTexture.SetPixels32(pixels); smokeTexture.Apply(false, true);
            runtimeSmokeMaterial.SetTexture("_BaseMap", smokeTexture);
        }
        var obj = new GameObject("Tire Smoke - " + wheel.collider.name);
        obj.transform.SetParent(transform, false);
        var smoke = obj.AddComponent<ParticleSystem>();
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = smoke.main;
        main.playOnAwake = false; main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.65f, 1.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(.35f, .65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.35f, .9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.maxParticles = 180; main.gravityModifier = -.035f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = smoke.emission; emission.rateOverTime = 0;
        var shape = smoke.shape; shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25; shape.radius = .12f;
        var size = smoke.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .45f, 1, 2f));
        var fade = smoke.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.5f, .12f), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        var renderer = smoke.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = runtimeSmokeMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        wheel.ownsSmoke = true;
        return smoke;
    }

    void OnDisable()
    {
        StrongestSlip = ContactSpeedKmh = 0; AnyGrounded = false;
        foreach (var wheel in wheels)
        {
            if (wheel == null) continue;
            wheel.hasPrevious = false; wheel.markIntensity = wheel.emissionRate = 0;
            if (wheel.smoke != null) wheel.smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void OnDestroy()
    {
        foreach (var wheel in wheels) if (wheel != null && wheel.ownsSmoke && wheel.smoke != null) Destroy(wheel.smoke.gameObject);
        if (runtimeSmokeMaterial != null) Destroy(runtimeSmokeMaterial);
        if (smokeTexture != null) Destroy(smokeTexture);
    }
}

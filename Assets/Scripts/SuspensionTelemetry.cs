using System;
using UnityEngine;

[DefaultExecutionOrder(60)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CarController), typeof(Rigidbody))]
public class SuspensionTelemetry : MonoBehaviour
{
    [Serializable]
    public struct WheelState
    {
        public bool grounded;
        [Range(0f, 1f)] public float compression;
        [Tooltip("Extension from fully compressed, in metres.")]
        public float travelPositionMetres;
        public float load, supportedMass;
        public int groundedSamples, airborneSamples, topOutSamples, bottomOutSamples;
        public float airborneSeconds, topOutSeconds, bottomOutSeconds;
        public float currentTopOutSeconds, longestTopOutSeconds, currentBottomOutSeconds, longestBottomOutSeconds;
    }
    [Min(.1f)] public float accelerationResponse = 12f;
    [Header("Live physical telemetry (m/s², normalized compression, degrees)")]
    [SerializeField] float localLateralAcceleration, localLongitudinalAcceleration;
    [SerializeField] float flCompression, frCompression, blCompression, brCompression;
    [SerializeField] float flLoad, frLoad, blLoad, brLoad;
    [SerializeField] float physicalBodyRoll, physicalBodyPitch;
    [SerializeField] int groundedWheels;
    [Header("Per-wheel travel and limit statistics (2% threshold, grounded only)")]
    [SerializeField] WheelState frontLeftState, frontRightState, backLeftState, backRightState;
    public WheelState FrontLeftState => frontLeftState;
    public WheelState FrontRightState => frontRightState;
    public WheelState BackLeftState => backLeftState;
    public WheelState BackRightState => backRightState;
    public float LocalLateralAcceleration => localLateralAcceleration;
    public float LocalLongitudinalAcceleration => localLongitudinalAcceleration;
    public float FLCompression => flCompression;
    public float FRCompression => frCompression;
    public float BLCompression => blCompression;
    public float BRCompression => brCompression;
    public float PhysicalBodyRoll => physicalBodyRoll;
    public float PhysicalBodyPitch => physicalBodyPitch;
    public int GroundedWheels => groundedWheels;
    public bool SampleValid { get; private set; }
    public int ResetVersion { get; private set; }
    CarController car;
    Rigidbody body;
    Vector3 previousVelocity, previousPosition;
    bool hasSample;

    void Awake() { car = GetComponent<CarController>(); body = GetComponent<Rigidbody>(); }
    void FixedUpdate()
    {
        Vector3 velocity = body.linearVelocity, position = body.position;
        float dt = Mathf.Max(.001f, Time.fixedDeltaTime);
        Vector3 acceleration = (velocity - previousVelocity) / dt;
        float travelLimit = Mathf.Max(2f, (velocity.magnitude + previousVelocity.magnitude) * dt * 1.5f + 1f);
        bool positionContinuous = (position - previousPosition).sqrMagnitude <= travelLimit * travelLimit;
        SampleValid = hasSample && positionContinuous && acceleration.sqrMagnitude < 80f * 80f;
        if (!SampleValid)
        {
            localLateralAcceleration = localLongitudinalAcceleration = 0f;
            // A curb impulse is not a respawn. Reject its acceleration sample
            // without snapping the visual angular spring back to neutral.
            if (!hasSample || !positionContinuous) ResetVersion++;
        }
        else
        {
            acceleration.y = 0f;
            Vector3 local = transform.InverseTransformDirection(Vector3.ClampMagnitude(acceleration, 25f));
            float follow = 1f - Mathf.Exp(-Mathf.Max(.1f, accelerationResponse) * dt);
            localLateralAcceleration = Mathf.Lerp(localLateralAcceleration, local.x, follow);
            localLongitudinalAcceleration = Mathf.Lerp(localLongitudinalAcceleration, local.z, follow);
        }
        previousVelocity = velocity; previousPosition = position; hasSample = true;
        groundedWheels = 0;
        flCompression = ReadWheel(car.frontLeft, out flLoad, ref frontLeftState);
        frCompression = ReadWheel(car.frontRight, out frLoad, ref frontRightState);
        blCompression = ReadWheel(car.backLeft, out blLoad, ref backLeftState);
        brCompression = ReadWheel(car.backRight, out brLoad, ref backRightState);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        physicalBodyRoll = Mathf.Atan2(Vector3.Dot(transform.up, right), Vector3.Dot(transform.up, Vector3.up)) * Mathf.Rad2Deg;
        physicalBodyPitch = -Mathf.Asin(Mathf.Clamp(transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
    }
    float ReadWheel(WheelCollider wheel, out float load, ref WheelState state)
    {
        load = 0f;
        state.grounded = false;
        if (wheel == null)
        { state.compression = state.load = state.supportedMass = state.travelPositionMetres = 0f; return 0f; }
        state.supportedMass = wheel.sprungMass;
        if (!wheel.GetGroundHit(out WheelHit hit))
        {
            state.compression = state.load = 0f;
            state.travelPositionMetres = wheel.suspensionDistance;
            state.airborneSamples++; state.airborneSeconds += Time.fixedDeltaTime;
            state.currentTopOutSeconds = state.currentBottomOutSeconds = 0f;
            return 0f;
        }
        state.grounded = true;
        groundedWheels++; load = hit.force;
        Vector3 local = wheel.transform.InverseTransformPoint(hit.point) - wheel.center;
        float compression = 1f - Mathf.Clamp01((-local.y - wheel.radius) / Mathf.Max(.001f, wheel.suspensionDistance));
        state.compression = compression; state.load = load;
        state.travelPositionMetres = (1f - compression) * wheel.suspensionDistance;
        state.groundedSamples++;
        if (compression <= .02f) { state.topOutSamples++; state.topOutSeconds += Time.fixedDeltaTime; state.currentTopOutSeconds += Time.fixedDeltaTime; }
        else state.currentTopOutSeconds = 0f;
        if (compression >= .98f) { state.bottomOutSamples++; state.bottomOutSeconds += Time.fixedDeltaTime; state.currentBottomOutSeconds += Time.fixedDeltaTime; }
        else state.currentBottomOutSeconds = 0f;
        state.longestTopOutSeconds = Mathf.Max(state.longestTopOutSeconds, state.currentTopOutSeconds);
        state.longestBottomOutSeconds = Mathf.Max(state.longestBottomOutSeconds, state.currentBottomOutSeconds);
        return compression;
    }
    [ContextMenu("Reset Suspension Statistics")]
    public void ResetStatistics()
    { frontLeftState = frontRightState = backLeftState = backRightState = default; }
    void OnEnable() { hasSample = false; SampleValid = false; }
}

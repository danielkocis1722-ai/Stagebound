using UnityEngine;

[DefaultExecutionOrder(60)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CarController), typeof(Rigidbody))]
public class SuspensionTelemetry : MonoBehaviour
{
    [Min(.1f)]
    public float accelerationResponse = 12f;

    [Header("Body motion (m/s², degrees)")]
    [SerializeField]
    private float localLateralAcceleration, localLongitudinalAcceleration;
    [SerializeField]
    private float physicalBodyRoll, physicalBodyPitch;
    [SerializeField]
    private int groundedWheels;

    public float LocalLateralAcceleration => localLateralAcceleration;
    public float LocalLongitudinalAcceleration => localLongitudinalAcceleration;
    public float PhysicalBodyRoll => physicalBodyRoll;
    public float PhysicalBodyPitch => physicalBodyPitch;
    public int GroundedWheels => groundedWheels;
    public bool SampleValid { get; private set; }
    public int ResetVersion { get; private set; }

    private WheelCollider[] wheels;
    private Rigidbody body;
    private Vector3 previousVelocity, previousPosition;
    private bool hasSample;

    private void Awake()
    {
        var car = GetComponent<CarController>();
        wheels = new[] { car.frontLeft, car.frontRight, car.backLeft, car.backRight };
        body = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
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
            if (!hasSample || !positionContinuous)
                ResetVersion++;
        }
        else
        {
            acceleration.y = 0f;
            Vector3 local = transform.InverseTransformDirection(Vector3.ClampMagnitude(acceleration, 25f));
            float follow = 1f - Mathf.Exp(-Mathf.Max(.1f, accelerationResponse) * dt);
            localLateralAcceleration = Mathf.Lerp(localLateralAcceleration, local.x, follow);
            localLongitudinalAcceleration = Mathf.Lerp(localLongitudinalAcceleration, local.z, follow);
        }

        previousVelocity = velocity;
        previousPosition = position;
        hasSample = true;
        groundedWheels = 0;
        foreach (var wheel in wheels)
            if (wheel != null && wheel.GetGroundHit(out _))
                groundedWheels++;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        physicalBodyRoll = Mathf.Atan2(Vector3.Dot(transform.up, right), Vector3.Dot(transform.up, Vector3.up)) * Mathf.Rad2Deg;
        physicalBodyPitch = -Mathf.Asin(Mathf.Clamp(transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    private void OnEnable()
    {
        hasSample = false;
        SampleValid = false;
    }
}

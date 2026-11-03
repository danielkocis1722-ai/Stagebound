using UnityEngine;

[DefaultExecutionOrder(200)]
public class FollowCamera : MonoBehaviour
{
    [Header("References")]
    public Transform target;
    public Rigidbody carRigidbody;

    [Header("Chase Camera")]
    public float slowDistance = 3.55f;
    public float fastDistance = 4.2f;
    public float slowHeight = 1.3f;
    public float fastHeight = 1.6f;
    public float positionSharpness = 7f;
    public float lookSharpness = 5f;
    public float lookAheadSlow = 1.1f;
    public float lookAheadFast = 2.8f;
    public float lookHeight = 0.4f;

    [Header("Drift Camera")]
    // Ako veľmi kamera reaguje na rozdiel
    // medzi smerom auta a smerom pohybu.
    [Range(0f, 1f)]
    public float driftSensitivity = 0.75f;

    // Pri tomto slip angle už považujeme
    // drift efekt kamery za prakticky maximálny.
    public float driftFullAngle = 35f;

    // Nižšie = kamera počas driftu zaostáva viac.
    public float driftLookSharpness = 2.5f;

    // Koľko z velocity direction môže kamera použiť.
    [Range(0f, 1f)]
    public float driftCameraRange = 0.85f;

    // Jemné posunutie kamery do strany počas driftu.
    public float driftSideOffset = 0.18f;
    public float minDriftSpeed = 20f;

    [Header("Speed Effects")]
    public float maxEffectSpeed = 160f;
    public float slowFOV = 62f;
    public float fastFOV = 73f;
    public float fovSharpness = 4f;
    [Header("Low-speed proximity (fades out by 80 km/h)")]
    [Min(0f)] public float lowSpeedDistanceReduction = .25f;
    [Min(0f)] public float lowSpeedFOVReduction = 2f;
    [Min(1f)] public float proximityFadeSpeed = 80f;

    [Header("Acceleration Effects")]
    public bool accelerationFeedbackEnabled = true;
    [Min(0f)]
    public float accelerationPullback = 0.25f;
    [Min(0f)]
    public float accelerationLowering = 0.025f;
    [Min(0f)]
    public float accelerationFOVBonus = 3f;
    [Min(0.31f)]
    public float fullFeedbackAcceleration = 6f;
    [Min(0.1f)]
    public float accelerationResponse = 10f;
    [Min(0.1f)]
    public float accelerationRecovery = 5f;
    public float brakingPush = 0.12f;

    [Header("Drivetrain Camera Hit")]
    [Min(0f)]
    public float torqueKickPullback = 0.03f;
    [Min(0f)]
    public float torqueKickFOVBonus = 1.25f;
    [Min(1f)]
    public float torqueKickThreshold = 150f;
    [Min(0.01f)]
    public float torqueKickDuration = 0.18f;
    [Min(0f)]
    public float torqueKickCooldown = 0.4f;

    public float AccelerationFeedbackAmount => feedback.AccelerationAmount;
    public float DrivetrainKickAmount => feedback.KickAmount;
    public float CurrentSlipAngle { get; private set; }

    private Camera cam;
    private Vector3 smoothedForward;
    private Vector3 smoothedPosition;
    private float speedFOV;
    private float brakingOffset;
    private CarController car;
    private readonly AccelerationCameraFeedback feedback = new AccelerationCameraFeedback();

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (target == null || carRigidbody == null)
            return;
        car = carRigidbody.GetComponent<CarController>();
        feedback.Reset(car != null ? car.DeliveredAxleTorque : 0f, car != null ? car.ThrottleInput : 0f, car != null ? car.ClutchEngagement : 1f);
        Vector3 carForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        smoothedForward = carForward;
        float speedKmh = carRigidbody.linearVelocity.magnitude * 3.6f;
        float speedFactor = Mathf.Clamp01(speedKmh / maxEffectSpeed);
        float distance = Mathf.Lerp(slowDistance, fastDistance, speedFactor) - lowSpeedDistanceReduction * Proximity(speedKmh);
        float height = Mathf.Lerp(slowHeight, fastHeight, speedFactor);
        // Kamera sa hneď spawnne na správnom mieste.
        smoothedPosition = target.position - carForward * distance + Vector3.up * height;
        transform.position = smoothedPosition;
        Vector3 lookTarget = target.position + carForward * lookAheadSlow + Vector3.up * lookHeight;
        transform.rotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        speedFOV = cam.fieldOfView = Mathf.Lerp(slowFOV, fastFOV, speedFactor) - lowSpeedFOVReduction * Proximity(speedKmh);
    }

    private void FixedUpdate()
    {
        if (car == null)
            return;
        if (!car.isActiveAndEnabled || !accelerationFeedbackEnabled)
        {
            feedback.Reset(car.DeliveredAxleTorque, car.ThrottleInput, car.ClutchEngagement);
            brakingOffset = 0f;
            return;
        }

        feedback.Step(car.LongitudinalAcceleration, car.DeliveredAxleTorque, car.ThrottleInput, car.ClutchEngagement, car.DynamicsSampleValid, car.LongitudinalAcceleration > .3f && car.currentGear > 0 && car.BrakeInput <= .02f && car.HandbrakeInput <= .05f, Time.fixedDeltaTime, fullFeedbackAcceleration, accelerationResponse, accelerationRecovery, torqueKickThreshold, torqueKickDuration, torqueKickCooldown);
        float brakingTarget = car.DynamicsSampleValid ? Mathf.Clamp(car.LongitudinalAcceleration * .025f, -brakingPush, 0f) : 0f;
        brakingOffset = Mathf.Lerp(brakingOffset, brakingTarget, 1f - Mathf.Exp(-accelerationRecovery * Time.fixedDeltaTime));
    }

    private void LateUpdate()
    {
        if (target == null || carRigidbody == null || Time.deltaTime <= 0f)
            return;
        // ====================================================
        // SPEED
        // ====================================================
        float speed = carRigidbody.linearVelocity.magnitude;
        float speedKmh = speed * 3.6f;
        float speedFactor = Mathf.Clamp01(speedKmh / maxEffectSpeed);
        // ====================================================
        // CAR DIRECTION
        // ====================================================
        Vector3 carForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        // ====================================================
        // VELOCITY DIRECTION
        // ====================================================
        Vector3 flatVelocity = Vector3.ProjectOnPlane(carRigidbody.linearVelocity, Vector3.up);
        Vector3 velocityForward = carForward;
        if (flatVelocity.sqrMagnitude > 0.25f)
        {
            velocityForward = flatVelocity.normalized;
        }

        // ====================================================
        // SLIP ANGLE
        // ====================================================
        if (speedKmh < 2f)
        {
            CurrentSlipAngle = 0f;
        }
        else
        {
            CurrentSlipAngle = Vector3.SignedAngle(carForward, velocityForward, Vector3.up);
        }

        // ====================================================
        // DRIFT CAMERA AMOUNT
        // ====================================================
        float driftAmount = 0f;
        if (speedKmh > minDriftSpeed)
        {
            driftAmount = Mathf.InverseLerp(4f, driftFullAngle, Mathf.Abs(CurrentSlipAngle));
            driftAmount *= driftSensitivity;
        }

        // Normálne sledujeme smer auta.
        // Pri drifte sa kamera začne orientovať
        // viac podľa reálneho smeru pohybu.
        Vector3 driftForward = Vector3.Slerp(carForward, velocityForward, driftAmount * driftCameraRange);
        // Počas driftu necháme kameru reagovať pomalšie.
        float currentLookSharpness = Mathf.Lerp(lookSharpness, driftLookSharpness, driftAmount);
        float directionLerp = 1f - Mathf.Exp(-currentLookSharpness * Time.deltaTime);
        smoothedForward = Vector3.Slerp(smoothedForward, driftForward, directionLerp).normalized;
        // ====================================================
        // CAMERA DISTANCE + HEIGHT
        // ====================================================
        float distance = Mathf.Lerp(slowDistance, fastDistance, speedFactor) - lowSpeedDistanceReduction * Proximity(speedKmh);
        float height = Mathf.Lerp(slowHeight, fastHeight, speedFactor);
        // ====================================================
        // ACCELERATION / BRAKING
        // ====================================================
        distance += brakingOffset + accelerationPullback * feedback.AccelerationAmount + torqueKickPullback * feedback.KickAmount;
        height -= accelerationLowering * feedback.AccelerationAmount;
        // ====================================================
        // DRIFT SIDE OFFSET
        // ====================================================
        Vector3 cameraRight = Vector3.Cross(Vector3.up, smoothedForward).normalized;
        float normalizedSlip = Mathf.Clamp(CurrentSlipAngle / driftFullAngle, -1f, 1f);
        float sideOffset = normalizedSlip * driftSideOffset * driftAmount;
        // ====================================================
        // DESIRED CAMERA POSITION
        // ====================================================
        Vector3 desiredPosition = target.position - smoothedForward * distance + Vector3.up * height + cameraRight * sideOffset;
        float positionLerp = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
        smoothedPosition = Vector3.Lerp(smoothedPosition, desiredPosition, positionLerp);
        transform.position = smoothedPosition;
        // ====================================================
        // LOOK TARGET
        // ====================================================
        float lookAhead = Mathf.Lerp(lookAheadSlow, lookAheadFast, speedFactor);
        Vector3 lookTarget = target.position + smoothedForward * lookAhead + Vector3.up * lookHeight;
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, directionLerp);
        // ====================================================
        // FOV
        // ====================================================
        float desiredFOV = Mathf.Lerp(slowFOV, fastFOV, speedFactor) - lowSpeedFOVReduction * Proximity(speedKmh);
        float fovLerp = 1f - Mathf.Exp(-fovSharpness * Time.deltaTime);
        speedFOV = Mathf.Lerp(speedFOV, desiredFOV, fovLerp);
        cam.fieldOfView = speedFOV + accelerationFOVBonus * feedback.AccelerationAmount + torqueKickFOVBonus * feedback.KickAmount;
    }

    private float Proximity(float speedKmh) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(speedKmh / proximityFadeSpeed));
}

using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(200)]
public class FollowCamera : MonoBehaviour
{
    [Header("References")]
    public Transform target;
    public Rigidbody carRigidbody;

    [Header("Chase Camera")]
    public float slowDistance = 3.85f;
    public float fastDistance = 4.15f;
    public float slowHeight = 1.42f;
    public float fastHeight = 1.5f;
    public float positionSharpness = 7f;
    public float lookSharpness = 5f;
    public float lookAheadSlow = 1.1f;
    public float lookAheadFast = 2.1f;
    public float lookHeight = 0.4f;

    [Header("Drift Camera")]
    // Ako veľmi kamera reaguje na rozdiel
    // medzi smerom auta a smerom pohybu.
    [Range(0f, 1f)]
    public float driftSensitivity = 0.55f;

    // Pri tomto slip angle už považujeme
    // drift efekt kamery za prakticky maximálny.
    public float driftFullAngle = 35f;

    // Nižšie = kamera počas driftu zaostáva viac.
    public float driftLookSharpness = 2.5f;

    // Koľko z velocity direction môže kamera použiť.
    [Range(0f, 1f)]
    public float driftCameraRange = 0.65f;

    // Jemné posunutie kamery do strany počas driftu.
    public float driftSideOffset = 0.08f;
    public float minDriftSpeed = 20f;

    [Header("Speed Effects")]
    public float maxEffectSpeed = 160f;
    public float slowFOV = 60f;
    public float fastFOV = 80f;
    [Min(1f)] public float framingSpeedKmh = 75f;
    public float framingFOV = 73f;
    public float fovSharpness = 2.2f;
    [Header("Low-speed proximity (fades out by 80 km/h)")]
    [Min(0f)] public float lowSpeedDistanceReduction = 0.08f;
    [Min(0f)] public float lowSpeedFOVReduction = 0f;
    [Min(1f)] public float proximityFadeSpeed = 80f;

    [Header("Acceleration Effects")]
    public bool accelerationFeedbackEnabled = true;
    [Min(0f)]
    public float accelerationPullback = 0.1f;
    [Min(0f)]
    public float accelerationLowering = 0.012f;
    [Min(0f)]
    public float accelerationFOVBonus = 0.8f;
    [Min(0.31f)]
    public float fullFeedbackAcceleration = 6f;
    [Min(0.1f)]
    public float accelerationResponse = 5.5f;
    [Min(0.1f)]
    public float accelerationRecovery = 5f;
    public float brakingPush = 0.05f;

    [Header("Drivetrain Camera Hit")]
    [Min(0f)]
    public float torqueKickPullback = 0.012f;
    [Min(0f)]
    public float torqueKickFOVBonus = 0.25f;
    [Min(1f)]
    public float torqueKickThreshold = 150f;
    [Min(0.01f)]
    public float torqueKickDuration = 0.18f;
    [Min(0f)]
    public float torqueKickCooldown = 0.4f;

    [Header("Road slope (pitch only)")]
    [Range(0f, 1f)] public float slopeFollowStrength = .85f;
    [Range(0f, 45f)] public float slopePitchLimit = 25f;
    [Min(.1f)] public float slopeSmoothing = 2f;
    [Min(.1f)] public float groundNormalSmoothing = 3f;
    [Min(0f)] public float slopeNoiseDeadband = 1f;
    [Min(.1f)] public float slopePitchRate = 12f;

    [Header("Right-stick orbit")]
    [Min(0f)] public float orbitYawSpeed = 90f;
    [Range(0f, 180f)] public float orbitYawLimit = 140f;
    [Min(0f)] public float orbitPitchSpeed = 35f;
    [Range(-20f, 0f)] public float orbitPitchMin = -10f;
    [Range(0f, 30f)] public float orbitPitchMax = 18f;
    [Tooltip("Additional radial deadzone after the Input System's existing stick processing. Zero uses the project deadzone alone.")]
    [Range(0f, .5f)] public float orbitDeadzone = .12f;
    [Min(0f)] public float orbitReturnDelay = .6f;
    [Min(.1f)] public float orbitReturnSpeed = 3f;
    public bool invertOrbitX;
    public bool invertOrbitY;

    public float SlopePitch => slopePitch;
    public int GroundedWheelCount { get; private set; }
    public float OrbitYaw => orbitYaw;
    public float OrbitPitch => orbitPitch;

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
    private InputAction cameraLook;
    private Vector3 groundNormal = Vector3.up, manualForward;
    private WheelCollider[] slopeWheels;
    private float slopePitch, missingGroundTime, orbitYaw, orbitPitch, orbitIdleTime;
    private bool manualOrbit;

    private void OnEnable()
    {
        // Driving already uses the Input System directly; own only this camera action.
        cameraLook = new InputAction("CameraLook", InputActionType.Value, "<Gamepad>/rightStick", expectedControlType: "Vector2");
        cameraLook.Enable();
    }

    private void OnDisable()
    {
        cameraLook?.Dispose();
        cameraLook = null;
        orbitYaw = orbitPitch = orbitIdleTime = 0f;
        manualOrbit = false;
    }

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (target == null || carRigidbody == null)
            return;
        car = carRigidbody.GetComponent<CarController>();
        if (car != null)
            slopeWheels = new[] { car.frontLeft, car.frontRight, car.backLeft, car.backRight };
        feedback.Reset(car != null ? car.DeliveredAxleTorque : 0f, car != null ? car.ThrottleInput : 0f, car != null ? car.ClutchEngagement : 1f);
        Vector3 carForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        smoothedForward = carForward;
        float speedKmh = carRigidbody.linearVelocity.magnitude * 3.6f;
        float speedFactor = Mathf.Clamp01(speedKmh / maxEffectSpeed);
        float distance = SpeedDistance(speedKmh);
        float height = Mathf.Lerp(slowHeight, fastHeight, speedFactor);
        // Kamera sa hneď spawnne na správnom mieste.
        smoothedPosition = target.position - carForward * distance + Vector3.up * height;
        transform.position = smoothedPosition;
        Vector3 lookTarget = target.position + carForward * lookAheadSlow + Vector3.up * lookHeight;
        transform.rotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        speedFOV = cam.fieldOfView = SpeedFOV(speedKmh);
    }

    private void FixedUpdate()
    {
        SampleRoadNormal();
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
        float distance = SpeedDistance(speedKmh);
        float height = Mathf.Lerp(slowHeight, fastHeight, speedFactor);
        // ====================================================
        // ACCELERATION / BRAKING
        // ====================================================
        distance += brakingOffset + accelerationPullback * feedback.AccelerationAmount + torqueKickPullback * feedback.KickAmount;
        height -= accelerationLowering * feedback.AccelerationAmount;
        // ====================================================
        // DRIFT SIDE OFFSET
        // ====================================================
        float normalizedSlip = Mathf.Clamp(CurrentSlipAngle / driftFullAngle, -1f, 1f);
        float sideOffset = normalizedSlip * driftSideOffset * driftAmount;
        // ====================================================
        // DESIRED CAMERA POSITION
        // ====================================================
        UpdateOrbit(Time.deltaTime);
        Vector3 chaseForward = manualOrbit ? manualForward : smoothedForward;
        Vector3 roadForward = Vector3.ProjectOnPlane(chaseForward, groundNormal).normalized;
        float roadPitch = Mathf.Atan2(roadForward.y, Vector3.ProjectOnPlane(roadForward, Vector3.up).magnitude) * Mathf.Rad2Deg;
        roadPitch = Mathf.Sign(roadPitch) * Mathf.Max(0f, Mathf.Abs(roadPitch) - slopeNoiseDeadband);
        float wantedPitch = Mathf.Clamp(roadPitch * slopeFollowStrength, -slopePitchLimit, slopePitchLimit);
        float filteredPitch = Mathf.Lerp(slopePitch, wantedPitch, 1f - Mathf.Exp(-slopeSmoothing * Time.deltaTime));
        slopePitch = Mathf.MoveTowards(slopePitch, filteredPitch, slopePitchRate * Time.deltaTime);
        Vector3 chaseRight = Vector3.Cross(Vector3.up, chaseForward).normalized;
        Quaternion roadRotation = Quaternion.AngleAxis(-slopePitch, chaseRight);
        Vector3 routeForward = roadRotation * chaseForward;
        Vector3 pivot = target.position + Vector3.up * lookHeight;
        // Right input moves the camera to the vehicle's right side. Positive Y raises it.
        Quaternion orbit = Quaternion.AngleAxis(-orbitYaw, Vector3.up) * Quaternion.AngleAxis(orbitPitch, chaseRight);
        Vector3 offset = -chaseForward * distance + Vector3.up * (height - lookHeight) + chaseRight * sideOffset;
        Vector3 desiredPosition = pivot + roadRotation * (orbit * offset);
        float positionLerp = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
        // Retain offset smoothing, but carry the camera with forward travel above
        // the speed that would otherwise add about 35 cm of steady chase lag.
        float forwardSpeed = Vector3.Dot(carRigidbody.linearVelocity, carForward);
        float carriedSpeed = Mathf.Max(0f, forwardSpeed - .35f * positionSharpness);
        smoothedPosition += carForward * (carriedSpeed * Time.deltaTime);
        smoothedPosition = Vector3.Lerp(smoothedPosition, desiredPosition, positionLerp);
        transform.position = smoothedPosition;
        // ====================================================
        // LOOK TARGET
        // ====================================================
        float lookAhead = Mathf.Lerp(lookAheadSlow, lookAheadFast, speedFactor);
        // At large orbit angles look near the car, rather than beyond its nose.
        float orbitLookWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Max(Mathf.Abs(orbitYaw) / 70f, Mathf.Abs(orbitPitch) / 18f)));
        Vector3 lookTarget = pivot + routeForward * (lookAhead * (1f - orbitLookWeight));
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        // Interpolate the viewing direction, then reconstruct with world up: no bank/body roll.
        float lookLerp = manualOrbit ? 1f - Mathf.Exp(-lookSharpness * Time.deltaTime) : directionLerp;
        transform.rotation = Quaternion.LookRotation(Vector3.Slerp(transform.forward, desiredRotation * Vector3.forward, lookLerp), Vector3.up);
        // ====================================================
        // FOV
        // ====================================================
        float desiredFOV = SpeedFOV(speedKmh);
        float fovLerp = 1f - Mathf.Exp(-fovSharpness * Time.deltaTime);
        speedFOV = Mathf.Lerp(speedFOV, desiredFOV, fovLerp);
        cam.fieldOfView = speedFOV + accelerationFOVBonus * feedback.AccelerationAmount + torqueKickFOVBonus * feedback.KickAmount;
    }

    private float SpeedDistance(float speedKmh) =>
        Mathf.Lerp(slowDistance, fastDistance, Mathf.InverseLerp(0f, framingSpeedKmh, speedKmh)) -
        lowSpeedDistanceReduction * Proximity(speedKmh);

    private float SpeedFOV(float speedKmh)
    {
        float fov = speedKmh <= framingSpeedKmh
            ? Mathf.Lerp(slowFOV, framingFOV, Mathf.InverseLerp(0f, framingSpeedKmh, speedKmh))
            : Mathf.Lerp(framingFOV, fastFOV, Mathf.InverseLerp(framingSpeedKmh, Mathf.Max(framingSpeedKmh + 1f, maxEffectSpeed), speedKmh));
        return fov - lowSpeedFOVReduction * Proximity(speedKmh);
    }

    private float Proximity(float speedKmh) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(speedKmh / proximityFadeSpeed));

    private void SampleRoadNormal()
    {
        GroundedWheelCount = 0;
        Vector3 normal = Vector3.zero;
        bool front = false, rear = false;
        if (slopeWheels != null)
            for (int i = 0; i < slopeWheels.Length; i++)
                if (slopeWheels[i] != null && slopeWheels[i].GetGroundHit(out var hit) && Vector3.Dot(hit.normal, Vector3.up) > .35f)
                {
                    normal += hit.normal;
                    GroundedWheelCount++;
                    if (i < 2) front = true; else rear = true;
                }
        if (GroundedWheelCount >= 3 && front && rear)
        {
            missingGroundTime = 0f;
            groundNormal = Vector3.Slerp(groundNormal, normal.normalized, 1f - Mathf.Exp(-groundNormalSmoothing * Time.fixedDeltaTime)).normalized;
        }
        else
        {
            // Hold the last road through a bump/jump; never use airborne body pitch.
            missingGroundTime += Time.fixedDeltaTime;
            if (missingGroundTime > .6f)
                groundNormal = Vector3.Slerp(groundNormal, Vector3.up, 1f - Mathf.Exp(-.3f * Time.fixedDeltaTime)).normalized;
        }
    }

    private void UpdateOrbit(float dt)
    {
        Vector2 input = (car != null && !car.isActiveAndEnabled) || PauseMenu.IsPaused
            ? Vector2.zero : cameraLook != null ? cameraLook.ReadValue<Vector2>() : Vector2.zero;
        float magnitude = input.magnitude;
        if (magnitude > orbitDeadzone)
        {
            input = input.normalized * Mathf.InverseLerp(orbitDeadzone, 1f, magnitude);
            if (!manualOrbit) manualForward = smoothedForward;
            manualOrbit = true;
            orbitIdleTime = 0f;
            orbitYaw = Mathf.Clamp(orbitYaw + input.x * (invertOrbitX ? -1f : 1f) * orbitYawSpeed * dt, -orbitYawLimit, orbitYawLimit);
            orbitPitch = Mathf.Clamp(orbitPitch + input.y * (invertOrbitY ? -1f : 1f) * orbitPitchSpeed * dt, orbitPitchMin, orbitPitchMax);
        }
        else if (manualOrbit)
        {
            orbitIdleTime += dt;
            if (orbitIdleTime < orbitReturnDelay) return;
            float blend = 1f - Mathf.Exp(-orbitReturnSpeed * dt);
            orbitYaw = Mathf.Lerp(orbitYaw, 0f, blend);
            orbitPitch = Mathf.Lerp(orbitPitch, 0f, blend);
            manualForward = Vector3.Slerp(manualForward, smoothedForward, blend).normalized;
            if (Mathf.Abs(orbitYaw) < .01f && Mathf.Abs(orbitPitch) < .01f && Vector3.Dot(manualForward, smoothedForward) > .9999f)
            {
                orbitYaw = orbitPitch = 0f;
                manualOrbit = false;
            }
        }
    }
}

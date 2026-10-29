using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CarController), typeof(Rigidbody))]
public class RespawnManager : MonoBehaviour
{
    [Tooltip("Authoritative START transform for initial Play spawn, Restart Stage and respawn before a checkpoint. Move this Transform to choose the starting lane and heading.")]
    public Transform startPoint;
    public RaceTimer raceTimer;
    CarController car;
    Rigidbody body;
    Pose startPose, checkpointPose;
    bool hasCheckpoint;
    float ignoreTriggersUntil = float.NegativeInfinity;

    public bool SuppressRaceTriggers => Time.fixedTime < ignoreTriggersUntil;

    void Awake()
    {
        car = GetComponent<CarController>();
        body = GetComponent<Rigidbody>();
        // Retain a fallback only for scenes without a configured startPoint.
        // SampleScene assigns Race Start-Finish/Start Respawn as the sole authority.
        startPose = new Pose(body.position, body.rotation);
        Teleport(GetStartPose(), true);
    }

    Pose GetStartPose() => startPoint != null
        ? new Pose(startPoint.position, startPoint.rotation)
        : startPose;

    void Update()
    {
        if (PauseMenu.IsPaused || Time.timeScale <= 0f) return;
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        // Triangle / Y is unused by the existing driving controls.
        if ((keyboard != null && keyboard.rKey.wasPressedThisFrame) ||
            (pad != null && pad.buttonNorth.wasPressedThisFrame)) Respawn();
    }

    public void BeginStage(RaceTimer timer)
    {
        raceTimer = timer;
        hasCheckpoint = false;
    }

    public void RecordCheckpoint(RaceTrigger checkpoint)
    {
        raceTimer = checkpoint.raceTimer;
        // An optional designer-authored pose overrides the safe crossing position.
        // Otherwise retain the road position and heading actually used by the car.
        var point = checkpoint.respawnPoint;
        checkpointPose = UprightPose(point != null ? point.position : body.position,
            point != null ? point.rotation : body.rotation);
        hasCheckpoint = true;
    }

    public void Respawn()
    {
        // A normal recovery deliberately leaves timing, splits and best records alone.
        Teleport(hasCheckpoint ? checkpointPose : GetStartPose(), false);
    }

    public void RestartStage()
    {
        if (raceTimer == null) raceTimer = FindAnyObjectByType<RaceTimer>();
        if (raceTimer != null) raceTimer.ResetTimer();
        hasCheckpoint = false;
        Teleport(GetStartPose(), true);
    }

    void Teleport(Pose pose, bool restart)
    {
        // Recovery banks only the active combo; the running stage and area budgets survive.
        var scoring = GetComponent<DriftScoreSystem>();
        if (scoring != null) scoring.EndCombo();
        var telemetry = GetComponent<DriftTelemetry>();
        if (telemetry != null) telemetry.ResetDrift();
        // Ignore the first two physics steps at the destination so teleporting into
        // START or a checkpoint cannot count as crossing it or restart the timer.
        ignoreTriggersUntil = Time.fixedTime + 2f * Time.fixedDeltaTime;
        foreach (var trigger in FindObjectsByType<RaceTrigger>()) trigger.ClearCarOccupancy(car);
        body.position = pose.position;
        body.rotation = pose.rotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        if (restart || car.currentGear < 1 || car.currentGear > car.gearRatios.Length)
            car.currentGear = 1;
        ResetWheel(car.frontLeft); ResetWheel(car.frontRight);
        ResetWheel(car.backLeft); ResetWheel(car.backRight);
        Physics.SyncTransforms();
        body.WakeUp();
    }

    static void ResetWheel(WheelCollider wheel)
    {
        if (wheel == null) return;
        wheel.motorTorque = 0;
        wheel.brakeTorque = 0;
    }

    static Pose UprightPose(Vector3 position, Quaternion rotation)
    {
        Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        return new Pose(position, Quaternion.LookRotation(forward.normalized, Vector3.up));
    }
}

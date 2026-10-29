using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(80)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SuspensionTelemetry))]
public class VisualBodyRoll : MonoBehaviour
{
    [Tooltip("Visual model only. CarRoot, Rigidbody and WheelColliders are never moved.")]
    public Transform visualModel;
    [Serializable] public struct UnsprungAxleVisual
    {
        public Transform visual;
        public Transform leftWheelMesh, rightWheelMesh;
    }
    [Tooltip("Visual axle beams follow the two wheel centers, independently of sprung-body roll and wheel spin.")]
    public UnsprungAxleVisual[] unsprungAxles = Array.Empty<UnsprungAxleVisual>();
    [Header("Degrees per m/s²")]
    [Min(0f)] public float visualRollStrength = .18f;
    [Min(0f)] public float visualPitchStrength = .08f;
    [Header("Damped angular spring")]
    [Min(.1f)] public float bodySpring = 90f;
    [Min(0f)] public float bodyDamping = 19f;
    [Range(0f, 3f)] public float maximumVisualRoll = 1f;
    [Range(0f, 2f)] public float maximumVisualPitch = .6f;
    [Header("Live body motion (positive roll = right down, pitch = nose down)")]
    [SerializeField] float visualRoll, visualPitch, rollVelocity, pitchVelocity;
    [SerializeField] float calculatedBodyRoll, calculatedBodyPitch;
    public float VisualRoll => visualRoll;
    public float VisualPitch => visualPitch;
    public float CalculatedBodyRoll => calculatedBodyRoll;
    public float CalculatedBodyPitch => calculatedBodyPitch;
    public Transform VisualPivot => pivot;
    SuspensionTelemetry telemetry;
    CarController car;
    Transform pivot;
    Quaternion restRotation;
    int resetVersion;
    readonly List<RestPose> parts = new List<RestPose>();
    AxlePose[] axlePoses;
    struct RestPose { public Transform part, parent; public Vector3 position, scale; public Quaternion rotation; public int sibling; }
    struct AxlePose { public Transform visual, left, right; public Vector3 offset, restPosition; public Quaternion rotation, restRotation; }

    void Start()
    {
        telemetry = GetComponent<SuspensionTelemetry>(); car = GetComponent<CarController>();
        if (visualModel == null || visualModel == transform || !visualModel.IsChildOf(transform)) return;
        // Reject physics-bearing model hierarchies before changing any parents.
        if (visualModel.GetComponentInChildren<Collider>(true) != null || visualModel.GetComponentInChildren<Rigidbody>(true) != null) return;
        var renderers = visualModel.GetComponentsInChildren<Renderer>(true);
        axlePoses = new AxlePose[unsprungAxles.Length];
        for (int i = 0; i < unsprungAxles.Length; i++)
        {
            var binding = unsprungAxles[i];
            if (binding.visual == null || binding.leftWheelMesh == null || binding.rightWheelMesh == null ||
                !binding.visual.IsChildOf(visualModel) || !binding.leftWheelMesh.IsChildOf(visualModel) || !binding.rightWheelMesh.IsChildOf(visualModel)) continue;
            Quaternion frame = AxleFrame(binding.leftWheelMesh, binding.rightWheelMesh);
            Vector3 middle = (binding.leftWheelMesh.position + binding.rightWheelMesh.position) * .5f;
            axlePoses[i] = new AxlePose { visual = binding.visual, left = binding.leftWheelMesh, right = binding.rightWheelMesh,
                offset = Quaternion.Inverse(frame) * (binding.visual.position - middle),
                rotation = Quaternion.Inverse(frame) * binding.visual.rotation,
                restPosition = binding.visual.localPosition, restRotation = binding.visual.localRotation };
        }
        pivot = new GameObject("Sprung visual body").transform;
        pivot.SetParent(visualModel, false);
        pivot.SetPositionAndRotation(GetComponent<Rigidbody>().worldCenterOfMass, transform.rotation);
        restRotation = pivot.localRotation;
        foreach (var renderer in renderers)
        {
            Transform part = renderer.transform;
            if (ProtectedWheelPart(part)) continue;
            // Only renderer leaves move. Never take a parent containing wheels,
            // colliders, rigidbodies or another body renderer along with it.
            if (part.GetComponentInChildren<Collider>(true) != null || part.GetComponentInChildren<Rigidbody>(true) != null) continue;
            bool containsWheel = false;
            foreach (var wheel in WheelRoots()) if (wheel != null && wheel.IsChildOf(part)) containsWheel = true;
            if (containsWheel || part.GetComponentsInChildren<Renderer>(true).Length > 1) continue;
            parts.Add(new RestPose { part = part, parent = part.parent, position = part.localPosition,
                rotation = part.localRotation, scale = part.localScale, sibling = part.GetSiblingIndex() });
        }
        foreach (var pose in parts) pose.part.SetParent(pivot, true);
        resetVersion = telemetry.ResetVersion;
    }
    Transform[] WheelRoots() => new[] { car.frontLeftWheel, car.frontRightWheel, car.backLeftWheel, car.backRightWheel };
    bool ProtectedWheelPart(Transform part)
    {
        foreach (var wheel in WheelRoots()) if (wheel != null && (part == wheel || part.IsChildOf(wheel))) return true;
        foreach (var binding in unsprungAxles)
            if (binding.visual != null && (part == binding.visual || part.IsChildOf(binding.visual))) return true;
        return false;
    }
    void LateUpdate() => Advance(Time.deltaTime);
    public void Advance(float dt)
    {
        if (pivot == null || telemetry == null || dt <= 0f || Time.timeScale <= 0f) return;
        if (resetVersion != telemetry.ResetVersion)
        { ResetMotion(); resetVersion = telemetry.ResetVersion; }
        float rollTarget = telemetry.SampleValid && telemetry.GroundedWheels >= 2
            ? Mathf.Clamp(-telemetry.LocalLateralAcceleration * visualRollStrength, -maximumVisualRoll, maximumVisualRoll) : 0f;
        float pitchTarget = telemetry.SampleValid && telemetry.GroundedWheels >= 2
            ? Mathf.Clamp(-telemetry.LocalLongitudinalAcceleration * visualPitchStrength, -maximumVisualPitch, maximumVisualPitch) : 0f;
        // Semi-implicit spring integration in small substeps, never an angle Lerp.
        float remaining = Mathf.Min(dt, .1f);
        while (remaining > .000001f)
        {
            float h = Mathf.Min(remaining, 1f / 120f); remaining -= h;
            Spring(ref visualRoll, ref rollVelocity, rollTarget, maximumVisualRoll, h);
            Spring(ref visualPitch, ref pitchVelocity, pitchTarget, maximumVisualPitch, h);
        }
        pivot.localRotation = restRotation * Quaternion.Euler(visualPitch, 0f, -visualRoll);
        // The two existing axle-beam meshes follow wheel-pair midpoints. They do
        // not inherit sprung-body tilt or spin/steer with one wheel in isolation.
        foreach (var axle in axlePoses)
            if (axle.visual != null && axle.left != null && axle.right != null)
            {
                Quaternion frame = AxleFrame(axle.left, axle.right);
                axle.visual.SetPositionAndRotation((axle.left.position + axle.right.position) * .5f + frame * axle.offset, frame * axle.rotation);
            }
        calculatedBodyRoll = telemetry.PhysicalBodyRoll + visualRoll;
        calculatedBodyPitch = telemetry.PhysicalBodyPitch + visualPitch;
    }
    Quaternion AxleFrame(Transform left, Transform right)
    {
        Vector3 across = (right.position - left.position).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, across).normalized;
        return forward.sqrMagnitude > .01f && across.sqrMagnitude > .01f
            ? Quaternion.LookRotation(forward, Vector3.Cross(forward, across)) : transform.rotation;
    }
    void Spring(ref float angle, ref float velocity, float target, float limit, float dt)
    {
        velocity += (Mathf.Max(.1f, bodySpring) * (target - angle) - Mathf.Max(0f, bodyDamping) * velocity) * dt;
        angle += velocity * dt;
        float clamped = Mathf.Clamp(angle, -limit, limit);
        if (clamped != angle) velocity = 0f;
        angle = clamped;
    }
    void ResetMotion()
    {
        visualRoll = visualPitch = rollVelocity = pitchVelocity = 0f;
        calculatedBodyRoll = telemetry != null ? telemetry.PhysicalBodyRoll : 0f;
        calculatedBodyPitch = telemetry != null ? telemetry.PhysicalBodyPitch : 0f;
        if (pivot != null) pivot.localRotation = restRotation;
    }
    void OnDisable()
    {
        ResetMotion();
        if (axlePoses != null) foreach (var axle in axlePoses)
            if (axle.visual != null) { axle.visual.localPosition = axle.restPosition; axle.visual.localRotation = axle.restRotation; }
    }
    void OnDestroy()
    {
        if (pivot == null || visualModel == null || !gameObject.scene.isLoaded) return;
        // Removing just this component restores the original visual hierarchy.
        foreach (var pose in parts)
            if (pose.part != null && pose.parent != null)
            {
                pose.part.SetParent(pose.parent, false); pose.part.localPosition = pose.position;
                pose.part.localRotation = pose.rotation; pose.part.localScale = pose.scale;
                pose.part.SetSiblingIndex(pose.sibling);
            }
        if (pivot != null)
        {
            if (Application.isPlaying) Destroy(pivot.gameObject);
            else DestroyImmediate(pivot.gameObject);
        }
    }
}

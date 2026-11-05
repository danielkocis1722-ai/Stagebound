using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Isolated native WheelCollider experiment. No game input, FFB, settings or scene writes.
public static class YawBuildupValidation
{
    const string Folder = "Logs/YawBuildup";
    [MenuItem("Stagebound/Start live yaw recording")]
    static void StartRecording()
    {
        if (!EditorApplication.isPlaying) return;
        var car = UnityEngine.Object.FindAnyObjectByType<CarController>();
        if (car == null) return;
        var recorder = car.GetComponent<YawBuildupTelemetry>();
        if (recorder == null) recorder = car.gameObject.AddComponent<YawBuildupTelemetry>();
        recorder.Save(); recorder.Begin();
    }
    [MenuItem("Stagebound/Stop live yaw recording")]
    static void StopRecording()
    {
        foreach (var recorder in UnityEngine.Object.FindObjectsByType<YawBuildupTelemetry>()) recorder.Save();
    }
    [InitializeOnLoadMethod]
    static void Schedule() => EditorApplication.delayCall += () =>
    {
        if (!File.Exists(Folder + "/run.request") || EditorApplication.isPlaying) return;
        File.Delete(Folder + "/run.request");
        Run();
    };

    [MenuItem("Stagebound/Validate yaw buildup (isolated physics)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play Mode before the isolated yaw test."); return; }
        Directory.CreateDirectory(Folder);
        var report = new StringBuilder("ISOLATED WHEELCOLLIDER TEST: no engine/clutch, combined-slip modifiers, recovery, anti-roll or human driver.\n");
        try
        {
            foreach (var name in new[] { "AE86", "RX7", "SkylineR32" })
            {
                var config = AssetDatabase.LoadAssetAtPath<VehicleConfig>($"Assets/Vehicles/Configs/{name}_VehicleConfig.asset");
                float candidate = name == "AE86" ? 1.2f : name == "RX7" ? 1.25f : 1.3f;
                foreach (float speed in new[] { 40f, 70f, 100f })
                    foreach (float multiplier in new[] { 1f, candidate })
                        foreach (float catchAngle in new[] { 0f, 8f, 20f, 40f })
                            Probe(config, name, speed, multiplier, report, catchAngle);
            }
            report.AppendLine("COMPLETE (synthetic fixture, not gameplay acceptance)");
        }
        catch (Exception error) { report.AppendLine("FAIL " + error); Debug.LogException(error); }
        File.WriteAllText(Folder + "/summary.txt", report.ToString());
    }

    static void Probe(VehicleConfig c, string name, float speed, float multiplier, StringBuilder report, float catchAngle)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var floor = new GameObject("Temporary flat pad");
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.position = new Vector3(0, -.5f, 0);
            floor.AddComponent<BoxCollider>().size = new Vector3(2000, 1, 2000);
            var root = new GameObject("Temporary " + name);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(0, .7f, 0);
            var body = root.AddComponent<Rigidbody>();
            body.mass = c.chassis.mass;
            body.linearDamping = c.chassis.linearDamping;
            body.angularDamping = c.chassis.angularDamping;
            body.centerOfMass = c.chassis.centerOfMass;
            var shell = new GameObject("Chassis"); shell.transform.SetParent(root.transform, false);
            shell.transform.SetLocalPositionAndRotation(c.chassis.colliderPosition, c.chassis.colliderRotation);
            shell.transform.localScale = c.chassis.colliderScale;
            var box = shell.AddComponent<BoxCollider>(); box.center = c.chassis.colliderCenter; box.size = c.chassis.colliderSize;
            var configs = new[] { c.frontLeft, c.frontRight, c.backLeft, c.backRight };
            var wheels = new WheelCollider[4];
            for (int i = 0; i < 4; i++)
            {
                var wheelObject = new GameObject("Wheel " + i); wheelObject.transform.SetParent(root.transform, false);
                wheels[i] = wheelObject.AddComponent<WheelCollider>(); configs[i].Apply(wheels[i]);
            }
            body.ResetInertiaTensor(); wheels[0].ResetSprungMasses();
            Vector3 original = body.inertiaTensor;
            // These chassis are axis-aligned. Reject an invalid simplifying assumption.
            if (Mathf.Abs(Vector3.Dot(body.inertiaTensorRotation * Vector3.up, Vector3.up)) < .99f)
                throw new Exception("Fixture inertia principal Y is not vehicle vertical");
            VehicleConfig.ApplyYawInertia(body, multiplier);
            if (Mathf.Abs(body.inertiaTensor.y - original.y * multiplier) > .1f ||
                Mathf.Abs(body.inertiaTensor.x - original.x) > .1f || Mathf.Abs(body.inertiaTensor.z - original.z) > .1f)
                throw new Exception("Yaw inertia scaling changed the wrong principal axis");
            var physics = scene.GetPhysicsScene();
            if (!physics.IsValid() || physics == Physics.defaultPhysicsScene)
                throw new Exception("Preview scene must have isolated physics");
            const float dt = .02f;
            for (int i = 0; i < 150; i++) physics.Simulate(dt);
            body.linearVelocity = Vector3.forward * (speed / 3.6f);
            for (int i = 0; i < 100; i++) physics.Simulate(dt);
            if (!wheels[0].isGrounded || !wheels[1].isGrounded || !wheels[2].isGrounded || !wheels[3].isGrounded)
                throw new Exception("Fixture failed four-wheel ground contact");
            body.linearVelocity = root.transform.forward * (speed / 3.6f); body.angularVelocity = Vector3.zero;
            var csv = new StringBuilder("t,speed,steer,throttle,sideslip,yaw_rate,yaw_accel,fl_side,fr_side,rl_side,rr_side,rl_load,rr_load,rl_forward,rr_forward,rl_stiffness,rr_stiffness,rl_extremum_slip,rl_extremum_value,rl_asymptote_slip,rl_asymptote_value\n");
            float previousYaw = 0f, peakYaw = 0f, peakAcceleration = 0f, peakSlip = 0f, first20 = -1f;
            float reactionAt = float.PositiveInfinity, finalSlip = 0f;
            for (int step = 0; step < 250; step++)
            {
                float t = step * dt;
                float steer = Mathf.Lerp(0f, 6f, Mathf.Clamp01(t));
                float throttle = Mathf.Lerp(.2f, .7f, Mathf.Clamp01(t / 2f));
                if (catchAngle > 0f && Mathf.Abs(finalSlip) >= catchAngle && float.IsPositiveInfinity(reactionAt)) reactionAt = t + .2f;
                if (t >= reactionAt)
                {
                    // Explicit synthetic feedback driver: not the production input system.
                    steer = Mathf.Clamp(.65f * finalSlip - .1f * previousYaw, -25f, 25f);
                    throttle = .15f;
                }
                wheels[0].steerAngle = wheels[1].steerAngle = steer;
                wheels[2].motorTorque = wheels[3].motorTorque = 600f * throttle;
                physics.Simulate(dt);
                float yaw = Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg;
                float accel = (yaw - previousYaw) / dt; previousYaw = yaw;
                float slip = Vector3.SignedAngle(Vector3.ProjectOnPlane(root.transform.forward, Vector3.up), Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up), Vector3.up);
                finalSlip = slip;
                peakYaw = Mathf.Max(peakYaw, Mathf.Abs(yaw)); peakAcceleration = Mathf.Max(peakAcceleration, Mathf.Abs(accel)); peakSlip = Mathf.Max(peakSlip, Mathf.Abs(slip));
                if (first20 < 0f && Mathf.Abs(slip) >= 20f) first20 = t;
                wheels[0].GetGroundHit(out var fl); wheels[1].GetGroundHit(out var fr); wheels[2].GetGroundHit(out var rl); wheels[3].GetGroundHit(out var rr);
                var curve = wheels[2].sidewaysFriction;
                csv.AppendLine(FormattableString.Invariant($"{t:F3},{body.linearVelocity.magnitude * 3.6f:F3},{steer:F3},{throttle:F3},{slip:F3},{yaw:F3},{accel:F3},{fl.sidewaysSlip:F4},{fr.sidewaysSlip:F4},{rl.sidewaysSlip:F4},{rr.sidewaysSlip:F4},{rl.force:F2},{rr.force:F2},{rl.forwardSlip:F4},{rr.forwardSlip:F4},{curve.stiffness:F4},{wheels[3].sidewaysFriction.stiffness:F4},{curve.extremumSlip},{curve.extremumValue},{curve.asymptoteSlip},{curve.asymptoteValue}"));
            }
            string variant = multiplier == 1f ? "baseline" : "candidate";
            File.WriteAllText($"{Folder}/{name}-{speed}-{variant}-catch{catchAngle}.csv", csv.ToString());
            report.AppendLine(FormattableString.Invariant($"{name} {speed}kmh {variant} catch={catchAngle} Iyy={body.inertiaTensor.y:F2} (base {original.y:F2}) peakYaw={peakYaw:F2} peakAccel={peakAcceleration:F2} peakSlip={peakSlip:F2} first20deg={first20:F2} reactionAt={reactionAt:F2} finalSlip={finalSlip:F2}"));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}

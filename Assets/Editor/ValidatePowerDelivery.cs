using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Explicit editor-only native driving experiments. No driver ships in the game.
public static class ValidatePowerDelivery
{
    const string Output = "Library/PowerDelivery";
    static readonly StringBuilder report = new StringBuilder();
    static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    static IEnumerator steps;
    static Gamepad pad;
    static InputSettings previousSettings;
    static InputDevice[] disabled;
    static bool previousOptionsEnabled, hadSelection;
    static EnterPlayModeOptions previousOptions;
    static string selection, variant = "baseline";
    static int frame = -1;
    static double deadline;
    static bool probe, comparison, controls;
    static WheelCollider[] Wheels(CarController c) => new[] { c.frontLeft, c.frontRight, c.backLeft, c.backRight };
    public static void Probe() { probe = true; Run(); }
    public static void Candidate() { variant = "candidate"; Run(); }
    public static void CandidateProbe() { variant = "candidate"; probe = true; Run(); }
    public static void Final() { variant = "final"; Run(); }
    public static void BaselineSlides() { variant = "baseline-slides"; Run(); }
    public static void Coupling() { variant = "coupling"; Run(); }
    public static void DeliveryComparison() { comparison = true; variant = "delivery-before"; Run(); }
    public static void ApplyAndValidate() { AuthorAssets(); controls = true; variant = "final-controls"; Run(); }
    public static void Run()
    {
        Directory.CreateDirectory(Output + "/" + variant);
        previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        previousOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        hadSelection = PlayerPrefs.HasKey(VehicleSelection.PlayerPrefsKey);
        selection = PlayerPrefs.GetString(VehicleSelection.PlayerPrefsKey);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        steps = Tests(); deadline = EditorApplication.timeSinceStartup + 1800;
        EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new Exception("Timeout")); return; }
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            var current = stack.Count > 0 ? stack.Peek() : steps;
            while (true)
            {
                if (!current.MoveNext())
                {
                    if (stack.Count == 0) { Finish(null); return; }
                    stack.Pop(); current = stack.Count > 0 ? stack.Peek() : steps; continue;
                }
                if (current.Current is IEnumerator child) { stack.Push(child); current = child; continue; }
                break;
            }
        }
        catch (Exception e) { Finish(e); }
    }
    static void Save() => File.WriteAllText(Output + "/" + (comparison ? "delivery-comparison" : variant) + (probe ? "-probe" : "") + ".txt", report.ToString());
    static void Finish(Exception e)
    {
        EditorApplication.update -= Tick; report.AppendLine(e == null ? "COMPLETE" : "FAIL " + e); Save();
        if (pad != null) InputSystem.RemoveDevice(pad);
        if (previousSettings != null) InputSystem.settings = previousSettings;
        if (disabled != null) foreach (var d in disabled) if (d.added) InputSystem.EnableDevice(d);
        if (hadSelection) PlayerPrefs.SetString(VehicleSelection.PlayerPrefsKey, selection); else PlayerPrefs.DeleteKey(VehicleSelection.PlayerPrefsKey);
        PlayerPrefs.Save(); Time.captureDeltaTime = 0;
        EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
        EditorApplication.Exit(e == null ? 0 : 1);
    }
    static void Input(float steer = 0, float throttle = 0)
        => InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(steer, 0), rightTrigger = throttle });
    static IEnumerator Wait(int count) { for (int i = 0; i < count; i++) yield return null; }
    static IEnumerator Reset(CarController c)
    {
        Input(); c.enabled = false;
        var b = c.GetComponent<Rigidbody>();
        b.position = new Vector3(0, -999.25f, 0); b.rotation = Quaternion.identity;
        b.linearVelocity = b.angularVelocity = Vector3.zero;
        foreach (var w in Wheels(c)) w.motorTorque = w.brakeTorque = w.steerAngle = 0;
        Physics.SyncTransforms(); yield return Wait(90);
        c.currentGear = 0; c.enabled = true; yield return Wait(120);
        if (Wheels(c).Any(w => !w.isGrounded)) throw new Exception("Fixture lacks wheel contact");
    }
    // A test driver uses ordinary steering input to attempt a 28-degree slide.
    // It never writes pose, velocity, torque, grip or RPM during measurement.
    static float DriftSteering(CarController c)
    {
        return Mathf.Clamp((c.VehicleSlipAngle - 28f) * .035f - c.YawRate * .004f, -.9f, .9f);
    }
    static float FlyingSteering(CarController c)
    {
        var b=c.GetComponent<Rigidbody>();
        var front=(c.frontLeft.transform.position+c.frontRight.transform.position)*.5f;
        var local=c.transform.InverseTransformDirection(b.GetPointVelocity(front));
        float velocityAngle=Mathf.Atan2(local.x,Mathf.Max(1f,local.z))*Mathf.Rad2Deg;
        float desiredYaw=-4.5f/Mathf.Max(5f,c.SpeedKmh/3.6f)*Mathf.Rad2Deg;
        float angle=velocityAngle-11f+(c.VehicleSlipAngle-20f)*.8f+(desiredYaw-c.YawRate)*.12f;
        float limit=Mathf.Lerp(c.maxSteerAngle,c.highSpeedMaxSteerAngle,Mathf.SmoothStep(0,1,Mathf.InverseLerp(c.fullLowSpeedLockKmh,c.highSpeedLockKmh,c.SpeedKmh)));
        float rack=Mathf.Clamp(angle/limit,-.95f,.95f);
        float exponent=Mathf.Lerp(c.lowSpeedSteeringExponent,c.highSpeedSteeringExponent,Mathf.SmoothStep(0,1,Mathf.Clamp01(c.SpeedKmh/c.fullSteeringCurveSpeed)));
        float axis=c.steeringDeadzone+(1-c.steeringDeadzone)*Mathf.Pow(Mathf.Abs(rack),1/exponent);
        return Mathf.Sign(rack)*Mathf.Lerp(InputSystem.settings.defaultDeadzoneMin,InputSystem.settings.defaultDeadzoneMax,axis);
    }
    public static AnimationCurve CandidateCurve(string id)
    {
        float[] values = id=="ae86" ? new[]{.30f,.55f,.78f,.95f,1f,.90f,.65f,.32f,.12f}
            : id=="skyline-r32" ? new[]{.25f,.50f,.75f,.95f,1f,.90f,.65f,.32f,.12f}
            : new[]{.25f,.48f,.72f,.94f,1f,.90f,.62f,.30f,.10f};
        float[] rpm = {900,2000,3000,4000,5000,6000,6500,7000,7500};
        return new AnimationCurve(rpm.Select((r,i)=>new Keyframe(r,values[i],0,0)).ToArray());
    }
    static AnimationCurve BeforeCurve()
    {
        float[] rpm={900,2000,3000,4000,5000,6000,7500};
        float[] value={.6f,.84f,.95f,1f,1f,.9f,.65f};
        return new AnimationCurve(rpm.Select((r,i)=>new Keyframe(r,value[i],0,0)).ToArray());
    }
    static void AuthorAssets()
    {
        if(!Path.GetFullPath(Application.dataPath).Replace('\\','/').EndsWith("/Stagebound-real-scale/Assets"))throw new Exception("Experimental worktree only");
        var text=new StringBuilder();
        foreach(var d in RealScaleMigration.Vehicles)
        {
            var before=BeforeCurve();var after=CandidateCurve(d.vehicleId);
            foreach(var pair in new[]{("before",before),("after",after)})
            {
                float peak=0,at=0;
                for(int rpm=900;rpm<=7500;rpm++)
                {
                    float kw=d.config.engine.maxEngineTorque*pair.Item2.Evaluate(rpm)*rpm*(2*Mathf.PI/60)/1000;
                    if(kw>peak){peak=kw;at=rpm;}
                }
                text.AppendLine(FormattableString.Invariant($"POWER {d.vehicleId} {pair.Item1} peakNm={d.config.engine.maxEngineTorque} peakKW={peak:F3} atRPM={at}"));
            }
            d.config.engine.torqueCurve=after;EditorUtility.SetDirty(d.config);
        }
        AssetDatabase.SaveAssets();File.WriteAllText(Output+"/authored.txt",text.ToString());
    }
    static IEnumerator Case(CarController c, string id, int gear, float speed, float throttle, bool drift, bool flying=false)
    {
        yield return Reset(c);
        var b = c.GetComponent<Rigidbody>();
        b.linearVelocity = Quaternion.Euler(0, drift ? 28 : 0, 0) * Vector3.forward * (speed / 3.6f);
        if (drift) b.angularVelocity = Vector3.down * (flying ? 4.5f/Mathf.Max(5,speed/3.6f) : 20*Mathf.Deg2Rad);
        // Condition rolling wheel RPM with the native solver, then connect the engine once.
        yield return Wait(6); c.currentGear = gear;
        c.EngineRPM = Mathf.Clamp(Mathf.Abs(c.SignedRearWheelRPM) * c.gearRatios[gear - 1] * c.finalDrive, c.idleRPM, c.redlineRPM);
        float began = Time.time, lastFixed = -1;
        float duration = 0, drifting = 0, limited = 0, speedSum = 0, rpmSum = 0, wheelSum = 0, forwardSum = 0, sideSum = 0, torqueSum = 0;
        float bandSeconds = 0, bandRPM = 0, bandWheel = 0, bandSlip = 0;
        float minimumSpeed = float.PositiveInfinity, maximumSpeed = 0;
        var csv = new StringBuilder("seconds,speedKmh,longitudinalKmh,engineRPM,gear,RLrpm,RRrpm,RLrollingKmh,RRrollingKmh,RLforwardSlip,RRforwardSlip,RLsidewaysSlip,RRsidewaysSlip,throttle,engineThrottle,engineTorqueNm,RLmotorNm,RRmotorNm,clutchTorqueNm,clutchRPMDifference,bodySlipDeg,yawDegPerSec,drifting,steer\n");
        while (Time.time - began < 10)
        {
            // The first two seconds of slide cases deliberately establish wheelspin.
            Input(drift ? (flying ? FlyingSteering(c) : DriftSteering(c)) : 0, drift && !flying && Time.time - began < 2 ? 1 : throttle);
            yield return null;
            if (Mathf.Approximately(Time.fixedTime, lastFixed)) continue;
            lastFixed = Time.fixedTime;
            var l = c.backLeft; var r = c.backRight;
            bool left = l.GetGroundHit(out var lh), right = r.GetGroundHit(out var rh);
            float wheelSpeed = (Mathf.Abs(l.rpm) * l.radius + Mathf.Abs(r.rpm) * r.radius) * Mathf.PI * .06f;
            float sl = left ? lh.forwardSlip : 0, sr = right ? rh.forwardSlip : 0;
            bool isDrift = c.GetComponent<DriftTelemetry>().IsDrifting;
            csv.AppendLine(FormattableString.Invariant($"{Time.time-began:F3},{c.SpeedKmh:F4},{c.SignedLongitudinalSpeedKmh:F4},{c.EngineRPM:F3},{c.currentGear},{l.rpm:F3},{r.rpm:F3},{Mathf.Abs(l.rpm)*l.radius*2*Mathf.PI*.06f:F4},{Mathf.Abs(r.rpm)*r.radius*2*Mathf.PI*.06f:F4},{sl:F5},{sr:F5},{lh.sidewaysSlip:F5},{rh.sidewaysSlip:F5},{c.ThrottleInput:F3},{c.EngineThrottle:F3},{c.EngineOutputTorque:F3},{l.motorTorque:F3},{r.motorTorque:F3},{c.ClutchCouplingTorque:F3},{c.ClutchRPMDifference:F3},{c.VehicleSlipAngle:F3},{c.YawRate:F3},{isDrift},{c.SteeringInput:F4}"));
            if (Time.time - began < (flying ? 1f : 2.5f)) continue;
            float dt = Time.fixedDeltaTime;
            duration += dt; if (isDrift) drifting += dt; if (c.EngineRPM > 7100) limited += dt;
            speedSum += c.SpeedKmh * dt; rpmSum += c.EngineRPM * dt; wheelSum += wheelSpeed * dt;
            forwardSum += (Mathf.Abs(sl) + Mathf.Abs(sr)) * .5f * dt;
            sideSum += (Mathf.Abs(lh.sidewaysSlip) + Mathf.Abs(rh.sidewaysSlip)) * .5f * dt;
            torqueSum += (l.motorTorque + r.motorTorque) * dt;
            minimumSpeed = Mathf.Min(minimumSpeed, c.SpeedKmh); maximumSpeed = Mathf.Max(maximumSpeed, c.SpeedKmh);
            if (isDrift && c.SpeedKmh >= 35 && c.SpeedKmh <= 50)
            {
                bandSeconds += dt; bandRPM += c.EngineRPM * dt; bandWheel += wheelSpeed * dt; bandSlip += (Mathf.Abs(sl) + Mathf.Abs(sr)) * .5f * dt;
            }
        }
        string name = id + "-g" + gear + "-" + speed + "-t" + throttle.ToString("F1", CultureInfo.InvariantCulture) + (drift ? (flying ? "-flying" : "-slide") : "-straight");
        File.WriteAllText(Output + "/" + variant + "/" + name + ".csv", csv.ToString());
        report.AppendLine(FormattableString.Invariant($"CASE {variant}/{name}: meanSpeed={speedSum/duration:F3} speedRange={minimumSpeed:F2}-{maximumSpeed:F2} meanRPM={rpmSum/duration:F2} wheelRoadKmh={wheelSum/duration:F3} forwardSlip={forwardSum/duration:F4} sideSlip={sideSum/duration:F4} axleMotorNm={torqueSum/duration:F2} driftSeconds={drifting:F3} limiterSeconds={limited:F3} band35to50DriftSeconds={bandSeconds:F3} bandRPM={bandRPM/Mathf.Max(.001f,bandSeconds):F2} bandWheelKmh={bandWheel/Mathf.Max(.001f,bandSeconds):F3} bandSlip={bandSlip/Mathf.Max(.001f,bandSeconds):F4}")); Save();
    }
    static IEnumerator Tests()
    {
        yield return Wait(10); Time.captureDeltaTime = 1f / 60;
        previousSettings = InputSystem.settings;
        var settings = UnityEngine.Object.Instantiate(previousSettings);
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = settings;
        disabled = InputSystem.devices.Where(d => d is Gamepad || d is Keyboard).ToArray();
        foreach (var d in disabled) InputSystem.DisableDevice(d);
        pad = InputSystem.AddDevice<Gamepad>();
        if(controls){yield return ControlTests();yield break;}
        if(comparison){yield return CompareDelivery();yield break;}
        foreach (var definition in RealScaleMigration.Vehicles)
        {
            PlayerPrefs.SetString(VehicleSelection.PlayerPrefsKey, definition.vehicleId);
            SceneManager.LoadScene("SampleScene"); yield return Wait(90);
            var c = UnityEngine.Object.FindAnyObjectByType<CarController>();
            if(variant=="candidate")c.torqueCurve=CandidateCurve(definition.vehicleId);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Temporary power delivery pad"; floor.transform.position = new Vector3(0, -1000.5f, 0); floor.transform.localScale = new Vector3(6000, 1, 6000);
            for (int i = 0; i < c.gearRatios.Length; i++) report.AppendLine(FormattableString.Invariant($"GEAR {definition.vehicleId}/{i+1}: ratio={c.gearRatios[i]:F7} FD={c.finalDrive:F3} radius={(c.backLeft.radius+c.backRight.radius)*.5f:F7} redlineKmh={c.redlineRPM*Mathf.PI*(c.backLeft.radius+c.backRight.radius)*.06f/(c.gearRatios[i]*c.finalDrive):F3}"));
            if (probe)
            {
                foreach(float throttle in new[]{.4f,.6f,.8f,1f}) yield return Case(c, definition.vehicleId, 3, 40, throttle, true);
                continue;
            }
            foreach(float speed in new[]{40f,60f,80f,100f}) yield return Case(c, definition.vehicleId, 3, speed, .6f, false);
            foreach(int gear in new[]{2,3}) foreach(float speed in gear==2?new[]{50f,65f,80f}:new[]{40f,70f,90f,110f})
                foreach(float throttle in new[]{.4f,.6f,.8f,1f}) yield return Case(c, definition.vehicleId, gear, speed, throttle, true);
        }
    }
    static IEnumerator CompareDelivery()
    {
        foreach(var definition in RealScaleMigration.Vehicles)
        {
            PlayerPrefs.SetString(VehicleSelection.PlayerPrefsKey,definition.vehicleId);
            SceneManager.LoadScene("SampleScene");yield return Wait(90);
            var c=UnityEngine.Object.FindAnyObjectByType<CarController>();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name="Temporary power delivery pad";floor.transform.position=new Vector3(0,-1000.5f,0);floor.transform.localScale=new Vector3(6000,1,6000);
            foreach(bool changed in new[]{false,true})
            {
                variant=changed?"delivery-after":"delivery-before";Directory.CreateDirectory(Output+"/"+variant);
                c.torqueCurve=changed?CandidateCurve(definition.vehicleId):BeforeCurve();
                foreach(float speed in new[]{40f,60f,80f,100f})yield return Case(c,definition.vehicleId,3,speed,.6f,false);
                foreach(int gear in new[]{2,3})foreach(float speed in gear==2?new[]{50f,80f}:new[]{40f,75f,110f})
                    foreach(float throttle in new[]{.4f,.6f,.8f,1f})yield return Case(c,definition.vehicleId,gear,speed,throttle,true,true);
                // Reproduce the established-wheelspin 40 km/h third-gear case too.
                foreach(float throttle in new[]{.4f,.6f,.8f,1f})yield return Case(c,definition.vehicleId,3,40,throttle,true);
            }
        }
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);report.AppendLine("PASS "+message);Save();}
    static IEnumerator ControlTests()
    {
        foreach(var d in RealScaleMigration.Vehicles)
        {
            PlayerPrefs.SetString(VehicleSelection.PlayerPrefsKey,d.vehicleId);SceneManager.LoadScene("SampleScene");yield return Wait(90);
            var c=UnityEngine.Object.FindAnyObjectByType<CarController>();var b=c.GetComponent<Rigidbody>();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-1000.5f,0);floor.transform.localScale=new Vector3(6000,1,6000);
            Check(c.torqueCurve.keys.Select(k=>k.value).SequenceEqual(d.config.engine.torqueCurve.keys.Select(k=>k.value)),d.vehicleId+" uses authoritative tuned curve");
            yield return Reset(c);c.currentGear=1;Input(throttle:1);yield return Wait(360);
            report.AppendLine($"LAUNCH {d.vehicleId} speed={c.SpeedKmh:F3} RPM={c.EngineRPM:F3}");
            Check(c.SignedLongitudinalSpeedKmh>20&&float.IsFinite(c.EngineRPM),d.vehicleId+" full-throttle launch");
            InputSystem.QueueStateEvent(pad,new GamepadState{rightTrigger=.6f}.WithButton(GamepadButton.RightShoulder));yield return Wait(2);Input(throttle:.6f);yield return Wait(30);
            Check(c.currentGear==2,d.vehicleId+" ordinary controller upshift");
            yield return Reset(c);b.linearVelocity=Vector3.forward*(60/3.6f);yield return Wait(20);c.currentGear=2;
            c.EngineRPM=Mathf.Clamp(Mathf.Abs(c.SignedRearWheelRPM)*c.gearRatios[1]*c.finalDrive,c.idleRPM,c.redlineRPM);Input(throttle:.6f);yield return Wait(60);
            InputSystem.QueueStateEvent(pad,new GamepadState{rightTrigger=1}.WithButton(GamepadButton.West));yield return Wait(60);
            Check(c.ClutchInput>.9f&&c.ClutchEngagement<.01f,d.vehicleId+" manual clutch disconnects");
            Input(throttle:.6f);float peak=0,kick=0;
            for(int i=0;i<180;i++){yield return null;peak=Mathf.Max(peak,c.backLeft.motorTorque+c.backRight.motorTorque);kick=Mathf.Max(kick,c.ClutchKickTorque);}
            report.AppendLine($"CLUTCH {d.vehicleId} peakAxleNm={peak:F3} peakKickNm={kick:F3} exitSpeed={c.SpeedKmh:F3}");
            Check(c.ClutchEngagement>.99f&&peak>0&&kick>0,d.vehicleId+" clutch reconnects with physical kick");
            yield return Reset(c);b.linearVelocity=Vector3.forward*(100/3.6f);yield return Wait(30);b.linearVelocity=Vector3.forward*(100/3.6f);
            var last=b.position;float distance=0,start=Time.time;
            InputSystem.QueueStateEvent(pad,new GamepadState{leftTrigger=1});
            while(b.linearVelocity.magnitude>.1f&&Time.time-start<6){yield return null;distance+=Vector3.Distance(last,b.position);last=b.position;}
            report.AppendLine($"BRAKE {d.vehicleId} distance={distance:F3} seconds={Time.time-start:F3}");
            Check(b.linearVelocity.magnitude<=.1f&&distance<55,d.vehicleId+" existing service braking remains effective");
        }
    }
}

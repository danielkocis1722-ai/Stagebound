using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ValidateRealScale
{
    static IEnumerator steps;
    static Stack<IEnumerator> stack = new Stack<IEnumerator>();
    static StringBuilder log = new StringBuilder();
    static Gamepad pad;
    static InputSettings previousSettings;
    static InputDevice[] disabled;
    static EnterPlayModeOptions previousOptions;
    static bool previousOptionsEnabled;
    static int frame = -1;
    static double deadline;
    static string variant;
    static bool refine;
    public static void Refine() { refine=true; Run(); }
    static WheelCollider[] Wheels(CarController c) => new[] {c.frontLeft,c.frontRight,c.backLeft,c.backRight};
    static void Save() => File.WriteAllText("Library/RealScale/"+(refine?"refined-results":"results")+".txt", log.ToString());
    static void Check(bool condition,string message) { if(!condition)throw new Exception(message);log.AppendLine("PASS "+message); }
    public static void Run()
    {
        Directory.CreateDirectory("Library/RealScale");
        previousOptionsEnabled=EditorSettings.enterPlayModeOptionsEnabled;
        previousOptions=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;
        EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene(RealScaleExperiment.ScenePath);
        steps=Tests();deadline=EditorApplication.timeSinceStartup+900;
        EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup>deadline){Finish(new Exception("Timeout"));return;}
        if(!EditorApplication.isPlaying||Time.frameCount==frame)return;
        frame=Time.frameCount;
        try
        {
            var current=stack.Count>0?stack.Peek():steps;
            while(true)
            {
                if(!current.MoveNext())
                {
                    if(stack.Count==0){Finish(null);return;}
                    stack.Pop();current=stack.Count>0?stack.Peek():steps;continue;
                }
                if(current.Current is IEnumerator child){stack.Push(child);current=child;continue;}
                break;
            }
        }
        catch(Exception e){Finish(e);}
    }
    static void Finish(Exception e)
    {
        EditorApplication.update-=Tick;log.AppendLine(e==null?"COMPLETE":"FAIL "+e);Save();
        if(pad!=null)InputSystem.RemoveDevice(pad);
        if(previousSettings!=null)InputSystem.settings=previousSettings;
        if(disabled!=null)foreach(var d in disabled)if(d.added)InputSystem.EnableDevice(d);
        EditorSettings.enterPlayModeOptionsEnabled=previousOptionsEnabled;EditorSettings.enterPlayModeOptions=previousOptions;
        Time.captureDeltaTime=0;EditorApplication.Exit(e==null?0:1);
    }
    static void Input(float steer=0,float throttle=0,float brake=0,bool handbrake=false)
    {
        var state=new GamepadState{leftStick=new Vector2(steer,0),rightTrigger=throttle,leftTrigger=brake};
        InputSystem.QueueStateEvent(pad,handbrake?state.WithButton(GamepadButton.South):state);
    }
    static IEnumerator Wait(int n){for(int i=0;i<n;i++)yield return null;}
    static IEnumerator Reset(CarController car)
    {
        car.enabled=false;Input();var b=car.GetComponent<Rigidbody>();
        b.position=new Vector3(0,1.25f,0);b.rotation=Quaternion.identity;b.linearVelocity=b.angularVelocity=Vector3.zero;
        foreach(var w in Wheels(car)){w.brakeTorque=w.motorTorque=w.steerAngle=0;}
        Physics.SyncTransforms();yield return Wait(120);car.currentGear=0;car.enabled=true;
        // Let the real controller unwind its internal rack, throttle, clutch and
        // grip state before seeding the next independent case. Clearing only the
        // collider angle would retain the previous case's rack target/state.
        Input();yield return Wait(120);
        Check(Mathf.Abs(car.frontLeft.steerAngle)<.001f&&Mathf.Abs(car.frontRight.steerAngle)<.001f&&car.EngineThrottle<.001f&&car.EngineRPM<=car.idleRPM+10,variant+" fixture begins with neutral rack/throttle/idle engine");
    }
    static string WheelData(CarController c) => string.Join(" | ",Wheels(c).Select(w=>{w.GetGroundHit(out var h);return $"{w.name}: rpm={w.rpm:F1} longSlip={h.forwardSlip:F3} sideSlip={h.sidewaysSlip:F3} load={h.force:F1} brake={w.brakeTorque:F1}";}));
    static IEnumerator RollingFixture(CarController car,int gear)
    {
        var body=car.GetComponent<Rigidbody>();
        body.linearVelocity=Vector3.forward*(100/3.6f);Input();yield return Wait(30);
        // Arrange a consistent rolling engine/shaft state instead of injecting
        // a neutral-idle-to-fourth engine-braking transient into a steering test.
        car.currentGear=gear;
        car.EngineRPM=Mathf.Clamp(Mathf.Abs(car.SignedRearWheelRPM)*car.gearRatios[gear-1]*car.finalDrive,car.idleRPM,car.redlineRPM);
        Input(throttle:.15f);yield return Wait(45);
        log.AppendLine($"ROLLING START {variant}: speed={car.SpeedKmh:F2} engineRPM={car.EngineRPM:F1} shaftRPM={car.SignedDrivetrainRPM:F1} clutch={car.ClutchEngagement:F3}");
    }
    static void Capture()
    {
        var camera=UnityEngine.Object.FindAnyObjectByType<FollowCamera>().GetComponent<Camera>();
        var rt=new RenderTexture(1280,720,24);
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
        File.WriteAllBytes("Library/RealScale/"+variant+"-camera.png",texture.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(rt);
    }
    static IEnumerator Tests()
    {
        yield return Wait(10);Time.captureDeltaTime=1f/60;
        previousSettings=InputSystem.settings;var settings=UnityEngine.Object.Instantiate(previousSettings);
        settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=settings;disabled=InputSystem.devices.Where(d=>d is Gamepad||d is Keyboard).ToArray();
        foreach(var d in disabled)InputSystem.DisableDevice(d);pad=InputSystem.AddDevice<Gamepad>();
        foreach(int mode in new[]{0,1,2})
        {
            variant=mode==0?"presentation":mode==1?"real-governor-on":"real-governor-off";
            SceneManager.LoadScene(mode==0?"AE86_Presentation_Comparison":"AE86_RealScale_Test");yield return Wait(90);
            var car=UnityEngine.Object.FindAnyObjectByType<CarController>();var body=car.GetComponent<Rigidbody>();
            var governor=car.GetComponent<DriftYawStability>();governor.enabled=mode!=2;
            var config=car.GetComponent<VehicleRigLoader>().CurrentDefinition.config;
            Check(mode==0?config==RealScaleExperiment.Original.config:config==AssetDatabase.LoadAssetAtPath<VehicleConfig>(RealScaleExperiment.ConfigPath),variant+" uses intended config");
            log.AppendLine($"VARIANT {variant} mass={body.mass} COM={body.centerOfMass:F4} inertia={body.inertiaTensor:F3} rotation={body.inertiaTensorRotation.eulerAngles:F3} radius={car.frontLeft.radius:F4} angularDamping={body.angularDamping}");
            Check(Mathf.Approximately(10f*3.6f,36f)&&Mathf.Abs(27.78f*3.6f-100)<.01f,"Metres per second map directly to km/h");
            yield return Reset(car);Check(Wheels(car).All(w=>w.isGrounded),variant+" four-wheel static contact");
            yield return Wait(30);Capture();
            if(refine)
            {
                yield return Reset(car);body.linearVelocity=Vector3.forward*(10/3.6f);car.currentGear=1;
                float integral=0,radius=0,speed=0;int count=0;
                for(int i=0;i<600;i++)
                {
                    float error=10-car.SpeedKmh;integral=Mathf.Clamp(integral+error/60f,0,8);
                    Input(1,Mathf.Clamp(error*.08f+integral*.04f,0,.6f));yield return null;
                    if(i>360&&Mathf.Abs(car.YawRate)>1){radius+=body.linearVelocity.magnitude/(Mathf.Abs(car.YawRate)*Mathf.Deg2Rad);speed+=car.SpeedKmh;count++;}
                }
                log.AppendLine($"MATCHED TURN {variant}: CG radius={radius/count:F3} speed={speed/count:F3}");
                foreach(float amplitude in new[]{.25f,.55f})
                {
                    yield return Reset(car);yield return RollingFixture(car,mode==0?2:4);
                    float yaw=0,slipAngle=0,smoke=0,skid=0,screech=0,frontGrip=1;
                    int driftFrames=0;
                    var refinedEffects=car.GetComponent<WheelEffects>();var refinedAudio=car.GetComponent<VehicleAudio>();
                    var responseTrace=new StringBuilder("time,speed,yawRate,slipAngle,drifting,frontGrip\n");
                    for(int i=0;i<360;i++)
                    {
                        float steering=i<60?amplitude:i<120?-amplitude:i<180?amplitude*.65f:0;
                        Input(steering,amplitude<.3f?.35f:.65f);yield return null;
                        yaw=Mathf.Max(yaw,Mathf.Abs(car.YawRate));slipAngle=Mathf.Max(slipAngle,Mathf.Abs(car.VehicleSlipAngle));
                        smoke=Mathf.Max(smoke,refinedEffects.wheels.Max(w=>w.emissionRate));skid=Mathf.Max(skid,refinedEffects.wheels.Max(w=>w.markIntensity));
                        if(refinedAudio.screechSource!=null)screech=Mathf.Max(screech,refinedAudio.screechSource.volume);
                        var telemetry=car.GetComponent<DriftTelemetry>();if(telemetry.IsDrifting)driftFrames++;
                        frontGrip=Mathf.Max(frontGrip,car.GetComponent<FrontAxleRecovery>().FrontLateralMultiplier);
                        if(i%6==0)responseTrace.AppendLine(FormattableString.Invariant($"{i/60f:F2},{car.SpeedKmh:F3},{car.YawRate:F3},{car.VehicleSlipAngle:F3},{telemetry.IsDrifting},{frontGrip:F3}"));
                    }
                    log.AppendLine($"RESPONSE {variant} amplitude={amplitude}: peakYaw={yaw:F2} peakSlip={slipAngle:F2} finalSpeed={car.SpeedKmh:F2} driftFrames={driftFrames} smokePeak={smoke:F2} skidPeak={skid:F2} screechPeak={screech:F2} frontGripMax={frontGrip:F3}");
                    File.WriteAllText("Library/RealScale/"+variant+"-"+amplitude.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-response.csv",responseTrace.ToString());
                }
                // Low-slip steering need not produce smoke. Exercise effects
                // separately with a real, deliberately locked rear-wheel coast.
                yield return Reset(car);body.linearVelocity=Vector3.forward*(50/3.6f);yield return Wait(30);
                Input(handbrake:true);float smokeTest=0,skidTest=0,screechTest=0;
                var effectTest=car.GetComponent<WheelEffects>();var soundTest=car.GetComponent<VehicleAudio>();
                for(int i=0;i<60;i++)
                {
                    yield return null;smokeTest=Mathf.Max(smokeTest,effectTest.wheels.Max(w=>w.emissionRate));skidTest=Mathf.Max(skidTest,effectTest.wheels.Max(w=>w.markIntensity));
                    if(soundTest.screechSource!=null)screechTest=Mathf.Max(screechTest,soundTest.screechSource.volume);
                }
                log.AppendLine($"LOCKED TIRE EFFECTS {variant}: smoke={smokeTest:F2} skid={skidTest:F2} screech={screechTest:F2}");
                Check(smokeTest>0&&skidTest>0&&screechTest>0,variant+" smoke, skidmark and tire-screech activity under measured lock");
                var keyboard=InputSystem.AddDevice<Keyboard>();
                bool enabled=governor.enabled;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F6));yield return Wait(3);
                Check(governor.enabled!=enabled,variant+" F6 toggles governor");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Wait(2);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F6));yield return Wait(3);
                Check(governor.enabled==enabled,variant+" F6 restores governor");
                InputSystem.RemoveDevice(keyboard);
                Save();Input();continue;
            }
            foreach(var w in Wheels(car))
            {
                w.GetWorldPose(out var center,out _);w.GetGroundHit(out var hit);
                float extension=Vector3.Dot(w.transform.TransformPoint(w.center)-center,car.transform.up);
                log.AppendLine($"STATIC {variant} {w.name}: extension={extension:F4}/{w.suspensionDistance} load={hit.force:F2} sprungMass={w.sprungMass:F2} hubHeight={center.y:F4}");
            }
            // Neutral coast plus a fixed initial speed; no corrections during stop.
            yield return Reset(car);body.linearVelocity=Vector3.forward*(100/3.6f);yield return Wait(60);
            body.linearVelocity=Vector3.forward*(100/3.6f);var origin=body.position;Input(brake:1);
            float elapsed=0,locked=0,slip=0;int samples=0;
            while(elapsed<15&&body.linearVelocity.magnitude>.5f)
            {
                yield return null;elapsed+=1f/60;
                if(car.SpeedKmh>10)foreach(var w in Wheels(car))if(w.GetGroundHit(out var hit)){samples++;slip+=Mathf.Abs(hit.forwardSlip);if(Mathf.Abs(w.rpm)<10||Mathf.Abs(hit.forwardSlip)>1)locked++;}
                if(Mathf.Abs(elapsed-.5f)<.009f)log.AppendLine("BRAKE WHEELS "+variant+" "+WheelData(car));
            }
            log.AppendLine($"BRAKE {variant}: distance={Vector3.Distance(origin,body.position):F3} time={elapsed:F3} lockFraction={locked/Mathf.Max(1,samples):F4} meanSlip={slip/Mathf.Max(1,samples):F3}");
            Check(elapsed<15,variant+" stops from 100 km/h");Save();
            // A speed-maintaining test driver queues only ordinary throttle input.
            yield return Reset(car);body.linearVelocity=Vector3.forward*(10/3.6f);car.currentGear=1;
            float radiusSum=0,speedSum=0,yawSum=0;int radiusSamples=0;
            for(int i=0;i<480;i++)
            {
                Input(1,Mathf.Clamp((10-car.SpeedKmh)*.08f,0,.3f));yield return null;
                if(i>240&&Mathf.Abs(car.YawRate)>1){radiusSum+=body.linearVelocity.magnitude/(Mathf.Abs(car.YawRate)*Mathf.Deg2Rad);speedSum+=car.SpeedKmh;yawSum+=Mathf.Abs(car.YawRate);radiusSamples++;}
            }
            log.AppendLine($"TURN {variant}: CG radius={radiusSum/Mathf.Max(1,radiusSamples):F3} speed={speedSum/Mathf.Max(1,radiusSamples):F2} yaw={yawSum/Mathf.Max(1,radiusSamples):F2} steer={car.frontLeft.steerAngle:F3}/{car.frontRight.steerAngle:F3}");
            foreach(float speed in new[]{30f,65f,100f})
            {
                yield return Reset(car);body.linearVelocity=Vector3.forward*(speed/3.6f);Input(.5f);yield return Wait(30);
                log.AppendLine($"YAW {variant} initial={speed}: speed={car.SpeedKmh:F3} yaw={car.YawRate:F3} slipAngle={car.VehicleSlipAngle:F3} steer={car.frontLeft.steerAngle:F3}/{car.frontRight.steerAngle:F3}");
            }
            foreach(float approach in new[]{.4f,.8f,1.2f})foreach(float recovery in new[]{.9f,-.9f})
            {
                yield return Reset(car);body.linearVelocity=Vector3.back*(35/3.6f);yield return Wait(30);
                Input(1);yield return Wait(Mathf.RoundToInt(approach*60));Input(1,handbrake:true);yield return Wait(18);
                float hbYaw=Quaternion.Angle(Quaternion.identity,body.rotation);log.AppendLine($"REVERSE HB {variant} approach={approach}: yaw={hbYaw:F2} signedSpeed={car.SignedLongitudinalSpeedKmh:F2} "+WheelData(car));
                car.currentGear=1;Input(recovery,.8f);float yaw=hbYaw,minTorque=float.PositiveInfinity,zeroTime=-1;bool backwardRpm=false,forwardRpm=false;
                for(int i=0;i<360;i++)
                {
                    if(i==90)Input(throttle:1);yield return null;yaw=Mathf.Max(yaw,Quaternion.Angle(Quaternion.identity,body.rotation));minTorque=Mathf.Min(minTorque,car.DeliveredAxleTorque);
                    backwardRpm|=car.SignedRearWheelRPM< -5;forwardRpm|=car.SignedRearWheelRPM>5;
                    if(zeroTime<0&&car.SignedLongitudinalSpeedKmh>=0)zeroTime=i/60f;
                }
                bool aligned=Vector3.Dot(body.linearVelocity.normalized,Vector3.back)>.866f;
                log.AppendLine($"REVERSE {variant} approach={approach} recovery={recovery}: maxYaw={yaw:F2} finalHeading={body.rotation.eulerAngles.y:F2} forward={car.SignedLongitudinalSpeedKmh:F2} zeroTime={zeroTime:F2} minMotorTorque={minTorque:F2} rpmCross={backwardRpm&&forwardRpm} travelAlignedWithin30={aligned}");
                Check(minTorque>=-.01f,variant+" first gear motor torque never reverses");
            }
            yield return Reset(car);yield return RollingFixture(car,mode==0?2:4);
            float peakYaw=0,peakSlip=0,maxPitch=0,maxRoll=0;int signChanges=0;float previousYaw=0;float maxAcceleration=0;
            var trace=new StringBuilder("time,speed,yawRate,slipAngle,pitch,roll\n");
            for(int i=0;i<360;i++)
            {
                float steering=i<60?.55f:i<120?-.55f:i<180?.35f:0;
                Input(steering,.65f);yield return null;
                peakYaw=Mathf.Max(peakYaw,Mathf.Abs(car.YawRate));peakSlip=Mathf.Max(peakSlip,Mathf.Abs(car.VehicleSlipAngle));
                if(i>5&&Mathf.Sign(previousYaw)!=Mathf.Sign(car.YawRate)&&Mathf.Abs(previousYaw)>1&&Mathf.Abs(car.YawRate)>1)signChanges++;
                previousYaw=car.YawRate;var suspension=car.GetComponent<SuspensionTelemetry>();
                maxPitch=Mathf.Max(maxPitch,Mathf.Abs(suspension.PhysicalBodyPitch));maxRoll=Mathf.Max(maxRoll,Mathf.Abs(suspension.PhysicalBodyRoll));maxAcceleration=Mathf.Max(maxAcceleration,car.LongitudinalAcceleration);
                if(i%6==0)trace.AppendLine(FormattableString.Invariant($"{i/60f:F2},{car.SpeedKmh:F3},{car.YawRate:F3},{car.VehicleSlipAngle:F3},{suspension.PhysicalBodyPitch:F3},{suspension.PhysicalBodyRoll:F3}"));
            }
            log.AppendLine($"TRANSITION {variant}: peakYaw={peakYaw:F2} peakSlip={peakSlip:F2} yawSignChanges={signChanges} finalYaw={car.YawRate:F2} finalSpeed={car.SpeedKmh:F2} maxPitch={maxPitch:F2} maxRoll={maxRoll:F2} maxAcceleration={maxAcceleration:F2}");
            File.WriteAllText("Library/RealScale/"+variant+"-transition.csv",trace.ToString());
            var effects=car.GetComponent<WheelEffects>();var audio=car.GetComponent<VehicleAudio>();
            Check(effects!=null&&effects.wheels.Length==4&&effects.wheels.All(w=>w.collider!=null),variant+" wheel effects references");
            Check(audio!=null&&audio.isActiveAndEnabled,variant+" engine/tire audio component enabled");
            Check(!float.IsNaN(body.position.x)&&body.position.y>-.5f,variant+" remains above test surface");
            Save();Input();
        }
    }
}

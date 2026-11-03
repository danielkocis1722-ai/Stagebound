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

// Explicit native Play Mode measurements; never included in a player build.
public static class ValidateMetrePhysics
{
    static readonly StringBuilder log=new StringBuilder();
    static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
    static IEnumerator steps;
    static Gamepad pad;
    static InputSettings previousSettings;
    static InputDevice[] disabled;
    static bool previousOptionsEnabled;
    static EnterPlayModeOptions previousOptions;
    static int frame=-1;
    static double deadline;
    static string selected; static bool hadSelection;
    static readonly string Output=RealScaleMigration.Output;
    static WheelCollider[] Wheels(CarController c)=>new[]{c.frontLeft,c.frontRight,c.backLeft,c.backRight};
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);log.AppendLine("PASS "+text);Save();}
    static void Save()=>File.WriteAllText(Output+"/physics.txt",log.ToString());
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        previousOptionsEnabled=EditorSettings.enterPlayModeOptionsEnabled;previousOptions=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        hadSelection=PlayerPrefs.HasKey(VehicleSelection.PlayerPrefsKey);selected=PlayerPrefs.GetString(VehicleSelection.PlayerPrefsKey);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        steps=Tests();deadline=EditorApplication.timeSinceStartup+1200;
        EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup>deadline){Finish(new Exception("Timeout"));return;}
        if(!EditorApplication.isPlaying||Time.frameCount==frame)return;frame=Time.frameCount;
        try
        {
            var current=stack.Count>0?stack.Peek():steps;
            while(true)
            {
                if(!current.MoveNext()){if(stack.Count==0){Finish(null);return;}stack.Pop();current=stack.Count>0?stack.Peek():steps;continue;}
                if(current.Current is IEnumerator child){stack.Push(child);current=child;continue;}break;
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
        if(hadSelection)PlayerPrefs.SetString(VehicleSelection.PlayerPrefsKey,selected);else PlayerPrefs.DeleteKey(VehicleSelection.PlayerPrefsKey);
        PlayerPrefs.Save();EditorSettings.enterPlayModeOptionsEnabled=previousOptionsEnabled;EditorSettings.enterPlayModeOptions=previousOptions;
        Time.captureDeltaTime=0;EditorApplication.Exit(e==null?0:1);
    }
    static void Input(float steer=0,float throttle=0,float brake=0,bool handbrake=false)
    {
        var state=new GamepadState{leftStick=new Vector2(steer,0),rightTrigger=throttle,leftTrigger=brake};
        InputSystem.QueueStateEvent(pad,handbrake?state.WithButton(GamepadButton.South):state);
    }
    static IEnumerator Wait(int n){for(int i=0;i<n;i++)yield return null;}
    static IEnumerator Reset(CarController c)
    {
        Input();c.enabled=false;var b=c.GetComponent<Rigidbody>();b.position=new Vector3(0,-999.25f,0);b.rotation=Quaternion.identity;b.linearVelocity=b.angularVelocity=Vector3.zero;
        foreach(var w in Wheels(c)){w.brakeTorque=w.motorTorque=w.steerAngle=0;}
        Physics.SyncTransforms();yield return Wait(120);c.currentGear=0;c.enabled=true;yield return Wait(120);
        Check(Wheels(c).All(w=>w.isGrounded),"Four grounded wheels on metre test pad");
    }
    static IEnumerator Tests()
    {
        yield return Wait(10);Time.captureDeltaTime=1f/60;
        previousSettings=InputSystem.settings;var settings=UnityEngine.Object.Instantiate(previousSettings);
        settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=settings;disabled=InputSystem.devices.Where(d=>d is Gamepad||d is Keyboard).ToArray();foreach(var d in disabled)InputSystem.DisableDevice(d);pad=InputSystem.AddDevice<Gamepad>();
        foreach(var definition in RealScaleMigration.Vehicles)
        {
            PlayerPrefs.SetString(VehicleSelection.PlayerPrefsKey,definition.vehicleId);SceneManager.LoadScene("SampleScene");yield return Wait(90);
            var c=UnityEngine.Object.FindAnyObjectByType<CarController>();var b=c.GetComponent<Rigidbody>();var config=definition.config;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Temporary metre physics pad";floor.transform.position=new Vector3(0,-1000.5f,0);floor.transform.localScale=new Vector3(6000,1,6000);
            Check(c.GetComponent<VehicleRigLoader>().CurrentDefinition==definition,"Authoritative selection "+definition.vehicleId);
            yield return Reset(c);
            var size=config.chassis.colliderSize;var box=b.mass/12*new Vector3(size.y*size.y+size.z*size.z,size.x*size.x+size.z*size.z,size.x*size.x+size.y*size.y);
            log.AppendLine($"INERTIA {definition.vehicleId} mass={b.mass} size={size:F6} automatic/chosen={b.inertiaTensor:F6} rotation={b.inertiaTensorRotation.eulerAngles:F6} uniformBox={box:F6}");
            foreach(var w in Wheels(c))
            {
                w.GetWorldPose(out var hub,out _);
                float extension=Vector3.Dot(w.transform.TransformPoint(w.center)-hub,w.transform.up);
                log.AppendLine($"SUSPENSION {definition.vehicleId}/{w.name} sprungMass={w.sprungMass:F3} ground={w.isGrounded} compression={(1-extension/w.suspensionDistance):F4}");
            }
            // Set an independent rolling fixture once; the stop uses normal service input.
            b.linearVelocity=Vector3.forward*(100/3.6f);yield return Wait(30);b.linearVelocity=Vector3.forward*(100/3.6f);
            var start=b.position;float startTime=Time.time;float distance=0,lockCount=0,total=0,slipSum=0;var last=b.position;
            var trace=new StringBuilder("seconds,speedKmh,FLrpm,FRrpm,RLrpm,RRrpm,FLslip,FRslip,RLslip,RRslip\n");
            Input(brake:1);
            for(int i=0;i<600&&b.linearVelocity.magnitude>.1f;i++)
            {
                yield return null;distance+=Vector3.Distance(last,b.position);last=b.position;
                var wheels=Wheels(c);float[] slip=new float[4];
                for(int w=0;w<4;w++)if(wheels[w].GetGroundHit(out var h))
                {
                    slip[w]=h.forwardSlip;slipSum+=Mathf.Abs(h.forwardSlip);total++;
                    if(c.SpeedKmh>10&&(Mathf.Abs(wheels[w].rpm)<10||Mathf.Abs(h.forwardSlip)>1))lockCount++;
                }
                trace.AppendLine(FormattableString.Invariant($"{Time.time-startTime:F3},{c.SpeedKmh:F3},{wheels[0].rpm:F2},{wheels[1].rpm:F2},{wheels[2].rpm:F2},{wheels[3].rpm:F2},{slip[0]:F4},{slip[1]:F4},{slip[2]:F4},{slip[3]:F4}"));
            }
            File.WriteAllText(Output+"/"+definition.vehicleId+"-braking.csv",trace.ToString());
            log.AppendLine($"BRAKING {definition.vehicleId} 100kmh stopTo0.1mps distance={distance:F3} time={Time.time-startTime:F3} lockFraction={lockCount/Mathf.Max(1,total):F4} meanAbsoluteSlip={slipSum/Mathf.Max(1,total):F4} baseTorque={c.brakeTorque} frontShare={c.frontBrakeBias}");
            Check(b.linearVelocity.magnitude<=.1f&&distance>25&&distance<65&&lockCount/Mathf.Max(1,total)<.2f,"Controlled 100 km/h service stop "+definition.vehicleId);
            yield return Reset(c);b.linearVelocity=Vector3.forward*(10/3.6f);c.currentGear=1;
            float integral=0,radius=0,speed=0;int count=0;
            for(int i=0;i<600;i++)
            {
                float error=10-c.SpeedKmh;integral=Mathf.Clamp(integral+error/60,0,8);Input(1,Mathf.Clamp(error*.08f+integral*.04f,0,.6f));yield return null;
                if(i>360&&Mathf.Abs(c.YawRate)>1){radius+=b.linearVelocity.magnitude/(Mathf.Abs(c.YawRate)*Mathf.Deg2Rad);speed+=c.SpeedKmh;count++;}
            }
            log.AppendLine($"TURN {definition.vehicleId} CGRadius={radius/Mathf.Max(1,count):F3} speed={speed/Mathf.Max(1,count):F3}");
            Check(count>0&&radius/count<6,"Metre-scale turning radius "+definition.vehicleId);
            foreach(float kmh in new[]{30f,60f,100f,125f})
            {
                yield return Reset(c);b.linearVelocity=Vector3.forward*(kmh/3.6f);yield return Wait(30);Input(.5f);yield return Wait(30);
                log.AppendLine($"STEER {definition.vehicleId} targetKmh={kmh} actualKmh={c.SpeedKmh:F3} yaw={c.YawRate:F3} slip={c.VehicleSlipAngle:F3} frontSteer={c.frontLeft.steerAngle:F3}/{c.frontRight.steerAngle:F3} grounded={Wheels(c).Count(w=>w.isGrounded)} roll={b.rotation.eulerAngles.z:F3} pitch={b.rotation.eulerAngles.x:F3} cameraFov={Camera.main.fieldOfView:F3}");
                Check(float.IsFinite(c.YawRate)&&Wheels(c).Count(w=>w.isGrounded)>=2,"Speed steering probe "+definition.vehicleId+" "+kmh);
            }
            foreach(bool governor in new[]{true,false})
            {
                yield return Reset(c);c.GetComponent<DriftYawStability>().enabled=governor;
                b.linearVelocity=Vector3.back*(35/3.6f);yield return Wait(30);Input(1);yield return Wait(72);Input(1,handbrake:true);yield return Wait(18);
                c.currentGear=1;Input(-1,1);yield return Wait(90);Input(throttle:1);yield return Wait(210);
                log.AppendLine($"JTURN {definition.vehicleId} governor={governor} finalHeading={b.rotation.eulerAngles.y:F3} signedForwardKmh={c.SignedLongitudinalSpeedKmh:F3} speed={c.SpeedKmh:F3}");
                Check(c.backLeft.motorTorque>=0&&c.backRight.motorTorque>=0,"Forward recovery torque has correct sign "+definition.vehicleId);
                yield return Reset(c);b.linearVelocity=Vector3.forward*(100/3.6f);yield return Wait(30);c.currentGear=3;
                c.EngineRPM=Mathf.Clamp(Mathf.Abs(c.SignedRearWheelRPM)*c.gearRatios[2]*c.finalDrive,c.idleRPM,c.redlineRPM);Input(throttle:.15f);yield return Wait(45);
                float peakYaw=0,peakSlip=0,driftSeconds=0,limiterSeconds=0;
                var transitions=new StringBuilder("seconds,speedKmh,yawRate,slipAngle,drifting,engineRPM\n");float began=Time.time;
                for(int i=0;i<360;i++)
                {
                    Input(i<60?.55f:i<120?-.55f:i<180?.35f:0,.65f);yield return null;
                    peakYaw=Mathf.Max(peakYaw,Mathf.Abs(c.YawRate));peakSlip=Mathf.Max(peakSlip,Mathf.Abs(c.VehicleSlipAngle));
                    if(c.GetComponent<DriftTelemetry>().IsDrifting)driftSeconds+=Time.deltaTime;if(c.EngineRPM>7100)limiterSeconds+=Time.deltaTime;
                    transitions.AppendLine(FormattableString.Invariant($"{Time.time-began:F3},{c.SpeedKmh:F3},{c.YawRate:F3},{c.VehicleSlipAngle:F3},{c.GetComponent<DriftTelemetry>().IsDrifting},{c.EngineRPM:F2}"));
                }
                File.WriteAllText(Output+"/"+definition.vehicleId+"-transition-governor-"+governor+".csv",transitions.ToString());
                log.AppendLine($"TRANSITION {definition.vehicleId} governor={governor} peakYaw={peakYaw:F3} peakSlip={peakSlip:F3} driftSeconds={driftSeconds:F3} above7100Seconds={limiterSeconds:F3} finalSpeed={c.SpeedKmh:F3}");Save();
            }
            c.GetComponent<DriftYawStability>().enabled=config.yawStabilityEnabled;
            yield return Reset(c);c.currentGear=1;
            for(int gear=1;gear<=5;gear++)
            {
                float began=Time.time;float throttle=.22f+(gear-1)*.045f;
                Input(throttle:throttle);
                while(Time.time-began<35&&c.EngineRPM<7200)yield return null;
                log.AppendLine($"GEAR_SWEEP {definition.vehicleId} gear={gear} seconds={Time.time-began:F3} speed={c.SpeedKmh:F3} rpm={c.EngineRPM:F3} throttle={throttle:F3}");
                if(gear==5)break;
                var state=new GamepadState{rightTrigger=throttle}.WithButton(GamepadButton.RightShoulder);InputSystem.QueueStateEvent(pad,state);yield return Wait(2);Input(throttle:throttle);yield return Wait(30);
                Check(c.currentGear==gear+1,"Normal upshift "+definition.vehicleId+" to "+(gear+1));
                log.AppendLine($"SHIFT_LANDING {definition.vehicleId} gear={c.currentGear} rpm={c.EngineRPM:F3}");
            }
            foreach(int gear in new[]{2,3})foreach(float kmh in gear==2?new[]{50f,70f,90f}:new[]{80f,100f,120f})
            {
                yield return Reset(c);b.linearVelocity=Quaternion.Euler(0,20,0)*Vector3.forward*(kmh/3.6f);yield return Wait(12);c.currentGear=gear;
                c.EngineRPM=Mathf.Clamp(Mathf.Abs(c.SignedRearWheelRPM)*c.gearRatios[gear-1]*c.finalDrive,c.idleRPM,c.redlineRPM);
                float drift=0,limit=0,minRPM=float.PositiveInfinity,maxRPM=0;
                for(int i=0;i<240;i++)
                {
                    Input(-.15f,.35f);yield return null;
                    if(c.GetComponent<DriftTelemetry>().IsDrifting)drift+=Time.deltaTime;if(c.EngineRPM>7100)limit+=Time.deltaTime;
                    minRPM=Mathf.Min(minRPM,c.EngineRPM);maxRPM=Mathf.Max(maxRPM,c.EngineRPM);
                }
                log.AppendLine($"DRIFT_GEAR {definition.vehicleId} gear={gear} targetKmh={kmh} rpm={minRPM:F2}-{maxRPM:F2} driftSeconds={drift:F3} above7100Seconds={limit:F3} finalSpeed={c.SpeedKmh:F3}");Save();
            }
        }
    }
}

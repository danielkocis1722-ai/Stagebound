using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Explicit experimental worktree authoring; never called automatically on import.
public static class RealScaleExperiment
{
    public const string Root = "Assets/Experiments/RealScale";
    public const string ScenePath = Root + "/AE86_RealScale_Test.unity";
    public const string ConfigPath = Root + "/AE86_RealScale_VehicleConfig.asset";
    public static VehicleDefinition Original => AssetDatabase.LoadAssetAtPath<VehicleDefinition>("Assets/Vehicles/Definitions/AE86_VehicleDefinition.asset");
    static Bounds Geometry(Transform rig, Transform part)
    {
        var min = Vector3.one * float.PositiveInfinity;
        var max = Vector3.one * float.NegativeInfinity;
        foreach (var f in part.GetComponentsInChildren<MeshFilter>())
            foreach (var v in f.sharedMesh.vertices)
            {
                var p = rig.InverseTransformPoint(f.transform.TransformPoint(v));
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
        return new Bounds((min + max) * .5f, max - min);
    }
    public static void Inspect()
    {
        try
        {
            Directory.CreateDirectory("Library/RealScale");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var car = UnityEngine.Object.FindAnyObjectByType<CarController>();
            var visual = UnityEngine.Object.Instantiate(Original.visualPrefab, car.transform, false);
            var config = Original.config;
            visual.transform.SetLocalPositionAndRotation(config.visualPositionOffset, Quaternion.Euler(config.visualRotationOffset));
            visual.transform.localScale = config.visualScale;
            var log = new StringBuilder();
            log.AppendLine($"MODEL all {Geometry(car.transform, visual.transform)}");
            foreach (var w in new[] {visual.frontLeftWheel, visual.frontRightWheel, visual.backLeftWheel, visual.backRightWheel})
                log.AppendLine($"WHEEL {w.name} hub={car.transform.InverseTransformPoint(w.position):F6} bounds={Geometry(car.transform, w)}");
            foreach (var f in visual.GetComponentsInChildren<MeshFilter>())
                log.AppendLine($"PART {f.name} bounds={Geometry(car.transform,f.transform)}");
            File.WriteAllText("Library/RealScale/inspection.txt", log.ToString());
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    public static void Audit()
    {
        try
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var camera=UnityEngine.Object.FindAnyObjectByType<FollowCamera>();
            camera.slowDistance=3.55f;camera.fastDistance=4.2f;
            camera.slowHeight=1.3f;camera.fastHeight=1.6f;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            var report=new StringBuilder();
            foreach(string path in Directory.GetFiles("Assets/Scenes","*.unity").Concat(Directory.GetFiles(Root,"*.unity")))
            {
                scene=EditorSceneManager.OpenScene(path);
                var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).ToArray();
                if(components.Any(c=>c==null))throw new Exception("Missing script "+path);
                if(components.OfType<MeshFilter>().Any(f=>f.sharedMesh==null)||components.OfType<MeshCollider>().Any(c=>c.sharedMesh==null))throw new Exception("Missing mesh "+path);
                if(components.OfType<Renderer>().Any(r=>r.sharedMaterials.Any(m=>m==null||m.shader==null)))throw new Exception("Missing material "+path);
                report.AppendLine("PASS references "+path);
                if(path.StartsWith(Root))
                    report.AppendLine("STATIC COLLIDERS "+string.Join(",",components.OfType<Collider>().Where(c=>c.attachedRigidbody==null).Select(c=>c.name)));
            }
            File.WriteAllText("Library/RealScale/audit.txt",report.ToString());
            EditorSceneManager.OpenScene(ScenePath);EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void Build()
    {
        try
        {
            Folder(Root);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var car=UnityEngine.Object.FindAnyObjectByType<CarController>();
            var visual=UnityEngine.Object.Instantiate(Original.visualPrefab,car.transform,false);
            var original=Original.config;
            visual.transform.SetLocalPositionAndRotation(original.visualPositionOffset,Quaternion.Euler(original.visualRotationOffset));visual.transform.localScale=original.visualScale;
            Bounds oldBounds=Geometry(car.transform,visual.transform);
            float scale=4.2f/oldBounds.size.z;
            var config=UnityEngine.Object.Instantiate(original);config.name="AE86_RealScale_VehicleConfig";
            config.visualScale=original.visualScale*scale;config.visualPositionOffset=original.visualPositionOffset*scale;
            var pivots=new[]{visual.frontLeftWheel,visual.frontRightWheel,visual.backLeftWheel,visual.backRightWheel};
            var wheels=new[]{config.frontLeft,config.frontRight,config.backLeft,config.backRight};
            var hubs=new Vector3[4];
            for(int i=0;i<4;i++)
            {
                var bounds=Geometry(car.transform,pivots[i]);
                hubs[i]=bounds.center*scale;
                // Correct the experimental visual pivots to the measured tire
                // centres; preserve every child mesh's world pose and scale.
                var oldPivot=pivots[i];
                var pivot=new GameObject(oldPivot.name+" Real Hub").transform;
                pivot.SetParent(oldPivot.parent,false);
                pivot.SetPositionAndRotation(car.transform.TransformPoint(bounds.center),car.transform.rotation);
                oldPivot.SetParent(pivot,true);
                foreach(var axleIndex in Enumerable.Range(0,visual.unsprungAxles.Length))
                {
                    var axle=visual.unsprungAxles[axleIndex];
                    if(axle.leftWheelMesh==oldPivot)axle.leftWheelMesh=pivot;
                    if(axle.rightWheelMesh==oldPivot)axle.rightWheelMesh=pivot;
                    visual.unsprungAxles[axleIndex]=axle;
                }
                if(i==0)visual.frontLeftWheel=pivot;else if(i==1)visual.frontRightWheel=pivot;else if(i==2)visual.backLeftWheel=pivot;else visual.backRightWheel=pivot;
                wheels[i].radius=Mathf.Max(bounds.size.y,bounds.size.z)*.5f*scale;
                wheels[i].localPosition=hubs[i]+Vector3.up*.055f;
                wheels[i].localScale=Vector3.one;wheels[i].center=Vector3.zero;
                wheels[i].suspensionDistance=.18f;wheels[i].spring=i<2?40000:36000;wheels[i].damper=i<2?3500:3200;wheels[i].targetPosition=.3f;
                wheels[i].forceAppPointDistance=.22f;wheels[i].mass=20;wheels[i].damping=.5f;
            }
            var visualPrefab=PrefabUtility.SaveAsPrefabAsset(visual.gameObject,Root+"/AE86_RealScale_Visual.prefab").GetComponent<VehicleVisualSetup>();
            Vector3 front=(hubs[0]+hubs[1])*.5f,rear=(hubs[2]+hubs[3])*.5f;
            float avgRadius=wheels.Average(w=>w.radius),avgHubY=hubs.Average(h=>h.y);
            config.chassis.mass=1100;
            config.chassis.centerOfMass=new Vector3((front.x+rear.x)*.5f,.5f-avgRadius+avgHubY,Mathf.Lerp(rear.z,front.z,.53f));
            config.chassis.colliderPosition=Vector3.zero;config.chassis.colliderScale=Vector3.one;config.chassis.colliderRotation=Quaternion.identity;
            config.chassis.colliderSize=new Vector3(1.65f,.7f,3.7f);
            config.chassis.colliderCenter=new Vector3(config.chassis.centerOfMass.x,config.chassis.centerOfMass.y+.04f,(front.z+rear.z)*.5f);
            config.handbrake.brakeTorque=650;config.handbrake.frontBrakeBias=.67f;config.handbrake.handbrakeTorque=2400;
            config.yawStabilityEnabled=false;
            if(!config.IsValid(out var reason))throw new Exception(reason);
            AssetDatabase.CreateAsset(config,ConfigPath);
            var definition=UnityEngine.Object.Instantiate(Original);definition.name="AE86_RealScale_VehicleDefinition";definition.config=config;definition.visualPrefab=visualPrefab;definition.vehicleId="ae86-real-scale";definition.displayName="Toyota AE86 — Real Scale Experiment";
            AssetDatabase.CreateAsset(definition,Root+"/AE86_RealScale_VehicleDefinition.asset");
            var realCatalog=ScriptableObject.CreateInstance<VehicleCatalog>();realCatalog.defaultVehicle=definition;realCatalog.vehicles=new[]{definition};AssetDatabase.CreateAsset(realCatalog,Root+"/AE86_RealScale_Catalog.asset");
            var baselineCatalog=ScriptableObject.CreateInstance<VehicleCatalog>();baselineCatalog.defaultVehicle=Original;baselineCatalog.vehicles=new[]{Original};AssetDatabase.CreateAsset(baselineCatalog,Root+"/AE86_Presentation_Catalog.asset");
            UnityEngine.Object.DestroyImmediate(visual.gameObject);
            var report=new StringBuilder();
            report.AppendLine($"SCALE {scale:F8} old visual envelope={oldBounds.size:F6} new envelope={oldBounds.size*scale:F6}");
            report.AppendLine($"GEOMETRY front={front:F6} rear={rear:F6} wheelbase={front.z-rear.z:F6} track={Mathf.Abs(hubs[1].x-hubs[0].x):F6} COM={config.chassis.centerOfMass:F6}");
            for(int i=0;i<4;i++)report.AppendLine($"WHEEL {i}: hub={hubs[i]:F6} mount={wheels[i].localPosition:F6} radius={wheels[i].radius:F6}");
            BuildScene(false,baselineCatalog,report);
            BuildScene(true,realCatalog,report);
            var paths=new[]{ScenePath,Root+"/AE86_Presentation_Comparison.unity"};
            EditorBuildSettings.scenes=paths.Select(p=>new EditorBuildSettingsScene(p,true)).Concat(EditorBuildSettings.scenes.Where(s=>!paths.Contains(s.path))).ToArray();
            AssetDatabase.SaveAssets();
            File.WriteAllText("Library/RealScale/setup.txt",report.ToString());
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText("Library/RealScale/setup-failure.txt",e.ToString());EditorApplication.Exit(1);}
    }
    static void BuildScene(bool real,VehicleCatalog catalog,StringBuilder report)
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        string name=real?"AE86_RealScale_Test":"AE86_Presentation_Comparison";
        EditorSceneManager.SaveScene(scene,Root+"/"+name+".unity",true);scene=EditorSceneManager.OpenScene(Root+"/"+name+".unity");
        foreach(var root in scene.GetRootGameObjects())
            if(root.GetComponentInChildren<CarController>()==null&&root.GetComponentInChildren<Camera>()==null&&root.GetComponentInChildren<Light>()==null&&root.GetComponentInChildren<RaceTimer>()==null&&root.GetComponentInChildren<UnityEngine.Rendering.Volume>()==null)
                UnityEngine.Object.DestroyImmediate(root);
        foreach(var trigger in UnityEngine.Object.FindObjectsByType<RaceTrigger>())UnityEngine.Object.DestroyImmediate(trigger.gameObject);
        var car=UnityEngine.Object.FindAnyObjectByType<CarController>();var loader=car.GetComponent<VehicleRigLoader>();loader.catalog=catalog;
        var config=catalog.defaultVehicle.config;config.ApplyTo(car,loader.chassisCollider);
        var lab=car.gameObject.AddComponent<RealScaleLab>();lab.variantLabel=real?"AE86 REAL SCALE — experimental":"AE86 PRESENTATION GEOMETRY — comparison only";lab.otherScene=real?"AE86_Presentation_Comparison":"AE86_RealScale_Test";
        car.transform.SetPositionAndRotation(new Vector3(0,1.25f,0),Quaternion.identity);
        var spawn=new GameObject("SpawnPoint").transform;spawn.position=car.transform.position;
        car.GetComponent<RespawnManager>().startPoint=spawn;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Metre-scale flat test pad — 600m x 600m";floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(600,1,600);floor.isStatic=true;
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.16f,.18f,.20f);material.SetFloat("_Smoothness",.1f);
        string matPath=Root+"/TestPad.mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(existing==null){AssetDatabase.CreateAsset(material,matPath);existing=material;}else UnityEngine.Object.DestroyImmediate(material);floor.GetComponent<Renderer>().sharedMaterial=existing;
        var paint=AssetDatabase.LoadAssetAtPath<Material>(Root+"/GridPaint.mat");if(paint==null){paint=new Material(Shader.Find("Universal Render Pipeline/Unlit"));paint.color=new Color(.55f,.58f,.60f);AssetDatabase.CreateAsset(paint,Root+"/GridPaint.mat");}
        var grid=new GameObject("Ten metre reference grid — no collisions").transform;
        for(int i=-20;i<=20;i++)foreach(bool alongX in new[]{true,false})
        {
            var line=GameObject.CreatePrimitive(PrimitiveType.Cube);line.name="10m grid "+i;line.transform.SetParent(grid);line.transform.position=alongX?new Vector3(0,.003f,i*10):new Vector3(i*10,.003f,0);line.transform.localScale=alongX?new Vector3(400,.005f,.025f):new Vector3(.025f,.005f,400);UnityEngine.Object.DestroyImmediate(line.GetComponent<Collider>());line.GetComponent<Renderer>().sharedMaterial=paint;
        }
        foreach(float radius in new[]{5f,10f})
        {
            var ring=new GameObject(radius+" metre radius reference").AddComponent<LineRenderer>();ring.sharedMaterial=paint;ring.widthMultiplier=.035f;ring.loop=true;ring.positionCount=128;
            for(int i=0;i<128;i++){float a=i*Mathf.PI*2/128;ring.SetPosition(i,new Vector3(30+Mathf.Cos(a)*radius,.012f,Mathf.Sin(a)*radius));}
        }
        var timer=UnityEngine.Object.FindAnyObjectByType<RaceTimer>();
        var stage=ScriptableObject.CreateInstance<StageDefinition>();stage.stageId=real?"ae86-real-scale-pad":"ae86-presentation-pad";stage.stageName=real?"REAL SCALE TEST PAD":"PRESENTATION COMPARISON PAD";stage.scenePath=Root+"/"+name+".unity";stage.checkpointCount=0;stage.importLegacySceneBest=false;
        AssetDatabase.CreateAsset(stage,Root+"/"+name+"_Stage.asset");timer.stageDefinition=stage;
        var gate=new GameObject("Test timing line");gate.transform.position=new Vector3(0,1,20);var box=gate.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(40,3,1);var triggerComponent=gate.AddComponent<RaceTrigger>();triggerComponent.raceTimer=timer;
        var camera=UnityEngine.Object.FindAnyObjectByType<FollowCamera>();
        if(real)
        {
            camera.slowDistance=3.55f;camera.fastDistance=4.2f;camera.slowHeight=1.3f;camera.fastHeight=1.6f;camera.lookHeight=.4f;camera.lookAheadSlow=1.1f;camera.lookAheadFast=2.8f;
            camera.lowSpeedDistanceReduction=.25f;camera.accelerationPullback=.25f;camera.accelerationLowering=.025f;camera.torqueKickPullback=.03f;camera.brakingPush=.12f;camera.driftSideOffset=.18f;
            var effects=car.GetComponent<WheelEffects>();foreach(var w in effects.wheels)w.markWidth=.20f;
        }
        // Save the visible rest model at the correct scale as well as runtime config.
        var visual=loader.existingVisual;visual.transform.SetLocalPositionAndRotation(config.visualPositionOffset,Quaternion.Euler(config.visualRotationOffset));visual.transform.localScale=config.visualScale;
        var body=car.GetComponent<Rigidbody>();body.ResetInertiaTensor();
            var size=Vector3.Scale(loader.chassisCollider.size,loader.chassisCollider.transform.lossyScale);var estimate=body.mass/12f*new Vector3(size.y*size.y+size.z*size.z,size.x*size.x+size.z*size.z,size.x*size.x+size.y*size.y);
        report.AppendLine($"INERTIA {name}: automatic={body.inertiaTensor:F6} rotation={body.inertiaTensorRotation.eulerAngles:F6} uniformBox={estimate:F6} colliderSize={size:F4}");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

}

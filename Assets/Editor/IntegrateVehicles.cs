using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Explicit, repeatable editor preparation. Does not run automatically on import.
public static class IntegrateVehicles
{
    const string Root="Assets/Vehicles";
    static readonly StringBuilder Report=new StringBuilder();
    [Serializable] class SourceMaterial { public string name="",texture=""; public Color color=Color.gray; public float metallic=0,smoothness=.5f,transmission=0; }
    [Serializable] class SourceMaterials { public SourceMaterial[] materials=Array.Empty<SourceMaterial>(); }
    static SourceMaterials rxMaterials;
    static void Require(bool ok,string reason) { if(!ok)throw new InvalidOperationException(reason); }
    static T Load<T>(string path) where T:UnityEngine.Object {
        var asset=AssetDatabase.LoadAssetAtPath<T>(path);Require(asset!=null,"Missing "+path);return asset;
    }
    static void Folder(string path) {
        if(AssetDatabase.IsValidFolder(path))return;
        Folder(Path.GetDirectoryName(path).Replace('\\','/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
    }
    static Bounds BoundsOf(IEnumerable<Renderer> renderers) {
        var rs=renderers.ToArray();Require(rs.Length>0,"No renderers.");
        var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);return bounds;
    }
    static Transform Exact(Transform model,string path) {
        var t=model.Find(path);Require(t!=null,"Missing explicit hierarchy path "+path);return t;
    }
    static void SaveAsset<T>(T asset,string path) where T:UnityEngine.Object {
        var existing=AssetDatabase.LoadAssetAtPath<T>(path);
        if(existing==null)AssetDatabase.CreateAsset(asset,path);
        else {EditorUtility.CopySerialized(asset,existing);UnityEngine.Object.DestroyImmediate(asset);EditorUtility.SetDirty(existing);}
    }
    static Material Remap(Material source,string folder,bool rx7) {
        Require(source!=null,"Unassigned imported material.");
        string safe=string.Join("_",source.name.Split(Path.GetInvalidFileNameChars()));
        string path=folder+"/"+safe+".mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
        var shader=Shader.Find("Universal Render Pipeline/Lit");Require(shader!=null,"URP Lit missing.");
        var mat=new Material(shader){name=source.name};
        string n=source.name.ToLowerInvariant();
        Color color=source.HasProperty("_Color")?source.GetColor("_Color"):Color.gray;
        Texture texture=source.mainTexture;
        if(n=="paint"||n.StartsWith("paint."))color=new Color(.11f,.24f,.32f);
        if(n.Contains("tyre")||n.Contains("tire"))color=new Color(.035f,.035f,.04f);
        if(n.Contains("black")||n.Contains("dummy"))color=new Color(.025f,.03f,.035f);
        if(n.Contains("rims")||n.Contains("chrome")||n.Contains("brake")||n.Contains("exhaust")) {
            mat.SetFloat("_Metallic",.7f);color=new Color(.42f,.45f,.48f);
        }
        if(n.Contains("rear light")||n.Contains("rear lights"))color=new Color(.55f,.012f,.018f);
        if(n.Contains("headlight"))color=n.Contains("orange")?new Color(1,.28f,.015f):new Color(.7f,.78f,.83f);
        bool transparent=n=="widnows"||n.Contains("glass");
        if(rx7) {
            if(rxMaterials==null)rxMaterials=JsonUtility.FromJson<SourceMaterials>(File.ReadAllText("Docs/RX7SourceMaterials.json"));
            var data=rxMaterials.materials.FirstOrDefault(m=>m.name==source.name);
            Require(data!=null,"No inspected Maya material metadata for "+source.name);
            color=data.color;mat.SetFloat("_Metallic",data.metallic);mat.SetFloat("_Smoothness",data.smoothness);
            transparent=data.transmission>.5f||n=="mirror";
            if(!string.IsNullOrEmpty(data.texture))texture=Load<Texture2D>("Assets/Models/Cars/texture/"+data.texture);
            if(n.Contains("tire")){mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.2f);}
        }
        mat.SetColor("_BaseColor",texture!=null?Color.white:color);
        if(texture!=null)mat.SetTexture("_BaseMap",texture);
        if(!rx7)mat.SetFloat("_Smoothness",n.Contains("tyre")||n.Contains("tire")||n.Contains("dummy")?.18f:.55f);
        if(transparent) {
            mat.SetColor("_BaseColor",new Color(.12f,.2f,.24f,.38f));
            mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=(int)RenderQueue.Transparent;
            mat.SetFloat("_Cull",(float)CullMode.Off);
            mat.SetOverrideTag("RenderType","Transparent");
        }
        mat.SetColor("_EmissionColor",Color.black);mat.DisableKeyword("_EMISSION");
        AssetDatabase.CreateAsset(mat,path);
        Report.AppendLine("Material "+source.name+" -> "+path+" texture="+(texture==null?"none":AssetDatabase.GetAssetPath(texture)));
        return mat;
    }
    public static void Run()
    {
        try { Prepare();Directory.CreateDirectory("Library/VehicleIntegration");File.WriteAllText("Library/VehicleIntegration/setup.txt",Report.ToString());EditorApplication.Exit(0); }
        catch(Exception e) {Debug.LogException(e);Directory.CreateDirectory("Library/VehicleIntegration");File.WriteAllText("Library/VehicleIntegration/setup.txt",Report+"\nFAIL: "+e);EditorApplication.Exit(1);}
    }
    static void Prepare()
    {
        Folder(Root+"/Materials/SkylineR32");Folder(Root+"/Materials/RX7");
        var catalog=Load<VehicleCatalog>(Root+"/VehicleCatalog.asset");
        var baseline=Load<VehicleDefinition>(Root+"/Definitions/AE86_VehicleDefinition.asset");
        Require(baseline.IsPlayable,"AE86 baseline must be complete before integration.");
        // This world already uses a car larger than real-world metres. Keep its
        // established world convention, measuring each imported model separately.
        float worldUnitsPerMetre=(baseline.config.frontLeft.localPosition.z-baseline.config.backLeft.localPosition.z)/2.4f;
        Report.AppendLine("World units per nominal metre: "+worldUnitsPerMetre);
        var skyline=Build(baseline,"skyline-r32","Nissan Skyline R32","Nissan","Skyline_R32.fbx",4.545f*worldUnitsPerMetre,false);
        var rx7=Build(baseline,"rx7-fd","Mazda RX-7 FD","Mazda","RX7_FBX.fbx",4.285f*worldUnitsPerMetre,true);
        baseline.power=.5f;baseline.handling=.8f;baseline.drift=.7f;baseline.stability=.5f;EditorUtility.SetDirty(baseline);
        catalog.vehicles=new[]{baseline,skyline,rx7};EditorUtility.SetDirty(catalog);
        Require(catalog.IsValid(out string reason),reason);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var car=UnityEngine.Object.FindAnyObjectByType<CarController>();
        Require(car!=null&&UnityEngine.Object.FindObjectsByType<CarController>().Length==1,"Expected exactly one shared car.");
        var loader=car.GetComponent<VehicleRigLoader>();Require(loader!=null&&loader.catalog==catalog,"Race catalog binding.");
        Require(loader.existingVisual!=null&&loader.existingVisual.IsComplete(out _),"AE86 scene visual.");
        var respawn=car.GetComponent<RespawnManager>();Require(respawn!=null&&respawn.startPoint!=null,"Authoritative spawn missing.");
        var timer=UnityEngine.Object.FindAnyObjectByType<RaceTimer>();Require(timer!=null&&timer.stageDefinition!=null,"Race stage missing.");
        int checkpointCount=UnityEngine.Object.FindObjectsByType<RaceTrigger>().Where(t=>t.triggerType!=RaceTrigger.TriggerType.StartFinish).Select(t=>t.checkpointIndex).Distinct().Count();
        Require(timer.stageDefinition.checkpointCount==checkpointCount,"Stage checkpoint metadata differs from scene.");
        Report.AppendLine("South Loop: "+checkpointCount+" checkpoint(s); spawn="+respawn.startPoint.name+" at "+respawn.startPoint.position);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        var menu=UnityEngine.Object.FindAnyObjectByType<MainMenu>();Require(menu!=null&&menu.vehicleCatalog==catalog&&menu.stageCatalog!=null,"Main Menu catalogs missing.");
        Require(menu.stageCatalog.Find("south-loop")!=null&&menu.stageCatalog.Find("south-loop").IsPlayable,"South Loop must remain available.");
        Report.AppendLine("PASS: menu catalogs, single rig, baseline, spawn, checkpoint metadata and stage scene bindings.");
    }
    static VehicleDefinition Build(VehicleDefinition baseline,string id,string display,string maker,string filename,float targetLength,bool rx7)
    {
        string stem=rx7?"RX7":"SkylineR32";
        var root=new GameObject(stem+"_Visual");
        try {
            var imported=UnityEngine.Object.Instantiate(Load<GameObject>("Assets/Models/Cars/"+filename),root.transform);
            imported.name=Path.GetFileNameWithoutExtension(filename);
            if(PrefabUtility.IsPartOfPrefabInstance(imported))PrefabUtility.UnpackPrefabInstance(imported,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            Require(imported.GetComponentInChildren<Collider>(true)==null&&imported.GetComponentInChildren<Rigidbody>(true)==null,"Imported model must be visual-only.");
            // Inspection: both FBXs face +Z. Skyline's Blender collections are
            // flattened; RX7 has explicit left/right and front/rear groups.
            string[] paths=rx7?new[]{"group9/group3","group10/group3","group9/group8","group10/group8"}:
                new[]{"Circle.014","Circle.020","Circle.017","Circle.023"};
            var wheelSources=paths.Select(p=>Exact(imported.transform,p)).ToArray();
            if(rx7) {
                // The source has an authored 15-degree front steer pose.
                // Undo it about each actual hub; retain the original camber.
                foreach(var wheel in wheelSources.Take(2))wheel.rotation=Quaternion.AngleAxis(15,Vector3.up)*wheel.rotation;
            }
            var rawBounds=BoundsOf(root.GetComponentsInChildren<Renderer>(true));
            float scale=targetLength/rawBounds.size.z;
            var centers=new Vector3[4];var radii=new float[4];
            var pivots=new Transform[4];string[] names={"FL_Wheel","FR_Wheel","RL_Wheel","RR_Wheel"};
            for(int i=0;i<4;i++) {
                var rs=wheelSources[i].GetComponentsInChildren<Renderer>(true);
                var bounds=BoundsOf(rs);
                centers[i]=rx7?wheelSources[i].position:bounds.center;
                radii[i]=Mathf.Max(bounds.size.y,bounds.size.z)*.5f*scale;
                pivots[i]=new GameObject(names[i]).transform;pivots[i].SetParent(root.transform,false);
                pivots[i].position=centers[i];wheelSources[i].SetParent(pivots[i],true);
                Report.AppendLine(display+" "+names[i]+" <- "+paths[i]+" renderers="+rs.Length+" raw hub="+centers[i].ToString("F6"));
            }
            var midpoint=(centers[0]+centers[1]+centers[2]+centers[3])*.25f;
            float referenceBottom=(baseline.config.frontLeft.localPosition.y-baseline.config.frontLeft.radius+
                baseline.config.backLeft.localPosition.y-baseline.config.backLeft.radius)*.5f;
            float averageRadius=radii.Average();
            float referenceAxleCenter=(baseline.config.frontLeft.localPosition.z+baseline.config.backLeft.localPosition.z)*.5f;
            var offset=new Vector3(-midpoint.x*scale,referenceBottom+averageRadius-midpoint.y*scale,referenceAxleCenter-midpoint.z*scale);
            var config=UnityEngine.Object.Instantiate(baseline.config);config.name=stem+"_VehicleConfig";
            config.visualScale=Vector3.one*scale;config.visualRotationOffset=Vector3.zero;config.visualPositionOffset=offset;
            var wc=new[]{config.frontLeft,config.frontRight,config.backLeft,config.backRight};
            float mass=rx7?1250:1450;
            float massRatio=mass/baseline.config.chassis.mass;
            for(int i=0;i<4;i++) {
                wc[i].localPosition=centers[i]*scale+offset;wc[i].localRotation=Quaternion.identity;wc[i].localScale=Vector3.one;wc[i].center=Vector3.zero;
                wc[i].radius=radii[i];wc[i].mass=rx7?22:25;wc[i].spring*=massRatio;wc[i].damper*=massRatio;
                Report.AppendLine("  "+names[i]+" collider="+wc[i].localPosition.ToString("F5")+" radius="+wc[i].radius.ToString("F5"));
            }
            // A compact box encloses the sprung body without colliding with the
            // road before the tires. Keep the existing single chassis collider.
            var bodyBounds=BoundsOf(imported.GetComponentsInChildren<Renderer>(true));
            config.chassis.colliderPosition=Vector3.zero;config.chassis.colliderRotation=Quaternion.identity;config.chassis.colliderScale=Vector3.one;
            config.chassis.colliderCenter=bodyBounds.center*scale+offset;
            config.chassis.colliderSize=Vector3.Scale(bodyBounds.size*scale,new Vector3(.86f,.5f,.88f));
            config.chassis.mass=mass;config.chassis.angularDamping=rx7?.24f:.28f;
            config.chassis.centerOfMass=new Vector3(0,-.3f,rx7?0:.12f);
            config.chassis.frontAntiRoll=rx7?2500:3200;config.chassis.rearAntiRoll=rx7?1900:2400;
            config.engine.maxEngineTorque=rx7?500:460;
            config.steering.steeringSpeed=rx7?105:82;config.steering.steeringReturnSpeed=rx7?155:130;
            config.transmission.finalDrive=rx7?4.3f:4.1f;
            config.differential.lsdStrength=rx7?.7f:.75f;
            config.clutch.engineInertia=rx7?.34f:.44f;
            config.rearGrip.powerSlipGripReduction=rx7?.1f:.07f;
            config.rearGrip.controlledDriftGripRecoveryResponse=rx7?.55f:.5f;
            Require(config.IsValid(out var reason),reason);
            SaveAsset(config,Root+"/Configs/"+config.name+".asset");
            config=Load<VehicleConfig>(Root+"/Configs/"+stem+"_VehicleConfig.asset");
            string materialFolder=Root+"/Materials/"+stem;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>Remap(m,materialFolder,rx7)).ToArray();
            }
            if(!rx7) {
                // Render inspection found these outer covers hiding the red
                // Plane.020 lamps. Give just these slots transparent lenses.
                var covers=Exact(imported.transform,"Plane.016").GetComponent<Renderer>();
                var slots=covers.sharedMaterials;
                for(int i=0;i<slots.Length;i++) {
                    string path=materialFolder+"/RearLens_"+i+".mat";
                    var lens=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(lens==null) {
                        lens=new Material(slots[i]){name="RearLens_"+i};
                        lens.SetColor("_BaseColor",new Color(.65f,.08f,.08f,.12f));lens.SetFloat("_Smoothness",.8f);
                        lens.SetFloat("_Surface",1);lens.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);lens.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                        lens.SetFloat("_ZWrite",0);lens.SetFloat("_Cull",(float)CullMode.Off);lens.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        lens.renderQueue=(int)RenderQueue.Transparent;lens.SetOverrideTag("RenderType","Transparent");
                        AssetDatabase.CreateAsset(lens,path);
                    }
                    slots[i]=lens;
                }
                covers.sharedMaterials=slots;Report.AppendLine("Plane.016 outer lens slots -> RearLens_0 / RearLens_1 (transparent, no emission).");
            }
            var visual=root.AddComponent<VehicleVisualSetup>();visual.bodyRoot=root.transform;
            visual.frontLeftWheel=pivots[0];visual.frontRightWheel=pivots[1];visual.backLeftWheel=pivots[2];visual.backRightWheel=pivots[3];
            string lightPath=rx7?"group11/polySurface534":"Plane.020";
            string lightMaterial=rx7?"aiStandardSurface12":"rear lights";
            var rearLight=Exact(imported.transform,lightPath).GetComponent<Renderer>();
            int lightIndex=Array.FindIndex(rearLight.sharedMaterials,m=>m!=null&&m.name==lightMaterial);
            // Only these inspected, rear-light-only renderers/slots may emit.
            // RX7's source glass/cluster material is deliberately not used.
            Require(lightIndex>=0&&rearLight.bounds.center.z<centers[2].z,"Rear light slot must be behind the rear axle.");
            visual.leftBrakeLight=rearLight;visual.leftMaterialIndex=lightIndex;
            visual.brakeLightMaterial=rearLight.sharedMaterials[lightIndex];
            EditorUtility.SetDirty(visual.brakeLightMaterial);
            Require(visual.IsComplete(out reason),reason);
            var saved=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Visuals/"+stem+"_Visual.prefab");
            Require(saved!=null,"Could not save visual prefab.");
            var definition=ScriptableObject.CreateInstance<VehicleDefinition>();definition.name=stem+"_VehicleDefinition";
            definition.vehicleId=id;definition.displayName=display;definition.manufacturer=maker;
            definition.drivetrainLabel=rx7?"RWD":"RWD (DRIFT SETUP)";
            definition.description=rx7?"Responsive rotary coupe.\nPower-oversteer and long drifts.":"Heavy, planted drift setup.\nStable at speed with sustained momentum.";
            definition.config=config;definition.visualPrefab=saved.GetComponent<VehicleVisualSetup>();
            definition.power=rx7?.8f:.7f;definition.handling=rx7?.8f:.7f;definition.drift=rx7?.9f:.8f;definition.stability=rx7?.7f:.8f;
            SaveAsset(definition,Root+"/Definitions/"+definition.name+".asset");
            definition=Load<VehicleDefinition>(Root+"/Definitions/"+stem+"_VehicleDefinition.asset");Require(definition.IsPlayable,"Generated definition is incomplete.");
            Report.AppendLine(display+" imported bounds="+rawBounds.size.ToString("F6")+" visual scale="+scale.ToString("F6")+" offset="+offset.ToString("F6")+" world bounds="+(rawBounds.size*scale).ToString("F5"));
            Report.AppendLine("Wheelbase="+Mathf.Abs(wc[0].localPosition.z-wc[2].localPosition.z)+" front track="+Mathf.Abs(wc[0].localPosition.x-wc[1].localPosition.x)+" rear track="+Mathf.Abs(wc[2].localPosition.x-wc[3].localPosition.x));
            Report.AppendLine("Brake light="+lightPath+" slot="+lightIndex+" material="+lightMaterial+"; all other slots non-emissive.");
            return definition;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}


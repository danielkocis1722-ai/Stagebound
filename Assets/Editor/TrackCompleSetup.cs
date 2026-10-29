using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Explicit authoring utility: never runs automatically on import. Rebuilding
// overwrites only the generated Track Comple scene and its supporting meshes.
public static class TrackCompleSetup
{
    public const string ScenePath="Assets/Scenes/TrackCompleCircuit.unity";
    public const string DefinitionPath="Assets/Stages/TrackComple/TrackComple.asset";
    const string FolderPath="Assets/Stages/TrackComple";
    const float Scale=6f;
    static readonly Vector3 Offset=new Vector3(0,-3.6f,0);
    static readonly Dictionary<string,MeshCollider> Roads=new Dictionary<string,MeshCollider>();
    static readonly StringBuilder Report=new StringBuilder();
    struct RoutePoint { public Vector3 position;public string road; }
    static readonly List<RoutePoint> Route=new List<RoutePoint>();
    static Vector3 World(float x,float z)=>new Vector3(x*Scale,0,z*Scale);
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static void Folder(string path) {
        if(AssetDatabase.IsValidFolder(path))return;
        Folder(Path.GetDirectoryName(path).Replace('\\','/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
    }
    static Mesh SaveMesh(Mesh mesh,string filename) {
        string path=FolderPath+"/CollisionMeshes/"+filename+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
    }
    static Mesh WorldMesh(MeshFilter filter,string name) {
        var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=name;
        mesh.vertices=mesh.vertices.Select(filter.transform.TransformPoint).ToArray();
        if(filter.transform.localToWorldMatrix.determinant<0) {
            for(int s=0;s<mesh.subMeshCount;s++) {
                var triangles=mesh.GetTriangles(s);for(int i=0;i<triangles.Length;i+=3){int a=triangles[i];triangles[i]=triangles[i+1];triangles[i+1]=a;}
                mesh.SetTriangles(triangles,s);
            }
        }
        mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static MeshCollider Collision(MeshFilter filter,Transform parent,bool road) {
        var go=new GameObject(filter.name+" Collision");go.transform.SetParent(parent,false);go.isStatic=true;
        var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=SaveMesh(WorldMesh(filter,filter.name+" Collision"),filter.name.Replace(' ','_'));
        collider.convex=false;
        Report.AppendLine((road?"ROAD ":"MESH SOLID ")+filter.name+" vertices="+collider.sharedMesh.vertexCount+" world bounds="+collider.bounds.size);
        if(road)Roads[filter.name]=collider;
        return collider;
    }
    static void Box(MeshFilter filter,Transform parent) {
        var bounds=filter.GetComponent<Renderer>().bounds;
        var go=new GameObject(filter.name+" Box");go.transform.SetParent(parent,false);go.transform.position=bounds.center;go.isStatic=true;
        var box=go.AddComponent<BoxCollider>();box.size=Vector3.Max(bounds.size,Vector3.one*.18f);
        Report.AppendLine("BOX SOLID "+filter.name+" center="+bounds.center+" size="+box.size);
    }
    static Vector3 Surface(string name,float x,float z) {
        var p=World(x,z);
        Require(Roads.TryGetValue(name,out var collider),"Missing road collider "+name);
        var ray=new Ray(p+Vector3.up*200,Vector3.down);
        if(collider.Raycast(ray,out var hit,400))return hit.point;
        // Rounded authoring coordinates can land a fraction beyond one piece's
        // end. Accept the real adjoining road, never a fabricated height.
        foreach(var adjacent in Roads.Values)if(adjacent!=collider&&adjacent.Raycast(ray,out hit,400))return hit.point;
        throw new InvalidOperationException("Road gap on "+name+" at raw "+x+","+z);
    }
    static void Point(string road,float x,float z) {
        var p=Surface(road,x,z);
        if(Route.Count>0&&Vector3.Distance(Route[Route.Count-1].position,p)<.1f)return;
        Route.Add(new RoutePoint{road=road,position=p});
    }
    static void Line(string road,float x1,float z1,float x2,float z2) {
        int count=Mathf.CeilToInt(Vector2.Distance(new Vector2(x1,z1),new Vector2(x2,z2))/.35f);
        for(int i=0;i<=count;i++){float t=(float)i/count;Point(road,Mathf.Lerp(x1,x2,t),Mathf.Lerp(z1,z2,t));}
    }
    static void Arc(string road,float cx,float cz,float radius,float first,float last) {
        int count=Mathf.CeilToInt(Mathf.Abs(last-first)/5);
        for(int i=0;i<=count;i++){float a=Mathf.Lerp(first,last,(float)i/count)*Mathf.Deg2Rad;Point(road,cx+Mathf.Cos(a)*radius,cz+Mathf.Sin(a)*radius);}
    }
    static void BuildGuide(Transform model) {
        Route.Clear();
        Line("bridge",-.0941f,4.6117f,-.0941f,-17.0064f);
        Arc("road 180.002",-2.6253f,-17.0064f,2.5312f,0,-180);
        Line("road.003",-5.1565f,-17.0064f,-5.1565f,-13.7471f);
        Arc("road 90 fix",-7.6877f,-13.7471f,2.5312f,0,90);
        Line("road 90 fix",-7.6877f,-11.2159f,-10.9674f,-11.2159f);
        Arc("road 90.001",-10.9674f,-8.6847f,2.5312f,-90,-180);
        var crooked=model.Find("crooked road").GetComponent<MeshFilter>();
        var vertices=crooked.sharedMesh.vertices.Select(crooked.transform.TransformPoint).ToArray();
        // Median across matched edge pairs recovers the supplied S-bend's
        // center, rather than cutting it with an invented straight line.
        var sections=vertices.GroupBy(v=>Mathf.Round(v.z/Scale*1000)).OrderBy(g=>g.Key).Select(g=>{
            var xs=g.Select(v=>v.x/Scale).OrderBy(v=>v).ToArray();
            return new Vector2((xs[(xs.Length-1)/2]+xs[xs.Length/2])*.5f,g.Key/1000f);
        }).ToArray();
        foreach(var section in sections)Point("crooked road",section.x,Mathf.Clamp(section.y,-8.6846f,3.8650f));
        Arc("road 90.002",-10.9674f,3.8651f,2.5312f,180,90);
        Line("springboard",-10.9674f,6.3963f,-2.0751f,6.3963f);
        Line("Jump Connector",-2.0749f,6.3963f,6.8762f,6.3963f);
        Line("springboard.001",6.8764f,6.3963f,9.2204f,6.3963f);
        Line("road.001",9.2206f,6.3963f,12.8782f,6.3963f);
        Arc("road 180.001",12.8783f,3.8651f,2.5312f,90,-90);
        Line("road",12.8782f,1.3339f,7.4996f,1.3339f);
        Arc("road 90",7.4995f,3.8651f,2.5312f,-90,-180);
        Line("road.002",4.9683f,3.8652f,4.9683f,14.2347f);
        Arc("road 180",2.4371f,14.2348f,2.5312f,0,180);
        Line("bridge",-.0941f,14.2347f,-.0941f,4.6118f);
        var guide=new GameObject("Route Guide").transform;
        float length=0;
        for(int i=0;i<Route.Count;i++) {
            var marker=new GameObject(i.ToString("0000")+" "+Route[i].road).transform;marker.SetParent(guide,false);marker.position=Route[i].position;
            var delta=Route[(i+1)%Route.Count].position-marker.position;
            if(delta.sqrMagnitude>.001f)marker.rotation=Quaternion.LookRotation(delta,Vector3.up);
            if(i>0)length+=Vector3.Distance(Route[i-1].position,Route[i].position);
        }
        Report.AppendLine("Route guide: "+Route.Count+" sampled surfaces, length="+length.ToString("F2")+" world units. +Y up, no track yaw rotation.");
    }
    static void Connector(Transform model) {
        var source=model.Find("road").GetComponent<MeshFilter>();
        var mesh=UnityEngine.Object.Instantiate(source.sharedMesh);mesh.name="Track Comple Jump Connector";
        var vertices=mesh.vertices.Select(v=>model.InverseTransformPoint(source.transform.TransformPoint(v))).ToArray();
        float min=vertices.Min(v=>v.x),max=vertices.Max(v=>v.x);
        float sourceTop=vertices.Where(v=>Mathf.Abs(v.z-1.3339f)<.005f).Max(v=>v.y);
        Func<string,bool,float> asphaltTip=(name,left)=>{
            var filter=model.Find(name).GetComponent<MeshFilter>();
            var middle=filter.sharedMesh.vertices.Select(v=>model.InverseTransformPoint(filter.transform.TransformPoint(v))).Where(v=>Mathf.Abs(v.z-6.3963f)<.005f).ToArray();
            float tip=left?middle.Max(v=>v.x):middle.Min(v=>v.x);
            return middle.Where(v=>Mathf.Abs(v.x-tip)<.005f).Max(v=>v.y);
        };
        // The ramps' tall guardrails are not their driving surface. Match the
        // actual asphalt centerline at each tip, avoiding a collision step.
        float leftTop=asphaltTip("springboard",true),rightTop=asphaltTip("springboard.001",false);
        for(int i=0;i<vertices.Length;i++) {
            var v=vertices[i];float t=Mathf.InverseLerp(min,max,v.x);
            v.x=Mathf.Lerp(-2.075f,6.8763f,t);v.z+=6.3963f-1.3339f;v.y+=Mathf.Lerp(leftTop,rightTop,t)-sourceTop;vertices[i]=v;
        }
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
        mesh=SaveMesh(mesh,"JumpConnectorVisual");
        var go=new GameObject("Jump Connector",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(model,false);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
        Report.AppendLine("Visible jump-gap connector: reused road mesh, raw X -2.075..6.8763, Z 6.3963, asphalt Y "+leftTop+".."+rightTop+". Source ramps retained.");
    }
    static RaceTrigger Trigger(Transform parent,RaceTimer timer,string name,string road,float x,float z,Vector3 direction,int index) {
        var surface=Surface(road,x,z);var go=new GameObject(name,typeof(BoxCollider),typeof(RaceTrigger));go.transform.SetParent(parent,false);
        go.transform.SetPositionAndRotation(surface+Vector3.up*2.5f,Quaternion.LookRotation(direction,Vector3.up));
        var box=go.GetComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(16.5f,6,1.5f);
        var trigger=go.GetComponent<RaceTrigger>();trigger.raceTimer=timer;trigger.checkpointIndex=Mathf.Max(1,index);
        trigger.triggerType=index==0?RaceTrigger.TriggerType.StartFinish:RaceTrigger.TriggerType.Checkpoint1;
        if(index>0) {
            var point=new GameObject("Checkpoint "+index+" Respawn").transform;point.SetParent(parent,false);
            var raw=new Vector2(x,z)+new Vector2(direction.x,direction.z)*1.5f;
            point.SetPositionAndRotation(Surface(road,raw.x,raw.y)+Vector3.up*1.25f,go.transform.rotation);trigger.respawnPoint=point;
        }
        Report.AppendLine(name+" at "+surface.ToString("F3")+" heading="+go.transform.eulerAngles.y+" box="+box.size+" positive scale="+go.transform.localScale);
        return trigger;
    }
    public static void Run() {
        try{Build();Directory.CreateDirectory("Library/TrackIntegration");File.WriteAllText("Library/TrackIntegration/setup.txt",Report.ToString());EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);Directory.CreateDirectory("Library/TrackIntegration");File.WriteAllText("Library/TrackIntegration/setup.txt",Report+"\nFAIL "+e);EditorApplication.Exit(1);}
    }
    static void Build() {
        Folder(FolderPath+"/CollisionMeshes");Roads.Clear();
        Require(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Tracks/track comple.fbx")!=null,"Missing source FBX.");
        var baseline=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        Require(EditorSceneManager.SaveScene(baseline,ScenePath,true),"Could not create separate track scene.");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        foreach(var root in scene.GetRootGameObjects()) {
            var source=PrefabUtility.GetCorrespondingObjectFromSource(root);
            if(source!=null&&AssetDatabase.GetAssetPath(source)=="Assets/Models/Tracks/SouthLoop.glb")UnityEngine.Object.DestroyImmediate(root);
            else if(root.name=="Cube")UnityEngine.Object.DestroyImmediate(root); // existing flat test floor, only in copied scene
        }
        var oldTriggers=UnityEngine.Object.FindObjectsByType<RaceTrigger>();
        foreach(var trigger in oldTriggers)UnityEngine.Object.DestroyImmediate(trigger.gameObject);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Tracks/track comple.fbx"),scene);
        model.name="Track Comple Model";model.transform.position=Offset;model.transform.localScale=Vector3.one*Scale;
        Connector(model.transform);
        var collisionRoot=new GameObject("Track Collisions").transform;
        var roadRoot=new GameObject("Road").transform;roadRoot.SetParent(collisionRoot,false);
        var solids=new GameObject("Solids").transform;solids.SetParent(collisionRoot,false);
        var terrain=new GameObject("Terrain").transform;terrain.SetParent(collisionRoot,false);
        foreach(var filter in model.GetComponentsInChildren<MeshFilter>()) {
            string name=filter.name;
            bool road=name.StartsWith("road",StringComparison.Ordinal)||name=="bridge"||name=="crooked road"||name.StartsWith("springboard",StringComparison.Ordinal)||name=="Jump Connector";
            if(road)Collision(filter,roadRoot,true);
            else if(name=="Earth"||name=="parking")Collision(filter,terrain,false);
            else if(name.StartsWith("fence",StringComparison.Ordinal)||name.StartsWith("barrier",StringComparison.Ordinal)||name=="house"||name=="garages"||name.StartsWith("tower",StringComparison.Ordinal)||name=="police car"||name.StartsWith("flower bed",StringComparison.Ordinal))Box(filter,solids);
            else if(name.StartsWith("bunch of wheels",StringComparison.Ordinal)||name.StartsWith("wheel fence",StringComparison.Ordinal)||name.StartsWith("stone",StringComparison.Ordinal)||name=="stack of wheels"||name=="tribune"||name=="finish board")Collision(filter,solids,false);
        }
        Physics.SyncTransforms();BuildGuide(model.transform);
        var car=UnityEngine.Object.FindAnyObjectByType<CarController>();Require(car!=null,"Copied scene lost shared CarRoot.");
        var timer=UnityEngine.Object.FindAnyObjectByType<RaceTimer>();Require(timer!=null,"Copied scene lost RaceTimer.");
        var definition=AssetDatabase.LoadAssetAtPath<StageDefinition>(DefinitionPath);
        if(definition==null){definition=ScriptableObject.CreateInstance<StageDefinition>();AssetDatabase.CreateAsset(definition,DefinitionPath);}
        definition.stageId="track-comple";definition.stageName="Track Comple Circuit";definition.scenePath=ScenePath;
        definition.description="A compact technical circuit with flowing S-bends, tight hairpins and an elevated crossing.";
        definition.checkpointCount=4;definition.recordVersion=1;definition.importLegacySceneBest=false;definition.pointsPerSecondCredit=2000;definition.maxStyleCredit=8;definition.selectable=true;definition.difficultyLabel="TECHNICAL";
        timer.stageDefinition=definition;
        var setup=new GameObject("Track Comple Race Setup").transform;
        Trigger(setup,timer,"Start Finish","bridge",-.0941f,4.6117f,Vector3.back,0);
        Trigger(setup,timer,"Checkpoint 1 - Lower Straight","bridge",-.0941f,-12,Vector3.back,1);
        Trigger(setup,timer,"Checkpoint 2 - West S Bend","crooked road",-13.52f,2,Vector3.forward,2);
        Trigger(setup,timer,"Checkpoint 3 - Elevated Crossing","Jump Connector",3,6.3963f,Vector3.right,3);
        Trigger(setup,timer,"Checkpoint 4 - Upper Return","road.002",4.9683f,11,Vector3.forward,4);
        var spawn=new GameObject("SpawnPoint").transform;spawn.SetParent(setup,false);
        spawn.SetPositionAndRotation(Surface("bridge",-.0941f,8)+Vector3.up*1.25f,Quaternion.Euler(0,180,0));
        var respawn=car.GetComponent<RespawnManager>();respawn.startPoint=spawn;respawn.raceTimer=timer;
        car.transform.SetPositionAndRotation(spawn.position,spawn.rotation);
        Report.AppendLine("SpawnPoint="+spawn.position.ToString("F4")+" yaw=180, 1.25 units above actual bridge road; upstream of start line.");
        var camera=UnityEngine.Object.FindAnyObjectByType<FollowCamera>();camera.transform.SetPositionAndRotation(spawn.position-spawn.forward*camera.slowDistance+Vector3.up*camera.slowHeight,spawn.rotation);
        var catalog=AssetDatabase.LoadAssetAtPath<StageCatalog>("Assets/Stages/StageCatalog.asset");
        var entries=catalog.stages.Where(s=>s!=null&&s!=definition).ToList();int firstDisabled=entries.FindIndex(s=>!s.selectable);
        entries.Insert(firstDisabled>=0?firstDisabled:entries.Count,definition);catalog.stages=entries.ToArray();
        var build=EditorBuildSettings.scenes.ToList();build.RemoveAll(s=>s.path==ScenePath);build.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=build.ToArray();
        EditorUtility.SetDirty(definition);EditorUtility.SetDirty(catalog);EditorUtility.SetDirty(timer);EditorUtility.SetDirty(respawn);
        EditorSceneManager.MarkSceneDirty(scene);Require(EditorSceneManager.SaveScene(scene),"Could not save the second stage.");AssetDatabase.SaveAssets();
        Require(definition.IsPlayable,"Stage scene not enabled in build settings.");
        Require(UnityEngine.Object.FindObjectsByType<CarController>().Length==1,"Expected one playable car.");
        Require(UnityEngine.Object.FindObjectsByType<RaceTrigger>().Count(t=>t.triggerType!=RaceTrigger.TriggerType.StartFinish)==4,"Expected four checkpoints.");
        Require(collisionRoot.GetComponentsInChildren<Collider>().All(c=>!c.isTrigger&&c.attachedRigidbody==null&&c.transform.lossyScale==Vector3.one),"Track colliders must be static with unit positive transforms.");
        Report.AppendLine("PASS scene, two available stages, positive static colliders, one common car, spawn and four ordered checkpoint references.");
    }
}

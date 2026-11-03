using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ValidateMetreAssets
{
    static readonly StringBuilder log=new StringBuilder();
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);log.AppendLine("PASS "+message);}
    static bool Road(RaycastHit h)
    {
        if(!(h.collider is MeshCollider mesh))return false;
        if(h.collider.transform.parent!=null&&h.collider.transform.parent.name=="Road")return true;
        if(!h.collider.name.Contains("SouthLoop-col"))return false;
        // South Loop's single source mesh also contains sand/concrete runoff.
        // Its material submeshes distinguish actual asphalt and painted lines.
        var renderer=GameObject.Find("SouthLoop-col").GetComponent<Renderer>();
        for(int i=0;i<mesh.sharedMesh.subMeshCount;i++)
        {
            var sub=mesh.sharedMesh.GetSubMesh(i);
            if(h.triangleIndex*3<sub.indexStart||h.triangleIndex*3>=sub.indexStart+sub.indexCount)continue;
            string material=renderer.sharedMaterials[i].name;
            return material.Contains("Tarmac")||material.Contains("Stripe");
        }
        return false;
    }
    static bool Support(Vector3 p,out RaycastHit hit)
    {
        hit=default;var hits=Physics.RaycastAll(p+Vector3.up*3,Vector3.down,6,~0,QueryTriggerInteraction.Ignore).Where(Road).OrderBy(h=>Mathf.Abs(h.point.y-p.y)).ToArray();
        if(hits.Length==0)return false;hit=hits[0];return Mathf.Abs(hit.point.y-p.y)<1.5f;
    }
    public static void Run()
    {
        try
        {
            foreach(var path in EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray())
            {
                var scene=EditorSceneManager.OpenScene(path);
                var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).ToArray();
                Check(all.All(c=>c!=null),"No missing scripts "+path);
                Check(all.OfType<MeshFilter>().All(f=>f.sharedMesh!=null)&&all.OfType<MeshCollider>().All(c=>c.sharedMesh!=null),"No missing geometry "+path);
                Check(all.OfType<Renderer>().All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader!=null)),"No missing materials/shaders "+path);
                foreach(var component in all)
                {
                    var so=new SerializedObject(component);var p=so.GetIterator();
                    while(p.NextVisible(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue==null&&p.objectReferenceEntityIdValue!=EntityId.None)throw new Exception("Missing reference "+path+"/"+component.name+"/"+p.propertyPath);
                }
                var car=all.OfType<CarController>().SingleOrDefault();if(car==null)continue;
                Check(car.transform.lossyScale==Vector3.one,"Unit-scale CarRoot "+path);
                Check(all.OfType<WheelCollider>().All(w=>w.transform.lossyScale==Vector3.one&&w.radius>.25f&&w.radius<.4f),"Unit-scale metre WheelColliders "+path);
                Check(all.OfType<Collider>().Where(c=>c.attachedRigidbody==null).All(c=>c.transform.lossyScale==Vector3.one),"Unit-scale static colliders and race triggers "+path);
                foreach(var mc in all.OfType<MeshCollider>().Where(c=>c.attachedRigidbody==null))
                {
                    var vertices=mc.sharedMesh.vertices;float best=float.PositiveInfinity;
                    foreach(var filter in all.OfType<MeshFilter>().Where(f=>f.GetComponentInParent<CarController>()==null&&f.sharedMesh.vertexCount==vertices.Length))
                    {
                        var visual=filter.sharedMesh.vertices;float error=0;
                        for(int i=0;i<vertices.Length;i++)error=Mathf.Max(error,Vector3.Distance(mc.transform.TransformPoint(vertices[i]),filter.transform.TransformPoint(visual[i])));
                        best=Mathf.Min(best,error);
                    }
                    Check(best<.002f,$"Visual/collider vertex agreement {stageLabel(path)}/{mc.name}: maxError={best:F6}m");
                }
                var timer=all.OfType<RaceTimer>().Single();var stage=timer.stageDefinition;
                Check(!stage.importLegacySceneBest&&stage.recordVersion>=(stage.stageId=="cherry-mountain"||stage.stageId=="drift-tracks"?3:2),"Real-scale record namespace "+stage.stageId);
                Check(all.OfType<RaceTrigger>().Count(g=>g.triggerType==RaceTrigger.TriggerType.Checkpoint1)==stage.checkpointCount,"Authored checkpoint count "+stage.stageId);
                Check(all.OfType<RaceTrigger>().All(g=>g.GetComponent<BoxCollider>().size.x<15),"Metre gate widths "+stage.stageId);
                Physics.SyncTransforms();var guide=GameObject.Find("Route Guide");
                var samples=guide!=null?guide.transform.Cast<Transform>().ToArray():all.OfType<RaceTrigger>().Select(g=>g.transform).ToArray();
                var widths=new System.Collections.Generic.List<float>();int probes=0;
                foreach(var sample in samples)
                {
                    var p=sample.position;
                    if(guide==null)
                    {
                        var hits=Physics.RaycastAll(p+Vector3.up*5,Vector3.down,15,~0,QueryTriggerInteraction.Ignore).Where(Road).OrderBy(h=>Mathf.Abs(h.point.y-p.y)).ToArray();
                        Check(hits.Length>0,"Gate over road "+sample.name);p=hits[0].point;
                    }
                    foreach(var d in RealScaleMigration.Vehicles)
                    {
                        var c=d.config;
                        foreach(var w in new[]{c.frontLeft,c.frontRight,c.backLeft,c.backRight})
                        {
                            var foot=p+sample.rotation*new Vector3(w.localPosition.x,0,w.localPosition.z);
                            CheckSupport(Support(foot,out var hit)&&hit.normal.y>.5f,stage.stageId+"/"+sample.name+"/"+d.vehicleId);probes++;
                        }
                    }
                    if(probes%120!=0&&guide!=null)continue;
                    float width=0;
                    foreach(float side in new[]{-1f,1f})for(float x=.1f;x<20;x+=.1f)
                    {
                        if(!Support(p+sample.right*(side*x),out _))break;width+=.1f;
                    }
                    widths.Add(width);
                }
                widths.Sort();log.AppendLine($"ROAD {stage.stageId} sampledWidthMedian={widths[widths.Count/2]:F3} minimum={widths.Min():F3} maximum={widths.Max():F3} widthSamples={widths.Count} supportedWheelProbes={probes} staticColliders={all.OfType<Collider>().Count(c=>c.attachedRigidbody==null&&!c.isTrigger)}");
                // Collider support may include shoulders or other atlas regions.
                // Record visual surface/material identity separately from that envelope.
                foreach(var f in all.OfType<MeshFilter>().Where(f=>stage.stageId=="south-loop"&&f.GetComponentInParent<CarController>()==null||stage.stageId=="track-comple"&&f.name=="road"||stage.stageId=="cherry-mountain"&&f.name=="Object_5"||stage.stageId=="drift-tracks"&&f.name=="Drift Track"))
                {
                    var r=f.GetComponent<Renderer>();
                    if(r!=null)log.AppendLine($"VISUAL_SURFACE {stage.stageId}/{f.name} bounds={r.bounds.size:F3} materials={string.Join(",",r.sharedMaterials.Select(m=>m.name))}");
                }
                if(stage.stageId=="cherry-mountain")
                {
                    var sample=guide.transform.GetChild(100);
                    Check(Support(sample.position-sample.right,out var left)&&Support(sample.position+sample.right,out var right),"Cherry native atlas cross section");
                    Support(sample.position+sample.right,out var rightHit);Support(sample.position-sample.right,out var leftHit);
                    float gradient=Vector2.Distance(leftHit.textureCoord,rightHit.textureCoord)/Vector3.Distance(leftHit.point,rightHit.point);
                    log.AppendLine($"PAVEMENT cherry-mountain atlasRows=527..752 textureHeight=1024 UVperMetre={gradient:F8} insideEdgeLines={(225f/1024)/gradient:F4} metres");
                }
            }
            foreach(var d in RealScaleMigration.Vehicles)
            {
                Check(d.IsPlayable,"Playable definition "+d.vehicleId);
                var prefab=d.visualPrefab;
                Check(prefab.GetComponentsInChildren<Component>(true).All(c=>c!=null),"Visual prefab scripts "+d.vehicleId);
                Check(prefab.GetComponentsInChildren<MeshFilter>(true).All(f=>f.sharedMesh!=null),"Visual prefab meshes "+d.vehicleId);
            }
            log.AppendLine("COMPLETE");File.WriteAllText(RealScaleMigration.Output+"/assets.txt",log.ToString());EditorApplication.Exit(0);
        }
        catch(Exception e){log.AppendLine("FAIL "+e);File.WriteAllText(RealScaleMigration.Output+"/assets.txt",log.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void CheckSupport(bool ok,string message){if(!ok)throw new Exception("Unsupported wheel footprint "+message);}
    static string stageLabel(string path)=>System.IO.Path.GetFileNameWithoutExtension(path);
}

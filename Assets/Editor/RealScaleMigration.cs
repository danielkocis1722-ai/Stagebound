using System.Linq;
using UnityEditor;
using UnityEngine;

// Read-only geometry helpers for explicit metre-scale editor validation.
public static class RealScaleMigration
{
    public const string Output = "Library/FullScale";
    public static VehicleDefinition[] Vehicles => AssetDatabase.LoadAssetAtPath<VehicleCatalog>("Assets/Vehicles/VehicleCatalog.asset").vehicles.Where(v => v != null && v.IsPlayable).ToArray();
    public static StageDefinition[] Stages => AssetDatabase.LoadAssetAtPath<StageCatalog>("Assets/Stages/StageCatalog.asset").stages.Where(s => s != null && s.selectable && !string.IsNullOrEmpty(s.scenePath)).ToArray();
    public static Bounds Geometry(Transform rig, Transform part)
    {
        var result = new Bounds(); bool first = true;
        foreach (var f in part.GetComponentsInChildren<MeshFilter>(true))
            foreach (var vertex in f.sharedMesh.vertices)
            {
                var p = rig.InverseTransformPoint(f.transform.TransformPoint(vertex));
                if (first) { result = new Bounds(p, Vector3.zero); first = false; } else result.Encapsulate(p);
            }
        return result;
    }
}

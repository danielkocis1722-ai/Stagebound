using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The supplied RX7 repeats wheel mesh names four times. Unique subasset names
// make Unity's mesh file IDs deterministic without editing or moving the FBX.
public sealed class RX7ImportPostprocessor : AssetPostprocessor
{
    public override uint GetVersion() => 1;
    void OnPostprocessModel(GameObject root)
    {
        if(assetPath!="Assets/Models/Cars/RX7_FBX.fbx")return;
        var seen=new HashSet<Mesh>();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)) {
            var mesh=filter.sharedMesh;
            if(mesh==null||!seen.Add(mesh))continue;
            mesh.name=AnimationUtility.CalculateTransformPath(filter.transform,root.transform).Replace('/','_')+"_Mesh";
        }
    }
}

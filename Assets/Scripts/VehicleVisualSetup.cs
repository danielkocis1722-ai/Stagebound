using System;
using UnityEngine;

// Put this on a visual-only prefab. Wheel pivots can contain rotated/offset meshes.
[DisallowMultipleComponent]
public class VehicleVisualSetup : MonoBehaviour
{
    public Transform bodyRoot;
    public Transform frontLeftWheel, frontRightWheel, backLeftWheel, backRightWheel;
    public VisualBodyRoll.UnsprungAxleVisual[] unsprungAxles = Array.Empty<VisualBodyRoll.UnsprungAxleVisual>();
    [Header("Optional rear lights (verified light-only material slots)")]
    public Renderer leftBrakeLight, rightBrakeLight;
    [Min(0)] public int leftMaterialIndex, rightMaterialIndex;
    public Material brakeLightMaterial;

    public bool IsComplete(out string reason)
    {
        reason=null;
        if(bodyRoot==null || (bodyRoot!=transform && !bodyRoot.IsChildOf(transform)))
            reason="Assign the visual/body root inside this prefab.";
        else {
            var wheels=new[]{frontLeftWheel,frontRightWheel,backLeftWheel,backRightWheel};
            var unique=new System.Collections.Generic.HashSet<Transform>();
            foreach(var wheel in wheels)
                if(wheel==null || !wheel.IsChildOf(bodyRoot) || !unique.Add(wheel))
                { reason="Assign four distinct wheel pivots inside the visual root."; break; }
        }
        if(reason==null && (GetComponentInChildren<Collider>(true)!=null ||
            GetComponentInChildren<Rigidbody>(true)!=null || GetComponentInChildren<CarController>(true)!=null))
            reason="Visual prefabs must contain no physics colliders, rigidbodies or vehicle controller.";
        return reason==null;
    }
}


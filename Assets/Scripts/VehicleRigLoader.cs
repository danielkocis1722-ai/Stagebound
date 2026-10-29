using UnityEngine;

// Apply before front-grip caches, respawn, effects Awake and controller Start.
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CarController),typeof(Rigidbody))]
public class VehicleRigLoader : MonoBehaviour
{
    public VehicleCatalog catalog;
    public VehicleDefinition existingDefinition;
    public VehicleVisualSetup existingVisual;
    public BoxCollider chassisCollider;
    public VehicleDefinition CurrentDefinition { get; private set; }
    public VehicleVisualSetup CurrentVisual { get; private set; }
    public bool Initialized { get; private set; }

    void Awake() => Initialize();
    public bool Initialize()
    {
        if(Initialized) return true;
        var definition=VehicleSelection.Resolve(catalog);
        if(definition==null) { Debug.LogError("Vehicle catalog has no valid playable selection.",this); return false; }
        var car=GetComponent<CarController>();
        if(car.frontLeft==null || car.frontRight==null || car.backLeft==null || car.backRight==null)
        { Debug.LogError("Vehicle rig needs its four WheelCollider references.",this); return false; }
        VehicleVisualSetup visual;
        if(definition==existingDefinition && existingVisual!=null && existingVisual.IsComplete(out _))
            visual=existingVisual;
        else {
            if(existingVisual!=null) existingVisual.gameObject.SetActive(false);
            visual=Instantiate(definition.visualPrefab,transform,false);
            visual.name=definition.displayName+" Visual";
        }
        var config=definition.config;
        config.ApplyTo(car,chassisCollider);
        visual.transform.SetLocalPositionAndRotation(config.visualPositionOffset,Quaternion.Euler(config.visualRotationOffset));
        visual.transform.localScale=config.visualScale;
        visual.gameObject.SetActive(true);
        car.frontLeftWheel=visual.frontLeftWheel; car.frontRightWheel=visual.frontRightWheel;
        car.backLeftWheel=visual.backLeftWheel; car.backRightWheel=visual.backRightWheel;
        var motion=GetComponent<VisualBodyRoll>();
        if(motion!=null) { motion.visualModel=visual.bodyRoot; motion.unsprungAxles=visual.unsprungAxles==null
            ? System.Array.Empty<VisualBodyRoll.UnsprungAxleVisual>() : (VisualBodyRoll.UnsprungAxleVisual[])visual.unsprungAxles.Clone(); }
        var lights=GetComponent<BrakeLights>();
        if(lights!=null) {
            lights.leftRear=new BrakeLights.Lamp { renderer=visual.leftBrakeLight, materialIndex=visual.leftMaterialIndex };
            lights.rightRear=new BrakeLights.Lamp { renderer=visual.rightBrakeLight, materialIndex=visual.rightMaterialIndex };
            lights.dedicatedRearLightMaterial=visual.brakeLightMaterial;
            lights.enabled=visual.brakeLightMaterial!=null && (visual.leftBrakeLight!=null || visual.rightBrakeLight!=null);
        }
        var audio=GetComponent<VehicleAudio>();
        if(audio!=null && definition.engineLoop!=null && audio.engineLayers!=null && audio.engineLayers.Length>0 && audio.engineLayers[0]!=null)
            audio.engineLayers[0].clip=definition.engineLoop;
        CurrentDefinition=definition; CurrentVisual=visual; Initialized=true;
        return true;
    }
}


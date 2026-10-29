using System;
using UnityEngine;

// Vehicle tuning only. Runtime state stays on the shared rig.
[CreateAssetMenu(menuName = "Stagebound/Vehicles/Vehicle Config")]
public class VehicleConfig : ScriptableObject
{
    [Serializable] public class Engine
    {
        public float maxEngineTorque;
        public float idleRPM;
        public float redlineRPM;
        public float engineRevUpSpeed;
        public float engineRevDownSpeed;
        public float engineThrottleResponse;
        public AnimationCurve torqueCurve = new AnimationCurve();
    }
    public Engine engine = new Engine();
    [Serializable] public class Transmission
    {
        public float[] gearRatios = Array.Empty<float>();
        public float reverseRatio;
        public float finalDrive;
        public float drivetrainEfficiency;
        public float shiftDuration;
    }
    public Transmission transmission = new Transmission();
    [Serializable] public class Steering
    {
        public float maxSteerAngle;
        public bool useAckermannSteering;
        public float steeringSpeed;
        public float steeringReturnSpeed;
        public float steeringInputSpeed;
        public float steeringInputReturnSpeed;
        public float throttleInputSpeed;
        public float throttleInputReturnSpeed;
        public float steeringDeadzone;
        public float lowSpeedSteeringExponent;
        public float highSpeedSteeringExponent;
        public float fullSteeringCurveSpeed;
    }
    public Steering steering = new Steering();
    [Serializable] public class Differential
    {
        public CarController.DifferentialMode differentialMode;
        public float lsdStrength;
        public float differentialResponse;
    }
    public Differential differential = new Differential();
    [Serializable] public class Clutch
    {
        public float launchClutchMin;
        public float launchClutchSpeed;
        public float clutchEngageSpeed;
        public float clutchDisengageSpeed;
        public float manualClutchEngageSpeed;
        public float manualClutchReleaseSpeed;
        public float maxClutchTorque;
        public float clutchCouplingStrength;
        public float clutchKickStrength;
        public float engineInertia;
    }
    public Clutch clutch = new Clutch();
    [Serializable] public class Handbrake
    {
        public float brakeTorque;
        public float handbrakeTorque;
        public float handbrakeRearGrip;
        public float handbrakeGripDropSpeed;
        public float handbrakeGripRecoverySpeed;
    }
    public Handbrake handbrake = new Handbrake();
    [Serializable] public class RearGrip
    {
        public float powerSlipGripReduction;
        public float powerSlipGripResponse;
        public float rearGripRecoveryResponse;
        public float countersteerGripRecoveryResponse;
        public float controlledDriftGripRecoveryResponse;
        public float driftGripEnterAngle;
        public float driftGripExitAngle;
        public bool enableCombinedSlip;
        public float combinedSlipStart;
        public float combinedSlipFull;
        public float minimumCombinedSlipMultiplier;
        public float combinedSlipResponse;
        public float breakawayHandoffStartAngle;
        public float breakawayHandoffFullAngle;
    }
    public RearGrip rearGrip = new RearGrip();
    [Serializable] public class FrontRecovery
    {
        public float minimumSpeedKmh;
        public float minimumForwardSpeedKmh;
        public float minimumDriftAngle;
        public float minimumRearSidewaysSlip;
        public float minimumRearForwardSlip;
        public float minimumCountersteerInput;
        public AnimationCurve recoveryByAngle = new AnimationCurve();
        public float maximumRecoveryMultiplier;
        public float blendResponse;
        public float recoveryGripInput;
        public float deliberateRecoveryMultiplier;
        public float recoveryStartAngle;
        public float recoveryFullAngle;
        public float recoveryThrottleCeiling;
        public float deliberateCountersteerInput;
        public float recoveryBlendResponse;
    }
    public FrontRecovery frontRecovery = new FrontRecovery();
    [Serializable] public class YawStability
    {
        public float minimumSpeedKmh;
        public float fullSpeedKmh;
        public float minimumForwardSpeedKmh;
        public float startAngle;
        public float fullAngle;
        public float startYawRate;
        public float fullYawRate;
        public float startAngleGrowth;
        public float fullAngleGrowth;
        public float maximumDampingRate;
    }
    public YawStability yawStability = new YawStability();
    [Serializable] public class VisualMotion
    {
        public float visualRollStrength;
        public float visualPitchStrength;
        public float bodySpring;
        public float bodyDamping;
        public float maximumVisualRoll;
        public float maximumVisualPitch;
    }
    public VisualMotion visualMotion = new VisualMotion();

    [Serializable] public class Chassis
    {
        public float mass, linearDamping, angularDamping;
        public Vector3 centerOfMass;
        public Vector3 colliderPosition, colliderScale = Vector3.one, colliderCenter, colliderSize;
        public Quaternion colliderRotation = Quaternion.identity;
        public float frontAntiRoll, rearAntiRoll;
    }
    public Chassis chassis = new Chassis();
    [Serializable] public class Tires
    {
        public float extremumSlip, extremumValue, asymptoteSlip, asymptoteValue, stiffness;
        public void Capture(WheelFrictionCurve curve)
        { extremumSlip=curve.extremumSlip; extremumValue=curve.extremumValue; asymptoteSlip=curve.asymptoteSlip; asymptoteValue=curve.asymptoteValue; stiffness=curve.stiffness; }
        public WheelFrictionCurve Curve => new WheelFrictionCurve { extremumSlip=extremumSlip, extremumValue=extremumValue, asymptoteSlip=asymptoteSlip, asymptoteValue=asymptoteValue, stiffness=stiffness };
    }
    [Serializable] public class Wheel
    {
        public Vector3 localPosition, localScale = Vector3.one, center;
        public Quaternion localRotation = Quaternion.identity;
        public float radius, mass, damping, suspensionDistance, forceAppPointDistance;
        public float spring, damper, targetPosition;
        public Tires forwardTires = new Tires(), sidewaysTires = new Tires();
        public void Capture(WheelCollider wheel)
        {
            localPosition=wheel.transform.localPosition; localRotation=wheel.transform.localRotation; localScale=wheel.transform.localScale;
            center=wheel.center; radius=wheel.radius; mass=wheel.mass; damping=wheel.wheelDampingRate;
            suspensionDistance=wheel.suspensionDistance; forceAppPointDistance=wheel.forceAppPointDistance;
            var suspension=wheel.suspensionSpring; spring=suspension.spring; damper=suspension.damper; targetPosition=suspension.targetPosition;
            forwardTires.Capture(wheel.forwardFriction); sidewaysTires.Capture(wheel.sidewaysFriction);
        }
        public void Apply(WheelCollider wheel)
        {
            wheel.transform.SetLocalPositionAndRotation(localPosition,localRotation); wheel.transform.localScale=localScale;
            wheel.center=center; wheel.radius=radius; wheel.mass=mass; wheel.wheelDampingRate=damping;
            wheel.suspensionDistance=suspensionDistance; wheel.forceAppPointDistance=forceAppPointDistance;
            wheel.suspensionSpring=new JointSpring { spring=spring, damper=damper, targetPosition=targetPosition };
            wheel.forwardFriction=forwardTires.Curve; wheel.sidewaysFriction=sidewaysTires.Curve;
        }
        public bool Valid => radius>0 && mass>0 && suspensionDistance>0 && spring>0 && damper>=0 &&
            targetPosition>=0 && targetPosition<=1 && localScale.x>0 && localScale.y>0 && localScale.z>0;
    }
    [Header("Suspension, wheel geometry and tires (individual wheels)")]
    public Wheel frontLeft = new Wheel(), frontRight = new Wheel(), backLeft = new Wheel(), backRight = new Wheel();
    [Header("Visual alignment")]
    public Vector3 visualPositionOffset, visualRotationOffset, visualScale = Vector3.one;
    [Header("Optional systems")]
    public bool frontRecoveryEnabled = true, yawStabilityEnabled = true, visualMotionEnabled = true;
    [Min(1)] public int startingGear = 1;

    public bool IsValid(out string reason)
    {
        reason = null;
        if (!(chassis.mass>0) || !(engine.idleRPM>0) || !(engine.redlineRPM>engine.idleRPM) ||
            !(engine.maxEngineTorque>0) || engine.torqueCurve==null || engine.torqueCurve.length<2 ||
            transmission.gearRatios==null || transmission.gearRatios.Length==0 || !(transmission.finalDrive>0))
            reason="Missing chassis, engine or transmission tuning.";
        else if (!frontLeft.Valid || !frontRight.Valid || !backLeft.Valid || !backRight.Valid)
            reason="Invalid wheel geometry or suspension tuning.";
        else if (visualScale.x<=0 || visualScale.y<=0 || visualScale.z<=0)
            reason="Visual scale must be positive.";
        else if (startingGear<1 || startingGear>transmission.gearRatios.Length)
            reason="Starting gear is outside the transmission.";
        else foreach(float ratio in transmission.gearRatios) if (!(ratio>0)) { reason="Forward gear ratios must be positive."; break; }
        return reason==null;
    }
    static AnimationCurve CopyCurve(AnimationCurve source) => source==null ? new AnimationCurve() :
        new AnimationCurve(source.keys) { preWrapMode=source.preWrapMode, postWrapMode=source.postWrapMode };

    // Called once, before the rig's Awake/Start caches. Never called each frame.
    public void ApplyTo(CarController car, BoxCollider chassisCollider)
    {
        var body=car.GetComponent<Rigidbody>();
        body.mass=chassis.mass; body.linearDamping=chassis.linearDamping; body.angularDamping=chassis.angularDamping;
        car.centerOfMass=chassis.centerOfMass; body.centerOfMass=chassis.centerOfMass;
        if(chassisCollider!=null) {
            chassisCollider.transform.SetLocalPositionAndRotation(chassis.colliderPosition,chassis.colliderRotation);
            chassisCollider.transform.localScale=chassis.colliderScale; chassisCollider.center=chassis.colliderCenter; chassisCollider.size=chassis.colliderSize;
        }
        frontLeft.Apply(car.frontLeft); frontRight.Apply(car.frontRight); backLeft.Apply(car.backLeft); backRight.Apply(car.backRight);
        var anti=car.GetComponent<AntiRollBar>();
        if(anti!=null) { anti.frontAntiRoll=chassis.frontAntiRoll; anti.rearAntiRoll=chassis.rearAntiRoll; }
        car.currentGear=startingGear;
        car.maxEngineTorque=engine.maxEngineTorque;
        car.idleRPM=engine.idleRPM;
        car.redlineRPM=engine.redlineRPM;
        car.engineRevUpSpeed=engine.engineRevUpSpeed;
        car.engineRevDownSpeed=engine.engineRevDownSpeed;
        car.engineThrottleResponse=engine.engineThrottleResponse;
        car.torqueCurve=CopyCurve(engine.torqueCurve);
        car.gearRatios=(float[])transmission.gearRatios.Clone();
        car.reverseRatio=transmission.reverseRatio;
        car.finalDrive=transmission.finalDrive;
        car.drivetrainEfficiency=transmission.drivetrainEfficiency;
        car.shiftDuration=transmission.shiftDuration;
        car.maxSteerAngle=steering.maxSteerAngle;
        car.useAckermannSteering=steering.useAckermannSteering;
        car.steeringSpeed=steering.steeringSpeed;
        car.steeringReturnSpeed=steering.steeringReturnSpeed;
        car.steeringInputSpeed=steering.steeringInputSpeed;
        car.steeringInputReturnSpeed=steering.steeringInputReturnSpeed;
        car.throttleInputSpeed=steering.throttleInputSpeed;
        car.throttleInputReturnSpeed=steering.throttleInputReturnSpeed;
        car.steeringDeadzone=steering.steeringDeadzone;
        car.lowSpeedSteeringExponent=steering.lowSpeedSteeringExponent;
        car.highSpeedSteeringExponent=steering.highSpeedSteeringExponent;
        car.fullSteeringCurveSpeed=steering.fullSteeringCurveSpeed;
        car.differentialMode=differential.differentialMode;
        car.lsdStrength=differential.lsdStrength;
        car.differentialResponse=differential.differentialResponse;
        car.launchClutchMin=clutch.launchClutchMin;
        car.launchClutchSpeed=clutch.launchClutchSpeed;
        car.clutchEngageSpeed=clutch.clutchEngageSpeed;
        car.clutchDisengageSpeed=clutch.clutchDisengageSpeed;
        car.manualClutchEngageSpeed=clutch.manualClutchEngageSpeed;
        car.manualClutchReleaseSpeed=clutch.manualClutchReleaseSpeed;
        car.maxClutchTorque=clutch.maxClutchTorque;
        car.clutchCouplingStrength=clutch.clutchCouplingStrength;
        car.clutchKickStrength=clutch.clutchKickStrength;
        car.engineInertia=clutch.engineInertia;
        car.brakeTorque=handbrake.brakeTorque;
        car.handbrakeTorque=handbrake.handbrakeTorque;
        car.handbrakeRearGrip=handbrake.handbrakeRearGrip;
        car.handbrakeGripDropSpeed=handbrake.handbrakeGripDropSpeed;
        car.handbrakeGripRecoverySpeed=handbrake.handbrakeGripRecoverySpeed;
        car.powerSlipGripReduction=rearGrip.powerSlipGripReduction;
        car.powerSlipGripResponse=rearGrip.powerSlipGripResponse;
        car.rearGripRecoveryResponse=rearGrip.rearGripRecoveryResponse;
        car.countersteerGripRecoveryResponse=rearGrip.countersteerGripRecoveryResponse;
        car.controlledDriftGripRecoveryResponse=rearGrip.controlledDriftGripRecoveryResponse;
        car.driftGripEnterAngle=rearGrip.driftGripEnterAngle;
        car.driftGripExitAngle=rearGrip.driftGripExitAngle;
        car.enableCombinedSlip=rearGrip.enableCombinedSlip;
        car.combinedSlipStart=rearGrip.combinedSlipStart;
        car.combinedSlipFull=rearGrip.combinedSlipFull;
        car.minimumCombinedSlipMultiplier=rearGrip.minimumCombinedSlipMultiplier;
        car.combinedSlipResponse=rearGrip.combinedSlipResponse;
        car.breakawayHandoffStartAngle=rearGrip.breakawayHandoffStartAngle;
        car.breakawayHandoffFullAngle=rearGrip.breakawayHandoffFullAngle;
        var front=car.GetComponent<FrontAxleRecovery>();
        if(front!=null) {
        front.minimumSpeedKmh=frontRecovery.minimumSpeedKmh;
        front.minimumForwardSpeedKmh=frontRecovery.minimumForwardSpeedKmh;
        front.minimumDriftAngle=frontRecovery.minimumDriftAngle;
        front.minimumRearSidewaysSlip=frontRecovery.minimumRearSidewaysSlip;
        front.minimumRearForwardSlip=frontRecovery.minimumRearForwardSlip;
        front.minimumCountersteerInput=frontRecovery.minimumCountersteerInput;
        front.recoveryByAngle=CopyCurve(frontRecovery.recoveryByAngle);
        front.maximumRecoveryMultiplier=frontRecovery.maximumRecoveryMultiplier;
        front.blendResponse=frontRecovery.blendResponse;
        front.recoveryGripInput=frontRecovery.recoveryGripInput;
        front.deliberateRecoveryMultiplier=frontRecovery.deliberateRecoveryMultiplier;
        front.recoveryStartAngle=frontRecovery.recoveryStartAngle;
        front.recoveryFullAngle=frontRecovery.recoveryFullAngle;
        front.recoveryThrottleCeiling=frontRecovery.recoveryThrottleCeiling;
        front.deliberateCountersteerInput=frontRecovery.deliberateCountersteerInput;
        front.recoveryBlendResponse=frontRecovery.recoveryBlendResponse;
        front.enabled=frontRecoveryEnabled;
        }
        var yaw=car.GetComponent<DriftYawStability>();
        if(yaw!=null) {
        yaw.minimumSpeedKmh=yawStability.minimumSpeedKmh;
        yaw.fullSpeedKmh=yawStability.fullSpeedKmh;
        yaw.minimumForwardSpeedKmh=yawStability.minimumForwardSpeedKmh;
        yaw.startAngle=yawStability.startAngle;
        yaw.fullAngle=yawStability.fullAngle;
        yaw.startYawRate=yawStability.startYawRate;
        yaw.fullYawRate=yawStability.fullYawRate;
        yaw.startAngleGrowth=yawStability.startAngleGrowth;
        yaw.fullAngleGrowth=yawStability.fullAngleGrowth;
        yaw.maximumDampingRate=yawStability.maximumDampingRate;
        yaw.enabled=yawStabilityEnabled;
        }
        var visual=car.GetComponent<VisualBodyRoll>();
        if(visual!=null) {
        visual.visualRollStrength=visualMotion.visualRollStrength;
        visual.visualPitchStrength=visualMotion.visualPitchStrength;
        visual.bodySpring=visualMotion.bodySpring;
        visual.bodyDamping=visualMotion.bodyDamping;
        visual.maximumVisualRoll=visualMotion.maximumVisualRoll;
        visual.maximumVisualPitch=visualMotion.maximumVisualPitch;
        visual.enabled=visualMotionEnabled;
        }
    }
#if UNITY_EDITOR
    // Copies values from the current serialized rig; does not invent AE86 tuning.
    public void CaptureFrom(CarController car, BoxCollider chassisCollider, Transform model)
    {
        var body=car.GetComponent<Rigidbody>();
        chassis.mass=body.mass; chassis.linearDamping=body.linearDamping; chassis.angularDamping=body.angularDamping; chassis.centerOfMass=car.centerOfMass;
        if(chassisCollider!=null) { chassis.colliderPosition=chassisCollider.transform.localPosition; chassis.colliderRotation=chassisCollider.transform.localRotation;
            chassis.colliderScale=chassisCollider.transform.localScale; chassis.colliderCenter=chassisCollider.center; chassis.colliderSize=chassisCollider.size; }
        var anti=car.GetComponent<AntiRollBar>(); if(anti!=null) { chassis.frontAntiRoll=anti.frontAntiRoll; chassis.rearAntiRoll=anti.rearAntiRoll; }
        frontLeft.Capture(car.frontLeft); frontRight.Capture(car.frontRight); backLeft.Capture(car.backLeft); backRight.Capture(car.backRight);
        if(model!=null) { visualPositionOffset=model.localPosition; visualRotationOffset=model.localEulerAngles; visualScale=model.localScale; }
        startingGear=car.currentGear;
        engine.maxEngineTorque=car.maxEngineTorque;
        engine.idleRPM=car.idleRPM;
        engine.redlineRPM=car.redlineRPM;
        engine.engineRevUpSpeed=car.engineRevUpSpeed;
        engine.engineRevDownSpeed=car.engineRevDownSpeed;
        engine.engineThrottleResponse=car.engineThrottleResponse;
        engine.torqueCurve=CopyCurve(car.torqueCurve);
        transmission.gearRatios=(float[])car.gearRatios.Clone();
        transmission.reverseRatio=car.reverseRatio;
        transmission.finalDrive=car.finalDrive;
        transmission.drivetrainEfficiency=car.drivetrainEfficiency;
        transmission.shiftDuration=car.shiftDuration;
        steering.maxSteerAngle=car.maxSteerAngle;
        steering.useAckermannSteering=car.useAckermannSteering;
        steering.steeringSpeed=car.steeringSpeed;
        steering.steeringReturnSpeed=car.steeringReturnSpeed;
        steering.steeringInputSpeed=car.steeringInputSpeed;
        steering.steeringInputReturnSpeed=car.steeringInputReturnSpeed;
        steering.throttleInputSpeed=car.throttleInputSpeed;
        steering.throttleInputReturnSpeed=car.throttleInputReturnSpeed;
        steering.steeringDeadzone=car.steeringDeadzone;
        steering.lowSpeedSteeringExponent=car.lowSpeedSteeringExponent;
        steering.highSpeedSteeringExponent=car.highSpeedSteeringExponent;
        steering.fullSteeringCurveSpeed=car.fullSteeringCurveSpeed;
        differential.differentialMode=car.differentialMode;
        differential.lsdStrength=car.lsdStrength;
        differential.differentialResponse=car.differentialResponse;
        clutch.launchClutchMin=car.launchClutchMin;
        clutch.launchClutchSpeed=car.launchClutchSpeed;
        clutch.clutchEngageSpeed=car.clutchEngageSpeed;
        clutch.clutchDisengageSpeed=car.clutchDisengageSpeed;
        clutch.manualClutchEngageSpeed=car.manualClutchEngageSpeed;
        clutch.manualClutchReleaseSpeed=car.manualClutchReleaseSpeed;
        clutch.maxClutchTorque=car.maxClutchTorque;
        clutch.clutchCouplingStrength=car.clutchCouplingStrength;
        clutch.clutchKickStrength=car.clutchKickStrength;
        clutch.engineInertia=car.engineInertia;
        handbrake.brakeTorque=car.brakeTorque;
        handbrake.handbrakeTorque=car.handbrakeTorque;
        handbrake.handbrakeRearGrip=car.handbrakeRearGrip;
        handbrake.handbrakeGripDropSpeed=car.handbrakeGripDropSpeed;
        handbrake.handbrakeGripRecoverySpeed=car.handbrakeGripRecoverySpeed;
        rearGrip.powerSlipGripReduction=car.powerSlipGripReduction;
        rearGrip.powerSlipGripResponse=car.powerSlipGripResponse;
        rearGrip.rearGripRecoveryResponse=car.rearGripRecoveryResponse;
        rearGrip.countersteerGripRecoveryResponse=car.countersteerGripRecoveryResponse;
        rearGrip.controlledDriftGripRecoveryResponse=car.controlledDriftGripRecoveryResponse;
        rearGrip.driftGripEnterAngle=car.driftGripEnterAngle;
        rearGrip.driftGripExitAngle=car.driftGripExitAngle;
        rearGrip.enableCombinedSlip=car.enableCombinedSlip;
        rearGrip.combinedSlipStart=car.combinedSlipStart;
        rearGrip.combinedSlipFull=car.combinedSlipFull;
        rearGrip.minimumCombinedSlipMultiplier=car.minimumCombinedSlipMultiplier;
        rearGrip.combinedSlipResponse=car.combinedSlipResponse;
        rearGrip.breakawayHandoffStartAngle=car.breakawayHandoffStartAngle;
        rearGrip.breakawayHandoffFullAngle=car.breakawayHandoffFullAngle;
        var front=car.GetComponent<FrontAxleRecovery>();
        if(front!=null) {
        frontRecovery.minimumSpeedKmh=front.minimumSpeedKmh;
        frontRecovery.minimumForwardSpeedKmh=front.minimumForwardSpeedKmh;
        frontRecovery.minimumDriftAngle=front.minimumDriftAngle;
        frontRecovery.minimumRearSidewaysSlip=front.minimumRearSidewaysSlip;
        frontRecovery.minimumRearForwardSlip=front.minimumRearForwardSlip;
        frontRecovery.minimumCountersteerInput=front.minimumCountersteerInput;
        frontRecovery.recoveryByAngle=CopyCurve(front.recoveryByAngle);
        frontRecovery.maximumRecoveryMultiplier=front.maximumRecoveryMultiplier;
        frontRecovery.blendResponse=front.blendResponse;
        frontRecovery.recoveryGripInput=front.recoveryGripInput;
        frontRecovery.deliberateRecoveryMultiplier=front.deliberateRecoveryMultiplier;
        frontRecovery.recoveryStartAngle=front.recoveryStartAngle;
        frontRecovery.recoveryFullAngle=front.recoveryFullAngle;
        frontRecovery.recoveryThrottleCeiling=front.recoveryThrottleCeiling;
        frontRecovery.deliberateCountersteerInput=front.deliberateCountersteerInput;
        frontRecovery.recoveryBlendResponse=front.recoveryBlendResponse;
        frontRecoveryEnabled=front.enabled;
        }
        var yaw=car.GetComponent<DriftYawStability>();
        if(yaw!=null) {
        yawStability.minimumSpeedKmh=yaw.minimumSpeedKmh;
        yawStability.fullSpeedKmh=yaw.fullSpeedKmh;
        yawStability.minimumForwardSpeedKmh=yaw.minimumForwardSpeedKmh;
        yawStability.startAngle=yaw.startAngle;
        yawStability.fullAngle=yaw.fullAngle;
        yawStability.startYawRate=yaw.startYawRate;
        yawStability.fullYawRate=yaw.fullYawRate;
        yawStability.startAngleGrowth=yaw.startAngleGrowth;
        yawStability.fullAngleGrowth=yaw.fullAngleGrowth;
        yawStability.maximumDampingRate=yaw.maximumDampingRate;
        yawStabilityEnabled=yaw.enabled;
        }
        var visual=car.GetComponent<VisualBodyRoll>();
        if(visual!=null) {
        visualMotion.visualRollStrength=visual.visualRollStrength;
        visualMotion.visualPitchStrength=visual.visualPitchStrength;
        visualMotion.bodySpring=visual.bodySpring;
        visualMotion.bodyDamping=visual.bodyDamping;
        visualMotion.maximumVisualRoll=visual.maximumVisualRoll;
        visualMotion.maximumVisualPitch=visual.maximumVisualPitch;
        visualMotionEnabled=visual.enabled;
        }
    }
#endif
}


using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VehicleSetupEditor
{
    [MenuItem("Stagebound/Vehicles/Capture Selected Rig as Config")]
    static void CaptureSelected()
    {
        var car=Selection.activeGameObject.GetComponentInParent<CarController>();
        string path=EditorUtility.SaveFilePanelInProject("Capture Vehicle Config","NewVehicle_VehicleConfig","asset","Save a copy of the selected rig's current tuning.");
        if(string.IsNullOrEmpty(path)) return;
        var config=ScriptableObject.CreateInstance<VehicleConfig>();
        config.CaptureFrom(car,FindBody(car),car.GetComponent<VisualBodyRoll>()?.visualModel);
        AssetDatabase.CreateAsset(config,path); AssetDatabase.SaveAssets(); Selection.activeObject=config;
    }
    [MenuItem("Stagebound/Vehicles/Capture Selected Rig as Config",true)]
    static bool CanCapture() => !EditorApplication.isPlaying && Selection.activeGameObject!=null &&
        Selection.activeGameObject.GetComponentInParent<CarController>()!=null;

    static BoxCollider FindBody(CarController car)
    {
        foreach(var box in car.GetComponentsInChildren<BoxCollider>(true))
            if(!box.isTrigger && box.attachedRigidbody==car.GetComponent<Rigidbody>()) return box;
        return null;
    }
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path)) return;
        Folder(Path.GetDirectoryName(path).Replace('\\','/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
    }
    // One-time migration entry point for a saved project copy. Intentionally not
    // an automatic import hook: later config edits must never be recaptured.
    public static void MigrateAE86Baseline()
    {
        const string root="Assets/Vehicles";
        if(AssetDatabase.LoadAssetAtPath<VehicleCatalog>(root+"/VehicleCatalog.asset")!=null)
            throw new InvalidOperationException("Vehicle catalog already exists; migration will not overwrite tuning.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var car=UnityEngine.Object.FindAnyObjectByType<CarController>();
        var motion=car.GetComponent<VisualBodyRoll>();
        if(motion==null || motion.visualModel==null) throw new InvalidOperationException("AE86 visual is not configured.");
        var body=FindBody(car);
        if(body==null) throw new InvalidOperationException("AE86 chassis collider is not configured.");
        Folder(root+"/Configs"); Folder(root+"/Definitions"); Folder(root+"/Visuals");
        Directory.CreateDirectory("Library/VehicleSelectionValidation");
        File.WriteAllText("Library/VehicleSelectionValidation/Car.before.json",EditorJsonUtility.ToJson(car,true));
        var config=ScriptableObject.CreateInstance<VehicleConfig>();
        config.CaptureFrom(car,body,motion.visualModel);
        if(!config.IsValid(out string reason)) throw new InvalidOperationException(reason);
        AssetDatabase.CreateAsset(config,root+"/Configs/AE86_VehicleConfig.asset");
        var visual=motion.visualModel.gameObject.AddComponent<VehicleVisualSetup>();
        visual.bodyRoot=motion.visualModel;
        visual.frontLeftWheel=car.frontLeftWheel; visual.frontRightWheel=car.frontRightWheel;
        visual.backLeftWheel=car.backLeftWheel; visual.backRightWheel=car.backRightWheel;
        visual.unsprungAxles=(VisualBodyRoll.UnsprungAxleVisual[])motion.unsprungAxles.Clone();
        var lamps=car.GetComponent<BrakeLights>();
        if(lamps!=null) {
            visual.leftBrakeLight=lamps.leftRear?.renderer; visual.leftMaterialIndex=lamps.leftRear?.materialIndex??0;
            visual.rightBrakeLight=lamps.rightRear?.renderer; visual.rightMaterialIndex=lamps.rightRear?.materialIndex??0;
            visual.brakeLightMaterial=lamps.dedicatedRearLightMaterial;
        }
        if(!visual.IsComplete(out reason)) throw new InvalidOperationException(reason);
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        var clone=UnityEngine.Object.Instantiate(visual.gameObject);
        if(PrefabUtility.IsPartOfPrefabInstance(clone)) PrefabUtility.UnpackPrefabInstance(clone,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        clone.name="AE86_Visual"; clone.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity); clone.transform.localScale=Vector3.one;
        var prefab=PrefabUtility.SaveAsPrefabAsset(clone,root+"/Visuals/AE86_Visual.prefab");
        UnityEngine.Object.DestroyImmediate(clone);
        var ae86=ScriptableObject.CreateInstance<VehicleDefinition>();
        ae86.vehicleId="ae86"; ae86.displayName="Toyota AE86"; ae86.manufacturer="Toyota";
        ae86.description="Lightweight\nResponsive\nDrift-focused";
        ae86.config=config; ae86.visualPrefab=prefab.GetComponent<VehicleVisualSetup>();
        AssetDatabase.CreateAsset(ae86,root+"/Definitions/AE86_VehicleDefinition.asset");
        var placeholder=ScriptableObject.CreateInstance<VehicleDefinition>();
        placeholder.vehicleId="vehicle02"; placeholder.displayName="Vehicle 02"; placeholder.drivetrainLabel="DEVELOPMENT";
        placeholder.description="Your next vehicle.\nModel and tuning to be prepared."; placeholder.selectable=false;
        placeholder.power=placeholder.handling=placeholder.drift=placeholder.stability=0;
        AssetDatabase.CreateAsset(placeholder,root+"/Definitions/Vehicle02_VehicleDefinition.asset");
        var catalog=ScriptableObject.CreateInstance<VehicleCatalog>();
        catalog.defaultVehicle=ae86; catalog.vehicles=new[]{ae86,placeholder};
        AssetDatabase.CreateAsset(catalog,root+"/VehicleCatalog.asset");
        if(!catalog.IsValid(out reason)) throw new InvalidOperationException(reason);
        var loader=car.gameObject.AddComponent<VehicleRigLoader>();
        loader.catalog=catalog; loader.existingDefinition=ae86; loader.existingVisual=visual; loader.chassisCollider=body;
        EditorUtility.SetDirty(visual); EditorUtility.SetDirty(loader); EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var menuScene=EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        var menu=UnityEngine.Object.FindAnyObjectByType<MainMenu>(); menu.vehicleCatalog=catalog;
        EditorUtility.SetDirty(menu); EditorSceneManager.MarkSceneDirty(menuScene); EditorSceneManager.SaveScene(menuScene);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Library/VehicleSelectionValidation/migration.txt","PASS: Current AE86 tuning captured; existing visual retained; menu and single rig wired.");
        EditorApplication.Exit(0);
    }
}

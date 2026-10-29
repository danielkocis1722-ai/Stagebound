using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Render only the visual prefab. The race rig and vehicle physics never run here.
public class VehiclePreview : MonoBehaviour
{
    public RawImage image;
    GameObject world;
    Transform turntable;
    RenderTexture texture;
    const int PreviewLayer = 30;
    public bool Show(VehicleDefinition definition)
    {
        ClearWorld();
        image.texture=null; image.enabled=false; image.uvRect=new Rect(0,0,1,1);
        if(definition==null) return false;
        if(definition.visualPrefab==null || !definition.visualPrefab.IsComplete(out _)) {
            if(definition.previewImage==null) return false;
            var sprite=definition.previewImage; image.texture=sprite.texture;
            image.uvRect=new Rect(sprite.rect.x/sprite.texture.width,sprite.rect.y/sprite.texture.height,
                sprite.rect.width/sprite.texture.width,sprite.rect.height/sprite.texture.height);
            image.enabled=true; return true;
        }
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) return false;
        if(texture==null) { texture=new RenderTexture(640,400,16) { name="Stagebound Vehicle Preview" }; texture.Create(); }
        world=new GameObject("Vehicle Preview (visual only)"); world.transform.position=new Vector3(0,-1000,0);
        turntable=new GameObject("Turntable").transform; turntable.SetParent(world.transform,false);
        var model=Instantiate(definition.visualPrefab,turntable,false);
        model.gameObject.SetActive(true);
        var config=definition.config;
        if(config!=null) {
            model.transform.SetLocalPositionAndRotation(config.visualPositionOffset,Quaternion.Euler(config.visualRotationOffset));
            model.transform.localScale=config.visualScale;
        }
        foreach(var child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=PreviewLayer;
        var renderers=model.GetComponentsInChildren<Renderer>(true);
        if(renderers.Length==0) { ClearWorld(); return false; }
        Bounds bounds=renderers[0].bounds; foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        model.transform.position+=turntable.position-bounds.center;
        float radius=Mathf.Max(.5f,bounds.extents.magnitude);
        var cameraObject=new GameObject("Preview Camera",typeof(Camera)); cameraObject.transform.SetParent(world.transform,false);
        var camera=cameraObject.GetComponent<Camera>(); camera.targetTexture=texture; camera.cullingMask=1<<PreviewLayer;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color32(9,17,26,255);
        camera.fieldOfView=35; camera.nearClipPlane=.05f; camera.farClipPlane=radius*12;
        camera.allowHDR=false; camera.allowMSAA=false;
        camera.transform.position=turntable.position+new Vector3(1,.5f,-1).normalized*radius*2.8f;
        camera.transform.LookAt(turntable.position);
        var lampObject=new GameObject("Preview Light",typeof(Light)); lampObject.transform.SetParent(world.transform,false);
        var lamp=lampObject.GetComponent<Light>(); lamp.type=LightType.Directional; lamp.intensity=1.6f;
        lamp.cullingMask=1<<PreviewLayer; lamp.shadows=LightShadows.None; lamp.transform.rotation=Quaternion.Euler(40,-35,0);
        image.texture=texture; image.enabled=true; world.SetActive(isActiveAndEnabled); return true;
    }
    void Update() { if(turntable!=null) turntable.Rotate(Vector3.up,12*Time.unscaledDeltaTime,Space.Self); }
    void OnEnable() { if(world!=null) world.SetActive(true); }
    void OnDisable() { if(world!=null) world.SetActive(false); }
    void ClearWorld() { if(world!=null) { world.SetActive(false); Destroy(world); } turntable=null; }
    void OnDestroy() { ClearWorld(); if(texture!=null) { texture.Release(); Destroy(texture); } }
}


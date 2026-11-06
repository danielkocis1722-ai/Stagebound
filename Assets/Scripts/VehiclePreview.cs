using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Render only the visual prefab. The race rig and vehicle physics never run here.
public class VehiclePreview : MonoBehaviour
{
    public RawImage image;
    private GameObject world;
    private Transform turntable;
    private RenderTexture texture;
    private RenderTexture previousTexture;
    private RawImage previousImage;
    private float blend = 1f;
    private Material floorMaterial;
    private const int PreviewLayer = 30;

    public bool Show(VehicleDefinition definition)
    {
        bool crossfade = image.enabled && image.texture == texture && texture != null && Application.isPlaying;
        if (crossfade)
        {
            if (previousTexture == null)
            {
                previousTexture = new RenderTexture(texture.descriptor) { name = "Previous showroom frame" };
                previousTexture.Create();
                previousImage = new GameObject("Preview crossfade", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                previousImage.transform.SetParent(image.transform, false);
                previousImage.rectTransform.anchorMin = Vector2.zero;
                previousImage.rectTransform.anchorMax = Vector2.one;
                previousImage.rectTransform.offsetMin = previousImage.rectTransform.offsetMax = Vector2.zero;
                previousImage.raycastTarget = false;
                previousImage.texture = previousTexture;
            }
            // Reuse the outgoing snapshot during rapid browsing instead of stacking cameras.
            if (blend >= .5f) Graphics.Blit(texture, previousTexture);
            blend = 0f;
            previousImage.enabled = true;
            previousImage.color = Color.white;
        }
        ClearWorld();
        image.texture = null;
        image.enabled = false;
        image.uvRect = new Rect(0, 0, 1, 1);
        if (definition == null)
            return false;
        if (definition.visualPrefab == null || !definition.visualPrefab.IsComplete(out _))
        {
            if (definition.previewImage == null)
                return false;
            var sprite = definition.previewImage;
            image.texture = sprite.texture;
            image.uvRect = new Rect(sprite.rect.x / sprite.texture.width, sprite.rect.y / sprite.texture.height, sprite.rect.width / sprite.texture.width, sprite.rect.height / sprite.texture.height);
            image.enabled = true;
            return true;
        }

        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            return false;
        if (texture == null)
        {
            texture = new RenderTexture(1440, 795, 24)
            {
                name = "Stagebound Vehicle Preview"
            };
            texture.Create();
        }

        world = new GameObject("Vehicle Preview (visual only)");
        world.transform.position = new Vector3(0, -1000, 0);
        turntable = new GameObject("Turntable").transform;
        turntable.SetParent(world.transform, false);
        var model = Instantiate(definition.visualPrefab, turntable, false);
        model.gameObject.SetActive(true);
        var config = definition.config;
        if (config != null)
        {
            model.transform.SetLocalPositionAndRotation(config.visualPositionOffset, Quaternion.Euler(config.visualRotationOffset));
            model.transform.localScale = config.visualScale;
        }

        foreach (var child in model.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = PreviewLayer;
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            ClearWorld();
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        model.transform.position += turntable.position - bounds.center;
        float radius = Mathf.Max(.5f, bounds.extents.magnitude);
        var cameraObject = new GameObject("Preview Camera", typeof(Camera));
        cameraObject.transform.SetParent(world.transform, false);
        var camera = cameraObject.GetComponent<Camera>();
        camera.targetTexture = texture;
        camera.cullingMask = 1 << PreviewLayer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(12, 21, 29, 255);
        camera.fieldOfView = 35;
        camera.nearClipPlane = .05f;
        camera.farClipPlane = radius * 12;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.transform.position = turntable.position + new Vector3(1, .40f, -1).normalized * radius * 2.65f;
        camera.transform.LookAt(turntable.position);
        var lampObject = new GameObject("Preview Light", typeof(Light));
        lampObject.transform.SetParent(world.transform, false);
        var lamp = lampObject.GetComponent<Light>();
        lamp.type = LightType.Directional;
        lamp.intensity = 1.6f;
        lamp.cullingMask = 1 << PreviewLayer;
        lamp.shadows = LightShadows.None;
        lamp.transform.rotation = Quaternion.Euler(40, -35, 0);
        var fillObject = new GameObject("Showroom fill", typeof(Light));
        fillObject.transform.SetParent(world.transform, false);
        var fill = fillObject.GetComponent<Light>();
        fill.type = LightType.Point;
        fill.color = new Color(.65f, .83f, 1f);
        fill.intensity = 4f;
        fill.range = radius * 5f;
        fill.cullingMask = 1 << PreviewLayer;
        fillObject.transform.localPosition = new Vector3(-radius, radius, -radius);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Showroom plinth";
        floor.layer = PreviewLayer;
        floor.transform.SetParent(world.transform, false);
        floor.transform.localPosition = new Vector3(0, -bounds.extents.y - .07f, 0);
        floor.transform.localScale = new Vector3(radius * 2.25f, .06f, radius * 2.25f);
        floor.GetComponent<Collider>().enabled = false;
        if (floorMaterial == null)
        {
            floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMaterial.SetColor("_BaseColor", new Color(.045f, .065f, .075f));
            floorMaterial.SetFloat("_Smoothness", .25f);
        }
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
        image.texture = texture;
        image.enabled = true;
        world.SetActive(isActiveAndEnabled);
        return true;
    }

    private void Update()
    {
        if (turntable != null)
            turntable.Rotate(Vector3.up, 7 * Time.unscaledDeltaTime, Space.Self);
        if (previousImage != null && previousImage.enabled)
        {
            blend = Mathf.Min(1f, blend + Time.unscaledDeltaTime / .38f);
            previousImage.color = new Color(1, 1, 1, 1f - Mathf.SmoothStep(0, 1, blend));
            if (blend >= 1f) previousImage.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (world != null)
            world.SetActive(true);
    }

    private void OnDisable()
    {
        if (world != null)
            world.SetActive(false);
    }

    private void ClearWorld()
    {
        if (world != null)
        {
            world.SetActive(false);
            Release(world);
        }

        turntable = null;
    }

    private void OnDestroy()
    {
        ClearWorld();
        if (texture != null)
        {
            texture.Release();
            Release(texture);
        }
        if (previousTexture != null) { previousTexture.Release(); Release(previousTexture); }
        Release(floorMaterial);
    }

    private static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}

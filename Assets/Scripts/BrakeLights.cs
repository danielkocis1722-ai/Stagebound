using System;
using UnityEngine;

[DefaultExecutionOrder(150)]
[RequireComponent(typeof(CarController))]
public class BrakeLights : MonoBehaviour
{
    [Serializable]
    public class Lamp
    {
        [Tooltip("Use a rear-light renderer or emission-masked light material slot, not an unmasked body material.")]
        public Renderer renderer;
        [Min(0)] public int materialIndex;
        [NonSerialized] public Material originalMaterial, runtimeMaterial;
        [NonSerialized] public MaterialPropertyBlock originalProperties, properties;
    }

    public Lamp leftRear = new Lamp();
    public Lamp rightRear = new Lamp();
    [Tooltip("Required safety reference: only a verified rear-light-only material may emit. Never assign Body or Body 1.")]
    public Material dedicatedRearLightMaterial;
    [ColorUsage(false, true)] public Color normalColor = new Color(1, .025f, .01f);
    [ColorUsage(false, true)] public Color brakingColor = new Color(1, .035f, .015f);
    [Min(0f)] public float normalIntensity = 0f;
    [Min(0f)] public float brakingIntensity = 4f;
    [Range(0f, 1f)] public float brakeThreshold = .02f;
    [Min(.1f)] public float response = 18f;
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    CarController car;
    Color currentEmission;

    void Awake()
    {
        car = GetComponent<CarController>();
        Configure(leftRear, dedicatedRearLightMaterial);
        if (leftRear == null || rightRear == null || leftRear.renderer != rightRear.renderer || leftRear.materialIndex != rightRear.materialIndex)
            Configure(rightRear, dedicatedRearLightMaterial);
        currentEmission = normalColor * normalIntensity;
        Apply(leftRear, currentEmission); Apply(rightRear, currentEmission);
    }

    static void Configure(Lamp lamp, Material allowedMaterial)
    {
        if (lamp == null || lamp.renderer == null || allowedMaterial == null) return;
        var materials = lamp.renderer.sharedMaterials;
        if (lamp.materialIndex < 0 || lamp.materialIndex >= materials.Length) return;
        var original = materials[lamp.materialIndex];
        if (original == null || original != allowedMaterial || !original.HasProperty(EmissionColor)) return;
        lamp.originalMaterial = original;
        lamp.originalProperties = new MaterialPropertyBlock();
        lamp.properties = new MaterialPropertyBlock();
        lamp.renderer.GetPropertyBlock(lamp.originalProperties, lamp.materialIndex);
        lamp.renderer.GetPropertyBlock(lamp.properties, lamp.materialIndex);
        // Shader keywords cannot be enabled by a property block. Clone only this
        // light slot so enabling emission never edits the shared imported material.
        lamp.runtimeMaterial = new Material(original) { name = original.name + " (Brake Light)" };
        lamp.runtimeMaterial.SetColor(EmissionColor, Color.black);
        lamp.runtimeMaterial.DisableKeyword("_EMISSION");
        materials[lamp.materialIndex] = lamp.runtimeMaterial;
        lamp.renderer.sharedMaterials = materials;
    }

    void Update()
    {
        // HandbrakeInput is deliberately not part of the brake-light signal.
        Color target = car.BrakeInput > brakeThreshold ? brakingColor * brakingIntensity : normalColor * normalIntensity;
        currentEmission = Color.Lerp(currentEmission, target, 1f - Mathf.Exp(-response * Time.deltaTime));
        // Exponential smoothing never reaches zero on its own. Finish the fade
        // exactly, then disable the private material's emission shader variant.
        Color difference = currentEmission - target;
        if (Mathf.Max(Mathf.Abs(difference.r), Mathf.Abs(difference.g), Mathf.Abs(difference.b)) < .001f)
            currentEmission = target;
        Apply(leftRear, currentEmission); Apply(rightRear, currentEmission);
    }

    static void Apply(Lamp lamp, Color emission)
    {
        if (lamp == null || lamp.renderer == null || lamp.runtimeMaterial == null) return;
        // Preserve any other property overrides applied to this material slot.
        lamp.renderer.GetPropertyBlock(lamp.properties, lamp.materialIndex);
        lamp.properties.SetColor(EmissionColor, emission);
        lamp.renderer.SetPropertyBlock(lamp.properties, lamp.materialIndex);
        bool emitting = Mathf.Max(emission.r, emission.g, emission.b) > 0f;
        if (emitting != lamp.runtimeMaterial.IsKeywordEnabled("_EMISSION"))
        {
            if (emitting) lamp.runtimeMaterial.EnableKeyword("_EMISSION");
            else lamp.runtimeMaterial.DisableKeyword("_EMISSION");
        }
    }

    void OnDisable()
    {
        Apply(leftRear, normalColor * normalIntensity); Apply(rightRear, normalColor * normalIntensity);
    }

    static void Restore(Lamp lamp)
    {
        if (lamp == null || lamp.runtimeMaterial == null) return;
        if (lamp.renderer != null)
        {
            var materials = lamp.renderer.sharedMaterials;
            if (lamp.materialIndex < materials.Length && materials[lamp.materialIndex] == lamp.runtimeMaterial)
            {
                materials[lamp.materialIndex] = lamp.originalMaterial;
                lamp.renderer.sharedMaterials = materials;
                lamp.renderer.SetPropertyBlock(lamp.originalProperties, lamp.materialIndex);
            }
        }
        Destroy(lamp.runtimeMaterial);
    }

    void OnDestroy() { Restore(leftRear); Restore(rightRear); }
}

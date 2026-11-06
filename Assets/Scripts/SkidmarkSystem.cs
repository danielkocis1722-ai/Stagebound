using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Bounded, reusable world-space mesh. No objects or allocations per skid segment.
public class SkidmarkSystem : MonoBehaviour
{
    public Material material;
    [Range(64, 8192)]
    public int maxSegments = 2048;
    [Min(.01f)]
    public float minSegmentLength = .06f;
    [Min(.1f)]
    public float maxSegmentLength = 3f;
    [Min(0f)]
    public float groundOffset = .003f;
    private Mesh mesh;
    private GameObject marksObject;
    private Vector3[] vertices;
    private Color32[] colors;
    private int nextSegment;
    private bool dirty;
    private Material runtimeMaterial;
    private Texture2D treadTexture;

    private void Awake()
    {
        if (material == null)
            return;
        maxSegments = Mathf.Clamp(maxSegments, 64, 8192);
        vertices = new Vector3[maxSegments * 4];
        colors = new Color32[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var indices = new int[maxSegments * 6];
        for (int i = 0; i < maxSegments; i++)
        {
            int v = i * 4, t = i * 6;
            uv[v] = new Vector2(0, 0);
            uv[v + 1] = new Vector2(1, 0);
            uv[v + 2] = new Vector2(0, 1);
            uv[v + 3] = new Vector2(1, 1);
            indices[t] = v;
            indices[t + 1] = v + 2;
            indices[t + 2] = v + 1;
            indices[t + 3] = v + 1;
            indices[t + 4] = v + 2;
            indices[t + 5] = v + 3;
        }

        mesh = new Mesh
        {
            name = "Vehicle skidmark ring buffer"
        };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.uv = uv;
        mesh.triangles = indices;
        // Soft transverse edges and restrained longitudinal rubber streaks.
        // Generated once; no imported artwork or material changes shared with smoke.
        treadTexture = new Texture2D(64, 16, TextureFormat.RGBA32, false);
        treadTexture.name = "Procedural rubber streaks";
        treadTexture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[64 * 16];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 64; x++)
            {
                float u = (x + .5f) / 64f;
                float edge = Mathf.SmoothStep(0, 1, Mathf.Min(u, 1 - u) * 12f);
                float streak = .80f + .12f * Mathf.Sin(u * 91f) + .08f * Mathf.Sin(u * 173f);
                pixels[y * 64 + x] = new Color32(255, 255, 255, (byte)(255 * edge * streak));
            }
        treadTexture.SetPixels32(pixels);
        treadTexture.Apply(false, true);
        runtimeMaterial = new Material(material);
        runtimeMaterial.SetTexture("_BaseMap", treadTexture);
        marksObject = new GameObject("Skidmarks - " + name, typeof(MeshFilter), typeof(MeshRenderer));
        SceneManager.MoveGameObjectToScene(marksObject, gameObject.scene);
        marksObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = marksObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = runtimeMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    public bool AddSegment(Vector3 from, Vector3 to, Vector3 fromNormal, Vector3 toNormal, float fromIntensity, float toIntensity, float width, Collider surface = null)
    {
        if (mesh == null || !isActiveAndEnabled)
            return false;
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance < minSegmentLength || distance > maxSegmentLength)
            return false;
        // Short projected pieces follow slopes/crests rather than bridging a curved road.
        int pieces = Mathf.CeilToInt(distance / .25f);
        for (int i = 0; i < pieces; i++)
        {
            float a = (float)i / pieces, b = (float)(i + 1) / pieces;
            WriteSegment(Vector3.Lerp(from, to, a), Vector3.Lerp(from, to, b),
                Vector3.Slerp(fromNormal, toNormal, a), Vector3.Slerp(fromNormal, toNormal, b),
                Mathf.Lerp(fromIntensity, toIntensity, a), Mathf.Lerp(fromIntensity, toIntensity, b), width, surface);
        }
        return true;
    }

    private void WriteSegment(Vector3 from, Vector3 to, Vector3 fromNormal, Vector3 toNormal, float fromIntensity, float toIntensity, float width, Collider surface)
    {
        Vector3 direction = to - from;
        Vector3 sideFrom = Vector3.Cross(fromNormal, direction).normalized * (width * .5f);
        Vector3 sideTo = Vector3.Cross(toNormal, direction).normalized * (width * .5f);
        if (!Project(from - sideFrom, fromNormal, surface, out var a) ||
            !Project(from + sideFrom, fromNormal, surface, out var b) ||
            !Project(to - sideTo, toNormal, surface, out var c) ||
            !Project(to + sideTo, toNormal, surface, out var d)) return;
        int v = nextSegment * 4;
        vertices[v] = a;
        vertices[v + 1] = b;
        vertices[v + 2] = c;
        vertices[v + 3] = d;
        var startColor = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(fromIntensity) * 255));
        var endColor = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(toIntensity) * 255));
        colors[v] = colors[v + 1] = startColor;
        colors[v + 2] = colors[v + 3] = endColor;
        nextSegment = (nextSegment + 1) % maxSegments;
        dirty = true;
    }

    private bool Project(Vector3 point, Vector3 normal, Collider surface, out Vector3 projected)
    {
        float offset = Mathf.Min(groundOffset, .004f);
        projected = point + normal * offset;
        if (surface == null) return true;
        if (!surface.Raycast(new Ray(point + normal * .2f, -normal), out var hit, .4f)) return false;
        if (Vector3.Dot(hit.normal, normal) < .5f) return false;
        projected = hit.point + hit.normal * offset;
        return true;
    }

    private void LateUpdate()
    {
        if (!dirty || mesh == null)
            return;
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.RecalculateBounds();
        dirty = false;
    }

    public void Clear()
    {
        if (vertices == null)
            return;
        System.Array.Clear(vertices, 0, vertices.Length);
        System.Array.Clear(colors, 0, colors.Length);
        nextSegment = 0;
        dirty = true;
    }

    private void OnDestroy()
    {
        Release(marksObject); Release(mesh); Release(runtimeMaterial); Release(treadTexture);
    }

    private static void Release(Object resource)
    {
        if (resource == null) return;
        if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
    }
}

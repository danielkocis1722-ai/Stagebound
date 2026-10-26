using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Bounded, reusable world-space mesh. No objects or allocations per skid segment.
public class SkidmarkSystem : MonoBehaviour
{
    public Material material;
    [Range(64, 8192)] public int maxSegments = 2048;
    [Min(.01f)] public float minSegmentLength = .12f;
    [Min(.1f)] public float maxSegmentLength = 3f;
    [Min(0f)] public float groundOffset = .015f;
    Mesh mesh;
    GameObject marksObject;
    Vector3[] vertices;
    Color32[] colors;
    int nextSegment;
    bool dirty;

    void Awake()
    {
        if (material == null) return;
        maxSegments = Mathf.Clamp(maxSegments, 64, 8192);
        vertices = new Vector3[maxSegments * 4];
        colors = new Color32[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var indices = new int[maxSegments * 6];
        for (int i = 0; i < maxSegments; i++)
        {
            int v = i * 4, t = i * 6;
            uv[v] = new Vector2(0, 0); uv[v + 1] = new Vector2(1, 0);
            uv[v + 2] = new Vector2(0, 1); uv[v + 3] = new Vector2(1, 1);
            indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
            indices[t + 3] = v + 1; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
        }
        mesh = new Mesh { name = "Vehicle skidmark ring buffer" };
        mesh.MarkDynamic();
        mesh.vertices = vertices; mesh.colors32 = colors; mesh.uv = uv; mesh.triangles = indices;
        marksObject = new GameObject("Skidmarks - " + name, typeof(MeshFilter), typeof(MeshRenderer));
        SceneManager.MoveGameObjectToScene(marksObject, gameObject.scene);
        marksObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = marksObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    public bool AddSegment(Vector3 from, Vector3 to, Vector3 fromNormal, Vector3 toNormal,
        float fromIntensity, float toIntensity, float width)
    {
        if (mesh == null || !isActiveAndEnabled) return false;
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance < minSegmentLength || distance > maxSegmentLength) return false;
        Vector3 sideFrom = Vector3.Cross(fromNormal, direction).normalized * (width * .5f);
        Vector3 sideTo = Vector3.Cross(toNormal, direction).normalized * (width * .5f);
        from += fromNormal * groundOffset; to += toNormal * groundOffset;
        int v = nextSegment * 4;
        vertices[v] = from - sideFrom; vertices[v + 1] = from + sideFrom;
        vertices[v + 2] = to - sideTo; vertices[v + 3] = to + sideTo;
        var startColor = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(fromIntensity) * 255));
        var endColor = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(toIntensity) * 255));
        colors[v] = colors[v + 1] = startColor;
        colors[v + 2] = colors[v + 3] = endColor;
        nextSegment = (nextSegment + 1) % maxSegments;
        dirty = true;
        return true;
    }

    void LateUpdate()
    {
        if (!dirty || mesh == null) return;
        mesh.vertices = vertices; mesh.colors32 = colors;
        mesh.RecalculateBounds();
        dirty = false;
    }

    public void Clear()
    {
        if (vertices == null) return;
        System.Array.Clear(vertices, 0, vertices.Length);
        System.Array.Clear(colors, 0, colors.Length);
        nextSegment = 0; dirty = true;
    }

    void OnDestroy()
    {
        if (marksObject != null) Destroy(marksObject);
        if (mesh != null) Destroy(mesh);
    }
}

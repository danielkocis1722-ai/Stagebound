using UnityEngine;
using UnityEngine.UI;

// Texture-free UI ring. Geometry changes only when its parameters/color change.
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ClusterArc : MaskableGraphic
{
    public float radius = 110f, thickness = 2f, startDegrees = 225f, sweepDegrees = -270f;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(sweepDegrees) / 3f));
        for (int i = 0; i < segments; i++)
        {
            float a = (startDegrees + sweepDegrees * i / segments) * Mathf.Deg2Rad;
            float b = (startDegrees + sweepDegrees * (i + 1) / segments) * Mathf.Deg2Rad;
            int n = vh.currentVertCount;
            Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), q = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            vh.AddVert(p * radius, color, Vector2.zero);
            vh.AddVert(p * (radius - thickness), color, Vector2.zero);
            vh.AddVert(q * (radius - thickness), color, Vector2.zero);
            vh.AddVert(q * radius, color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}

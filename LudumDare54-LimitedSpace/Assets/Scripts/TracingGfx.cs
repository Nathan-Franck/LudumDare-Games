using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
public record TracingSettings
{
    public float zOffset = -10f;
    public float targetPadding = 0.1f;
    public float lineThickness = 0.1f;
    public Color lineColor = Color.white;
}
public class TracingGfx
{
    public record Trace(Transform Target, Bounds From);
    public List<Trace> traces = new List<Trace>();
    private Dictionary<Transform, LineRenderer> lineRenderers = new Dictionary<Transform, LineRenderer>();
    private HashSet<Transform> unusedRenderers = new HashSet<Transform>();
    public void Update(TracingSettings settings)
    {
        unusedRenderers.Clear();
        unusedRenderers.AddRange(lineRenderers.Keys);
        foreach (var trace in traces)
        {
            if (!lineRenderers.ContainsKey(trace.Target))
            {
                var go = new GameObject("Trace " + trace.Target.name);
                go.transform.position = Vector3.zero;

                var lineRenderer = go.AddComponent<LineRenderer>();
                lineRenderer.startWidth = 0.1f;
                lineRenderer.endWidth = 0.1f;
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startColor = Color.red;
                lineRenderer.endColor = Color.red;
                lineRenderers.Add(trace.Target, lineRenderer);
            }
            unusedRenderers.Remove(trace.Target);

            var targetbounds = BoundsDictionary.Calculate(trace.Target.gameObject);

            var signX = Mathf.Sign(trace.From.center.x - targetbounds.center.x);
            var signY = Mathf.Sign(trace.From.center.y - targetbounds.center.y);

            var line = lineRenderers[trace.Target];
            line.startColor = settings.lineColor;
            line.endColor = settings.lineColor;
            line.widthMultiplier = settings.lineThickness;
            line.positionCount = 4;
            line.SetPosition(0, targetbounds.center + settings.zOffset * Vector3.forward - signX * targetbounds.extents.x * Vector3.right + signY * (targetbounds.extents.y + settings.targetPadding) * Vector3.up);
            line.SetPosition(1, targetbounds.center + settings.zOffset * Vector3.forward + signX * targetbounds.extents.x * Vector3.right + signY * (targetbounds.extents.y + settings.targetPadding) * Vector3.up);
            line.SetPosition(2, trace.From.center + settings.zOffset * Vector3.forward - signX * trace.From.extents.x * Vector3.right - signY * (trace.From.extents.y + settings.targetPadding) * Vector3.up);
            line.SetPosition(3, trace.From.center  + settings.zOffset * Vector3.forward+ signX * trace.From.extents.x * Vector3.right - signY * (trace.From.extents.y + settings.targetPadding) * Vector3.up);
        }
        foreach (var leftover in unusedRenderers)
        {
            Object.Destroy(lineRenderers[leftover]);
            lineRenderers.Remove(leftover);
        }
    }
}
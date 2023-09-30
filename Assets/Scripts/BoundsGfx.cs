using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
public record BoundsSettings
{
    public float zOffset = -10f;
    public float targetPadding = 0.1f;
    public float lineThickness = 0.1f;
    public Color lineColor = Color.white;
}
public class BoundsGfx
{
    public List<Bounds> boundses = new List<Bounds>();
    private List<LineRenderer> lineRenderers = new List<LineRenderer>();
    private HashSet<LineRenderer> unusedRenderers = new HashSet<LineRenderer>();
    public void Update(BoundsSettings settings)
    {
        unusedRenderers.Clear();
        unusedRenderers.AddRange(lineRenderers);
        foreach (var bounds in boundses)
        {
            LineRenderer lineRenderer;
            if (!unusedRenderers.Any())
            {
                var go = new GameObject("Bounds");
                go.transform.position = Vector3.zero;

                lineRenderer = go.AddComponent<LineRenderer>();
                lineRenderer.startWidth = 0.1f;
                lineRenderer.endWidth = 0.1f;
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startColor = Color.red;
                lineRenderer.endColor = Color.red;
                lineRenderers.Add(lineRenderer);
            }
            else {
                lineRenderer = unusedRenderers.First();
                unusedRenderers.Remove(lineRenderer);
            }

            var line = lineRenderer;
            line.startColor = settings.lineColor;
            line.endColor = settings.lineColor;
            line.widthMultiplier = settings.lineThickness;
            line.positionCount = 5;
            line.SetPosition(0, bounds.center + settings.zOffset * Vector3.forward - bounds.extents.x * Vector3.right + bounds.extents.y * Vector3.up);
            line.SetPosition(1, bounds.center + settings.zOffset * Vector3.forward + bounds.extents.x * Vector3.right + bounds.extents.y * Vector3.up);
            line.SetPosition(2, bounds.center + settings.zOffset * Vector3.forward + bounds.extents.x * Vector3.right - bounds.extents.y * Vector3.up);
            line.SetPosition(3, bounds.center + settings.zOffset * Vector3.forward - bounds.extents.x * Vector3.right - bounds.extents.y * Vector3.up);
            line.SetPosition(4, bounds.center + settings.zOffset * Vector3.forward - bounds.extents.x * Vector3.right + bounds.extents.y * Vector3.up);
        }
        foreach (var leftover in unusedRenderers)
        {
            Object.Destroy(leftover);
            lineRenderers.Remove(leftover);
        }
    }
}
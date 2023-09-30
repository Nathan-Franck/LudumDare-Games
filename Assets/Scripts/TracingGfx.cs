using System.Collections.Generic;

public class TracingGfx
{
    public record struct Trace(Transform target, Transform label);
    public List<Trace> traces = new List<Trace>();
    public Dictionary<Trace, LineRenderer> lineRenderers = new Dictionary<Trace, LineRenderer>();

    public void Update()
    {
    }
}
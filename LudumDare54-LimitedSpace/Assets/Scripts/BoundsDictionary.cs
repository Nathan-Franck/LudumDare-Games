using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class BoundsDictionary
{
    static Dictionary<GameObject, Bounds> boundsDictionary = new Dictionary<GameObject, Bounds>();
    public static Bounds Get(GameObject go)
    {
        if (!boundsDictionary.ContainsKey(go))
        {
            go.transform.position = Vector3.zero;
            var bounds = Calculate(go);
            boundsDictionary[go] = bounds;
        }
        return boundsDictionary[go];
    }
    public static Bounds Calculate(GameObject go)
    {
        return go.GetComponents<Renderer>().Select(r => r.bounds)
            .Concat(go.GetComponents<SpriteRenderer>().Select(r => r.bounds))
            .Aggregate((a, b) => { a.Encapsulate(b); return a; });
    }
}
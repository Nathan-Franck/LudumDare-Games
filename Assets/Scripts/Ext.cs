using System.Collections.Generic;
using UnityEngine;

public static class Ext
{
    public static T Random<T>(this IEnumerable<T> enumerable)
    {
        var list = new List<T>(enumerable);
        return list[UnityEngine.Random.Range(0, list.Count)];
    }

    public static Vector3 Quantize(this Vector3 v, float quant)
    {
        return new Vector3(
            Mathf.Round(v.x / quant) * quant,
            Mathf.Round(v.y / quant) * quant,
            Mathf.Round(v.z / quant) * quant
        );
    }
}
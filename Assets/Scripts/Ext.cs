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

    public static Vector3 QuantizeFloor(this Vector3 v, float quant)
    {
        return new Vector3(
            Mathf.Floor(v.x / quant) * quant,
            Mathf.Floor(v.y / quant) * quant,
            Mathf.Floor(v.z / quant) * quant
        );
    }

    public static Vector3 QuantizeCeil(this Vector3 v, float quant)
    {
        return new Vector3(
            Mathf.Ceil(v.x / quant) * quant,
            Mathf.Ceil(v.y / quant) * quant,
            Mathf.Ceil(v.z / quant) * quant
        );
    }

    public static Bounds Quantize(this Bounds b, float quant)
    {
        var min = b.min.QuantizeFloor(quant);
        var max = b.max.QuantizeCeil(quant);
        return new Bounds((min + max) / 2, max - min);
    }
}
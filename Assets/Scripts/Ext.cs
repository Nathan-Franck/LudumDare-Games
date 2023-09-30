using System.Collections.Generic;

public static class Ext
{
    public static T Random<T>(this IEnumerable<T> enumerable)
    {
        var list = new List<T>(enumerable);
        return list[UnityEngine.Random.Range(0, list.Count)];
    }
}
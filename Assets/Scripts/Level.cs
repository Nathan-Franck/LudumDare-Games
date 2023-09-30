using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Level : MonoBehaviour
{
    public Transform[] emptySpaces;
    public Vector3[] carPath;
    
    public float fitTolerance = 0.25f;

    public static Vector3 ClosestPointOnLineSegment(Vector3 start, Vector3 end, Vector3 point)
    {
        Vector3 line = end - start;
        float lineLength = line.magnitude;
        line.Normalize();

        Vector3 pointToStart = point - start;

        float dot = Vector3.Dot(pointToStart, line);

        if (dot <= 0)
            return start;

        if (dot >= lineLength)
            return end;

        return start + line * dot;
    }

}
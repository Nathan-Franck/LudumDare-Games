using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Level : MonoBehaviour
{

    [System.Serializable]
    class SpaceSettings
    {
        public Transform space;
        public float designatedParkTime = 10;
        public float carSpeed = 4;
    }
    public string LevelName = "Test";
    public [] emptySpaces;
    public Vector3[] carPath;
    public GameObject carPrefab;
    public Bounds carBounds;
    public List<Transform> activeCars;
    public List<Transform> parkedCars;
    public float timeToComplete = 30;
    public float fitTolerance = 0.25f;
    public float carSpeedScale = 1.0f;

    public IEnumerator StartLevel()
    {
        // Cleanup previous state
        foreach (var car in activeCars)
        {
            Destroy(car);
        }
        activeCars.Clear();
        foreach (var car in parkedCars)
        {
            Destroy(car.gameObject);
        }
        parkedCars.Clear();
        // Calculate the bounds of the car prefab
        carBounds = carPrefab.GetComponents<Renderer>().Select(r => r.bounds)
            .Concat(carPrefab.GetComponents<SpriteRenderer>().Select(r => r.bounds))
            .Aggregate((a, b) => { a.Encapsulate(b); return a; });
        var carFront = carBounds.min.x - carPrefab.transform.position.x;
        var carBack = carBounds.max.x - carPrefab.transform.position.x;
        // Other stuff.
        var totalLength = TotalLength();
        // Instantiate one car per-empty space
        for (int i = 0; i < emptySpaces.Length; i++)
        {
            var car = Instantiate(carPrefab, emptySpaces[i].position, Quaternion.identity);
            activeCars.Add(car.transform);
        }
        var solved = false;
        while (!solved)
        {
            // just wait for any key, then we're solved
            if (Input.anyKeyDown)
            {
                solved = true;
            }
            yield return null;
        }
    }

    // Gather all distances between points
    public float TotalLength()
    {
        var totalDistance = 0.0f;
        for (int i = 0; i < carPath.Length; i++)
        {
            totalDistance += Vector3.Distance(carPath[i], carPath[(i + 1) % carPath.Length]);
        }
        return totalDistance;
    }

    public Vector3 SegmentLocation(float x)
    {
        var left = x;
        var pathIndex = 0;
        while (true)
        {
            pathIndex = (pathIndex + 1) % carPath.Length;
            var dist = Vector3.Distance(carPath[pathIndex], carPath[(pathIndex + 1) % carPath.Length]);
            if (left > dist)
                left -= dist;
            return Vector3.Lerp(carPath[pathIndex], carPath[(pathIndex + 1) % carPath.Length], left / dist);
        }
    }

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
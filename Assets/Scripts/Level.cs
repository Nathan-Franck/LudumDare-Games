using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

public class Level : MonoBehaviour
{

    [System.Serializable]
    public class SpaceSettings
    {
        public Transform space;
        public float designatedParkTime = 10;
        public float carSpeed = 4;
    }
    public string LevelName = "Test";
    public SpaceSettings[] emptySpaces;
    public Vector3[] carPath;
    public GameObject carPrefab;
    [System.Serializable]
    public record ActiveCar(Transform Transform, float Progress);
    public List<ActiveCar> activeCars;
    public List<Transform> parkedCars;
    public float timeToComplete = 30;
    public float fitTolerance = 0.25f;
    public float carSpeedScale = 1.0f;

    public IEnumerator StartLevel()
    {
        // Cleanup previous state
        foreach (var car in activeCars)
        {
            Destroy(car.Transform.gameObject);
        }
        activeCars.Clear();
        foreach (var car in parkedCars)
        {
            Destroy(car.gameObject);
        }
        parkedCars.Clear();
        // Calculate the bounds of the car prefab
        var carBounds = carPrefab.GetComponents<Renderer>().Select(r => r.bounds)
            .Concat(carPrefab.GetComponents<SpriteRenderer>().Select(r => r.bounds))
            .Aggregate((a, b) => { a.Encapsulate(b); return a; });
        var carFront = carBounds.min.x - carPrefab.transform.position.x;
        var carBack = carBounds.max.x - carPrefab.transform.position.x;
        // Create le cars
        foreach (var initialCarProgress in InitialCarProgresses())
        {
            var car = Instantiate(carPrefab, Vector3.zero, Quaternion.identity);
            car.transform.parent = transform;
            car.transform.localPosition = LocationOnPath(initialCarProgress);
            activeCars.Add(new(car.transform, initialCarProgress));
        }
        var solved = false;
        while (!solved)
        {
            var totalLength = TotalLength();
            // Update all active cars
            for (int i = 0; i < activeCars.Count; i++)
            {
                var car = activeCars[i];
                var newProgess = Mathf.Repeat(car.Progress + Time.deltaTime * carSpeedScale * emptySpaces[i].carSpeed, totalLength);
                car.Transform.localPosition = LocationOnPath(newProgess);
                var frontProgress = newProgess + carFront;
                var backProgress = newProgess + carBack;
                car.Transform.localRotation = Quaternion.FromToRotation(Vector3.up, LocationOnPath(backProgress) - LocationOnPath(frontProgress));
                activeCars[i] = new(car.Transform, newProgess);
            }
            // just wait for any key, then we're solved
            if (Input.anyKeyDown)
            {
                solved = true;
                // Force-park all cars
                for (int i = 0; i < activeCars.Count; i++)
                {
                    var car = activeCars[i];
                    car.Transform.position = emptySpaces[i].space.position;
                    parkedCars.Add(car.Transform);
                }
                activeCars.Clear();
            }
            yield return null;
        }
    }

    public float[] InitialCarProgresses()
    {
        var carProgresses = new float[emptySpaces.Length];
        var progresses = SegmentProgresses();
        for (int i = 0; i < emptySpaces.Length; i++)
        {
            var emptySpace = emptySpaces[i];
            var sample = ClosestPointOnPath(emptySpace.space.position);
            var parkProgress = progresses[sample.PathIndex] + sample.Progress;
            var carProgress = parkProgress - emptySpace.designatedParkTime * emptySpace.carSpeed * carSpeedScale;
            carProgresses[i] = carProgress;
        }
        return carProgresses;
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

    // Draw path in gui gizmos
    private void OnDrawGizmos()
    {
        if (carPath == null)
            return;
        Gizmos.color = Color.red;
        for (int i = 0; i < carPath.Length; i++)
        {
            Gizmos.DrawWireSphere(transform.TransformPoint(carPath[i]), 0.1f);
            Gizmos.DrawLine(transform.TransformPoint(carPath[i]), transform.TransformPoint(carPath[(i + 1) % carPath.Length]));
        }
        Gizmos.color = Color.green;
        for (int i = 0; i < emptySpaces.Length; i++)
        {
            var closestPoint = ClosestPointOnPath(emptySpaces[i].space.position).Point;
            Gizmos.DrawWireSphere(closestPoint, fitTolerance);
        }
        foreach (var carProgress in InitialCarProgresses())
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.TransformPoint(LocationOnPath(carProgress)), 0.1f);
        }
    }

    public Vector3 LocationOnPath(float progress)
    {
        while (progress < 0)
            progress += TotalLength();
        var left = progress;
        var pathIndex = 0;
        while (true)
        {
            var dist = Vector3.Distance(carPath[pathIndex], carPath[(pathIndex + 1) % carPath.Length]);
            if (left > dist)
            {
                left -= dist;
                pathIndex = (pathIndex + 1) % carPath.Length;
            }
            else
            {
                return Vector3.Lerp(carPath[pathIndex], carPath[(pathIndex + 1) % carPath.Length], left / dist);
            }
        }
    }

    public float[] SegmentProgresses()
    {
        var progresses = new float[carPath.Length];
        var progress = 0.0f;
        progresses[0] = progress;
        for (int i = 1; i < carPath.Length; i++)
        {
            progress += Vector3.Distance(carPath[i - 1], carPath[i % carPath.Length]);
            progresses[i] = progress;
        }
        return progresses;
    }

    public record ClosestPointResult(Vector3 Point, float Distance, int PathIndex, float Progress);
    public ClosestPointResult ClosestPointOnPath(Vector3 point)
    {
        Vector3 closestPoint = Vector3.zero;
        float closestDistance = Mathf.Infinity;
        int pathIndex = 0;
        var progress = 0.0f;

        for (int j = 0; j < carPath.Length; j++)
        {
            var sample = ClosestPointOnLineSegment(transform.TransformPoint(carPath[j]), transform.TransformPoint(carPath[(j + 1) % carPath.Length]), point);

            float distance = Vector3.Distance(point, sample.Point);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = sample.Point;
                progress = sample.Progress;
                pathIndex = j;
            }
        }
        return new ClosestPointResult(closestPoint, closestDistance, pathIndex, progress);
    }

    public record ClosestSegmentPointResult(Vector3 Point, float Progress);
    public ClosestSegmentPointResult ClosestPointOnLineSegment(Vector3 start, Vector3 end, Vector3 point)
    {
        Vector3 line = end - start;
        float lineLength = line.magnitude;
        line.Normalize();

        Vector3 pointToStart = point - start;

        float dot = Vector3.Dot(pointToStart, line);

        if (dot <= 0)
            return new ClosestSegmentPointResult(start, 0);

        if (dot >= lineLength)
            return new ClosestSegmentPointResult(end, lineLength);

        return new ClosestSegmentPointResult(start + line * dot, dot);
    }
}
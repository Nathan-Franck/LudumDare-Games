using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Collections.Specialized;

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
    [System.Serializable]
    public record ActiveCar(Transform Transform, float Progress, string Label);
    [Header("Settings")]
    public string LevelName = "Test";
    public float timeToComplete = 30;
    public float carSpeedScale = 1.0f;
    public LabelGfx labelGfx = new LabelGfx();
    public TracingGfx tracingGfx = new TracingGfx();
    public BoundsGfx collisionBoundsGfx = new BoundsGfx();
    public BoundsGfx dockableBoundsGfx = new BoundsGfx();

    [Header("Rigging")]
    public SpaceSettings[] emptySpaces;
    public Vector3[] carPath;
    public GameObject carPrefab;
    [Header("State")]
    public List<ActiveCar> activeCars;
    public List<Transform> parkedCars;
    public bool solved;
    public float timeLeft;
    

    public (List<List<Transform>>, List<Tuple<Transform, Transform>>) GetActionResults(Game game, float[] segmentProgresses)
    {
        var collisions = new List<List<Transform>>();
        var dockables = new List<Tuple<Transform, Transform>>();

        for (var i = 0; i < activeCars.Count; i++)
        {
            var car = activeCars[i];
            for (var j = 0; j < emptySpaces.Length; j++)
            {
                var emptySpace = emptySpaces[j];
                var sample = ClosestPointOnPath(emptySpace.space.position);
                var parkProgress = segmentProgresses[sample.PathIndex] + sample.Progress;
                if (Mathf.Abs(car.Progress - parkProgress) < game.fitTolerance)
                {
                    dockables.Add(new Tuple<Transform, Transform>(car.Transform, emptySpace.space));
                }
            }
        }

        for (var i = 0; i < activeCars.Count; i++)
        {
            var car = activeCars[i];
            var carBounds = BoundsDictionary.Calculate(car.Transform.gameObject);
            for (var j = i + 1; j < activeCars.Count; j++)
            {
                var otherCar = activeCars[j];
                var otherCarBounds = BoundsDictionary.Calculate(otherCar.Transform.gameObject);
                if (carBounds.Intersects(otherCarBounds))
                {
                    collisions.Add(new List<Transform> { car.Transform, otherCar.Transform });
                }
            }
        }

        // Any dockables that share the same space are collisions
        for (var i = 0; i < dockables.Count; i++)
        {
            var dockable = dockables[i];
            for (var j = i + 1; j < dockables.Count; j++)
            {
                var otherDockable = dockables[j];
                if (dockable.Item2 == otherDockable.Item2)
                {
                    collisions.Add(new List<Transform> { dockable.Item1, otherDockable.Item1, dockable.Item2 });
                    dockables.RemoveAt(j);
                    j--;
                }
            }
        }

        // Merge any collisions that share a car
        for (var i = 0; i < collisions.Count; i++)
        {
            var collision = collisions[i];
            for (var j = i + 1; j < collisions.Count; j++)
            {
                var otherCollision = collisions[j];
                if (collision.Intersect(otherCollision).Any())
                {
                    collision.AddRange(otherCollision);
                    collisions.RemoveAt(j);
                    j--;
                }
            }
        }

        // Any dockables that are also collisions are not dockables
        for (var i = 0; i < dockables.Count; i++)
        {
            var dockable = dockables[i];
            for (var j = 0; j < collisions.Count; j++)
            {
                var collision = collisions[j];
                if (collision.Contains(dockable.Item1) || collision.Contains(dockable.Item2))
                {
                    dockables.RemoveAt(i);
                    i--;
                    break;
                }
            }
        }

        return new (collisions, dockables);
    }

    public IEnumerator LabelCars(Game game)
    {
        var segmentProgresses = SegmentProgresses();

        labelGfx.font = game.labelFont;

        collisionBoundsGfx.boundses.Clear();
        collisionBoundsGfx.Update(game.boundsSettings);
        dockableBoundsGfx.boundses.Clear();
        dockableBoundsGfx.Update(game.boundsSettings);

        // Reveal names of cars
        {
            var startTime = Time.time;
            var bottomLeftScreen = Camera.main.ScreenToWorldPoint(new Vector3(0, 0, 0));
            while (Time.time - startTime < game.carLabelSettings.TotalTime)
            {
                labelGfx.Clear();
                tracingGfx.traces.Clear();

                var stackHeight = game.carLabelSettings.LabelVerticalCurve.Evaluate((Time.time - startTime).Quantize(game.animationCurveQuantum));
                for (int i = 0; i < activeCars.Count; i++)
                {
                    var car = activeCars[i];
                    var iconBounds = BoundsDictionary.Get(game.carLabelSettings.revealPrefab);
                    // Label le car!
                    var bounds = labelGfx.Add(new(new(game.carLabelSettings.revealPrefab, car.Label), Vector3.up * stackHeight + bottomLeftScreen + iconBounds.extents + Vector3.one * game.labelSettings.Padding));
                    stackHeight += bounds.size.y + game.labelSettings.Padding + game.traceSettings.targetPadding;
                    // Trace from label to car!
                    tracingGfx.traces.Add(new(car.Transform, bounds));
                }

                // Update labels and tracing
                labelGfx.Update(game.labelSettings);
                tracingGfx.Update(game.traceSettings with { lineThickness = game.traceSettings.lineThickness * game.carLabelSettings.LineThicknessCurve.Evaluate((Time.time - startTime).Quantize(game.animationCurveQuantum)) });

                yield return null;
            }
        }

        // Hide all
        labelGfx.Clear();
        tracingGfx.traces.Clear();
        labelGfx.Update(game.labelSettings);
        tracingGfx.Update(game.traceSettings);

        // Portray car statuses
        {
            while (!solved)
            {
                labelGfx.Clear();
                collisionBoundsGfx.boundses.Clear();
                dockableBoundsGfx.boundses.Clear();

                var (collisions, dockables) = GetActionResults(game, segmentProgresses);

                for (var collisionID = 0; collisionID < collisions.Count; collisionID++)
                {
                    var collision = collisions[collisionID];
                    var collisionBounds = collision.Select(t => BoundsDictionary.Calculate(t.gameObject)).Aggregate((a, b) => { a.Encapsulate(b); return a; }).Quantize(game.carLabelSettings.quantization);
                    var iconBounds = BoundsDictionary.Get(game.carLabelSettings.collisionPrefab);
                    collisionBounds = new Bounds(collisionBounds.center, collisionBounds.size + 2 * game.boundsSettings.targetPadding * Vector3.one);
                    var position = new Vector3(collisionBounds.min.x - iconBounds.min.x, collisionBounds.min.y - game.boundsSettings.targetPadding - iconBounds.max.y, 0);
                    labelGfx.Add(new(new(game.carLabelSettings.collisionPrefab, "COLLISION_" + (char)('A' + collisionID)), position));
                    collisionBoundsGfx.boundses.Add(collisionBounds);
                }

                for (var dockableID = 0; dockableID < dockables.Count; dockableID++)
                {
                    var dockable = dockables[dockableID];
                    var dockableBounds = BoundsDictionary.Calculate(dockable.Item1.gameObject);
                    dockableBounds.Encapsulate(BoundsDictionary.Calculate(dockable.Item2.gameObject));
                    dockableBounds = dockableBounds.Quantize(game.carLabelSettings.quantization);
                    var iconBounds = BoundsDictionary.Get(game.carLabelSettings.dockablePrefab);
                    dockableBounds = new Bounds(dockableBounds.center, dockableBounds.size + 2 * game.boundsSettings.targetPadding * Vector3.one);
                    var position = new Vector3(dockableBounds.min.x - iconBounds.min.x, dockableBounds.min.y - game.boundsSettings.targetPadding - iconBounds.max.y, 0);
                    labelGfx.Add(new(new(game.carLabelSettings.dockablePrefab, "DOCKABLE_" + (char)('A' + dockableID)), position));
                    dockableBoundsGfx.boundses.Add(dockableBounds);
                }

                labelGfx.Update(game.labelSettings);
                collisionBoundsGfx.Update(game.boundsSettings with { lineColor = Color.red });
                dockableBoundsGfx.Update(game.boundsSettings with { lineColor = Color.green });

                yield return null;
            }
        }

        // Hide all
        labelGfx.Clear();
        tracingGfx.traces.Clear();
        collisionBoundsGfx.boundses.Clear();
        dockableBoundsGfx.boundses.Clear();
        labelGfx.Update(game.labelSettings);
        tracingGfx.Update(game.traceSettings);
        collisionBoundsGfx.Update(game.boundsSettings);
        dockableBoundsGfx.Update(game.boundsSettings);
    }

    public void UpdateCar(ActiveCar car, float newProgress, float carFront, float carBack)
    {
        car.Transform.localPosition = LocationOnPath(newProgress);
        var frontProgress = newProgress + carFront;
        var backProgress = newProgress + carBack;
        car.Transform.localRotation = Quaternion.FromToRotation(Vector3.up, LocationOnPath(backProgress) - LocationOnPath(frontProgress));
    }

    public IEnumerator Timer() {

        // timeLeft = timeToComplete;
        // var timeText = 
        yield return new WaitForSeconds(1);
    }

    public IEnumerator StartLevel(Game game, int currentLevel)
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
            var carGo = Instantiate(carPrefab, Vector3.zero, Quaternion.identity);
            carGo.transform.parent = transform;
            carGo.transform.localPosition = LocationOnPath(initialCarProgress);
            string label;
            {
                var labels = game.carLabelSettings.car_names.ToList();
                foreach (var usedAlready in game.usedLabels)
                {
                    labels.Remove(usedAlready);
                }
                if (labels.Count == 0)
                {
                    labels = new List<string> { "ran_out_of_files.ohno" };
                }
                label = labels.Random();
                game.usedLabels.Add(label);
            }
            var car = new ActiveCar(carGo.transform, initialCarProgress, label);
            UpdateCar(car, initialCarProgress, carFront, carBack);
            activeCars.Add(car);
        }

        StartCoroutine(game.ShowMessageToUser($"Level {currentLevel + 1} - {LevelName}"));

        solved = false;

        yield return new WaitForSeconds(0.5f);

        StartCoroutine(LabelCars(game));

        yield return new WaitForSeconds(0.75f);

        StartCoroutine(Timer());

        yield return new WaitForSeconds(0.75f);

        // Level loop
        while (!solved)
        {
            labelGfx.Clear();
            tracingGfx.traces.Clear();
            var totalLength = TotalLength();

            // Update all active cars
            for (int i = 0; i < activeCars.Count; i++)
            {
                var car = activeCars[i];
                var progress = Mathf.Repeat(car.Progress + Time.deltaTime * carSpeedScale * emptySpaces[i].carSpeed, totalLength);
                UpdateCar(car, progress, carFront, carBack);
                activeCars[i] = car with { Progress = progress };
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

        yield return StartCoroutine(game.ShowMessageToUser($"Done Deal!"));
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
            Gizmos.DrawWireSphere(closestPoint, 0.25f);
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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Level))]
public class LevelEditor : Editor
{
    private void OnSceneGUI()
    {
        var level = target as Level;

        if (level == null || level.carPath == null)
            return;

        Handles.color = Color.red;

        for (int i = 0; i < level.carPath.Length; i++)
        {
            Vector3 newPos = Handles.PositionHandle(level.transform.TransformPoint(level.carPath[i]), Quaternion.identity);
            
            if (level.carPath[i] != newPos)
            {
                // Ensure the changes are registered as undoable actions
                Undo.RecordObject(level, "Move Car Path Point");
                level.carPath[i] = level.transform.InverseTransformPoint(newPos);
            }
        }

        // Render the path
        for (int i = 0; i < level.carPath.Length; i++)
        {
            Handles.DrawLine(level.transform.TransformPoint(level.carPath[i]), level.transform.TransformPoint(level.carPath[(i + 1)%level.carPath.Length]));
        }

        // For each empty space, check to see which path is closest and draw a point on that path
        for (int i = 0; i < level.emptySpaces.Length; i++)
        {
            Vector3 closestPoint = Vector3.zero;
            float closestDistance = Mathf.Infinity;

            for (int j = 0; j < level.carPath.Length; j++)
            {
                Vector3 point = Level.ClosestPointOnLineSegment(level.transform.TransformPoint(level.carPath[j]), level.transform.TransformPoint(level.carPath[(j + 1)%level.carPath.Length]), level.emptySpaces[i].position);

                float distance = Vector3.Distance(level.emptySpaces[i].position, point);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPoint = point;
                }
            }

            Handles.DrawWireDisc(closestPoint, Vector3.forward, level.fitTolerance);
        }
    }
}
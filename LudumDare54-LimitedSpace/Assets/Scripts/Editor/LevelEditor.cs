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
    }
}
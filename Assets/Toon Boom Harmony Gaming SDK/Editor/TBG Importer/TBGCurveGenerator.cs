#if ENABLE_UNITY_2D_ANIMATION && ENABLE_UNITY_COLLECTIONS

using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine.Profiling;

namespace ToonBoom.TBGImporter
{
    public enum InterpolationType
    {
        AutoDetect,
        Constant,
        Auto,
    }
    public class TBGCurveGenerator
    {
        public bool Stepped;
        public int AnimationLength;
        public float Framerate;
        public struct CurveInput
        {
            public string info;
            public string node;
            public string attribute;
            public float value;
            public InterpolationType interpolation;
            public IEnumerable<TimedValuePoint> timedValuePoints;
        }
        public QuantizedCurve FromTimedValues(CurveInput input, ValueMap valueMap)
        {
            try
            {
                Profiler.BeginSample("FromTimedValues");

                // Constant curve.
                if (input.timedValuePoints == null)
                {
                    return new QuantizedCurve
                    {
                        values = Enumerable.Repeat(valueMap(input.value), AnimationLength).ToList(),
                        interpolation = InterpolationType.Constant,
                    };
                }

                // Curve from timed value.
                var intermediatePoints = input.timedValuePoints
                    .Select(point =>
                    {
                        var constant = point.constSeg;
                        var inDiffX = (point.lx - point.x) / Framerate;
                        var inDiffY = valueMap((float)point.ly) - valueMap((float)point.y);
                        var outDiffX = (point.rx - point.x) / Framerate;
                        var outDiffY = valueMap((float)point.ry) - valueMap((float)point.y);
                        return new
                        {
                            x = (float)(point.x - 1) / Framerate,
                            y = valueMap((float)point.y),
                            inDiffX = (float)inDiffX,
                            inDiffY = (float)inDiffY,
                            outDiffX = (float)outDiffX,
                            outDiffY = (float)outDiffY,
                            constant,
                        };
                    })
                    .ToArray();
                var curveKeys = intermediatePoints
                    .Select((point, pointIndex) =>
                    {
                        var previousPoint = pointIndex <= 0 ? point : intermediatePoints[pointIndex - 1];
                        var nextPoint = pointIndex >= intermediatePoints.Length - 1 ? point : intermediatePoints[pointIndex + 1];
                        return new Keyframe(
                            time: point.x,
                            value: point.y,
                            inTangent: point.inDiffY / point.inDiffX,
                            outTangent: point.outDiffY / point.outDiffX,
                            inWeight: -point.inDiffX / (point.x - previousPoint.x),
                            outWeight: point.outDiffX / (nextPoint.x - point.x));
                    })
                    .ToArray();
                var curve = new AnimationCurve(curveKeys);

                if (curve.length == 0)
                {
                    Debug.Log("Invalid curve: " + input.info);
                    return new QuantizedCurve
                    {
                        values = Enumerable.Repeat(valueMap(input.value), AnimationLength).ToList(),
                        interpolation = InterpolationType.Constant,
                    };
                }
                // Set tangents in seperate pass.
                if (input.interpolation == InterpolationType.Constant)
                {
                    for (var i = 0; i < intermediatePoints.Length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                    }
                }
                else if (input.interpolation == InterpolationType.Auto)
                {
                    for (var i = 0; i < intermediatePoints.Length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
                    }
                }
                else
                {
                    for (var i = 0; i < intermediatePoints.Length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, intermediatePoints[i].inDiffX == 0
                            ? AnimationUtility.TangentMode.Linear
                            : AnimationUtility.TangentMode.Free);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, intermediatePoints[i].constant
                            ? AnimationUtility.TangentMode.Constant
                            : intermediatePoints[i].outDiffX == 0
                                ? AnimationUtility.TangentMode.Linear
                                : AnimationUtility.TangentMode.Free);
                    }
                }
                // Resample curve to fit within animation time and allow for removed interpolation and easy per-frame blending.
                var quantizedCurve = new QuantizedCurve
                {
                    values = Enumerable.Range(0, AnimationLength)
                        .Select(index => curve.Evaluate((float)index / Framerate))
                        .ToList(),
                    interpolation = InterpolationType.AutoDetect,
                };
                // Ensure that original keyframe values are preserved.
                foreach (var originalKeyframes in input.timedValuePoints)
                {
                    if (originalKeyframes.x > 0 && originalKeyframes.x <= AnimationLength)
                    {
                        quantizedCurve.values[(int)originalKeyframes.x - 1] = valueMap((float)originalKeyframes.y);
                    }
                }
                return quantizedCurve;
            }
            catch (Exception e)
            {
                Debug.Log($"Could not set curve on clip for curve {input.info}");
                Debug.LogException(e);
                return null;
            }
            finally
            {
                Profiler.EndSample();
            }
        }
    }
}

#endif

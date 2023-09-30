#if ENABLE_UNITY_2D_ANIMATION && ENABLE_UNITY_COLLECTIONS

using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System;
using UnityEngine.Profiling;

namespace ToonBoom.TBGImporter
{
    public struct RegisteredCurvesKey
    {
        public string TransformPath;
        public string PropertyName;
    }
    public class QuantizedCurve
    {
        public List<float> values;
        public InterpolationType interpolation;
    }
    public class TBGClipBuilderSettings
    {
        public bool Stepped;
        public string Name;
        public float Framerate;
        public SkeletonSettings Skeleton;
        public GameObject RootGameObject;
        public ILookup<string, int> NodeNameToIDs;
        public ILookup<int, GameObject> NodeToInstantiated;
        public AnimationSettings Animation;
        public Dictionary<int, string> NodeIDToName;
        public ILookup<int, int> OutToIn;
        public DrawingAnimationSettings DrawingAnimation;
        public Dictionary<RegisteredCurvesKey, QuantizedCurve> RegisteredCurvesLookup = new Dictionary<RegisteredCurvesKey, QuantizedCurve>();
        public Dictionary<string, ObjectReferenceKeyframe[]> RegisteredSpriteCurvesLookup = new Dictionary<string, ObjectReferenceKeyframe[]>();
        private int? _animationLength;
        public int animationLength
        {
            get
            {
                if (_animationLength != null)
                    return (int)_animationLength;
                try
                {
                    _animationLength = DrawingAnimation.drawings
                        .Select(drawing => drawing.Value
                            .Select(drw => drw.frame + drw.repeat)
                            .DefaultIfEmpty(0)
                            .Max())
                        .DefaultIfEmpty(0)
                        .Max() - 1;
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    _animationLength = 0;
                }
                return (int)_animationLength;
            }
        }

        private AnimationClip _clip;
        public AnimationClip clip
        {
            get
            {
                if (_clip != null)
                    return _clip;
                _clip = new AnimationClip()
                {
                    name = Name,
                    frameRate = Framerate,
                };
                AnimationUtility.SetAnimationClipSettings(_clip, new AnimationClipSettings
                {
                    loopTime = true,
                });
                return _clip;
            }
            set
            {
                _clip = value;
            }
        }
    }


    public class TBGClipBuilder
    {
        public TBGClipBuilderSettings Settings;
        public Dictionary<string, string> AttributeToProperty;
        public HashSet<string> AttributesToSplit3D;
        public Dictionary<string, NodeToValueMap> AttributeToNodeToValueMap;
        public Dictionary<string, AdvancedNodeMapping[]> AttributeToAdvancedNodeMappings;

        public delegate OffsetRetriever NodeToOffsetRetriever(string node);

        public ValueMap GetValueMap(string nodeName, string attribute, string property, AdvancedNodeMapping advancedNodeMapping)
        {
            return AttributeToNodeToValueMap != null && AttributeToNodeToValueMap.TryGetValue(attribute, out var nodeToValueMap)
                ? nodeToValueMap(nodeName)
                : advancedNodeMapping != null && advancedNodeMapping.propertyToNodeValueTransform != null
                    ? advancedNodeMapping.propertyToNodeValueTransform[property].nodeToValueMap(nodeName)
                    : value => value;
        }
        public TBGClipBuilder WithTimedValueCurves()
        {
            Profiler.BeginSample("WithTimedValueCurves");
            var curveGenerator = new TBGCurveGenerator
            {
                AnimationLength = Settings.animationLength,
                Framerate = Settings.Framerate,
                Stepped = Settings.Stepped,
            };
            foreach (var attrlink in Settings.Animation.attrlinks)
            {
                Profiler.BeginSample("Retrieve Curve Inputs");

                IEnumerable<TBGCurveGenerator.CurveInput> curveInputs;
                if (AttributesToSplit3D.Contains(attrlink.attr))
                {
                    curveInputs = new string[] { "x", "y", "z" }
                        .Select(subAttribute =>
                        {
                            var attribute = $"{attrlink.attr}.{subAttribute}";
                            return new TBGCurveGenerator.CurveInput
                            {
                                info = $"{attrlink.node}.{attribute}",
                                node = attrlink.node,
                                attribute = attribute,
                                interpolation = InterpolationType.Constant,
                                timedValuePoints = Settings.Animation.timedvalues[attrlink.timedvalue]
                                    .First().points
                                    .Select(point =>
                                    {
                                        var time = point.lockedInTime != null
                                            ? (double)point.lockedInTime
                                            : point.start != null
                                            ? (double)point.start : 0.0;
                                        return subAttribute switch
                                        {
                                            "x" => new TimedValuePoint
                                            {
                                                x = time,
                                                lx = time,
                                                rx = time,
                                                y = point.x,
                                                ly = point.x,
                                                ry = point.x,
                                                constSeg = true,
                                            },
                                            "y" => new TimedValuePoint
                                            {
                                                x = time,
                                                lx = time,
                                                rx = time,
                                                y = point.y,
                                                ly = point.y,
                                                ry = point.y,
                                                constSeg = true,
                                            },
                                            _ => new TimedValuePoint
                                            {
                                                x = time,
                                                lx = time,
                                                rx = time,
                                                y = point.z,
                                                ly = point.z,
                                                ry = point.z,
                                                constSeg = true,
                                            },
                                        };
                                    }),
                            };
                        });
                }
                else
                {
                    curveInputs = new TBGCurveGenerator.CurveInput[] { new TBGCurveGenerator.CurveInput {
                        info = $"{attrlink.node}.{attrlink.attr}",
                        node = attrlink.node,
                        attribute = attrlink.attr,
                        value = (float)attrlink.value,
                        interpolation = attrlink.timedvalue == null || Settings.Animation.timedvalues[attrlink.timedvalue].First().tag == "bezier"
                            ? InterpolationType.AutoDetect
                            : InterpolationType.Constant,
                        timedValuePoints = attrlink.timedvalue == null
                            ? null
                            : Settings.Animation.timedvalues[attrlink.timedvalue].First().points,
                    } };
                }
                Profiler.EndSample();

                foreach (var curveInput in curveInputs)
                {
                    if (AttributeToAdvancedNodeMappings == null
                        || !AttributeToAdvancedNodeMappings.TryGetValue(curveInput.attribute, out var advancedNodeMappings)
                        || advancedNodeMappings.Length == 0)
                    {
                        advancedNodeMappings = new AdvancedNodeMapping[] { null };
                    }

                    foreach (var advancedNodeMapping in advancedNodeMappings)
                    {
                        var properties = advancedNodeMapping != null && advancedNodeMapping.propertyToNodeValueTransform != null
                            ? (IEnumerable<string>)advancedNodeMapping.propertyToNodeValueTransform.Keys
                            : AttributeToProperty != null && AttributeToProperty.TryGetValue(attrlink.attr, out var propertyResult)
                                ? new string[] { propertyResult }
                                : new string[] { };
                        foreach (var property in properties)
                        {

                            Profiler.BeginSample("Retrieve Instantiated");

                            IEnumerable<NodeInstance> instantiated;
                            if (!(advancedNodeMapping != null
                                && advancedNodeMapping.nodeToInstance != null
                                && (instantiated = advancedNodeMapping.nodeToInstance(curveInput.node)).Any()))
                            {
                                instantiated = Settings.NodeNameToIDs[curveInput.node]
                                    .SelectMany(nodeID => Settings.NodeToInstantiated[nodeID]
                                        .Select(instance => new NodeInstance { name = Settings.NodeIDToName[nodeID], transform = instance.transform }));
                            }

                            Profiler.EndSample();

                            foreach (var instantiatedEntry in instantiated)
                            {
                                var curve = curveGenerator.FromTimedValues(curveInput, GetValueMap(instantiatedEntry.name, curveInput.attribute, property, advancedNodeMapping));

                                var blendFunction = advancedNodeMapping?.propertyToNodeValueTransform?[property]?.blendFunction;
                                if (blendFunction != null) BlendCurve(blendFunction, property, curve, instantiatedEntry.transform, Settings.clip.frameRate);

                                var transformPath = AnimationUtility.CalculateTransformPath(instantiatedEntry.transform, Settings.RootGameObject.transform);
                                RegisterCurve(transformPath, property, curve);
                            }
                        }
                    }
                }
            }
            Profiler.EndSample();
            return this;
        }

        private void RegisterCurve(string transformPath, string property, QuantizedCurve curve)
        {
            Settings.RegisteredCurvesLookup[new RegisteredCurvesKey { TransformPath = transformPath, PropertyName = property }] = curve;
        }

        private void BlendCurve(BlendFunction blendFunction, string propertyName, QuantizedCurve curve, Transform transform, float frameRate)
        {
            Profiler.BeginSample("BlendCurve");
            try
            {
                var firstTransformPath = AnimationUtility.CalculateTransformPath(transform, Settings.RootGameObject.transform);

                if (!Settings.RegisteredCurvesLookup.TryGetValue(new RegisteredCurvesKey { TransformPath = firstTransformPath, PropertyName = propertyName }, out var existingCurve))
                    return;

                if (existingCurve == null)
                    return;

                // Apply blend function to existing curve
                var existingCurveLength = existingCurve.values.Count;
                var quantizedListLength = curve.values.Count;
                for (int i = 0; i < quantizedListLength; i++)
                {
                    var existingCurveValue = existingCurve.values[i];
                    var quantizedValue = curve.values[i];
                    var blendedValue = blendFunction(existingCurveValue, quantizedValue);
                    curve.values[i] = blendedValue;
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        public TBGClipBuilder ApplyRegisteredCurvesToClip()
        {
            Profiler.BeginSample("ApplyRegisteredCurvesToClip");
            foreach (var addedCurves in Settings.RegisteredCurvesLookup)
            {
                var propertyName = addedCurves.Key.PropertyName;
                var transformPath = addedCurves.Key.TransformPath;
                var curve = addedCurves.Value;
                var tangentMode = Settings.Stepped || curve.interpolation == InterpolationType.Constant
                    ? AnimationUtility.TangentMode.Constant
                    : AnimationUtility.TangentMode.Linear;
                var offset = tangentMode == AnimationUtility.TangentMode.Constant ? 0.0f : 0.5f;
                var keyframes = curve.values
                    .Select((value, index) => new Keyframe((index + offset) / Settings.clip.frameRate, value))
                    .Where((keyframe, index) => index == 0
                        || index == curve.values.Count - 1
                        || keyframe.value != curve.values[index - 1]
                        || keyframe.value != curve.values[index + 1]);

                var animationCurve = new AnimationCurve(keyframes.ToArray());
                var keyframeCount = animationCurve.length;
                for (int i = 0; i < keyframeCount; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(animationCurve, i, tangentMode);
                    AnimationUtility.SetKeyRightTangentMode(animationCurve, i, tangentMode);
                }
                Settings.clip.SetCurve(transformPath, typeof(Transform), propertyName, animationCurve);
            }
            Profiler.EndSample();

            Profiler.BeginSample("ApplyRegisteredSpriteCurvesToClip");
            foreach (var addedCurves in Settings.RegisteredSpriteCurvesLookup)
            {
                var binding = new EditorCurveBinding
                {
                    path = addedCurves.Key,
                    type = typeof(SpriteRenderer),
                    propertyName = "m_Sprite"
                };
                AnimationUtility.SetObjectReferenceCurve(Settings.clip, binding, addedCurves.Value);
            }
            Profiler.EndSample();
            return this;
        }

        public TBGClipBuilder WithDrawingAnimationCurves(
            Dictionary<string, Dictionary<int, SpriteRenderer>> nodeToSkinToSpriteRenderer,
            Dictionary<string, Sprite> spriteNameToSprite)
        {
            Profiler.BeginSample("WithDrawingAnimationCurves");
            foreach (var drawing in Settings.DrawingAnimation.drawings)
            {
                var node = drawing.Key;
                if (!nodeToSkinToSpriteRenderer.TryGetValue(node, out var skinToSpriteRenderer))
                    continue;
                foreach (var entry in skinToSpriteRenderer)
                {
                    var skinID = entry.Key;
                    var spriteRenderer = entry.Value;
                    var drws = drawing.Value
                        .Where(drw => drw.skinId == skinID)
                        .ToList();
                    var lastDrawingFrame = drws.LastOrDefault().frame + drws.LastOrDefault().repeat - 1;
                    var emptyKeyframes = drws
                        .Select((drw, index) =>
                        {
                            var lastFrameEnd = index == 0
                                ? -1
                                : drws[index - 1].frame + drws[index - 1].repeat - 1;
                            var currentFrameStart = drw.frame - 1;
                            return new { lastFrameEnd, currentFrameStart };
                        })
                        .Where(entry => entry.lastFrameEnd < entry.currentFrameStart)
                        .Select(entry => new ObjectReferenceKeyframe { time = entry.lastFrameEnd / (float)Settings.Framerate, value = null });
                    var finalKeyframe = lastDrawingFrame == Settings.animationLength
                        ? new ObjectReferenceKeyframe[] { new ObjectReferenceKeyframe {
                            time = (lastDrawingFrame - 1) / (float)Settings.Framerate,
                            value = spriteNameToSprite.TryGetValue(drws.LastOrDefault().name, out var sprite) ? sprite : null
                        } }
                        : new ObjectReferenceKeyframe[] { new ObjectReferenceKeyframe { time = lastDrawingFrame / (float)Settings.Framerate, value = null } };
                    var visibleKeyframes = drws
                        .Select(drw =>
                        {
                            spriteNameToSprite.TryGetValue(drw.name, out var sprite);
                            return new ObjectReferenceKeyframe { time = (drw.frame - 1) / Settings.clip.frameRate, value = sprite };
                        });
                    var keyframes =
                        emptyKeyframes
                            .Concat(finalKeyframe)
                            .Concat(visibleKeyframes)
                            .OrderBy(keyframe => keyframe.time)
                            .ToArray();
                    var transformPath = AnimationUtility.CalculateTransformPath(spriteRenderer.transform, Settings.RootGameObject.transform);
                    Settings.RegisteredSpriteCurvesLookup.Add(transformPath, keyframes.ToArray());
                }
            }
            Profiler.EndSample();
            return this;
        }

        static HashSet<string> spriteOrderAttributes = new HashSet<string> {
            "position.z",
            "offset.z",
        };

        /** <summary>
        Generate new curves for localPosition.z on every spriteRenderer
        transform to sort rendering based on Harmony rules.
        </summary> */
        public TBGClipBuilder WithSpriteOrderCurves(NodeToTransform nodeToTransform)
        {
            Profiler.BeginSample("WithSpriteOrderCurves");
            var curveGenerator = new TBGCurveGenerator
            {
                AnimationLength = Settings.animationLength,
                Framerate = Settings.Framerate,
                Stepped = Settings.Stepped,
            };
            // Collect data for sorting sprites.
            var nodeToZCurve = Settings.Animation.attrlinks
                .Where(attrlink => spriteOrderAttributes.Contains(attrlink.attr))
                .Select(attrlink =>
                {
                    var curve = curveGenerator.FromTimedValues(new TBGCurveGenerator.CurveInput
                    {
                        info = $"{attrlink.node}.position.z",
                        attribute = "position.z",
                        value = (float)attrlink.value,
                        interpolation = attrlink.timedvalue == null || Settings.Animation.timedvalues[attrlink.timedvalue].First().tag == "bezier"
                                ? InterpolationType.AutoDetect
                                : InterpolationType.Constant,
                        timedValuePoints = attrlink.timedvalue == null
                                ? null
                                : Settings.Animation.timedvalues[attrlink.timedvalue].First().points,
                    },
                        valueMap: value => value);

                    return new { attrlink.node, curve };
                })
                .ToLookup(entry => entry.node, entry => entry.curve);
            var drawingNodes = Settings.Skeleton.nodes
                .Where(node => node.tag == "read")
                .ToList();
            var frameToNodeToZKeyframe = Enumerable
                .Range(0, Settings.animationLength)
                .Select(frame =>
                {
                    var nodeInfo = drawingNodes
                        .Select((node, zIndex) =>
                        {
                            var parentID = Settings.OutToIn[node.id].First();
                            var nodeChain = new List<string>();
                            nodeChain.Add(node.name);
                            while (parentID > -1)
                            {
                                nodeChain.Add(Settings.NodeIDToName[parentID]);
                                parentID = Settings.OutToIn[parentID].First();
                            }
                            return new NodeOrderInfo
                            {
                                nodeID = node.id,
                                zIndex = zIndex,
                                zOffset = nodeChain.Aggregate(0.0f, (last, current) =>
                                {
                                    var curves = nodeToZCurve[current];
                                    return last + (!curves.Any()
                                        ? 0
                                        : curves.First().values[frame]);
                                }),
                            };
                        })
                        .ToList();
                    nodeInfo.Sort();
                    return nodeInfo
                        .Select((nodeInfo, index) => new
                        {
                            nodeID = nodeInfo.nodeID,
                            value = index * 0.001f,
                        })
                        .ToDictionary(entry => entry.nodeID, entry => entry.value);
                })
                .ToList();

            // Force sorting order on animation position.z curves.
            foreach (var node in drawingNodes)
            {
                var curve = new QuantizedCurve
                {
                    values = frameToNodeToZKeyframe
                        .Select(nodeToZKeyframe => nodeToZKeyframe[node.id])
                        .ToList(),
                    interpolation = InterpolationType.Constant,
                };
                var transforms = nodeToTransform(node.name);
                foreach (var transform in transforms != null && transforms.Any()
                    ? transforms
                    : Settings.NodeToInstantiated[node.id]
                        .Select(instantiated => instantiated.gameObject.transform)
                        .ToArray())
                {
                    var transformPath = AnimationUtility.CalculateTransformPath(transform, Settings.RootGameObject.transform);
                    RegisterCurve(transformPath, "localPosition.z", curve);

                    // Update localPosition of node to reflect sorting order in prefab preview window.
                    try
                    {
                        var localPosition = transform.localPosition;
                        var zKeyframe = frameToNodeToZKeyframe.First()[node.id];
                        localPosition.z = zKeyframe;
                        transform.localPosition = localPosition;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
            Profiler.EndSample();

            return this;
        }
        /**
        <summary>Read in the first animation and set all transforms based on values.</summary>
        */
        public static void ApplyAnimationClip(
            TBGClipBuilderSettings settings,
            Transform rootTransform,
            int frame,
            Dictionary<string, string> fieldMap,
            HashSet<Transform> transformWhitelist = null)
        {
            Profiler.BeginSample("ApplyAnimationClip");

            foreach (var curveEntry in settings.RegisteredSpriteCurvesLookup)
            {
                var nodeTransform = rootTransform;
                var transformPath = curveEntry.Key;
                try
                {
                    foreach (var child in transformPath.Split('/'))
                    {
                        nodeTransform = nodeTransform.Find(child);
                    }
                }
                catch (Exception e)
                {
                    Debug.Log($"Path could not be resolved: {transformPath}");
                    Debug.LogException(e);
                    continue;
                }
                if (nodeTransform == null)
                {
                    Debug.Log($"Last child missing from transform path: {transformPath}");
                    continue;
                }
                var renderer = nodeTransform.GetComponent<SpriteRenderer>();
                renderer.sprite = (UnityEngine.Sprite)(
                    curveEntry.Value
                        .Where(keyframe => keyframe.time <= frame / settings.Framerate)
                        .LastOrDefault()
                        .value
                    ?? curveEntry.Value.FirstOrDefault().value);
            }
            foreach (var curveEntry in settings.RegisteredCurvesLookup)
            {
                var transformPath = curveEntry.Key.TransformPath;
                var nodeTransform = rootTransform;
                try
                {
                    foreach (var child in transformPath.Split('/'))
                    {
                        nodeTransform = nodeTransform.Find(child);
                    }
                }
                catch (Exception e)
                {
                    Debug.Log($"Path could not be resolved: {transformPath}");
                    Debug.LogException(e);
                    continue;
                }
                if (nodeTransform == null)
                {
                    Debug.Log($"Last child missing from transform path: {transformPath}");
                    continue;
                }
                if (transformWhitelist != null
                    && !transformWhitelist.Contains(nodeTransform))
                    continue;
                var pathToField = curveEntry.Key.PropertyName.Split('.');
                var component = nodeTransform.GetComponent<Transform>();
                System.Object childData = curveEntry.Value.values[frame];
                var parent = new { type = typeof(Transform), data = (System.Object)component };
                foreach (var entry in pathToField.Select(fieldName =>
                {
                    var remappedFieldName = fieldMap.TryGetValue(fieldName, out var result) ? result : fieldName;
                    var fieldInfo = parent.type.GetField(remappedFieldName);
                    var propertyInfo = parent.type.GetProperty(remappedFieldName);
                    var entry = new { fieldInfo, propertyInfo, parent.data };
                    parent = fieldInfo != null
                        ? new { type = fieldInfo.FieldType, data = fieldInfo.GetValue(parent.data) }
                        : new { type = propertyInfo.PropertyType, data = propertyInfo.GetValue(parent.data) };
                    return entry;
                }).Reverse())
                {
                    if (entry.fieldInfo != null)
                        entry.fieldInfo.SetValue(entry.data, childData);
                    else
                        entry.propertyInfo.SetValue(entry.data, childData);
                    childData = entry.data;
                }
            }

            Profiler.EndSample();
        }
    }
}

#endif

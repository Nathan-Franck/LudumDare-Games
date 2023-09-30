#if ENABLE_UNITY_2D_ANIMATION && ENABLE_UNITY_COLLECTIONS

using UnityEditor;
using UnityEngine;
using System.Linq;
using UnityEditor.U2D;
using UnityEngine.Profiling;
using UnityEditor.Animations;

namespace ToonBoom.TBGImporter
{
    public class TBGPostProcessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var imported in importedAssets)
            {
                var assetImporter = AssetImporter.GetAtPath(imported);
                if (!(assetImporter is TBGImporter))
                    continue;
                var tbgImporter = (TBGImporter)assetImporter;
                var animatorController = tbgImporter.AnimatorController;
                if (animatorController != null)
                {
                    var savedClips = AssetDatabase.LoadAllAssetsAtPath(assetImporter.assetPath)
                        .Select(asset => asset as AnimationClip)
                        .Where(asset => asset != null);
                    foreach (var clip in savedClips)
                    {
                        var animatorStates = animatorController.layers
                            .SelectMany(layer => layer.stateMachine.states
                                .Select(childState => childState.state)
                                .Where(state => state.name == clip.name))
                            .ToArray();
                        if (animatorStates.Length == 0)
                        {
                            animatorStates = new AnimatorState[] { animatorController.layers[0].stateMachine.AddState(clip.name) };
                        }
                        foreach (var animatorState in animatorStates)
                        {
                            if (animatorState.motion == null)
                            {
                                // Assign new clip onto existing empty state - previous clip from old import?
                                animatorState.motion = clip;
                            }
                            else if (animatorState.motion != clip && tbgImporter.MaintainCurvesOnClonedClips)
                            {
                                // Assign new curves from generated clip onto independant (cloned) clip in project.
                                var existingClip = animatorState.motion as AnimationClip;
                                if (existingClip != null && existingClip != clip)
                                {
                                    Debug.Log($"Cloning curves from {clip.name} onto {existingClip.name}");
                                    Profiler.BeginSample("GetCurveBindings");
                                    var curveBindings = AnimationUtility.GetCurveBindings(clip);
                                    Profiler.EndSample();

                                    foreach (var curveBinding in curveBindings)
                                    {
                                        Profiler.BeginSample("GetEditorCurve");
                                        var generatedCurve = AnimationUtility.GetEditorCurve(clip, curveBinding);
                                        Profiler.EndSample();

                                        existingClip.SetCurve(curveBinding.path, curveBinding.type, curveBinding.propertyName, generatedCurve);
                                    }

                                    Profiler.BeginSample("GetObjectReferenceCurveBindings");
                                    var propertyBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                                    Profiler.EndSample();
                                    foreach (var propertyBinding in propertyBindings)
                                    {
                                        Profiler.BeginSample("GetObjectReferenceCurve");
                                        var generatedProperty = AnimationUtility.GetObjectReferenceCurve(clip, propertyBinding);
                                        Profiler.EndSample();

                                        AnimationUtility.SetObjectReferenceCurve(existingClip, propertyBinding, generatedProperty);
                                    }
                                }
                            }
                        }
                    }
                }
                var spriteAtlas = tbgImporter.SpriteAtlas;
                if (spriteAtlas != null)
                {
                    var savedSprites = AssetDatabase.LoadAllAssetsAtPath(assetImporter.assetPath)
                        .Select(asset => asset as Sprite)
                        .Where(asset => asset != null)
                        .ToArray();
                    spriteAtlas.Remove(savedSprites);
                    spriteAtlas.Add(savedSprites);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
    }
}

#endif
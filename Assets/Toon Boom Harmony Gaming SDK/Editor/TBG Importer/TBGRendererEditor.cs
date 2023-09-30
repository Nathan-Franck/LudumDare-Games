#if ENABLE_UNITY_2D_ANIMATION && ENABLE_UNITY_COLLECTIONS

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ToonBoom.TBGImporter
{
    [CustomEditor(typeof(TBGRenderer.TBGRenderer), true)]
    // [CanEditMultipleObjects()]
    public class TBGRendererEditor : Editor
    {
        public void OnEnable()
        {
        }
        public override void OnInspectorGUI()
        {
            var renderer = (TBGRenderer.TBGRenderer)target;

            renderer.Store = (ToonBoom.TBGRenderer.TBGStore)EditorGUILayout.ObjectField("Project", renderer.Store, typeof(TBGRenderer.TBGStore), true);

            var store = renderer.Store;

            EditorGUILayout.LabelField("Sprite Sheets", EditorStyles.boldLabel);

            var resolutionID = serializedObject.FindProperty(nameof(renderer.ResolutionID));
            var paletteID = serializedObject.FindProperty(nameof(renderer.PaletteID));
            var currentResolution = store.Resolutions[Math.Min(resolutionID.intValue, store.Resolutions.Length - 1)];
            var currentSpriteSheet = currentResolution.Palettes[Math.Min(paletteID.intValue, currentResolution.Palettes.Length - 1)];
            var resolutions = store.Resolutions.Select(spriteSheet => spriteSheet.ResolutionName).Distinct().ToArray();
            var palettes = store.Resolutions.Length == 0
                ? new string[] { }
                : store.Resolutions[0].Palettes.Select(spriteSheet => spriteSheet.PaletteName).Distinct().ToArray();
            resolutionID.intValue = EditorGUILayout.Popup(
                "Resolution",
                resolutions
                    .Select((name, index) => new { index, name })
                    .Where(entry => entry.name == currentResolution.ResolutionName)
                    .First().index,
                resolutions);
            paletteID.intValue = EditorGUILayout.Popup(
                "Palette",
                palettes
                    .Select((name, index) => new { index, name })
                    .Where(entry => entry.name == currentSpriteSheet.PaletteName)
                    .First().index,
                palettes);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Sprite Renderers", EditorStyles.boldLabel);
            var materials = renderer.ReadToSpriteRenderers
                .SelectMany(entry => entry.spriteRenderers
                    .Where(renderer => renderer != null)
                    .Select(renderer => renderer.sharedMaterial));
            using (new MixedValueBlock(!materials.All(material => material == materials.First())))
            {
                var result = (Material)EditorGUILayout.ObjectField("Material", materials.FirstOrDefault(), typeof(Material), false);
                if (result != materials.FirstOrDefault())
                {
                    Undo.RecordObjects(renderer.GetComponentsInChildren<SpriteRenderer>(), "Change Renderer Material");
                    renderer.SetMaterial(result);
                }
            }
            var colors = renderer.ReadToSpriteRenderers
                .SelectMany(entry => entry.spriteRenderers
                    .Where(renderer => renderer != null)
                    .Select(renderer => renderer.color));
            using (new MixedValueBlock(!colors.All(color => color == colors.First())))
            {
                var result = EditorGUILayout.ColorField("Color", colors.FirstOrDefault());
                if (result != colors.FirstOrDefault())
                {
                    Undo.RecordObjects(renderer.GetComponentsInChildren<SpriteRenderer>(), "Change Renderer Color");
                    renderer.SetColor(result);
                }
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Skins", EditorStyles.boldLabel);

            var groupToSkinIDProperty = serializedObject.FindProperty(nameof(renderer.GroupToSkinID));

            var groupToSkinID = Enumerable.Range(0, groupToSkinIDProperty.arraySize)
                .Select(groupID =>
                {
                    var property = groupToSkinIDProperty.GetArrayElementAtIndex(groupID);
                    return new
                    {
                        groupID,
                        skinID = (ushort)(property.intValue),
                        property,
                    };
                })
                .ToArray();
            var skinGroups = store.SkinGroups
                .Select(group => new
                {
                    group.GroupName,
                    SkinNames = group.SkinNames,
                })
                .ToArray();
            var globalSkins = skinGroups
                .SelectMany(group => group.SkinNames)
                .Distinct()
                .ToArray();
            if (globalSkins.Length == 0)
                globalSkins = new string[] { "none" };
            var globalSkinName = groupToSkinID
                .Aggregate(globalSkins.FirstOrDefault(), (global, entry) =>
                {
                    var skinGroupID = Math.Min(entry.groupID, skinGroups.Length - 1);
                    var skinGroup = skinGroups[skinGroupID];
                    var skinID = Math.Min(entry.skinID, skinGroup.SkinNames.Length - 1);
                    return skinGroup.SkinNames[skinID];
                });
            var globalSkinIsMixed = groupToSkinID
                .Where(entry =>
                {
                    var skinGroupID = Math.Min(entry.groupID, skinGroups.Length - 1);
                    var skinGroup = skinGroups[skinGroupID];
                    var skinID = Math.Min(entry.skinID, skinGroup.SkinNames.Length - 1);
                    return skinGroup.SkinNames[skinID] == globalSkinName;
                })
                .Count() != groupToSkinID.Length;
            if (globalSkinIsMixed)
                globalSkinName = null;
            using (new MixedValueBlock(globalSkinIsMixed))
            {
                var globalSkinID = Array.IndexOf(globalSkins, globalSkinName);
                var newGlobalSkinID = EditorGUILayout.Popup(
                    "Global Skin",
                    globalSkinID,
                   globalSkins);
                if (newGlobalSkinID != globalSkinID)
                {
                    var newGlobalSkinName = globalSkins[newGlobalSkinID];
                    foreach (var entry in groupToSkinID)
                    {
                        var newSkinID = Array.IndexOf(
                                skinGroups[Math.Min(entry.groupID, skinGroups.Length - 1)].SkinNames,
                                newGlobalSkinName);
                        if (newSkinID < 0) newSkinID = 0;
                        entry.property.intValue = (ushort)newSkinID;
                    }
                }
            }
            EditorGUILayout.Separator();
            foreach (var entry in groupToSkinID)
            {
                var skinGroup = skinGroups[Math.Min(entry.groupID, skinGroups.Length - 1)];
                entry.property.intValue = (ushort)EditorGUILayout.Popup(
                    skinGroup.GroupName,
                    (ushort)(entry.property.intValue),
                    skinGroup.SkinNames);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
    public class MixedValueBlock : IDisposable
    {
        public MixedValueBlock(bool showMixedValue = true)
        {
            EditorGUI.showMixedValue = showMixedValue;
        }
        public void Dispose()
        {
            EditorGUI.showMixedValue = false;

        }
    }
}

#endif

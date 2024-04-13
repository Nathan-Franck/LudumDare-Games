#if ENABLE_UNITY_2D_ANIMATION && ENABLE_UNITY_COLLECTIONS

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToonBoom.TBGRenderer
{
    /// <summary> Data unique to each Harmony exported project, does not need to be duplicated per-behavior </summary>
    public class TBGStore : ScriptableObject
    {
        [Serializable]
        public struct PaletteInfo
        {
            public string PaletteName;
            public Sprite[] Sprites;
        }
        [Serializable]
        public struct ResolutionInfo
        {
            public string ResolutionName;
            public PaletteInfo[] Palettes;
        }
        [Serializable]
        public struct SkinGroupInfo
        {
            public string GroupName;
            public string[] SkinNames;
        }
        public SkinGroupInfo[] SkinGroups;
        [Serializable]
        public struct MetadataEntry
        {
            public string Node;
            public string Name;
            public string Value;
        }
        public Material Material;
        public bool[] CutterToInverse;
        public ushort[] CutterToCutteeReadIndex;
        public ushort[] CutterToMatteReadIndex;
        public ResolutionInfo[] Resolutions;
        public string[] SpriteNames;
        public MetadataEntry[] Metadata;
        public Dictionary<Sprite, int> SpriteToIndex = new Dictionary<Sprite, int>();
        public void OnEnable()
        {
            if (Resolutions == null)
                return;
            for (int i = 0; i < Resolutions.Length; i++)
            {
                var resolution = Resolutions[i];
                for (int j = 0; j < resolution.Palettes.Length; j++)
                {
                    var palette = resolution.Palettes[j];
                    for (int k = 0; k < palette.Sprites.Length; k++)
                    {
                        SpriteToIndex.Add(palette.Sprites[k], k);
                    }
                }
            }
        }
    }
}

#endif
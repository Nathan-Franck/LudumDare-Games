using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

using Unity.Profiling;
using System.Linq;
using UnityEngine.Serialization;

namespace ToonBoom.Harmony
{
    /// <summary>
    /// Project data used mostly for interface
    /// </summary>
    [Serializable]
    public abstract class HarmonyProject : ScriptableObject
    {
        [NonReorderable]
        public List<ClipData> Clips;
        [NonReorderable]
        public List<ClipContents> ClipContents;
        [NonReorderable]
        public List<ClipData> Props;
        [NonReorderable]
        public List<HarmonyNode> Nodes; // all the drawings nodes data
        public List<string> Skins; // all the skins used by all the nodes
        public List<string> Groups; // all the groups used by all the nodes 
        [NonReorderable]
        public List<AnchorMeta> AnchorsMeta;
        [NonReorderable]
        public List<PropMeta> PropsMeta;
        public List<GenericMeta> GenericMeta;
        [FormerlySerializedAs("SheetResolutions")]
        [NonReorderable]
        public List<Spritesheet> SpriteSheets;
        [NonReorderable]
        public List<CustomSpriteData> CustomSprites;

        [NonSerialized]
        protected bool _isLoadedInNative;
        
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        [NonSerialized]
        private bool _boneCountWarningIssued = false;

        public void IssueBoneCountWarning(int boneCountUse, int boneCountSupport)
        {
            if (!_boneCountWarningIssued)
            {
                Debug.LogWarning($"{name} using {boneCountUse} bone matrices, but harmony is currently configured to support at most {boneCountSupport}. This should work but hit a CPU fallback (in the native lib). See comments around SHADER_ARRAY_SIZE in HarmonyRendererMesh.cs for how to support more.", this);
                _boneCountWarningIssued = true;
            }
        }
#endif //#if DEVELOPMENT_BUILD || UNITY_EDITOR
        
        public int ClampClipIndex(int index)
        {
            return Mathf.Clamp(index, 0, Clips.Count - 1);
        }
        
        public ClipData GetClipByIndex(int index)
        {
            return Clips[Mathf.Clamp(index, 0, Clips.Count - 1)];
        }

        public int ClampSpriteSheetIndex(int index)
        {
            return Mathf.Clamp(index, 0, SpriteSheets.Count - 1);
        }
        public Spritesheet GetSpriteSheetByIndex(int index)
        {
            return SpriteSheets[index];
        }
        
        public void OnEnable()
        {
            if (IsValid() && !IsLoadedInNative())
            {
                LoadProjectInNative();
            }
            _SpriteSheetLookup = SpriteSheetLookup.FromProject(this);
            _GroupSkinLookup = GroupSkinLookup.FromProject(this);
        }
        
        public void OnDisable()
        {
            if (IsLoadedInNative())
            {
                UnloadProjectInNative();
            }
        }

        protected abstract void LoadFromSourceProject();
        protected abstract void LoadProjectInNative();
        protected abstract void UnloadProjectInNative();
        
        public bool IsValid()
        {
            if(Clips == null || Clips.Count <= 0 || SpriteSheets == null || SpriteSheets.Count <= 0)
            {
                return false;
            }

            return true;
        }

        protected bool IsLoadedInNative()
        {
            return _isLoadedInNative;
        }

        internal bool TryToLoadNative()
        {
            if (IsLoadedInNative()) return true;
            if (!IsValid())
            {
                Debug.LogError($"Invalid HarmonyProject {name}.", this);
                return false;
            }
            LoadProjectInNative();
            _SpriteSheetLookup = SpriteSheetLookup.FromProject(this);
            _GroupSkinLookup = GroupSkinLookup.FromProject(this);
            
            return _isLoadedInNative;
        }

#if UNITY_EDITOR
        public bool TryToReloadNative()
        {
            if (IsLoadedInNative())
            {
                UnloadProjectInNative();
                return TryToLoadNative();
            }

            return false;
        }
#endif //UNITY_EDITOR

        internal int GetNativeProjectId()
        {
            return GetInstanceID();
        }

        internal int CreateRenderScript(int currentClipIndex)
        {
            string clipName = GetClipByIndex(currentClipIndex).FullName;
            return HarmonyInternal.CreateRenderScript(GetNativeProjectId(), clipName);
        }

        internal JobHandle DispatchUpdateRenderScript(int nativeRenderScriptId, NativeArray<byte> nativeClipName, NativeArray<byte> nativeResolutionName, float currentFrame, int currentDiscretizationStep, JobHandle _dependency)
        {
            HarmonyInternalUpdateRenderScriptJob job = new HarmonyInternalUpdateRenderScriptJob()
            {
                nativeRenderScriptId = nativeRenderScriptId,
                nativeProjectId = GetNativeProjectId(),
                clipNameNative = nativeClipName,
                resolutionNameNative = nativeResolutionName,
                currentFrame = currentFrame,
                currentDiscretizationStep = currentDiscretizationStep
            };
            
            var jobhandle = job.Schedule(_dependency);

            return jobhandle;
        }

        /// <summary>
        /// Unity Job to perform UpdateRenderScript native call from unity jobs
        /// </summary>
        public struct HarmonyInternalUpdateRenderScriptJob : IJob
        {
            private static readonly ProfilerMarker s_HarmonyInternalUpdateRenderScriptPerfMarker = new ProfilerMarker("HarmonyInternal.UpdateRenderScript");
            
            public int nativeRenderScriptId;
            public int nativeProjectId;
            [ReadOnly]
            public NativeArray<byte> clipNameNative;
            [ReadOnly]
            public NativeArray<byte> resolutionNameNative;
            public float currentFrame;
            public int currentDiscretizationStep;

            public unsafe void Execute()
            {
                s_HarmonyInternalUpdateRenderScriptPerfMarker.Begin();
                {
                    HarmonyInternal.UpdateRenderScript(nativeRenderScriptId, nativeProjectId,(IntPtr)clipNameNative.GetUnsafeReadOnlyPtr(), (IntPtr)resolutionNameNative.GetUnsafeReadOnlyPtr(), currentFrame, currentDiscretizationStep);
                }
                s_HarmonyInternalUpdateRenderScriptPerfMarker.End();
            }
        }

        internal static void UnloadRenderScript(int nativeRenderScriptId)
        {
            HarmonyInternal.UnloadRenderScript(nativeRenderScriptId);
        }

        /// <summary>
        /// Loads sprites from a project folder at runtime.
        /// In editor, it is preferable to use a HarmonyProjectImport to automatically load assets and create sprite atlases
        /// </summary>
        /// <param name="projectFolder">The root XML project folder</param>
        public void LoadSprites(string projectFolder)
        {
            bool useSprites = Directory.Exists(projectFolder + "/sprites/");

            //Pair atlases to resolutions
            for (int i = 0, len = SpriteSheets.Count; i < len; i++)
            {
                Spritesheet resolution = SpriteSheets[i];
                resolution.Sprites.Clear();

                if (useSprites)
                {
                    string resolutionFolder = projectFolder + "/sprites/" + resolution.SheetName + "/" + resolution.ResolutionName;
                    foreach (string file in Directory.GetFiles(resolutionFolder, "*.png"))
                    {
                        Sprite importSprite = LoadSprite(file);
                        if (importSprite != null)
                        {
                            resolution.Sprites.Add(importSprite);
                        }
                    }
                }
                else
                {
                    string resolutionFile = projectFolder + "/spriteSheets/" + resolution.SheetName + "-" + resolution.ResolutionName + ".png";
                    Sprite importSprite = LoadSprite(resolutionFile);
                    if (importSprite != null)
                    {
                        resolution.Sprites.Add(importSprite);
                    }
                }
            }
        }

        private Sprite LoadSprite(string file)
        {
            if (!File.Exists(file))
                return null;

            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, true);
            texture.LoadImage(File.ReadAllBytes(file));
            Sprite result = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            result.name = Path.GetFileName(file);

            return result;
        }

        public bool IsUsingCustomSprites()
        {
            return CustomSprites != null && CustomSprites.Count > 0;
        }

        protected void ReloadNativeSpritesheetsFromCustom()
        {
            if (IsUsingCustomSprites())
            {
                List<HarmonyBinarySpriteSheet> spritesheetBuffer = Buffers.GetSpritesheetBuffer(SpriteSheets.Count);

                try
                {
                    int size = 0;
                    for (int i = 0, len = SpriteSheets.Count; i < len; i++)
                    {
                        HarmonyBinarySpriteSheet sheet = HarmonyBinarySpriteSheet.MakeFromCustomSprites(
                            SpriteSheets[i].Sprites,
                            SpriteSheets[i].SheetName,
                            SpriteSheets[i].ResolutionName,
                            CustomSprites);
                        size += sheet.GetMarshalSize();
                        spritesheetBuffer.Add(sheet);
                    }

                    byte[] sheetBytes = new byte[size];
                    using (MemoryStream stream = new MemoryStream(sheetBytes))
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        for (int i = 0, len = spritesheetBuffer.Count; i < len; i++)
                        {
                            spritesheetBuffer[i].StoreToMemory(writer);
                        }
                    }

                    IntPtr pointerToData = Marshal.AllocHGlobal(size);
                    try
                    {
                        Marshal.Copy(sheetBytes, 0, pointerToData, sheetBytes.Length);
                        HarmonyInternal.ReloadSpreadsheets(GetNativeProjectId(), pointerToData, size, spritesheetBuffer.Count);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pointerToData);
                    }
                }
                finally
                {
                    spritesheetBuffer.Clear();
                }
            }
        }

        private static class Buffers
        {
            [ThreadStatic]
            private static List<HarmonyBinarySpriteSheet> _spritesheetBuffer;
            internal static List<HarmonyBinarySpriteSheet> GetSpritesheetBuffer(int neededCount = 8)
            {
                if (_spritesheetBuffer == null)
                {
                    _spritesheetBuffer = new List<HarmonyBinarySpriteSheet>(neededCount);
                }
                else
                {
                    _spritesheetBuffer.Clear();
                }
                return _spritesheetBuffer;
            }
        }
        private SpriteSheetLookup _SpriteSheetLookup;

        private GroupSkinLookup _GroupSkinLookup;

        public int GetSheetIndex(params string[] keys)
        {
            return _SpriteSheetLookup.FirstSatisfiesKeys(keys.SelectMany(key => key.Split('-')).ToArray());
        }

        public GroupSkin GetGroupSkin(string groupKey, string skinKey) {
            return _GroupSkinLookup.GetKeyValuePair(groupKey, skinKey);
        }
    }
}

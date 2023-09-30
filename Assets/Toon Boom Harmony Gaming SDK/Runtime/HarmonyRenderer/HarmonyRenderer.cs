using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Serialization;

using Unity.Profiling;
using Unity.Jobs;
using UnityEngine.Rendering;

namespace ToonBoom.Harmony
{
    /*!
     *  @class HarmonyRenderer
     *  Main Harmony game object component.
     *  This script will calculate the mesh, uvs, bones data
     *  and send it the the shader
     */
    [ExecuteInEditMode]
    [AddComponentMenu("Harmony/Core/HarmonyRenderer")]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public partial class HarmonyRenderer : MonoBehaviour
    {
        private const int INVALID_RENDER_SCRIPT = -1;
        
        
        // script id used for call to native code
        private int _nativeRenderScriptId = INVALID_RENDER_SCRIPT;

        public HarmonyProject Project;
        private HarmonyProject _loadedProject;

        [Header("Rendering")]
        [FormerlySerializedAs("ResolutionIndex")]
        public int SpriteSheetIndex;
        private int _lastSpriteSheetIndex;
        public void FindSpriteSheet(params string[] keys)
        {
            SpriteSheetIndex = Project.GetSheetIndex(keys);
        }
        public void FindSkin(string groupKey, string skinKey)
        {
            GroupSkin pair = Project.GetGroupSkin(groupKey, skinKey);
            foreach (GroupSkin groupSkin in GroupSkins)
            {
                if (groupSkin.GroupId == pair.GroupId)
                {
                    GroupSkins.SetSkin(GroupSkins.IndexOf(groupSkin), pair.SkinId);
                    break;
                }
            }
        }

        [FormerlySerializedAs("material")]
        public Material Material;

        private Material _lastMaterial = null;

        //  Color of the layer.  Will be multiplied with the texture color.
        [FormerlySerializedAs("color")]
        public Color Color = Color.white;
        private Color _lastColor = Color.white;

        // set the nb of discretization step, will add definition to the mesh to help with bone animation
        [FormerlySerializedAs("discretizationStep")]
        [Range(1, 50)]
        public int DiscretizationStep = 4;
        private int _lastDiscretizationStep = 0;

        [Header("Animation")]
        [FormerlySerializedAs("AnimatedClipIndex")]
        public int CurrentClipIndex = 0;
        private int _lastClip = -1;

        public ClipData CurrentClip
        {
            get => Project.GetClipByIndex(CurrentClipIndex);
        }
        
        private static readonly ProfilerMarker s_OnWillRenderObjectPerfMarker = new ProfilerMarker("OnWillRenderObject");
        [NonSerialized]
        private bool _renderDataDirty = false;
        [NonSerialized]
        private bool _propertyBlockDirty = false;

        private MaterialPropertyBlock _globalPropertyBlock;
        private static int BonesShaderPropertyID = Shader.PropertyToID("_Bones");
        private static int MainTexShaderPropertyID = Shader.PropertyToID("_MainTex");
        private static int MaskTexShaderPropertyID = Shader.PropertyToID("_MaskTex");
        private static int ColorShaderPropertyID = Shader.PropertyToID("_Color");

        // current frame where at, will mostly be driven by the animator
        [FormerlySerializedAs("Frame")]
        public float CurrentFrame = 1;
        private float _lastFrame = -1;
        
        private bool _markedDirty = false;

        private List<HarmonyAnchor> _anchors = new List<HarmonyAnchor>();
        private bool _anchorsChanged = false;
        
        [Header("Animation Generation")]
        public HarmonyAnimationSettings AnimationSettings;

        public void AddAnchor(HarmonyAnchor anchor)
        {
            _anchors.Add(anchor);
            _anchorsChanged = true;
        }

        public void RemoveAnchor(HarmonyAnchor anchor)
        {
            _anchors.Remove(anchor);
            _anchorsChanged = true;
        }
        
        private List<NativeArray<byte>> _nativeClipFullNames;
        private List<NativeArray<byte>> _nativeSpriteSheetNames;
        
        private NativeArray<byte> GetNativeClipFullName(int index)
        {
            return _nativeClipFullNames[index];
        }

        private NativeArray<byte> GetNativeSpriteSheetName(int index)
        {
            return _nativeSpriteSheetNames[index];
        }
        
        private void RefreshClipFullNamesNativeCache()
        {
            ReleaseClipFullNamesNativeCache();
            
            if (_nativeClipFullNames == null)
            {
                _nativeClipFullNames = new List<NativeArray<byte>>(Project.Clips.Count);
            }

            // Allocate Native UTF8 versions of the clip full names, for use by the native lib from unity jobs
            foreach (var clipData in Project.Clips)
            {
                _nativeClipFullNames.Add(HarmonyUtils.NativeArrayString(clipData.FullName, Allocator.Persistent));
            }
        }

        private void ReleaseClipFullNamesNativeCache()
        {
            // Release NativeArrays
            if (_nativeClipFullNames != null)
            {
                foreach (var clipData in _nativeClipFullNames)
                {
                    clipData.Dispose();
                }
                _nativeClipFullNames.Clear();
            }
        }
        
        private void RefreshResolutionNamesNativeCache()
        {
            ReleaseResolutionNamesNativeCache();
            
            if (_nativeSpriteSheetNames == null)
            {
                _nativeSpriteSheetNames = new List<NativeArray<byte>>(Project.Clips.Count);
            }

            // Allocate Native UTF8 versions of the resolution names, for use by the native lib from unity jobs
            foreach (var resolution in Project.SpriteSheets)
            {
                _nativeSpriteSheetNames.Add(HarmonyUtils.NativeArrayString(resolution.ResolutionName, Allocator.Persistent));
            }
        }
        private void ReleaseResolutionNamesNativeCache()
        {
            // Release NativeArrays
            if (_nativeSpriteSheetNames != null)
            {
                foreach (var resolutionName in _nativeSpriteSheetNames)
                {
                    resolutionName.Dispose();
                }

                _nativeSpriteSheetNames.Clear();
            }
        }
        
        protected void OnEnable()
        {
            AwakeMesh();
            
            // _globalPropertyBlock is a MaterialPropertyBlock, which is not unity serializable. Thus when hot-reloading it is null and needs to be re-created.
            if (_globalPropertyBlock == null)
            {
                _globalPropertyBlock = new MaterialPropertyBlock();
            }

            // make sure the native code is initialized for this instance
            CreateRenderScript();

            // Hook Render Callbacks 
            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                // Built in render pipeline
                Camera.onPreCull += OnPreCullCallback;
            }
            else
            {
                // Scriptable Render Pipeline (Including Universal Render Pipeline and High Definition Render Pipeline)
                // Called automatically for URP and HDRP, if using a custom SRP you must call RenderPipelineManager.beginFrameRendering at the start of your RenderPipeline.Render function.
                RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
            }
        }

        protected void OnDisable()
        {
            // In Case disabling one in flight
            SyncUpdates();
            
            // Un-Hook Render Callbacks
            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                // Built in render pipeline
                Camera.onPreCull -= OnPreCullCallback;
            }
            else
            {
                // Scriptable Render Pipeline (Including Universal Render Pipeline and High Definition Render Pipeline)
                RenderPipelineManager.beginFrameRendering -= OnBeginFrameRendering;
            }
            
            DestroyRenderScript();
        }
        
        // Only called when using the Built In Render Pipeline (non SRP / non URP / non HDRP)
        private void OnPreCullCallback(Camera camera)
        {
            // Mesh updates sycned in pre-cull in case UpdateMesh would change if it got culled or not
            // Note this will be called for every camera, however; UpdateMesh knows if updates are pending and will only do so once, no matter how many times it is called.
            SyncUpdates();
        }

        // Only Called for SRP (Including URP or HDRP)
        // Called Automatically for URP or HDRP, for custom SRP you must call RenderPipeline.BeginFrameRendering at the start of RenderPipline.Render
        private void OnBeginFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            // Only called once per render for SRP (URP and HDRP)
            SyncUpdates();
        }

        private void SyncUpdates()
        {
            UpdateMesh();

            foreach (var anchor in _anchors)
            {
                anchor.SyncCalculateLocatorTransform();
            }
        }

        protected void CreateRenderScript()
        {
            if (Project == null)
            {
                //Debug.LogError($"No HarmonyProject set for HarmonyRenderer {name}", this);
                return;
            }
            else if (!Project.TryToLoadNative())
            {
                Debug.LogError($"Failed to load native HarmonyProject {Project.name}", this);
                return;
            }
            
            if (_nativeRenderScriptId == INVALID_RENDER_SCRIPT) //-1 Should always be the case, potentially remove
            {
                _nativeRenderScriptId = Project.CreateRenderScript(CurrentClipIndex);
                
                CurrentClipIndex = _lastClip = Project.ClampClipIndex(CurrentClipIndex);
                SpriteSheetIndex = _lastSpriteSheetIndex = Project.ClampSpriteSheetIndex(SpriteSheetIndex);
                
                RefreshClipFullNamesNativeCache();
                RefreshResolutionNamesNativeCache();
            }

            _meshRenderer.enabled = true;
            _loadedProject = Project;

            MarkDirty();
        }

        protected void DestroyRenderScript()
        {
            //  Free render scripts.  They'll be recreated if the game
            //  object is reactivated.  Clips and sprite sheet still
            //  remain loaded in memory.
            if (_nativeRenderScriptId != INVALID_RENDER_SCRIPT)
            {
                HarmonyProject.UnloadRenderScript(_nativeRenderScriptId);
                _nativeRenderScriptId = INVALID_RENDER_SCRIPT;
                
                ReleaseClipFullNamesNativeCache();
                ReleaseResolutionNamesNativeCache();
            }

            if (_meshRenderer)
            {
                _meshRenderer.enabled = false;
            }

            _loadedProject = null;
        }

        protected void OnDestroy()
        {
            // In case destroying one in flight
            SyncUpdates();
            
            DestroyMesh();
        }

        protected void LateUpdate()
        {
            if (Project == null && _loadedProject == null && _meshRenderer.enabled)
            {
                // Missing Reference
                Debug.LogError($"Missing Project Reference for renderer {name}", this);
                DestroyRenderScript();
                return;
            }
            if (Project != _loadedProject)
            {
                DestroyRenderScript();
                CreateRenderScript();
            }
            
            DispatchMeshUpdates();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Called when the project reloaded in place (same 'Project' unity asset, but different data)
        /// </summary>
        public void ProjectReloaded()
        {
            DestroyRenderScript();
            CreateRenderScript();
        }
#endif // UNITY_EDITOR

        private void MarkDirty()
        {
            _markedDirty = true;
        }

        /// <summary>
        /// Forces a inline update, vs the normal path of dispatching jobs and syncing before rendering
        /// Used by Harmony Game Previewer (To get bounds of mesh on load)
        /// </summary>
        public void UpdateRenderer()
        {
            LateUpdate();
            SyncUpdates();
        }

        private void DispatchMeshUpdates()
        {
            if (!_meshRenderer.enabled)
                return;


            var nonClampedClipIndex = CurrentClipIndex;
            CurrentClipIndex = Project.ClampClipIndex(CurrentClipIndex);
            if (nonClampedClipIndex != CurrentClipIndex)
            {
                Debug.LogWarning($"Clip id {nonClampedClipIndex} is not valid for project {Project.name} where clip count = {Project.Clips.Count}, clamping to {CurrentClipIndex}");
            }
            
            bool scriptIsDirty = false;

            JobHandle skinJobHandle = new JobHandle();

            // update native skins values
            if (UpdateSkins() || _markedDirty)
            {
                scriptIsDirty = true;
                skinJobHandle = DispatchUpdateSkinsJob();
            }

            scriptIsDirty = scriptIsDirty ||
                CurrentFrame != _lastFrame ||
                CurrentClipIndex != _lastClip ||
                SpriteSheetIndex != _lastSpriteSheetIndex ||
                DiscretizationStep != _lastDiscretizationStep ||
                Material != _lastMaterial ||
                _anchorsChanged;

            if (Color != _lastColor)
            {
                _lastColor = Color;
                _propertyBlockDirty = true;
            }

            if (scriptIsDirty)
            {
                float duration = Project.GetClipByIndex(CurrentClipIndex).FrameCount;
                CurrentFrame = Mathf.Clamp(CurrentFrame, 1, (int)duration);

                foreach (var anchor in _anchors)
                {
                    anchor.DispatchCalculateLocatorTransform(Project.GetNativeProjectId(), CurrentFrame, GetNativeClipFullName(CurrentClipIndex));
                }

                MakeSureTexturesAreUpToDate(_markedDirty);
                
                _updateJobHandle = Project.DispatchUpdateRenderScript(_nativeRenderScriptId, GetNativeClipFullName(CurrentClipIndex), GetNativeSpriteSheetName(SpriteSheetIndex), CurrentFrame, DiscretizationStep, skinJobHandle);
                 
                // Make scheduled jobs available for worker threads to execute
                // This includes: UpdateSkinsJob, CalculateLocatorTransformJob, and UpdateRenderScriptJobs
                JobHandle.ScheduleBatchedJobs();
                
                _lastFrame = CurrentFrame;
                _lastClip = CurrentClipIndex;
                _lastSpriteSheetIndex = SpriteSheetIndex;
                _lastDiscretizationStep = DiscretizationStep;
                _markedDirty = false;
                _lastMaterial = Material;
                _anchorsChanged = false;

                _renderDataDirty = true;
                _propertyBlockDirty = true;
            }
        }
        public void UpdateAudio(int currentClipIndex, int lastClipIndex, float currentFrame, float lastFrame)
        {
            if (!Application.isPlaying || _audioSource == null)
            {
                return;
            }
            if (currentClipIndex == lastClipIndex && currentFrame > lastFrame)
            {
                return;
            }
            if (Project.ClipContents.Count <= currentClipIndex)
            {
                return;
            }

            ClipContents clipContents = Project.ClipContents[currentClipIndex];

            if (clipContents.Sounds.Count == 0)
            {
                return;
            }
            SoundData soundData = clipContents.Sounds[0];

            float clipFrameCount = CurrentClip.FrameCount;
            float startTime = (soundData.StartFrame - 1) / AnimationSettings.FrameRate;

            _audioSource.clip = soundData.Clip;
            _audioSource.Stop();
            if (startTime >= 0)
            {
                float stopTime = clipFrameCount / AnimationSettings.FrameRate - startTime;
                _audioSource.time = 0;
                _audioSource.PlayScheduled(AudioSettings.dspTime + startTime);
                _audioSource.SetScheduledEndTime(AudioSettings.dspTime + stopTime);
            }
            else
            {
                _audioSource.time = -startTime;
                _audioSource.PlayScheduled(AudioSettings.dspTime);
                _audioSource.SetScheduledEndTime(AudioSettings.dspTime + clipFrameCount / AnimationSettings.FrameRate);
            }
        }

        // Note this won't work for UI (Though harmony doesn't currently work as UI either)
        // If the object doesn't render this frame (for example it gets culled) this won't trigger as we don't need
        // the property block updated if it is not drawn
        private void OnWillRenderObject()
        {
            if (_propertyBlockDirty)
            {
                using (s_OnWillRenderObjectPerfMarker.Auto())
                {
                    _meshRenderer.GetPropertyBlock(_globalPropertyBlock);
                    _globalPropertyBlock.SetMatrixArray(BonesShaderPropertyID, _boneMatrixArray);
                    _globalPropertyBlock.SetColor(ColorShaderPropertyID, Color);
                
                    List<Material> materials = UpdateBuffers.GetMaterialBuffer();
                    _meshRenderer.GetSharedMaterials(materials);
                    for (int i = 0, len = materials.Count; i < len; i++)
                    {
                        _globalPropertyBlock.SetTexture(MainTexShaderPropertyID, _orderedTextures[i]);
                        _globalPropertyBlock.SetTexture(MaskTexShaderPropertyID, _orderedMasks[i]);

                        _meshRenderer.SetPropertyBlock(_globalPropertyBlock, i);
                    }
                    materials.Clear();
                    _propertyBlockDirty = false;
                }
            }
        }
        
        /// <summary>
        /// Sets a property on the material property block.
        /// </summary>        
        public void SetMaterialProperty(string propertyName, float value)
        {
            _meshRenderer.GetPropertyBlock(_globalPropertyBlock);
            _globalPropertyBlock.SetFloat(propertyName, value);
            _meshRenderer.SetPropertyBlock(_globalPropertyBlock);
        }
        
        public void SetMaterialProperty(int propertyID, float value)
        {
            _meshRenderer.GetPropertyBlock(_globalPropertyBlock);
            _globalPropertyBlock.SetFloat(propertyID, value);
            _meshRenderer.SetPropertyBlock(_globalPropertyBlock);
        }
    }
}

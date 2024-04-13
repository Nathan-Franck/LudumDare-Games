using System;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Profiling;

namespace ToonBoom.Harmony
{
    /*!
     *  @class HarmonyAnchor
     *  Extract position from animation node.
     *  To be used in conjunction with HarmonyRenderer.
     */
    [ExecuteInEditMode]
    [AddComponentMenu("Harmony/Core/HarmonyAnchor")]
    public class HarmonyAnchor : MonoBehaviour
    {
        public string NodeName;
        private HarmonyRenderer _harmonyRenderer = null;

        private bool _jobInFlight = false;
        private JobHandle _jobHandle;

        private string _lastNodeName;
        private NativeArray<byte> _nodeNameNative;
        private NativeArray<Vector3> _positionNative;
        private NativeArray<Vector3> _rotationNative;
        private NativeArray<Vector3> _scaleNative;
        private NativeArray<bool> _resultNative;

        protected void OnEnable()
        {
            // Cache node name in native utf8 for use by native Harmony lib
            _nodeNameNative = HarmonyUtils.NativeArrayString(NodeName, Allocator.Persistent);
            _lastNodeName = NodeName;

            _harmonyRenderer = GetComponentInParent<HarmonyRenderer>();
            _harmonyRenderer.AddAnchor(this);
        }

        protected void OnDisable()
        {
            SyncCalculateLocatorTransform();
            _harmonyRenderer.RemoveAnchor(this);
            
            // Free native memory
            _nodeNameNative.Dispose();
        }

        public bool IsValid()
        {
            return _harmonyRenderer != null && _harmonyRenderer.Project != null && _harmonyRenderer.CurrentClip.Name != null;
        }
        
        public void DispatchCalculateLocatorTransform(int nativeProjectId, float currentFrame, NativeArray<byte> nativeClipFullName)
        {
            Debug.Assert(_jobInFlight == false, "Dispatching CalculateLocatorTransform while previous job is still in flight.");
            
            // If the NodeName changes at runtime, need to update the native utf8 version for use by the native lib
            if (_lastNodeName != NodeName)
            {
                _nodeNameNative.Dispose();
                _nodeNameNative = HarmonyUtils.NativeArrayString(NodeName, Allocator.Persistent);
                _lastNodeName = NodeName;
            }
            
            // Native memory for use by update jobs
            _resultNative = new NativeArray<bool>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            _positionNative = new NativeArray<Vector3>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            _rotationNative = new NativeArray<Vector3>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            _scaleNative = new NativeArray<Vector3>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            
            // Configure anchor update job
            HarmonyInternalUpdateAnchorJob job = new HarmonyInternalUpdateAnchorJob()
            {
                nativeProjectId = nativeProjectId,
                currentFrame = currentFrame,
                clipnName = nativeClipFullName,
                position = _positionNative,
                rotation = _rotationNative,
                scale = _scaleNative,
                result = _resultNative,
                nodeName = _nodeNameNative
            };
            
            // Schedule job
            _jobHandle = job.Schedule();
            _jobInFlight = true;
        }

        public void SyncCalculateLocatorTransform()
        {
            if (_jobInFlight)
            {
                _jobHandle.Complete();

                if (_resultNative[0])
                {
                    transform.localPosition = _positionNative[0];
                    transform.localRotation = Quaternion.Euler(_rotationNative[0]);
                    transform.localScale = _scaleNative[0];
                }
                
                _resultNative.Dispose();
                _positionNative.Dispose();
                _rotationNative.Dispose();
                _scaleNative.Dispose();

                _jobInFlight = false;
            }
        }
        
        /*!
         *  @struct HarmonyInternalUpdateAnchorJob
         *  Unity Job to perform native CalculateLocatorTransform from Job thread pool
         */
        private struct HarmonyInternalUpdateAnchorJob : IJob
        {
            private static readonly ProfilerMarker s_UpdateInternalAnchorPerfMarker = new ProfilerMarker("HarmonyInternal.CalculateLocatorTransform");
            
            public int nativeProjectId;
            public float currentFrame;

            public NativeArray<Vector3> position;
            public NativeArray<Vector3> rotation;
            public NativeArray<Vector3> scale;
            public NativeArray<bool> result;
            
            [ReadOnly]
            public NativeArray<byte> clipnName;
            
            [ReadOnly]
            public NativeArray<byte> nodeName;
            
            public unsafe void Execute()
            {
                s_UpdateInternalAnchorPerfMarker.Begin();
                {
                    IntPtr positionNative = (IntPtr) position.GetUnsafePtr();
                    IntPtr rotationNative = (IntPtr) rotation.GetUnsafePtr();
                    IntPtr scaleNative = (IntPtr) scale.GetUnsafePtr();
                    
                    IntPtr nodeNameNative = (IntPtr) nodeName.GetUnsafeReadOnlyPtr();
                    IntPtr clipNameNative = (IntPtr) clipnName.GetUnsafeReadOnlyPtr();
                    
                    result[0] = HarmonyInternal.CalculateLocatorTransform(nativeProjectId, clipNameNative, currentFrame, nodeNameNative, positionNative, rotationNative, scaleNative);
                }
                s_UpdateInternalAnchorPerfMarker.End();
            }
        }
    }
}
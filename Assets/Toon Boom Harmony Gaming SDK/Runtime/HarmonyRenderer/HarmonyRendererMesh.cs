using UnityEngine;
using System.Collections.Generic;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine.Rendering;

namespace ToonBoom.Harmony
{
    public partial class HarmonyRenderer
    {
        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;
        private AudioSource _audioSource;
        private Mesh _mesh;
        private VertexAttributeDescriptor[] _layout;

        List<Texture2D> _orderedTextures = new List<Texture2D>();
        List<Texture2D> _orderedMasks = new List<Texture2D>();
        
        // 32 is the default Harmony bone matrix array size
        // To change this you must
        // 1) Change HarmonyRendererMesh.SHADER_ARRAY_SIZE
        // 2) Change Corresponding arrays in harmony shaders / shader graph
        // 3) Change MAX_BONES_GPU and MAX_BONES_GPUf in the native library (RD_RenderScriptFx.h)
        const int SHADER_ARRAY_SIZE = 32; 
        Matrix4x4[] _boneMatrixArray = new Matrix4x4[SHADER_ARRAY_SIZE];
        private int _boneCount = 0;

        private List<Texture2D> _textures;
        private JobHandle _updateJobHandle;
        
        private static readonly ProfilerMarker s_UpdateMeshPerfMarker = new ProfilerMarker("UpdateMesh");
        private static readonly ProfilerMarker s_UpdateBonesPerfMarker = new ProfilerMarker("UpdateBones");
        private static readonly ProfilerMarker s_ReadSubmeshIndicesAndTexturesPerfMarker = new ProfilerMarker("UpdateSubmeshIndicesAndTextures");

        VertexAttributeDescriptor[] CreateVertexLayout()
        {
            return new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3), // verticies
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3), // texCoord & Opacity
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4), // fxparams
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4), // fxViewports
                new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4) // boneParams
            };
        }
        
        /// <summary>
        /// Vertex Buffer struct. Must match the VertexAttributeDescriptor
        /// This is formatted to match directly the data coming from VertexData in RD_RenderScriptFx.h so no fixup is needed on the data
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct VertexStruct
        {
            public Vector3 pos;
            public Vector3 texCoordAndOpacity;
            public Vector4 fxParams;
            public Vector4 fxViewports;
            public Vector4 boneParams;
        }
        
        private void AwakeMesh()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshFilter.hideFlags = HideFlags.NotEditable;

            _meshFilter.mesh = _mesh = new Mesh();
            _mesh.name = "Harmony Mesh";
            _mesh.MarkDynamic();
            _mesh.hideFlags = HideFlags.DontSave;

            _layout = CreateVertexLayout();

            _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.hideFlags = HideFlags.NotEditable;
            _meshRenderer.enabled = false;
        }

        private void MakeSureTexturesAreUpToDate(bool force = false)
        {
            if (_textures == null || _textures.Count == 0 || SpriteSheetIndex != _lastSpriteSheetIndex || force)
            {
                Spritesheet resolution = Project.GetSpriteSheetByIndex(SpriteSheetIndex);
                if(resolution.HasValidSprites())
                {
                    if (_textures == null)
                    {
                        _textures = new List<Texture2D>();
                    }
                    
                    _textures.Clear();

                    for (int i = 0, len = resolution.Sprites.Count; i < len; i++)
                    {
                        Sprite sprite = resolution.Sprites[i];
                        if (sprite == null)
                        {
                            _textures.Add(null);
                        }
                        else if (!_textures.Contains(sprite.texture))
                        {
                            _textures.Add(sprite.texture);
                        }
                    }
                }
            }
        }

        private void UpdateMesh()
        {
            if (_renderDataDirty)
            {
                using (s_UpdateMeshPerfMarker.Auto())
                {
                    // Ensure the update job is complete, before retrieving results
                    _updateJobHandle.Complete();

                    IntPtr vertexData, indexData, textureData;
                    int vertexCount, indexCount, textureCount;
                    bool gotModelData = HarmonyInternal.GetModelData(_nativeRenderScriptId, out vertexData, out vertexCount, out indexData, out indexCount, out textureData, out textureCount);
                    if (gotModelData)
                    {
                        // read natives data
                        ReadVertices(vertexData, vertexCount);
                        ReadSubmeshIndicesAndTextures(indexData, indexCount, textureData, textureCount);

                        // Bones
                        UpdateBones();
                    }
                    else
                    {
                        Debug.LogError("No model data", gameObject);
                    }
                    
                    _renderDataDirty = false;
                }
            }
        }

        private void DestroyMesh()
        {
            DestroyImmediate(_mesh);
        }

        unsafe private void ReadVertices(IntPtr vertexData, int vertexCount)
        {
            _mesh.SetVertexBufferParams(vertexCount, _layout);
            
            var vertexBufferData = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<VertexStruct>(vertexData.ToPointer(), vertexCount, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var atomicSafetyHandle = AtomicSafetyHandle.Create();
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref vertexBufferData, atomicSafetyHandle);
#endif // ENABLE_UNITY_COLLECTIONS_CHECKS
            _mesh.SetVertexBufferData(vertexBufferData, 0, 0, vertexCount);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(atomicSafetyHandle);
#endif // ENABLE_UNITY_COLLECTIONS_CHECKS
        }

        unsafe private void ReadSubmeshIndicesAndTextures(IntPtr indexData, int indexCount, IntPtr textureData, int textureCount)
        {
            using(s_ReadSubmeshIndicesAndTexturesPerfMarker.Auto())
            {
                var subMeshDescriptors = UpdateBuffers.GetSubMeshDescriptorsBuffer(textureCount);
                
                _orderedTextures.Clear();
                _orderedMasks.Clear();

                int indicesOffset = 0;
                _mesh.subMeshCount = textureCount;

                for (int textureIndex = 0, i = 0; textureIndex < textureCount * 3; textureIndex += 3, i++)
                {
                    int* nativeTextureMappingArray = (int*)textureData.ToPointer();
                
                    int textureId = nativeTextureMappingArray[textureIndex];
                    int maskTextureId = nativeTextureMappingArray[textureIndex + 1];
                    int count = nativeTextureMappingArray[textureIndex + 2];
                    
                    _orderedTextures.Add(_textures[textureId]);
                    _orderedMasks.Add(_textures[maskTextureId]);
                    
                    subMeshDescriptors.Add(new SubMeshDescriptor(indicesOffset, count, MeshTopology.Triangles));

                    indicesOffset += count;
                }

                _mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
                var indexBufferData = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Int16>(indexData.ToPointer(), indexCount, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var atomicSafetyHandle = AtomicSafetyHandle.Create();
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref indexBufferData, atomicSafetyHandle);
#endif // ENABLE_UNITY_COLLECTIONS_CHECKS
                
                _mesh.SetIndexBufferData(indexBufferData, 0, 0, indexCount, MeshUpdateFlags.DontValidateIndices);
                
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.Release(atomicSafetyHandle);
#endif // ENABLE_UNITY_COLLECTIONS_CHECKS

                
                _mesh.SetSubMeshes(subMeshDescriptors, MeshUpdateFlags.Default);
            }


            List<Material> materials = UpdateBuffers.GetMaterialBuffer();
            _meshRenderer.GetSharedMaterials(materials);
            bool needReassign = false;
            if (materials.Count > _orderedTextures.Count)
            {
                needReassign = true;
                materials.RemoveRange(_orderedTextures.Count, materials.Count - _orderedTextures.Count);
            }
            else if(materials.Count < _orderedTextures.Count)
            {
                needReassign = true;
                for (int i = 0, len = _orderedTextures.Count - materials.Count; i < len; i++)
                {
                    materials.Add(Material);
                }
            }

            for (int i = 0, len = materials.Count; i < len; i++)
            {
                if (materials[i] != Material)
                {
                    materials[i] = Material;
                    needReassign = true;
                }
            }

            if(needReassign)
            {
                _meshRenderer.sharedMaterials = materials.ToArray();
            }
        }
        
        unsafe private void UpdateBones()
        {
            using (s_UpdateBonesPerfMarker.Auto())
            {
                IntPtr boneData = IntPtr.Zero;
                if (HarmonyInternal.GetBoneData(_nativeRenderScriptId, ref boneData, ref _boneCount))
                {
                    if (_boneCount > 0)
                    {
                        if (_boneMatrixArray.Length < _boneCount)
                        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                            Project.IssueBoneCountWarning(_boneCount, _boneMatrixArray.Length);
#endif // DEVELOPMENT_BUILD || UNITY_EDITOR
                            _boneCount = _boneMatrixArray.Length;
                        }

                        var boneMatrixArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Matrix4x4>(boneData.ToPointer(), _boneCount, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        var atomicSafetyHandle = AtomicSafetyHandle.Create();
                        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref boneMatrixArray, atomicSafetyHandle);
#endif // ENABLE_UNITY_COLLECTIONS_CHECKS
                        NativeArray<Matrix4x4>.Copy(boneMatrixArray, _boneMatrixArray, Mathf.Min(_boneCount, _boneMatrixArray.Length));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        AtomicSafetyHandle.Release(atomicSafetyHandle);
#endif // ENABLE_UNITY_COLLECTIONS_CHECKS
                    }
                }
                else
                {
                    Debug.LogError("No Bones data", gameObject);
                }
            }
        }

        private static class UpdateBuffers
        {
            [ThreadStatic]
            private static List<Material> _materials;
            internal static List<Material> GetMaterialBuffer(int neededCount = 8)
            {
                if (_materials == null)
                {
                    _materials = new List<Material>(neededCount);
                }
                else
                {
                    _materials.Clear();
                }
                return _materials;
            }
            
            [ThreadStatic]
            private static List<SubMeshDescriptor> _subMeshDescriptors;
            internal static List<SubMeshDescriptor> GetSubMeshDescriptorsBuffer(int neededCount = 8)
            {
                if (_subMeshDescriptors == null)
                {
                    _subMeshDescriptors = new List<SubMeshDescriptor>(neededCount);
                }
                else
                {
                    _subMeshDescriptors.Clear();
                }
                return _subMeshDescriptors;
            }
        }
    }
}

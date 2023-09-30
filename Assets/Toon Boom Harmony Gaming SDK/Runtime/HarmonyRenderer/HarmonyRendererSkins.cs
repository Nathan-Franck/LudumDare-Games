using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;

namespace ToonBoom.Harmony
{
    public partial class HarmonyRenderer
    {
        public GroupSkinList GroupSkins;

        private static readonly ProfilerMarker s_UpdateSkinsPerfMarker = new ProfilerMarker("UpdateSkins");
        private uint[] _skins = new uint[0];

        /// <summary>
        /// Update nodes skins values modified by the Group Skins
        /// </summary>
        public bool UpdateSkins()
        {
            using (s_UpdateSkinsPerfMarker.Auto())
            {
                bool changed = false;

                // make sure Skins length is the same as the number of node
                if (_skins.Length != Project.Nodes.Count)
                {
                    Array.Resize(ref _skins, Project.Nodes.Count);
                    changed = true;
                }

                for (int i = 0; i < _skins.Length; i++)
                {
                    uint skinValue = 0;
                    HarmonyNode currentNode = Project.Nodes[i];

                    if (currentNode != null && currentNode.SkinIds != null)
                    {
                        // group skins applied in order. Last one always overrides
                        for (int j = GroupSkins.Count - 1; j >= 0; j--)
                        {
                            var groupSkin = GroupSkins[j];
                            int groupId = groupSkin.GroupId;
                            if (groupId == 0 || currentNode.GroupId == groupId)
                            {
                                int skinId = groupSkin.SkinId;
                                for (int k = 0; k < currentNode.SkinIds.Length; k++)
                                {
                                    if (currentNode.SkinIds[k] == skinId)
                                    {
                                        skinValue = (uint)skinId;
                                        break;
                                    }
                                }

                                break;
                            }
                        }
                    }

                    changed = changed || _skins[i] != skinValue;
                    _skins[i] = skinValue;
                }

                return changed;
            }
        }

        public JobHandle DispatchUpdateSkinsJob()
        {
            var skinsNative = new NativeArray<uint>(_skins, Allocator.TempJob);

            HarmonyInternalUpdateSkinsJob job = new HarmonyInternalUpdateSkinsJob()
            {
                nativeRenderScriptId = _nativeRenderScriptId,
                skins = skinsNative
            };

            return job.Schedule();
        }

        public struct HarmonyInternalUpdateSkinsJob : IJob
        {
            private static readonly ProfilerMarker s_UpdateInternalSkinsPerfMarker = new ProfilerMarker("HarmonyInternal.UpdateSkins");

            public int nativeRenderScriptId;

            [ReadOnly]
            [DeallocateOnJobCompletionAttribute]
            public NativeArray<uint> skins;
            public void Execute()
            {
                s_UpdateInternalSkinsPerfMarker.Begin();
                {
                    unsafe
                    {
                        UIntPtr nativeSkins = (UIntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(skins);
                        HarmonyInternal.UpdateSkins(nativeRenderScriptId, nativeSkins, skins.Length);
                    }

                }
                s_UpdateInternalSkinsPerfMarker.End();
            }
        }
    }
}

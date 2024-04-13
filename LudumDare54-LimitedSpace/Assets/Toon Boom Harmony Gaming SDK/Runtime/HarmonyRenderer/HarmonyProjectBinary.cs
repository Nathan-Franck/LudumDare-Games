using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace ToonBoom.Harmony
{
    public class HarmonyProjectBinary : HarmonyProject
    {
#if UNITY_EDITOR
        public static Dictionary<string, int> ProjectPathToIdMap = new Dictionary<string, int>();

        public static void UnloadProjectById(int projectId)
        {
            Debug.Log($"Unloading bytes project by ID in native - {projectId}");

            HarmonyInternal.UnloadProject(projectId);
        }
#endif //UNITY_EDITOR
        public static HarmonyProjectBinary CreateFromFile(string projectFolder)
        {
            byte[] bytes = Xml2Bin.ConvertToMemory(Path.GetFullPath(projectFolder));
            return CreateFromBytes(bytes);
        }
        public static HarmonyProjectBinary CreateFromBytes(byte[] projectBytes)
        {
            HarmonyProjectBinary project = CreateInstance<HarmonyProjectBinary>();
            HarmonyBinaryUtil.FillProjectFromBinary(project, projectBytes);
            project.ProjectBytes = projectBytes;

            return project;
        }

        [HideInInspector]
        public byte[] ProjectBytes;

        [ContextMenu("Load")]
        protected override void LoadFromSourceProject()
        {
            HarmonyBinaryUtil.FillProjectFromBinary(this, ProjectBytes);
            if(IsValid())
            {
                LoadProjectInNative();
            }
        }

        protected override void LoadProjectInNative()
        {
            if(IsLoadedInNative())
            {
                UnloadProjectInNative();
            }

            byte[] bytes = ProjectBytes;
            int size = sizeof(byte) * bytes.Length;
            int id = GetNativeProjectId();
            IntPtr pointerToData = Marshal.AllocHGlobal(size);
            try
            {
#if UNITY_EDITOR
                var assetPath = AssetDatabase.GetAssetPath(this);
                if (assetPath != null && assetPath != "")
                {
                    Debug.Assert(!ProjectPathToIdMap.ContainsKey(assetPath), $"Loaded project {assetPath} twice.", this);
                    
                    ProjectPathToIdMap[assetPath] = id;
                }
                //Debug.Log($"Loading bytes project ({name}) in native - {GetInstanceID()} - ({assetPath})", this);
#else
                //Debug.Log($"Loading bytes project ({name}) in native - {GetInstanceID()}", this);
#endif // UNITY_EDITOR
                
                Marshal.Copy(bytes, 0, pointerToData, bytes.Length);
                HarmonyInternal.LoadProject(id, pointerToData, size);
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToData);
            }

            ReloadNativeSpritesheetsFromCustom();
            _isLoadedInNative = true;
        }

        protected override void UnloadProjectInNative()
        {
#if UNITY_EDITOR
            var assetPath = AssetDatabase.GetAssetPath(this);
            //Debug.Log($"Unloading bytes project ({name}) in native - {GetInstanceID()} - ({assetPath})", this);
            
            if (assetPath != null && assetPath != "")
            {
                if (!ProjectPathToIdMap.Remove(assetPath))
                {
                    Debug.LogError($"Harmony Project {assetPath} was not loaded but is unloading.", this);
                }
            }
#else
            Debug.Log($"Unloading bytes project ({name}) in native - {GetInstanceID()}", this);
#endif // UNITY_EDITOR
            HarmonyInternal.UnloadProject(GetNativeProjectId());

            _isLoadedInNative = false;
        }
    }
}

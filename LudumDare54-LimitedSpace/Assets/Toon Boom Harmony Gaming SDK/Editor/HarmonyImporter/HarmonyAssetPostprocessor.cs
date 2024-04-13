using System.Collections.Generic;
using ToonBoom.Harmony;
using UnityEngine;
using UnityEditor;
using System;

namespace ToonBoom.Harmony
{
    class HarmonyAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Harmony Render's using updated HarmonyProjects need to reload their native render scripts, and be marked dirty
            HarmonyRenderer[] harmonyRenderers = UnityEngine.Object.FindObjectsOfType<HarmonyRenderer>();
            // Imported or Updated Assets
            foreach (string path in importedAssets)
            {
                try
                {
                    var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                    if (assetType != null && assetType.IsSubclassOf(typeof(HarmonyProject)))
                    {
                        if (AssetDatabase.IsMainAssetAtPathLoaded(path))
                        {
                            var harmonyProject = AssetDatabase.LoadMainAssetAtPath(path) as HarmonyProject;
                            if (harmonyProject.TryToReloadNative())
                            {
                                foreach (var harmonyRenderer in harmonyRenderers)
                                {
                                    if (harmonyRenderer.enabled && harmonyRenderer.Project == harmonyProject)
                                    {
                                        // Note: Yes this can be 'null' and also equal 'harmonyProject'
                                        // this is what happens with Missing asset references
                                        if (harmonyRenderer.Project == null)
                                        {
                                            // Restoring lost reference (will be enable/disabled automatically)
                                            Debug.Log($"Restoring missing reference");
                                            harmonyRenderer.Project = harmonyProject;
                                        }
                                        else
                                        {
                                            harmonyRenderer.ProjectReloaded();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
				catch (Exception e)
				{
					Debug.LogException(e);
				}
            }

            // When deleted, projects that were loaded do not get their OnDisable call, and would leak the load in the native dll
            foreach (var path in deletedAssets)
            {
                if (HarmonyProjectBinary.ProjectPathToIdMap.TryGetValue(path, out int id))
                {
                    Debug.Log($"Deleted Harmony Project: {path}");
                    HarmonyProjectBinary.UnloadProjectById(id);
                    HarmonyProjectBinary.ProjectPathToIdMap.Remove(path);
                }
            }

            // Need to correct moved assets that are loaded, so we can cleanup their load in the native dll if they are deleted
            for (int i = 0; i < movedAssets.Length; ++i)
            {
                var newPath = movedAssets[i];
                var oldPath = movedFromAssetPaths[i];

                if (HarmonyProjectBinary.ProjectPathToIdMap.TryGetValue(oldPath, out int id))
                {
                    Debug.Log($"Moved Harmony Project From: {oldPath} To: {newPath}");
                    HarmonyProjectBinary.ProjectPathToIdMap.Remove(oldPath);
                    HarmonyProjectBinary.ProjectPathToIdMap[newPath] = id;
                }
            }
        }
    }
}
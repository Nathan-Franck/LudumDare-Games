using System;
using UnityEditor;
using UnityEngine;

namespace ToonBoom.Harmony
{
	public class HarmonyShaderGUI : ShaderGUI
	{
		private const string SAVED_PROJECT_KEY = "HarmonyShaderPreviewProject";
		private const string SAVED_CLIP_KEY = "HarmonyShaderPreviewClip";
		private HarmonyProject _harmonyProject;
		private HarmonyProjectPreview _harmonyProjectPreview;

		public HarmonyShaderGUI()
		{
			_harmonyProjectPreview = new HarmonyProjectPreview();
			LoadPreviewHarmonyProject();
		}

		private void LoadPreviewHarmonyProject()
		{
			var previewProjectGUIDString = EditorPrefs.GetString(SAVED_PROJECT_KEY);
			GUID previewProjectGUID = new GUID();
			if (!String.IsNullOrEmpty(previewProjectGUIDString))
			{
				GUID.TryParse(previewProjectGUIDString, out previewProjectGUID);
			}
			
			if (previewProjectGUID.Empty())
			{
				var harmonyProjects = AssetDatabase.FindAssets("t:HarmonyProject");
				if (harmonyProjects.Length > 0)
				{
					GUID.TryParse(harmonyProjects[0], out previewProjectGUID);
				}
			}

			if (!previewProjectGUID.Empty())
			{
				var assetPath = AssetDatabase.GUIDToAssetPath(previewProjectGUID);
				var harmonyProject = AssetDatabase.LoadAssetAtPath<HarmonyProject>(assetPath);
				if (harmonyProject)
				{
					_harmonyProject = harmonyProject;
					_harmonyProjectPreview.SetProject(harmonyProject);
					EditorPrefs.SetString(SAVED_PROJECT_KEY, previewProjectGUID.ToString());
					
					if(int.TryParse(EditorPrefs.GetString(SAVED_CLIP_KEY, "0"), out int clip))
					{
						_harmonyProjectPreview.SetClipIndex(clip);
					}
				}
			}
		}

		~HarmonyShaderGUI()
		{
			_harmonyProjectPreview.OnDisable();
			_harmonyProjectPreview = null;
		}
		
		// Force material editor to constant repaint or not 
		// Uses reflection to set private member as we can not override the material editor to do this, and there are no external sets
		private void SetRequiresConstantRepaint(MaterialEditor materialEditor, bool constantRepaint)
		{
			var prop = materialEditor.GetType().GetField("m_TimeUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			prop.SetValue(materialEditor, constantRepaint ? 1 : 0);
		}

		public override void OnMaterialPreviewGUI(MaterialEditor materialEditor, Rect rect, GUIStyle background)
		{
			var material = materialEditor.target as Material;
			if (material)
			{
				_harmonyProjectPreview.SetMaterial(material);
				_harmonyProjectPreview.DrawPreviewTexture(rect, background);
			}

			SetRequiresConstantRepaint(materialEditor, _harmonyProjectPreview.RequiresConstantRepaint());
		}

		public override void OnMaterialInteractivePreviewGUI(MaterialEditor materialEditor, Rect rect, GUIStyle background)
		{
			var material = materialEditor.target as Material;
			if (material)
			{
				_harmonyProject = EditorGUILayout.ObjectField("Preview Harmony Project", _harmonyProject, typeof(HarmonyProject), false) as HarmonyProject;
				_harmonyProjectPreview.SetProject(_harmonyProject);
				EditorPrefs.SetString(SAVED_PROJECT_KEY, AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(_harmonyProject)).ToString());

				_harmonyProjectPreview.SetMaterial(material);
				
				_harmonyProjectPreview.OnPreviewGUI(rect, background);

				var clip = _harmonyProjectPreview.GetClipIndex();
				EditorPrefs.SetString(SAVED_CLIP_KEY, clip.ToString());
			}
		}
		
		public override void OnMaterialPreviewSettingsGUI(MaterialEditor materialEditor)
		{
			_harmonyProjectPreview.OnPreviewSettings();
		}
		
		public override void OnClosed(Material material)
		{
			_harmonyProjectPreview.OnDisable();
			_harmonyProjectPreview = null;
		}
	}
}
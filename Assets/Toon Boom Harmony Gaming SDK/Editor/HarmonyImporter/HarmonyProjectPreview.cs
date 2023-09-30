
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace ToonBoom.Harmony
{
	[InitializeOnLoad]
	[Serializable]
	public class HarmonyProjectPreview
	{
		// Shared for all harmony project previews
		private static int s_numberOfHarmonyProjectPreviews = 0;
		private static PreviewRenderUtility s_previewRenderUtility;
		private static readonly GUIContent[] s_TimeIcons = new GUIContent[2];
		private static HarmonyRenderer s_previewHarmonyRenderer;
		private static Material s_defaultMaterial;
		private static bool s_animate = true;
		private static int _previewFrameRate = 30;
		private static bool _skinFoldout = false;
		
		// Set every draw for preview
		private HarmonyProject _project;
		private Material _material;
		private GroupSkinList _groupSkins;
		private int _clipIndex = 0;
		private float _frame = 0.0f;
		
		private Vector2 _rotate;
		private Vector2 _pan = Vector3.zero;
		private const int _zoomSpeed = 2;
		private int _zoom = 0;
		private float _startTime = 0.0f;
		

		// Cleanup on assembly reload
		static HarmonyProjectPreview()
		{
			AssemblyReloadEvents.beforeAssemblyReload += CleanUpPreviewRenderUtility;
			AssemblyReloadEvents.beforeAssemblyReload += CleanupPreviewHarmonyRenderer;
		}
		
		public void OnEnable()
		{
			++s_numberOfHarmonyProjectPreviews;
			_startTime = Time.realtimeSinceStartup;
		}

		public void OnDisable()
		{
			if (--s_numberOfHarmonyProjectPreviews == 0)
			{
				CleanUpPreviewRenderUtility();
				CleanupPreviewHarmonyRenderer();
			}
		}
		
		private static PreviewRenderUtility GetPreviewRendererUtility()
		{
			if (s_previewRenderUtility == null)
			{
				s_previewRenderUtility = new PreviewRenderUtility();
				s_previewRenderUtility.camera.fieldOfView = 30f;
				s_previewRenderUtility.lights[0].transform.localEulerAngles = new Vector3(30, 30, 0);
				s_previewRenderUtility.lights[0].intensity = 2;

				EditorUtility.SetCameraAnimateMaterials(s_previewRenderUtility.camera, true);
				
				
			}
			return s_previewRenderUtility;
		}

		private static void CleanUpPreviewRenderUtility()
		{
			if (s_previewRenderUtility == null)
				return;
			s_previewRenderUtility.Cleanup();
			s_previewRenderUtility = null;
		}

		private static HarmonyRenderer GetPreviewHarmonyRenderer()
		{
			if (s_previewHarmonyRenderer == null)
			{
				// Harmony Preview Game Object
				var gameObject = new GameObject($"Harmony Project Preview");
				gameObject.hideFlags = HideFlags.HideAndDontSave;

				s_previewHarmonyRenderer = gameObject.AddComponent<HarmonyRenderer>();
				s_defaultMaterial = s_previewHarmonyRenderer.Material;

				var previewRenderUtility = GetPreviewRendererUtility();
				previewRenderUtility.AddSingleGO(gameObject);
			}

			return s_previewHarmonyRenderer;
		}

		private static void CleanupPreviewHarmonyRenderer()
		{
			if (s_previewHarmonyRenderer == null)
				return;
			
			var gameObject = s_previewHarmonyRenderer.gameObject;
			Object.DestroyImmediate(gameObject);
			s_previewHarmonyRenderer = null;
		}
		
		public bool RequiresConstantRepaint()
		{
			return s_animate;
		}

		public void SetProject(HarmonyProject harmonyProject)
		{
			_project = harmonyProject;
			
			_groupSkins.Clear();
			if (_project != null)
			{
				for (int groupIdx = 0; groupIdx < _project.Groups.Count; ++groupIdx)
				{
					var groupName = _project.Groups[groupIdx];
					_groupSkins.Add(new GroupSkin(groupIdx, 0));
				}
			}
		}

		public int GetClipIndex()
		{
			return _clipIndex;
		}

		public void SetClipIndex(int clip)
		{
			_clipIndex = clip;
		}

		public void SetMaterial(Material material)
		{
			_material = material;
		}

		public void OnPreviewSettings()
		{
			if (!ShaderUtil.hardwareSupportsRectRenderTexture)
				return;
			
			// FPS
			GUILayout.Label("FPS");
			var fpsString = GUILayout.TextField(_previewFrameRate.ToString(), 3);
			int.TryParse(fpsString, out _previewFrameRate);
			
			// Static play/pause
			if (s_TimeIcons[0] == null)
			{
				s_TimeIcons[0] = EditorGUIUtility.TrIconContent("PlayButton");
				s_TimeIcons[1] = EditorGUIUtility.TrIconContent("PauseButton");

				if (bool.TryParse(EditorPrefs.GetString("HarmonyPreviewAnimating"), out bool animate))
				{
					s_animate = animate;
				}
			}

			// Animate or not
			GUIStyle toolbarButton = (GUIStyle) "toolbarbutton";
			if (GUILayout.Button(s_TimeIcons[s_animate ? 1 : 0], toolbarButton))
			{
				s_animate = !s_animate;
				EditorPrefs.SetString("HarmonyPreviewAnimating", s_animate.ToString());
			}

			// Reset Camera
			if (GUILayout.Button("Reset Camera", EditorStyles.whiteMiniLabel))
			{
				_rotate = Vector2.zero;
				_pan = Vector2.zero;
				_zoom = 0;
			}
		}

		public void OnPreviewGUI(Rect r, GUIStyle background)
		{
			if (!ShaderUtil.hardwareSupportsRectRenderTexture)
			{
				if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint)
					return;
				EditorGUI.DropShadowLabel(new Rect(r.x, r.y, r.width, 40f), "Material preview \nnot available");
				return;
			}
			
			if (_project == null)
				return;
			
			MouseInteract(r);
			ConfigurePreview();
			
			
			DrawPreviewTexture(r, background);
			
			// Animation
			_clipIndex = EditorGUILayout.Popup("Animation Clip", _clipIndex, _project.Clips.Select(clip => clip.DisplayName).ToArray());
			
			// Skin Controls
			if (_project.Groups.Count > 1 && _project.Skins.Count > 0)
			{
				_skinFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_skinFoldout, "Skins");
				if (_skinFoldout)
				{
					EditorGUI.indentLevel++;

					var groupNames = _project.Groups.ToArray();
					var skinNames = _project.Skins.ToArray();

					for (int groupIdx = 1; groupIdx < groupNames.Length; ++groupIdx)
					{
						var skins = new List<string>();
						var skinIdx = 0;
						for (int i = 0; i < _groupSkins.Count; ++i)
						{
							var groupSkin = _groupSkins[i];
							if (groupSkin.GroupId == groupIdx)
							{
								skins.Add(skinNames[groupSkin.SkinId]);
								skinIdx = groupSkin.SkinId;

								groupSkin.SkinId = EditorGUILayout.Popup(groupNames[groupIdx], groupSkin.SkinId, skinNames);
								_groupSkins[i] = groupSkin;
								break;
							}
						}
					}

					EditorGUI.indentLevel--;
				}

				EditorGUILayout.EndFoldoutHeaderGroup();
			}
		}

		private void MouseInteract(Rect position)
		{
			int controlID = GUIUtility.GetControlID("Test".GetHashCode(), FocusType.Passive);
			Event current = Event.current;
			switch (current.GetTypeForControl(controlID))
			{
				case EventType.MouseDown:
					if (position.Contains(current.mousePosition) && position.width > 50f)
					{
						GUIUtility.hotControl = controlID;
						current.Use();
						
						// Undocumented: when dragging something on windows (e.g. using rotation tool) make it possible to continuously drag mouse by jumping to the other side of screen when at the edge
						EditorGUIUtility.SetWantsMouseJumping(1);
					}

					break;
				case EventType.MouseUp:
					if (GUIUtility.hotControl == controlID)
					{
						GUIUtility.hotControl = 0;
					}

					// Set back
					EditorGUIUtility.SetWantsMouseJumping(0);
					break;
				case EventType.MouseDrag:
					if (GUIUtility.hotControl == controlID)
					{
						if (current.button == 2)
						{
							_pan -= current.delta;
						}
						else
						{
							_rotate -= current.delta * (float) ((!current.shift) ? 1 : 3) / Mathf.Min(position.width, position.height) * 140f;
							_rotate.y = Mathf.Clamp(_rotate.y, -90f, 90f);
						}

						current.Use();
						GUI.changed = true;
					}
					break;
				case EventType.ScrollWheel:
					if (position.Contains(current.mousePosition) && position.width > 50f)
					{
						_zoom -= (int)current.delta.y*2;
						_zoom = Mathf.Clamp(_zoom, -100, 100);
						current.Use();
					}
					break;
			}
		}
		
		private void ConfigurePreview()
		{
			if (_project)
			{
				_clipIndex = _project.ClampClipIndex(_clipIndex);
			}
			
			// Time Tick
			if (s_animate)
			{
				float timeSinceStart = Time.realtimeSinceStartup - _startTime;
				float frame = timeSinceStart * (float) _previewFrameRate;

				if (_project)
				{
					var clip = _project.GetClipByIndex(_clipIndex);
					int frameIndex = (int) frame % (int) clip.FrameCount;
					_frame = frameIndex;
				}
				else
				{
					_frame = (int)frame;
				}
				
			}
			
			// Need a material
			if (_material == null)
			{
				_material = s_defaultMaterial;
			}
			
			// Configure preview harmony renderer
			var harmonyRenderer = GetPreviewHarmonyRenderer();
			harmonyRenderer.Project = _project;
			harmonyRenderer.Material = _material;
			harmonyRenderer.GroupSkins = _groupSkins;
			harmonyRenderer.CurrentClipIndex = _clipIndex;
			harmonyRenderer.CurrentFrame = _frame;
			
			// Inline Update
			harmonyRenderer.UpdateRenderer();

			var previewRenderUtility = GetPreviewRendererUtility();
			var camera = previewRenderUtility.camera;
			
			Bounds bounds = harmonyRenderer.GetComponent<MeshFilter>().sharedMesh.bounds;
			Vector3 objectSizes = bounds.max - bounds.min;
			float largestObjectSize = Mathf.Max(objectSizes.x, objectSizes.y, objectSizes.z);
			
			// Visible height 1 unit from camera
			float cameraViewHeight = 2.0f * Mathf.Tan(0.5f * Mathf.Deg2Rad * camera.fieldOfView);
			
			// Combined wanted distance from the object
			float distanceToFill = largestObjectSize / cameraViewHeight;
			float distanceZoomOut = (largestObjectSize*2.0f) / cameraViewHeight;
			float distanceZoomIn = largestObjectSize*0.10f / cameraViewHeight;
			
			// V-Center on the object
			Vector3 origin = Vector3.zero;
			origin.y = bounds.center.y;
			camera.transform.position = origin;
			
			// Mouse rotation
			camera.transform.rotation = Quaternion.Euler(new Vector3(-_rotate.y, -_rotate.x, 0));
			
			// Zoom goes from -100 to 100
			float zoomPercent = (_zoom + 100) / 200.0f;
			float distance = Mathf.Lerp(distanceZoomOut, distanceZoomIn, zoomPercent);
			
			// Cap Panning keep object in bounds
			_pan.x = Mathf.Clamp(_pan.x, -largestObjectSize, largestObjectSize);
			_pan.y = Mathf.Clamp(_pan.y, -largestObjectSize, largestObjectSize);
			Vector3 pan = new Vector3(_pan.x, -_pan.y, 0.0f);
			
			// Camera Position Accounts for mesh origin & distance back to see the bounds (+/- zoom) & middle mouse panning
			camera.transform.position = origin - distance * camera.transform.forward + pan;

			// Don't allow near clip plane to go < 1
			float maxBoundsSize = bounds.extents.magnitude * 2;
			camera.nearClipPlane = Mathf.Max(distance - maxBoundsSize, 1.0f);
			camera.farClipPlane = distance + maxBoundsSize;
		}

		public void DrawPreviewTexture(Rect rect, GUIStyle background)
		{
			if (!ShaderUtil.hardwareSupportsRectRenderTexture)
				return;
			
			ConfigurePreview();
			
			var previewRenderUtility = GetPreviewRendererUtility();
			previewRenderUtility.BeginPreview(rect, background);
			previewRenderUtility.camera.Render();
			previewRenderUtility.EndAndDrawPreview(rect);
		}
		public RenderTexture CreatePreviewTexture(Rect rect, GUIStyle background)
		{
			if (!ShaderUtil.hardwareSupportsRectRenderTexture)
				return null;
			
			ConfigurePreview();
			
			var previewRenderUtility = GetPreviewRendererUtility();
			previewRenderUtility.BeginPreview(rect, background);
			previewRenderUtility.camera.Render();
			return (RenderTexture)previewRenderUtility.EndPreview();
		}
		public Texture2D CreateStaticPreviewTexture(Rect rect)
		{
			if (!ShaderUtil.hardwareSupportsRectRenderTexture)
				return null;
			
			ConfigurePreview();
			
			var previewRenderUtility = GetPreviewRendererUtility();
			previewRenderUtility.BeginStaticPreview(rect);
			previewRenderUtility.camera.Render();
			return previewRenderUtility.EndStaticPreview();
		}
		
	}
}
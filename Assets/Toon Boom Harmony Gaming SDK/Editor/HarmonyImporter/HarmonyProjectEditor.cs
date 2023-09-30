using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToonBoom.Harmony
{
	[CustomEditor(typeof(HarmonyProject), true)]
	public class HarmonyProjectEditor : Editor
	{
		private HarmonyProjectPreview _harmonyProjectPreview;

		//https://forum.unity.com/threads/drag-and-drop-scriptable-object-to-scene.546975/
		private void OnSceneDrag(SceneView sceneView, int index)
		{
			Event e = Event.current;
			GameObject gameObject = HandleUtility.PickGameObject(e.mousePosition, false);
			HarmonyRenderer harmonyRenderer = gameObject ? gameObject.GetComponent<HarmonyRenderer>() : null;
 
			if (e.type == EventType.DragUpdated)
			{
				if (harmonyRenderer)
				{
					// Would set the Project on the renderer
					DragAndDrop.visualMode = DragAndDropVisualMode.Link;
				}
				else
				{
					// Would make a new game object instead
					DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
				}
				e.Use();
			}
			else if (e.type == EventType.DragPerform)
			{
				DragAndDrop.AcceptDrag();
				e.Use();
				
				// If nothing with a harmonyRenderer was hit, we will be creating a new game object
				if (!harmonyRenderer)
				{
					// Cast from mouse into world to determine where to create a game object
					Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
					Vector3 spawnPosition;
					
					// Is there a physics mesh we can use to determine where to spawn?
					if (Physics.Raycast(ray, out RaycastHit hitInfo))
					{
						spawnPosition = hitInfo.point;
						Debug.Log($"Spawn at physics intersection {spawnPosition} with {hitInfo.collider.gameObject}");
					}
					else
					{
						// No physics hit, cast against y=0 plane to put on the 'ground'
						Plane floor = new Plane(Vector3.up, Vector3.zero);
						if (floor.Raycast(ray, out float enter))
						{
							spawnPosition = ray.GetPoint(enter);
							Debug.Log($"Spawn at floor intersection  {spawnPosition}");
						}
						else
						{
							// No ground hit, just put in camera space
							spawnPosition = Camera.current.ScreenToWorldPoint(new Vector3(e.mousePosition.x, e.mousePosition.y, (Camera.current.farClipPlane - Camera.current.nearClipPlane) / 2.0f));
							Debug.Log($"Spawn in camera space {spawnPosition}");
						}
					}
					
					// Spawn GameObject
					gameObject = new GameObject(target.name);
					gameObject.transform.position = spawnPosition;
					gameObject.SetActive(false); // Activating after HarmonyProject set prevents warning about harmony renderer not having a project set
					// Configure Harmony
					harmonyRenderer = gameObject.AddComponent<HarmonyRenderer>();
					harmonyRenderer.Project = target as HarmonyProject;
					
					gameObject.SetActive(true);
					harmonyRenderer.UpdateRenderer();
					
					// Select newly created game object
					Selection.activeGameObject = gameObject;
				}
				else
				{
					// Set the project on the renderer
					harmonyRenderer.Project = target as HarmonyProject;
					harmonyRenderer.UpdateRenderer();
					// Select the newly updated game object
					Selection.activeGameObject = gameObject;
				}
			}
		}

		public override bool HasPreviewGUI()
		{
			var harmonyProject = target as HarmonyProject;
			return harmonyProject != null && harmonyProject.IsValid();
		}
		
		public override void OnPreviewGUI(Rect r, GUIStyle background)
		{
			_harmonyProjectPreview.OnPreviewGUI(r, background);
		}

		public override void OnPreviewSettings()
		{
			_harmonyProjectPreview.OnPreviewSettings();
		}

		private void OnEnable()
		{
			var harmonyProject = target as HarmonyProject;

			_harmonyProjectPreview = new HarmonyProjectPreview();
			_harmonyProjectPreview.OnEnable();
			_harmonyProjectPreview.SetProject(harmonyProject);
		}
		
		override public bool RequiresConstantRepaint()
		{
			return _harmonyProjectPreview.RequiresConstantRepaint();
		}

		private void OnDisable()
		{
			_harmonyProjectPreview.OnDisable();
		}
		
		public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
		{
			Rect r = new Rect(Vector2.zero, new Vector2(width, height));
			var staticPreviewTexture = _harmonyProjectPreview.CreateStaticPreviewTexture(r);
			
			Texture2D tex = new Texture2D (width, height);
			EditorUtility.CopySerialized (staticPreviewTexture, tex);

			return tex;
		}
	}
}


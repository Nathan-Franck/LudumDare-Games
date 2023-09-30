#if ENABLE_UNITY_2D_ANIMATION && ENABLE_UNITY_COLLECTIONS

using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace ToonBoom.TBGImporter
{
    public struct RenderState
    {
        public GameObject instance;
        public PreviewRenderUtility previewRenderUtility;

        public bool Initialize(Object target)
        {
            if (this.previewRenderUtility != null)
                return false;
            var assetPath = AssetDatabase.GetAssetPath(target);
            var gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (gameObject == null)
                return false;
            this.previewRenderUtility = new PreviewRenderUtility();
            if (this.instance == null)
                this.instance = this.previewRenderUtility.InstantiatePrefabInScene(gameObject);
            this.previewRenderUtility.AddSingleGO(this.instance);
            var camera = this.previewRenderUtility.camera;
            camera.orthographic = true;
            var renderers = this.instance.GetComponentsInChildren<SpriteRenderer>();
            var bounds = new Bounds();
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 0.5f;
            camera.transform.position = bounds.center - Vector3.forward * 4;
            EditorUtility.SetCameraAnimateMaterials(this.previewRenderUtility.camera, true);
            var animator = instance.GetComponent<Animator>();
            if (animator != null)
            {
                var layer = animator.GetLayerName(0);
                var state = animator.GetNextAnimatorStateInfo(0).fullPathHash;
                animator.Play(state, 0, 0);
                animator.enabled = true;
                animator.Update(0);
            }
            return true;
        }
        public Texture Render(Rect rect, GUIStyle background)
        {
            previewRenderUtility.BeginPreview(rect, background);
            previewRenderUtility.camera.Render();
            return previewRenderUtility.EndPreview();
        }
        public Texture2D RenderStatic(Rect rect)
        {
            previewRenderUtility.BeginStaticPreview(rect);
            previewRenderUtility.camera.Render();
            return previewRenderUtility.EndStaticPreview();
        }
        public void Destroy()
        {
            if (previewRenderUtility != null)
            {
                previewRenderUtility.Cleanup();
                previewRenderUtility = null;
            }
            if (instance == null)
                GameObject.DestroyImmediate(instance);
        }
    }
    [CustomEditor(typeof(TBGImporter), true)]
    public class TBGImporterEditor : ScriptedImporterEditor
    {
        public override bool HasPreviewGUI()
        {
            return true;
        }
        protected override bool useAssetDrawPreview => false;
        private static GUIContent[] timeIcons = new GUIContent[2];
        private static bool shouldAnimate = true;
        private static float lastFrameTime = 0;
        private static RenderState renderState;

        public override void OnPreviewSettings()
        {
            if (!ShaderUtil.hardwareSupportsRectRenderTexture)
                return;

            if (timeIcons[0] == null)
            {
                timeIcons[0] = EditorGUIUtility.TrIconContent("PlayButton");
                timeIcons[1] = EditorGUIUtility.TrIconContent("PauseButton");

                if (bool.TryParse(EditorPrefs.GetString("HarmonyPreviewAnimating"), out bool animate))
                {
                    shouldAnimate = animate;
                }
            }

            GUIStyle toolbarButton = (GUIStyle)"toolbarbutton";
            if (GUILayout.Button(timeIcons[shouldAnimate ? 1 : 0], toolbarButton))
            {
                shouldAnimate = !shouldAnimate;
                EditorPrefs.SetString("HarmonyPreviewAnimating", shouldAnimate.ToString());
            }
        }
        public override bool RequiresConstantRepaint()
        {
            return shouldAnimate;
        }
        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            // Reinit.
            renderState.Initialize(target);

            // Every frame.
            {
                var animator = renderState.instance.GetComponent<Animator>();
                if (animator != null)
                {
                    var previewUpdateDelta = Time.realtimeSinceStartup - lastFrameTime;
                    if (shouldAnimate)
                    {
                        animator.Update(previewUpdateDelta);
                    }
                    lastFrameTime = Time.realtimeSinceStartup;
                }
                var texture = renderState.Render(rect, background);
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
            }
        }
        public override void OnDisable()
        {
            renderState.Destroy();
            base.OnDisable();
        }
        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            // BUG - this is never called from Unity
            // Debug.Log("RenderStaticPreview");

            // var pinkTex = new Texture2D(1, 1);
            // pinkTex.SetPixel(0, 0, Color.magenta);
            // return pinkTex; // If this is returned, the preview is a magenta square.

            renderState.Initialize(target);
            return renderState.RenderStatic(new Rect(0, 0, width, height));
        }
    }
}

#endif
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rendering
{
    public class BackgroundRendererFeature : ScriptableRendererFeature
    {
        private static readonly int INNER_COLOR_ID = Shader.PropertyToID("_InnerColor");
        private static readonly int OUTER_COLOR_ID = Shader.PropertyToID("_OuterColor");
        private static readonly int BACKGROUND_CENTER_ID = Shader.PropertyToID("_BackgroundCenter");
        private static readonly int BACKGROUND_SIZE_ID = Shader.PropertyToID("_BackgroundSize");
        private static readonly int HIGHLIGHT_COLOR_ID = Shader.PropertyToID("_HighlightColor");
        private static readonly int HIGHLIGHT_CENTER_ID = Shader.PropertyToID("_HighlightCenter");
        private static readonly int HIGHLIGHT_SIZE_ID = Shader.PropertyToID("_HighlightSize");

        public static Bounds? HighlightBounds { get; set; }

        [SerializeField] private BackgroundSettings backgroundSettings;
        [SerializeField] private Shader shader;
        [SerializeField] private RenderPassEvent renderPassEvent;

        private Material material;
        private BackgroundRenderPass renderPass;

        public override void Create()
        {
            if (shader == null)
            {
                return;
            }

            material = new Material(shader);
            material.SetColor(INNER_COLOR_ID, backgroundSettings.inner);
            material.SetColor(OUTER_COLOR_ID, backgroundSettings.outer);
            material.SetColor(HIGHLIGHT_COLOR_ID, backgroundSettings.highlightColor);
            material.SetVector(BACKGROUND_CENTER_ID, backgroundSettings.center);
            material.SetFloat(BACKGROUND_SIZE_ID, backgroundSettings.size);

            renderPass = new BackgroundRenderPass(material)
            {
                renderPassEvent = renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderPass == null)
            {
                return;
            }

            if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                UpdateMaterial();
                renderer.EnqueuePass(renderPass);
            }
        }

        private void UpdateMaterial()
        {
            if (HighlightBounds.HasValue)
            {
                var bounds = HighlightBounds.Value;

                material.SetVector(HIGHLIGHT_CENTER_ID, bounds.center);
                material.SetVector(HIGHLIGHT_SIZE_ID, bounds.size);
            }
            else
            {
                material.SetVector(HIGHLIGHT_CENTER_ID, Vector2.zero);
                material.SetVector(HIGHLIGHT_SIZE_ID, Vector2.zero);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }

            material = null;
            HighlightBounds = null;
        }
    }

    [Serializable]
    public class BackgroundSettings
    {
        public Color inner;
        public Color outer;
        public Vector2 center;
        public float size;


        [Header("Dynamic Highlight")] public Color highlightColor;
    }
}
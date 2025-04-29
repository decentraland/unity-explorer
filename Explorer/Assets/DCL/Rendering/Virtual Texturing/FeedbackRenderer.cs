using System;
using UnityEngine;

namespace VirtualTexture
{
    /// <summary>
    /// Renders a specialized feedback buffer for virtual texturing.
    /// 
    /// The feedback renderer uses a dedicated camera and shader to render the scene,
    /// capturing information about which virtual texture pages are needed by the current view.
    /// This information includes page table indices and mipmap levels required by visible objects.
    /// </summary>
    public class FeedbackRenderer : MonoBehaviour, IFeedbackRenderer
    {
        /// <summary>
        /// Event fired when feedback rendering is complete, providing the resulting texture.
        /// </summary>
        public event Action<RenderTexture> OnFeedbackRenderComplete;

        /// <summary>
        /// Scale factor applied to the feedback buffer relative to the main camera's resolution.
        /// Lower resolutions improve performance at the cost of precision.
        /// </summary>
        [SerializeField]
        private ScaleFactor m_Scale = default;

        /// <summary>
        /// Specialized shader used for rendering the feedback buffer.
        /// This shader outputs page table coordinates and mipmap levels instead of colors.
        /// </summary>
        [SerializeField]
        private Shader m_FeedbackShader = default;

        /// <summary>
        /// Bias applied to mipmap level selection.
        /// Positive values increase quality by selecting more detailed mipmap levels,
        /// while negative values improve performance by selecting less detailed mipmap levels.
        /// </summary>
        [SerializeField]
        private int m_MipmapBias = default;

        /// <summary>
        /// The camera used for rendering the feedback buffer.
        /// </summary>
        private Camera m_FeedbackCamera;

        /// <summary>
        /// The render target for the feedback buffer.
        /// </summary>
        public RenderTexture TargetTexture { get; private set; }

        /// <summary>
        /// Performance statistics for the rendering operation.
        /// </summary>
        public FrameStat Stat { get; private set; } = new FrameStat();

        private void Start()
        {
            InitCamera();
        }

        private void OnPreCull()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            // Handle screen size changes and resize the feedback buffer if needed
            var scale = m_Scale.ToFloat();
            var width = (int)(mainCamera.pixelWidth * scale);
            var height = (int)(mainCamera.pixelHeight * scale);
            if (TargetTexture == null || TargetTexture.width != width || TargetTexture.height != height)
            {
                TargetTexture = new RenderTexture(width, height, 0);
                TargetTexture.useMipMap = false;
                TargetTexture.wrapMode = TextureWrapMode.Clamp;
                TargetTexture.filterMode = FilterMode.Point;

                m_FeedbackCamera.targetTexture = TargetTexture;

                // Set global shader parameters for the feedback rendering system
                // x: Page table size (in tiles)
                // y: Virtual texture size (in pixels)
                // z: Maximum mipmap level
                // w: Mipmap bias
                var tileTexture = GetComponent(typeof(ITiledTexture)) as ITiledTexture;
                var virtualTable = GetComponent(typeof(IPageTable)) as IPageTable;
                Shader.SetGlobalVector(
                    "_VTFeedbackParam",
                    new Vector4(virtualTable.TableSize,
                                virtualTable.TableSize * tileTexture.TileSize * scale,
                                virtualTable.MaxMipLevel,
                                m_MipmapBias));
            }

            // Synchronize feedback camera with main camera parameters
            CopyCamera(mainCamera);
        }

        private void OnPreRender()
        {
            Stat.BeginFrame();
        }

        private void OnPostRender()
        {
            if (TargetTexture == null)
                return;

            Stat.EndFrame();

            // Notify subscribers that feedback rendering is complete
            OnFeedbackRenderComplete?.Invoke(TargetTexture);
        }

        /// <summary>
        /// Initializes the feedback camera with appropriate settings.
        /// </summary>
        private void InitCamera()
        {
            m_FeedbackCamera = GetComponent<Camera>();
            if(m_FeedbackCamera == null)
                m_FeedbackCamera = gameObject.AddComponent<Camera>();

            m_FeedbackCamera.allowHDR = false;
            m_FeedbackCamera.allowMSAA = false;
            m_FeedbackCamera.renderingPath = RenderingPath.Forward;

            // Clear render target to white (RGBA: 255,255,255,255)
            // In the feedback system, a mipmap level of 255 indicates that the pixel can be skipped
            m_FeedbackCamera.clearFlags = CameraClearFlags.Color;
            m_FeedbackCamera.backgroundColor = Color.white;

            // Set up shader replacement
            // The "VirtualTextureType" tag in materials allows the system to
            // automatically filter out non-VT objects during feedback rendering
            m_FeedbackCamera.SetReplacementShader(m_FeedbackShader, "VirtualTextureType");
        }

        /// <summary>
        /// Synchronizes the feedback camera with main camera parameters.
        /// </summary>
        /// <param name="camera">The source camera to copy parameters from</param>
        private void CopyCamera(Camera camera)
        {
            if(camera == null)
                return;

            // Manually copy specific camera parameters
            // We avoid Camera.CopyFrom() as it would copy all parameters, which we don't want
            m_FeedbackCamera.transform.position = camera.transform.position;
            m_FeedbackCamera.transform.rotation = camera.transform.rotation;
            m_FeedbackCamera.cullingMask = camera.cullingMask;
            m_FeedbackCamera.projectionMatrix = camera.projectionMatrix;
            m_FeedbackCamera.fieldOfView = camera.fieldOfView;
            m_FeedbackCamera.nearClipPlane = camera.nearClipPlane;
            m_FeedbackCamera.farClipPlane = camera.farClipPlane;
        }
    }
}
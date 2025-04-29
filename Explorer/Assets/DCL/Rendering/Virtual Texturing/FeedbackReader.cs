using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace VirtualTexture
{
    /// <summary>
    /// Handles readback of feedback buffer from GPU to CPU.
    /// 
    /// The FeedbackReader is responsible for reading back the pre-rendered feedback
    /// texture from GPU memory to CPU memory. This feedback texture contains information
    /// about which virtual texture pages are needed for the current view, allowing the
    /// system to load only the necessary texture data.
    /// </summary>
    public class FeedbackReader : MonoBehaviour, IFeedbackReader
    {
        /// <summary>
        /// Event fired when feedback texture readback is complete.
        /// Subscribers can use this to process the feedback data.
        /// </summary>
        public event Action<Texture2D> OnFeedbackReadComplete;

        /// <summary>
        /// Scale factor applied to the feedback buffer before readback.
        /// Lower resolutions improve performance but may reduce precision.
        /// </summary>
        [SerializeField]
        private ScaleFactor m_ReadbackScale = default;

        /// <summary>
        /// Shader used for downscaling the feedback texture.
        /// 
        /// This shader implements specific downscaling logic for feedback textures: 
        /// finding the pixel with the smallest mipmap level in each region and 
        /// discarding the rest, ensuring the most detailed texture requests are preserved.
        /// </summary>
        [SerializeField]
        private Shader m_DownScaleShader = default;

        /// <summary>
        /// Shader used for visualizing mipmap levels in the editor.
        /// Only active when ENABLE_DEBUG_TEXTURE is defined.
        /// </summary>
        [SerializeField]
        private Shader m_DebugShader = default;

        /// <summary>
        /// Material instance of the downscale shader.
        /// </summary>
        private Material m_DownScaleMaterial;

        /// <summary>
        /// Pass index used with the downscale material, determined by the scale factor.
        /// </summary>
        private int m_DownScaleMaterialPass;

        /// <summary>
        /// Render texture containing the downscaled feedback data.
        /// </summary>
        private RenderTexture m_DownScaleTexture;

        /// <summary>
        /// Material instance of the debug shader for visualizing mipmap levels.
        /// </summary>
        private Material m_DebugMaterial;

        /// <summary>
        /// Queue of active GPU readback requests.
        /// </summary>
        private Queue<AsyncGPUReadbackRequest> m_ReadbackRequests = new Queue<AsyncGPUReadbackRequest>();

        /// <summary>
        /// Texture2D holding the readback data from GPU after copying to CPU memory.
        /// </summary>
        private Texture2D m_ReadbackTexture;

        /// <summary>
        /// Performance statistics for the update operation.
        /// </summary>
        public FrameStat UpdateStat { get; private set; } = new FrameStat();

        /// <summary>
        /// Performance statistics for the GPU readback operation.
        /// </summary>
        public ReadbackStat ReadbackStat { get; } = new ReadbackStat();

        /// <summary>
        /// Debug texture for visualizing mipmap levels.
        /// Only available when ENABLE_DEBUG_TEXTURE is defined.
        /// </summary>
        public RenderTexture DebugTexture { get; private set; }

        private void Start()
        {
            if (m_ReadbackScale != ScaleFactor.One)
            {
                m_DownScaleMaterial = new Material(m_DownScaleShader);

                // Select the appropriate shader pass based on the scale factor
                switch(m_ReadbackScale)
                {
                case ScaleFactor.Half:
                    m_DownScaleMaterialPass = 0;
                    break;
                case ScaleFactor.Quarter:
                    m_DownScaleMaterialPass = 1;
                    break;
                case ScaleFactor.Eighth:
                    m_DownScaleMaterialPass = 2;
                    break;
                }
            }

            // Subscribe to the feedback render complete event
            var renderer = GetComponent(typeof(IFeedbackRenderer)) as IFeedbackRenderer;
            renderer.OnFeedbackRenderComplete += NewRequest;
        }

        private void Update()
        {
            UpdateRequest();
        }

        /// <summary>
        /// Initiates a new readback request for the provided render texture.
        /// Downscales the texture if necessary before starting the readback.
        /// </summary>
        /// <param name="texture">The feedback render texture to read back</param>
        private void NewRequest(RenderTexture texture)
        {
            // Limit the number of in-flight readback requests to prevent GPU stalls
            if(m_ReadbackRequests.Count > 8)
                return;

            // Calculate dimensions for the scaled texture
            var width = (int)(texture.width * m_ReadbackScale.ToFloat());
            var height = (int)(texture.height * m_ReadbackScale.ToFloat());

            // Apply downscaling if needed
            if (m_ReadbackScale != ScaleFactor.One)
            {
                if (m_DownScaleTexture == null || m_DownScaleTexture.width != width || m_DownScaleTexture.height != height)
                {
                    m_DownScaleTexture = new RenderTexture(width, height, 0);
                }

                m_DownScaleTexture.DiscardContents();
                Graphics.Blit(texture, m_DownScaleTexture, m_DownScaleMaterial, m_DownScaleMaterialPass);
                texture = m_DownScaleTexture;
            }

            // Create or resize the readback texture if needed
            if (m_ReadbackTexture == null || m_ReadbackTexture.width != width || m_ReadbackTexture.height != height)
            {
                m_ReadbackTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                m_ReadbackTexture.filterMode = FilterMode.Point;
                m_ReadbackTexture.wrapMode = TextureWrapMode.Clamp;

                InitDebugTexture(width, height);
            }

            // Initiate an asynchronous GPU readback request
            var request = AsyncGPUReadback.Request(texture);
            m_ReadbackRequests.Enqueue(request);

            ReadbackStat.BeginRequest(request);
        }

        /// <summary>
        /// Processes pending readback requests, updating textures when readbacks complete.
        /// </summary>
        private void UpdateRequest()
        {
            UpdateStat.BeginFrame();

            bool complete = false;
            while(m_ReadbackRequests.Count > 0)
            {
                var req = m_ReadbackRequests.Peek();

                if(req.hasError)
                {
                    ReadbackStat.EndRequest(req, false);
                    m_ReadbackRequests.Dequeue();
                }
                else if(req.done)
                {
                    // Update texture data and mark as complete
                    m_ReadbackTexture.GetRawTextureData<Color32>().CopyFrom(req.GetData<Color32>());
                    complete = true;

                    ReadbackStat.EndRequest(req, true);
                    m_ReadbackRequests.Dequeue();
                }
                else
                {
                    // Request not yet complete, try again next frame
                    break;
                }
            }

            if (complete)
            {
                UpdateStat.EndFrame();

                // Notify subscribers that the feedback readback is complete
                OnFeedbackReadComplete?.Invoke(m_ReadbackTexture);

                // Update debug visualization if enabled
                UpdateDebugTexture();
            }
        }

        /// <summary>
        /// Initializes the debug visualization texture.
        /// Only compiled when ENABLE_DEBUG_TEXTURE is defined.
        /// </summary>
        /// <param name="width">Width of the debug texture</param>
        /// <param name="height">Height of the debug texture</param>
        [Conditional("ENABLE_DEBUG_TEXTURE")]
        private void InitDebugTexture(int width, int height)
        {
            DebugTexture = new RenderTexture(width, height, 0);
            DebugTexture.filterMode = FilterMode.Point;
            DebugTexture.wrapMode = TextureWrapMode.Clamp;
        }

        /// <summary>
        /// Updates the debug visualization texture with current mipmap level data.
        /// Only compiled when ENABLE_DEBUG_TEXTURE is defined.
        /// </summary>
        [Conditional("ENABLE_DEBUG_TEXTURE")]
        protected void UpdateDebugTexture()
        {
            if(m_ReadbackTexture == null || m_DebugShader == null)
                return;

            if(m_DebugMaterial == null)
                m_DebugMaterial = new Material(m_DebugShader);

            m_ReadbackTexture.Apply(false);

            DebugTexture.DiscardContents();
            Graphics.Blit(m_ReadbackTexture, DebugTexture, m_DebugMaterial);
        }
    }
}
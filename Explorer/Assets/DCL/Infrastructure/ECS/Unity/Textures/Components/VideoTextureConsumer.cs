using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.Textures.Components
{
    public struct VideoTextureConsumer : IDisposable
    {
        private readonly IObjectPool<RenderTexture> videoTexturesPool;

        /// <summary>
        /// Gets the current world position of the maximum corner of the bounding box that contains all the mesh renderers used by video consumers of one texture.
        /// </summary>
        public Vector3 BoundsMax
        {
            get
            {
                Vector3 boundsMax = Vector3.one * float.MinValue;

                for (int i = 0; i < renderers.Count; ++i)
                {
                    Vector3 inputBoundsMax = renderers[i].bounds.max;
                    boundsMax = new Vector3(Mathf.Max(inputBoundsMax.x, boundsMax.x), Mathf.Max(inputBoundsMax.y, boundsMax.y), Mathf.Max(inputBoundsMax.z, boundsMax.z));
                }

                return boundsMax;
            }
        }

        /// <summary>
        /// Gets the current world position of the minimum corner of the bounding box that contains all the mesh renderers used by video consumers of one texture.
        /// </summary>
        public Vector3 BoundsMin
        {
            get
            {
                Vector3 boundsMin = Vector3.one * float.MaxValue;

                for (int i = 0; i < renderers.Count; ++i)
                {
                    Vector3 inputBoundsMin = renderers[i].bounds.min;
                    boundsMin = new Vector3(Mathf.Min(inputBoundsMin.x, boundsMin.x), Mathf.Min(inputBoundsMin.y, boundsMin.y), Mathf.Min(inputBoundsMin.z, boundsMin.z));
                }

                return boundsMin;
            }
        }

        // All the renderers that use the video texture
        private readonly List<Renderer> renderers;

        public RenderTexture Texture { get; }

        /// <summary>
        ///     The single copy kept for the single Entity with VideoPlayer,
        ///     we don't use the original texture from AVPro
        /// </summary>

        // public Texture2DData Texture { get; private set; }

        // public bool IsDirty;
        public VideoTextureConsumer(IObjectPool<RenderTexture> videoTexturesPool)
        {
            this.videoTexturesPool = videoTexturesPool;
            Texture = videoTexturesPool.Get();

            // TODO should be pooled
            renderers = new List<Renderer>();
        }

        public void Dispose()
        {
            videoTexturesPool.Release(Texture);
            renderers.Clear();
        }

        /// <summary>
        /// Stores a reference to a renderer that consumes the same texture.
        /// </summary>
        /// <param name="renderer">The renderer using the video texture.</param>
        public void AddConsumer(Renderer renderer)
        {
            renderers.Add(renderer);
        }

        /// <summary>
        /// Removes a reference to a renderer that was consuming the same texture.
        /// </summary>
        /// <param name="renderer">The renderer to stop referencing to.</param>
        public void RemoveConsumer(Renderer renderer)
        {
            renderers.Remove(renderer);
        }

        public void Resize(int width, int height)
        {
            if (Texture.IsCreated())
                Texture.Release();

            Texture.width = width;
            Texture.height = height;

            Texture.Create();
        }
    }
}

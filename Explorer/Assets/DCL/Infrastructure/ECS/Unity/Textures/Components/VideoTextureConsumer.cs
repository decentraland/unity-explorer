using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.Textures.Components
{
    public struct VideoTextureConsumer : IDisposable
    {
        private readonly IObjectPool<RenderTexture> videoTexturesPool;

        // All the renderers that use the video texture
        private readonly List<Renderer> renderers;

        /// <summary>
        ///     The single copy kept for the single Entity with VideoPlayer,
        ///     we don't use the original texture from AVPro
        /// </summary>
        public RenderTexture Texture { get; }

        /// <summary>
        ///     Consumers without a renderer that draw the texture over the whole screen (the scene skybox).
        ///     Mutate it through <see cref="AddScreenSpaceConsumer" /> / <see cref="RemoveScreenSpaceConsumer" />
        ///     on a reference to the component stored in the world, never on a copy.
        /// </summary>
        public int ScreenSpaceConsumers { get; private set; }

        public VideoTextureConsumer(IObjectPool<RenderTexture> videoTexturesPool)
        {
            this.videoTexturesPool = videoTexturesPool;
            Texture = videoTexturesPool.Get();
            ScreenSpaceConsumers = 0;

            // TODO should be pooled
            renderers = new List<Renderer>();
        }

        public void Dispose()
        {
            // On application exit Unity destroys the render texture before the world is finalized;
            // a destroyed texture must not go back to the pool where the release callback would touch its native side
            if (Texture != null)
                videoTexturesPool.Release(Texture);

            renderers.Clear();
            ScreenSpaceConsumers = 0;
        }

        public (Vector3 min, Vector3 max) GetBounds()
        {
            Vector3 boundsMin = Vector3.one * float.MaxValue;
            Vector3 boundsMax = Vector3.one * float.MinValue;

            for (int i = 0; i < renderers.Count; ++i)
            {
                Bounds bounds = renderers[i].bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;

                boundsMin = new Vector3(
                    Mathf.Min(min.x, boundsMin.x),
                    Mathf.Min(min.y, boundsMin.y),
                    Mathf.Min(min.z, boundsMin.z));

                boundsMax = new Vector3(
                    Mathf.Max(max.x, boundsMax.x),
                    Mathf.Max(max.y, boundsMax.y),
                    Mathf.Max(max.z, boundsMax.z));
            }

            return (boundsMin, boundsMax);
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

        /// <summary>
        /// Counts one more consumer that draws the texture over the whole screen without a renderer.
        /// </summary>
        public void AddScreenSpaceConsumer()
        {
            ScreenSpaceConsumers++;
        }

        /// <summary>
        /// Counts one screen-space consumer less; the counter never goes below zero.
        /// </summary>
        public void RemoveScreenSpaceConsumer()
        {
            ScreenSpaceConsumers = Mathf.Max(0, ScreenSpaceConsumers - 1);
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

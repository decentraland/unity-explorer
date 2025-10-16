using ECS.StreamableLoading.Textures;
using System;
using System.Collections.Generic;
using DCL.Shaders;
using UnityEngine;

namespace ECS.Unity.Textures.Components
{
    public struct VideoTextureConsumer : IDisposable
    {
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
        private List<Renderer> renderers;

        public int referenceCount;

        /// <summary>
        ///     The single copy kept for the single Entity with VideoPlayer,
        ///     we don't use the original texture from AVPro
        /// </summary>
        public RenderTexture Texture { get; private set; }

        public static VideoTextureConsumer CreateVideoTextureConsumer()
        {
            VideoTextureConsumer consumer = new VideoTextureConsumer();

            RenderTexture texture = new RenderTexture(1, 1, 0, RenderTextureFormat.BGRA32)
            {
                useMipMap = false,
                autoGenerateMips = false,
            };
            texture.Create();
            consumer.Texture = texture;
            consumer.renderers = new List<Renderer>();
            consumer.referenceCount = 0;

            return consumer;
        }


        public void Dispose()
        {
            // On Dispose video textures are dereferenced by material that acquired it
            Texture = null!;
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

        public void AddReference()
        {
            referenceCount++;
        }

        public void DecreaseReference()
        {
            referenceCount--;
        }

        public void ResizeAndReassing(Texture to)
        {
            Texture.Release();
            Texture.width = to.width;
            Texture.height = to.height;
            Texture.Create();
        }

    }
}

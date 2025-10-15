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

        /// <summary>
        ///     The single copy kept for the single Entity with VideoPlayer,
        ///     we don't use the original texture from AVPro
        /// </summary>
        public RenderTexture Texture { get; private set; }



        public static VideoTextureConsumer CreateVideoTextureConsumer()
        {
            // This allows to clear the existing data on the texture,
            // to avoid "ghost" images in the textures before they are loaded with new data,
            // particularly when dealing with streaming textures from videos
            /*texture.Reinitialize(1, 1);
            texture.SetPixel(0, 0, Color.clear);
            texture.Apply();*/


            VideoTextureConsumer consumer = new VideoTextureConsumer();

            RenderTexture Texture = new RenderTexture(1, 1, 0, RenderTextureFormat.BGRA32)
            {
                useMipMap = false,
                autoGenerateMips = false,
            };
            Texture.Create();
            consumer.Texture = Texture;
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

        public void SetTextureScale(Vector2 texScale)
        {
            foreach (var meshRenderer in renderers)
            {
                meshRenderer.sharedMaterial.SetTextureScale(ShaderUtils.BaseMap, texScale);
                meshRenderer.sharedMaterial.SetTextureScale(ShaderUtils.AlphaTexture, texScale);
            }
        }

        public int referenceCount;

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
            //TODO (Leak?)
            Texture = new RenderTexture(to.width, to.height, 0, RenderTextureFormat.BGRA32)
            {
                useMipMap = false,
                autoGenerateMips = false,
            };
            Texture.Create();

            //TODO (Will the renderer be assigned at this point)
            foreach (var meshRenderer in renderers)
                meshRenderer.sharedMaterial.SetTexture(ShaderUtils.BaseMap, Texture);
        }

    }
}

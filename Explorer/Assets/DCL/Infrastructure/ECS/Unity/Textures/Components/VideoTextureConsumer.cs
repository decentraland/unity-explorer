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
        public readonly List<Renderer> renderers;

        /// <summary>
        ///     The single copy kept for the single Entity with VideoPlayer,
        ///     we don't use the original texture from AVPro
        /// </summary>
        public Texture2DData Texture { get; private set; }

        public bool IsDirty;

        // Track the last applied texture scale to detect changes and handle material updates
        private Vector2 lastAppliedTextureScale;
        private bool hasAppliedScale;

        public int ConsumersCount => Texture.referenceCount;

        public VideoTextureConsumer(Texture2D texture)
        {
            Texture = new Texture2DData(texture);
            renderers = new List<Renderer>();
            IsDirty = false;
            lastAppliedTextureScale = Vector2.zero;
            hasAppliedScale = false;
        }

        public VideoTextureConsumer(Texture2DData t2dd)
        {
            Texture = t2dd;
            renderers = new List<Renderer>();
            IsDirty = false;
            lastAppliedTextureScale = Vector2.zero;
            hasAppliedScale = false;
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
            if (!hasAppliedScale || lastAppliedTextureScale != texScale)
            {
                foreach (var meshRenderer in renderers)
                {
                    if (meshRenderer.sharedMaterial != null)
                    {
                        meshRenderer.sharedMaterial.SetTextureScale(ShaderUtils.BaseMap, texScale);
                        meshRenderer.sharedMaterial.SetTextureScale(ShaderUtils.AlphaTexture, texScale);
                    }
                }

                lastAppliedTextureScale = texScale;
                hasAppliedScale = true;
            }
        }

        public bool NeedsScaleReapplication(Vector2 expectedScale)
        {
            if (!hasAppliedScale)
                return true;

            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterial == null)
                    continue;

                Vector2 currentBaseMapScale = renderer.sharedMaterial.GetTextureScale(ShaderUtils.BaseMap);
                Vector2 currentAlphaScale = renderer.sharedMaterial.GetTextureScale(ShaderUtils.AlphaTexture);

                if (currentBaseMapScale != expectedScale || currentAlphaScale != expectedScale)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

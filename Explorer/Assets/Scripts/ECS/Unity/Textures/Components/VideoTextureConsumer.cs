using ECS.StreamableLoading.Textures;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.Textures.Components
{
    public struct VideoTextureConsumer : IDisposable
    {
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

        private readonly List<MeshRenderer> renderers;

        /// <summary>
        ///     The single copy kept for the single Entity with VideoPlayer,
        ///     we don't use the original texture from AVPro
        /// </summary>
        public Texture2DData Texture { get; private set; }

        public int ConsumersCount => Texture.referenceCount;

        public VideoTextureConsumer(Texture2D texture)
        {
            Texture = new Texture2DData(texture);
            renderers = new List<MeshRenderer>();
        }

        public void Dispose()
        {
            // On Dispose video textures are dereferenced by material that acquired it
            Texture = null!;
            renderers.Clear();
        }

        public void AddConsumerMeshRenderer(MeshRenderer renderer)
        {
            renderers.Add(renderer);
        }
    }
}

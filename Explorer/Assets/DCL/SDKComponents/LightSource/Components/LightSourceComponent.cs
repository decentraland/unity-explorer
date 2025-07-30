using Arch.Core;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Textures;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.LightSource
{
    public struct LightSourceComponent
    {
        public readonly Light LightSourceInstance;

        public float MaxIntensity;

        public float CurrentIntensityNormalized;

        public float DistanceToPlayerSq;

        public int Index;

        public int Rank;

        public int TypeRank;

        public int LOD;

        public CullingFlags Culling;

        public CookieInfo Cookie;

        public bool IsCulled => Culling != CullingFlags.None;

        public LightSourceComponent(Light lightSourceInstance) : this()
        {
            LightSourceInstance = lightSourceInstance;
        }

        [Flags]
        public enum CullingFlags
        {
            None = 0,

            TooManyLightSources = 1,

            CulledByLOD = 1 << 1
        }

        public struct CookieInfo
        {
            public GetTextureIntention LoadingIntention;

            public AssetPromise<Texture2DData, GetTextureIntention>? LoadingPromise;

            public Texture2DData SourceTextureData;

            public Cubemap PointLightCubemap;

            public void CleanUp(in World world)
            {
                LoadingIntention = default(GetTextureIntention);

                if (LoadingPromise != null)
                {
                    LoadingPromise.Value.ForgetLoading(world);
                    LoadingPromise = null;
                }

                SourceTextureData?.Dereference();
                SourceTextureData = null;

                if (PointLightCubemap)
                {
                    Object.Destroy(PointLightCubemap);
                    PointLightCubemap = null;
                }
            }
        }
    }
}

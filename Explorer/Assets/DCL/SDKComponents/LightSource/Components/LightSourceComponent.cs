using System;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.LightSource
{
    public struct LightSourceComponent
    {
        public readonly Light LightSourceInstance;

        public float MaxIntensity;

        public float CurrentIntensity;

        public float TargetIntensity;

        public float DistanceToPlayer;

        public int Index;

        public int Rank;

        public int TypeRank;

        public int LOD;

        public CullingFlags Culling;

        public Promise? TextureMaskPromise;

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
    }
}

using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.LightSource
{
    public struct LightSourceComponent
    {
        public readonly Light LightSourceInstance;

        public readonly float MaxIntensity;

        public float CurrentIntensity;

        public float TargetIntensity;

        public int Index;

        public int Rank;

        public bool IsCulled;

        public Promise? TextureMaskPromise;

        public LightSourceComponent(Light lightSourceInstance, float maxIntensity, float initialIntensity = 0)
        {
            LightSourceInstance = lightSourceInstance;
            MaxIntensity = maxIntensity;
            CurrentIntensity = TargetIntensity = initialIntensity;
            Index = -1;
            Rank = -1;
            IsCulled = false;
            TextureMaskPromise = null;
        }
    }
}

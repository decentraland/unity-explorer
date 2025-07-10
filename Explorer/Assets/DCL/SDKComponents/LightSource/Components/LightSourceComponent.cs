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

        public Promise? TextureMaskPromise;

        public LightSourceComponent(Light lightSourceInstance, float maxIntensity, float initialIntensity = 0)
        {
            LightSourceInstance = lightSourceInstance;
            TextureMaskPromise = null;
            MaxIntensity = maxIntensity;
            CurrentIntensity = TargetIntensity = initialIntensity;
        }
    }
}

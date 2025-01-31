using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.LightSource
{
    public struct LightSourceComponent
    {
        public readonly Light lightSourceInstance;
        public Promise? TextureMaskPromise;

        public LightSourceComponent(Light lightSourceInstance)
        {
            this.lightSourceInstance = lightSourceInstance;
            TextureMaskPromise = null;
        }
    }
}

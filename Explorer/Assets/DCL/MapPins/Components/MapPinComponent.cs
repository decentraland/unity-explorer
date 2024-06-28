using Arch.Core;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.MapPins.Components
{
    public struct MapPinComponent
    {
        public Promise? TexturePromise;
        public bool IsDirty { get; set; }
        public Vector2 Position { get; set; }
    }
}

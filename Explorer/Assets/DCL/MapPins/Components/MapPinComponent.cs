using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.MapPins.Components
{
    public struct MapPinComponent
    {
        public Promise? TexturePromise;

        public bool IsDirty { get; set; }
        public Vector2 Position { get; set; }
        public float IconSize { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }
}

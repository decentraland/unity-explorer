using Arch.Core;
using ECS.Unity.Textures.Components;

namespace DCL.SDKComponents.MediaStream
{
    public struct InitializeVideoPlayerMaterialRequest
    {
        public VideoTextureConsumer Consumer;
        public Entity MediaPlayerComponentEntity;
    }
}

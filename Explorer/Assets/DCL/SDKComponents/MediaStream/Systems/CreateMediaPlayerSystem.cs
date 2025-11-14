using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Textures.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    [ThrottlingEnabled]
    public partial class CreateMediaPlayerSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly MediaFactory mediaFactory;

        public CreateMediaPlayerSystem(
            World world,
            ISceneStateProvider sceneStateProvider,
            MediaFactory mediaFactory
        ) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.mediaFactory = mediaFactory;
        }

        protected override void Update(float t)
        {
            CreateAudioStreamQuery(World);
            CreateVideoPlayerQuery(World);
        }

        [Query]
        [None(typeof(MediaPlayerComponent))]
        private void CreateAudioStream(Entity entity, ref PBAudioStream sdkComponent)
        {
            // If the player has no transform, it will appear at 0,0,0 and nobody will hear it if it is in 3D
            bool isSpatialAudio = World!.Has<TransformComponent>(entity) && sdkComponent is {HasSpatial: true, Spatial: true };

            if (mediaFactory.TryCreateMediaPlayer(sdkComponent.Url, sdkComponent.HasVolume, sdkComponent.Volume,
                    isSpatialAudio, sdkComponent.HasSpatialMaxDistance ? sdkComponent.SpatialMaxDistance : null,
                    out MediaPlayerComponent component))
                World.Add(entity, component);
        }

        [Query]
        private void CreateVideoPlayer(Entity entity, PBVideoPlayer sdkComponent)
        {
            var address = MediaAddress.New(sdkComponent.Src!);

            // Streams rely on livekit room being active; which can only be in we are on the same scene. Let's not create media that is wrong
            if (address.IsLivekitAddress(out _) && !sceneStateProvider.IsCurrent)
                return;

            // MediaPlayerComponent / VideoTextureConsumer can be present in any combination

            if (!World.Has<MediaPlayerComponent>(entity))
            {
                // If the player has no transform, it will appear at 0,0,0 and nobody will hear it if it is in 3D
                bool isSpatialAudio = World!.Has<TransformComponent>(entity) && sdkComponent is {HasSpatial: true, Spatial: true };

                if (!mediaFactory.TryCreateMediaPlayer(sdkComponent.Src, sdkComponent.HasVolume, sdkComponent.Volume,
                        isSpatialAudio, sdkComponent.HasSpatialMaxDistance ? sdkComponent.SpatialMaxDistance : null,
                        out MediaPlayerComponent mediaPlayerComponent))
                    return;

                World.Add(entity, mediaPlayerComponent);
            }

            if (!World.Has<VideoTextureConsumer>(entity))
                World.Add(entity, mediaFactory.CreateVideoConsumer());
        }
    }
}

using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Textures.Components;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class CreateMediaPlayerSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        private CreateMediaPlayerSystem(World world, IComponentPool<MediaPlayer> mediaPlayerPool, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.mediaPlayerPool = mediaPlayerPool;
        }

        protected override void Update(float t)
        {
            CreateAudioStreamQuery(World);
            CreateVideoPlayerQuery(World);
        }

        [Query]
        [None(typeof(MediaPlayerComponent))]
        private void CreateAudioStream(in Entity entity, ref PBAudioStream sdkComponent)
        {
            var component = CreateMediaPlayerComponent(sdkComponent.Url, sdkComponent.HasVolume, sdkComponent.Volume, autoPlay: sdkComponent.HasPlaying && sdkComponent.Playing);
            World.Add(entity, component);
        }

        [Query]
        [None(typeof(MediaPlayerComponent))]
        [All(typeof(VideoTextureComponent))]
        private void CreateVideoPlayer(in Entity entity, ref PBVideoPlayer sdkComponent)
        {
            var component = CreateMediaPlayerComponent(sdkComponent.Src, sdkComponent.HasVolume, sdkComponent.Volume, autoPlay: sdkComponent.HasPlaying && sdkComponent.Playing);
            component.MediaPlayer.SetPlaybackProperties(sdkComponent);
            World.Add(entity, component);
        }

        private MediaPlayerComponent CreateMediaPlayerComponent(string url, bool hasVolume, float volume, bool autoPlay)
        {
            var component = new MediaPlayerComponent
            {
                MediaPlayer = mediaPlayerPool.Get(),
                URL = url,
                State = url.IsValidUrl() ? VideoState.VsNone : VideoState.VsError,
            };

            component.MediaPlayer
                     .OpenMediaIfValid(component.URL, autoPlay)
                     .UpdateVolume(sceneStateProvider.IsCurrent, hasVolume, volume);

            return component;
        }
    }
}

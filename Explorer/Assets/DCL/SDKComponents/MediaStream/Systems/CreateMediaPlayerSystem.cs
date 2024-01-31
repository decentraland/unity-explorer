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
            var component = new MediaPlayerComponent(mediaPlayerPool.Get(), sdkComponent.Url);

            OpenAndPlayMediaIfValid(component, sdkComponent.Url, sdkComponent.HasPlaying, sdkComponent.Playing);
            component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, sdkComponent.Volume);

            World.Add(entity, component);
        }

        [Query]
        [None(typeof(MediaPlayerComponent))]
        [All(typeof(VideoTextureComponent))]
        private void CreateVideoPlayer(in Entity entity, ref PBVideoPlayer sdkComponent)
        {
            var component = new MediaPlayerComponent(mediaPlayerPool.Get(), sdkComponent.Src);

            OpenAndPlayMediaIfValid(component, sdkComponent.Src, sdkComponent.HasPlaying, sdkComponent.Playing);
            component.MediaPlayer.UpdateVolume(sceneStateProvider.IsCurrent, sdkComponent.HasVolume, sdkComponent.Volume)
                     .UpdatePlaybackProperties(sdkComponent);

            World.Add(entity, component);
        }

        private static void OpenAndPlayMediaIfValid(MediaPlayerComponent component, string url, bool hasPlaying, bool playing)
        {
            if (url.IsValidUrl())
            {
                component.MediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, url, autoPlay: false);

                if (hasPlaying && playing)
                    component.MediaPlayer.Play();
            }
            else
                component.State = VideoState.VsError;
        }
    }
}

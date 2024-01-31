using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Unity.Groups;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using System.Runtime.CompilerServices;

namespace DCL.SDKComponents.AudioStream
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AUDIO_STREAM)]
    public partial class AudioStreamSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        private AudioStreamSystem(World world, IComponentPool<MediaPlayer> mediaPlayerPool, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.mediaPlayerPool = mediaPlayerPool;
        }

        protected override void Update(float t)
        {
            CreateAudioStreamQuery(World);
            UpdateAudioStreamQuery(World);
        }

        [Query]
        [None(typeof(AudioStreamComponent))]
        private void CreateAudioStream(in Entity entity, ref PBAudioStream sdkComponent)
        {
            var component = new AudioStreamComponent(sdkComponent, mediaPlayerPool.Get());
            UpdateVolume(ref component, sdkComponent, sceneStateProvider.IsCurrent);

            World.Add(entity, component);
        }

        [Query]
        private void UpdateAudioStream(ref AudioStreamComponent component, ref PBAudioStream sdkComponent)
        {
            UpdateVolume(ref component, sdkComponent, sceneStateProvider.IsCurrent);

            if (!sdkComponent.IsDirty || !sdkComponent.Url.IsValidUrl()) return;

            UpdateComponentChange(ref component, sdkComponent);
            sdkComponent.IsDirty = false;
        }

        private static void UpdateComponentChange(ref AudioStreamComponent component, PBAudioStream sdkComponent)
        {
            UpdateStreamUrl(ref component, sdkComponent.Url);
            UpdatePlayback(ref component, sdkComponent);
        }

        private static void UpdateVolume(ref AudioStreamComponent component, PBAudioStream sdkComponent, bool isCurrentScene)
        {
            if (isCurrentScene)
                component.MediaPlayer.AudioVolume = sdkComponent.HasVolume ? sdkComponent.Volume : AudioStreamComponent.DEFAULT_VOLUME;
            else
                component.MediaPlayer.AudioVolume = 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdatePlayback(ref AudioStreamComponent component, PBAudioStream sdkComponent)
        {
            if (sdkComponent.HasPlaying && sdkComponent.Playing != component.MediaPlayer.Control.IsPlaying())
            {
                if (sdkComponent.Playing)
                    component.MediaPlayer.Play();
                else
                    component.MediaPlayer.Stop();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateStreamUrl(ref AudioStreamComponent component, string newUrl)
        {
            if (component.URL == newUrl) return;

            component.URL = newUrl;
            component.MediaPlayer.CloseCurrentStream();
            component.MediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, newUrl, autoPlay: false);
        }
    }
}

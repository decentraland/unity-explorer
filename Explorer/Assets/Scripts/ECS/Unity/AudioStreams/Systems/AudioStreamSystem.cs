using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Unity.AudioStreams.Components;
using ECS.Unity.Groups;
using RenderHeads.Media.AVProVideo;

namespace ECS.Unity.AudioStreams.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]

    // [LogCategory(ReportCategory.AUDIO_SOURCES)]
    public partial class AudioStreamSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        private AudioStreamSystem(World world, IComponentPoolsRegistry componentPoolsRegistry) : base(world)
        {
            mediaPlayerPool = componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>();
        }

        protected override void Update(float t)
        {
            InstantiateAudioStreamQuery(World);
            UpdateAudioStreamQuery(World);
        }

        [Query]
        [None(typeof(AudioStreamComponent))]
        private void InstantiateAudioStream(in Entity entity, ref PBAudioStream sdkAudio)
        {
            AudioStreamComponent component = AddComponentToWorldEntity(entity, sdkAudio);

            if (sdkAudio.Url.IsValidUrl())
                ReplicateAudioValues(sdkAudio, component.MediaPlayer);
        }

        [Query]
        private void UpdateAudioStream(ref PBAudioStream sdkComponent, ref AudioStreamComponent component)
        {
            if (!sdkComponent.IsDirty || !sdkComponent.Url.IsValidUrl()) return;

            if (sdkComponent.HasPlaying && sdkComponent.Playing != component.MediaPlayer.Control.IsPlaying())
            {
                if (sdkComponent.Playing)
                    component.MediaPlayer.Play();
                else
                    component.MediaPlayer.Stop();
            }

            if (sdkComponent.HasVolume)
                component.MediaPlayer.AudioVolume = sdkComponent.Volume;

            sdkComponent.IsDirty = false;
        }

        private AudioStreamComponent AddComponentToWorldEntity(Entity entity, PBAudioStream sdkAudio)
        {
            MediaPlayer? mediaPlayer = mediaPlayerPool.Get();
            var component = new AudioStreamComponent(sdkAudio, mediaPlayer);
            World.Add(entity, component);

            return component;
        }

        private static void ReplicateAudioValues(PBAudioStream sdkAudio, MediaPlayer mediaPlayer)
        {
            mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, sdkAudio.Url, autoPlay: false);

            if (sdkAudio is { HasPlaying: true, Playing: true })
                mediaPlayer.Play();

            if (sdkAudio.HasVolume)
                mediaPlayer.AudioVolume = sdkAudio.Volume;
        }
    }
}

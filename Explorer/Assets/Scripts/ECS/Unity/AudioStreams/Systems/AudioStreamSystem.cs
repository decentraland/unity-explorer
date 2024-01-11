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
        private readonly IComponentPoolsRegistry poolsRegistry;

        private AudioStreamSystem(World world, IComponentPoolsRegistry componentPoolsRegistry) : base(world)
        {
            poolsRegistry = componentPoolsRegistry;

            {
                MediaPlayer? mediaPlayer = poolsRegistry.GetReferenceTypePool<MediaPlayer>().Get();
                mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, "http://ice3.somafm.com/groovesalad-128-mp3", autoPlay: true);
            }
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
            var component = new AudioStreamComponent(sdkAudio, poolsRegistry);
            World.Add(entity, component);
        }

        [Query]
        private void UpdateAudioStream(ref PBAudioStream sdkComponent, ref AudioStreamComponent component)
        {
            if (!sdkComponent.IsDirty || !sdkComponent.Url.IsValidUrl())
                return;

            component.Update(sdkComponent);
            sdkComponent.IsDirty = false;
        }
    }
}

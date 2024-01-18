using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;

namespace DCL.SDKComponents.AudioStream
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AUDIO_STREAM)]
    public partial class AudioStreamSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        private AudioStreamSystem(World world, IComponentPoolsRegistry componentPoolsRegistry, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            mediaPlayerPool = componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>();
        }

        protected override void Update(float t)
        {
            HandleSdkComponentRemovalQuery(World);
            InstantiateAudioStreamQuery(World);
            UpdateAudioStreamQuery(World);
        }

        [Query]
        [None(typeof(AudioStreamComponent))]
        private void InstantiateAudioStream(in Entity entity, ref PBAudioStream sdkAudio)
        {
            var component = new AudioStreamComponent(sdkAudio, mediaPlayerPool.Get(), sceneStateProvider.IsCurrent);
            World.Add(entity, component);
        }

        [Query]
        private void UpdateAudioStream(ref PBAudioStream sdkComponent, ref AudioStreamComponent component)
        {
            component.UpdateVolume(sdkComponent, sceneStateProvider.IsCurrent);

            if (!sdkComponent.IsDirty || !sdkComponent.Url.IsValidUrl()) return;

            component.UpdateComponentChange(sdkComponent);
            sdkComponent.IsDirty = false;
        }

        [Query]
        [None(typeof(PBAudioStream), typeof(DeleteEntityIntention))]
        private void HandleSdkComponentRemoval(ref AudioStreamComponent component)
        {
            component.Dispose();
            mediaPlayerPool.Release(component.MediaPlayer);
        }
    }
}

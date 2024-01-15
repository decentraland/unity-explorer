using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.AudioStream.Components;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Unity.Groups;
using SceneRunner.Scene;

namespace DCL.SDKComponents.AudioStream.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AUDIO_STREAM)]
    public partial class AudioStreamSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly ISceneStateProvider sceneStateProvider;

        private AudioStreamSystem(World world, IComponentPoolsRegistry componentPoolsRegistry, ISceneStateProvider sceneStateProvider) : base(world)
        {
            poolsRegistry = componentPoolsRegistry;
            this.sceneStateProvider = sceneStateProvider;
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
            var component = new AudioStreamComponent(sdkAudio, poolsRegistry, sceneStateProvider.IsCurrent);
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
    }
}

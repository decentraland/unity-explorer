using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Unity.AudioStreams.Components;
using ECS.Unity.Groups;
using RenderHeads.Media.AVProVideo;

namespace ECS.Unity.AudioStreams.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]

    // [LogCategory(ReportCategory.AUDIO_SOURCES)]
    public partial class InstantiateAudioStreamSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        private InstantiateAudioStreamSystem(World world, IComponentPoolsRegistry componentPoolsRegistry) : base(world)
        {
            mediaPlayerPool = componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>();
        }

        protected override void Update(float t)
        {
            InstantiateAudioSource2Query(World);
        }

        [Query]
        [None(typeof(AudioStreamComponent))]
        private void InstantiateAudioSource2(in Entity entity, ref PBAudioStream sdkAudio)
        {
            var component = new AudioStreamComponent(sdkAudio, mediaPlayerPool.Get());
            component.MediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, sdkAudio.Url, autoPlay: true);
            World.Add(entity, component);

            // var component = new PrimitiveColliderComponent();
            // Instantiate(entity, crdtEntity, setupColliderCases[sdkComponent.MeshCase], ref component, ref sdkComponent, ref transform);
            // World.Add(entity, component);
        }
    }
}

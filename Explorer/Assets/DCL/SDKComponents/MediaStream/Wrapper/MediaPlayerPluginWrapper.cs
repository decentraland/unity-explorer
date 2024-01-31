using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream.Wrapper
{
    public class MediaPlayerPluginWrapper
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IExtendedObjectPool<Texture2D> videoTexturePool;

        public MediaPlayerPluginWrapper(IComponentPoolsRegistry componentPoolsRegistry, CacheCleaner cacheCleaner, IExtendedObjectPool<Texture2D> videoTexturePool)
        {
#if AV_PRO_PRESENT
            this.componentPoolsRegistry = componentPoolsRegistry;

            this.videoTexturePool = videoTexturePool;
            cacheCleaner.Register(videoTexturePool);

            componentPoolsRegistry.AddGameObjectPool<MediaPlayer>(onRelease: mp => mp.CloseCurrentStream());
            cacheCleaner.Register(componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>());
#endif
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCrdtWriter, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
#if AV_PRO_PRESENT
            IComponentPool<MediaPlayer> mediaPlayerPool = componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>();

            CreateMediaPlayerSystem.InjectToWorld(ref builder, mediaPlayerPool, sceneStateProvider);
            UpdateMediaPlayerSystem.InjectToWorld(ref builder, sceneStateProvider);
            CleanUpMediaPlayerSystem.InjectToWorld(ref builder, mediaPlayerPool, videoTexturePool);

            VideoEventsSystem.InjectToWorld(ref builder, ecsToCrdtWriter, sceneStateProvider, componentPoolsRegistry.GetReferenceTypePool<PBVideoEvent>());

            ResetDirtyFlagSystem<PBAudioStream>.InjectToWorld(ref builder);
            ResetDirtyFlagSystem<PBVideoPlayer>.InjectToWorld(ref builder);

            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<MediaPlayer, MediaPlayerComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
#endif
        }
    }
}

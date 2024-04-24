using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using SceneRunner.Scene;
using UnityEngine;

#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
using DCL.ECSComponents;
using RenderHeads.Media.AVProVideo;
#endif

namespace DCL.SDKComponents.MediaStream.Wrapper
{
    public class MediaPlayerPluginWrapper
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IExtendedObjectPool<Texture2D> videoTexturePool;
        private readonly IPerformanceBudget frameTimeBudget;

        public MediaPlayerPluginWrapper(IComponentPoolsRegistry componentPoolsRegistry, CacheCleaner cacheCleaner, IExtendedObjectPool<Texture2D> videoTexturePool, IPerformanceBudget frameTimeBudget)
        {
#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
            this.componentPoolsRegistry = componentPoolsRegistry;

            this.videoTexturePool = videoTexturePool;
            this.frameTimeBudget = frameTimeBudget;
            cacheCleaner.Register(videoTexturePool);

            componentPoolsRegistry.AddGameObjectPool<MediaPlayer>(onRelease: mp => mp.CloseCurrentStream());
            cacheCleaner.Register(componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>());
#endif
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, ISceneData sceneData, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCrdtWriter)
        {
#if AV_PRO_PRESENT && !UNITY_EDITOR_LINUX && !UNITY_STANDALONE_LINUX
            IComponentPool<MediaPlayer> mediaPlayerPool = componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>();

            CreateMediaPlayerSystem.InjectToWorld(ref builder, sceneData, mediaPlayerPool, sceneStateProvider, frameTimeBudget);
            UpdateMediaPlayerSystem.InjectToWorld(ref builder, sceneStateProvider, frameTimeBudget);
            CleanUpMediaPlayerSystem.InjectToWorld(ref builder, mediaPlayerPool, videoTexturePool);

            VideoEventsSystem.InjectToWorld(ref builder, ecsToCrdtWriter, sceneStateProvider, componentPoolsRegistry.GetReferenceTypePool<PBVideoEvent>(), frameTimeBudget);
#endif
        }
    }
}

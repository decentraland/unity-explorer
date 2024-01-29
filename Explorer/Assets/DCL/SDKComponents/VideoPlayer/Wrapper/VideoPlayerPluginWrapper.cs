using Arch.Core;
using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using ECS.LifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

#if AV_PRO_PRESENT
using DCL.SDKComponents.AudioStream;
using DCL.SDKComponents.VideoPlayer.Systems;
using RenderHeads.Media.AVProVideo;
#endif

namespace DCL.SDKComponents.VideoPlayer.Wrapper
{
    public class VideoPlayerPluginWrapper
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IExtendedObjectPool<Texture2D> videoTexturePool;

        public VideoPlayerPluginWrapper(IComponentPoolsRegistry componentPoolsRegistry, CacheCleaner cacheCleaner, IExtendedObjectPool<Texture2D> videoTexturePool)
        {
#if AV_PRO_PRESENT
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.videoTexturePool = videoTexturePool;

            if (!componentPoolsRegistry.TryGetPool<MediaPlayer>(out _))
            {
                componentPoolsRegistry.AddGameObjectPool<MediaPlayer>(onRelease: mp => mp.CloseCurrentStream());
                cacheCleaner.Register(componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>());
                cacheCleaner.Register(videoTexturePool);
            }
#endif
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, ISceneStateProvider sceneStateProvider)
        {
#if AV_PRO_PRESENT
            IComponentPool<MediaPlayer> mediaPlayerPool = componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>();

            VideoPlayerSystem.InjectToWorld(ref builder, mediaPlayerPool, sceneStateProvider);
            CleanUpVideoPlayerSystem.InjectToWorld(ref builder, mediaPlayerPool, videoTexturePool);
#endif
        }
    }
}

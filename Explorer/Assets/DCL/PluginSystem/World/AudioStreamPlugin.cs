using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.AudioStream.Systems;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;

#if AV_PRO_PRESENT
using DCL.SDKComponents.AudioStream;
using DCL.SDKComponents.AudioStream.Components;
using RenderHeads.Media.AVProVideo;
#endif

namespace DCL.PluginSystem.World
{
    public class AudioStreamPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public AudioStreamPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, CacheCleaner cacheCleaner)
        {
#if AV_PRO_PRESENT
            componentPoolsRegistry = sharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddGameObjectPool<MediaPlayer>(onRelease: mp => mp.CloseCurrentStream());

            cacheCleaner.Register(componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>());
#endif
        }

        // private static MediaPlayer HandleCreation()
        // {
        //     var poolObject = GameObjectPool<MediaPlayer>.HandleCreation();
        //     poolObject.gameObject.TryAddComponent<AudioSource>();
        //     poolObject.gameObject.TryAddComponent<AudioOutput>();
        //     return poolObject;
        // }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
#if AV_PRO_PRESENT
            AudioStreamSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.SceneStateProvider);
            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<MediaPlayer, AudioStreamComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
#endif
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}

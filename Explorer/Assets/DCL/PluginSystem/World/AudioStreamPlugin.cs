using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.AudioStream;
using DCL.SDKComponents.AudioStream.Components;
using DCL.SDKComponents.AudioStream.Systems;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using RenderHeads.Media.AVProVideo;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class AudioStreamPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public AudioStreamPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, CacheCleaner cacheCleaner)
        {
            componentPoolsRegistry = sharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddGameObjectPool<MediaPlayer>(onRelease: mp => mp.CloseCurrentStream());

            cacheCleaner.Register(componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>());
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
            AudioStreamSystem.InjectToWorld(ref builder, componentPoolsRegistry, sharedDependencies.SceneStateProvider);
            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<MediaPlayer, AudioStreamComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}

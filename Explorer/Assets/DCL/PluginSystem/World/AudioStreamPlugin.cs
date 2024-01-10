using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.Unity.AudioStreams.Systems;
using RenderHeads.Media.AVProVideo;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class AudioStreamPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public AudioStreamPlugin(ECSWorldSingletonSharedDependencies sharedDependencies)
        {
            componentPoolsRegistry = sharedDependencies.ComponentPoolsRegistry;
            componentPoolsRegistry.AddGameObjectPool<MediaPlayer>(); // componentPoolsRegistry.AddGameObjectPool<MediaPlayer>(HandleCreation);

            // cacheCleaner.Register(componentPoolsRegistry.GetReferenceTypePool<AudioSource>());
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
            InstantiateAudioStreamSystem.InjectToWorld(ref builder, componentPoolsRegistry);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}

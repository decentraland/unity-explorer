using Arch.Core;
using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.Unity.SceneBoundsChecker;
using System.Collections.Generic;

namespace DCL.Gizmos.Plugin
{
    public class GizmosWorldPlugin : IDCLWorldPluginWithoutSettings
    {
        /// <summary>
        ///     Add more constructor here as needed
        /// </summary>
        private static readonly CreateSceneGizmosDelegate[] POSSIBLE_PROVIDERS_CONSTRUCTORS = { () => new GizmosDrawSceneBounds() };

        private static readonly ThreadSafeObjectPool<DrawSceneGizmosHub.ProviderState[]> SCENE_GIZMOS_PROVIDERS_POOL;

        static GizmosWorldPlugin()
        {
            SCENE_GIZMOS_PROVIDERS_POOL = new ThreadSafeObjectPool<DrawSceneGizmosHub.ProviderState[]>(() =>
            {
                var result = new DrawSceneGizmosHub.ProviderState[POSSIBLE_PROVIDERS_CONSTRUCTORS.Length];

                for (var i = 0; i < POSSIBLE_PROVIDERS_CONSTRUCTORS.Length; i++)
                {
                    SceneGizmosProviderBase provider = POSSIBLE_PROVIDERS_CONSTRUCTORS[i]();
                    result[i] = new DrawSceneGizmosHub.ProviderState(provider);
                }

                return result;
            }, actionOnRelease: array =>
            {
                for (var i = 0; i < array.Length; i++)
                {
                    ref DrawSceneGizmosHub.ProviderState providerState = ref array[i];
                    providerState.gizmosProvider.SceneData = null;

                    // Reset activity
                    providerState.active = true;
                }
            }, defaultCapacity: PoolConstants.SCENES_COUNT);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            DrawSceneGizmosHubSystem.InjectToWorld(ref builder, sharedDependencies.SceneData, SCENE_GIZMOS_PROVIDERS_POOL);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}

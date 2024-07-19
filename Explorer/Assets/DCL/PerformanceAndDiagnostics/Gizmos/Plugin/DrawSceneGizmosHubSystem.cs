using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components.Special;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine.Pool;
using Utility;

namespace DCL.Gizmos.Plugin
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class DrawSceneGizmosHubSystem : ISystem<float>
    {
        private readonly IObjectPool<DrawSceneGizmosHub.ProviderState[]> sceneGizmosProvidersPool;
        private readonly World world;
        private readonly ISceneData sceneData;

        private DrawSceneGizmosHub hub;

        internal DrawSceneGizmosHubSystem(World world, ISceneData sceneData, IObjectPool<DrawSceneGizmosHub.ProviderState[]> sceneGizmosProvidersPool)
        {
            this.world = world;
            this.sceneData = sceneData;
            this.sceneGizmosProvidersPool = sceneGizmosProvidersPool;
        }

        public void Initialize()
        {
            // Create a hub after Transform is attached to the scene root
            AttachHubQuery(world);
        }

        public void Dispose()
        {
            if (hub == null) return;

            // Release providers to the pool
            DrawSceneGizmosHub.ProviderState[] providers = hub.GetCachedProviders();

            if (providers != null)
                sceneGizmosProvidersPool.Release(providers);

            UnityObjectUtils.SafeDestroy(hub);
        }

        private DrawSceneGizmosHub.ProviderState[] InitializeProvidersLazily()
        {
            DrawSceneGizmosHub.ProviderState[] result = sceneGizmosProvidersPool.Get();

            // Setup scene data
            for (var i = 0; i < result.Length; i++)
            {
                ref DrawSceneGizmosHub.ProviderState providerState = ref result[i];
                providerState.gizmosProvider.SceneData = sceneData;

                providerState.gizmosProvider.OnInitialize();
            }

            return result;
        }

        [Query]
        [All(typeof(SceneRootComponent))]
        [None(typeof(DrawSceneGizmosHub))] // just for extra safety
        private void AttachHub(ref TransformComponent transformComponent)
        {
            hub = transformComponent.Transform.gameObject.AddComponent<DrawSceneGizmosHub>();
            hub.Setup(InitializeProvidersLazily);
        }

        public void BeforeUpdate(in float t) { }

        public void Update(in float t) { }

        public void AfterUpdate(in float t) { }
    }
}

using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using SceneRunner.EmptyScene;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     The logic of loading an empty scene, it requires fewer steps and some data is mocked
    /// </summary>
    public class LoadEmptySceneSystemLogic : IDisposable
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IWebRequestController webRequestController;
        private readonly IEmptyScenesWorldFactory emptyScenesWorldFactory;
        private readonly URLAddress mappingURL;

        private EmptyScenesWorld sharedWorld;

        /// <summary>
        ///     Indicates that mapping could not be loaded and the whole logic will be skipped
        /// </summary>
        public bool Inactive { get; private set; }

        internal EmptySceneData emptySceneData { get; private set; }

        public LoadEmptySceneSystemLogic(
            IWebRequestController webRequestController,
            IEmptyScenesWorldFactory emptyScenesWorldFactory,
            IComponentPoolsRegistry componentPoolsRegistry,
            URLAddress mappingURL)
        {
            this.webRequestController = webRequestController;
            this.emptyScenesWorldFactory = emptyScenesWorldFactory;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.mappingURL = mappingURL;
        }

        public void Dispose()
        {
            sharedWorld?.Dispose();
        }

        internal async UniTask LoadMappingAsync(CancellationToken ct)
        {
            EmptySceneMappings mappings = await (await webRequestController.GetAsync(new CommonArguments(mappingURL), ct, ReportCategory.SCENE_LOADING))
               .CreateFromJson<EmptySceneMappings>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

            emptySceneData = new EmptySceneData(mappings.mappings);
        }

        public async UniTask<ISceneFacade> FlowAsync(World world, GetSceneFacadeIntention intent, IPartitionComponent partition, CancellationToken ct)
        {
            if (emptySceneData == null)
            {
                try
                {
                    await LoadMappingAsync(ct);
                    await UniTask.SwitchToMainThread();

                    if (sharedWorld == null)
                    {
                        sharedWorld = emptyScenesWorldFactory.Create(emptySceneData);
                        sharedWorld.SystemGroupWorld.Initialize();
                    }
                }
                catch
                {
                    Inactive = true;
                    throw;
                }
            }

            // pick one of available scenes randomly based on coordinates
            Vector2Int parcel = intent.DefinitionComponent.Parcels[0];
            EmptySceneMapping choice = emptySceneData.Mappings[Mathf.Abs(parcel.GetHashCode()) % emptySceneData.Mappings.Count];

            var emptyScene = EmptySceneFacade.Create(
                new EmptySceneFacade.Args(sharedWorld.FakeEntitiesMap, sharedWorld.EcsWorld, world, choice, componentPoolsRegistry,
                    ParcelMathHelper.GetPositionByParcelPosition(parcel), partition, sharedWorld.MutexSync));

            return emptyScene;
        }
    }
}

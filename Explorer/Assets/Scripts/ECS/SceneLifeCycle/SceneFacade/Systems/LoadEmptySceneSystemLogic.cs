using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using SceneRunner.EmptyScene;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     The logic of loading an empty scene, it requires fewer steps and some data is mocked
    /// </summary>
    public class LoadEmptySceneSystemLogic : IDisposable
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IEmptyScenesWorldFactory emptyScenesWorldFactory;
        private readonly string mappingURL;

        private EmptyScenesWorld sharedWorld;

        /// <summary>
        ///     Indicates that mapping could not be loaded and the whole logic will be skipped
        /// </summary>
        public bool Inactive { get; private set; }

        internal EmptySceneData emptySceneData { get; private set; }

        public LoadEmptySceneSystemLogic(
            IEmptyScenesWorldFactory emptyScenesWorldFactory,
            IComponentPoolsRegistry componentPoolsRegistry,
            string mappingURL)
        {
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
            string text;

            using (var webRequest = UnityWebRequest.Get(mappingURL))
            {
                await webRequest.SendWebRequest().WithCancellation(ct);
                text = webRequest.downloadHandler.text;
            }

            await UniTask.SwitchToThreadPool();

            EmptySceneMappings mappings = JsonUtility.FromJson<EmptySceneMappings>(text);
            emptySceneData = new EmptySceneData(mappings.mappings);
        }

        public async UniTask<ISceneFacade> FlowAsync(GetSceneFacadeIntention intent, IPartitionComponent partition, CancellationToken ct)
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
                new EmptySceneFacade.Args(sharedWorld.FakeEntitiesMap, sharedWorld.EcsWorld, choice, componentPoolsRegistry,
                    ParcelMathHelper.GetPositionByParcelPosition(parcel), partition, sharedWorld.MutexSync));

            return emptyScene;
        }
    }
}

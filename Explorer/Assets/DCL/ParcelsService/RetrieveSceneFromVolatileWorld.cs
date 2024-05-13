using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.Optimization.Pools;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace DCL.ParcelsService
{
    public class RetrieveSceneFromVolatileWorld : IRetrieveScene
    {
        private static readonly ListObjectPool<int2> POINTERS_POOL = new (defaultCapacity: 2, listInstanceDefaultCapacity: 1);
        private static readonly ListObjectPool<SceneEntityDefinition> TARGET_COLLECTION_POOL = new (defaultCapacity: 2, listInstanceDefaultCapacity: 1);

        private readonly IRealmData realmData;

        public World? World { get; set; }

        public RetrieveSceneFromVolatileWorld(IRealmData realmData)
        {
            this.realmData = realmData;
        }

        public async UniTask<SceneEntityDefinition?> ByParcelAsync(Vector2Int parcel, CancellationToken ct)
        {
            if (!realmData.Configured) return null;
            if (World == null) return null;

            IIpfsRealm realmIpfs = realmData.Ipfs;

            // Just make a request, cache to be implemented on the side of LoadSceneDefinition Systems
            using PoolExtensions.Scope<List<int2>> pointersList = POINTERS_POOL.AutoScope();
            using PoolExtensions.Scope<List<SceneEntityDefinition>> targetCollection = TARGET_COLLECTION_POOL.AutoScope();
            pointersList.Value.Add(parcel.ToInt2());

            var promise = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(World,
                new GetSceneDefinitionList(targetCollection.Value, pointersList.Value, new CommonLoadingArguments(realmIpfs.EntitiesActiveEndpoint)),
                PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(World, cancellationToken: ct);
            SceneDefinitions result = promise.Result.UnwrapAndRethrow();

            return result.Value.Count > 0 ? result.Value[0] : null;
        }
    }
}

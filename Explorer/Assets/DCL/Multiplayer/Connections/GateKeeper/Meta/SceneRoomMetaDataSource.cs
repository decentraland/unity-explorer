using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Utility.Types;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    public class SceneRoomMetaDataSource : ISceneRoomMetaDataSource
    {
        private readonly IRealmData realmData;
        private readonly IExposedTransform characterTransform;
        private readonly World world;

        private readonly bool forceSceneIsolation;

        private Vector2Int previousParcel = new (int.MaxValue, int.MaxValue);

        public bool ScenesCommunicationIsIsolated => forceSceneIsolation || !realmData.ScenesAreFixed;

        public bool MetadataIsDirty
        {
            get
            {
                CanBeDirty<Vector3> characterPosition = characterTransform.Position;

                bool positionIsDirty = !realmData.ScenesAreFixed && characterPosition.IsDirty;

                if (!positionIsDirty)
                    return false;

                Vector2Int parcel = characterPosition.ToParcel();

                if (parcel == previousParcel)
                    return false;

                previousParcel = parcel;
                return true;
            }
        }

        public SceneRoomMetaDataSource(IRealmData realmData, IExposedTransform characterTransform, World world, bool forceSceneIsolation)
        {
            this.realmData = realmData;
            this.characterTransform = characterTransform;
            this.world = world;
            this.forceSceneIsolation = forceSceneIsolation;
        }

        public MetaData.Input GetMetadataInput() =>
            new (
                realmData.RealmName,
                realmData.IsWorld()
                    ? Vector2Int.zero
                    : characterTransform.Position.ToParcel()
            );

        public async UniTask<Result<MetaData>> MetaDataAsync(MetaData.Input input, CancellationToken token)
        {
            // Places API is relevant for Genesis City only
            if (realmData.IsWorld())
                return Result<MetaData>.SuccessResult(new MetaData(input.RealmName, input));

            using PooledObject<List<SceneEntityDefinition>> pooledEntityDefinitionList = ListPool<SceneEntityDefinition>.Get(out List<SceneEntityDefinition>? entityDefinitionList);
            using PooledObject<List<int2>> pooledPointersList = ListPool<int2>.Get(out List<int2>? pointersList);

            pointersList.Add(input.Parcel.ToInt2());

            // TODO: instead of making a new request, Room Change request should be initiated when the scene definition is loaded by ECS,
            // currently these processes are completely separated
            var promise = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(world,
                new GetSceneDefinitionList(entityDefinitionList, pointersList, new CommonLoadingArguments(realmData.Ipfs.AssetBundleRegistry)),
                PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(world, cancellationToken: token);

            StreamableLoadingResult<SceneDefinitions> result = promise.Result!.Value;

            return Result<MetaData>.SuccessResult(
                result.Succeeded && entityDefinitionList.Count > 0
                    ? new MetaData(entityDefinitionList[0].id, input)
                    : new MetaData(null, input)
            );
        }
    }
}

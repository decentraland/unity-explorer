using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    public class LocalSceneDevelopmentSceneRoomMetaDataSource : ISceneRoomMetaDataSource
    {
        private readonly ILocalSceneEntityIdSource entityIdSource;

        public LocalSceneDevelopmentSceneRoomMetaDataSource(ILocalSceneEntityIdSource entityIdSource)
        {
            this.entityIdSource = entityIdSource;
        }

        public bool ScenesCommunicationIsIsolated => false;

        public MetaData.Input GetMetadataInput() =>
            new ("LocalSceneDevelopment", Vector2Int.zero);

        public async UniTask<Result<MetaData>> MetaDataAsync(MetaData.Input input, CancellationToken token)
        {
            Result<LocalSceneEntity> entity = await entityIdSource.EntityAsync(token);

            if (!entity.Success)
                return Result<MetaData>.ErrorResult(entity.ErrorMessage!);

            return Result<MetaData>.SuccessResult(new MetaData(entity.Value.Id, Vector2Int.zero, new MetaData.Input("LocalPreview", Vector2Int.zero)));
        }
    }
}

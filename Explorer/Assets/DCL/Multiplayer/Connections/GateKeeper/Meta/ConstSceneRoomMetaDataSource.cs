using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using Plugins.MachineInfo.MachineInfoWrap;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    public class ConstSceneRoomMetaDataSource : ISceneRoomMetaDataSource
    {
        private readonly MetaData.Input metadataInput;
        private readonly MetaData metaData;

        public ConstSceneRoomMetaDataSource(string name)
        {
            metadataInput = new MetaData.Input(name, Vector2Int.zero);
            metaData = new MetaData(name, metadataInput);
        }

        public static ConstSceneRoomMetaDataSource FromMachineUUID()
        {
            string uuid = MachineInfo.UUID();
            using var key = HashKey.FromString(uuid);

            //hashed due privacy purposes to don't expose user's unique machine uuid
            string hashed = HashUtility.ByteString(key.Hash.Memory);
            return new ConstSceneRoomMetaDataSource(hashed);
        }

        public bool ScenesCommunicationIsIsolated => true;

        public bool MetadataIsDirty => false;

        public MetaData.Input GetMetadataInput() =>
            metadataInput;

        public UniTask<Result<MetaData>> MetaDataAsync(MetaData.Input input, CancellationToken token)
        {
            ReportHub.LogWarning(ReportCategory.LIVEKIT, "I'm static, I won't consider the input param");
            return UniTask.FromResult(Result<MetaData>.SuccessResult(metaData));
        }
    }
}

using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    public class SceneRoomLogMetaDataSource : ISceneRoomMetaDataSource
    {
        private const string PREFIX = "MetaDataSource:";

        private readonly ISceneRoomMetaDataSource origin;

        public SceneRoomLogMetaDataSource(ISceneRoomMetaDataSource origin)
        {
            this.origin = origin;
        }

        public bool ScenesCommunicationIsIsolated => origin.ScenesCommunicationIsIsolated;

        public MetaData.Input GetMetadataInput()
        {
            ReportHub.WithReport(ReportCategory.LIVEKIT).Log($"{PREFIX} {nameof(GetMetadataInput)}");
            MetaData.Input result = origin.GetMetadataInput();
            return result;
        }

        public async UniTask<MetaData> MetaDataAsync(MetaData.Input input, CancellationToken token)
        {
            ReportHub.WithReport(ReportCategory.LIVEKIT).Log($"{PREFIX} {nameof(MetaDataAsync)} start: {input}");
            MetaData result = await origin.MetaDataAsync(input, token);
            ReportHub.WithReport(ReportCategory.LIVEKIT).Log($"{PREFIX} {nameof(MetaDataAsync)} finish {result.realmName} {result.sceneId}");
            return result;
        }

        public bool MetadataIsDirty => origin.MetadataIsDirty;
    }
}

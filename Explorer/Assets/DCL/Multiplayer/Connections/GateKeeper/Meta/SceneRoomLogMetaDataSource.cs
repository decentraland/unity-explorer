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
        private readonly Action<string> log;

        public SceneRoomLogMetaDataSource(ISceneRoomMetaDataSource origin)
        {
            this.origin = origin;
        }

        public bool ScenesCommunicationIsIsolated => origin.ScenesCommunicationIsIsolated;

        public async UniTask<MetaData> MetaDataAsync(CancellationToken token)
        {
            ReportHub.WithReport(ReportCategory.LIVEKIT).Log($"{PREFIX} MetaDataAsync start");
            MetaData result = await origin.MetaDataAsync(token);
            ReportHub.WithReport(ReportCategory.LIVEKIT).Log($"{PREFIX} MetaDataAsync finish {result.realmName} {result.sceneId}");
            return result;
        }

        public bool MetadataIsDirty => origin.MetadataIsDirty;
    }
}

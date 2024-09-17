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

        public SceneRoomLogMetaDataSource(ISceneRoomMetaDataSource origin) : this(origin, ReportHub.WithReport(ReportCategory.LIVEKIT).Log) { }

        public SceneRoomLogMetaDataSource(ISceneRoomMetaDataSource origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public bool ScenesCommunicationIsIsolated => origin.ScenesCommunicationIsIsolated;

        public async UniTask<MetaData> MetaDataAsync(CancellationToken token)
        {
            log($"{PREFIX} MetaDataAsync start");
            MetaData result = await origin.MetaDataAsync(token);
            log($"{PREFIX} MetaDataAsync finish {result.realmName} {result.sceneId}");
            return result;
        }

        public async UniTask WaitForMetaDataIsDirtyAsync(CancellationToken token)
        {
            log($"{PREFIX} WaitForMetaDataIsDirtyAsync start");
            await origin.WaitForMetaDataIsDirtyAsync(token);
            log($"{PREFIX} WaitForMetaDataIsDirtyAsync finish");
        }
    }
}

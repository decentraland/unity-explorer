using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    public class LogMetaDataSource : IMetaDataSource
    {
        private const string PREFIX = "MetaDataSource:";

        private readonly IMetaDataSource origin;

        public LogMetaDataSource(IMetaDataSource origin)
        {
            this.origin = origin;
        }

        public async UniTask<MetaData> MetaDataAsync(CancellationToken token)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{PREFIX} MetaDataAsync start");
            MetaData result = await origin.MetaDataAsync(token);
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{PREFIX} MetaDataAsync finish {result.realmName} {result.sceneId}");
            return result;
        }
    }
}

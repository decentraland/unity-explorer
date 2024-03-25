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
        private readonly Action<string> log;

        public LogMetaDataSource(IMetaDataSource origin) : this(origin, ReportHub.WithReport(ReportCategory.LIVEKIT).Log) { }

        public LogMetaDataSource(IMetaDataSource origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public async UniTask<MetaData> MetaDataAsync(CancellationToken token)
        {
            log($"{PREFIX} MetaDataAsync start");
            MetaData result = await origin.MetaDataAsync(token);
            log($"{PREFIX} MetaDataAsync finish {result.ToJson()}");
            return result;
        }
    }
}

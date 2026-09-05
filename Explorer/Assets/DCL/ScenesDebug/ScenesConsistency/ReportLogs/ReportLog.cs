using DCL.ScenesDebug.ScenesConsistency.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DCL.ScenesDebug.ScenesConsistency.ReportLogs
{
    public class ReportLog : IReportLog
    {
        private readonly IReadOnlyCollection<SceneEntity> entities;
        private readonly StreamWriter writer;

        public ReportLog(IReadOnlyCollection<SceneEntity> entities, StreamWriter writer)
        {
            this.entities = entities;
            this.writer = writer;
        }

        public void Start()
        {
            writer.WriteLine($"{DateTime.Now:HH:mm:ss} Logging for scenes: ");

            foreach (SceneEntity sceneEntity in entities)
                writer.WriteLine(sceneEntity.ToString()!);

            writer.WriteLine($"{DateTime.Now:HH:mm:ss} Result of logs: ");

            Application.logMessageReceivedThreaded += ApplicationOnlogMessageReceivedThreaded;
        }

        private void ApplicationOnlogMessageReceivedThreaded(string condition, string stacktrace, LogType type)
        {
            writer.WriteLine($"{DateTime.Now:HH:mm:ss} {type}: {condition} {stacktrace}");
        }

        public void Dispose()
        {
            writer.Flush();
            writer.Dispose();
            Application.logMessageReceivedThreaded -= ApplicationOnlogMessageReceivedThreaded;
        }
    }
}

using DCL.Diagnostics;
using System;
using System.Collections.Generic;

namespace Utility.Multithreading
{
    public class SyncLogsBuffer
    {
        public readonly SceneShortInfo sceneShortInfo;
        private readonly int logsToKeep;
        private readonly DateTime creationTime;

        private readonly LinkedList<Entry> circularBuffer;

        public SyncLogsBuffer(SceneShortInfo sceneShortInfo, int logsToKeep)
        {
            this.sceneShortInfo = sceneShortInfo;
            this.logsToKeep = logsToKeep;
            creationTime = DateTime.Now;

            circularBuffer = new LinkedList<Entry>();
        }

        public void Report(string eventLog, string source)
        {
            if (circularBuffer.Count >= logsToKeep)
                circularBuffer.RemoveFirst();

            circularBuffer.AddLast(new Entry(eventLog, DateTime.Now - creationTime, source));
        }

        public void Print()
        {
            var reportData = new ReportData(ReportCategory.SYNC, sceneShortInfo: sceneShortInfo);

            foreach (Entry entry in circularBuffer)
                ReportHub.Log(reportData, $"T: {entry.TimeSinceCreation.TotalSeconds}, {entry.EventLog} {entry.Source}");
        }

        private struct Entry
        {
            public readonly string EventLog;
            public readonly TimeSpan TimeSinceCreation;
            public readonly string Source;

            public Entry(string eventLog, TimeSpan timeSinceCreation, string source)
            {
                EventLog = eventLog;
                TimeSinceCreation = timeSinceCreation;
                Source = source;
            }
        }
    }
}

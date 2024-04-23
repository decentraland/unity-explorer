using System;

namespace DCL.ScenesDebug.ScenesConsistency.ReportLogs
{
    public interface IReportLog : IDisposable
    {
        void Start();
    }
}

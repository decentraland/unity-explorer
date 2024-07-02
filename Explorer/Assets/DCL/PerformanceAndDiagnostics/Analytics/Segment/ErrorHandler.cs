using DCL.Diagnostics;
using Segment.Concurrent;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    internal class SegmentErrorHandler : ICoroutineExceptionHandler
    {
        public void OnExceptionThrown(Exception e)
        {
            ReportHub.LogException(e, ReportCategory.ANALYTICS);
        }
    }
}

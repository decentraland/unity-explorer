using DCL.Diagnostics;
using NSubstitute;
using System;
using System.Collections.Generic;

namespace SceneRunner.Scene.Tests
{
    /// <summary>
    ///     Provides a report logger for tests so all logs are registered by nothing is sent to console
    /// </summary>
    public class MockedReportScope : IDisposable
    {
        public readonly IReportHandler Mock;
        private readonly ReportHubLogger savedInstance;

        public MockedReportScope()
        {
            savedInstance = ReportHub.Instance;
            ReportHub.Initialize(new ReportHubLogger(
                new List<(ReportHandler, IReportHandler)> { (ReportHandler.DebugLog, Mock = Substitute.For<IReportHandler>()) }));
        }

        public void Dispose()
        {
            ReportHub.Initialize(savedInstance);
        }
    }
}

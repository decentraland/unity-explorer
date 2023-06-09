﻿using Diagnostics.ReportsHandling;
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
        private readonly ReportHubLogger savedInstance;

        public readonly IReportHandler Mock;

        public MockedReportScope()
        {
            savedInstance = ReportHub.Instance;

            ReportHub.Instance = new ReportHubLogger(
                new List<(ReportHandler, IReportHandler)> { (ReportHandler.DebugLog, Mock = Substitute.For<IReportHandler>()) });
        }

        public void Dispose()
        {
            ReportHub.Instance = savedInstance;
        }
    }
}

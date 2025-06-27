using JetBrains.Annotations;
using NSubstitute;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Diagnostics.Tests
{
    /// <summary>
    ///     Provides a report logger for tests so all logs are registered by nothing is sent to console
    /// </summary>
    public class MockedReportScope : IDisposable
    {
        public readonly IReportHandler Mock;
        private readonly ReportHubLogger savedInstance;

        public MockedReportScope([CanBeNull] IReportHandler mock = null)
        {
            savedInstance = ReportHub.Instance;

            if (mock == null)
            {
                mock = Substitute.For<IReportHandler>();
                mock.Type.Returns(ReportHandler.DebugLog);
            }

            ReportHub.Initialize(new ReportHubLogger(new List<IReportHandler> { mock }));
        }

        public static ReportHandlerBase CreateHandlerFromBaseClass(out ICategorySeverityMatrix severityMatrix, ReportHandler type = ReportHandler.None, bool debounceEnabled = true)
        {
            severityMatrix = Substitute.For<ICategorySeverityMatrix>();
            severityMatrix.IsEnabled(Arg.Any<string>(), Arg.Any<LogType>()).Returns(true);
            ReportHandlerBase handler = Substitute.For<ReportHandlerBase>(type, severityMatrix, debounceEnabled);
            return handler;
        }

        public static MockedReportScope CreateFromBaseClass(out ReportHandlerBase @base, out ICategorySeverityMatrix severityMatrix, ReportHandler type = ReportHandler.None, bool debounceEnabled = true)
        {
            @base = CreateHandlerFromBaseClass(out severityMatrix, type, debounceEnabled);
            return new MockedReportScope(@base);
        }

        public void Dispose()
        {
            ReportHub.Initialize(savedInstance);
        }
    }
}

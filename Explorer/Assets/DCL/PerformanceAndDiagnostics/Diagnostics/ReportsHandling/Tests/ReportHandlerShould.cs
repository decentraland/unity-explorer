using DCL.Diagnostics;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;

namespace Diagnostics.ReportsHandling.Tests
{
    public class ReportHandlerShould
    {
        private ICategorySeverityMatrix categorySeverityMatrix;
        private ReportHandlerBase reportHandlerBase;

        [SetUp]
        public void SetUp()
        {
            categorySeverityMatrix = Substitute.For<ICategorySeverityMatrix>();
            reportHandlerBase = Substitute.For<ReportHandlerBase>(ReportHandler.DebugLog, categorySeverityMatrix, true);
        }

        [Test]
        public void Log([Values(LogType.Log, LogType.Warning, LogType.Error, LogType.Exception)] LogType logType)
        {
            categorySeverityMatrix.IsEnabled(Arg.Any<string>(), logType).Returns(true);

            reportHandlerBase.Log(logType, new ReportData("TEST"), null, "message");
            reportHandlerBase.Received(1).LogInternal(logType, new ReportData("TEST"), null, "message");
        }

        [Test]
        public void Debounce([Values(LogType.Log, LogType.Warning, LogType.Error, LogType.Exception)] LogType logType)
        {
            categorySeverityMatrix.IsEnabled(Arg.Any<string>(), logType).Returns(true);

            ReportDebounce debounce = ReportDebounce.ASSEMBLY_STATIC;

            reportHandlerBase.Log(logType, new ReportData("TEST", debounce), null, "message");

            // the second message must be debounced
            reportHandlerBase.Log(logType, new ReportData("TEST", debounce), null, "message");
            reportHandlerBase.Received(1).LogInternal(logType, new ReportData("TEST", debounce), null, "message");
        }

        [Ignore("Can't be debounced because exceptions can't be compared")]
        [Test]
        public void DebounceException()
        {
            var e = new ArgumentOutOfRangeException("test_message");

            categorySeverityMatrix.IsEnabled(Arg.Any<string>(), LogType.Exception).Returns(true);

            reportHandlerBase.LogException(e, new ReportData("TEST", ReportDebounce.ASSEMBLY_STATIC), null);
            reportHandlerBase.LogException(e, new ReportData("TEST", ReportDebounce.ASSEMBLY_STATIC), null);

            reportHandlerBase.Received(1).LogExceptionInternal(e, new ReportData("TEST", ReportDebounce.ASSEMBLY_STATIC), null);
        }

        [Ignore("Can't be debounced because exceptions can't be compared")]
        [Test]
        public void DebounceEcsException()
        {
            var e = new EcsSystemException(null, new ArgumentException("test"), new ReportData("TEST", ReportDebounce.ASSEMBLY_STATIC));

            categorySeverityMatrix.IsEnabled(Arg.Any<string>(), LogType.Exception).Returns(true);

            reportHandlerBase.LogException(e);
            reportHandlerBase.LogException(e);

            reportHandlerBase.Received(1).LogExceptionInternal(e);
        }

        [Test]
        public void Filter()
        {
            categorySeverityMatrix.IsEnabled(Arg.Any<string>(), Arg.Is<LogType>(l => l == LogType.Log)).Returns(false);
            categorySeverityMatrix.IsEnabled(Arg.Any<string>(), Arg.Is<LogType>(l => l == LogType.Error)).Returns(true);

            reportHandlerBase.Log(LogType.Log, new ReportData("TEST"), null, "message");
            reportHandlerBase.DidNotReceive().LogInternal(LogType.Log, new ReportData("TEST"), null, "message");

            reportHandlerBase.Log(LogType.Error, new ReportData("TEST"), null, "error");
            reportHandlerBase.Received(1).LogInternal(LogType.Error, new ReportData("TEST"), null, "error");
        }
    }
}

using DCL.Diagnostics;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Diagnostics.ReportsHandling.Tests
{
    public class ReportHandlerShould
    {
        private ICategorySeverityMatrix categorySeverityMatrix;
        private TestHandler reportHandlerBase;


        public void SetUp()
        {
            categorySeverityMatrix = Substitute.For<ICategorySeverityMatrix>();
            reportHandlerBase = Substitute.For<TestHandler>(categorySeverityMatrix, true);
        }


        public void Log([Values(LogType.Log, LogType.Warning, LogType.Error, LogType.Exception)] LogType logType)
        {
            categorySeverityMatrix.IsEnabled(Arg.Any<string>(), logType).Returns(true);

            reportHandlerBase.Log(logType, new ReportData("TEST"), null, "message");
            reportHandlerBase.Received(1).LogTest(logType, new ReportData("TEST"), null, "message");
        }


        public void Debounce([Values(LogType.Log, LogType.Warning, LogType.Error, LogType.Exception)] LogType logType,
            [Values(ReportHint.AssemblyStatic, ReportHint.SessionStatic)] ReportHint hint)
        {
            categorySeverityMatrix.IsEnabled(Arg.Any<string>(), logType).Returns(true);

            reportHandlerBase.Log(logType, new ReportData("TEST", hint), null, "message");

            // the second message must be debounced
            reportHandlerBase.Log(logType, new ReportData("TEST", hint), null, "message");
            reportHandlerBase.Received(1).LogTest(logType, new ReportData("TEST", hint), null, "message");
        }


        public void DebounceException()
        {
            var e = new ArgumentOutOfRangeException("test_message");

            categorySeverityMatrix.IsEnabled(Arg.Any<string>(), LogType.Exception).Returns(true);

            reportHandlerBase.LogException(e, new ReportData("TEST", ReportHint.AssemblyStatic), null);
            reportHandlerBase.LogException(e, new ReportData("TEST", ReportHint.AssemblyStatic), null);

            reportHandlerBase.Received(1).LogExceptionTest(e, null);
        }


        public void DebounceEcsException()
        {
            var e = new EcsSystemException(null, new ArgumentException("test"), new ReportData("TEST", ReportHint.AssemblyStatic));

            categorySeverityMatrix.IsEnabled(Arg.Any<string>(), LogType.Exception).Returns(true);

            reportHandlerBase.LogException(e);
            reportHandlerBase.LogException(e);

            reportHandlerBase.Received(1).LogExceptionTest(e);
        }


        public void Filter()
        {
            categorySeverityMatrix.IsEnabled(Arg.Any<string>(), Arg.Is<LogType>(l => l == LogType.Log)).Returns(false);
            categorySeverityMatrix.IsEnabled(Arg.Any<string>(), Arg.Is<LogType>(l => l == LogType.Error)).Returns(true);

            reportHandlerBase.Log(LogType.Log, new ReportData("TEST"), null, "message");
            reportHandlerBase.DidNotReceive().LogInternal(LogType.Log, new ReportData("TEST"), null, "message");

            reportHandlerBase.Log(LogType.Error, new ReportData("TEST"), null, "error");
            reportHandlerBase.Received(1).LogInternal(LogType.Error, new ReportData("TEST"), null, "error");
        }

        public abstract class TestHandler : ReportHandlerBase
        {
            public TestHandler(ICategorySeverityMatrix matrix, bool debounceEnabled) : base(matrix, debounceEnabled) { }

            public abstract void LogTest(LogType logType, ReportData category, Object context, object message);

            public abstract void LogFormatTest(LogType logType, ReportData category, Object context, object message, params object[] args);

            public abstract void LogExceptionTest<T>(T ecsSystemException) where T: Exception, IManagedEcsException;

            public abstract void LogExceptionTest(Exception exception, Object context);

            internal override void LogInternal(LogType logType, ReportData category, Object context, object message)
            {
                LogTest(logType, category, context, message);
            }

            internal override void LogFormatInternal(LogType logType, ReportData category, Object context, object message, params object[] args)
            {
                LogFormatTest(logType, category, context, message, args);
            }

            internal override void LogExceptionInternal<T>(T ecsSystemException)
            {
                LogExceptionTest(ecsSystemException);
            }

            internal override void LogExceptionInternal(Exception exception, ReportData reportData, Object context)
            {
                LogExceptionTest(exception, context);
            }

            protected sealed override bool DebounceInternal(in object message, in ReportData reportData, LogType logType) =>
                base.DebounceInternal(in message, in reportData, logType);
        }
    }
}

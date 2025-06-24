using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.Diagnostics.Tests
{
    public class FrameDebouncerShould
    {
        private MockedReportScope mockedReportScope;
        private ReportData reportData;
        private ReportHandlerBase handler;

        [SetUp]
        public void SetUp()
        {
            mockedReportScope = MockedReportScope.CreateFromBaseClass(out handler, out _, ReportHandler.Sentry);
            reportData = new ReportData(ReportCategory.FRIENDS, debouncer: new FrameDebouncer(1));
        }

        [TearDown]
        public void Reset()
        {
            MultithreadingUtility.ResetFrameCount();
            mockedReportScope.Dispose();
        }

        [Test]
        public void DebounceExceptions([Values(3, 10)] int count)
        {
            var exception = new InvalidOperationException("Test exception", new Exception("Inner exception"));

            MultithreadingUtility.SetFrameCount(10);

            // Split into 2 halves
            for (var i = 0; i < Mathf.FloorToInt(count / 2f); i++)
                ReportHub.LogException(exception, reportData);

            // Skip frames
            MultithreadingUtility.SetFrameCount(11);

            // The second half
            for (var i = 0; i < Mathf.CeilToInt(count / 2f); i++)
                ReportHub.LogException(exception, reportData);

            // Only one call should be recorded
            handler.Received(1)
                   .LogExceptionInternal(exception, reportData, null);
        }

        [Test]
        public void NotDebounceDistantExceptions([Values(2, 10)] int frameDistance)
        {
            var exception = new InvalidOperationException("Test exception", new Exception("Inner exception"));

            MultithreadingUtility.SetFrameCount(10);

            ReportHub.LogException(exception, reportData);

            // Skip frames
            MultithreadingUtility.SetFrameCount(10 + frameDistance);

            // The second half
            ReportHub.LogException(exception, reportData);

            // All calls should be recorded
            handler.Received(2)
                   .LogExceptionInternal(exception, reportData, null);
        }

        [Test]
        public void NotDebounceDifferentExceptions()
        {
            var exception1 = new InvalidOperationException("Test exception", new Exception("Inner exception"));
            var exception2 = new ArgumentNullException("Test exception 2", new Exception("Inner exception 2"));

            MultithreadingUtility.SetFrameCount(10);

            ReportHub.LogException(exception1, reportData);

            // The second half
            ReportHub.LogException(exception2, reportData);

            // All calls should be recorded
            handler.Received(1)
                   .LogExceptionInternal(exception1, reportData, null);

            handler.Received(1)
                   .LogExceptionInternal(exception2, reportData, null);
        }
    }
}

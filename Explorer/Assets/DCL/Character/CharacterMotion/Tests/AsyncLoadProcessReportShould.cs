using Cysharp.Threading.Tasks;
using DCL.Utilities;
using DCL.Utility.Types;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.CharacterMotion.Tests
{
    /// <summary>
    /// Regression tests for <see cref="AsyncLoadProcessReport.SetException"/>.
    ///
    /// Covers the spurious Sentry error reported in
    /// https://github.com/decentraland/unity-explorer/issues/7380
    /// where an <see cref="OperationCanceledException"/> wrapped inside a generic
    /// <see cref="Exception"/> was surfaced as an error instead of being handled as
    /// a clean cancellation.
    /// </summary>
    [TestFixture]
    public class AsyncLoadProcessReportShould
    {
        [Test]
        public async Task ReturnCancelledWhenDirectOperationCanceledExceptionIsSet()
        {
            var report = AsyncLoadProcessReport.Create(CancellationToken.None);

            report.SetException(new OperationCanceledException());

            EnumResult<TaskError> result = await report.WaitUntilFinishedAsync().AsTask();

            Assert.AreEqual(TaskError.Cancelled, result.Error,
                "A direct OperationCanceledException must be treated as Cancelled");
        }

        [Test]
        public async Task ReturnCancelledWhenWrappedOperationCanceledExceptionIsSet()
        {
            // Reproduces the exact pattern from the Sentry report:
            // some code calls SetException(new Exception("Cancelled: OperationCanceledException",
            // new OperationCanceledException())) instead of SetCancelled().
            var report = AsyncLoadProcessReport.Create(CancellationToken.None);
            var wrappedException = new Exception("Cancelled: OperationCanceledException",
                new OperationCanceledException());

            report.SetException(wrappedException);

            EnumResult<TaskError> result = await report.WaitUntilFinishedAsync().AsTask();

            Assert.AreEqual(TaskError.Cancelled, result.Error,
                "A wrapped OperationCanceledException must be treated as Cancelled, not an error");
        }

        [Test]
        public async Task ReturnErrorWhenUnrelatedExceptionIsSet()
        {
            var report = AsyncLoadProcessReport.Create(CancellationToken.None);

            report.SetException(new Exception("some unrelated error"));

            EnumResult<TaskError> result = await report.WaitUntilFinishedAsync();

            Assert.AreEqual(TaskError.UnexpectedException, result.Error,
                "An unrelated exception must still be surfaced as UnexpectedException");
        }

        [Test]
        public async Task ReturnSuccessWhenProgressReaches1()
        {
            var report = AsyncLoadProcessReport.Create(CancellationToken.None);

            report.SetProgress(1f);

            EnumResult<TaskError> result = await report.WaitUntilFinishedAsync().AsTask();

            Assert.IsTrue(result.Success, "Progress 1.0 should complete successfully");
        }

        [Test]
        public async Task PropagateWrappedCancellationToParentReport()
        {
            var parent = AsyncLoadProcessReport.Create(CancellationToken.None);
            AsyncLoadProcessReport child = parent.CreateChildReport(1f);

            child.SetException(new Exception("inner cancel",
                new OperationCanceledException()));

            // Both parent and child should resolve as cancelled.
            EnumResult<TaskError> childResult = await child.WaitUntilFinishedAsync().AsTask();
            EnumResult<TaskError> parentResult = await parent.WaitUntilFinishedAsync().AsTask();

            Assert.AreEqual(TaskError.Cancelled, childResult.Error,
                "Child should be cancelled");
            Assert.AreEqual(TaskError.Cancelled, parentResult.Error,
                "Parent should also be cancelled when child is cancelled via wrapped exception");
        }
    }
}

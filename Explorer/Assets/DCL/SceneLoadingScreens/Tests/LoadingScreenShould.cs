using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.SceneLoadingScreens.LoadingScreen;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;
using Utility.Types;

namespace DCL.SceneLoadingScreens.Tests
{
    public class LoadingScreenShould
    {
        private static readonly LoadingScreenTimeout TIMEOUT = new()
            { Value = TimeSpan.FromSeconds(60) };

        /// <summary>
        ///     Happy path
        /// </summary>
        [Test]
        [TestCaseSource(nameof(PossibleResults))]
        public async Task ReportResultOfOperationAsync(Result result)
        {
            var loadingScreen = new LoadingScreen.LoadingScreen(CreateMVCManagerNeverFails(), TIMEOUT);

            Result finalRes = await loadingScreen.ShowWhileExecuteTaskAsync(CreateOp, CancellationToken.None);

            async UniTask<Result> CreateOp(AsyncLoadProcessReport report, CancellationToken ct)
            {
                await UniTask.DelayFrame(10);

                // if the internal operation didn't modify the loading report on its own, finalize it
                if (result.Success)
                    report.SetProgress(1.0f);
                else
                    report.SetException(new Exception(result.ErrorMessage));

                return result;
            }

            Assert.AreEqual(result, finalRes);
        }

        [Test]
        [TestCaseSource(nameof(PossibleResults))]
        public async Task FixUpLoadingReportAsync(Result result)
        {
            var loadingScreen = new LoadingScreen.LoadingScreen(CreateMVCManagerNeverFails(), TIMEOUT);
            AsyncLoadProcessReport outerReport = null;

            Result finalRes = await loadingScreen.ShowWhileExecuteTaskAsync(CreateOp, CancellationToken.None);

            async UniTask<Result> CreateOp(AsyncLoadProcessReport report, CancellationToken ct)
            {
                outerReport = report;
                await UniTask.DelayFrame(10);
                return result;
            }

            Assert.AreEqual(result, finalRes);

            // Report must be modified by the loading screen
            Assert.That(outerReport!.GetStatus().TaskStatus, result.Success ? Is.EqualTo(UniTaskStatus.Succeeded) : Is.EqualTo(UniTaskStatus.Faulted));
        }

        [Test]
        public async Task ReduceLoadingScreenExceptionToOperationResultAsync()
        {
            LogAssert.ignoreFailingMessages = true;

            var loadingScreen = new LoadingScreen.LoadingScreen(CreateMVCManagerThrowsException(), TIMEOUT);
            AsyncLoadProcessReport outerReport = null;

            Result finalRes = await loadingScreen.ShowWhileExecuteTaskAsync(CreateOp, CancellationToken.None);

            async UniTask<Result> CreateOp(AsyncLoadProcessReport report, CancellationToken ct)
            {
                outerReport = report;
                await UniTask.DelayFrame(10);
                return Result.SuccessResult();
            }

            Assert.That(finalRes, Is.EqualTo(Result.SuccessResult()));
        }

        [Test]
        public async Task TimeoutFinishAllInternalsOperationsAsync()
        {
            CancellationToken mvcCancellation = CancellationToken.None;
            CancellationToken opCancellation = CancellationToken.None;
            AsyncLoadProcessReport outerReport = null;

            var mvcFinished = false;
            var opFinished = false;

            IMVCManager? mvc = Substitute.For<IMVCManager>();

            mvc.ShowAsync(Arg.Any<ShowCommand<SceneLoadingScreenView, SceneLoadingScreenController.Params>>(), Arg.Any<CancellationToken>())
               .Returns<UniTask>(async info =>
                {
                    mvcCancellation = info.Arg<ShowCommand<SceneLoadingScreenView, SceneLoadingScreenController.Params>>()
                                          .InputData.AsyncLoadProcessReport.WaitUntilFinished()
                                          .ToCancellationToken();

                    await UniTask.Never(mvcCancellation).SuppressCancellationThrow();
                    mvcFinished = true;
                });

            async UniTask<Result> CreateOp(AsyncLoadProcessReport report, CancellationToken ct)
            {
                outerReport = report;
                opCancellation = ct;
                await UniTask.Never(ct).SuppressCancellationThrow();
                opFinished = true;
                return Result.CancelledResult();
            }

            var loadingScreen = new LoadingScreen.LoadingScreen(mvc, new LoadingScreenTimeout { Value = TimeSpan.FromMilliseconds(200) });
            Result result = await loadingScreen.ShowWhileExecuteTaskAsync(CreateOp, CancellationToken.None);

            // let internal operations spin to the end
            await UniTask.Yield();

            Assert.IsTrue(mvcFinished);
            Assert.IsTrue(opFinished);
            Assert.IsTrue(mvcCancellation.IsCancellationRequested);
            Assert.IsTrue(opCancellation.IsCancellationRequested);

            Assert.That(result.ErrorMessage, Is.EqualTo("Load Timeout!"));
            Assert.That(outerReport!.GetStatus().TaskStatus, Is.EqualTo(UniTaskStatus.Faulted));
        }

        [Test]
        public async Task CancelLoadingScreenOnOpFinishedAsync()
        {
            CancellationToken mvcCancellation = CancellationToken.None;
            IMVCManager mvc = Substitute.For<IMVCManager>();

            mvc.ShowAsync(Arg.Any<ShowCommand<SceneLoadingScreenView, SceneLoadingScreenController.Params>>(), Arg.Any<CancellationToken>())
               .Returns<UniTask>(async info =>
                {
                    mvcCancellation = info.Arg<ShowCommand<SceneLoadingScreenView, SceneLoadingScreenController.Params>>()
                                          .InputData.AsyncLoadProcessReport.WaitUntilFinished()
                                          .ToCancellationToken();
                    await UniTask.Never(mvcCancellation).SuppressCancellationThrow();
                });

            var loadingScreen = new LoadingScreen.LoadingScreen(mvc, TIMEOUT);

            await loadingScreen.ShowWhileExecuteTaskAsync(CreateOp, CancellationToken.None);

            async UniTask<Result> CreateOp(AsyncLoadProcessReport report, CancellationToken ct)
            {
                await UniTask.DelayFrame(10);

                // if the internal operation didn't modify the loading report on its own, finalize it
                report.SetProgress(1.0f);
                return Result.SuccessResult();
            }

            await UniTask.Yield();

            Assert.That(mvcCancellation.IsCancellationRequested, Is.True);
        }

        private IMVCManager CreateMVCManagerThrowsException()
        {
            IMVCManager? sub = Substitute.For<IMVCManager>();

            sub.ShowAsync(Arg.Any<ShowCommand<SceneLoadingScreenView, SceneLoadingScreenController.Params>>(), Arg.Any<CancellationToken>())
               .Returns<UniTask>(async _ =>
                {
                    await UniTask.DelayFrame(2);
                    throw new Exception("MVC Exception");
                });

            return sub;
        }

        private IMVCManager CreateMVCManagerNeverFails()
        {
            IMVCManager? sub = Substitute.For<IMVCManager>();

            sub.ShowAsync(Arg.Any<ShowCommand<SceneLoadingScreenView, SceneLoadingScreenController.Params>>(), Arg.Any<CancellationToken>())
               .Returns(info => info.Arg<ShowCommand<SceneLoadingScreenView, SceneLoadingScreenController.Params>>().InputData.AsyncLoadProcessReport.WaitUntilFinished());

            return sub;
        }

        private static Result[] PossibleResults() =>
            new[]
            {
                Result.SuccessResult(),
                Result.ErrorResult("TEST ERROR"),
                Result.CancelledResult(),
            };
    }
}

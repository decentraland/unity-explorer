using Cysharp.Threading.Tasks;
using Global.Dynamic;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace Global.Tests.EditMode
{
    public class BootstraperShould
    {
        private IMVCManager mvcManager;

        [SetUp]
        public async void Setup()
        {
            mvcManager = Substitute.For<IMVCManager>();
            await UniTask.Yield();
        }

        [Test]
        public async Task DisableHudOnStartup_OnlyTouchesMVCCanvases_NotSceneUIDocuments()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => mvcManager != null);

            await Bootstrap.DisableHudOnStartupAsync(mvcManager, CancellationToken.None);

            mvcManager.Received(1).SetAllViewsCanvasActive(false);
            mvcManager.DidNotReceiveWithAnyArgs().SetAllViewsCanvasActive(default(IController), default);
        }

        [Test]
        public async Task DisableHudOnStartup_DoesNothing_WhenCancelledBeforeFrameAdvances()
        {
            // Workaround for Unity bug not awaiting async Setup correctly
            await UniTask.WaitUntil(() => mvcManager != null);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Bootstrap.DisableHudOnStartupAsync(mvcManager, cts.Token);

            mvcManager.DidNotReceiveWithAnyArgs().SetAllViewsCanvasActive(default);
        }
    }
}

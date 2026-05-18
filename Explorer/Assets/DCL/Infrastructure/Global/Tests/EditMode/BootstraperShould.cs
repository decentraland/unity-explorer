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
        [Test]
        public async Task DisableHudOnStartup_OnlyTouchesMVCCanvases_NotSceneUIDocuments()
        {
            IMVCManager mvcManager = Substitute.For<IMVCManager>();

            await Bootstrap.DisableHudOnStartupAsync(mvcManager, CancellationToken.None);

            mvcManager.Received(1).SetAllViewsCanvasActive(false);
            mvcManager.DidNotReceiveWithAnyArgs().SetAllViewsCanvasActive(default(IController), default);
        }

        [Test]
        public async Task DisableHudOnStartup_DoesNothing_WhenCancelledBeforeFrameAdvances()
        {
            IMVCManager mvcManager = Substitute.For<IMVCManager>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Bootstrap.DisableHudOnStartupAsync(mvcManager, cts.Token);

            mvcManager.DidNotReceiveWithAnyArgs().SetAllViewsCanvasActive(default);
        }
    }
}

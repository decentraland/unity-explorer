using Cysharp.Threading.Tasks;
using Global.Dynamic;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System.Collections;
using System.Threading;
using UnityEngine.TestTools;

namespace Global.Tests.EditMode
{
    public class BootstraperShould
    {
        [UnityTest]
        public IEnumerator DisableHudOnStartup_OnlyTouchesMVCCanvases_NotSceneUIDocuments()
        {
            IMVCManager mvcManager = Substitute.For<IMVCManager>();

            yield return Bootstrap.DisableHudOnStartupAsync(mvcManager, CancellationToken.None).ToCoroutine();

            mvcManager.Received(1).SetAllViewsCanvasActive(false);
            mvcManager.DidNotReceiveWithAnyArgs().SetAllViewsCanvasActive(default(IController), default);
        }

        [UnityTest]
        public IEnumerator DisableHudOnStartup_DoesNothing_WhenCancelledBeforeFrameAdvances()
        {
            IMVCManager mvcManager = Substitute.For<IMVCManager>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            yield return Bootstrap.DisableHudOnStartupAsync(mvcManager, cts.Token).ToCoroutine();

            mvcManager.DidNotReceiveWithAnyArgs().SetAllViewsCanvasActive(default);
        }
    }
}

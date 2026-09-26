#if ALTTESTER
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MVC.Tests
{
    public class ViewBaseAltTesterProbeShould
    {
        private class ProbeTestView : ViewBase { }

        private class GatedShowProbeView : ViewBase
        {
            public readonly UniTaskCompletionSource ShowGate = new ();

            protected override UniTask PlayShowAnimationAsync(CancellationToken ct) => ShowGate.Task;
        }

        private class GatedHideProbeView : ViewBase
        {
            public readonly UniTaskCompletionSource HideGate = new ();

            protected override UniTask PlayHideAnimationAsync(CancellationToken ct) => HideGate.Task;
        }

        private ProbeTestView view;

        [SetUp]
        public void SetUp()
        {
            view = new GameObject(nameof(ProbeTestView)).AddComponent<ProbeTestView>();
            view.gameObject.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void ReportUnknownBeforeAnyShow()
        {
            Assert.AreEqual("Unknown", AltTesterViewProbe.GetState("NeverShownView"));
        }

        [Test]
        public async Task ReportShownAfterShowCompletes()
        {
            await view.ShowAsync(CancellationToken.None);
            Assert.AreEqual("Shown", AltTesterViewProbe.GetState(nameof(ProbeTestView)));
        }

        [Test]
        public async Task ReportHiddenAfterHideCompletes()
        {
            await view.ShowAsync(CancellationToken.None);
            await view.HideAsync(CancellationToken.None);
            Assert.AreEqual("Hidden", AltTesterViewProbe.GetState(nameof(ProbeTestView)));
        }

        [Test]
        public async Task ListKnownViewsAndSnapshotThem()
        {
            await view.ShowAsync(CancellationToken.None);
            StringAssert.Contains(nameof(ProbeTestView), AltTesterViewProbe.GetKnownViews());
            StringAssert.Contains("\"" + nameof(ProbeTestView) + "\":\"Shown\"", AltTesterViewProbe.Snapshot());
        }

        [Test]
        public async Task NotReportShownUntilShowAnimationCompletes()
        {
            var gatedView = new GameObject(nameof(GatedShowProbeView)).AddComponent<GatedShowProbeView>();
            gatedView.gameObject.SetActive(false);

            try
            {
                UniTask show = gatedView.ShowAsync(CancellationToken.None);

                Assert.AreEqual("Showing", AltTesterViewProbe.GetState(nameof(GatedShowProbeView)),
                    "Fails if Shown is reported right after SetActive(true), before the animation finishes.");

                gatedView.ShowGate.TrySetResult();
                await show;

                Assert.AreEqual("Shown", AltTesterViewProbe.GetState(nameof(GatedShowProbeView)));
            }
            finally
            {
                Object.DestroyImmediate(gatedView.gameObject);
            }
        }

        [Test]
        public async Task NotReportHiddenUntilHideAnimationCompletes()
        {
            var gatedView = new GameObject(nameof(GatedHideProbeView)).AddComponent<GatedHideProbeView>();
            gatedView.gameObject.SetActive(false);

            try
            {
                await gatedView.ShowAsync(CancellationToken.None);

                UniTask hide = gatedView.HideAsync(CancellationToken.None);

                Assert.AreEqual("Hiding", AltTesterViewProbe.GetState(nameof(GatedHideProbeView)));
                Assert.IsTrue(gatedView.gameObject.activeSelf);

                gatedView.HideGate.TrySetResult();
                await hide;

                Assert.AreEqual("Hidden", AltTesterViewProbe.GetState(nameof(GatedHideProbeView)));
            }
            finally
            {
                Object.DestroyImmediate(gatedView.gameObject);
            }
        }
    }
}
#endif

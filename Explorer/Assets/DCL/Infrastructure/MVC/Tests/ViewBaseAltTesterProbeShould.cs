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
    }
}
#endif

using DCL.Diagnostics;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Utility.Multithreading;

namespace CrdtEcsBridge.JsModulesImplementation.Tests
{
    [TestFixture]
    public class MultiThreadSyncShould
    {
        // Regression: MultiThreadSync.Acquire left a timed-out waiter in the queue → next acquire null-derefs (KM5/NQC/NQD).
        // The acquire timeout is a hard-coded 10s const with no injectable knob, so this test runs on the real timeout.
        [Test]
        public void RemoveTimedOutWaiterFromQueueSoTheNextAcquireSucceeds()
        {
            var multiThreadSync = new MultiThreadSync(new SceneShortInfo());

            try
            {
                var mainOwner = new MultiThreadSync.Owner("MAIN");
                var backgroundOwner = new MultiThreadSync.Owner("BACKGROUND");

                MultiThreadSync.Scope mainScope = multiThreadSync.GetScope(mainOwner);

                Exception? backgroundError = null;

                Task background = Task.Run(() =>
                {
                    try
                    {
                        using MultiThreadSync.Scope scope = multiThreadSync.GetScope(backgroundOwner);
                    }
                    catch (Exception e) { backgroundError = e; }
                });

                Assert.That(background.Wait(TimeSpan.FromSeconds(30)), Is.True,
                    "Background acquire never returned - the timeout path did not fire.");
                Assert.That(backgroundError, Is.InstanceOf<TimeoutException>(),
                    "Background acquire should time out with a TimeoutException.");

                try { mainScope.Dispose(); }
                catch (TimeoutException) { /* held longer than TIMEOUT: expected */ }

                Assert.DoesNotThrow(() =>
                {
                    using MultiThreadSync.Scope scope = multiThreadSync.GetScope(mainOwner);
                }, "A timed-out waiter must be removed from the queue so the next acquire succeeds.");
            }
            finally
            {
                multiThreadSync.Dispose();
            }
        }
    }
}

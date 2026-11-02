using DCL.Diagnostics;
using NUnit.Framework;
using System;
using System.Threading;
using UnityEngine;

namespace Utility.Multithreading.Tests
{
    public class MultiThreadSyncShould
    {
        [Test]
        public void AcquireAfterTimedOutWaiterLeavesQueue()
        {
            using var sync = new MultiThreadSync(new SceneShortInfo(Vector2Int.zero, "test"), TimeSpan.FromMilliseconds(50));
            MultiThreadSync.Scope a = sync.GetScope(new MultiThreadSync.Owner("A"));

            Assert.Throws<TimeoutException>(() => sync.GetScope(new MultiThreadSync.Owner("B")));

            // A was held past the timeout while B waited, so its release reports it
            Assert.Throws<TimeoutException>(() => a.Dispose());

            Assert.DoesNotThrow(() => sync.GetScope(new MultiThreadSync.Owner("C")).Dispose());
        }

        [Test]
        public void ReleaseIfAcquiredDoesNotReleaseTwiceAfterSlowScope()
        {
            using var sync = new MultiThreadSync(new SceneShortInfo(Vector2Int.zero, "test"), TimeSpan.FromMilliseconds(20));
            var boxed = new MultiThreadSync.BoxedScope(sync);

            boxed.Acquire(new MultiThreadSync.Owner("A"));
            Thread.Sleep(40);

            Assert.Throws<TimeoutException>(boxed.ReleaseIfAcquired);
            Assert.DoesNotThrow(boxed.ReleaseIfAcquired);
        }
    }
}

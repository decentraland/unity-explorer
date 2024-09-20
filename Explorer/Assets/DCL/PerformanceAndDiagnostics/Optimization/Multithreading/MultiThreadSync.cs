using DCL.Diagnostics;
using System;
using System.Threading;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace Utility.Multithreading
{
    public class MultiThreadSync : IDisposable
    {
        internal readonly struct AcquisitionInfo
        {
            public readonly Owner Owner;
            public readonly DateTime AcquiredAt;

            public AcquisitionInfo(Owner owner, DateTime acquiredAt)
            {
                Owner = owner;
                AcquiredAt = acquiredAt;
            }
        }

        private static readonly ProfilerMarker COMMON_SAMPLER;

        private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(10);

        private readonly object monitor = new ();

        private readonly Queue<Owner> queue = new ();

        private readonly SyncLogsBuffer syncLogsBuffer;

        private readonly CustomSampler perSceneSampler;

        private readonly CancellationTokenSource cts = new ();

        private AcquisitionInfo? currentAcquisitionInfo;
        private bool isDisposing;

        static MultiThreadSync()
        {
            COMMON_SAMPLER = new ProfilerMarker("MultithreadSync.Wait");
        }

        public MultiThreadSync(SceneShortInfo sceneInfo)
        {
            syncLogsBuffer = new SyncLogsBuffer(sceneInfo, 20);
            perSceneSampler = CustomSampler.Create("MultithreadSync.Wait " + sceneInfo.BaseParcel);
        }

        public void Dispose()
        {
            lock (monitor)
            {
                isDisposing = true;

                cts.SafeCancelAndDispose();

                // Dispose owners currently in use
                // Don't accept any new ones

                foreach (Owner owner in queue)
                    owner.Dispose();

                queue.Clear();
            }
        }

        private void Acquire(Owner owner)
        {
            bool shouldWait;

            lock (monitor)
            {
#if SYNC_DEBUG
                syncLogsBuffer.Report("MultithreadSync Acquire start for:", owner.Name);
#endif

                if (isDisposing)
                    throw new ObjectDisposedException(nameof(MultiThreadSync));

                shouldWait = queue.Count > 0;
                queue.Enqueue(owner);
            }

            // There is already one thread doing work. Wait for the signal
            if (shouldWait)
            {
                if (!owner.Wait(TIMEOUT, cts.Token, out bool wasCancelled) && !wasCancelled)
                {
                    lock (monitor)
                    {
                        DateTime time = DateTime.Now;
                        AcquisitionInfo current = currentAcquisitionInfo!.Value;
                        TimeSpan difference = time - current.AcquiredAt;

                        syncLogsBuffer.Print();
                        throw new TimeoutException($"{nameof(MultiThreadSync)} timeout, cannot acquire for: {owner.Name}, current owner: \"{current.Owner!.Name}\" takes too long: {difference.TotalSeconds}");
                    }
                }
            }

            lock (monitor)
            {
                currentAcquisitionInfo = new AcquisitionInfo(owner, DateTime.Now);

#if SYNC_DEBUG
                syncLogsBuffer.Report("MultithreadSync Acquire finished for:", owner.Name);
#endif
            }
        }

        private void Release(Owner owner)
        {
            lock (monitor)
            {
                string source = owner.Name;

#if SYNC_DEBUG
                syncLogsBuffer.Report("MultithreadSync Release start for:", source);
#endif

                if (isDisposing)
                    return;

                // If the queue is empty, then our logic is wrong
                if (queue.TryDequeue(out Owner? finishedWaiter))
                {
                    // The one releasing should be the one at the top of the queue
                    if (owner != finishedWaiter)
                    {
                        syncLogsBuffer.Print();
                        throw new OwnerMismatchException(owner, finishedWaiter);
                    }

                    finishedWaiter.Reset();

                    if (queue.TryPeek(out Owner? next))
                        next.Set(); // Signal the next waiter in line

#if SYNC_DEBUG
                    syncLogsBuffer.Report("MultithreadSync Release finished for:", source);
#endif
                }
#if SYNC_DEBUG
                else
                    syncLogsBuffer.Report("MultithreadSync Release finished CANNOT", source);
#endif

                currentAcquisitionInfo = null;
            }
        }

        public Scope GetScope(Owner source)
        {
            COMMON_SAMPLER.Begin(source.Name);
            perSceneSampler.Begin();

            Scope scope;

            try { scope = new Scope(this, source); }
            finally
            {
                perSceneSampler.End();
                COMMON_SAMPLER.End();
            }

            return scope;
        }

        public class OwnerMismatchException : Exception
        {
            private readonly Owner releasingOwner;
            private readonly Owner firstInQueueOwner;

            public override string Message => $"Releasing owner {releasingOwner.Name} != Queue owner {firstInQueueOwner.Name}";

            public OwnerMismatchException(Owner releasingOwner, Owner firstInQueueOwner)
            {
                this.releasingOwner = releasingOwner;
                this.firstInQueueOwner = firstInQueueOwner;
            }
        }

        public class Owner
        {
            private readonly ManualResetEventSlim eventSlim = new (false);
            public readonly string Name;

            public Owner(string name)
            {
                Name = name;
            }

            public bool Wait(TimeSpan timeout, CancellationToken ct, out bool wasCancelled)
            {
                try
                {
                    wasCancelled = false;
                    return eventSlim.Wait(timeout, ct);
                }
                catch (OperationCanceledException)
                {
                    wasCancelled = true;
                    return false;
                }
            }

            public void Set()
            {
                eventSlim.Set();
            }

            public void Reset()
            {
                eventSlim.Reset();
            }

            public void Dispose()
            {
                eventSlim.Dispose();
            }
        }

        public readonly struct Scope : IDisposable
        {
            private readonly MultiThreadSync multiThreadSync;
            private readonly Owner source;
            private readonly DateTime start;

            public Scope(MultiThreadSync multiThreadSync, Owner source)
            {
                this.multiThreadSync = multiThreadSync;
                this.source = source;
                multiThreadSync.Acquire(source);
                start = DateTime.Now;
            }

            public void Dispose()
            {
                multiThreadSync.Release(source);

                if (DateTime.Now - start > TIMEOUT)
                    throw new TimeoutException($"{nameof(MultiThreadSync)} source {source.Name} took too much time! cannot release for: {source.Name}");
            }
        }

        public class BoxedScope
        {
            private readonly MultiThreadSync multiThreadSync;
            private Scope scope;
            private bool isScoped;

            public BoxedScope(MultiThreadSync multiThreadSync)
            {
                this.multiThreadSync = multiThreadSync;
                scope = default(Scope);
                isScoped = false;
            }

            public void Acquire(Owner source)
            {
                scope = multiThreadSync.GetScope(source);
                isScoped = true;
            }

            public void ReleaseIfAcquired()
            {
                if (isScoped)
                {
                    scope.Dispose();
                    isScoped = false;
                }
            }
        }
    }
}

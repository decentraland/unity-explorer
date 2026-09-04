using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using Utility.Multithreading;

namespace Utility
{
    public interface IEventBus
    {
        void Publish<T>(T evt);
        IDisposable Subscribe<T>(Action<T> handler);
    }

    public class EventBus : IEventBus
    {
        private readonly bool invokeSubscribersOnMainThread;

        public EventBus(bool invokeSubscribersOnMainThread)
        {
            this.invokeSubscribersOnMainThread = invokeSubscribersOnMainThread;
        }

        private readonly Dictionary<Type, Delegate> handlers = new();

        public void Publish<T>(T evt)
        {
            if (handlers.TryGetValue(typeof(T), out var del))
            {
                var typedDelegate = (Action<T>)del;

                if (invokeSubscribersOnMainThread && !PlayerLoopHelper.IsMainThread)
                    PooledContinuation<T>.Schedule(typedDelegate, evt);
                else
                    typedDelegate?.Invoke(evt);
            }
        }

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            var eventType = typeof(T);
            handlers[eventType] = Delegate.Combine(
                handlers.GetValueOrDefault(eventType),
                handler
            );
            return new Unsubscriber<T>(this, handler);
        }

        /// <summary>
        ///     Pooled, closure-free carrier for the main-thread hop. State is copied to locals and the
        ///     entry recycled before handlers run, so reentrant publishes are safe and payloads are not retained.
        /// </summary>
        private sealed class PooledContinuation<T>
        {
            private static readonly DCLConcurrentQueue<PooledContinuation<T>> POOL = new ();

            private readonly Action run;
            private Action<T>? typedDelegate;
            // Stamped by Schedule() before Run() reads it; default! suppresses the unconstrained-T nullable warning
            private T evt = default!;

            private PooledContinuation()
            {
                run = Run;
            }

            public static void Schedule(Action<T> typedDelegate, T evt)
            {
                if (!POOL.TryDequeue(out PooledContinuation<T> continuation))
                    continuation = new PooledContinuation<T>();

                continuation.typedDelegate = typedDelegate;
                continuation.evt = evt;
                PlayerLoopHelper.AddContinuation(PlayerLoopTiming.Update, continuation.run);
            }

            private void Run()
            {
                // Reachable only if the player loop ran the same continuation twice; recycling here would double-enqueue the entry.
                if (typedDelegate is not { } invokeTarget)
                {
                    ReportHub.LogError(ReportCategory.UNSPECIFIED, "EventBus pooled continuation ran without a stamped delegate: exactly-once scheduling was violated and an event was dropped");
                    return;
                }

                T payload = evt;
                typedDelegate = null;
                evt = default!;
                POOL.Enqueue(this);

                invokeTarget.Invoke(payload);
            }
        }

        private class Unsubscriber<T> : IDisposable
        {
            private readonly EventBus bus;
            private readonly Action<T> handler;

            public Unsubscriber(EventBus bus, Action<T> handler)
            {
                (this.bus, this.handler) = (bus, handler);
            }

            public void Dispose()
            {
                var key = typeof(T);
                if (bus.handlers.TryGetValue(key, out var existing))
                {
                    var next = Delegate.Remove(existing, handler);
                    if (next == null)
                        bus.handlers.Remove(key);
                    else
                        bus.handlers[key] = next;
                }
            }
        }
    }
}

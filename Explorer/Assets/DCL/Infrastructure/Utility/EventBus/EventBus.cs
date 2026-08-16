using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

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
        ///     Carries the delegate snapshot and event payload across the main-thread hop without a
        ///     compiler-synthesized closure; entries are pooled so steady-state publishes allocate nothing.
        ///     State is copied to locals and the entry recycled before the handlers run, so reentrant
        ///     publishes are safe and class-typed payloads are not retained by the pool.
        /// </summary>
        private sealed class PooledContinuation<T>
        {
            private static readonly ConcurrentQueue<PooledContinuation<T>> POOL = new ();

            private readonly Action run;
            private Action<T>? typedDelegate;
            private T evt = default!;

            private PooledContinuation()
            {
                run = Run;
            }

            public static void Schedule(Action<T> typedDelegate, T evt)
            {
                if (!POOL.TryDequeue(out PooledContinuation<T>? continuation))
                    continuation = new PooledContinuation<T>();

                continuation.typedDelegate = typedDelegate;
                continuation.evt = evt;
                PlayerLoopHelper.AddContinuation(PlayerLoopTiming.Update, continuation.run);
            }

            private void Run()
            {
                if (typedDelegate is not { } invokeTarget)
                    return;

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

using System;
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
        private readonly Dictionary<Type, Delegate> handlers = new();

        public void Publish<T>(T evt)
        {
            if (handlers.TryGetValue(typeof(T), out var del))
                ((Action<T>)del)?.Invoke(evt);
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

        private class Unsubscriber<T> : IDisposable
        {
            private readonly EventBus bus;
            private readonly Action<T> handler;

            public Unsubscriber(EventBus bus, Action<T> handler)
                => (this.bus, this.handler) = (bus, handler);

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

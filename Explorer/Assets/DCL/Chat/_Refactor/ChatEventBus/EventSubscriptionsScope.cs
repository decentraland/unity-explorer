namespace DCL.Chat.EventBus
{
    using System;
    using System.Collections.Generic;

    public class EventSubscriptionScope : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        public void Add(IDisposable subscription)
        {
            _subscriptions.Add(subscription);
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();
        }
    }
}
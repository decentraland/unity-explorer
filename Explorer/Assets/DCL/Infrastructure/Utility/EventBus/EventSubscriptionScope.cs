using System;
using System.Collections.Generic;

namespace Utility
{
    public class EventSubscriptionScope : IDisposable
    {
        private readonly List<IDisposable> subscriptions = new ();

        public void Add(IDisposable subscription)
        {
            subscriptions.Add(subscription);
        }

        public void Dispose()
        {
            foreach (IDisposable? subscription in subscriptions)
            {
                subscription.Dispose();
            }

            subscriptions.Clear();
        }
    }
}

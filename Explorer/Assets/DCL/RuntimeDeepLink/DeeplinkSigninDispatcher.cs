using System;
using UnityEngine;

namespace DCL.RuntimeDeepLink
{
    /// <inheritdoc cref="IDeeplinkSigninDispatcher" />
    public class DeeplinkSigninDispatcher : IDeeplinkSigninDispatcher
    {
        private Subscription? current;

        public bool HasSubscriber => current != null;

        public IDisposable Subscribe(Action<string> onSigninReceived, string? expectedRequestId = null)
        {
            Debug.Log("[DLDBG] Dispatcher.Subscribe"); // TODO: temporary deep-link debug log, remove.

            var subscription = new Subscription(this, onSigninReceived);
            current = subscription;
            return subscription;
        }

        public void Dispatch(string identityId, string? sourceRequestId = null)
        {
            Debug.Log($"[DLDBG] Dispatcher.Dispatch id='{identityId}' hasSubscriber={current != null}"); // TODO: temporary deep-link debug log, remove.

            // Stage 1: the sourceRequestId / expectedRequestId correlation is intentionally not yet applied.
            current?.Handler(identityId);
        }

        private void Remove(Subscription subscription)
        {
            if (current != subscription)
                return;

            current = null;
        }

        private sealed class Subscription : IDisposable
        {
            public readonly Action<string> Handler;
            private DeeplinkSigninDispatcher? owner;

            public Subscription(DeeplinkSigninDispatcher owner, Action<string> handler)
            {
                this.owner = owner;
                Handler = handler;
            }

            public void Dispose()
            {
                owner?.Remove(this);
                owner = null;
            }
        }
    }
}

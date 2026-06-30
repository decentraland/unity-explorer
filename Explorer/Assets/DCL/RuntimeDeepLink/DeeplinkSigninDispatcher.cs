using System;
using UnityEngine;

namespace DCL.RuntimeDeepLink
{
    /// <inheritdoc cref="IDeeplinkSigninDispatcher" />
    public class DeeplinkSigninDispatcher : IDeeplinkSigninDispatcher
    {
        private string? bufferedIdentityId;
        private Subscription? current;

        public IDisposable Subscribe(Action<string> onSigninReceived, string? expectedRequestId = null)
        {
            Debug.Log($"[DLDBG] Dispatcher.Subscribe (buffered={bufferedIdentityId ?? "<null>"})");

            var subscription = new Subscription(this, onSigninReceived);
            current = subscription;

            if (bufferedIdentityId != null)
                onSigninReceived(bufferedIdentityId);

            return subscription;
        }

        public void Dispatch(string identityId, string? sourceRequestId = null)
        {
            Debug.Log($"[DLDBG] Dispatcher.Dispatch id='{identityId}' hasSubscriber={current != null}");

            // Stage 1: the sourceRequestId / expectedRequestId correlation is intentionally not yet applied.
            bufferedIdentityId = identityId;
            current?.Handler(identityId);
        }

        private void Remove(Subscription subscription)
        {
            if (current != subscription)
                return;

            current = null;
            bufferedIdentityId = null;
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

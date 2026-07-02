using System;

namespace DCL.RuntimeDeepLink
{
    /// <summary>
    ///     Single-event pub/sub for <c>signin</c> deep links.
    /// </summary>
    public class DeeplinkSigninDispatcher
    {
        private Subscription? current;

        /// <summary>
        ///     Whether a live subscriber is currently listening for signin deep links.
        /// </summary>
        public bool HasSubscriber => current != null;

        /// <summary>
        ///     Subscribes to incoming signin deep links.
        /// </summary>
        /// <param name="onSigninReceived">Invoked with the <c>identityId</c> carried by the deep link.</param>
        public IDisposable Subscribe(Action<string> onSigninReceived)
        {
            var subscription = new Subscription(this, onSigninReceived);
            current = subscription;
            return subscription;
        }

        /// <summary>
        ///     Delivers a signin deep link to the active subscriber, if any.
        /// </summary>
        /// <param name="identityId">The opaque identity id carried by the deep link.</param>
        public void Dispatch(string identityId)
        {
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

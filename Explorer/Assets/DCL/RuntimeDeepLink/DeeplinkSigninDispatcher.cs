using Global.AppArgs;
using System;

namespace DCL.RuntimeDeepLink
{
    /// <inheritdoc cref="IDeeplinkSigninDispatcher" />
    public class DeeplinkSigninDispatcher : IDeeplinkSigninDispatcher
    {
        private string? bufferedIdentityId;
        private Subscription? current;

        public DeeplinkSigninDispatcher() { }

        /// <summary>
        ///     Cold-start path: when the OS launches the process via a <c>decentraland://?signin=...</c> deep link, the
        ///     value is parsed into the app-args before any subscriber exists. Reading it here buffers it so the first
        ///     subscriber (the deep-link auth state) receives it on subscribe.
        /// </summary>
        public DeeplinkSigninDispatcher(IAppArgs appArgs)
        {
            if (appArgs.TryGetValue(AppArgsFlags.SIGNIN, out string? signin) && !string.IsNullOrEmpty(signin))
                bufferedIdentityId = signin;
        }

        public IDisposable Subscribe(Action<string> onSigninReceived, string? expectedRequestId = null)
        {
            var subscription = new Subscription(this, onSigninReceived);
            current = subscription;

            if (bufferedIdentityId != null)
                onSigninReceived(bufferedIdentityId);

            return subscription;
        }

        public void Dispatch(string identityId, string? sourceRequestId = null)
        {
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

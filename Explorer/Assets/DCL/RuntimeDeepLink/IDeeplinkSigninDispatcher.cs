using System;

namespace DCL.RuntimeDeepLink
{
    /// <summary>
    ///     Single-event pub/sub for <c>signin</c> deep links. Bridges the producer (<c>DeepLinkHandle</c>, which parses
    ///     the runtime-arriving <c>signin</c> deep link) and the consumer (the deep-link login, which awaits it). Only a
    ///     live subscriber receives a signin; there is no buffering, so a signin dispatched with no subscriber is dropped.
    /// </summary>
    public interface IDeeplinkSigninDispatcher
    {
        /// <summary>
        ///     Whether a live subscriber is currently listening for signin deep links.
        /// </summary>
        bool HasSubscriber { get; }

        /// <summary>
        ///     Subscribes to incoming signin deep links. Dispose the returned handle to stop listening.
        /// </summary>
        /// <param name="onSigninReceived">Invoked with the <c>identityId</c> carried by the deep link.</param>
        /// <param name="expectedRequestId">
        ///     Forward-compatible correlation key. Unused in Stage 1; in Stage 2 the dispatcher drops any dispatch
        ///     whose <c>sourceRequestId</c> does not match.
        /// </param>
        IDisposable Subscribe(Action<string> onSigninReceived, string? expectedRequestId = null);

        /// <summary>
        ///     Delivers a signin deep link to the active subscriber, if any. Dispatched with no subscriber, the signin
        ///     is dropped; callers gate on <see cref="HasSubscriber" /> before dispatching.
        /// </summary>
        /// <param name="identityId">The opaque identity id carried by the deep link.</param>
        /// <param name="sourceRequestId">
        ///     Forward-compatible correlation key carried by the deep link. Unused in Stage 1.
        /// </param>
        void Dispatch(string identityId, string? sourceRequestId = null);
    }
}

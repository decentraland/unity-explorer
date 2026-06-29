using System;

namespace DCL.RuntimeDeepLink
{
    /// <summary>
    ///     Single-event pub/sub for <c>signin</c> deep links. Bridges the producer (<c>DeepLinkHandle</c>, which parses
    ///     the runtime-arriving <c>signin</c> deep link) and the consumer (the deep-link login, which awaits it) by
    ///     replaying the most recent unconsumed signin to a late subscriber.
    /// </summary>
    public interface IDeeplinkSigninDispatcher
    {
        /// <summary>
        ///     Subscribes to incoming signin deep links. Replays the most recent unconsumed signin immediately if one
        ///     is buffered. Dispose the returned handle to stop listening and clear the buffer.
        /// </summary>
        /// <param name="onSigninReceived">Invoked with the <c>identityId</c> carried by the deep link.</param>
        /// <param name="expectedRequestId">
        ///     Forward-compatible correlation key. Unused in Stage 1; in Stage 2 the dispatcher drops any dispatch
        ///     whose <c>sourceRequestId</c> does not match.
        /// </param>
        IDisposable Subscribe(Action<string> onSigninReceived, string? expectedRequestId = null);

        /// <summary>
        ///     Delivers a signin deep link to the active subscriber, or buffers it for the next subscriber.
        /// </summary>
        /// <param name="identityId">The opaque identity id carried by the deep link.</param>
        /// <param name="sourceRequestId">
        ///     Forward-compatible correlation key carried by the deep link. Unused in Stage 1.
        /// </param>
        void Dispatch(string identityId, string? sourceRequestId = null);
    }
}

using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.RuntimeDeepLink
{
    /// <summary>
    ///     Subscribes to <see cref="IDeeplinkSigninDispatcher" /> and resolves with the first <c>identityId</c> delivered,
    ///     applying a timeout and honoring external cancellation. Disposing the subscription on completion clears the
    ///     dispatcher buffer so a deep link consumed (or cancelled) by one attempt does not bleed into the next.
    /// </summary>
    public static class DeeplinkIdentityWaiter
    {
        public static async UniTask<string> WaitForSigninAsync(
            IDeeplinkSigninDispatcher dispatcher,
            TimeSpan timeout,
            string? expectedRequestId,
            CancellationToken ct)
        {
            var completionSource = new UniTaskCompletionSource<string>();

            using IDisposable subscription = dispatcher.Subscribe(identityId => completionSource.TrySetResult(identityId), expectedRequestId);

            // Realtime, not the default DeltaTime: the wait spans the user switching to the browser and back, during
            // which the app is backgrounded and game time stalls. Only wall-clock measures the deadline correctly.
            return await completionSource.Task.Timeout(timeout, DelayType.Realtime).AttachExternalCancellation(ct);
        }
    }
}

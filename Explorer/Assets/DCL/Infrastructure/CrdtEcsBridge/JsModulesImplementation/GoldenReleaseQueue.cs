using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Utility;

namespace CrdtEcsBridge.JsModulesImplementation
{
    /// <summary>
    ///     Golden-capture determinism: held scene-network completions (fetch resolutions, websocket
    ///     rejections) must reach the JS microtask queue in the same order every run, or
    ///     order-sensitive scene init flips between boots even when every payload is pinned.
    ///     Callers take a ticket at issue time — issue order is scene-deterministic — and the
    ///     queue releases exactly one ticket at a time, in ticket order, once the phase-lock
    ///     release passes.
    /// </summary>
    internal static class GoldenReleaseQueue
    {
        private static readonly double RELEASE_AT_SECONDS = (double.TryParse(
            Environment.GetEnvironmentVariable("DCL_GOLDEN_READY_WAIT"), out double w) ? w : 240.0) - 45.0;

        private static int nextTicket;
        private static int released;

        public static bool Enabled { get; } = Environment.GetEnvironmentVariable("DCL_PLAZABENCH_DETERM_CLOCK") == "1";

        public static int TakeTicket() =>
            Interlocked.Increment(ref nextTicket);

        public static async UniTask AwaitTurnAsync(int ticket, CancellationToken ct)
        {
            int lastSeenReleased = -1;
            var stalledSince = 0.0;

            while (!ct.IsCancellationRequested)
            {
                double now = (DateTime.UtcNow - ProcessEpoch.StartUtc).TotalSeconds;

                if (now >= RELEASE_AT_SECONDS)
                {
                    int rel = Volatile.Read(ref released);

                    if (rel >= ticket - 1) break;

                    // Head-of-line stall protection: a request that never completes would hold
                    // its ticket forever and starve everything behind it. Only the queue making
                    // NO progress counts as a stall — a moving line resets the clock, so a
                    // ticket far back in a healthy queue never skips ahead. A hung request
                    // hangs identically every run, so the skip itself stays deterministic.
                    if (rel != lastSeenReleased)
                    {
                        lastSeenReleased = rel;
                        stalledSince = now;
                    }
                    else if (now - stalledSince > 60.0)
                    {
                        Interlocked.CompareExchange(ref released, rel + 1, rel);
                        stalledSince = now;
                    }
                }

                await UniTask.Delay(25, DelayType.Realtime, PlayerLoopTiming.Update, ct);
            }

            // Move the released cursor to at least this ticket.
            int cur;
            while ((cur = Volatile.Read(ref released)) < ticket)
                Interlocked.CompareExchange(ref released, ticket, cur);
        }
    }
}

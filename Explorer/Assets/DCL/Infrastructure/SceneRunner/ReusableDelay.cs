#if !UNITY_WEBGL
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace SceneRunner
{
    /// <summary>
    ///     Reusable, allocation-amortized replacement for the per-tick
    ///     <see cref="System.Threading.Tasks.Task" />.Delay(int, CancellationToken) awaited by
    ///     <see cref="SceneFacade" />'s update loop. One <see cref="Timer" /> and one
    ///     <see cref="CancellationTokenRegistration" /> are created for the whole loop lifetime, so a
    ///     steady-state <see cref="Delay" /> await allocates nothing (the standard Task.Delay path allocates a
    ///     promise, a timer, and a per-call CTR each tick).
    ///     NOT thread-safe for concurrent Delay calls — the update loop awaits one Delay at a time.
    /// </summary>
    internal sealed class ReusableDelay : IValueTaskSource, IDisposable
    {
        private const int STATE_IDLE = 0;
        private const int STATE_ARMED = 1;

        private ManualResetValueTaskSourceCore<bool> core = new () { RunContinuationsAsynchronously = false };

        private readonly Timer timer;

        private int state = STATE_IDLE;
        private volatile bool cancelled;
        private CancellationTokenRegistration ctr;

        public ReusableDelay()
        {
            timer = new Timer(static s => ((ReusableDelay)s!).OnTimer(), this, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        ///     Registers one cancellation callback for the whole loop lifetime, amortizing the per-tick CTR that
        ///     Task.Delay(int, ct) would otherwise allocate.
        /// </summary>
        public void AttachCancellation(CancellationToken ct)
        {
            ctr = ct.Register(static s => ((ReusableDelay)s!).OnCancel(), this);
        }

        public ValueTask Delay(int ms)
        {
            if (cancelled)
                throw new OperationCanceledException();

            if (ms <= 0)
                return default;

            core.Reset();
            Interlocked.Exchange(ref state, STATE_ARMED);

            // A cancellation arriving between the guard above and this arm would have found state IDLE
            // and no-op'd; re-check so that tick's cancellation is delivered rather than silently lost.
            if (cancelled && Interlocked.CompareExchange(ref state, STATE_IDLE, STATE_ARMED) == STATE_ARMED)
            {
                core.SetException(new OperationCanceledException());
                return new ValueTask(this, core.Version);
            }

            timer.Change(ms, Timeout.Infinite);
            return new ValueTask(this, core.Version);
        }

        private void OnTimer()
        {
            if (Interlocked.CompareExchange(ref state, STATE_IDLE, STATE_ARMED) == STATE_ARMED)
                core.SetResult(true);
        }

        private void OnCancel()
        {
            cancelled = true;

            if (Interlocked.CompareExchange(ref state, STATE_IDLE, STATE_ARMED) == STATE_ARMED)
                core.SetException(new OperationCanceledException());
        }

        public void Dispose()
        {
            ctr.Dispose();
            timer.Dispose();
        }

        void IValueTaskSource.GetResult(short token) => core.GetResult(token);

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => core.GetStatus(token);

        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            core.OnCompleted(continuation, state, token, flags);
    }
}
#endif

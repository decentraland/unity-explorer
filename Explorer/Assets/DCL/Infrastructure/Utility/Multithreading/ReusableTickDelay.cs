// TRUST_WEBGL_THREAD_SAFETY_FLAG

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Utility.Multithreading
{
#if UNITY_WEBGL
    /// <summary>
    ///     WebGL is single-threaded on the Unity player loop, so there is no off-main thread to resume on
    ///     and no cross-thread hazard to guard. UniTask.Delay is already pooled and allocation-free and
    ///     drives off the player loop, so this platform variant is a thin wrapper that preserves the
    ///     existing WebGL delay behavior exactly (no Timer, no custom source).
    /// </summary>
    public sealed class ReusableTickDelay : IDisposable
    {
        private CancellationToken cancellationToken;

        public void AttachCancellation(CancellationToken ct) =>
            cancellationToken = ct;

        public UniTask Delay(int ms) =>
            UniTask.Delay(ms, cancellationToken: cancellationToken);

        public void Dispose() { }
    }
#else
    /// <summary>
    ///     Reusable, allocation-amortized per-tick delay for a serially-awaited update loop (see
    ///     <c>SceneFacade</c>'s update loop). One <see cref="Timer" /> and one
    ///     <see cref="CancellationTokenRegistration" /> are created for the whole loop lifetime, so a
    ///     steady-state <see cref="Delay" /> await allocates nothing, whereas UniTask.Delay / Task.Delay
    ///     allocate a promise and a per-call cancellation registration each tick.
    ///     <para>
    ///     Invariants (the reason this is a hardened relocation, not a verbatim move):
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             Single consumer, serialized: the loop fully awaits one <see cref="Delay" /> before
    ///             arming the next, so the reused source is <see cref="UniTaskCompletionSourceCore{T}.Reset" />
    ///             (version-token bumped) between ticks and is never awaited concurrently. The bumped
    ///             version turns any stale awaiter into a loud throw rather than a silent wrong-tick
    ///             completion.
    ///         </item>
    ///         <item>
    ///             Off-main completion: the <see cref="Timer" /> callback runs on a threadpool thread and
    ///             completes the source synchronously there, so the awaiter resumes off the main thread —
    ///             matching the loop's <c>AssertMainThread(isMainThread:false)</c> after the delay. This is
    ///             why UniTask.Delay cannot be used on this path: it resumes on the player loop (main
    ///             thread).
    ///         </item>
    ///         <item>
    ///             The <see cref="Timer" /> is the single banned threading primitive; it is legitimate only
    ///             inside this WebGL-excluded infra file and is covered by the whole-file
    ///             TRUST_WEBGL_THREAD_SAFETY_FLAG above. This variant is compiled out of a WebGL build.
    ///         </item>
    ///     </list>
    /// </summary>
    public sealed class ReusableTickDelay : IUniTaskSource, IDisposable
    {
        private const int STATE_IDLE = 0;
        private const int STATE_ARMED = 1;

        private UniTaskCompletionSourceCore<AsyncUnit> core;

        private readonly Timer timer;

        private int state = STATE_IDLE;
        private volatile bool cancelled;
        private CancellationTokenRegistration ctr;

        public ReusableTickDelay()
        {
            timer = new Timer(static s => ((ReusableTickDelay)s!).OnTimer(), this, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        ///     Registers one cancellation callback for the whole loop lifetime, amortizing the per-tick
        ///     registration that UniTask.Delay(ms, ct) would otherwise allocate.
        /// </summary>
        public void AttachCancellation(CancellationToken ct)
        {
            ctr = ct.Register(static s => ((ReusableTickDelay)s!).OnCancel(), this);
        }

        public UniTask Delay(int ms)
        {
            if (cancelled)
                throw new OperationCanceledException();

            // Nothing to wait for: complete synchronously on the (off-main) caller thread, keeping the
            // loop off the main thread without arming the timer or touching the reused source.
            if (ms <= 0)
                return UniTask.CompletedTask;

            core.Reset();
            DCLInterlocked.Exchange(ref state, STATE_ARMED);

            // A cancellation arriving between the guard above and this arm would have found state IDLE and
            // no-op'd; re-check under the ARMED state so that tick's cancellation is delivered rather than
            // silently lost against the freshly reset source.
            if (cancelled && DCLInterlocked.CompareExchange(ref state, STATE_IDLE, STATE_ARMED) == STATE_ARMED)
            {
                core.TrySetException(new OperationCanceledException());
                return new UniTask(this, core.Version);
            }

            timer.Change(ms, Timeout.Infinite);
            return new UniTask(this, core.Version);
        }

        private void OnTimer()
        {
            if (DCLInterlocked.CompareExchange(ref state, STATE_IDLE, STATE_ARMED) == STATE_ARMED)
                core.TrySetResult(AsyncUnit.Default);
        }

        private void OnCancel()
        {
            cancelled = true;

            if (DCLInterlocked.CompareExchange(ref state, STATE_IDLE, STATE_ARMED) == STATE_ARMED)
                core.TrySetException(new OperationCanceledException());
        }

        public void Dispose()
        {
            ctr.Dispose();
            timer.Dispose();
        }

        UniTaskStatus IUniTaskSource.GetStatus(short token) =>
            core.GetStatus(token);

        void IUniTaskSource.OnCompleted(Action<object> continuation, object continuationState, short token) =>
            core.OnCompleted(continuation, continuationState, token);

        void IUniTaskSource.GetResult(short token) =>
            core.GetResult(token);

        UniTaskStatus IUniTaskSource.UnsafeGetStatus() =>
            core.UnsafeGetStatus();
    }
#endif
}

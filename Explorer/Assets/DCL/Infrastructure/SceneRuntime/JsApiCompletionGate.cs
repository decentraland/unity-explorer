using System;
using System.Threading;

namespace SceneRuntime
{
    /// <summary>
    ///     Coordinates safe completion of disconnected JS promises against scene disposal.
    ///     Disposal must wait for any in-flight completion to finish before releasing the V8
    ///     engine; otherwise ClearScript's <c>CompletePromise</c> continuation can call into a
    ///     released V8 isolate and throw <c>"The V8 object has been released"</c>.
    /// </summary>
    public sealed class JsApiCompletionGate : IDisposable
    {
        private readonly CancellationTokenSource cts;

        private readonly CancellationTokenSource finalizationGraceCts;

        private int pending;

        /// <summary>
        ///     Grace period applied AFTER cancellation has been signalled. Protects scene disposal
        ///     from underlying tasks that ignore disposeCts.Token: the disposer waits for the
        ///     completion gate to drain, so a stuck task would otherwise block disposal indefinitely.
        /// </summary>
        public CancellationToken FinalizationGraceCt => finalizationGraceCts.Token;

        public bool HasPending => Volatile.Read(ref pending) > 0;

        public JsApiCompletionGate(CancellationTokenSource cts, TimeSpan finalizationGraceTimeout)
        {
            this.cts = cts;
            finalizationGraceCts = new CancellationTokenSource();

            cts.Token.Register(() => finalizationGraceCts.CancelAfter(finalizationGraceTimeout));
        }

        /// <summary>
        ///     Reserves a slot for completing a JS promise. Returns false when the scene is
        ///     being disposed, in which case the caller MUST NOT touch the V8 engine.
        /// </summary>
        public bool TryEnter()
        {
            Interlocked.Increment(ref pending);

            if (cts.IsCancellationRequested)
            {
                Interlocked.Decrement(ref pending);
                return false;
            }

            return true;
        }

        public void Exit() =>
            Interlocked.Decrement(ref pending);

        public void Dispose()
        {
            cts.Dispose();
            finalizationGraceCts.Dispose();
        }
    }
}

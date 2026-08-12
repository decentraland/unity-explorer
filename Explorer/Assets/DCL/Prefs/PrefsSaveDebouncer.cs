using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Utility;

namespace DCL.Prefs
{
    /// <summary>
    ///     Coalesces a burst of save requests (e.g. one per slider onValueChanged tick during a drag) into a single
    ///     deferred flush. The in-memory pref write stays immediate and cheap at each call site; only the disk flush
    ///     (DCLPlayerPrefs.Save) is debounced, turning O(drag-ticks) full-blob serializations into O(1) per interaction.
    ///     Every request cancels and restarts a debounce window; the coalesced save fires once the window elapses
    ///     without a newer request superseding it, or immediately via <see cref="FlushIfPending" />.
    /// </summary>
    public sealed class PrefsSaveDebouncer : IDisposable
    {
        public static readonly PrefsSaveDebouncer Shared = new(DCLPlayerPrefs.Save);

        private readonly Action saveAction;
        private readonly int debounceMs;

        private CancellationTokenSource cts = null!;
        private bool pending;
        private bool disposed;

        public PrefsSaveDebouncer(Action saveAction, int debounceMs = 400)
        {
            this.saveAction = saveAction;
            this.debounceMs = debounceMs;
        }

        /// <summary>Marks a save as pending and (re)starts the debounce window: cancel-and-restart on every tick.</summary>
        public void RequestSave()
        {
            if (disposed)
                return;

            pending = true;
            cts = cts.SafeRestart();
            WaitAndFlushAsync(cts.Token).Forget();
        }

        /// <summary>Stops the debounce window and, if a save is still pending, flushes it synchronously right now.</summary>
        public void FlushIfPending()
        {
            cts.SafeCancelAndDispose();

            if (!pending)
                return;

            pending = false;
            saveAction();
        }

        private async UniTaskVoid WaitAndFlushAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(debounceMs, DelayType.Realtime, cancellationToken: token);

                if (!pending)
                    return;

                pending = false;
                saveAction();
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            disposed = true;
            pending = false;
            cts.SafeCancelAndDispose();
        }
    }
}

using System;
using System.Threading;

namespace DCL.Prefs
{
    /// <summary>
    ///     Coalesces a burst of save requests (e.g. one per slider onValueChanged tick during a drag) into a single
    ///     deferred flush. The in-memory pref write stays immediate and cheap at each call site; only the disk flush
    ///     (DCLPlayerPrefs.Save) is debounced, turning O(drag-ticks) full-blob serializations into O(1) per interaction.
    ///     DCLPlayerPrefs.Save is safe to invoke off the main thread (FileDCLPlayerPrefs only flips a flag and Task.Run's
    ///     the disk write), so a System.Threading.Timer is enough and no UniTask dependency is required.
    /// </summary>
    public sealed class PrefsSaveDebouncer : IDisposable
    {
        public static readonly PrefsSaveDebouncer Shared = new(DCLPlayerPrefs.Save);

        private readonly Action saveAction;
        private readonly int debounceMs;
        private readonly Timer timer;
        private readonly object gate = new();
        private bool pending;
        private bool disposed;

        public PrefsSaveDebouncer(Action saveAction, int debounceMs = 400)
        {
            this.saveAction = saveAction;
            this.debounceMs = debounceMs;
            timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>Marks a save as pending and (re)starts the debounce window: cancel-and-restart on every tick.</summary>
        public void RequestSave()
        {
            lock (gate)
            {
                if (disposed)
                    return;

                pending = true;
                timer.Change(debounceMs, Timeout.Infinite);
            }
        }

        /// <summary>Stops the timer and, if a save is still pending, flushes it synchronously right now.</summary>
        public void FlushIfPending()
        {
            bool flush;

            lock (gate)
            {
                timer.Change(Timeout.Infinite, Timeout.Infinite);
                flush = pending;
                pending = false;
            }

            if (flush)
                saveAction();
        }

        private void OnTimer(object state)
        {
            bool flush;

            lock (gate)
            {
                flush = pending;
                pending = false;
            }

            if (flush)
                saveAction();
        }

        public void Dispose()
        {
            lock (gate)
            {
                disposed = true;
                pending = false;
            }

            timer.Dispose();
        }
    }
}

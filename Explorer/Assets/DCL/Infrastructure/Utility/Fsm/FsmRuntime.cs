using DCL.Diagnostics;
using System;
using Utility.Multithreading;

namespace Utility.Fsm
{
    /// <summary>
    ///     Elm-style runtime: messages from any thread are queued by <see cref="Send"/> and applied one at a time, in order and logged,
    ///     by the thread that calls <see cref="Drain"/>. Messages sent while a command executes are queued, never applied nested.
    /// </summary>
    public class FsmRuntime<TModel, TMsg, TCmd> : IMsgInbox<TMsg>, IDisposable
    {
        /// <summary>Pure transition: the same model and message always yield the same result.</summary>
        public delegate (TModel model, TCmd cmd) UpdateFn(in TModel model, in TMsg msg);

        private readonly DCLConcurrentQueue<TMsg> inbox = new ();
        private readonly UpdateFn update;
        private readonly ICmdExecutor<TCmd, TMsg> executor;
        private readonly string tag;
        private readonly string category;
        private readonly Mutex<TModel> model; // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG

        private bool isDraining;

        /// <summary>Snapshot of the current model, readable from any thread; read-only even when <typeparamref name="TModel"/> is a reference type.</summary>
        public TModel ModelSnapshot
        {
            get
            {
                using Mutex<TModel>.Guard guard = model.Lock(); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
                return guard.Value;
            }
        }

        /// <summary>Every message, model and failure is logged under <paramref name="category"/>, a <see cref="ReportCategory"/>.</summary>
        public FsmRuntime(string tag, string category, TModel initialModel, UpdateFn update, ICmdExecutor<TCmd, TMsg> executor)
        {
            this.tag = tag;
            this.category = category;
            this.update = update;
            this.executor = executor;
            model = new Mutex<TModel>(initialModel); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
        }

        public void Dispose() =>
            executor.Dispose();

        public void Send(in TMsg msg) =>
            inbox.Enqueue(msg);

        /// <summary>Applies every queued message in arrival order, including those enqueued meanwhile. Single-threaded; a nested call returns at once.</summary>
        public void Drain()
        {
            if (isDraining)
                return;

            isDraining = true;

            try
            {
                while (inbox.TryDequeue(out TMsg msg))
                    Step(msg);
            }
            finally { isDraining = false; }
        }

        private void Step(in TMsg msg)
        {
            try
            {
                ReportHub.Log(category, $"[{tag}] msg: {msg}");

                (TModel next, TCmd cmd) = update(ModelSnapshot, msg);

                using (Mutex<TModel>.Guard guard = model.Lock()) // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
                    guard.Value = next;

                ReportHub.Log(category, $"[{tag}] model: {next} cmd: {cmd}");

                executor.Execute(cmd, this);
            }
            catch (Exception e) { ReportHub.LogException(e, category); }
        }
    }
}

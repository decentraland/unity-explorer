using DCL.Diagnostics;
using System;
using Utility.Multithreading;

namespace Utility.Fsm
{
    /// <summary>
    ///     Elm-style runtime. Owns the model, accepts messages from any thread through <see cref="Send"/>, and applies them
    ///     one at a time on the single thread that calls <see cref="Drain"/>. Every message is logged before it is applied.
    ///     The pure <see cref="UpdateFn"/> produces the next model and one command; the executor performs the command.
    ///     Messages the executor sends while executing are queued and applied later in the same drain, never nested.
    /// </summary>
    public class FsmRuntime<TModel, TMsg, TCmd> : IMsgInbox<TMsg>
    {
        /// <summary>
        ///     Pure transition. No IO, no time, no shared state: the same model and message always yield the same result.
        /// </summary>
        public delegate (TModel model, TCmd cmd) UpdateFn(in TModel model, in TMsg msg);

        private readonly DCLConcurrentQueue<TMsg> inbox = new ();
        private readonly UpdateFn update;
        private readonly ICmdExecutor<TCmd, TMsg> executor;
        private readonly string tag;

        private bool isDraining;

        /// <summary>
        ///     Current model. Written only by <see cref="Drain"/>, so read it on the draining thread.
        /// </summary>
        public TModel Model { get; private set; }

        public FsmRuntime(string tag, TModel initialModel, UpdateFn update, ICmdExecutor<TCmd, TMsg> executor)
        {
            this.tag = tag;
            this.update = update;
            this.executor = executor;
            Model = initialModel;
        }

        public void Send(in TMsg msg) =>
            inbox.Enqueue(msg);

        /// <summary>
        ///     Applies every queued message in arrival order, including the ones enqueued while draining.
        ///     Call it from one thread only. A call made while a drain is already running returns immediately.
        /// </summary>
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
            ReportHub.LogProductionInfo($"[{tag}] msg: {msg}");

            try
            {
                (TModel next, TCmd cmd) = update(Model, msg);
                Model = next;

                ReportHub.LogProductionInfo($"[{tag}] model: {next} cmd: {cmd}");

                executor.Execute(cmd, this);
            }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.ALWAYS); }
        }
    }
}

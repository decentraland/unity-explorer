using System;

namespace Utility.Fsm
{
    /// <summary>
    ///     Performs the side effects of the commands an FSM update emits. This is the only place with IO in an FSM.
    ///     The runtime that receives it owns it and disposes it, which ends the work the commands started.
    /// </summary>
    public interface ICmdExecutor<TCmd, TMsg> : IDisposable
    {
        /// <summary>
        ///     Runs one command on the draining thread. Facts it produces, now or when a spawned task completes,
        ///     go to <paramref name="inbox"/>; the executor never touches the model.
        /// </summary>
        void Execute(in TCmd cmd, IMsgInbox<TMsg> inbox);
    }
}

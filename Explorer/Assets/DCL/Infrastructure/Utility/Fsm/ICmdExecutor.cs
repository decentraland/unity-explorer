using System;

namespace Utility.Fsm
{
    /// <summary>Performs the side effects of the commands an FSM update emits, the only place with IO. Disposing it ends the work the commands started.</summary>
    public interface ICmdExecutor<TCmd, TMsg> : IDisposable
    {
        /// <summary>Runs one command on the draining thread; its outcomes, now or later, go to <paramref name="inbox"/>, never to the model.</summary>
        void Execute(in TCmd cmd, IMsgInbox<TMsg> inbox);
    }
}

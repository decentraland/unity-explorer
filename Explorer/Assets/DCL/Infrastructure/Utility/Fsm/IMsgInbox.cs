namespace Utility.Fsm
{
    /// <summary>Write side of an FSM inbox.</summary>
    public interface IMsgInbox<TMsg>
    {
        /// <summary>Thread-safe and non-blocking; the message is applied on a later <c>Drain</c>, never inside this call.</summary>
        void Send(in TMsg msg);
    }
}

namespace Utility.Fsm
{
    /// <summary>
    ///     Write side of an FSM inbox. The only way anything outside the runtime feeds a fact into the model.
    /// </summary>
    public interface IMsgInbox<TMsg>
    {
        /// <summary>
        ///     Thread-safe and non-blocking. The message is applied on a later <c>Drain</c>, never inside this call.
        /// </summary>
        void Send(in TMsg msg);
    }
}

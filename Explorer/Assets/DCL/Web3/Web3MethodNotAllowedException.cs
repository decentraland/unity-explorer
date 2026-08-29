namespace DCL.Web3
{
    /// <summary>
    ///     Raised when an SDK scene requests a Web3 operation that the explorer deterministically rejects by its
    ///     allow-list / permission rules (a non-whitelisted RPC method, or the Web3 API being disabled for the scene).
    ///     This is driven purely by scene input, not by an engine fault, so the scene runtime returns a proper
    ///     JSON-RPC error to the scene instead of routing it through the engine-exception handler (which would report
    ///     it to Sentry and could suspend the scene on repeated calls).
    /// </summary>
    public class Web3MethodNotAllowedException : Web3Exception
    {
        public Web3MethodNotAllowedException(string message)
            : base(message) { }
    }
}

namespace DCL.Web3
{
    /// <summary>
    ///     Raised when a scene's Web3 request is rejected deterministically by the allow-list / permission rules,
    ///     as opposed to a genuine engine or provider fault.
    /// </summary>
    public class Web3MethodNotAllowedException : Web3Exception
    {
        public Web3MethodNotAllowedException(string message)
            : base(message) { }
    }
}

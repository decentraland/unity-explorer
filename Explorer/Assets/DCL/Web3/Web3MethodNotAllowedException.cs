namespace DCL.Web3
{
    /// <summary>Separates allow-list rejections from genuine engine or provider faults, which must be reported differently.</summary>
    public class Web3MethodNotAllowedException : Web3Exception
    {
        public Web3MethodNotAllowedException(string message)
            : base(message) { }
    }
}

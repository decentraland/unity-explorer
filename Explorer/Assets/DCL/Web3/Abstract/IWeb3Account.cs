namespace DCL.Web3.Abstract
{
    public interface IWeb3Account
    {
        public Web3Address Address { get; }

        string Sign(string message);

        /// <summary>
        /// Check the message has been signed by this identity's address
        /// </summary>
        /// <param name="message">The original non-signed message</param>
        /// <param name="signature">The signed message generated from original message</param>
        /// <returns></returns>
        bool Verify(string message, string signature);
    }
}

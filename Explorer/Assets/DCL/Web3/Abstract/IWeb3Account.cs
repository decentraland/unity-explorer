namespace DCL.Web3.Abstract
{
    public interface IWeb3Account
    {
        public Web3Address Address { get; }

        public string PrivateKey { get; }

        string Sign(string message);

        /// <summary>
        /// Check the message has been signed by this identity's address
        /// </summary>
        /// <param name="message">The original non-signed message</param>
        /// <param name="signature">The signed message generated from original message</param>
        /// <returns></returns>
        bool Verify(string message, string signature);
        /// <summary>
        /// Check the message has been signed by this identity's address
        /// </summary>
        /// <param name="message">The original non-signed message</param>
        /// <param name="signature">The signed message generated from original message</param>
        /// <param name="address">The expected address from the validation</param>
        /// <returns></returns>
        bool Verify(string message, string signature, Web3Address address);
    }
}

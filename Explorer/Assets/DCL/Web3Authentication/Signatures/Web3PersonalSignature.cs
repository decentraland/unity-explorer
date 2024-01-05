namespace DCL.Web3Authentication.Signatures
{
    public struct Web3PersonalSignature
    {
        public string Result { get; }
        public Web3Address Signer { get; }

        public Web3PersonalSignature(string result, Web3Address signer)
        {
            Result = result;
            Signer = signer;
        }
    }
}

namespace DCL.Web3Authentication
{
    public class FakeWeb3Account : IWeb3Account
    {
        public Web3Address Address { get; }

        public FakeWeb3Account(Web3Address publicAddress)
        {
            Address = publicAddress;
        }

        public string Sign(string message) =>
            $"fakeSign:{message}";

        public bool Verify(string message, string signature) =>
            signature.StartsWith("fakeSign:") && signature.EndsWith(message);
    }
}

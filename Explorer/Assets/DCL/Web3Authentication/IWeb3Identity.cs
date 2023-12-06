namespace DCL.Web3Authentication
{
    public interface IWeb3Identity
    {
        public string Address { get; }

        string Sign(string message);

        bool Verify(string message, string signature);
    }
}

using Nethereum.Signer;

namespace DCL.Web3.Accounts
{
    public class NethereumAccount : IWeb3Account, IEthKeyOwner
    {
        private static readonly EthereumMessageSigner signer = new ();
        internal readonly EthECKey key;

        public Web3Address Address { get; }

        public EthECKey Key => key;

        public NethereumAccount(EthECKey key)
        {
            this.key = key;
            Address = new Web3Address(key.GetPublicAddress());
        }

        public static NethereumAccount CreateRandom() =>
            new (EthECKey.GenerateKey());

        public string Sign(string message) =>
            signer.EncodeUTF8AndSign(message, key);

        public bool Verify(string message, string signature) =>
            signer.EncodeUTF8AndEcRecover(message, signature) == Address;
    }
}

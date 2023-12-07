using Nethereum.Signer;

namespace DCL.Web3Authentication
{
    public class NethereumAccount : IWeb3Account
    {
        private static readonly EthereumMessageSigner signer = new ();
        private readonly EthECKey key;

        public string Address => key.GetPublicAddress();

        public NethereumAccount(EthECKey key)
        {
            this.key = key;
        }

        public static NethereumAccount CreateRandom() =>
            new (EthECKey.GenerateKey());

        public string Sign(string message) =>
            signer.EncodeUTF8AndSign(message, key);

        public bool Verify(string message, string signature) =>
            signer.EncodeUTF8AndEcRecover(message, signature) == Address;
    }
}

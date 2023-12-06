using Nethereum.Signer;

namespace DCL.Web3Authentication
{
    public class Identity
    {
        private readonly EthECKey key;
        private static readonly EthereumMessageSigner Signer = new ();

        public Identity(EthECKey key)
        {
            this.key = key;
        }

        public static Identity CreateRandom() =>
            new (EthECKey.GenerateKey());

        public string Sign(string message) =>
            Signer.EncodeUTF8AndSign(message, key);

        public bool Verify(string message, string signature)
        {
            string address = Signer.EncodeUTF8AndEcRecover(message, signature);
            return address == Address();
        }

        public string Address() =>
            key.GetPublicAddress();
    }
}

using Nethereum.Signer;

namespace DCLCrypto
{
    public class Identity
    {
        private readonly EthECKey key;
        private readonly EthereumMessageSigner signer = new ();

        public Identity(EthECKey key)
        {
            this.key = key;
        }

        public static Identity CreateRandom() =>
            new (EthECKey.GenerateKey());

        public string Sign(string message) =>
            signer.EncodeUTF8AndSign(message, key);

        public bool Verify(string message, string signature)
        {
            string address = signer.EncodeUTF8AndEcRecover(message, signature);
            return address == Address();
        }

        public string Address() =>
            key.GetPublicAddress();
    }
}

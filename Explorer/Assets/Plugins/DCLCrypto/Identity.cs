using System.Text;
using Nethereum.Signer;

namespace DCLCrypto
{
    public class Identity
    {
        private readonly EthECKey key;
        private readonly EthereumMessageSigner signer = new EthereumMessageSigner();

        public static Identity CreateRandom()
        {
            return new Identity(EthECKey.GenerateKey());
        }

        public Identity(EthECKey key)
        {
            this.key = key;
        }

        public string Sign(string message)
        {
            return signer.EncodeUTF8AndSign(message, key);
        }

        public bool Verify(string message, string signature)
        {
            var address = signer.EncodeUTF8AndEcRecover(message, signature);
            return address == Address();
        }

        public string Address()
        {
            return key.GetPublicAddress();
        }
    }
}
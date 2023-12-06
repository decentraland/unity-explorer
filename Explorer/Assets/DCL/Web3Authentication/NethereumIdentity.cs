using Nethereum.Signer;

namespace DCL.Web3Authentication
{
    public class NethereumIdentity : IWeb3Identity
    {
        private static readonly EthereumMessageSigner signer = new ();
        private readonly EthECKey key;

        public string Address => key.GetPublicAddress();

        public NethereumIdentity(EthECKey key)
        {
            this.key = key;
        }

        public static NethereumIdentity CreateRandom() =>
            new (EthECKey.GenerateKey());

        public string Sign(string message) =>
            signer.EncodeUTF8AndSign(message, key);

        public bool Verify(string message, string signature) =>
            signer.EncodeUTF8AndEcRecover(message, signature) == Address;
    }
}

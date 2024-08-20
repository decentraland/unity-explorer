using DCL.Web3.Abstract;
using DCL.Web3.Accounts;
using Nethereum.Signer;
using UnityEngine;

namespace Plugins.RustEthereum.SignServerWrap.Playground
{
    public class SignServerWrapPlayground : MonoBehaviour
    {
        public enum Mode
        {
            Nethereum,
            Rust,
        }

        [SerializeField] private int callsPerSecond = 1000;
        [SerializeField] private string exampleMessage = "Hello world! Best day ever!";
        [SerializeField] private Mode mode = Mode.Nethereum;

        private RustEthereumAccount rustEthereumAccount = null!;
        private NethereumAccount nethereumAccount = null!;

        private void Start()
        {
            var key = EthECKey.GenerateKey()!;
            rustEthereumAccount = new RustEthereumAccount(key);
            nethereumAccount = NethereumAccount.CreateForVerifyOnly(key)!;
        }

        private void Update()
        {
            IWeb3Account account = mode is Mode.Nethereum ? nethereumAccount : rustEthereumAccount;
            for (var i = 0; i < callsPerSecond; i++) account.Sign(exampleMessage);
        }
    }
}

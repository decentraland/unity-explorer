using DCL.Utilities.Extensions;
using DCL.Web3;
using DCL.Web3.Abstract;
using DCL.Web3.Accounts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using System;

namespace Plugins.RustEthereum.SignServerWrap
{
    public class RustEthereumAccount : IWeb3Account, IEthKeyOwner
    {
        private readonly IWeb3Account verifierAccount;

        public Web3Address Address { get; }

        public EthECKey Key { get; }

        public RustEthereumAccount(EthECKey key)
        {
            this.verifierAccount = NethereumAccount.CreateForVerifyOnly(key);

            Key = key;
            Address = new Web3Address(key.GetPublicAddress()!);
            byte[] bytes = key.GetPrivateKeyAsBytes().EnsureNotNull();
            var bytesLen = new UIntPtr((ulong)bytes.Length);

            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    bool result = NativeMethods.SignServerInitialize(ptr, bytesLen);

                    if (result == false)
                        throw new Exception("Failed to initialize sign server");
                }
            }
        }

        public string Sign(string message)
        {
            const int SIZE_OF_SIGN = 65;

            var buffer = new byte[SIZE_OF_SIGN];

            unsafe
            {
                fixed (byte* signature = buffer) { NativeMethods.SignServerSignMessage(message, signature); }
            }

            return buffer.ToHex()!;
        }

        public bool Verify(string message, string signature) =>
            verifierAccount.Verify(message, signature);
    }
}

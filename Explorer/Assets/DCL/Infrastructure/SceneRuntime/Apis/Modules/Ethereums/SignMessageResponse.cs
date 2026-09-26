using DCL.Web3.Chains;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Text;

namespace SceneRuntime.Apis.Modules.Ethereums
{
    [Serializable]
    public struct SignMessageResponse
    {
        public string hexEncodedMessage;
        public string message;
        public string signature;

        public SignMessageResponse(AuthLink authLink) : this(
            Encoding.UTF8.GetBytes(authLink.payload).ToHex()!,
            authLink.payload,
            authLink.signature!
        ) { }

        public SignMessageResponse(string hexEncodedMessage, string message, string signature)
        {
            this.hexEncodedMessage = hexEncodedMessage;
            this.message = message;
            this.signature = signature;
        }
    }
}

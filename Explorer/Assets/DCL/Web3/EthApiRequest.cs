using Newtonsoft.Json;
using System;

namespace DCL.Web3
{
    [Serializable]
    public struct EthApiRequest
    {
        public long id;
        public string method;

        // Newtonsoft-serialized wire DTO (JsonConvert over the RPC WebSocket); Unity serialization never sees this field.
#pragma warning disable UAC1001
        public object[] @params;
#pragma warning restore UAC1001

        // This field is only used for readonly requests.
        // Use this to specify the network to use for the request that is neither `mainnet` nor `sepolia`.
        [JsonIgnore]
        public string? readonlyNetwork;
    }
}

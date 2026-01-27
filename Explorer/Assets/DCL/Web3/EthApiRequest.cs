using Newtonsoft.Json;
using System;

namespace DCL.Web3
{
    [Serializable]
    public struct EthApiRequest
    {
        public long id;
        public string method;
        public object[] @params;

        // This field is only used for readonly requests.
        // Use this to specify the network to use for the request that is neither `mainnet` nor `sepolia`.
        [JsonIgnore]
        public string? readonlyNetwork;
    }
}

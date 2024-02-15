using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace DCL.Web3.Chains
{
    [Serializable]
    public struct AuthLink
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public AuthLinkType type;
        public string payload;
        public string? signature;

        public override string ToString() =>
            $"AuthLink: {{type: {type}; payload: {payload}; signature: {signature}}}";

        public string ToJson() =>
            JsonConvert.SerializeObject(this);
    }
}

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace DCL.Web3Authentication
{
    [Serializable]
    public struct AuthLink
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public AuthLinkType type;
        public string payload;
        public string? signature;
    }
}

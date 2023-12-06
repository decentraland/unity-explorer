using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace DCL.Web3Authentication
{
    [Serializable]
    public struct AuthLink
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public AuthLinkType type;
        public string payload;
        [CanBeNull] public string signature;
    }

    public class AuthChain : List<AuthLink>
    {
        public AuthChain() { }

        public AuthChain(AuthChain otherAuthChain)
            : base(otherAuthChain)
        {
        }

        public AuthChain Clone() =>
            new (this);

        // TODO: single responsibility issue
        public string ToJsonString() =>
            JsonConvert.SerializeObject(this);
    }
}

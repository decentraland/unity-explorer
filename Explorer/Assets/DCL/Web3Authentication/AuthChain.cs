using DCL.Optimization.ThreadSafePool;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections;
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

    public class AuthChain : IEnumerable<AuthLink>, IDisposable
    {
        private static readonly ThreadSafeObjectPool<AuthChain> pool = new (() => new AuthChain());

        private readonly AuthLink[] chain;

        public static AuthChain Create() =>
            pool.Get();

        private AuthChain()
        {
            chain = new AuthLink[3];
        }

        public void Dispose()
        {
            pool.Release(this);
        }

        public AuthLink Get(AuthLinkType type) =>
            chain[(int)type];

        public void Set(AuthLinkType type, AuthLink link)
        {
            if (link.type != type)
                throw new AuthChainException(this, $"Invalid link type ${link.type}. Expected ${type}");

            chain[(int)type] = link;
        }

        public IEnumerator<AuthLink> GetEnumerator() =>
            ((IEnumerable<AuthLink>)chain).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            chain.GetEnumerator();
    }
}

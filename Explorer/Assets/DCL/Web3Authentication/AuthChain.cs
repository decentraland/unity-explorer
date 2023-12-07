using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using Utility.ThreadSafePool;

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

    public class AuthChain : IReadOnlyCollection<AuthLink>, IDisposable
    {
        private static readonly ThreadSafeObjectPool<AuthChain> pool = new (() => new AuthChain(new AuthLink[3]));

        private readonly AuthLink[] chain;

        public int Count => chain.Length;

        public static AuthChain Create() =>
            pool.Get();

        private AuthChain(AuthLink[] chain)
        {
            this.chain = chain;
        }

        public void Dispose()
        {
            pool.Release(this);
        }

        public AuthLink Get(AuthLinkType index) =>
            chain[(int)index];

        public void Set(AuthLinkType index, AuthLink link) =>
            chain[(int)index] = link;

        public IEnumerator<AuthLink> GetEnumerator() =>
            ((IEnumerable<AuthLink>)chain).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            chain.GetEnumerator();
    }
}

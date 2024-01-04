using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DCL.Web3Authentication
{
    public class AuthChain : IEnumerable<AuthLink>, IDisposable
    {
        private static readonly ThreadSafeObjectPool<AuthChain> pool = new (() => new AuthChain());

        private readonly Dictionary<AuthLinkType, AuthLink> chain = new ();

        public static AuthChain Create() =>
            pool.Get();

        private AuthChain() { }

        public void Dispose()
        {
            chain.Clear();
            pool.Release(this);
        }

        public bool TryGet(AuthLinkType type, out AuthLink link) =>
            chain.TryGetValue(type, out link);

        public AuthLink Get(AuthLinkType type) =>
            chain[type];

        public void Set(AuthLinkType type, AuthLink link)
        {
            if (link.type != type)
                throw new AuthChainException(this, $"Invalid link type ${link.type}. Expected ${type}");

            chain[type] = link;
        }

        public IEnumerator<AuthLink> GetEnumerator() =>
            ((IEnumerable<AuthLink>)chain.Values).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            chain.GetEnumerator();
    }
}

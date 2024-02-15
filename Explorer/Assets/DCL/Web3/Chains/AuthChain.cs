using DCL.Optimization.ThreadSafePool;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DCL.Web3.Chains
{
    public class AuthChain : IEnumerable<AuthLink>, IDisposable
    {
        private static readonly ThreadSafeObjectPool<AuthChain> POOL = new (() => new AuthChain());

        private readonly Dictionary<AuthLinkType, AuthLink> chain = new ();

        public static AuthChain Create() =>
            POOL.Get()!;

        private AuthChain() { }

        public void Dispose()
        {
            chain.Clear();
            POOL.Release(this);
        }

        public bool TryGet(AuthLinkType type, out AuthLink link) =>
            chain.TryGetValue(type, out link);

        public AuthLink Get(AuthLinkType type) =>
            chain[type];

        //TODO I see some flow of design here: type and link.type can be assigned to different values, but I don't think it's expected behavior
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

        public override string ToString() =>
            ToJson();

        public string ToJson() =>
            new Serialized(this).ToJson();

        [Serializable]
        private struct Serialized
        {
            private List<AuthLink> chain;

            public Serialized(AuthChain authChain)
            {
                chain = new List<AuthLink>(authChain.chain.Values);
                chain.Sort(Comparer.INSTANCE);
            }

            public string ToJson() =>
                JsonConvert.SerializeObject(chain);

            private class Comparer : IComparer<AuthLink>
            {
                public static readonly Comparer INSTANCE = new ();

                private static readonly List<AuthLinkType> ORDER = new ()
                {
                    AuthLinkType.SIGNER,
                    AuthLinkType.ECDSA_EPHEMERAL,
                    AuthLinkType.ECDSA_SIGNED_ENTITY,
                    AuthLinkType.ECDSA_EIP_1654_EPHEMERAL,
                    AuthLinkType.ECDSA_EIP_1654_SIGNED_ENTITY,
                };

                public int Compare(AuthLink x, AuthLink y) =>
                    ORDER.IndexOf(x.type).CompareTo(ORDER.IndexOf(y.type));
            }
        }
    }
}

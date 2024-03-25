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

        private bool disposed;

        public static AuthChain Create() =>
            POOL.Get()!;

        private AuthChain() { }

        ~AuthChain()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            chain.Clear();
            POOL.Release(this);
            disposed = true;
        }

        public bool TryGet(AuthLinkType type, out AuthLink link) =>
            chain.TryGetValue(type, out link);

        public AuthLink Get(AuthLinkType type) =>
            chain[type];

        public void Set(AuthLink link) =>
            chain[link.type] = link;

        public void SetSigner(string signerAddress)
        {
            Set(new AuthLink
            {
                type = AuthLinkType.SIGNER,
                payload = signerAddress,
                signature = "",
            });
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

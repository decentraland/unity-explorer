using DCL.Prefs;
using DCL.Web3.Chains;
using System;

namespace DCL.Web3.Identities
{
    public partial class PlayerPrefsIdentityProvider : IWeb3IdentityCache
    {
        private readonly IWeb3IdentityJsonSerializer identitySerializer;
        private readonly EthereumNetwork ethereumNetwork;

        public event Action? OnIdentityCleared;
        public event Action? OnIdentityChanged;

        /// <summary>
        ///     The stored identity is chain-scoped, so it gets a slot per network: an identity signed for one chain
        ///     must not overwrite the identity signed for the other. Two networks, two slots - the sepolia slot
        ///     keeps its legacy "zone" key.
        /// </summary>
        private string GetIdentityKey() =>
            ethereumNetwork switch
            {
                EthereumNetwork.Mainnet => DCLPrefKeys.WEB3_IDENTITY,
                EthereumNetwork.Sepolia => DCLPrefKeys.WEB3_IDENTITY_ZONE,
                _ => throw new ArgumentOutOfRangeException(nameof(ethereumNetwork), ethereumNetwork, null),
            };

        public IWeb3Identity? Identity
        {
            get
            {
                string key = GetIdentityKey();
                if (!DCLPlayerPrefs.HasKey(key)) return null;
                string json = DCLPlayerPrefs.GetString(key, string.Empty)!;
                if (string.IsNullOrEmpty(json)) return null;
                return identitySerializer.Deserialize(json);
            }

            set
            {
                if (value == null)
                    Clear();
                else
                {
                    DCLPlayerPrefs.SetString(GetIdentityKey(), identitySerializer.Serialize(value), save: true);
                    OnIdentityChanged?.Invoke();
                }
            }
        }

        public PlayerPrefsIdentityProvider(IWeb3IdentityJsonSerializer identitySerializer, EthereumNetwork ethereumNetwork)
        {
            this.identitySerializer = identitySerializer;
            this.ethereumNetwork = ethereumNetwork;
        }

        public void Dispose()
        {

        }

        public void Clear()
        {
            DCLPlayerPrefs.DeleteKey(GetIdentityKey(), save: true);
            OnIdentityCleared?.Invoke();
        }
    }
}

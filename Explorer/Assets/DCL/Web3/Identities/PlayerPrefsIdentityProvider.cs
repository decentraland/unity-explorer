using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Prefs;
using System;

namespace DCL.Web3.Identities
{
    public partial class PlayerPrefsIdentityProvider : IWeb3IdentityCache
    {
        private readonly IWeb3IdentityJsonSerializer identitySerializer;
        private readonly DecentralandEnvironment dclEnv;

        public event Action? OnIdentityCleared;
        public event Action? OnIdentityChanged;

        /// <summary>
        ///     The stored identity is chain-scoped, so the key follows <c>ChainUtils</c>: the mainnet environments
        ///     share one slot and the sepolia ones the other. A <c>--base-domain</c> deployment signs against
        ///     sepolia, so it must not overwrite the mainnet identity.
        /// </summary>
        private string GetIdentityKey() =>
            dclEnv switch
            {
                DecentralandEnvironment.Org => DCLPrefKeys.WEB3_IDENTITY,
                DecentralandEnvironment.Today => DCLPrefKeys.WEB3_IDENTITY,
                DecentralandEnvironment.Zone => DCLPrefKeys.WEB3_IDENTITY_ZONE,
                DecentralandEnvironment.Custom => DCLPrefKeys.WEB3_IDENTITY_ZONE,
                _ => throw new ArgumentOutOfRangeException(nameof(dclEnv), dclEnv, null),
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

        public PlayerPrefsIdentityProvider(IWeb3IdentityJsonSerializer identitySerializer, DecentralandEnvironment dclEnv)
        {
            this.identitySerializer = identitySerializer;
            this.dclEnv = dclEnv;
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

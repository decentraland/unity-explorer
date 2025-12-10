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

        private string GetIdentityKey()
        {
            return dclEnv == DecentralandEnvironment.Zone
                ? DCLPrefKeys.WEB3_IDENTITY_ZONE
                : DCLPrefKeys.WEB3_IDENTITY;
        }

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
                    DCLPlayerPrefs.SetString(GetIdentityKey(), identitySerializer.Serialize(value));
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
            DCLPlayerPrefs.DeleteKey(GetIdentityKey());
            OnIdentityCleared?.Invoke();
        }
    }
}

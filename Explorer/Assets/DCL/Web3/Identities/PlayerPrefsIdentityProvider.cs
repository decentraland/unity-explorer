using UnityEngine;

namespace DCL.Web3.Identities
{
    public partial class PlayerPrefsIdentityProvider : IWeb3IdentityCache
    {
        private const string DEFAULT_PREFS_KEY = "Web3Authentication.Identity";

        private readonly string playerPrefsKey;
        private readonly IWeb3IdentityJsonSerializer identitySerializer;

        public IWeb3Identity? Identity
        {
            get
            {
                if (!PlayerPrefs.HasKey(playerPrefsKey)) return null;
                string json = PlayerPrefs.GetString(playerPrefsKey, string.Empty)!;
                if (string.IsNullOrEmpty(json)) return null;
                return identitySerializer.Deserialize(json);
            }

            set
            {
                if (value == null)
                    Clear();
                else
                    PlayerPrefs.SetString(playerPrefsKey, identitySerializer.Serialize(value));
            }
        }

        public PlayerPrefsIdentityProvider(IWeb3IdentityJsonSerializer identitySerializer, string playerPrefsKey = DEFAULT_PREFS_KEY)
        {
            this.identitySerializer = identitySerializer;
            this.playerPrefsKey = playerPrefsKey;
        }

        public void Dispose() { }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(playerPrefsKey);
        }
    }
}

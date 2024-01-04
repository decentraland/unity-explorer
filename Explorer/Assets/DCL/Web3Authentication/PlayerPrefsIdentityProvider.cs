using UnityEngine;

namespace DCL.Web3Authentication
{
    public partial class PlayerPrefsIdentityProvider : IWeb3IdentityCache
    {
        private const string PREFS_KEY = "Web3Authentication.Identity";

        private readonly IWeb3IdentityJsonSerializer identitySerializer;

        public IWeb3Identity? Identity
        {
            get
            {
                if (!PlayerPrefs.HasKey(PREFS_KEY)) return null;
                string json = PlayerPrefs.GetString(PREFS_KEY, "");
                if (string.IsNullOrEmpty(json)) return null;
                return identitySerializer.Deserialize(json);
            }

            set
            {
                if (value == null)
                    Clear();
                else
                    PlayerPrefs.SetString(PREFS_KEY, identitySerializer.Serialize(value));
            }
        }

        public PlayerPrefsIdentityProvider(IWeb3IdentityJsonSerializer identitySerializer)
        {
            this.identitySerializer = identitySerializer;
        }

        public void Dispose() { }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(PREFS_KEY);
        }
    }
}

using DCL.Prefs;
using System;

namespace DCL.Web3.Identities
{
    public partial class PlayerPrefsIdentityProvider : IWeb3IdentityCache
    {
        private readonly IWeb3IdentityJsonSerializer identitySerializer;

        public event Action? OnIdentityCleared;
        public event Action? OnIdentityChanged;

        public IWeb3Identity? Identity
        {
            get
            {
                if (!DCLPlayerPrefs.HasKey(DCLPrefKeys.WEB3_IDENTITY)) return null;
                string json = DCLPlayerPrefs.GetString(DCLPrefKeys.WEB3_IDENTITY, string.Empty)!;
                if (string.IsNullOrEmpty(json)) return null;
                return identitySerializer.Deserialize(json);
            }

            set
            {
                if (value == null)
                    Clear();
                else
                {
                    DCLPlayerPrefs.SetString(DCLPrefKeys.WEB3_IDENTITY, identitySerializer.Serialize(value));
                    OnIdentityChanged?.Invoke();
                }
            }
        }

        public PlayerPrefsIdentityProvider(IWeb3IdentityJsonSerializer identitySerializer)
        {
            this.identitySerializer = identitySerializer;
        }

        public void Dispose()
        {

        }

        public void Clear()
        {
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.WEB3_IDENTITY);
            OnIdentityCleared?.Invoke();
        }
    }
}

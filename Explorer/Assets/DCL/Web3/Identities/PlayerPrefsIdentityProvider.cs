using System;
using UnityEngine;

namespace DCL.Web3.Identities
{
    public partial class PlayerPrefsIdentityProvider : IWeb3IdentityCache
    {
        private readonly IWeb3IdentityJsonSerializer identitySerializer;
        private readonly IPlayerPrefsIdentityProviderKeyStrategy keyStrategy;

        public event Action? OnIdentityCleared;
        public event Action? OnIdentityChanged;

        public IWeb3Identity? Identity
        {
            get
            {
                if (!PlayerPrefs.HasKey(keyStrategy.PlayerPrefsKey)) return null;
                string json = PlayerPrefs.GetString(keyStrategy.PlayerPrefsKey, string.Empty)!;
                if (string.IsNullOrEmpty(json)) return null;
                return identitySerializer.Deserialize(json);
            }

            set
            {
                if (value == null)
                    Clear();
                else
                {
                    PlayerPrefs.SetString(keyStrategy.PlayerPrefsKey, identitySerializer.Serialize(value));
                    OnIdentityChanged?.Invoke();
                }
            }
        }

        public PlayerPrefsIdentityProvider(IWeb3IdentityJsonSerializer identitySerializer, IPlayerPrefsIdentityProviderKeyStrategy keyStrategy)
        {
            this.identitySerializer = identitySerializer;
            this.keyStrategy = keyStrategy;
        }

        public void Dispose()
        {
            keyStrategy.Dispose();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(keyStrategy.PlayerPrefsKey);
            OnIdentityCleared?.Invoke();
        }
    }
}

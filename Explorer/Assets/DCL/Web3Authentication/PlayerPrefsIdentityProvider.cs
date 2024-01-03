using Nethereum.Signer;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DCL.Web3Authentication
{
    public class PlayerPrefsIdentityProvider : IWeb3IdentityCache
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

        public interface IWeb3IdentityJsonSerializer
        {
            IWeb3Identity? Deserialize(string json);

            string Serialize(IWeb3Identity identity);
        }

        public class DecentralandIdentityWithNethereumAccountJsonSerializer : IWeb3IdentityJsonSerializer
        {
            public IWeb3Identity? Deserialize(string json)
            {
                IdentityJsonDto jsonRoot = JsonConvert.DeserializeObject<IdentityJsonDto>(json);

                if (!jsonRoot.IsValid) return null;

                var authChain = AuthChain.Create();

                if (jsonRoot.ephemeralAuthChain != null)
                    foreach (AuthLink link in jsonRoot.ephemeralAuthChain)
                        authChain.Set(link.type, link);

                return new DecentralandIdentity(new Web3Address(jsonRoot.address),
                    new NethereumAccount(new EthECKey(jsonRoot.key)),
                    DateTime.Parse(jsonRoot.expiration, null, DateTimeStyles.RoundtripKind),
                    authChain);
            }

            public string Serialize(IWeb3Identity identity)
            {
                var dclIdentity = (DecentralandIdentity)identity;
                var account = (NethereumAccount)identity.EphemeralAccount;

                IdentityJsonDto jsonRoot = new ()
                {
                    address = identity.Address,
                    expiration = $"{identity.Expiration:O}",
                    ephemeralAuthChain = dclIdentity.authChain.ToArray(),
                    key = account.key.GetPrivateKey(),
                };

                return JsonConvert.SerializeObject(jsonRoot);
            }
        }

        [Serializable]
        private struct IdentityJsonDto
        {
            public string address;
            public string key;
            public string expiration;
            public AuthLink[]? ephemeralAuthChain;

            public bool IsValid => !string.IsNullOrEmpty(address)
                                   && !string.IsNullOrEmpty(key)
                                   && !string.IsNullOrEmpty(expiration);
        }
    }
}

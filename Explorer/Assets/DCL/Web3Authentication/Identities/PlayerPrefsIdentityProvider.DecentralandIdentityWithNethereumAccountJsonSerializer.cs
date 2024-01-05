using DCL.Web3Authentication.Accounts;
using DCL.Web3Authentication.Chains;
using Nethereum.Signer;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace DCL.Web3Authentication.Identities
{
    public partial class PlayerPrefsIdentityProvider
    {
        public class DecentralandIdentityWithNethereumAccountJsonSerializer : IWeb3IdentityJsonSerializer
        {
            private readonly IdentityJsonDto jsonRoot = new ();

            public IWeb3Identity? Deserialize(string json)
            {
                jsonRoot.Clear();
                JsonConvert.PopulateObject(json, jsonRoot);

                if (!jsonRoot.IsValid) return null;

                var authChain = AuthChain.Create();

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

                jsonRoot.Clear();
                jsonRoot.address = identity.Address;
                jsonRoot.expiration = $"{identity.Expiration:O}";
                jsonRoot.ephemeralAuthChain.AddRange(dclIdentity.AuthChain);
                jsonRoot.key = account.key.GetPrivateKey();

                return JsonConvert.SerializeObject(jsonRoot);
            }
        }
    }
}

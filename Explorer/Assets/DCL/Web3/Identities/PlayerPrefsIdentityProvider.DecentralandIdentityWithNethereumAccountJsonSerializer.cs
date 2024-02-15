using DCL.Web3.Accounts;
using DCL.Web3.Chains;
using Nethereum.Signer;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace DCL.Web3.Identities
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
                    authChain.Set(link);

                return new DecentralandIdentity(new Web3Address(jsonRoot.address),
                    new NethereumAccount(new EthECKey(jsonRoot.key)),
                    DateTime.Parse(jsonRoot.expiration, null, DateTimeStyles.RoundtripKind),
                    authChain);
            }

            public string Serialize(IWeb3Identity identity)
            {
                var account = identity.EphemeralAccount as IEthKeyOwner
                              ?? throw new Exception("The identity account is not an IEthKeyOwner");

                jsonRoot.Clear();
                jsonRoot.address = identity.Address;
                jsonRoot.expiration = $"{identity.Expiration:O}";
                jsonRoot.ephemeralAuthChain.AddRange(identity.AuthChain);
                jsonRoot.key = account.Key.GetPrivateKey()!;

                return JsonConvert.SerializeObject(jsonRoot);
            }
        }
    }
}

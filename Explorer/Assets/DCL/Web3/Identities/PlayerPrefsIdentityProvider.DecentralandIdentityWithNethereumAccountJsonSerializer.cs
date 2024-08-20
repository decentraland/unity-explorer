using DCL.Web3.Abstract;
using DCL.Web3.Accounts.Factory;
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
            private readonly IWeb3AccountFactory accountFactory = new Web3AccountFactory();

            public IWeb3Identity? Deserialize(string json)
            {
                jsonRoot.Clear();
                JsonConvert.PopulateObject(json, jsonRoot);

                if (!jsonRoot.IsValid) return null;

                var authChain = AuthChain.Create();

                foreach (AuthLink link in jsonRoot.ephemeralAuthChain)
                    authChain.Set(link);

                return new DecentralandIdentity(new Web3Address(jsonRoot.address),
                    accountFactory.CreateAccount(new EthECKey(jsonRoot.key)),
                    DateTime.Parse(jsonRoot.expiration, null, DateTimeStyles.RoundtripKind),
                    authChain);
            }

            public string Serialize(IWeb3Identity identity)
            {
                jsonRoot.Clear();
                jsonRoot.address = identity.Address;
                jsonRoot.expiration = $"{identity.Expiration:O}";
                jsonRoot.ephemeralAuthChain.AddRange(identity.AuthChain);
                jsonRoot.key = identity.EphemeralAccount.PrivateKey;

                return JsonConvert.SerializeObject(jsonRoot);
            }
        }
    }
}

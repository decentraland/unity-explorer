using DCL.Web3.Chains;
using System;
using System.Collections.Generic;


namespace DCL.Web3.Identities
{
    public partial class PlayerPrefsIdentityProvider
    {
        // ReSharper disable InconsistentNaming
        [Serializable]
        private class IdentityJsonDto
        {
            public string address = "";
            public string key = "";
            public string expiration = "";
            public string source = "";
            public List<AuthLink> ephemeralAuthChain = new ();

            public bool IsValid => !string.IsNullOrEmpty(address)
                                   && !string.IsNullOrEmpty(key)
                                   && !string.IsNullOrEmpty(expiration);

            public void Clear()
            {
                address = "";
                key = "";
                expiration = "";
                source = "";
                ephemeralAuthChain.Clear();
            }
        }
    }
}

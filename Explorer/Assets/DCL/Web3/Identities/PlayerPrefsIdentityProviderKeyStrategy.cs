using System;

namespace DCL.Web3.Identities
{
    public interface IPlayerPrefsIdentityProviderKeyStrategy : IDisposable
    {
        const string DEFAULT_PREFS_KEY = "Web3Authentication.Identity";

        string PlayerPrefsKey { get; }

        class Const : IPlayerPrefsIdentityProviderKeyStrategy
        {
            public string PlayerPrefsKey { get; }

            public Const(string playerPrefsKey = DEFAULT_PREFS_KEY)
            {
                PlayerPrefsKey = playerPrefsKey;
            }

            public void Dispose()
            {
                //ignore
            }
        }
    }
}

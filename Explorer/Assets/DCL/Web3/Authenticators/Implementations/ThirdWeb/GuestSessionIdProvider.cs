using DCL.Prefs;
using System;

namespace DCL.Web3.Authenticators
{
    public static class GuestSessionIdProvider
    {
        public static string Resolve()
        {
            string sessionId = DCLPlayerPrefs.GetString(DCLPrefKeys.GUEST_SESSION_ID, string.Empty);

            if (!string.IsNullOrEmpty(sessionId))
                return sessionId;

            sessionId = Guid.NewGuid().ToString("N");
            DCLPlayerPrefs.SetString(DCLPrefKeys.GUEST_SESSION_ID, sessionId, save: true);

            return sessionId;
        }
    }
}

using DCL.VoiceChat.Nearby;
using DCL.Web3.Identities;
using System;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Keeps Nearby voice off while the session belongs to a guest, who is not allowed to speak or hear.
    ///     Suppression rather than <see cref="NearbyVoiceChatStateModel.Disable" /> so the user's own on/off
    ///     preference is left untouched and comes back once the account is upgraded.
    /// </summary>
    public class NearbyVoiceGuestWatcher : IDisposable
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly NearbyVoiceChatStateModel stateModel;

        private bool suppressed;

        public NearbyVoiceGuestWatcher(IWeb3IdentityCache identityCache, NearbyVoiceChatStateModel stateModel)
        {
            this.identityCache = identityCache;
            this.stateModel = stateModel;

            identityCache.OnIdentityChanged += SyncWithIdentity;

            SyncWithIdentity();
        }

        public void Dispose()
        {
            identityCache.OnIdentityChanged -= SyncWithIdentity;
        }

        private void SyncWithIdentity()
        {
            bool isGuest = identityCache.IsGuest();

            if (isGuest == suppressed) return;

            suppressed = isGuest;

            if (isGuest)
                stateModel.Suppress(SuppressionReason.Guest);
            else
                stateModel.Resume(SuppressionReason.Guest);
        }
    }
}

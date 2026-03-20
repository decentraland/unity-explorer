using DCL.Diagnostics;
using System;
using System.Collections.Generic;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Manages per-user mute state for proximity voice chat.
    /// Shared between <see cref="ProximityVoiceChatManager"/> (subscribes to <see cref="MuteStateChanged"/>)
    /// and the user profile context menu (calls <see cref="ToggleMute"/>).
    /// </summary>
    public class ProximityMuteService
    {
        private const string TAG = nameof(ProximityMuteService);

        private readonly HashSet<string> mutedWalletIds = new ();

        /// <summary>
        /// Raised when a participant's mute state changes.
        /// Parameters: walletId, isMuted.
        /// </summary>
        public event Action<string, bool>? MuteStateChanged;

        public bool IsMuted(string walletId) =>
            mutedWalletIds.Contains(walletId);

        public void ToggleMute(string walletId)
        {
            bool muted = !IsMuted(walletId);
            SetMuted(walletId, muted);
        }

        public void SetMuted(string walletId, bool muted)
        {
            if (muted)
                mutedWalletIds.Add(walletId);
            else
                mutedWalletIds.Remove(walletId);

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} {walletId} {(muted ? "muted" : "unmuted")}");
            MuteStateChanged?.Invoke(walletId, muted);
        }
    }
}

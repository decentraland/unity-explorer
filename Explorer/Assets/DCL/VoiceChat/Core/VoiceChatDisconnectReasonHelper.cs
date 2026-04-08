using DCL.Diagnostics;
using LiveKit.Proto;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Static helper for determining if a disconnect reason is valid (expected) or invalid (unexpected).
    /// Used by voice chat services to determine whether reconnection attempts should be made.
    /// Invalid reasons (returns false) trigger reconnection attempts.
    /// </summary>
    public static class VoiceChatDisconnectReasonHelper
    {
        public static bool IsValidDisconnectReason(DisconnectReason? disconnectReason)
        {
            if (!disconnectReason.HasValue)
                return false;

            return disconnectReason.Value switch
                   {
                       DisconnectReason.RoomDeleted => true,
                       DisconnectReason.RoomClosed => true,
                       DisconnectReason.ParticipantRemoved => true,
                       DisconnectReason.DuplicateIdentity => true,
                       DisconnectReason.ServerShutdown => true,
                       DisconnectReason.ClientInitiated => true,
                       DisconnectReason.JoinFailure => true,
                       DisconnectReason.UserRejected => true,
                       DisconnectReason.UserUnavailable => true,
                       DisconnectReason.SipTrunkFailure => true,
                       DisconnectReason.Migration => true,
                       DisconnectReason.SignalClose => LogAndReturn(disconnectReason.Value),
                       DisconnectReason.ConnectionTimeout => LogAndReturn(disconnectReason.Value),
                       DisconnectReason.StateMismatch => LogAndReturn(disconnectReason.Value),
                       DisconnectReason.UnknownReason => LogAndReturn(disconnectReason.Value),
                       _ => false,
                   };
        }

        private static bool LogAndReturn(DisconnectReason reason)
        {
            ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Ambiguous disconnect reason treated as valid: {reason}");
            return true;
        }
    }
}

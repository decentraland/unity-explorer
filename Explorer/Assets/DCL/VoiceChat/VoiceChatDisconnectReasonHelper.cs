using LiveKit.Proto;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Static helper for determining if a disconnect reason is valid (expected) or invalid (unexpected).
    /// Used by voice chat services to determine whether reconnection attempts should be made.
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
                       DisconnectReason.SignalClose => true,
                       DisconnectReason.ConnectionTimeout => true,
                       DisconnectReason.StateMismatch => true,
                       DisconnectReason.Migration => true,
                       DisconnectReason.UnknownReason => false,
                       DisconnectReason.UserUnavailable => true,
                       DisconnectReason.SipTrunkFailure => true,
                       _ => false,
                   };
        }
    }
}

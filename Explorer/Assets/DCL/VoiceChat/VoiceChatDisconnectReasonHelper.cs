using DCL.LiveKit.Public;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Static helper for determining if a disconnect reason is valid (expected) or invalid (unexpected).
    /// Used by voice chat services to determine whether reconnection attempts should be made.
    /// </summary>
    public static class VoiceChatDisconnectReasonHelper
    {
        public static bool IsValidDisconnectReason(LKDisconnectReason? disconnectReason)
        {
            if (!disconnectReason.HasValue)
                return false;

            return disconnectReason.Value switch
                   {
                       LKDisconnectReason.RoomDeleted => true,
                       LKDisconnectReason.RoomClosed => true,
                       LKDisconnectReason.ParticipantRemoved => true,
                       LKDisconnectReason.DuplicateIdentity => true,
                       LKDisconnectReason.ServerShutdown => true,
                       LKDisconnectReason.ClientInitiated => true,
                       LKDisconnectReason.JoinFailure => true,
                       LKDisconnectReason.UserRejected => true,
                       LKDisconnectReason.SignalClose => true,
                       LKDisconnectReason.ConnectionTimeout => true,
                       LKDisconnectReason.StateMismatch => true,
                       LKDisconnectReason.Migration => true,
                       LKDisconnectReason.UnknownReason => true,
                       LKDisconnectReason.UserUnavailable => true,
                       LKDisconnectReason.SipTrunkFailure => true,
                       _ => false,
                   };
        }
    }
}

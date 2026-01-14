using System;

namespace DCL.Friends
{
    public class FriendRequest
    {
        protected bool Equals(FriendRequest other) =>
            FriendRequestId == other.FriendRequestId && Timestamp == other.Timestamp && From.Equals(other.From) && To.Equals(other.To) && MessageBody == other.MessageBody;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FriendRequest)obj);
        }

        public override int GetHashCode() =>
            HashCode.Combine(FriendRequestId, Timestamp, From, To, MessageBody);

        public string FriendRequestId { get; }
        public DateTime Timestamp { get; }
        public FriendProfile From { get; }
        public FriendProfile To { get; }
        public string? MessageBody { get; }
        public bool HasBodyMessage => !string.IsNullOrEmpty(MessageBody);

        public FriendRequest(string friendRequestId, DateTime timestamp, FriendProfile from, FriendProfile to, string? messageBody)
        {
            FriendRequestId = friendRequestId;
            Timestamp = timestamp;
            From = from;
            To = to;
            MessageBody = messageBody;
        }
    }
}

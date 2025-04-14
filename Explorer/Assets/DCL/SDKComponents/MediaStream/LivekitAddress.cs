using DCL.Multiplayer.Connections.Rooms;
using System;

namespace DCL.SDKComponents.MediaStream
{
    public readonly struct LivekitAddress
    {
        public static readonly LivekitAddress EMPTY = new (Kind.CURRENT_STREAM, string.Empty, string.Empty, string.Empty);

        public enum Kind
        {
            CURRENT_STREAM,
            USER_STREAM,
        }

        public readonly Kind StreamKind;

        private readonly string identity;
        private readonly string sid;
        private readonly string rawAddress;

        public string CurrentStream
        {
            get
            {
                if (StreamKind is not Kind.CURRENT_STREAM)
                    throw new Exception($"This Address is not {Kind.CURRENT_STREAM}");

                return ParticipantExtensions.LIVEKIT_CURRENT_STREAM;
            }
        }

        public (string identity, string sid) UserStream
        {
            get
            {
                if (StreamKind is not Kind.USER_STREAM)
                    throw new Exception($"This Address is not {Kind.USER_STREAM}");

                return (identity, sid);
            }
        }

        public bool IsEmpty => StreamKind switch
                               {
                                   Kind.CURRENT_STREAM => string.IsNullOrEmpty(ParticipantExtensions.LIVEKIT_CURRENT_STREAM),
                                   Kind.USER_STREAM => string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(sid),
                                   _ => throw new ArgumentOutOfRangeException()
                               };

        private LivekitAddress(Kind streamKind, string identity, string sid, string rawAddress)
        {
            StreamKind = streamKind;
            this.identity = identity;
            this.sid = sid;
            this.rawAddress = rawAddress;
        }

        public static LivekitAddress New(string rawAddress)
        {
            if (rawAddress.IsLivekitAddress() == false)
                throw new InvalidOperationException();

            if (rawAddress == ParticipantExtensions.LIVEKIT_CURRENT_STREAM)
                return new LivekitAddress(Kind.CURRENT_STREAM, string.Empty, string.Empty, string.Empty);

            (string identity, string sid) = rawAddress.DeconstructLivekitAddress();
            return new LivekitAddress(Kind.USER_STREAM, identity, sid, rawAddress);
        }

        public override string ToString() =>
            StreamKind is Kind.CURRENT_STREAM ? ParticipantExtensions.LIVEKIT_CURRENT_STREAM : rawAddress;

        public bool Equals(LivekitAddress other) =>
            this == other;

        public static bool operator ==(LivekitAddress left, LivekitAddress right) =>
            left.StreamKind == right.StreamKind
            && left.identity == right.identity
            && left.sid == right.sid;

        public static bool operator !=(LivekitAddress left, LivekitAddress right) =>
            !(left == right);

        public override bool Equals(object? obj) =>
            obj is LivekitAddress other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)StreamKind, identity, sid);
    }
}

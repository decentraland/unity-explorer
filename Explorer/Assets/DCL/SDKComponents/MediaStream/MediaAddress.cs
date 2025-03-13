using DCL.Multiplayer.Connections.Rooms;
using System;

namespace DCL.SDKComponents.MediaStream
{
    public readonly struct MediaAddress : IEquatable<MediaAddress>
    {
        public enum Kind
        {
            URL,
            LIVEKIT,
        }

        public readonly Kind MediaKind;
        private readonly string url;

        private readonly string identity;
        private readonly string sid;

        public string Url
        {
            get
            {
                if (MediaKind is not Kind.URL)
                    throw new Exception("This MediaAddress is not a URL");

                return url;
            }
        }

        public (string identity, string sid) Livekit
        {
            get
            {
                if (MediaKind is not Kind.LIVEKIT)
                    throw new Exception("This MediaAddress is not a LIVEKIT");

                return (identity, sid);
            }
        }

        public bool IsEmpty => MediaKind switch
                               {
                                   Kind.URL => string.IsNullOrEmpty(url),
                                   Kind.LIVEKIT => string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(sid),
                                   _ => throw new ArgumentOutOfRangeException()
                               };

        private MediaAddress(Kind mediaKind, string url, string identity, string sid)
        {
            MediaKind = mediaKind;
            this.url = url;
            this.identity = identity;
            this.sid = sid;
        }

        public static MediaAddress New(string rawAddress)
        {
            if (rawAddress.IsLivekitAddress())
            {
                (string identity, string sid) = rawAddress.DeconstructLivekitAddress();
                return new MediaAddress(Kind.LIVEKIT, string.Empty, identity, sid);
            }

            return new MediaAddress(Kind.URL, rawAddress, string.Empty, string.Empty);
        }

        public override string ToString() =>
            MediaKind is Kind.URL ? Url : identity.ToLivekitAddress(sid);

        public bool Equals(MediaAddress other) =>
            MediaKind == other.MediaKind
            && url == other.url
            && identity == other.identity
            && sid == other.sid;

        public static bool operator ==(MediaAddress left, MediaAddress right) =>
            left.MediaKind == right.MediaKind
            && left.url == right.url
            && left.identity == right.identity
            && left.sid == right.sid;

        public static bool operator !=(MediaAddress left, MediaAddress right) =>
            !(left == right);

        public override bool Equals(object? obj) =>
            obj is MediaAddress other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)MediaKind, url, identity, sid);
    }
}

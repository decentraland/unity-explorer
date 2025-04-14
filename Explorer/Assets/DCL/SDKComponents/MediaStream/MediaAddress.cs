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
        private readonly LivekitAddress livekitAddress;

        public string Url
        {
            get
            {
                if (MediaKind is not Kind.URL)
                    throw new InvalidOperationException("This MediaAddress is not a URL");

                return url;
            }
        }

        public LivekitAddress Livekit
        {
            get
            {
                if (MediaKind is not Kind.LIVEKIT)
                    throw new InvalidOperationException("This MediaAddress is not a LIVEKIT");

                return livekitAddress;
            }
        }

        public bool IsEmpty => MediaKind switch
                               {
                                   Kind.URL => string.IsNullOrEmpty(url),
                                   Kind.LIVEKIT => livekitAddress.IsEmpty,
                                   _ => throw new InvalidOperationException()
                               };

        private MediaAddress(Kind mediaKind, string url, LivekitAddress livekitAddress)
        {
            MediaKind = mediaKind;
            this.url = url;
            this.livekitAddress = livekitAddress;
        }

        public static MediaAddress New(string rawAddress)
        {
            if (rawAddress.IsLivekitAddress())
            {
                var livekitAddress = LivekitAddress.New(rawAddress);
                return new MediaAddress(Kind.LIVEKIT, string.Empty, livekitAddress);
            }

            return new MediaAddress(Kind.URL, rawAddress, LivekitAddress.EMPTY);
        }

        public override string ToString() =>
            MediaKind is Kind.URL ? Url : livekitAddress.ToString();

        public bool Equals(MediaAddress other) =>
            MediaKind == other.MediaKind
            && url == other.url
            && livekitAddress == other.livekitAddress;

        public static bool operator ==(MediaAddress left, MediaAddress right) =>
            left.MediaKind == right.MediaKind
            && left.url == right.url
            && left.livekitAddress == right.livekitAddress;

        public static bool operator !=(MediaAddress left, MediaAddress right) =>
            !(left == right);

        public override bool Equals(object? obj) =>
            obj is MediaAddress other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)MediaKind, url, livekitAddress.GetHashCode());
    }
}

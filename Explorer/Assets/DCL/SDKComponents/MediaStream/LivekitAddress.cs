using DCL.Multiplayer.Connections.Rooms;
using REnum;
using System;

namespace DCL.SDKComponents.MediaStream
{
    public readonly struct UserStream
    {
        public readonly string Identity;
        public readonly string Sid;
        private readonly string raw;

        public UserStream(string rawAddress)
        {
            (string identity, string sid) = rawAddress.DeconstructLivekitAddress();
            this.Identity = identity;
            this.Sid = sid;
            this.raw = rawAddress;
        }

        public override string ToString() =>
            raw;
    }

    [REnum]
    [REnumField(typeof(UserStream))]
    [REnumFieldEmpty("CurrentStream")]
    public partial struct LivekitAddress
    {
        public static readonly LivekitAddress EMPTY = CurrentStream();

        public bool IsEmpty => Match(
            static stream => string.IsNullOrEmpty(stream.Identity) || string.IsNullOrEmpty(stream.Sid),
            onCurrentStream: static () => string.IsNullOrEmpty(ParticipantExtensions.LIVEKIT_CURRENT_STREAM)
        );

        public static LivekitAddress New(string rawAddress)
        {
            if (rawAddress.IsLivekitAddress() == false)
                throw new InvalidOperationException();

            return rawAddress == ParticipantExtensions.LIVEKIT_CURRENT_STREAM
                ? CurrentStream()
                : FromUserStream(new UserStream(rawAddress));
        }
    }
}

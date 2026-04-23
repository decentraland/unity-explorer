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

        public UserStream(string identity, string sid)
        {
            this.Identity = identity;
            this.Sid = sid;
            this.raw = $"livekit-video://{identity}/{sid}";
        }

        public override string ToString() =>
            raw;
    }

    public readonly struct PresentationBotStream
    {
        public readonly string Identity;
        public readonly string Sid;

        public PresentationBotStream(string identity, string sid)
        {
            this.Identity = identity;
            this.Sid = sid;
        }
    }

    [REnum]
    [REnumField(typeof(UserStream))]
    [REnumField(typeof(PresentationBotStream))]
    [REnumFieldEmpty("CurrentStream")]
    public partial struct LivekitAddress
    {
        public static readonly LivekitAddress EMPTY = CurrentStream();

        public bool IsEmpty => Match(
            onUserStream: static stream => string.IsNullOrEmpty(stream.Identity) || string.IsNullOrEmpty(stream.Sid),
            onPresentationBotStream: static bot => string.IsNullOrEmpty(bot.Identity) || string.IsNullOrEmpty(bot.Sid),
            onCurrentStream: static () => string.IsNullOrEmpty(LiveKitMediaExtensions.LIVEKIT_CURRENT_STREAM)
        );

        public bool IsPresentationBot => IsPresentationBotStream(out _);

        public string? Identity => Match<string?>(
            onUserStream: static userStream => userStream.Identity,
            onPresentationBotStream: static bot => bot.Identity,
            onCurrentStream: static () => null
        );

        public static LivekitAddress New(string rawAddress)
        {
            if (rawAddress.IsLivekitAddress() == false)
                throw new InvalidOperationException();

            return rawAddress == LiveKitMediaExtensions.LIVEKIT_CURRENT_STREAM
                ? CurrentStream()
                : FromUserStream(new UserStream(rawAddress));
        }

        public static LivekitAddress FromIdentity(string identity, string sid) =>
            identity.IsPresentationBotIdentity()
                ? FromPresentationBotStream(new PresentationBotStream(identity, sid))
                : FromUserStream(new UserStream(identity, sid));
    }
}

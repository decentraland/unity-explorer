using DCL.Multiplayer.Connections.Rooms;
using REnum;
using System;

namespace DCL.SDKComponents.MediaStream
{
    public readonly struct UrlMediaAddress
    {
        public readonly string Url;

        public UrlMediaAddress(string url)
        {
            Url = url;
        }

        public override string ToString() =>
            Url;
    }

    [REnum]
    [REnumField(typeof(UrlMediaAddress))]
    [REnumField(typeof(LivekitAddress))]
    public partial struct MediaAddress : IEquatable<MediaAddress>
    {
        public bool IsEmpty => Match(
            onUrlMediaAddress: static address => string.IsNullOrEmpty(address.Url),
            onLivekitAddress: static address => address.IsEmpty
        );

        public static MediaAddress New(string rawAddress)
        {
            if (rawAddress.IsLivekitAddress())
            {
                return FromLivekitAddress(LivekitAddress.New(rawAddress));
            }

            return FromUrlMediaAddress(new UrlMediaAddress(rawAddress));
        }
    }
}

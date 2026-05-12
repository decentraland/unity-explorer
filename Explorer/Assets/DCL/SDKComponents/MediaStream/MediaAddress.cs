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
#if !UNITY_WEBGL
    [REnumField(typeof(LivekitAddress))]
#endif
    public partial struct MediaAddress
    {
        public bool IsEmpty => Match(
            onUrlMediaAddress: static address => string.IsNullOrEmpty(address.Url)
#if !UNITY_WEBGL
            , onLivekitAddress: static address => address.IsEmpty
#endif
        );

        public static MediaAddress New(string rawAddress)
        {
#if !UNITY_WEBGL
            if (rawAddress.IsLivekitAddress())
            {
                return FromLivekitAddress(LivekitAddress.New(rawAddress));
            }
#endif

            return FromUrlMediaAddress(new UrlMediaAddress(rawAddress));
        }
    }
}

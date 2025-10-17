using System.Diagnostics.CodeAnalysis;

namespace DCL.SDKComponents.MediaStream
{
    public static class LiveKitMediaExtensions
    {
        /// <summary>
        ///     Contract predefined value
        /// </summary>
        public const string LIVEKIT_CURRENT_STREAM = "livekit-video://current-stream";

        [SuppressMessage("ReSharper", "StringStartsWithIsCultureSpecific")]
        public static bool IsLivekitAddress(this string address) =>
            address.StartsWith("livekit-video://");

        public static (string identity, string sid) DeconstructLivekitAddress(this string address)
        {
            string[]? parts = address.Split('/');
            return (parts![2], parts[3]);
        }
    }
}

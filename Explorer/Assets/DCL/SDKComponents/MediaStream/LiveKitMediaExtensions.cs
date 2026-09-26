using System.Diagnostics.CodeAnalysis;

namespace DCL.SDKComponents.MediaStream
{
    public static class LiveKitMediaExtensions
    {
        /// <summary>
        ///     Contract predefined value
        /// </summary>
        public const string LIVEKIT_CURRENT_STREAM = "livekit-video://current-stream";

        public const string PRESENTATION_BOT_IDENTITY_PREFIX = "presentation-bot:";

        [SuppressMessage("ReSharper", "StringStartsWithIsCultureSpecific")]
        public static bool IsLivekitAddress(this string address) =>
            address.StartsWith("livekit-video://");

        [SuppressMessage("ReSharper", "StringStartsWithIsCultureSpecific")]
        public static bool IsPresentationBotIdentity(this string identity) =>
            identity.StartsWith(PRESENTATION_BOT_IDENTITY_PREFIX);

        public static (string identity, string sid) DeconstructLivekitAddress(this string address)
        {
            string[]? parts = address.Split('/');
            return (parts![2], parts[3]);
        }
    }
}

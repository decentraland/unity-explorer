using Newtonsoft.Json;

namespace DCL.WebRequests
{
    public readonly struct GetTextureArguments
    {
        public readonly TextureType TextureType;
        public readonly bool UseKtx;
        public readonly bool DisableRedirects;

        /// <summary>
        ///     Decodes a non-KTX body into a non-readable texture on a worker thread with <see cref="UnityEngine.Networking.DownloadHandlerTexture" />.
        /// </summary>
        public readonly bool DecodeOffMainThread;

        [JsonConstructor]
        public GetTextureArguments(TextureType textureType, bool useKtx = true, bool disableRedirects = false, bool decodeOffMainThread = false)
        {
            this.TextureType = textureType;
            this.UseKtx = useKtx;
            this.DisableRedirects = disableRedirects;
            this.DecodeOffMainThread = decodeOffMainThread;
        }
    }
}

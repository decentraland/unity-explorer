using Newtonsoft.Json;

namespace DCL.WebRequests
{
    public readonly struct GetTextureArguments
    {
        public readonly TextureType TextureType;
        public readonly bool UseKtx;
        public readonly bool DisableRedirects;

        [JsonConstructor]
        public GetTextureArguments(TextureType textureType, bool useKtx = true, bool disableRedirects = false)
        {
            this.TextureType = textureType;
            this.UseKtx = useKtx;
            DisableRedirects = false;
        }
    }
}

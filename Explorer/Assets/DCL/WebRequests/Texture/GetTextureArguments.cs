using Newtonsoft.Json;

namespace DCL.WebRequests
{
    public readonly struct GetTextureArguments
    {
        public readonly TextureType TextureType;
        public readonly bool UseKtx;

        [JsonConstructor]
        public GetTextureArguments(TextureType textureType, bool useKtx = true)
        {
            this.TextureType = textureType;
            this.UseKtx = useKtx;
        }
    }
}

namespace DCL.WebRequests
{
    public readonly struct GetTextureArguments
    {
        public readonly TextureType TextureType;
        public readonly bool UseKtx;

        public GetTextureArguments(TextureType textureType, bool useKtx = false)
        {
            this.TextureType = textureType;
            this.UseKtx = useKtx;
        }
    }
}

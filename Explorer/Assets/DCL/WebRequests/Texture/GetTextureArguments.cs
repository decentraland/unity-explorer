using Plugins.TexturesFuse.TexturesServerWrap.Unzips;

namespace DCL.WebRequests
{
    public readonly struct GetTextureArguments
    {
        public readonly TextureType TextureType;

        public GetTextureArguments(TextureType textureType)
        {
            TextureType = textureType;
        }
    }
}

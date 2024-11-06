using Plugins.TexturesFuse.TexturesServerWrap.Unzips;

namespace DCL.WebRequests
{
    public readonly struct GetTextureArguments
    {
        public readonly ITexturesFuse TexturesFuse;
        public readonly TextureType TextureType;

        public GetTextureArguments(ITexturesFuse texturesFuse, TextureType textureType)
        {
            TexturesFuse = texturesFuse;
            TextureType = textureType;
        }
    }
}

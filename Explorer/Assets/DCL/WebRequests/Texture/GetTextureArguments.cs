using Plugins.TexturesFuse.TexturesServerWrap.Unzips;

namespace DCL.WebRequests
{
    public readonly struct GetTextureArguments
    {
        public readonly ITexturesUnzip TexturesUnzip;
        public readonly TextureType TextureType;

        public GetTextureArguments(ITexturesUnzip texturesUnzip, TextureType textureType)
        {
            TexturesUnzip = texturesUnzip;
            TextureType = textureType;
        }
    }
}

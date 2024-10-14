using Plugins.TexturesFuse.TexturesServerWrap.Unzips;

namespace DCL.WebRequests
{
    public readonly struct GetTextureArguments
    {
        public readonly ITexturesUnzip TexturesUnzip;

        public GetTextureArguments(ITexturesUnzip texturesUnzip)
        {
            TexturesUnzip = texturesUnzip;
        }
    }
}

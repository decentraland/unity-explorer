using Plugins.TexturesFuse.TexturesServerWrap.Unzips;

namespace DCL.WebRequests.ArgsFactory
{
    public class GetTextureArgsFactory : IGetTextureArgsFactory
    {
        private readonly ITexturesUnzip texturesUnzip;

        public GetTextureArgsFactory(ITexturesUnzip texturesUnzip)
        {
            this.texturesUnzip = texturesUnzip;
        }

        public GetTextureArguments NewArguments() =>
            new (texturesUnzip);

        public void Dispose()
        {
            texturesUnzip.Dispose();
        }
    }
}

using Plugins.TexturesFuse.TexturesServerWrap.Unzips;

namespace DCL.WebRequests.ArgsFactory
{
    public class GetTextureArgsFactory : IGetTextureArgsFactory
    {
        private readonly ITexturesFuse texturesFuse;

        public GetTextureArgsFactory(ITexturesFuse texturesFuse)
        {
            this.texturesFuse = texturesFuse;
        }

        public GetTextureArguments NewArguments(TextureType textureType) =>
            new (texturesFuse, textureType);

        public void Dispose()
        {
            texturesFuse.Dispose();
        }
    }
}

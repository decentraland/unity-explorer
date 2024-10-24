using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;

namespace DCL.WebRequests.ArgsFactory
{
    public interface IGetTextureArgsFactory : IDisposable
    {
        GetTextureArguments NewArguments(TextureType textureType);
    }
}

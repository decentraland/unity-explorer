using System;

namespace DCL.WebRequests.ArgsFactory
{
    public interface IGetTextureArgsFactory : IDisposable
    {
        GetTextureArguments NewArguments(bool isReadable);
    }
}

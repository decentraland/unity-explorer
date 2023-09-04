using Microsoft.ClearScript.JavaScript;
using System;

namespace SceneRuntime
{
    public interface IJSOperations : IDisposable
    {
        ITypedArray<byte> CreateUint8Array(int bytes);
    }
}

using Microsoft.ClearScript.JavaScript;
using System;
using System.Buffers;
using System.Collections.Generic;

namespace SceneRuntime
{
    public interface IJsOperations : IDisposable
    {
        ITypedArray<byte> CreateUint8Array(int bytes);

        object ConvertToScriptTypedArrays(IReadOnlyList<IMemoryOwner<byte>> byteArrays);
    }
}

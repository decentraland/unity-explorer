using Microsoft.ClearScript.JavaScript;
using System;

namespace SceneRuntime
{
    public interface IJsOperations : IDisposable
    {
        ITypedArray<byte> CreateUint8Array(int bytes);
        object ConvertToScriptTypedArrays(byte[][] byteArrays);
    }
}

using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;

namespace SceneRuntime
{
    public interface IJsOperations : IDisposable
    {
        public const int LIVEKIT_MAX_SIZE = 1024 * 13;

        ITypedArray<byte> GetTempUint8Array();
        ScriptObject NewArray();
        ITypedArray<byte> NewUint8Array(int length);
    }
}

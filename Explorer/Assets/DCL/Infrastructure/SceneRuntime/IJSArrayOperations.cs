using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;

namespace SceneRuntime
{
    /// <summary>
    ///     Operations bound to the instance of <see cref="IScriptEngine" />. If it is disposed of, calling methods will throw <see cref="ObjectDisposedException" />
    /// </summary>
    public interface IJsOperations : IDisposable
    {
        public const int LIVEKIT_MAX_SIZE = 1024 * 13;

        ITypedArray<byte> GetTempUint8Array();

        ScriptObject NewArray();

        ITypedArray<byte> NewUint8Array(int length);
    }
}

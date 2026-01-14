using System;
using Utility;

namespace SceneRuntime
{
    /// <summary>
    ///     Operations bound to the instance of <see cref="IJavaScriptEngine" />. If it is disposed of, calling methods will throw <see cref="ObjectDisposedException" />
    /// </summary>
    public interface IJsOperations : IDisposable
    {
        public const int LIVEKIT_MAX_SIZE = 1024 * 13;

        IDCLTypedArray<byte> GetTempUint8Array();

        IDCLScriptObject NewArray();

        IDCLTypedArray<byte> NewUint8Array(int length);
    }
}

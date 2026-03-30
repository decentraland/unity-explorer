using System;

namespace SceneRuntime
{
    /// <summary>
    ///     Operations bound to the instance of <see cref="IJavaScriptEngine" />. If it is disposed of, calling methods will throw <see cref="ObjectDisposedException" />
    /// </summary>
    public interface IJsOperations : IDisposable
    {
        public const int LIVEKIT_MAX_SIZE = 1024 * 13;

#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
        WebClient.WebClientTypedArrayAdapter GetTempUint8Array();

        WebClient.WebClientScriptObject NewArray();

        WebClient.WebClientTypedArrayAdapter NewUint8Array(int length);
#else
        V8.V8TypedArrayAdapter GetTempUint8Array();

        V8.V8ScriptObjectAdapter NewArray();

        V8.V8TypedArrayAdapter NewUint8Array(int length);
#endif
    }
}

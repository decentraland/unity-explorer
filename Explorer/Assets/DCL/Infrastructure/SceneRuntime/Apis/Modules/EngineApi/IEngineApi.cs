using CrdtEcsBridge.PoolsProviders;
using System;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    /// <summary>
    ///     The contracts correspond directly to the JS-SDK-Toolchain and its transport API.
    ///     They don't have Protobuf related stuff
    /// </summary>
    public interface IEngineApi : IDisposable
    {
        /// <param name="dataMemory"></param>
        /// <param name="returnData"></param>
        /// <returns>A contiguous byte array of the CRDT Message representing the outgoing messages</returns>
        public PoolableByteArray CrdtSendToRenderer(ReadOnlyMemory<byte> dataMemory, bool returnData = true);

        /// <returns>The full serialized CRDT State, A contiguous byte array of the CRDT Message</returns>
        public PoolableByteArray CrdtGetState();

#if UNITY_INCLUDE_TESTS || UNITY_EDITOR
        public class Fake : IEngineApi
        {
            public PoolableByteArray CrdtSendToRenderer(ReadOnlyMemory<byte> dataMemory, bool returnData = true)
            {
               return PoolableByteArray.EMPTY;
            }

            public PoolableByteArray CrdtGetState()
            {
               return PoolableByteArray.EMPTY;
            }

            public void Dispose()
            {
                // Ignore
            }
        }
#endif
    }
}

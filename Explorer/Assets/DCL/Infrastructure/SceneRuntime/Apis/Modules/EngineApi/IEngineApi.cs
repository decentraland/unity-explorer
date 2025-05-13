using CrdtEcsBridge.PoolsProviders;
using System;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    /// <summary>
    ///     The contracts correspond directly to the JS-SDK-Toolchain and its transport API.
    ///     They don't have Protobuf related stuff
    /// </summary>
    public interface IEngineApi
    {
        /// <param name="dataMemory"></param>
        /// <param name="returnData"></param>
        /// <returns>A contiguous byte array of the CRDT Message representing the outgoing messages</returns>
        public PoolableByteArray CrdtSendToRenderer(ReadOnlyMemory<byte> dataMemory, bool returnData = true);

        /// <returns>The full serialized CRDT State, A contiguous byte array of the CRDT Message</returns>
        public PoolableByteArray CrdtGetState();

        /// <summary>
        ///     Prevents handling messages while the scene runtime is being disposed
        /// </summary>
        void SetIsDisposing();
    }
}

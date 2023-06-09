﻿using System;

namespace SceneRuntime.Apis.Modules
{
    /// <summary>
    /// The contracts correspond directly to the JS-SDK-Toolchain and its transport API.
    /// They don't have Protobuf related stuff
    /// </summary>
    public interface IEngineApi : IDisposable
    {
        /// <param name="dataMemory"></param>
        /// <returns>A contiguous byte array of the CRDT Message representing the outgoing messages</returns>
        public ArraySegment<byte> CrdtSendToRenderer(ReadOnlyMemory<byte> dataMemory);

        /// <returns>The full serialized CRDT State, A contiguous byte array of the CRDT Message</returns>
        public ArraySegment<byte> CrdtGetState();

        /// <summary>
        ///     Prevents handling messages while the scene runtime is being disposed
        /// </summary>
        void SetIsDisposing();
    }
}

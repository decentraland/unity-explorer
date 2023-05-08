using Cysharp.Threading.Tasks;
using System;

namespace SceneRuntime.Apis.Modules
{
    /// <summary>
    /// The contracts correspond directly to the JS-SDK-Toolchain and its transport API.
    /// They don't have Protobuf related stuff
    /// </summary>
    public interface IEngineApi : IDisposable
    {
        /// <param name="data">A contiguous byte array of the CRDT Message</param>
        /// <returns>A contiguous byte array of the CRDT Message representing the outgoing messages</returns>
        public UniTask<byte[]> CrdtSendToRenderer(byte[] data);

        /// <returns>The full serialized CRDT State, A contiguous byte array of the CRDT Message</returns>
        public UniTask<byte[]> CrdtGetState();
    }
}

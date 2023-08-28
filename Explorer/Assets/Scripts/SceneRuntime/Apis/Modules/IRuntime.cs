using Cysharp.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;
using System;

namespace SceneRuntime.Apis.Modules
{
    /// <summary>
    /// The contracts correspond directly to the JS-SDK-Toolchain and its transport API.
    /// They don't have Protobuf related stuff
    /// </summary>
    public interface IRuntime : IDisposable
    {
        /// <param name="dataMemory"></param>
        /// <returns>A contiguous byte array of the CRDT Message representing the outgoing messages</returns>
        public UniTask<ITypedArray<byte>> ReadFile(string fileName);
    }
}

using CRDT.Protocol;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.PoolsProviders
{
    /// <summary>
    ///     Provides single threaded pools dedicated to a single scene instance
    /// </summary>
    public interface IInstancePoolsProvider : IDisposable
    {
        /// <summary>
        ///     Get a poolable byte array for usage in JS API implementations and wrappers
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        PoolableByteArray GetAPIRawDataPool(int size);

        List<CRDTMessage> GetDeserializationMessagesPool();

        void ReleaseDeserializationMessagesPool(List<CRDTMessage> messages);
    }
}

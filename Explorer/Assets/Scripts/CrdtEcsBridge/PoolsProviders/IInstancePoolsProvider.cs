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
        PoolableByteArray GetCrdtRawDataPool(int size);

        List<CRDTMessage> GetDeserializationMessagesPool();

        void ReleaseDeserializationMessagesPool(List<CRDTMessage> messages);
    }
}

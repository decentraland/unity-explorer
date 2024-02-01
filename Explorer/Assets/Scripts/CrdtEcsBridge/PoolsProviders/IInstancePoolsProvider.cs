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
        public byte[] GetCrdtRawDataPool(int size);

        public void ReleaseCrdtRawDataPool(byte[] bytes);

        List<CRDTMessage> GetDeserializationMessagesPool();

        void ReleaseDeserializationMessagesPool(List<CRDTMessage> messages);
    }
}

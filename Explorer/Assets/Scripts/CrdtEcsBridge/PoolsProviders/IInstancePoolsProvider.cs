using CRDT.Protocol;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.Engine
{
    /// <summary>
    ///     Provides single threaded pools dedicated to a single scene instance
    /// </summary>
    public interface IInstancePoolsProvider : IDisposable
    {
        public byte[] GetCrdtRawDataPool(int size);

        public void ReleaseCrdtRawDataPool(byte[] bytes);

        IList<CRDTMessage> GetDeserializationMessagesPool();

        void ReleaseDeserializationMessagesPool(IList<CRDTMessage> messages);
    }
}

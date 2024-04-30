using CrdtEcsBridge.PoolsProviders;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents;
using System;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi
{
    public interface ICommunicationsControllerAPI : IDisposable
    {
        object SendBinary(IReadOnlyList<PoolableByteArray> data);
        void Send(byte[] data);

        void OnSceneIsCurrentChanged(bool isCurrent);

        List<CommsPayload> SceneCommsMessages { get; }
    }
}

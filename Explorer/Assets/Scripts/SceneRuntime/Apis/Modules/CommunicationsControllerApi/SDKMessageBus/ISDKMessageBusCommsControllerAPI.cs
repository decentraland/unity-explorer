using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi.SDKMessageBus
{
    public interface ISDKMessageBusCommsControllerAPI : ICommunicationsControllerAPI
    {
        List<CommsPayload> SceneCommsMessages { get; }
        void Send(string data);
    }
}


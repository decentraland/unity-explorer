using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi.SDKMessageBus
{
    public interface ISDKMessageBusCommsControllerAPI : ICommunicationsControllerAPI
    {
        IReadOnlyList<CommsPayload> SceneCommsMessages { get; }

        void ClearMessages();

        void Send(string data);
    }
}

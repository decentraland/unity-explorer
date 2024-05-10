using CRDT.Protocol.Factory;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public interface ISDKObservableEventsEngineApi : IEngineApi
    {
        List<ProcessedCRDTMessage> OutgoingCRDTMessages { get; }
        bool EnableSDKObservableMessagesDetection { get; set; }

        void ClearMessages();
    }
}

using CRDT.Protocol;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public interface ISDKObservableEventsEngineApi : IEngineApi
    {
        List<CRDTMessage> PriorityOutgoingCRDTMessages { get; }
        List<CRDTMessage> OutgoingCRDTMessages { get; }
        bool EnableSDKObservableMessagesDetection { get; set; }

        void ClearMessages();
    }
}

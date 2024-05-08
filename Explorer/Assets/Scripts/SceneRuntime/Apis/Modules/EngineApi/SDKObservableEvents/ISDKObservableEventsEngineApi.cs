using CRDT.Protocol.Factory;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public interface ISDKObservableEventsEngineApi : IEngineApi
    {
        HashSet<ProcessedCRDTMessage> OutgoingCRDTMessages { get; }

        void ClearOutgoingCRDTMessages();
    }
}

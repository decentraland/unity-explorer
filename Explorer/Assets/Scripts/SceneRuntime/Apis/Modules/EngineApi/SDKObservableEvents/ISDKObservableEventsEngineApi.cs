using CrdtEcsBridge.PoolsProviders;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public interface ISDKObservableEventsEngineApi : IEngineApi
    {
        void AddSDKObservableEvent(SDKObservableEvent sdkObservableEvent);

        void ClearSDKObservableEvents();

        PoolableSDKObservableEventArray? ConsumeSDKObservableEvents();

        HashSet<string> SdkObservableEventSubscriptions { get; }

        bool EnableSDKObservableMessagesDetection { set; }
    }
}

using CrdtEcsBridge.PoolsProviders;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public interface ISDKObservableEventsEngineApi : IEngineApi
    {
        List<SDKObservableEvent> SdkObservableEvents { get; }
        HashSet<string> SdkObservableEventSubscriptions { get; }
        PoolableSDKObservableEventArray? ConsumeSDKObservableEvents();
        bool EnableSDKObservableMessagesDetection { set; }
    }
}

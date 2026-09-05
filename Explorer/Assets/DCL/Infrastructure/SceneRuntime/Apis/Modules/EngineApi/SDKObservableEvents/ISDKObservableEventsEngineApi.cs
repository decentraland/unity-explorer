using CrdtEcsBridge.PoolsProviders;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public interface ISDKObservableEventsEngineApi : IEngineApi
    {
        void AddSDKObservableEvent(SDKObservableEvent sdkObservableEvent);

        void ClearSDKObservableEvents();

        PoolableSDKObservableEventArray? ConsumeSDKObservableEvents();

        void TryAddSubscription(string eventId);

        void RemoveSubscriptionIfExists(string eventId);

        bool HasSubscription(string eventId);

        bool IsAnySubscription();
    }
}

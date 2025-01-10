using CrdtEcsBridge.PoolsProviders;
using JetBrains.Annotations;
using Microsoft.ClearScript.V8.SplitProxy;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi.SDKMessageBus;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public class SDKObservableEventsEngineApiWrapper : EngineApiWrapper
    {
        private readonly ISDKObservableEventsEngineApi engineApi;
        private readonly ISDKMessageBusCommsControllerAPI commsApi;

        private readonly InvokeHostObject subscribeToSDKObservableEvent;
        private readonly InvokeHostObject unsubscribeFromSDKObservableEvent;

        public SDKObservableEventsEngineApiWrapper(ISDKObservableEventsEngineApi api, ISDKMessageBusCommsControllerAPI commsApi, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler exceptionsHandler) : base(api, instancePoolsProvider, exceptionsHandler)
        {
            engineApi = api;
            this.commsApi = commsApi;

            subscribeToSDKObservableEvent = (args, result) =>
            {
                string eventId = args[0].GetString();
                SubscribeToSDKObservableEvent(eventId);
            };

            unsubscribeFromSDKObservableEvent = (args, result) =>
            {
                string eventId = args[0].GetString();
                UnsubscribeFromSDKObservableEvent(eventId);
            };
        }

        // Used for SDK Observables + SDK Comms MessageBus
        [UsedImplicitly]
        public override ScriptableSDKObservableEventArray? SendBatch()
        {
            // If there are no subscriptions at all there is nothing to handle
            if (engineApi.IsAnySubscription() == false)
            {
                engineApi.ClearSDKObservableEvents();
                commsApi.ClearMessages();
                return null;
            }

            try
            {
                // SDK 'comms' observable for scenes MessageBus
                DetectSceneMessageBusCommsObservableEvent();

                PoolableSDKObservableEventArray? result = engineApi.ConsumeSDKObservableEvents();

                return result.HasValue ? new ScriptableSDKObservableEventArray(result.Value) : null;
            }
            catch (Exception e)
            {
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return null;
            }
        }

        private void DetectSceneMessageBusCommsObservableEvent()
        {
            if (!engineApi.HasSubscription(SDKObservableEventIds.Comms))
                return;

            if (commsApi.SceneCommsMessages.Count == 0) return;

            foreach (CommsPayload currentPayload in commsApi.SceneCommsMessages)
                engineApi.AddSDKObservableEvent(SDKObservableUtils.NewSDKObservableEventFromData(SDKObservableEventIds.Comms, currentPayload));

            commsApi.ClearMessages();
        }

        [UsedImplicitly]
        public void SubscribeToSDKObservableEvent(string eventId)
        {
            engineApi.TryAddSubscription(eventId);
        }

        [UsedImplicitly]
        public void UnsubscribeFromSDKObservableEvent(string eventId)
        {
            engineApi.RemoveSubscriptionIfExists(eventId);
        }

        protected override void GetNamedProperty(StdString name, V8Value value, out bool isConst)
        {
            isConst = true;

            if (name.Equals(nameof(SubscribeToSDKObservableEvent)))
                value.SetHostObject(subscribeToSDKObservableEvent);
            else if (name.Equals(nameof(UnsubscribeFromSDKObservableEvent)))
                value.SetHostObject(unsubscribeFromSDKObservableEvent);
            else
                base.GetNamedProperty(name, value, out isConst);
        }
    }
}

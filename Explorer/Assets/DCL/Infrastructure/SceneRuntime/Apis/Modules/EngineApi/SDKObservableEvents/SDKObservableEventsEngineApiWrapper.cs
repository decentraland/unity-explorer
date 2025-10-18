using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript.V8.FastProxy;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi.SDKMessageBus;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System;
using System.Threading;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public sealed class SDKObservableEventsEngineApiWrapper : EngineApiWrapper
    {
        private readonly ISDKObservableEventsEngineApi engineApi;
        private readonly ISDKMessageBusCommsControllerAPI commsApi;

        private static readonly V8FastHostObjectOperations<SDKObservableEventsEngineApiWrapper> OPERATIONS = new ();
        protected override IV8FastHostObjectOperations operations => OPERATIONS;

        static SDKObservableEventsEngineApiWrapper()
        {
            OPERATIONS.Configure(static configuration =>
            {
                EngineApiWrapper.Configure(configuration);

                configuration.AddMethodGetter(nameof(SubscribeToSDKObservableEvent),
                    static (SDKObservableEventsEngineApiWrapper self, in V8FastArgs args, in V8FastResult _) =>
                        self.SubscribeToSDKObservableEvent(args.GetString(0)));

                configuration.AddMethodGetter(nameof(UnsubscribeFromSDKObservableEvent),
                    static (SDKObservableEventsEngineApiWrapper self, in V8FastArgs args, in V8FastResult _) =>
                        self.UnsubscribeFromSDKObservableEvent(args.GetString(0)));
            });
        }

        public SDKObservableEventsEngineApiWrapper(ISDKObservableEventsEngineApi api,
            ISDKMessageBusCommsControllerAPI commsApi,
            IInstancePoolsProvider instancePoolsProvider,
            ISceneExceptionsHandler exceptionsHandler,
            CancellationTokenSource disposeCts)
            : base(api, instancePoolsProvider, exceptionsHandler, disposeCts)
        {
            engineApi = api;
            this.commsApi = commsApi;
        }

        // Used for SDK Observables + SDK Comms MessageBus
        protected override ScriptableSDKObservableEventArray? SendBatch()
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

        private void SubscribeToSDKObservableEvent(string eventId)
        {
            engineApi.TryAddSubscription(eventId);
        }

        private void UnsubscribeFromSDKObservableEvent(string eventId)
        {
            engineApi.RemoveSubscriptionIfExists(eventId);
        }
    }
}

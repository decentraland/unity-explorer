using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript.V8.FastProxy;
using Microsoft.ClearScript.V8.SplitProxy;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi.SDKMessageBus;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System;
using System.Threading;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public sealed class SDKObservableEventsEngineApiWrapper : EngineApiWrapper, IV8FastHostObject
    {
        private readonly ISDKObservableEventsEngineApi engineApi;
        private readonly ISDKMessageBusCommsControllerAPI commsApi;

        private readonly InvokeHostObject subscribeToSDKObservableEvent;
        private readonly InvokeHostObject unsubscribeFromSDKObservableEvent;

        private static readonly V8FastHostObjectOperations<SDKObservableEventsEngineApiWrapper> OPERATIONS = new ();
        IV8FastHostObjectOperations IV8FastHostObject.Operations => OPERATIONS;

        static SDKObservableEventsEngineApiWrapper()
        {
            OPERATIONS.Configure(static configuration =>
            {
                configuration.AddMethodGetter(nameof(CrdtSendToRenderer),
                    static (SDKObservableEventsEngineApiWrapper self, in V8FastArgs args, in V8FastResult result) =>
                        self.CrdtSendToRenderer(args.GetUint8Array(0)));

                configuration.AddMethodGetter(nameof(CrdtGetState),
                    static (SDKObservableEventsEngineApiWrapper self, in V8FastArgs args, in V8FastResult result) =>
                        result.Set(self.CrdtGetState()));

                configuration.AddMethodGetter(nameof(SendBatch),
                    static (SDKObservableEventsEngineApiWrapper self, in V8FastArgs args, in V8FastResult result) =>
                        result.Set(self.SendBatch()));
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

            subscribeToSDKObservableEvent = SubscribeToSDKObservableEvent;
            unsubscribeFromSDKObservableEvent = UnsubscribeFromSDKObservableEvent;
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

        private void SubscribeToSDKObservableEvent(ReadOnlySpan<V8Value.Decoded> args, V8Value result)
        {
            string eventId = args[0].GetString();
            SubscribeToSDKObservableEvent(eventId);
        }

        private void SubscribeToSDKObservableEvent(string eventId)
        {
            engineApi.TryAddSubscription(eventId);
        }

        private void UnsubscribeFromSDKObservableEvent(ReadOnlySpan<V8Value.Decoded> args,
            V8Value result)
        {
            string eventId = args[0].GetString();
            UnsubscribeFromSDKObservableEvent(eventId);
        }

        private void UnsubscribeFromSDKObservableEvent(string eventId)
        {
            engineApi.RemoveSubscriptionIfExists(eventId);
        }

        protected override void GetProperty(StdString name, V8Value value, out bool isConst)
        {
            isConst = true;

            if (name.Equals(nameof(SubscribeToSDKObservableEvent)))
                value.SetHostObject(subscribeToSDKObservableEvent);
            else if (name.Equals(nameof(UnsubscribeFromSDKObservableEvent)))
                value.SetHostObject(unsubscribeFromSDKObservableEvent);
            else
                base.GetProperty(name, value, out isConst);
        }
    }
}

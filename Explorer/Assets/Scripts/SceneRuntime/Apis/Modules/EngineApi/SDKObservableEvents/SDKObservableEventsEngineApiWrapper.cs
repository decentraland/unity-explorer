using CrdtEcsBridge.PoolsProviders;
using DCL.Diagnostics;
using JetBrains.Annotations;
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

        public SDKObservableEventsEngineApiWrapper(ISDKObservableEventsEngineApi api, ISDKMessageBusCommsControllerAPI commsApi, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler exceptionsHandler) : base(api, instancePoolsProvider, exceptionsHandler)
        {
            engineApi = api;
            this.commsApi = commsApi;
        }

        // Used for SDK Observables + SDK Comms MessageBus
        [UsedImplicitly]
        public override ScriptableSDKObservableEventArray? SendBatch()
        {
            // If there are no subscriptions at all there is nothing to handle
            if (engineApi.SdkObservableEventSubscriptions.Count == 0)
            {
                engineApi.SdkObservableEvents.Clear();
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
            if (!engineApi.SdkObservableEventSubscriptions.Contains(SDKObservableEventIds.Comms))
                return;

            if (commsApi.SceneCommsMessages.Count == 0) return;

            foreach (CommsPayload currentPayload in commsApi.SceneCommsMessages)
                engineApi.SdkObservableEvents.Add(SDKObservableUtils.NewSDKObservableEventFromData(SDKObservableEventIds.Comms, currentPayload));

            commsApi.ClearMessages();
        }

        [UsedImplicitly]
        public void SubscribeToSDKObservableEvent(string eventId)
        {
            engineApi.SdkObservableEventSubscriptions.Add(eventId);

            if (eventId == SDKObservableEventIds.PlayerClicked)
                ReportHub.LogWarning(new ReportData(ReportCategory.SDK_OBSERVABLES), "Scene subscribed to unsupported SDK Observable 'PlayerClicked'");
            else
                engineApi.EnableSDKObservableMessagesDetection = true;
        }

        [UsedImplicitly]
        public void UnsubscribeFromSDKObservableEvent(string eventId)
        {
            engineApi.SdkObservableEventSubscriptions.Remove(eventId);

            if (engineApi.SdkObservableEventSubscriptions.Count == 0)
                engineApi.EnableSDKObservableMessagesDetection = false;
        }
    }
}

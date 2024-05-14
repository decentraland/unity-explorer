using CrdtEcsBridge.PoolsProviders;
using DCL.Diagnostics;
using JetBrains.Annotations;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi.SDKMessageBus;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System;
using System.Collections.Generic;

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
        public override List<SDKObservableEvent> SendBatch()
        {
            if (engineApi.SdkObservableEventSubscriptions.Count == 0)
            {
                engineApi.SdkObservableEvents.Clear();
                commsApi.SceneCommsMessages.Clear();
                return null;
            }

            try
            {
                // SDK 'comms' observable for scenes MessageBus
                DetectSceneMessageBusCommsObservableEvent();

                return engineApi.ConsumeSDKObservableEvents();
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

            foreach (CommsPayload currentPayload in commsApi.SceneCommsMessages)
                engineApi.SdkObservableEvents.Add(SDKObservableUtils.GenerateSDKObservableEvent(SDKObservableEventIds.Comms, currentPayload));

            commsApi.SceneCommsMessages.Clear();
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

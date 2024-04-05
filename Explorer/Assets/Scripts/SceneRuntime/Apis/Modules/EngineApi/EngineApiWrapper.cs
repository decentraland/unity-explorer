using CrdtEcsBridge.PoolsProviders;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine.Profiling;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    // TODO: make clear in COMMENTS these observables can be removed/flagged to only use observable-replacement components
    public struct SDKObservableEvent {
        public struct Generic
        {
            public string eventId;
            public string eventData; // stringified JSON
        }

        public Generic generic;
    }

    public class EngineApiWrapper : IDisposable
    {
        internal readonly IEngineApi api;

        private readonly IInstancePoolsProvider instancePoolsProvider;
        private readonly ISceneExceptionsHandler exceptionsHandler;

        private byte[] lastInput;

        private HashSet<string> sdkObservableEventSubscriptions = new HashSet<string>();
        private List<SDKObservableEvent> sdkObservableEventsToTrigger = new List<SDKObservableEvent>();

        public EngineApiWrapper(IEngineApi api, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler exceptionsHandler)
        {
            this.api = api;
            this.instancePoolsProvider = instancePoolsProvider;
            this.exceptionsHandler = exceptionsHandler;

            // TO DEBUG
            // sdkObservableEventsToTrigger.Add(new SDKObservableEvent()
            // {
            //     generic = new SDKObservableEvent.Generic()
            //     {
            //         eventId = "sceneStart",
            //         eventData = "{}"
            //     }
            // });

            // string eventId = "sceneStart";
            // SubscribeToObservableEvent(eventId);
            // TriggerSDKObservableEvent(eventId, new SceneStart());
        }

        public void Dispose()
        {
            // Dispose the last input buffer
            if (lastInput != null)
                instancePoolsProvider.ReleaseCrdtRawDataPool(lastInput);

            lastInput = null;

            // Dispose the engine API Implementation
            // It will dispose its buffers
            api.Dispose();
        }

        [UsedImplicitly]
        public ScriptableByteArray CrdtSendToRenderer(ITypedArray<byte> data)
        {
            try
            {
                Profiler.BeginThreadProfiling("SceneRuntime", "CrdtSendToRenderer");

                var intLength = (int)data.Length;

                if (lastInput == null || lastInput.Length < intLength)
                {
                    // Release the old one
                    if (lastInput != null)
                        instancePoolsProvider.ReleaseCrdtRawDataPool(lastInput);

                    // Rent a new one
                    lastInput = instancePoolsProvider.GetCrdtRawDataPool(intLength);
                }

                // V8ScriptItem does not support zero length
                if (data.Length > 0)

                    // otherwise use the existing one
                    data.Read(0, data.Length, lastInput, 0);

                PoolableByteArray result = api.CrdtSendToRenderer(lastInput.AsMemory().Slice(0, intLength));

                Profiler.EndThreadProfiling();

                return result.IsEmpty ? ScriptableByteArray.EMPTY : new ScriptableByteArray(result);
            }
            catch (Exception e)
            {
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return ScriptableByteArray.EMPTY;
            }
        }

        [UsedImplicitly]
        public ScriptableByteArray CrdtGetState()
        {
            try
            {
                PoolableByteArray result = api.CrdtGetState();
                return result.IsEmpty ? ScriptableByteArray.EMPTY : new ScriptableByteArray(result);
            }
            catch (Exception e)
            {
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return ScriptableByteArray.EMPTY;
            }
        }

        [UsedImplicitly]
        public List<SDKObservableEvent> SendBatch()
        {
            try
            {
                // PoolableByteArray result = api.CrdtGetState();
                // return result.IsEmpty ? ScriptableByteArray.EMPTY : new ScriptableByteArray(result);

                var returnCollection = new List<SDKObservableEvent>(sdkObservableEventsToTrigger);
                sdkObservableEventsToTrigger.Clear();
                return returnCollection;
            }
            catch (Exception e)
            {
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return new List<SDKObservableEvent>();
            }
        }

        [UsedImplicitly]
        public void SubscribeToObservableEvent(string targetEventId)
        {
            sdkObservableEventSubscriptions.Add(targetEventId);
        }

        [UsedImplicitly]
        public void UnsubscribeFromObservableEvent(string targetEventId)
        {
            sdkObservableEventSubscriptions.Remove(targetEventId);
        }

        public void TriggerSDKObservableEvent<T> (string targetEventId, T eventData)
        {
            if (sdkObservableEventSubscriptions.Contains(targetEventId))
            {
                sdkObservableEventsToTrigger.Add(new SDKObservableEvent()
                {
                    generic = new SDKObservableEvent.Generic()
                    {
                        eventId = targetEventId,
                        // eventData = eventData
                        eventData = JsonSerializer.Serialize(eventData)
                    }
                });
            }
        }

        public void SetIsDisposing()
        {
            api.SetIsDisposing();
        }
    }
}

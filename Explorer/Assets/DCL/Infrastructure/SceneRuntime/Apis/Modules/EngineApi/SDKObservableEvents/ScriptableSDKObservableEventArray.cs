using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8.FastProxy;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public sealed class ScriptableSDKObservableEventArray
        : V8FastHostObject<ScriptableSDKObservableEventArray>, IDisposable
    {
        private PoolableSDKObservableEventArray array;

        static ScriptableSDKObservableEventArray()
        {
            Configure(static configuration =>
            {
                configuration.SetEnumeratorFactory(static self => new Enumerator(self));
            });
        }

        public ScriptableSDKObservableEventArray(PoolableSDKObservableEventArray array)
        {
            this.array = array;
        }

        public void Dispose()
        {
            array.Dispose();
        }

        private sealed class Enumerator : IV8FastEnumerator
        {
            private PoolableSDKObservableEventArray.Enumerator enumerator;
            private readonly ScriptObject factory;

            public Enumerator(ScriptableSDKObservableEventArray array)
            {
                enumerator = array.array.GetEnumerator();

                // TODO: Don't create a new factory function every time.
                factory = (ScriptObject)ScriptEngine.Current.Evaluate(@"
                    (function(eventId, eventData) {
                        return {
                            generic: {
                                eventId: eventId,
                                eventData: eventData
                            }
                        };
                    })
                ");
            }

            public bool MoveNext() =>
                enumerator.MoveNext();

            public void Dispose()
            {
                enumerator.Dispose();
                factory.Dispose();
            }

            public void GetCurrent(in V8FastResult item)
            {
                SDKObservableEvent.Generic generic = enumerator.Current.generic;
                item.Set(factory.InvokeAsFunction(generic.eventId, generic.eventData));
            }
        }
    }
}

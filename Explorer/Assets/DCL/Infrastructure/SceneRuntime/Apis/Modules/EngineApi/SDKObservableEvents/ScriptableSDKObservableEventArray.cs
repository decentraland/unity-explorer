using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8.SplitProxy;
using System;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public class ScriptableSDKObservableEventArray : IV8HostObject, IDisposable
    {
        private PoolableSDKObservableEventArray array;

        public ScriptableSDKObservableEventArray(PoolableSDKObservableEventArray array)
        {
            this.array = array;
        }

        public void Dispose()
        {
            array.Dispose();
        }

        void IV8HostObject.GetEnumerator(V8Value result) =>
            result.SetHostObject(new Enumerator(this));

        private sealed class Enumerator : IV8HostObject
        {
            private PoolableSDKObservableEventArray.Enumerator enumerator;
            private readonly ScriptObject factory;
            private readonly InvokeHostObject scriptableMoveNext;
            private readonly InvokeHostObject scriptableDispose;

            public Enumerator(ScriptableSDKObservableEventArray array)
            {
                enumerator = array.array.GetEnumerator();
                scriptableMoveNext = ScriptableMoveNext;
                scriptableDispose = ScriptableDispose;

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

            private void ScriptableMoveNext(ReadOnlySpan<V8Value.Decoded> args, V8Value result) =>
                result.SetBoolean(enumerator.MoveNext());

            private void ScriptableDispose(ReadOnlySpan<V8Value.Decoded> args, V8Value result)
            {
                enumerator.Dispose();
                factory.Dispose();
            }

            void IV8HostObject.GetNamedProperty(StdString name, V8Value value, out bool isConst)
            {
                if (name.Equals("ScriptableCurrent"))
                {
                    var current = enumerator.Current;
                    using var fields = new StdV8ValueArray(2);
                    fields[0].SetString(current.generic.eventId);
                    fields[1].SetString(current.generic.eventData);
                    new V8Object(factory).Invoke(fields, value);
                    isConst = false;
                }
                else if (name.Equals(nameof(ScriptableMoveNext)))
                {
                    value.SetHostObject(scriptableMoveNext);
                    isConst = true;
                }
                else if (name.Equals(nameof(ScriptableDispose)))
                {
                    value.SetHostObject(scriptableDispose);
                    isConst = true;
                }
                else
                    throw new NotImplementedException(
                        $"Named property {name.ToString()} is not implemented");
            }
        }
    }
}

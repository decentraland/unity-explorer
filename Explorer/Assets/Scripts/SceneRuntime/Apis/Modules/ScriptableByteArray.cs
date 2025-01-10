using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript.V8.SplitProxy;
using System;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules
{
    public sealed class ScriptableByteArray : IV8HostObject
    {
        public static readonly ScriptableByteArray EMPTY = new (PoolableByteArray.EMPTY);

        private readonly IEnumerator<byte> enumerator;
        private PoolableByteArray array;

        public ScriptableByteArray(PoolableByteArray array)
        {
            this.array = array;
            enumerator = array.GetEnumerator();
        }

        public void Dispose()
        {
            enumerator.Dispose();
            array.Dispose();
        }

        void IV8HostObject.GetEnumerator(V8Value result) =>
            result.SetHostObject(new Enumerator(this));

        private sealed class Enumerator : IV8HostObject
        {
            private PoolableByteArray.Enumerator enumerator;
            private readonly InvokeHostObject scriptableMoveNext;
            private readonly InvokeHostObject scriptableDispose;

            public Enumerator(ScriptableByteArray array)
            {
                enumerator = array.array.GetEnumerator();
                scriptableMoveNext = ScriptableMoveNext;
                scriptableDispose = ScriptableDispose;
            }

            private void ScriptableMoveNext(ReadOnlySpan<V8Value.Decoded> args, V8Value result) =>
                result.SetBoolean(enumerator.MoveNext());

            private void ScriptableDispose(ReadOnlySpan<V8Value.Decoded> args, V8Value result) =>
                enumerator.Dispose();

            void IV8HostObject.GetNamedProperty(StdString name, V8Value value, out bool isConst)
            {
                if (name.Equals("ScriptableCurrent"))
                {
                    value.SetInt32(enumerator.Current);
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

using CrdtEcsBridge.PoolsProviders;
using Microsoft.ClearScript.V8.FastProxy;
using System;

namespace SceneRuntime.Apis.Modules
{
    public sealed class ScriptableByteArray : V8FastHostObject<ScriptableByteArray>, IDisposable
    {
        public static readonly ScriptableByteArray EMPTY = new (PoolableByteArray.EMPTY);

        private PoolableByteArray array;

        static ScriptableByteArray()
        {
            Configure(static configuration =>
            {
                configuration.SetEnumeratorFactory(static self => new Enumerator(self));
            });
        }

        public ScriptableByteArray(PoolableByteArray array)
        {
            this.array = array;
        }

        public void Dispose()
        {
            array.Dispose();
        }

        private sealed class Enumerator : IV8FastEnumerator
        {
            private PoolableByteArray.Enumerator enumerator;

            public Enumerator(ScriptableByteArray array)
            {
                enumerator = array.array.GetEnumerator();
            }

            public bool MoveNext() =>
                enumerator.MoveNext();

            public void Dispose() =>
                enumerator.Dispose();

            public void GetCurrent(in V8FastResult item) =>
                item.Set(enumerator.Current);
        }
    }
}

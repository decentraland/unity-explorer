using CrdtEcsBridge.PoolsProviders;
using DCL.Diagnostics;
using Microsoft.ClearScript.Util;
using System.Collections;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules
{
    public class ScriptableByteArray : IScriptableEnumerator<byte>
    {
        public static readonly ScriptableByteArray EMPTY = new (PoolableByteArray.EMPTY);

        private readonly IEnumerator<byte> enumerator;
        private PoolableByteArray array;

        public byte Current => enumerator.Current;

        public byte ScriptableCurrent => Current;

        object IEnumerator.Current => Current;

        object IScriptableEnumerator.ScriptableCurrent => ScriptableCurrent;

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

        public bool MoveNext()
        {
            if (array.IsDisposed)
            {
                ReportHub.LogError(ReportCategory.CRDT_ECS_BRIDGE, "Trying to move next on a disposed ScriptableByteArray");
                return false;
            }

            return enumerator.MoveNext();
        }

        public void Reset()
        {
            enumerator.Reset();
        }

        public bool ScriptableMoveNext() =>
            MoveNext();

        public void ScriptableDispose()
        {
            Dispose();
        }
    }
}

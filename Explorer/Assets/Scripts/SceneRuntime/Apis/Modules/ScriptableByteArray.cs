using Microsoft.ClearScript.Util;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules
{
    public class ScriptableByteArray : IScriptableEnumerator<byte>
    {
        public static readonly ScriptableByteArray EMPTY = new (Array.Empty<byte>());
        private readonly IEnumerator<byte> enumerator;

        public byte Current => enumerator.Current;

        public byte ScriptableCurrent => Current;

        object IEnumerator.Current => Current;

        object IScriptableEnumerator.ScriptableCurrent => ScriptableCurrent;

        public ScriptableByteArray(ArraySegment<byte> array)
        {
            enumerator = array.GetEnumerator();
        }

        public void Dispose()
        {
            enumerator.Dispose();
        }

        public bool MoveNext() =>
            enumerator.MoveNext();

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

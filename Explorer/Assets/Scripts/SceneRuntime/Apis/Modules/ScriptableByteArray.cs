using Microsoft.ClearScript.Util;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules
{
    public class ScriptableByteArray : IScriptableEnumerator<byte>
    {
        private readonly IEnumerator<byte> enumerator;

        public static readonly ScriptableByteArray EMPTY = new (Array.Empty<byte>());

        public ScriptableByteArray(ArraySegment<byte> array)
        {
            enumerator = array.GetEnumerator();
        }

        public bool MoveNext() =>
            enumerator.MoveNext();

        public void Reset()
        {
            enumerator.Reset();
        }

        public byte Current => enumerator.Current;

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            enumerator.Dispose();
        }

        public bool ScriptableMoveNext() =>
            MoveNext();

        public void ScriptableDispose()
        {
            Dispose();
        }

        public byte ScriptableCurrent => Current;

        object IScriptableEnumerator.ScriptableCurrent => ScriptableCurrent;
    }
}

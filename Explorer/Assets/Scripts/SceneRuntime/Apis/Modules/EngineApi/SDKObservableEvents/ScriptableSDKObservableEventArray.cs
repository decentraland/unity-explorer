using DCL.Diagnostics;
using Microsoft.ClearScript.Util;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System.Collections;
using System.Collections.Generic;
using CrdtEcsBridge.PoolsProviders;

namespace SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents
{
    public class ScriptableSDKObservableEventArray : IScriptableEnumerator<SDKObservableEvent>
    {
        private readonly IEnumerator<SDKObservableEvent> enumerator;
        private PoolableSDKObservableEventArray array;

        public SDKObservableEvent Current => enumerator.Current;

        public SDKObservableEvent ScriptableCurrent => Current;

        object IEnumerator.Current => Current;

        object IScriptableEnumerator.ScriptableCurrent => ScriptableCurrent;

        public ScriptableSDKObservableEventArray(PoolableSDKObservableEventArray array)
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
                ReportHub.LogError(ReportCategory.CRDT_ECS_BRIDGE, "Trying to move next on a disposed ScriptableSDKObservableEventArray");
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

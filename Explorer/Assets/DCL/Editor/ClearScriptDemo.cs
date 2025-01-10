using Microsoft.ClearScript.V8;
using Microsoft.ClearScript.V8.SplitProxy;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL
{
    public static class ClearScriptDemo
    {
        [MenuItem("Demo/The Bomb")]
        private static void TheBomb()
        {
            using var engine = new V8ScriptEngine();
            engine.AddHostObject("bomb", new Bomb(Debug.Log));

            Profiler.BeginSample("The Bomb");
            engine.Execute("bomb.explode();");
            Profiler.EndSample();
        }
    }

    internal sealed class Bomb : IV8HostObject
    {
        private Action<string> log;
        private InvokeHostObject explode;

        public Bomb(Action<string> log)
        {
            this.log = log;
            explode = Explode;
        }

        private void Explode(ReadOnlySpan<V8Value.Decoded> args, V8Value result)
        {
            log("Boom!");
            result.SetNonexistent();
        }

        public void GetNamedProperty(StdString name, V8Value value, out bool isConst)
        {
            if (name.Equals(nameof(explode)))
            {
                value.SetHostObject(explode);
                isConst = true;
            }
            else
            {
                value.SetNonexistent();
                isConst = true;
            }
        }
    }
}

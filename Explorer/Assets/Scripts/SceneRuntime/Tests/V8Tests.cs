using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using NUnit.Framework;
using UnityEngine;

namespace SceneRuntime.Tests
{
    public class V8Tests
    {

        public void CallInvokeAsFunction()
        {
            V8ScriptEngine engine = V8EngineFactory.Create();

            engine.AddHostType("Debug", typeof(Debug));

            engine.Execute("function func() { Debug.Log(\"test func\") }");
            var sceneScriptObject = engine.Evaluate("func") as ScriptObject;

            sceneScriptObject!.InvokeAsFunction();
        }


        public void ConvertCSharpByteArrayToUint8Array()
        {
            V8ScriptEngine engine = V8EngineFactory.Create();

            engine.AddHostType("Assert", typeof(Assert));

            engine.Execute(@"
                function func(data) {
                    var array = new Uint8Array(data)
                    for (let i = 0; i < array.length; ++i) {
                        Assert.IsTrue(data[i] === i)
                    }
                }");

            var sceneScriptObject = engine.Evaluate("func") as ScriptObject;

            var data = new byte[10];

            for (byte i = 0; i < 10; ++i) { data[i] = i; }

            sceneScriptObject!.InvokeAsFunction(data);
        }
    }
}

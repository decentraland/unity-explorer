using DCL.Diagnostics;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using NUnit.Framework;
using System;
using UnityEngine;

namespace SceneRuntime.Tests
{
    public class V8Tests
    {
        private V8EngineFactory engineFactory;
        private V8ScriptEngine engine;

        [SetUp]
        public void SetUp()
        {
            engineFactory = new V8EngineFactory();
            engine = engineFactory.Create(new SceneShortInfo(new Vector2Int(0, 0), "test"));
        }

        [TearDown]
        public void TearDown()
        {
            engine.Dispose();
        }

        [Test]
        public void CallInvokeAsFunction()
        {
            engine.AddHostType("Debug", typeof(Debug));

            engine.Execute("function func() { Debug.Log(\"test func\") }");
            var sceneScriptObject = engine.Evaluate("func") as ScriptObject;

            sceneScriptObject!.InvokeAsFunction();
        }

        [Test]
        public void ConvertCSharpByteArrayToUint8Array()
        {
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

        [Test]
        public void DisableReflectionOnCreatedEngine()
        {
            // Assert — the factory keeps AllowReflection disabled on the scene engine.
            Assert.IsFalse(engine.AllowReflection);
        }

        [Test]
        public void BlockReflectionFromSceneScript()
        {
            // Arrange
            engine.AddHostObject("host", new ReflectionProbe());

            // Act — normal host member access is bound via UseReflectionBindFallback and must still work.
            var value = engine.Evaluate("host.Value");

            // Assert
            Assert.AreEqual(42, value);

            // Act — reflection members are not available to scene scripts.
            Exception reflectionException = Assert.Catch(() => engine.Evaluate("host.GetType()"));

            // Assert
            Assert.That(reflectionException.ToString(), Does.Contain("reflection").IgnoreCase);
        }

        private class ReflectionProbe
        {
            public int Value => 42;
        }
    }
}

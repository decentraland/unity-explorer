using System.Collections;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class V8Tests
{
    [Test]
    public void CallInvokeAsFunction()
    {
        var engine = V8EngineFactory.Create();

        engine.AddHostType("Debug", typeof(Debug));

        engine.Execute("function func() { Debug.Log(\"test func\") }");
        var sceneScriptObject = engine.Evaluate("func") as ScriptObject;

        sceneScriptObject!.InvokeAsFunction();
    }
}

using System.Collections;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.Util.Web;
using Microsoft.ClearScript.V8;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NUnit.Framework;
using System;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

public static class AwaiterExtensions
{
    public static UniTask InvokeMethodAsUniTask(this ScriptObject target, params object[] args)
    {
        UniTaskCompletionSource completionSource = new UniTaskCompletionSource();
        try
        {
            var result = target.InvokeAsFunction(args) as ScriptObject;
            Action<object> action1 = (Action<object>) (result => completionSource.TrySetResult());
            Action<object> action2 = (Action<object>) (error => completionSource.TrySetException(new Exception("Error")));
            result!.InvokeMethod("then", (object) action1, (object) action2);
        }
        catch (Exception e)
        {
            completionSource.TrySetException(e);
        }

        return completionSource.Task;
    }
}

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

    [UnityTest]
    public IEnumerator InvokeJSPromiseMethod1() => UniTask.ToCoroutine(async () =>
    {
        var code = @"
            const onUpdate = async function(dt) {};
        ";

        var engine = V8EngineFactory.Create();

        engine.Execute(code);

        UniTask AwaitJSPromiseMethod1(float dt)
        {
            Profiler.BeginSample("ExampleCall_Evaluate");
            var res = engine.Evaluate("onUpdate(0.01)");
            Profiler.EndSample();

            Profiler.BeginSample("ExampleCall_ToTask");
            var task = res.ToTask().AsUniTask();
            Profiler.EndSample();
            return task;
        }

        // hot
        await AwaitJSPromiseMethod1(0.01f);

        for (var i = 0; i < 10; ++i)
        {
            await UniTask.Yield();
            await AwaitJSPromiseMethod1(0.01f);
        }
    });

    public class UniTaskResolver
    {
        public UniTaskCompletionSource source = new UniTaskCompletionSource();

        [UsedImplicitly]
        public void Completed()
        {
            source.TrySetResult();
        }

        [UsedImplicitly]
        public void Reject(string message)
        {
            source.TrySetException(new Exception(message));
        }
    }

    [UnityTest]
    public IEnumerator InvokeJSPromiseMethod2() => UniTask.ToCoroutine(async () =>
    {
        var code = @"
            const onUpdate = async function(dt) {};
            const wrapperOnUpdate = async function(dt) {
                await onUpdate(dt)
                UniTaskResolver.Completed()
            };
        ";

        var engine = V8EngineFactory.Create();

        engine.Execute(code);

        UniTask AwaitJSPromiseMethod2(float dt)
        {
            Profiler.BeginSample("ExampleCall_UniTaskResolver");
            var unitaskResolver = new UniTaskResolver();
            engine.AddHostObject("UniTaskResolver", unitaskResolver);
            Profiler.EndSample();

            Profiler.BeginSample("ExampleCall_Evaluate");
            engine.Execute("wrapperOnUpdate(0.01)");
            Profiler.EndSample();

            return unitaskResolver.source.Task;
        }

        // hot
        await AwaitJSPromiseMethod2(0.01f);

        for (var i = 0; i < 10; ++i)
        {
            await UniTask.Yield();
            await AwaitJSPromiseMethod2(0.01f);
        }
    });


    [UnityTest]
    public IEnumerator InvokeJSPromiseMethod3() =>
        UniTask.ToCoroutine(async () =>
        {
            var engine = V8EngineFactory.Create();

            engine.Execute("async function func() {}");
            var funcCall = engine.Evaluate("func") as ScriptObject;

            // hot
            await funcCall.InvokeAsFunction().ToTask().AsUniTask();

            for (var i = 0; i < 10; ++i)
            {
                await UniTask.Yield();
                Profiler.BeginSample("ExampleCall_ToTask");
                var ut = funcCall.InvokeAsFunction().ToTask().AsUniTask();
                Profiler.EndSample();
                await ut;
            }

            // hot
            await funcCall!.InvokeMethodAsUniTask();

            for (var i = 0; i < 10; ++i)
            {
                await UniTask.Yield();
                Profiler.BeginSample("ExampleCall_ToUniTask");
                var ut = funcCall!.InvokeMethodAsUniTask();
                Profiler.EndSample();
                await ut;
            }
        });


    [UnityTest]
    public IEnumerator CreateJavaScriptArray() =>
        UniTask.ToCoroutine(async () =>
        {
            var engine = V8EngineFactory.Create();

            await UniTask.Yield();

            ITypedArray<byte> AllocUint8Array(ulong length)
            {
                return engine.Evaluate($"new Uint8Array({length})") as ITypedArray<byte>;
            }

            // hot
            AllocUint8Array(1000);

            for (int i = 0; i < 10; ++i)
            {
                await UniTask.Yield();
                Profiler.BeginSample("ReserveData");
                var array = AllocUint8Array(1000);
                Profiler.EndSample();
                Assert.IsTrue(array.Length == 1000);
            }
        });
}

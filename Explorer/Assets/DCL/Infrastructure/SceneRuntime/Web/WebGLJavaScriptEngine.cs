using AOT;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SceneRuntime.Web
{
    public class WebGLJavaScriptEngine : IJavaScriptEngine
    {
        private delegate string JSHostObjectInvokeDelegate(string contextId, string objectId, string methodName, string argsJson);
        internal readonly string contextId;
        private bool disposed;

        public IDCLScriptObject Global
        {
            get
            {
                if (disposed) throw new ObjectDisposedException(nameof(WebGLJavaScriptEngine));
                return new WebGLScriptObject(this, "globalThis");
            }
        }

        public WebGLJavaScriptEngine(string contextId)
        {
            this.contextId = contextId;
            IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(contextId);

            try
            {
                int result = JSContext_Create(contextIdPtr);

                if (result == 0)
                    throw new InvalidOperationException($"Failed to create JavaScript context {contextId}");
            }
            finally { Marshal.FreeHGlobal(contextIdPtr); }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(contextId);

                try { JSContext_Dispose(contextIdPtr); }
                finally { Marshal.FreeHGlobal(contextIdPtr); }

                HostObjectRegistry.UnregisterAll(contextId);
                disposed = true;
            }
        }

        public void Execute(string code)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebGLJavaScriptEngine));
            IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(contextId);
            IntPtr codePtr = Marshal.StringToHGlobalAnsi(code);

            try
            {
                int result = JSContext_Execute(contextIdPtr, codePtr);

                if (result == 0)
                    throw new InvalidOperationException($"Failed to execute JavaScript code in context {contextId}");
            }
            finally
            {
                Marshal.FreeHGlobal(contextIdPtr);
                Marshal.FreeHGlobal(codePtr);
            }
        }

        public ICompiledScript Compile(string code)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebGLJavaScriptEngine));
            var scriptId = Guid.NewGuid().ToString();
            IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(contextId);
            IntPtr codePtr = Marshal.StringToHGlobalAnsi(code);
            IntPtr scriptIdPtr = Marshal.StringToHGlobalAnsi(scriptId);

            try
            {
                int result = JSContext_Compile(contextIdPtr, codePtr, scriptIdPtr);

                if (result == 0)
                    throw new InvalidOperationException($"Failed to compile JavaScript code in context {contextId}");

                return new WebGLCompiledScript(scriptId);
            }
            finally
            {
                Marshal.FreeHGlobal(contextIdPtr);
                Marshal.FreeHGlobal(codePtr);
                Marshal.FreeHGlobal(scriptIdPtr);
            }
        }

        public object Evaluate(ICompiledScript script)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebGLJavaScriptEngine));

            if (script is WebGLCompiledScript webglScript) { return EvaluateScript(webglScript.ScriptId); }

            throw new ArgumentException("Script must be a WebGLCompiledScript", nameof(script));
        }

        public object Evaluate(string expression)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebGLJavaScriptEngine));
            IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(contextId);
            IntPtr exprPtr = Marshal.StringToHGlobalAnsi(expression);

            try
            {
                int bufferSize = 1024 * 64;
                IntPtr resultPtr = Marshal.AllocHGlobal(bufferSize);

                try
                {
                    int result = JSContext_Evaluate(contextIdPtr, exprPtr, resultPtr, bufferSize);

                    if (result <= 0)
                    {
                        if (result < 0)
                        {
                            bufferSize = -result;
                            Marshal.FreeHGlobal(resultPtr);
                            resultPtr = Marshal.AllocHGlobal(bufferSize);
                            result = JSContext_Evaluate(contextIdPtr, exprPtr, resultPtr, bufferSize);
                        }

                        if (result <= 0)
                            throw new InvalidOperationException($"Failed to evaluate expression in context {contextId}");
                    }

                    string resultStr = Marshal.PtrToStringAnsi(resultPtr, result);
                    return DeserializeResult(resultStr);
                }
                finally { Marshal.FreeHGlobal(resultPtr); }
            }
            finally
            {
                Marshal.FreeHGlobal(contextIdPtr);
                Marshal.FreeHGlobal(exprPtr);
            }
        }

        private object EvaluateScript(string scriptId)
        {
            IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(contextId);
            IntPtr scriptIdPtr = Marshal.StringToHGlobalAnsi(scriptId);

            try
            {
                int bufferSize = 1024 * 64;
                IntPtr resultPtr = Marshal.AllocHGlobal(bufferSize);

                try
                {
                    int result = JSContext_EvaluateScript(contextIdPtr, scriptIdPtr, resultPtr, bufferSize);

                    if (result <= 0)
                    {
                        if (result < 0)
                        {
                            bufferSize = -result;
                            Marshal.FreeHGlobal(resultPtr);
                            resultPtr = Marshal.AllocHGlobal(bufferSize);
                            result = JSContext_EvaluateScript(contextIdPtr, scriptIdPtr, resultPtr, bufferSize);
                        }

                        if (result <= 0)
                            throw new InvalidOperationException($"Failed to evaluate script {scriptId} in context {contextId}");
                    }

                    string resultStr = Marshal.PtrToStringAnsi(resultPtr, result);
                    return DeserializeResult(resultStr);
                }
                finally { Marshal.FreeHGlobal(resultPtr); }
            }
            finally
            {
                Marshal.FreeHGlobal(contextIdPtr);
                Marshal.FreeHGlobal(scriptIdPtr);
            }
        }

        public void AddHostObject(string itemName, object target)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebGLJavaScriptEngine));
            var objectId = Guid.NewGuid().ToString();
            HostObjectRegistry.Register(contextId, objectId, target);

            IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(contextId);
            IntPtr namePtr = Marshal.StringToHGlobalAnsi(itemName);
            IntPtr objectIdPtr = Marshal.StringToHGlobalAnsi(objectId);

            try
            {
                int result = JSContext_AddHostObject(contextIdPtr, namePtr, objectIdPtr);

                if (result == 0)
                    throw new InvalidOperationException($"Failed to add host object {itemName} to context {contextId}");
            }
            finally
            {
                Marshal.FreeHGlobal(contextIdPtr);
                Marshal.FreeHGlobal(namePtr);
                Marshal.FreeHGlobal(objectIdPtr);
            }
        }

        public IRuntimeHeapInfo? GetRuntimeHeapInfo() =>
            null;

        public object CreatePromiseFromTask<T>(Task<T> task)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebGLJavaScriptEngine));
            var resolver = new JSPromiseResolver<T>(task);
            var resolverId = Guid.NewGuid().ToString();
            var resolverName = $"__promiseResolver_{resolverId}";

            AddHostObject(resolverName, resolver);

            var promiseExpression = $@"
                (function() {{
                    var resolver = {resolverName};
                    return new Promise(function(resolve, reject) {{
                        var checkComplete = function() {{
                            if (resolver.IsCompleted()) {{
                                if (resolver.IsFaulted()) {{
                                    reject(new Error(resolver.GetError()));
                                }} else {{
                                    var result = resolver.GetResult();
                                    resolve(result);
                                }}
                            }} else {{
                                setTimeout(checkComplete, 0);
                            }}
                        }};
                        checkComplete();
                    }});
                }})()
            ";

            IntPtr exprPtr = Marshal.StringToHGlobalAnsi(promiseExpression);
            IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(contextId);
            IntPtr objectIdPtr = Marshal.AllocHGlobal(256);

            try
            {
                int result = JSContext_StoreObject(contextIdPtr, exprPtr, objectIdPtr, 256);

                if (result <= 0)
                    throw new InvalidOperationException($"Failed to store promise object in context {contextId}");

                string objectId = Marshal.PtrToStringAnsi(objectIdPtr, result) ?? throw new InvalidOperationException("Failed to get object ID");
                return new WebGLScriptObject(this, objectId);
            }
            finally
            {
                Marshal.FreeHGlobal(exprPtr);
                Marshal.FreeHGlobal(contextIdPtr);
                Marshal.FreeHGlobal(objectIdPtr);
            }
        }

        public object CreatePromiseFromTask(Task task)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebGLJavaScriptEngine));
            var resolver = new JSPromiseResolver(task);
            var resolverId = Guid.NewGuid().ToString();
            var resolverName = $"__promiseResolver_{resolverId}";

            AddHostObject(resolverName, resolver);

            var promiseExpression = $@"
                (function() {{
                    var resolver = {resolverName};
                    return new Promise(function(resolve, reject) {{
                        var checkComplete = function() {{
                            if (resolver.IsCompleted()) {{
                                if (resolver.IsFaulted()) {{
                                    reject(new Error(resolver.GetError()));
                                }} else {{
                                    resolve();
                                }}
                            }} else {{
                                setTimeout(checkComplete, 0);
                            }}
                        }};
                        checkComplete();
                    }});
                }})()
            ";

            IntPtr exprPtr = Marshal.StringToHGlobalAnsi(promiseExpression);
            IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(contextId);
            IntPtr objectIdPtr = Marshal.AllocHGlobal(256);

            try
            {
                int result = JSContext_StoreObject(contextIdPtr, exprPtr, objectIdPtr, 256);

                if (result <= 0)
                    throw new InvalidOperationException($"Failed to store promise object in context {contextId}");

                string objectId = Marshal.PtrToStringAnsi(objectIdPtr, result) ?? throw new InvalidOperationException("Failed to get object ID");
                return new WebGLScriptObject(this, objectId);
            }
            finally
            {
                Marshal.FreeHGlobal(exprPtr);
                Marshal.FreeHGlobal(contextIdPtr);
                Marshal.FreeHGlobal(objectIdPtr);
            }
        }

        public string GetStackTrace()
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebGLJavaScriptEngine));
            IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(contextId);
            IntPtr resultPtr = Marshal.AllocHGlobal(1024 * 16);

            try
            {
                int result = JSContext_GetStackTrace(contextIdPtr, resultPtr, 1024 * 16);

                if (result > 0)
                    return Marshal.PtrToStringAnsi(resultPtr, result);

                return string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(contextIdPtr);
                Marshal.FreeHGlobal(resultPtr);
            }
        }

        private static object? DeserializeResult(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "null")
                return null;

            try { return JsonConvert.DeserializeObject(json); }
            catch { return json; }
        }

        [DllImport("__Internal")]
        private static extern int JSContext_Create(IntPtr contextId);

        [DllImport("__Internal")]
        private static extern int JSContext_Execute(IntPtr contextId, IntPtr code);

        [DllImport("__Internal")]
        private static extern int JSContext_Compile(IntPtr contextId, IntPtr code, IntPtr scriptId);

        [DllImport("__Internal")]
        private static extern int JSContext_Evaluate(IntPtr contextId, IntPtr expression, IntPtr result, int resultSize);

        [DllImport("__Internal")]
        private static extern int JSContext_EvaluateScript(IntPtr contextId, IntPtr scriptId, IntPtr result, int resultSize);

        [DllImport("__Internal")]
        private static extern int JSContext_AddHostObject(IntPtr contextId, IntPtr name, IntPtr objectId);

        [DllImport("__Internal")]
        private static extern int JSContext_GetGlobalProperty(IntPtr contextId, IntPtr name, IntPtr result, int resultSize);

        [DllImport("__Internal")]
        private static extern int JSContext_InvokeFunction(IntPtr contextId, IntPtr functionName, IntPtr argsJson, IntPtr result, int resultSize);

        [DllImport("__Internal")]
        private static extern int JSContext_Dispose(IntPtr contextId);

        [DllImport("__Internal")]
        private static extern int JSContext_GetStackTrace(IntPtr contextId, IntPtr result, int resultSize);

        [DllImport("__Internal")]
        private static extern int JSContext_StoreObject(IntPtr contextId, IntPtr expression, IntPtr objectId, int objectIdSize);

        [MonoPInvokeCallback(typeof(JSHostObjectInvokeDelegate))]
        private static string JSHostObject_Invoke(string contextId, string objectId, string methodName, string argsJson)
        {
            object? hostObject = HostObjectRegistry.Get(contextId, objectId);

            if (hostObject == null)
            {
                Debug.LogError($"Host object with ID {objectId} not found in context {contextId}.");
                return JsonConvert.SerializeObject(null);
            }

            MethodInfo method = hostObject.GetType().GetMethod(methodName);

            if (method == null)
            {
                Debug.LogError($"Method {methodName} not found on host object {objectId}.");
                return JsonConvert.SerializeObject(null);
            }

            object[]? args = JsonConvert.DeserializeObject<object[]>(argsJson);

            try
            {
                object? result = method.Invoke(hostObject, args);
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error invoking method {methodName} on host object {objectId}: {e}");
                return JsonConvert.SerializeObject(null);
            }
        }
    }

    public class WebGLCompiledScript : ICompiledScript
    {
        public string ScriptId { get; }

        public WebGLCompiledScript(string scriptId)
        {
            ScriptId = scriptId;
        }
    }

    internal static class HostObjectRegistry
    {
        private static readonly Dictionary<string, Dictionary<string, object>> contextObjects = new ();

        public static void Register(string contextId, string objectId, object obj)
        {
            if (!contextObjects.TryGetValue(contextId, out Dictionary<string, object> objects))
            {
                objects = new Dictionary<string, object>();
                contextObjects[contextId] = objects;
            }

            objects[objectId] = obj;
        }

        public static object? Get(string contextId, string objectId)
        {
            if (contextObjects.TryGetValue(contextId, out Dictionary<string, object> objects))
            {
                objects.TryGetValue(objectId, out object? obj);
                return obj;
            }

            return null;
        }

        public static void UnregisterAll(string contextId)
        {
            contextObjects.Remove(contextId);
        }
    }

    internal class JSPromiseResolver<T>
    {
        private readonly Task<T> task;
        private T? result;
        private string? error;
        private bool isCompleted;
        private bool isFaulted;

        public JSPromiseResolver(Task<T> task)
        {
            this.task = task;

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    isFaulted = true;
                    error = t.Exception?.GetBaseException()?.Message ?? "Unknown error";
                }
                else if (t.IsCompletedSuccessfully) { result = t.Result; }

                isCompleted = true;
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        [UsedImplicitly]
        public bool IsCompleted() =>
            isCompleted;

        [UsedImplicitly]
        public bool IsFaulted() =>
            isFaulted;

        [UsedImplicitly]
        public T? GetResult() =>
            result;

        [UsedImplicitly]
        public string? GetError() =>
            error;
    }

    internal class JSPromiseResolver
    {
        private readonly Task task;
        private string? error;
        private bool isCompleted;
        private bool isFaulted;

        public JSPromiseResolver(Task task)
        {
            this.task = task;

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    isFaulted = true;
                    error = t.Exception?.GetBaseException()?.Message ?? "Unknown error";
                }

                isCompleted = true;
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        [UsedImplicitly]
        public bool IsCompleted() =>
            isCompleted;

        [UsedImplicitly]
        public bool IsFaulted() =>
            isFaulted;

        [UsedImplicitly]
        public string? GetError() =>
            error;
    }
}

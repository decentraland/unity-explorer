using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AOT;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SceneRuntime.WebClient
{
    public class WebClientJavaScriptEngine : IJavaScriptEngine
    {
        internal readonly string contextId;
        private bool disposed;
        
        // Static flag to track if callback is registered
        private static bool s_callbackRegistered;

        public IDCLScriptObject Global
        {
            get
            {
                if (disposed) throw new ObjectDisposedException(nameof(WebClientJavaScriptEngine));
                return new WebClientScriptObject(this, "globalThis");
            }
        }

        public WebClientJavaScriptEngine(string contextId)
        {
            this.contextId = contextId;
            
            // Register the callback once on first engine creation
            if (!s_callbackRegistered)
            {
                JSContext_RegisterHostObjectCallback(InvokeHostObjectMethod);
                s_callbackRegistered = true;
            }
            
            IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(contextId);

            try
            {
                int result = JSContext_Create(contextIdPtr);

                if (result == 0)
                    throw new InvalidOperationException($"Failed to create JavaScript context {contextId}");
            }
            finally { Marshal.FreeHGlobal(contextIdPtr); }
        }
        
        // Delegate type for the callback
        private delegate void HostObjectCallbackDelegate(IntPtr contextId, IntPtr objectId, IntPtr methodName, IntPtr argsJson);
        
        /// <summary>
        /// Static callback that JavaScript can invoke when it needs to call a method on a registered host object.
        /// </summary>
        [MonoPInvokeCallback(typeof(HostObjectCallbackDelegate))]
        private static void InvokeHostObjectMethod(IntPtr contextIdPtr, IntPtr objectIdPtr, IntPtr methodNamePtr, IntPtr argsJsonPtr)
        {
            try
            {
                string contextId = Utf8Marshal.PtrToStringUTF8(contextIdPtr);
                string objectId = Utf8Marshal.PtrToStringUTF8(objectIdPtr);
                string methodName = Utf8Marshal.PtrToStringUTF8(methodNamePtr);
                string argsJson = Utf8Marshal.PtrToStringUTF8(argsJsonPtr);
                
                object? hostObject = WebClientHostObjectRegistry.Get(contextId, objectId);
                if (hostObject == null)
                {
                    Debug.LogWarning($"[WebClientJavaScriptEngine] Host object not found: contextId={contextId}, objectId={objectId}");
                    return;
                }
                
                // Find and invoke the method
                MethodInfo? method = hostObject.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                if (method == null)
                {
                    Debug.LogWarning($"[WebClientJavaScriptEngine] Method not found: {methodName} on {hostObject.GetType().Name}");
                    return;
                }
                
                // Parse arguments
                object?[]? args = null;
                if (!string.IsNullOrEmpty(argsJson) && argsJson != "[]")
                {
                    args = JsonConvert.DeserializeObject<object?[]>(argsJson);
                }
                
                // Invoke the method
                method.Invoke(hostObject, args);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebClientJavaScriptEngine] Error invoking host object method: {ex}");
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(contextId);

                try { JSContext_Dispose(contextIdPtr); }
                finally { Marshal.FreeHGlobal(contextIdPtr); }

                WebClientHostObjectRegistry.UnregisterAll(contextId);
                disposed = true;
            }
        }

        public void Execute(string code)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebClientJavaScriptEngine));
            IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(contextId);
            IntPtr codePtr = Utf8Marshal.StringToHGlobalUTF8(code);

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
            if (disposed) throw new ObjectDisposedException(nameof(WebClientJavaScriptEngine));
            var scriptId = Guid.NewGuid().ToString();
            IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(contextId);
            IntPtr codePtr = Utf8Marshal.StringToHGlobalUTF8(code);
            IntPtr scriptIdPtr = Utf8Marshal.StringToHGlobalUTF8(scriptId);

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
            if (disposed) throw new ObjectDisposedException(nameof(WebClientJavaScriptEngine));

            if (script is WebGLCompiledScript webglScript) { return EvaluateScript(webglScript.ScriptId); }

            throw new ArgumentException("Script must be a WebGLCompiledScript", nameof(script));
        }

        public object Evaluate(string expression)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebClientJavaScriptEngine));
            IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(contextId);
            IntPtr exprPtr = Utf8Marshal.StringToHGlobalUTF8(expression);

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

                    string resultStr = Utf8Marshal.PtrToStringUTF8(resultPtr, result);
                    return DeserializeResultWithObjectRef(resultStr);
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
            IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(contextId);
            IntPtr scriptIdPtr = Utf8Marshal.StringToHGlobalUTF8(scriptId);

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

                    string resultStr = Utf8Marshal.PtrToStringUTF8(resultPtr, result);
                    return DeserializeResultWithObjectRef(resultStr);
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
            if (disposed) throw new ObjectDisposedException(nameof(WebClientJavaScriptEngine));
            var objectId = Guid.NewGuid().ToString();
            WebClientHostObjectRegistry.Register(contextId, objectId, target);

            IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(contextId);
            IntPtr namePtr = Utf8Marshal.StringToHGlobalUTF8(itemName);
            IntPtr objectIdPtr = Utf8Marshal.StringToHGlobalUTF8(objectId);

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

        /// <summary>
        /// Registers a module with its compiled script ID so it can be looked up by name in JavaScript.
        /// This must be called before executing Init.js for all modules that will be required.
        /// </summary>
        public void RegisterModule(string moduleName, string scriptId)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebClientJavaScriptEngine));
            
            IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(contextId);
            IntPtr moduleNamePtr = Utf8Marshal.StringToHGlobalUTF8(moduleName);
            IntPtr scriptIdPtr = Utf8Marshal.StringToHGlobalUTF8(scriptId);

            try
            {
                int result = JSContext_RegisterModule(contextIdPtr, moduleNamePtr, scriptIdPtr);

                if (result == 0)
                    throw new InvalidOperationException($"Failed to register module {moduleName} in context {contextId}");
            }
            finally
            {
                Marshal.FreeHGlobal(contextIdPtr);
                Marshal.FreeHGlobal(moduleNamePtr);
                Marshal.FreeHGlobal(scriptIdPtr);
            }
        }

        public IRuntimeHeapInfo? GetRuntimeHeapInfo() =>
            null;

        public object CreatePromiseFromTask<T>(Task<T> task)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebClientJavaScriptEngine));
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

            IntPtr exprPtr = Utf8Marshal.StringToHGlobalUTF8(promiseExpression);
            IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(contextId);
            IntPtr objectIdPtr = Marshal.AllocHGlobal(256);

            try
            {
                int result = JSContext_StoreObject(contextIdPtr, exprPtr, objectIdPtr, 256);

                if (result <= 0)
                    throw new InvalidOperationException($"Failed to store promise object in context {contextId}");

                string objectId = Utf8Marshal.PtrToStringUTF8(objectIdPtr, result);
                if (string.IsNullOrEmpty(objectId))
                    throw new InvalidOperationException("Failed to get object ID");
                return new WebClientScriptObject(this, objectId);
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
            if (disposed) throw new ObjectDisposedException(nameof(WebClientJavaScriptEngine));
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

            IntPtr exprPtr = Utf8Marshal.StringToHGlobalUTF8(promiseExpression);
            IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(contextId);
            IntPtr objectIdPtr = Marshal.AllocHGlobal(256);

            try
            {
                int result = JSContext_StoreObject(contextIdPtr, exprPtr, objectIdPtr, 256);

                if (result <= 0)
                    throw new InvalidOperationException($"Failed to store promise object in context {contextId}");

                string objectId = Utf8Marshal.PtrToStringUTF8(objectIdPtr, result);
                if (string.IsNullOrEmpty(objectId))
                    throw new InvalidOperationException("Failed to get object ID");
                return new WebClientScriptObject(this, objectId);
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
            if (disposed) throw new ObjectDisposedException(nameof(WebClientJavaScriptEngine));
            IntPtr contextIdPtr = Utf8Marshal.StringToHGlobalUTF8(contextId);
            IntPtr resultPtr = Marshal.AllocHGlobal(1024 * 16);

            try
            {
                int result = JSContext_GetStackTrace(contextIdPtr, resultPtr, 1024 * 16);

                if (result > 0)
                    return Utf8Marshal.PtrToStringUTF8(resultPtr, result);

                return string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(contextIdPtr);
                Marshal.FreeHGlobal(resultPtr);
            }
        }


        /// <summary>
        /// Deserializes a JSON result, handling special __objectRef format for non-serializable JS objects.
        /// </summary>
        private object? DeserializeResultWithObjectRef(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "null")
                return null;

            try
            {
                // Check if this is an object reference (for functions, objects that can't be JSON-serialized)
                var jObject = JObject.Parse(json);
                if (jObject.TryGetValue("__objectRef", out JToken? objectRefToken))
                {
                    string objectId = objectRefToken.Value<string>()!;
                    return new WebClientScriptObject(this, objectId);
                }
                
                // Regular JSON deserialization
                return JsonConvert.DeserializeObject(json);
            }
            catch
            {
                // If parsing fails, try simple deserialization or return the raw string
                try { return JsonConvert.DeserializeObject(json); }
                catch { return json; }
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
        private static extern int JSContext_RegisterModule(IntPtr contextId, IntPtr moduleName, IntPtr scriptId);

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
        
        [DllImport("__Internal")]
        private static extern void JSContext_RegisterHostObjectCallback(HostObjectCallbackDelegate callback);
    }

    public class WebGLCompiledScript : ICompiledScript
    {
        public string ScriptId { get; }

        public WebGLCompiledScript(string scriptId)
        {
            ScriptId = scriptId;
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
        public bool IsCompleted() => isCompleted;

        [UsedImplicitly]
        public bool IsFaulted() => isFaulted;

        [UsedImplicitly]
        public T? GetResult() => result;

        [UsedImplicitly]
        public string? GetError() => error;
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
        public bool IsCompleted() => isCompleted;

        [UsedImplicitly]
        public bool IsFaulted() => isFaulted;

        [UsedImplicitly]
        public string? GetError() => error;
    }
}

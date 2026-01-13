using System;
using System.Runtime.InteropServices;

namespace SceneRuntime.Web
{
    public class WebGLJavaScriptEngine : IJavaScriptEngine
    {
        internal readonly string contextId;
        private bool disposed;

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
            finally
            {
                Marshal.FreeHGlobal(contextIdPtr);
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
            if (script is WebGLCompiledScript webglScript)
            {
                return EvaluateScript(webglScript.ScriptId);
            }
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
                finally
                {
                    Marshal.FreeHGlobal(resultPtr);
                }
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
                finally
                {
                    Marshal.FreeHGlobal(resultPtr);
                }
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

        public IDCLScriptObject Global
        {
            get
            {
                if (disposed) throw new ObjectDisposedException(nameof(WebGLJavaScriptEngine));
                return new WebGLScriptObject(this, "globalThis");
            }
        }

        public IRuntimeHeapInfo? GetRuntimeHeapInfo() =>
            null;

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

        public void Dispose()
        {
            if (!disposed)
            {
                IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(contextId);
                try
                {
                    JSContext_Dispose(contextIdPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(contextIdPtr);
                }
                HostObjectRegistry.UnregisterAll(contextId);
                disposed = true;
            }
        }

        private static object? DeserializeResult(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "null")
                return null;
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject(json);
            }
            catch
            {
                return json;
            }
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

        [AOT.MonoPInvokeCallback(typeof(JSHostObjectInvokeDelegate))]
        private static string JSHostObject_Invoke(string contextId, string objectId, string methodName, string argsJson)
        {
            object? hostObject = HostObjectRegistry.Get(contextId, objectId);
            if (hostObject == null)
            {
                UnityEngine.Debug.LogError($"Host object with ID {objectId} not found in context {contextId}.");
                return Newtonsoft.Json.JsonConvert.SerializeObject(null);
            }

            var method = hostObject.GetType().GetMethod(methodName);
            if (method == null)
            {
                UnityEngine.Debug.LogError($"Method {methodName} not found on host object {objectId}.");
                return Newtonsoft.Json.JsonConvert.SerializeObject(null);
            }

            object[]? args = Newtonsoft.Json.JsonConvert.DeserializeObject<object[]>(argsJson);
            try
            {
                object? result = method.Invoke(hostObject, args);
                return Newtonsoft.Json.JsonConvert.SerializeObject(result);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Error invoking method {methodName} on host object {objectId}: {e}");
                return Newtonsoft.Json.JsonConvert.SerializeObject(null);
            }
        }

        private delegate string JSHostObjectInvokeDelegate(string contextId, string objectId, string methodName, string argsJson);
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
        private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, object>> contextObjects = new();

        public static void Register(string contextId, string objectId, object obj)
        {
            if (!contextObjects.TryGetValue(contextId, out var objects))
            {
                objects = new System.Collections.Generic.Dictionary<string, object>();
                contextObjects[contextId] = objects;
            }
            objects[objectId] = obj;
        }

        public static object? Get(string contextId, string objectId)
        {
            if (contextObjects.TryGetValue(contextId, out var objects))
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
}

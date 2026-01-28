using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AOT;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SceneRuntime.WebClient
{
    public class WebClientJavaScriptEngine : IJavaScriptEngine
    {
        // Delegate type for the void callback (used for methods like Completed/Reject)
        private delegate void HostObjectCallbackDelegate(IntPtr contextId, IntPtr objectId, IntPtr methodName, IntPtr argsJson);

        // Delegate type for the callback that returns values
        private delegate int HostObjectCallbackWithReturnDelegate(IntPtr contextId, IntPtr objectId, IntPtr methodName, IntPtr argsJson, IntPtr resultBuffer,
            int resultBufferSize);

        // Static flag to track if callback is registered
        private static bool s_callbackRegistered;
        internal readonly string contextId;
        private bool disposed;

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

            // Register the callbacks once on first engine creation
            if (!s_callbackRegistered)
            {
                JSContext_RegisterHostObjectCallback(InvokeHostObjectMethod);
                JSContext_RegisterHostObjectCallbackWithReturn(InvokeHostObjectMethodWithReturn);
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

        /// <summary>
        ///     Finds a method by name, handling overloads by matching argument count.
        ///     Uses GetMethods() to avoid AmbiguousMatchException from GetMethod().
        /// </summary>
        private static MethodInfo? FindMethod(Type type, string methodName, int argCount)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            MethodInfo? exactMatch = null;
            MethodInfo? zeroParamFallback = null;

            foreach (var method in methods)
            {
                if (method.Name != methodName)
                    continue;

                var parameters = method.GetParameters();

                // Exact match on parameter count - return immediately
                if (parameters.Length == argCount)
                    return method;

                // Keep zero-param method as fallback only if we're looking for zero args
                // This handles cases where JS passes empty array but method has no params
                if (parameters.Length == 0 && zeroParamFallback == null)
                    zeroParamFallback = method;

                // Keep first exact match by name
                if (exactMatch == null)
                    exactMatch = method;
            }

            // Only return fallback if arg count is 0 and we have a zero-param method
            if (argCount == 0 && zeroParamFallback != null)
                return zeroParamFallback;

            // If we have a method by name but wrong param count, log and return it
            // The caller will get a more helpful error
            if (exactMatch != null)
            {
                Debug.LogWarning($"[WebClientJavaScriptEngine] Method {methodName} found but param count mismatch. Expected: {exactMatch.GetParameters().Length}, Got: {argCount}");
            }

            return exactMatch;
        }

        /// <summary>
        ///     Static callback that JavaScript can invoke when it needs to call a method on a registered host object (void return).
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

                // Parse arguments first to determine count
                object?[]? args = null;
                int argCount = 0;

                if (!string.IsNullOrEmpty(argsJson) && argsJson != "[]")
                {
                    args = JsonConvert.DeserializeObject<object?[]>(argsJson);
                    argCount = args?.Length ?? 0;
                }

                // Find method handling overloads
                MethodInfo? method = FindMethod(hostObject.GetType(), methodName, argCount);

                if (method == null)
                {
                    Debug.LogWarning($"[WebClientJavaScriptEngine] Method not found: {methodName} on {hostObject.GetType().Name}");
                    return;
                }

                // Invoke the method
                method.Invoke(hostObject, args);
            }
            catch (Exception ex) { Debug.LogError($"[WebClientJavaScriptEngine] Error invoking host object method: {ex}"); }
        }

        /// <summary>
        ///     Static callback that JavaScript can invoke when it needs to call a method and get a return value.
        ///     Returns the length of the result written to the buffer, or negative if buffer too small.
        /// </summary>
        [MonoPInvokeCallback(typeof(HostObjectCallbackWithReturnDelegate))]
        private static int InvokeHostObjectMethodWithReturn(IntPtr contextIdPtr, IntPtr objectIdPtr, IntPtr methodNamePtr, IntPtr argsJsonPtr, IntPtr resultBuffer,
            int resultBufferSize)
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
                    return WriteResultToBuffer("{\"error\":\"Host object not found\"}", resultBuffer, resultBufferSize);
                }

                // Pre-parse arguments to determine count for method resolution
                int argCount = 0;
                JArray? jsonArray = null;

                if (!string.IsNullOrEmpty(argsJson) && argsJson != "[]")
                {
                    jsonArray = JArray.Parse(argsJson);
                    argCount = jsonArray.Count;
                }

                // Find method handling overloads
                MethodInfo? method = FindMethod(hostObject.GetType(), methodName, argCount);

                if (method == null)
                {
                    Debug.LogWarning($"[WebClientJavaScriptEngine] Method not found: {methodName} on {hostObject.GetType().Name}");
                    return WriteResultToBuffer("{\"error\":\"Method not found\"}", resultBuffer, resultBufferSize);
                }

                // Parse arguments and convert to method parameter types
                object?[]? args = ParseAndConvertArguments(argsJson, method.GetParameters());

                // Invoke the method
                object? result = method.Invoke(hostObject, args);

                // Serialize the result
                string serializedResult = SerializeResult(result);

                return WriteResultToBuffer(serializedResult, resultBuffer, resultBufferSize);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebClientJavaScriptEngine] Error invoking host object method with return: {ex}");
                string errorJson = JsonConvert.SerializeObject(new { error = ex.Message });
                return WriteResultToBuffer(errorJson, resultBuffer, resultBufferSize);
            }
        }

        /// <summary>
        ///     Parses JSON arguments and converts them to the expected parameter types.
        /// </summary>
        private static object?[]? ParseAndConvertArguments(string argsJson, ParameterInfo[] parameters)
        {
            // No parameters expected - return null (not empty array) for proper invocation
            if (parameters.Length == 0)
                return null;

            // Empty args but parameters expected - return array with default values
            if (string.IsNullOrEmpty(argsJson) || argsJson == "[]")
            {
                var defaultArgs = new object?[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;
                    defaultArgs[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                }
                return defaultArgs;
            }

            try
            {
                var jsonArray = JArray.Parse(argsJson);
                var args = new object?[parameters.Length];

                for (var i = 0; i < parameters.Length; i++)
                {
                    ParameterInfo param = parameters[i];

                    // If we have a JSON value for this parameter
                    if (i < jsonArray.Count)
                    {
                        JToken? jsonValue = jsonArray[i];

                        if (jsonValue == null || jsonValue.Type == JTokenType.Null)
                        {
                            args[i] = null;
                        }
                        else if (jsonValue is JObject jObj && jObj.TryGetValue("__type", out JToken? typeToken) && typeToken.Value<string>() == "ByteArray")
                        {
                            // Handle ByteArray type marker - convert Base64 to byte array wrapper
                            string base64Data = jObj.Value<string>("data") ?? "";
                            byte[] bytes = string.IsNullOrEmpty(base64Data) ? Array.Empty<byte>() : Convert.FromBase64String(base64Data);
                            args[i] = new WebClientByteArrayWrapper(bytes);
                        }
                        else
                        {
                            // Special handling for IList<object> - JSON.NET can't instantiate interfaces
                            if (param.ParameterType == typeof(IList<object>) && jsonValue is JArray jArr)
                            {
                                args[i] = jArr.ToObject<List<object>>();
                            }
                            else
                            {
                                // Convert the JSON value to the expected parameter type
                                args[i] = jsonValue.ToObject(param.ParameterType);
                            }
                        }
                    }
                    else
                    {
                        // No JSON value for this parameter - use default
                        var paramType = param.ParameterType;
                        args[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                    }
                }

                return args;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WebClientJavaScriptEngine] Error parsing arguments: {ex.Message}\nArgsJson: {argsJson}\nParameters: {string.Join(", ", parameters.Select(p => p.ParameterType.Name))}\nStack: {ex.StackTrace}");
                // Return array with defaults rather than null to avoid invoke errors
                var fallbackArgs = new object?[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;
                    fallbackArgs[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                }
                return fallbackArgs;
            }
        }

        /// <summary>
        ///     Serializes a method return value to JSON, with special handling for certain types.
        /// </summary>
        private static string SerializeResult(object? result)
        {
            // Debug.Log($"[SerializeResult] Input type: {result?.GetType().Name ?? "null"}");

            if (result == null)
                return "null";

            // Handle PoolableByteArray - serialize as Base64 with type marker
            if (result is PoolableByteArray poolableByteArray)
            {
                // Debug.Log("[SerializeResult] Serializing PoolableByteArray");
                if (poolableByteArray.IsEmpty) { return JsonConvert.SerializeObject(new { __type = "ByteArray", data = "", isEmpty = true }); }

                byte[] bytes = poolableByteArray.Memory.ToArray();
                string base64 = Convert.ToBase64String(bytes);
                return JsonConvert.SerializeObject(new { __type = "ByteArray", data = base64, isEmpty = false });
            }

            // Handle primitive types directly
            if (result is bool boolResult)
            {
                // Debug.Log("[SerializeResult] Serializing bool");
                return boolResult ? "true" : "false";
            }

            if (result is int or long or float or double or decimal)
            {
                // Debug.Log("[SerializeResult] Serializing number");
                return result.ToString()!;
            }

            if (result is string stringResult)
            {
                string serialized = JsonConvert.SerializeObject(stringResult);

                // Debug.Log($"[SerializeResult] Serializing string, length={stringResult.Length}, serialized={serialized.Substring(0, Math.Min(100, serialized.Length))}...");
                return serialized;
            }

            // Handle WebClientScriptObject - return the object ID reference
            if (result is WebClientScriptObject scriptObject)
            {
                // Debug.Log($"[SerializeResult] Serializing WebClientScriptObject: {scriptObject.ObjectId}");
                return JsonConvert.SerializeObject(new { __objectRef = scriptObject.ObjectId });
            }

            // Default: JSON serialize the object
            // Debug.Log($"[SerializeResult] Default serialization for: {result.GetType().Name}");
            try { return JsonConvert.SerializeObject(result); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WebClientJavaScriptEngine] Error serializing result: {ex.Message}");
                return JsonConvert.SerializeObject(new { __type = "Object", toString = result.ToString() });
            }
        }

        /// <summary>
        ///     Writes a UTF8 string to the result buffer.
        ///     Returns the number of bytes written, or negative of required size if buffer too small.
        /// </summary>
        private static unsafe int WriteResultToBuffer(string result, IntPtr buffer, int bufferSize)
        {
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(result);
            int requiredSize = utf8Bytes.Length + 1; // +1 for null terminator

            if (bufferSize < requiredSize)
                return -requiredSize;

            Marshal.Copy(utf8Bytes, 0, buffer, utf8Bytes.Length);
            ((byte*)buffer.ToPointer())[utf8Bytes.Length] = 0; // Null terminator

            return utf8Bytes.Length;
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
                int bufferSize = 1024 * 128; // Increased to 128KB to handle large CRDT state responses
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
                int bufferSize = 1024 * 128; // Increased to 128KB to handle large CRDT state responses
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
        ///     Registers a module with its compiled script ID so it can be looked up by name in JavaScript.
        ///     This must be called before executing Init.js for all modules that will be required.
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

        public object CreatePromise<T>(UniTask<T> uniTask)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebClientJavaScriptEngine));
            var resolver = new JSUniTaskPromiseResolver<T>(uniTask);
            var resolverId = Guid.NewGuid().ToString("N"); // "N" format = no dashes, valid JS identifier
            var resolverName = $"__promiseResolver_{resolverId}";

            AddHostObject(resolverName, resolver);

            // IMPORTANT: Expression must start with '(' immediately (no leading whitespace/newline)
            // to avoid JavaScript ASI treating 'return\n(...)' as 'return; (...)'
            var promiseExpression = $"(function(){{var resolver=globalThis['{resolverName}'];if(!resolver){{return Promise.reject(new Error('Resolver not found'));}}return new Promise(function(resolve,reject){{var checkComplete=function(){{if(resolver.IsCompleted()){{if(resolver.IsFaulted()){{reject(new Error(resolver.GetError()));}}else{{resolve(resolver.GetResult());}}}}else{{setTimeout(checkComplete,0);}}}};checkComplete();}});}})()";

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

        public object CreatePromise(UniTask uniTask)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WebClientJavaScriptEngine));
            var resolver = new JSUniTaskPromiseResolver(uniTask);
            var resolverId = Guid.NewGuid().ToString("N"); // "N" format = no dashes, valid JS identifier
            var resolverName = $"__promiseResolver_{resolverId}";

            AddHostObject(resolverName, resolver);

            // IMPORTANT: Expression must start with '(' immediately (no leading whitespace/newline)
            // to avoid JavaScript ASI treating 'return\n(...)' as 'return; (...)'
            var promiseExpression = $"(function(){{var resolver=globalThis['{resolverName}'];if(!resolver){{return Promise.reject(new Error('Resolver not found'));}}return new Promise(function(resolve,reject){{var checkComplete=function(){{if(resolver.IsCompleted()){{if(resolver.IsFaulted()){{reject(new Error(resolver.GetError()));}}else{{resolve();}}}}else{{setTimeout(checkComplete,0);}}}};checkComplete();}});}})()";

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
        ///     Deserializes a JSON result, handling special __objectRef format for non-serializable JS objects.
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

        [DllImport("__Internal")]
        private static extern void JSContext_RegisterHostObjectCallbackWithReturn(HostObjectCallbackWithReturnDelegate callback);
    }

    public class WebGLCompiledScript : ICompiledScript
    {
        public string ScriptId { get; }

        public WebGLCompiledScript(string scriptId)
        {
            ScriptId = scriptId;
        }
    }

    internal class JSUniTaskPromiseResolver<T>
    {
        private T? result;
        private string? error;
        private bool isCompleted;
        private bool isFaulted;

        public JSUniTaskPromiseResolver(UniTask<T> uniTask)
        {
            RunAsync(uniTask).Forget();
        }

        private async UniTaskVoid RunAsync(UniTask<T> uniTask)
        {
            try { result = await uniTask; }
            catch (Exception e)
            {
                isFaulted = true;
                error = e.Message;
            }
            finally { isCompleted = true; }
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

    internal class JSUniTaskPromiseResolver
    {
        private string? error;
        private bool isCompleted;
        private bool isFaulted;

        public JSUniTaskPromiseResolver(UniTask uniTask)
        {
            RunAsync(uniTask).Forget();
        }

        private async UniTaskVoid RunAsync(UniTask uniTask)
        {
            try { await uniTask; }
            catch (Exception e)
            {
                isFaulted = true;
                error = e.Message;
            }
            finally { isCompleted = true; }
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

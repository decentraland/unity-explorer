using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Utility;

namespace SceneRuntime.Web
{
    public class WebGLScriptObject : IDCLScriptObject
    {
        private readonly WebGLJavaScriptEngine engine;
        private readonly string propertyPath;

        public IEnumerable<string> PropertyNames => Array.Empty<string>();

        public object this[string name, params object[] args]
        {
            get
            {
                if (args.Length > 0)
                    throw new NotImplementedException("Indexer get with args not supported");

                return GetProperty(name);
            }

            set
            {
                if (args.Length > 0)
                    throw new NotImplementedException("Indexer set with args not supported");

                SetProperty(name, value);
            }
        }

        public WebGLScriptObject(WebGLJavaScriptEngine engine, string propertyPath)
        {
            this.engine = engine;
            this.propertyPath = propertyPath;
        }

        public object InvokeMethod(string name, params object[] args)
        {
            if (!IsObjectId(propertyPath))
                throw new InvalidOperationException($"Cannot invoke method on non-object path: {propertyPath}");

            string argsJson = JsonConvert.SerializeObject(args);
            IntPtr objectIdPtr = Marshal.StringToHGlobalAnsi(propertyPath);
            IntPtr methodNamePtr = Marshal.StringToHGlobalAnsi(name);
            IntPtr argsJsonPtr = Marshal.StringToHGlobalAnsi(argsJson);

            try
            {
                int bufferSize = 1024 * 64;
                IntPtr resultPtr = Marshal.AllocHGlobal(bufferSize);

                try
                {
                    IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(engine.contextId);

                    try
                    {
                        int result = JSContext_InvokeObjectMethod(contextIdPtr, objectIdPtr, methodNamePtr, argsJsonPtr, resultPtr, bufferSize);

                        if (result > 0)
                        {
                            string resultStr = Marshal.PtrToStringAnsi(resultPtr, result);
                            object deserialized = JsonConvert.DeserializeObject(resultStr);

                            if (deserialized is string str && IsObjectId(str))
                                return new WebGLScriptObject(engine, str);

                            return deserialized;
                        }

                        throw new InvalidOperationException($"Failed to invoke method {name} on object {propertyPath}");
                    }
                    finally { Marshal.FreeHGlobal(contextIdPtr); }
                }
                finally { Marshal.FreeHGlobal(resultPtr); }
            }
            finally
            {
                Marshal.FreeHGlobal(objectIdPtr);
                Marshal.FreeHGlobal(methodNamePtr);
                Marshal.FreeHGlobal(argsJsonPtr);
            }
        }

        public object InvokeAsFunction(params object[] args)
        {
            string argsJson = JsonConvert.SerializeObject(args);

            if (IsObjectId(propertyPath))
            {
                IntPtr objectIdPtr = Marshal.StringToHGlobalAnsi(propertyPath);
                IntPtr argsJsonPtr = Marshal.StringToHGlobalAnsi(argsJson);

                try
                {
                    int bufferSize = 1024 * 64;
                    IntPtr resultPtr = Marshal.AllocHGlobal(bufferSize);

                    try
                    {
                        IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(engine.contextId);

                        try
                        {
                            int result = JSContext_InvokeObjectAsFunction(contextIdPtr, objectIdPtr, argsJsonPtr, resultPtr, bufferSize);

                            if (result > 0)
                            {
                                string resultStr = Marshal.PtrToStringAnsi(resultPtr, result);
                                return JsonConvert.DeserializeObject(resultStr);
                            }

                            throw new InvalidOperationException($"Object {propertyPath} is not callable as a function");
                        }
                        finally { Marshal.FreeHGlobal(contextIdPtr); }
                    }
                    finally { Marshal.FreeHGlobal(resultPtr); }
                }
                finally
                {
                    Marshal.FreeHGlobal(objectIdPtr);
                    Marshal.FreeHGlobal(argsJsonPtr);
                }
            }
            else
            {
                IntPtr funcNamePtr = Marshal.StringToHGlobalAnsi(propertyPath);
                IntPtr argsJsonPtr = Marshal.StringToHGlobalAnsi(argsJson);

                try
                {
                    int bufferSize = 1024 * 64;
                    IntPtr resultPtr = Marshal.AllocHGlobal(bufferSize);

                    try
                    {
                        IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(engine.contextId);

                        try
                        {
                            int result = JSContext_InvokeFunction(contextIdPtr, funcNamePtr, argsJsonPtr, resultPtr, bufferSize);

                            if (result > 0)
                            {
                                string resultStr = Marshal.PtrToStringAnsi(resultPtr, result);
                                return JsonConvert.DeserializeObject(resultStr);
                            }

                            return null;
                        }
                        finally { Marshal.FreeHGlobal(contextIdPtr); }
                    }
                    finally { Marshal.FreeHGlobal(resultPtr); }
                }
                finally
                {
                    Marshal.FreeHGlobal(funcNamePtr);
                    Marshal.FreeHGlobal(argsJsonPtr);
                }
            }
        }

        public object GetProperty(string name)
        {
            if (IsObjectId(propertyPath))
            {
                IntPtr objectIdPtr = Marshal.StringToHGlobalAnsi(propertyPath);
                IntPtr namePtr = Marshal.StringToHGlobalAnsi(name);

                try
                {
                    int bufferSize = 1024 * 64;
                    IntPtr resultPtr = Marshal.AllocHGlobal(bufferSize);

                    try
                    {
                        IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(engine.contextId);

                        try
                        {
                            int result = JSContext_GetObjectProperty(contextIdPtr, objectIdPtr, namePtr, resultPtr, bufferSize);

                            if (result > 0)
                            {
                                string resultStr = Marshal.PtrToStringAnsi(resultPtr, result);
                                return JsonConvert.DeserializeObject(resultStr);
                            }

                            return null;
                        }
                        finally { Marshal.FreeHGlobal(contextIdPtr); }
                    }
                    finally { Marshal.FreeHGlobal(resultPtr); }
                }
                finally
                {
                    Marshal.FreeHGlobal(objectIdPtr);
                    Marshal.FreeHGlobal(namePtr);
                }
            }

            string fullPath = string.IsNullOrEmpty(propertyPath) ? name : $"{propertyPath}.{name}";
            return engine.Evaluate(fullPath);
        }

        public void SetProperty(string name, object value)
        {
            if (IsObjectId(propertyPath))
            {
                string valueJson = JsonConvert.SerializeObject(value);
                IntPtr objectIdPtr = Marshal.StringToHGlobalAnsi(propertyPath);
                IntPtr namePtr = Marshal.StringToHGlobalAnsi(name);
                IntPtr valueJsonPtr = Marshal.StringToHGlobalAnsi(valueJson);

                try
                {
                    IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(engine.contextId);

                    try
                    {
                        int result = JSContext_SetObjectProperty(contextIdPtr, objectIdPtr, namePtr, valueJsonPtr);

                        if (result == 0)
                            throw new InvalidOperationException($"Failed to set property {name} on object {propertyPath}");
                    }
                    finally { Marshal.FreeHGlobal(contextIdPtr); }
                }
                finally
                {
                    Marshal.FreeHGlobal(objectIdPtr);
                    Marshal.FreeHGlobal(namePtr);
                    Marshal.FreeHGlobal(valueJsonPtr);
                }
            }
            else
            {
                string fullPath = string.IsNullOrEmpty(propertyPath) ? name : $"{propertyPath}.{name}";
                string valueJson = JsonConvert.SerializeObject(value);
                engine.Execute($"{fullPath} = {valueJson};");
            }
        }

        public object GetProperty(string name, params object[] args)
        {
            if (args.Length > 0)
                throw new NotImplementedException("GetProperty with args not supported");

            return GetProperty(name);
        }

        public void SetProperty(string name, params object[] args)
        {
            if (args.Length == 0)
                throw new ArgumentException("SetProperty requires at least one argument (the value)", nameof(args));

            object value = args[args.Length - 1];
            SetProperty(name, value);
        }

        public void SetProperty(int index, object value)
        {
            throw new NotImplementedException();
        }

        private static bool IsObjectId(string path) =>
            !string.IsNullOrEmpty(path) && path.StartsWith("__obj_");

        public object Invoke(bool asConstructor, params object[] args)
        {
            if (asConstructor)
            {
                string argsJson = JsonConvert.SerializeObject(args);
                var newExpr = $"new {propertyPath}(...{argsJson})";

                IntPtr exprPtr = Marshal.StringToHGlobalAnsi(newExpr);

                try
                {
                    var bufferSize = 256;
                    IntPtr objectIdPtr = Marshal.AllocHGlobal(bufferSize);

                    try
                    {
                        IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(engine.contextId);

                        try
                        {
                            int result = JSContext_StoreObject(contextIdPtr, exprPtr, objectIdPtr, bufferSize);

                            if (result <= 0)
                            {
                                if (result < 0)
                                {
                                    bufferSize = -result;
                                    Marshal.FreeHGlobal(objectIdPtr);
                                    objectIdPtr = Marshal.AllocHGlobal(bufferSize);
                                    result = JSContext_StoreObject(contextIdPtr, exprPtr, objectIdPtr, bufferSize);
                                }

                                if (result <= 0)
                                    throw new InvalidOperationException($"Failed to create object instance using {propertyPath}");
                            }

                            string objectId = Marshal.PtrToStringAnsi(objectIdPtr, result);
                            return new WebGLScriptObject(engine, objectId);
                        }
                        finally { Marshal.FreeHGlobal(contextIdPtr); }
                    }
                    finally { Marshal.FreeHGlobal(objectIdPtr); }
                }
                finally { Marshal.FreeHGlobal(exprPtr); }
            }

            return this;
        }

        public void SetProperty(int index, IDCLTypedArray<byte> value)
        {
            throw new NotImplementedException();
        }

        public object InvokeMethod(string subarray, int i, int dataOffset) =>
            throw new NotImplementedException();

        [DllImport("__Internal")]
        private static extern int JSContext_InvokeFunction(IntPtr contextId, IntPtr functionName, IntPtr argsJson, IntPtr result, int resultSize);

        [DllImport("__Internal")]
        private static extern int JSContext_StoreObject(IntPtr contextId, IntPtr expression, IntPtr objectId, int objectIdSize);

        [DllImport("__Internal")]
        private static extern int JSContext_GetObjectProperty(IntPtr contextId, IntPtr objectId, IntPtr name, IntPtr result, int resultSize);

        [DllImport("__Internal")]
        private static extern int JSContext_SetObjectProperty(IntPtr contextId, IntPtr objectId, IntPtr name, IntPtr valueJson);

        [DllImport("__Internal")]
        private static extern int JSContext_InvokeObjectMethod(IntPtr contextId, IntPtr objectId, IntPtr methodName, IntPtr argsJson, IntPtr result,
            int resultSize);

        [DllImport("__Internal")]
        private static extern int JSContext_InvokeObjectAsFunction(IntPtr contextId, IntPtr objectId, IntPtr argsJson, IntPtr result, int resultSize);

        public static WebGLScriptObject CreateFromExpression(WebGLJavaScriptEngine engine, string expression)
        {
            IntPtr exprPtr = Marshal.StringToHGlobalAnsi(expression);

            try
            {
                var bufferSize = 256;
                IntPtr objectIdPtr = Marshal.AllocHGlobal(bufferSize);

                try
                {
                    IntPtr contextIdPtr = Marshal.StringToHGlobalAnsi(engine.contextId);

                    try
                    {
                        int result = JSContext_StoreObject(contextIdPtr, exprPtr, objectIdPtr, bufferSize);

                        if (result <= 0)
                        {
                            if (result < 0)
                            {
                                bufferSize = -result;
                                Marshal.FreeHGlobal(objectIdPtr);
                                objectIdPtr = Marshal.AllocHGlobal(bufferSize);
                                result = JSContext_StoreObject(contextIdPtr, exprPtr, objectIdPtr, bufferSize);
                            }

                            if (result <= 0)
                                throw new InvalidOperationException($"Failed to create object from expression: {expression}");
                        }

                        string objectId = Marshal.PtrToStringAnsi(objectIdPtr, result);
                        return new WebGLScriptObject(engine, objectId);
                    }
                    finally { Marshal.FreeHGlobal(contextIdPtr); }
                }
                finally { Marshal.FreeHGlobal(objectIdPtr); }
            }
            finally { Marshal.FreeHGlobal(exprPtr); }
        }

        /// <inheritdoc />
        public object GetNativeObject() =>
            this;
    }
}

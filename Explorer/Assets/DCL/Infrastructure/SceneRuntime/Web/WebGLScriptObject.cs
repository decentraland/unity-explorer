using System;
using System.Runtime.InteropServices;
using Utility;

namespace SceneRuntime.Web
{
    public class WebGLScriptObject : IScriptObject
    {
        private readonly WebGLJavaScriptEngine engine;
        private readonly string propertyPath;

        public WebGLScriptObject(WebGLJavaScriptEngine engine, string propertyPath)
        {
            this.engine = engine;
            this.propertyPath = propertyPath;
        }

        public object InvokeAsFunction(params object[] args)
        {
            string argsJson = Newtonsoft.Json.JsonConvert.SerializeObject(args);

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
                                return Newtonsoft.Json.JsonConvert.DeserializeObject(resultStr);
                            }
                            throw new InvalidOperationException($"Object {propertyPath} is not callable as a function");
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(contextIdPtr);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(resultPtr);
                    }
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
                                return Newtonsoft.Json.JsonConvert.DeserializeObject(resultStr);
                            }
                            return null;
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(contextIdPtr);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(resultPtr);
                    }
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
                                return Newtonsoft.Json.JsonConvert.DeserializeObject(resultStr);
                            }
                            return null;
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(contextIdPtr);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(resultPtr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(objectIdPtr);
                    Marshal.FreeHGlobal(namePtr);
                }
            }
            else
            {
                string fullPath = string.IsNullOrEmpty(propertyPath) ? name : $"{propertyPath}.{name}";
                return engine.Evaluate(fullPath);
            }
        }

        public void SetProperty(string name, object value)
        {
            if (IsObjectId(propertyPath))
            {
                string valueJson = Newtonsoft.Json.JsonConvert.SerializeObject(value);
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
                    finally
                    {
                        Marshal.FreeHGlobal(contextIdPtr);
                    }
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
                string valueJson = Newtonsoft.Json.JsonConvert.SerializeObject(value);
                engine.Execute($"{fullPath} = {valueJson};");
            }
        }

        public void SetProperty(int index, object value)
        {
            throw new NotImplementedException();
        }

        private static bool IsObjectId(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("__obj_");
        }

        public IScriptObject Invoke(bool asConstructor, params object[] args)
        {
            if (asConstructor)
            {
                string argsJson = Newtonsoft.Json.JsonConvert.SerializeObject(args);
                string newExpr = $"new {propertyPath}(...{argsJson})";

                IntPtr exprPtr = Marshal.StringToHGlobalAnsi(newExpr);
                try
                {
                    int bufferSize = 256;
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
                        finally
                        {
                            Marshal.FreeHGlobal(contextIdPtr);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(objectIdPtr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(exprPtr);
                }
            }
            return this;
        }

        public void SetProperty(int index, IDCLTypedArray<byte> value)
        {
            throw new NotImplementedException();
        }

        public IDCLTypedArray<byte> InvokeMethod(string subarray, int i, int dataOffset) =>
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
        private static extern int JSContext_InvokeObjectMethod(IntPtr contextId, IntPtr objectId, IntPtr methodName, IntPtr argsJson, IntPtr result, int resultSize);

        [DllImport("__Internal")]
        private static extern int JSContext_InvokeObjectAsFunction(IntPtr contextId, IntPtr objectId, IntPtr argsJson, IntPtr result, int resultSize);

        public static WebGLScriptObject CreateFromExpression(WebGLJavaScriptEngine engine, string expression)
        {
            IntPtr exprPtr = Marshal.StringToHGlobalAnsi(expression);
            try
            {
                int bufferSize = 256;
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
                    finally
                    {
                        Marshal.FreeHGlobal(contextIdPtr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(objectIdPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(exprPtr);
            }
        }
    }
}

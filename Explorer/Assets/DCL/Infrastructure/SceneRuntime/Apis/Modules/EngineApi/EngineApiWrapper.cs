using CrdtEcsBridge.PoolsProviders;
using JetBrains.Annotations;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Profiling;
using Utility.Memory;

namespace SceneRuntime.Apis.Modules.EngineApi
{
    public class EngineApiWrapper : JsApiWrapper<IEngineApi>
    {
        protected readonly ISceneExceptionsHandler exceptionsHandler;

        private readonly string threadName;
        private readonly SingleUnmanagedMemoryManager<byte> singleMemoryManager = new ();

        public EngineApiWrapper(
            IEngineApi api,
            ISceneData sceneData,
            ISceneExceptionsHandler exceptionsHandler,
            CancellationTokenSource disposeCts
        ) : base(api, disposeCts)
        {
            this.exceptionsHandler = exceptionsHandler;
            threadName = $"CrdtSendToRenderer({sceneData.SceneShortInfo})";
        }

        [UsedImplicitly]
        public PoolableByteArray CrdtSendToRenderer(ITypedArray<byte> data)
        {
            if (disposeCts.IsCancellationRequested)
                return PoolableByteArray.EMPTY;

            // V8ScriptItem does not support zero length
            ulong length = data.Length;

            if (length == 0)
                return PoolableByteArray.EMPTY;

            try
            {
                Profiler.BeginThreadProfiling("SceneRuntime", threadName);
                PoolableByteArray result = SendToRenderer(data, length);
                Profiler.EndThreadProfiling();

                return result.IsEmpty ? PoolableByteArray.EMPTY : result;
            }
            catch (Exception e)
            {
                if (!disposeCts.IsCancellationRequested)

                    // Report an uncategorized MANAGED exception (don't propagate it further)
                    exceptionsHandler.OnEngineException(e);

                return PoolableByteArray.EMPTY;
            }
        }

        private PoolableByteArray SendToRenderer(ITypedArray<byte> data, ulong length)
        {
            // Avoid copying of the buffer
            // InvokeWithDirectAccess<TArg, TResult>(Func<IntPtr, TArg, TResult>, TArg)
            return data.InvokeWithDirectAccess(
                static (ptr, args) =>
                {
                    args.singleMemoryManager.Assign(ptr, (int)args.length);
                    return args.api.CrdtSendToRenderer(args.singleMemoryManager.Memory);
                },
                (api, length, singleMemoryManager)
            );
        }

#if UNITY_INCLUDE_TESTS || UNITY_EDITOR
        public PoolableByteArray SendToRendererTest(ITypedArray<byte> data)
        {
            return SendToRenderer(data, data.Length);
        }

        private PoolableByteArray lastInput = PoolableByteArray.EMPTY;

        public PoolableByteArray SendToRendererTestLegacy(ITypedArray<byte> data, IInstancePoolsProvider instancePoolsProvider)
        {
            RenewCrdtRawDataPoolFromScriptArray(instancePoolsProvider, data, ref lastInput);
            return api.CrdtSendToRenderer(lastInput.Memory);
        }

        private static void RenewCrdtRawDataPoolFromScriptArray(
            IInstancePoolsProvider instancePoolsProvider, ITypedArray<byte> scriptArray,
            ref PoolableByteArray lastInput)
        {
            EnsureArrayLength(instancePoolsProvider, (int)scriptArray.Length, ref lastInput);

            // V8ScriptItem does not support zero length
            if (scriptArray.Length > 0)
                scriptArray.Read(0, scriptArray.Length, lastInput.Array, 0);
        }

        private static void EnsureArrayLength(IInstancePoolsProvider instancePoolsProvider,
            int scriptArrayLength, ref PoolableByteArray lastInput)
        {
            // if the rented array can't keep the desired data, replace it
            if (lastInput.Array.Length < scriptArrayLength)
            {
                // Release the old one
                lastInput.Dispose();

                // Rent a new one
                lastInput = instancePoolsProvider.GetAPIRawDataPool(scriptArrayLength);
            }
            // Otherwise set the desired length to the existing array so it provides a correct span
            else
                lastInput.SetLength(scriptArrayLength);
        }
#endif

        [UsedImplicitly]
        public PoolableByteArray CrdtGetState()
        {
            if (disposeCts.IsCancellationRequested)
                return PoolableByteArray.EMPTY;

            try
            {
                PoolableByteArray result = api.CrdtGetState();
                return result.IsEmpty ? PoolableByteArray.EMPTY : result;
            }
            catch (Exception e)
            {
                // Report an uncategorized MANAGED exception (don't propagate it further)
                exceptionsHandler.OnEngineException(e);
                return PoolableByteArray.EMPTY;
            }
        }

        [UsedImplicitly]
        public virtual PoolableSDKObservableEventArray? SendBatch() =>
            null;
    }

#if UNITY_INCLUDE_TESTS || UNITY_EDITOR
    public class TestArray : ITypedArray<byte>, IEnumerable<byte>
    {
        private byte[] data;

        public ulong Length => (ulong) data.Length;

        public IArrayBuffer ArrayBuffer => throw new NotImplementedException();

        public ulong Offset => throw new NotImplementedException();

        public ulong Size => throw new NotImplementedException();

        public JavaScriptObjectKind Kind => throw new NotImplementedException();

        public JavaScriptObjectFlags Flags => throw new NotImplementedException();

        public ScriptEngine Engine => throw new NotImplementedException();

        public IEnumerable<int> PropertyIndices => throw new NotImplementedException();

        public IEnumerable<string> PropertyNames => throw new NotImplementedException();

        public TestArray(byte[] data)
        {
            this.data = data;
        }

        public void Dispose()
        {
            //Ignore
        }

        public object this[int index]
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public object this[string name, params object[] o]
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public byte[] ToArray()
        {
            return data;
        }

        public ulong Read(
                ulong index,
                ulong length,
                byte[] destination,
                ulong destinationIndex)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            ulong dataLength = (ulong)data.Length;
            ulong destLength = (ulong)destination.Length;

            if (index > dataLength)
                return 0;

            ulong available = dataLength - index;
            ulong toCopy = length <= available ? length : available;

            ulong destAvailable = destLength - destinationIndex;
            if (toCopy > destAvailable)
                toCopy = destAvailable;

            if (toCopy == 0)
                return 0;

            Buffer.BlockCopy(
                    data,
                    (int)index,
                    destination,
                    (int)destinationIndex,
                    (int)toCopy
                    );

            return toCopy;
        }

        public ulong Read(ulong _, ulong __, Span<byte> ___, ulong ____)
        {
            throw new NotImplementedException();
        }

        public ulong Write(byte[] _, ulong __, ulong ___, ulong ____)
        {
            throw new NotImplementedException();
        }

        public ulong Write(ReadOnlySpan<byte> _, ulong __, ulong ___, ulong ____)
        {
            throw new NotImplementedException();
        }

        public byte[] GetBytes()
        {
            return data;
        }

        public ulong ReadBytes(ulong _, ulong __, byte[] ___, ulong ____)
        {
            throw new NotImplementedException();
        }

        public ulong ReadBytes(ulong _, ulong __, Span<byte> ___, ulong ____)
        {
            throw new NotImplementedException();
        }

        public ulong WriteBytes(byte[] _, ulong __, ulong ___, ulong ____)
        {
            throw new NotImplementedException();
        }

        public ulong WriteBytes(ReadOnlySpan<byte> _, ulong __, ulong ___, ulong ____)
        {
            throw new NotImplementedException();
        }

        public object GetProperty(int i)
        {
            throw new NotImplementedException();
        }

        public object GetProperty(string name, params object[] p)
        {
            throw new NotImplementedException();
        }

        public void SetProperty(string name, params object[] p)
        {
            throw new NotImplementedException();
        }

        public void SetProperty(int i, object o)
        {
            throw new NotImplementedException();
        }

        public bool DeleteProperty(string name)
        {
            throw new NotImplementedException();
        }

        public bool DeleteProperty(int i)
        {
            throw new NotImplementedException();
        }

        public object Invoke(bool b, params object[] o)
        {
            throw new NotImplementedException();
        }

        public object InvokeMethod(string s, params object[] o)
        {
            throw new NotImplementedException();
        }

        public object InvokeAsFunction(params object[] o)
        {
            throw new NotImplementedException();
        }

        public void InvokeWithDirectAccess(Action<IntPtr> action)
        {
            unsafe
            {
                fixed (byte* ptr = data)
                {
                    IntPtr intPtr = new IntPtr(ptr);
                    action(intPtr);
                }
            }
        }

        public void InvokeWithDirectAccess<TArgs>(Action<IntPtr, TArgs> action, in TArgs args)
        {
            unsafe
            {
                fixed (byte* ptr = data)
                {
                    IntPtr intPtr = new IntPtr(ptr);
                    action(intPtr, args);
                }
            }
        }

        public TResult InvokeWithDirectAccess<TResult>(Func<IntPtr, TResult> func)
        {
            unsafe
            {
                fixed (byte* ptr = data)
                {
                    IntPtr intPtr = new IntPtr(ptr);
                    return func(intPtr);
                }
            }
        }

        public TResult InvokeWithDirectAccess<TArgs, TResult>(Func<IntPtr, TArgs, TResult> func, in TArgs args)
        {
            unsafe
            {
                fixed (byte* ptr = data)
                {
                    IntPtr intPtr = new IntPtr(ptr);
                    return func(intPtr, args);
                }
            }
        }

        public IEnumerator<byte> GetEnumerator()
        {
            return ((IEnumerable<byte>)data).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return data.GetEnumerator();
        }
    }
#endif

}

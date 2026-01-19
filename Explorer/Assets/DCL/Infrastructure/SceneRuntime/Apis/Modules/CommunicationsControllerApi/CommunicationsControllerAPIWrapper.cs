using JetBrains.Annotations;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using CrdtEcsBridge.PoolsProviders;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi
{
    public class CommunicationsControllerAPIWrapper : JsApiWrapper<ICommunicationsControllerAPI>
    {
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;

        public CommunicationsControllerAPIWrapper(ICommunicationsControllerAPI api, ISceneExceptionsHandler sceneExceptionsHandler, CancellationTokenSource disposeCts) : base(api, disposeCts)
        {
            this.sceneExceptionsHandler = sceneExceptionsHandler;
        }

        // avoid copying, just iterator over the dataList
        private struct PoolableByteArrayListWrap : IEnumerable<IPoolableByteArray>
        {
            private IList<object> origin;

            public PoolableByteArrayListWrap(IList<object> origin)
            {
                this.origin = origin;
            }

            public Enumerator GetEnumerator() => new Enumerator(origin);

            IEnumerator<IPoolableByteArray> IEnumerable<IPoolableByteArray>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public struct Enumerator : IEnumerator<IPoolableByteArray>
            {
                private readonly IList<object> origin;
                private int index;

                public Enumerator(IList<object> origin)
                {
                    this.origin = origin;
                    index = -1;
                }

                public IPoolableByteArray Current => new PoolableArrayOverTypedArray(origin[index]);
                object IEnumerator.Current => Current;

                public bool MoveNext()
                {
                    index++;
                    return index < origin.Count;
                }

                public void Reset() => index = -1;
                public void Dispose() { }
            }
        }

        private struct PoolableArrayOverTypedArray : IPoolableByteArray
        {
            private ITypedArray<byte> origin;

            public PoolableArrayOverTypedArray(object origin)
            {
                this.origin = (ITypedArray<byte>) origin;
            }

            public int Length => (int) origin.Length;


            public void InvokeWithDirectAccess<TArgs>(Action<IntPtr, TArgs> action, in TArgs args)
            {
                origin.InvokeWithDirectAccess(action, args);
            }

            public byte[] CloneAsArray()
            {
                var result = new byte[Length];

                origin.InvokeWithDirectAccess(
                        static (ptr, state) =>
                        {
                        System.Runtime.InteropServices.Marshal.Copy(ptr, state.Buffer, 0, state.Length);
                        },
                        new CopyState(result, result.Length)
                        );

                return result;
            }

            private readonly struct CopyState
            {
                public readonly byte[] Buffer;
                public readonly int Length;

                public CopyState(byte[] buffer, int length)
                {
                    Buffer = buffer;
                    Length = length;
                }
            }
        }

        private void SendBinaryToParticipants(IList<object> dataList, string? recipient)
        {
            try
            {
                var wrap = new PoolableByteArrayListWrap(dataList);
                api.SendBinary(wrap, recipient);
            }
            catch (Exception e)
            {
                sceneExceptionsHandler.OnEngineException(e);
            }
        }

        [UsedImplicitly]
        public object SendBinary(IList<object> broadcastData) =>
            SendBinary(broadcastData, null);

        [UsedImplicitly]
        public object SendBinary(IList<object> broadcastData, IList<object>? peerData)
        {
            SendBinaryToParticipants(broadcastData, null);

            if (peerData != null)
                for (var i = 0; i < peerData.Count; i++)
                {
                    object? obj = peerData[i];

                    if (obj is IScriptObject perRecipientStruct)
                    {
                        var recipient = (IList<object>)perRecipientStruct.GetProperty("address")!;
                        var data = (IList<object>)perRecipientStruct.GetProperty("data")!;

                        if (data.Count is 0)
                            continue;

                        if (recipient.Count is 0)
                            SendBinaryToParticipants(data, null);

                        foreach (object? address in recipient)
                            if (address != null)
                            {
                                var stringAddress = (string)address;

                                if (!string.IsNullOrEmpty(stringAddress))
                                    SendBinaryToParticipants(data, stringAddress);
                            }
                    }
                }

            return api.GetResult();
        }
    }
}

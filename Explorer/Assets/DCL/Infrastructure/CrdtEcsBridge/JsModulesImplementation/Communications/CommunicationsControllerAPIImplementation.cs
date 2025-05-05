using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene;
using SceneRuntime;
using System;
using System.IO;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    public class CommunicationsControllerAPIImplementation : CommunicationsControllerAPIImplementationBase
    {
        public CommunicationsControllerAPIImplementation(ISceneData sceneData,
            ISceneCommunicationPipe messagePipesHub, IJsOperations jsOperations)
            : base(sceneData, messagePipesHub, jsOperations, ISceneCommunicationPipe.MsgType.Uint8Array)
        {
        }

        protected override void OnMessageReceived(ISceneCommunicationPipe.DecodedMessage message)
        {
            string walletId = message.FromWalletId;
            int dataLength = message.Data.Length;
            int dataOffset = 0;
            var array = jsOperations.GetTempUint8Array();

            unsafe
            {
                fixed (byte* dataPtr = message.Data)
                {
                    var data = (IntPtr)dataPtr;

                    array.InvokeWithDirectAccess(buffer =>
                    {
                        var bufferPtr = (byte*)buffer;

                        fixed (char* walletIdPtr = walletId)
                            bufferPtr[0] = (byte)Encoding.UTF8.GetBytes(walletIdPtr, walletId.Length,
                                bufferPtr + 1, byte.MaxValue);

                        dataOffset = bufferPtr[0] + 1;

                        if (dataOffset + dataLength > IJsOperations.LIVEKIT_MAX_SIZE)
                            throw new InternalBufferOverflowException(
                                "Received a message larger than LIVEKIT_MAX_SIZE");

                        UnsafeUtility.MemCpy(bufferPtr + dataOffset, (byte*)data, dataLength);
                    });
                }
            }

            var arrayObj = (ScriptObject)array;

            var subArray = (ITypedArray<byte>)arrayObj.InvokeMethod("subarray", 0,
                dataOffset + dataLength);

            lock (eventsToProcess)
                eventsToProcess.Add(subArray);
        }
    }
}

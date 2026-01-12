using SceneRunner.Scene;
using SceneRuntime;
using System;
using System.IO;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using Utility;

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
            var dataOffset = 0;
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
                        {
                            bufferPtr[0] = (byte)Encoding.UTF8.GetBytes(walletIdPtr, walletId.Length,
                                bufferPtr + 1, byte.MaxValue);
                        }

                        dataOffset = bufferPtr[0] + 1;

                        if (dataOffset + dataLength > IJsOperations.LIVEKIT_MAX_SIZE)
                            throw new InternalBufferOverflowException(
                                "Received a message larger than LIVEKIT_MAX_SIZE");

                        UnsafeUtility.MemCpy(bufferPtr + dataOffset, (byte*)data, dataLength);
                    });
                }
            }

            //TODO FRAN: FIX THIS ->
            //Suspicious cast: there is no type in the solution which is inherited from both 'Utility.IDCLTypedArray<byte>' and 'SceneRuntime.IScriptObject'
            var arrayObj = (IScriptObject)array;

            var subArray = arrayObj.InvokeMethod("subarray", 0,
                dataOffset + dataLength);

            lock (eventsToProcess)
            {
                eventsToProcess.Add(subArray);
            }
        }
    }
}

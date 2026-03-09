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
            int dataOffset;
            var array = jsOperations.GetTempUint8Array();

#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
            // WebGL has no direct memory access — build the payload in managed memory then bulk-copy to JS
            byte[] walletIdBytes = Encoding.UTF8.GetBytes(walletId);
            int walletIdByteLength = Math.Min(walletIdBytes.Length, byte.MaxValue);
            dataOffset = walletIdByteLength + 1;

            if (dataOffset + dataLength > IJsOperations.LIVEKIT_MAX_SIZE)
                throw new InternalBufferOverflowException("Received a message larger than LIVEKIT_MAX_SIZE");

            var buffer = new byte[dataOffset + dataLength];
            buffer[0] = (byte)walletIdByteLength;
            Buffer.BlockCopy(walletIdBytes, 0, buffer, 1, walletIdByteLength);
            message.Data.CopyTo(buffer.AsSpan(dataOffset));

            array.WriteBytes(buffer, 0, (ulong)(dataOffset + dataLength), 0);
#else
            dataOffset = 0;

            unsafe
            {
                fixed (byte* dataPtr = message.Data)
                {
                    var data = (IntPtr)dataPtr;

                    array.InvokeWithDirectAccess(bufferPtr =>
                    {
                        var ptr = (byte*)bufferPtr;

                        fixed (char* walletIdPtr = walletId)
                        {
                            ptr[0] = (byte)Encoding.UTF8.GetBytes(walletIdPtr, walletId.Length,
                                ptr + 1, byte.MaxValue);
                        }

                        dataOffset = ptr[0] + 1;

                        if (dataOffset + dataLength > IJsOperations.LIVEKIT_MAX_SIZE)
                            throw new InternalBufferOverflowException(
                                "Received a message larger than LIVEKIT_MAX_SIZE");

                        UnsafeUtility.MemCpy(ptr + dataOffset, (byte*)data, dataLength);
                    });
                }
            }
#endif

            IDCLScriptObject arrayObj = (IDCLScriptObject)array;
            object subArrayResult = arrayObj.InvokeMethod("subarray", 0, dataOffset + dataLength);
            IDCLTypedArray<byte> subArray = (IDCLTypedArray<byte>)subArrayResult;

            lock (eventsToProcess)
            {
                eventsToProcess.Add(subArray);
            }
        }
    }
}

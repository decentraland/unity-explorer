using CrdtEcsBridge.PoolsProviders;
using SceneRunner.Scene;
using SceneRuntime;
using System;
using System.IO;
using System.Text;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    public class CommunicationsControllerAPIImplementation : CommunicationsControllerAPIImplementationBase
    {
        private readonly IInstancePoolsProvider byteArrayPool;

        public CommunicationsControllerAPIImplementation(ISceneData sceneData,
            ISceneCommunicationPipe messagePipesHub, IJsOperations jsOperations,
            IInstancePoolsProvider byteArrayPool)
            : base(sceneData, messagePipesHub, jsOperations, ISceneCommunicationPipe.MsgType.Uint8Array)
        {
            this.byteArrayPool = byteArrayPool;
        }

        protected override void OnMessageReceived(ISceneCommunicationPipe.DecodedMessage message)
        {
#if WEBGL_ACTIVE
            // WebGL has no direct memory access — build the payload in managed memory then bulk-copy to JS
            var array = jsOperations.GetTempUint8Array();
            string walletId = message.FromWalletId;
            int dataLength = message.Data.Length;

            byte[] walletIdBytes = Encoding.UTF8.GetBytes(walletId);
            int walletIdByteLength = Math.Min(walletIdBytes.Length, byte.MaxValue);
            int dataOffset = walletIdByteLength + 1;

            if (dataOffset + dataLength > IJsOperations.LIVEKIT_MAX_SIZE)
                throw new InternalBufferOverflowException("Received a message larger than LIVEKIT_MAX_SIZE");

            var buffer = new byte[dataOffset + dataLength];
            buffer[0] = (byte)walletIdByteLength;
            Buffer.BlockCopy(walletIdBytes, 0, buffer, 1, walletIdByteLength);
            message.Data.CopyTo(buffer.AsSpan(dataOffset));

            array.WriteBytes(buffer, 0, (ulong)(dataOffset + dataLength), 0);
#else
            var array = byteArrayPool.GetAPIRawDataPool(
                IJsOperations.LIVEKIT_MAX_SIZE);

            int walletIdLength = Encoding.UTF8.GetBytes(message.FromWalletId,
                array.Array.AsSpan(1));

            if (walletIdLength > 255)
                throw new OverflowException("Wallet ID is too long");

            array.Array[0] = (byte)walletIdLength;
            int dataOffset = walletIdLength + 1;
            int totalLength = dataOffset + message.Data.Length;

            if (totalLength > IJsOperations.LIVEKIT_MAX_SIZE)
                throw new InternalBufferOverflowException(
                    "Received a message larger than LIVEKIT_MAX_SIZE");

            message.Data.CopyTo(array.Array.AsSpan(dataOffset));
            array.SetLength(totalLength);

            lock (eventsToProcess) { eventsToProcess.Add(array); }
#endif
        }
    }
}

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
        }
    }
}

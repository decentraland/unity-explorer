using CrdtEcsBridge.PoolsProviders;
using SceneRunner.Scene;
using SceneRuntime;
using System;
using System.Text;
using SceneRunner.Admins;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    public class CommunicationsControllerAPIImplementation : CommunicationsControllerAPIImplementationBase
    {
        private readonly IInstancePoolsProvider byteArrayPool;
        private readonly SceneAdmins sceneAdmins;

        public CommunicationsControllerAPIImplementation(
                ISceneData sceneData,
                ISceneCommunicationPipe messagePipesHub,
                IJsOperations jsOperations,
                IInstancePoolsProvider byteArrayPool,
                SceneAdmins sceneAdmins
                )
            : base(sceneData, messagePipesHub, jsOperations, ISceneCommunicationPipe.MsgType.Uint8Array)
        {
            this.byteArrayPool = byteArrayPool;
            this.sceneAdmins = sceneAdmins;
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
            int totalLength = dataOffset;

            // At this point data is already without MsgType (Explorer routing is truncated a step above).

            // read first byte as SDK routing
            CommsMessageType commsMessageType = (CommsMessageType)message.Data[0];
            // Copy and filter batch
            ReadOnlySpan<byte> sourceData = message.Data;
            // Filtered data is already a view of the target array
            Span<byte> filteredUnbounded = array.Array.AsSpan(dataOffset);

            // Message is considered safe if it's from a scene admin // TODO call Api to check if wallet is an admin
            bool? adminResult = sceneAdmins.IsAdmin(message.FromWalletId);
            // Consider the user as non-admin until we know for sure
            bool isFromSceneAdmin = adminResult == null ? false : adminResult.Value;


            // TODO This logic mostly duplicates CommunicationsControllerAPIImplementationBase.SendBinary we should standardise it later
            // Filter CRDT messages before receiving
            if (commsMessageType == CommsMessageType.CRDT)
            {
                int filteredLength = FilterCRDTMessage(sourceData, filteredUnbounded, isTrustedSource: isFromSceneAdmin);
                totalLength += filteredLength;
            }
            // Filter RES_CRDT_STATE messages before receiving
            else if (commsMessageType == CommsMessageType.RES_CRDT_STATE)
            {
                int filteredLength = FilterCRDTStateMessage(sourceData, filteredUnbounded, isTrustedSource: isFromSceneAdmin);
                totalLength += filteredLength;
            }
            // No filter in the case of REQ_CRDT_STATE
            else
            {
                sourceData.CopyTo(filteredUnbounded); // basically no filtering
                totalLength += sourceData.Length;
            }


            if (totalLength > IJsOperations.LIVEKIT_MAX_SIZE)
                throw new Exception("Received a message larger than LIVEKIT_MAX_SIZE");

            array.SetLength(totalLength);
            base.Enqueue(array);
        }
    }
}

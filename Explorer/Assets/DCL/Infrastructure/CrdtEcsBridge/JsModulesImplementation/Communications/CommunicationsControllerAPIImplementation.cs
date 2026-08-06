using CrdtEcsBridge.PoolsProviders;
using DCL.Diagnostics;
using SceneRunner.Scene;
using SceneRuntime;
using System;
using System.Text;
using RichTypes;

namespace CrdtEcsBridge.JsModulesImplementation.Communications
{
    public class CommunicationsControllerAPIImplementation : CommunicationsControllerAPIImplementationBase
    {
        private readonly IInstancePoolsProvider byteArrayPool;

        public CommunicationsControllerAPIImplementation(
                ISceneData sceneData,
                ISceneCommunicationPipe messagePipesHub,
                IJsOperations jsOperations,
                IInstancePoolsProvider byteArrayPool
                )
            : base(sceneData, messagePipesHub, jsOperations, ISceneCommunicationPipe.MsgType.Uint8Array)
        {
            this.byteArrayPool = byteArrayPool;
        }

        protected override void OnMessageReceived(ISceneCommunicationPipe.DecodedMessage message)
        {
            int walletIdLength = Encoding.UTF8.GetByteCount(message.FromWalletId);

            if (walletIdLength > 255)
                throw new OverflowException("Wallet ID is too long");

            int dataOffset = walletIdLength + 1;

            // The receive buffer also carries the locally prepended [len][walletId] header, so the
            // peer-controlled payload is bounded by the same LIVEKIT_MAX_SIZE budget. The size is
            // remote and untrusted: an oversized packet is dropped, not thrown, because throwing
            // turns a routine network condition into a reported error for every such packet.
            if (message.Data.Length > IJsOperations.LIVEKIT_MAX_SIZE)
            {
                ReportHub.LogWarning(ReportCategory.LIVEKIT,
                    $"Dropped oversized scene message ({message.Data.Length} bytes) from {message.FromWalletId}");
                return;
            }

            var array = byteArrayPool.GetAPIRawDataPool(dataOffset + IJsOperations.LIVEKIT_MAX_SIZE);

            Encoding.UTF8.GetBytes(message.FromWalletId, array.Array.AsSpan(1));

            array.Array[0] = (byte)walletIdLength;
            int totalLength = dataOffset;

            // At this point data is already without MsgType (Explorer routing is truncated a step above).

            // read first byte as SDK routing
            CommsMessageType commsMessageType = (CommsMessageType)message.Data[0];
            // Copy and filter batch
            ReadOnlySpan<byte> sourceData = message.Data;
            bool isTrustedSource = message.IsTrustedSource;
            // Filtered data is already a view of the target array
            Span<byte> filteredUnbounded = array.Array.AsSpan(dataOffset);

            // TODO This logic mostly duplicates CommunicationsControllerAPIImplementationBase.SendBinary we should standardise it later
            // Filter CRDT messages before receiving
            if (commsMessageType == CommsMessageType.CRDT)
            {
                int filteredLength = FilterCRDTMessage(sourceData, filteredUnbounded, isTrustedSource);
                totalLength += filteredLength;
            }
            // Filter RES_CRDT_STATE messages before receiving
            else if (commsMessageType == CommsMessageType.ResCRDTState)
            {
                int filteredLength = FilterCRDTStateMessage(sourceData, filteredUnbounded, isTrustedSource);
                totalLength += filteredLength;
            }
            // No filter in the case of REQ_CRDT_STATE
            else
            {
                sourceData.CopyTo(filteredUnbounded); // basically no filtering
                totalLength += sourceData.Length;
            }

            array.SetLength(totalLength);
            base.Enqueue(array);
        }
    }
}

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
            int maxWalletIdBytes = Encoding.UTF8.GetMaxByteCount(message.FromWalletId.Length);
            var array = byteArrayPool.GetAPIRawDataPool(maxWalletIdBytes + 1 + IJsOperations.LIVEKIT_MAX_SIZE);
            int walletIdLength = Encoding.UTF8.GetBytes(message.FromWalletId, array.Array.AsSpan(1));

            if (walletIdLength > 255)
            {
                array.Dispose();
                throw new OverflowException("Wallet ID is too long");
            }

            array.Array[0] = (byte)walletIdLength;
            int dataOffset = walletIdLength + 1;

            // The locally prepended [len][walletId] header plus the peer-controlled payload must
            // together fit the fixed LIVEKIT_MAX_SIZE Uint8Array that GetResult() writes into
            // (SceneRuntimeImpl.GetTempUint8Array). An oversized combination is dropped, not
            // thrown, because throwing turns a routine network condition into a reported error
            // for every such packet.
            if (dataOffset + message.Data.Length > IJsOperations.LIVEKIT_MAX_SIZE)
            {
                array.Dispose();
                ReportHub.LogWarning(ReportCategory.LIVEKIT,
                    $"Dropped oversized scene message ({message.Data.Length} bytes) from {message.FromWalletId}");
                return;
            }

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

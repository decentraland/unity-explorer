#nullable enable

using CRDT.Protocol;
using System;
using Utility;

namespace CRDT
{
    public class CRDTFilter
    {
        // “asset-packs::VideoControlState” is 2092194694
        private static readonly uint NO_SYNC_COMPONENT_ID = 2092194694;

        /// <summary>
        /// Output must be equal or bigger than memory
        /// </summary>
        public static void FilterSceneMessageBatch(ReadOnlySpan<byte> memory, Span<byte> output, out int totalWrite)
        {
            totalWrite = 0;

            // write first byte (message type: CRDT = 1)
            const int CRDT_STATE_LENGTH = 1;
            totalWrite += CRDT_STATE_LENGTH;
            memory.Slice(0, CRDT_STATE_LENGTH).CopyTo(output);
            memory = memory.Slice(CRDT_STATE_LENGTH);
            output = output.Slice(CRDT_STATE_LENGTH);

            // Filter the CRDT messages
            FilterCRDTMessages(memory, output, out int filteredLength);
            totalWrite += filteredLength;
        }

        /// <summary>
        /// Filters CRDT state messages (RES_CRDT_STATE).
        /// Encoding happens at the SDK runtime: https://github.com/decentraland/js-sdk-toolchain/blob/f122eaa2acaaed80db7ee0302e8d60ca7d2337bf/packages/@dcl/sdk/src/network/message-bus-sync.ts#L177-L208
        /// The format is: [message type byte] + [1 byte: address length] + [address bytes] + [raw CRDT messages]
        /// Output must be equal or bigger than memory
        /// </summary>
        public static void FilterCRDTState(ReadOnlySpan<byte> memory, Span<byte> output, out int totalWrite)
        {
            totalWrite = 0;

            if (memory.Length < 3) // Need at least: 1 byte type + 1 byte address length + some data
                return;

            // Write the first byte (message type: RES_CRDT_STATE = 3)
            output[0] = memory[0];
            totalWrite = 1;

            // Read the address length (1 byte)
            byte addressLength = memory[1];

            UnityEngine.Debug.Log($"FilterCRDTState - Message type: {memory[0]}, Address length: {addressLength}, Total length: {memory.Length}");

            if (memory.Length < 2 + addressLength)
                return; // Not enough data

            // Copy the address length and address bytes as-is
            output[1] = addressLength;
            memory.Slice(2, addressLength).CopyTo(output.Slice(2));
            totalWrite += 1 + addressLength;

            // The CRDT messages start after: type byte (1) + address length (1) + address bytes
            int crdtStartOffset = 2 + addressLength;
            ReadOnlySpan<byte> crdtMessages = memory.Slice(crdtStartOffset);

            // Filter the CRDT messages into the output after the address
            int outputCrdtOffset = 2 + addressLength;
            FilterCRDTMessages(crdtMessages, output.Slice(outputCrdtOffset), out int filteredLength);
            totalWrite += filteredLength; // Already counted: type byte + address length + address bytes, now add filtered data
        }

        private static uint ComponentIdOfPutNetworkComponentType(ReadOnlySpan<byte> memory) =>
            memory.Slice(4).ReadConst<uint>(); // offset entityId

        /// <summary>
        /// Filters CRDT messages from a span, removing PUT_COMPONENT_NETWORK messages with NO_SYNC_COMPONENT_ID.
        /// This is the core filtering logic shared by both FilterSceneMessageBatch and FilterCRDTState.
        /// </summary>
        private static void FilterCRDTMessages(ReadOnlySpan<byte> crdtMessages, Span<byte> output, out int totalWrite)
        {
            totalWrite = 0;

            while (crdtMessages.Length > CRDTConstants.MESSAGE_HEADER_LENGTH)
            {
                uint messageLength = crdtMessages.Read<uint>();
                CRDTMessageType messageType = crdtMessages.ReadEnumAs<CRDTMessageType, uint>();

                // Message length lower than minimal, it's an invalid message
                if (messageLength <= CRDTConstants.MESSAGE_HEADER_LENGTH)
                    break;

                // Do we have the bytes computed in the header?
                uint remainingBytesToRead = messageLength - CRDTConstants.MESSAGE_HEADER_LENGTH;

                if (remainingBytesToRead > crdtMessages.Length)
                    break;

                uint bodyLength = CRDTMessageTypeUtils.TypeLengthBytes(messageType, crdtMessages);

                // Filter out PUT_COMPONENT_NETWORK messages with NO_SYNC_COMPONENT_ID
                if (messageType is not CRDTMessageType.PUT_COMPONENT_NETWORK || ComponentIdOfPutNetworkComponentType(crdtMessages) != NO_SYNC_COMPONENT_ID)
                {
                    output.Write(messageLength);
                    output.Write((uint)messageType);
                    totalWrite += 8;

                    crdtMessages.Slice(0, (int)bodyLength).CopyTo(output);
                    output = output.Slice((int)bodyLength);
                    totalWrite += (int)bodyLength;
                }

                crdtMessages = crdtMessages.Slice((int)bodyLength);
            }
        }
    }
}

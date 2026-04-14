#nullable enable

using CRDT.Protocol;
using DCL.Diagnostics;
using System;
using Utility;

namespace CRDT
{
    public class CRDTFilter
    {
        // Components that must NOT be synced between clients.
        // ComponentId is at body offset 4 (after entityId) for all relevant message types.
        private const uint VIDEO_CONTROL_STATE = 2092194694;   // asset-packs::VideoControlState
        private const uint PHYSICS_COMBINED_IMPULSE = DCL.ECS7.ComponentID.PHYSICS_COMBINED_IMPULSE;    // Player-specific physics impulse
        private const uint PHYSICS_COMBINED_FORCE = DCL.ECS7.ComponentID.PHYSICS_COMBINED_FORCE;      // Player-specific physics force

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

            // Need at least 3 -> 1 byte type + 1 byte address length + some data
            // Schema at https://github.com/decentraland/js-sdk-toolchain/blob/f122eaa2acaaed80db7ee0302e8d60ca7d2337bf/packages/@dcl/sdk/src/network/message-bus-sync.ts#L197-L208
            if (memory.Length < 3)
                return;

            const int MIN_OFFSET = 2;

            // Write the first byte (message type: RES_CRDT_STATE = 3)
            output[0] = memory[0];
            totalWrite = 1;

            // Read the address length (1 byte)
            byte addressLength = memory[1];

            ReportHub.Log(ReportCategory.CRDT, $"FilterCRDTState - Message type: {memory[0]}, Address length: {addressLength}, Total length: {memory.Length}");

            if (memory.Length < MIN_OFFSET + addressLength)
                return; // Not enough data

            // Copy the address length and address bytes as-is
            output[1] = addressLength;
            memory.Slice(MIN_OFFSET, addressLength).CopyTo(output.Slice(2));
            totalWrite += 1 + addressLength;

            // The CRDT messages start after: type byte (1) + address length (1) + address bytes
            int crdtStartOffset = MIN_OFFSET + addressLength;
            ReadOnlySpan<byte> crdtMessages = memory.Slice(crdtStartOffset);

            // Filter the CRDT messages into the output after the address
            int outputCrdtOffset = MIN_OFFSET + addressLength;
            FilterCRDTMessages(crdtMessages, output.Slice(outputCrdtOffset), out int filteredLength);
            totalWrite += filteredLength; // Already counted: type byte + address length + address bytes, now add filtered data
        }

        /// <summary>
        /// Reads the componentId from the message body. Located at offset 4 (after 4-byte entityId)
        /// for PUT_COMPONENT_NETWORK, DELETE_COMPONENT_NETWORK, and APPEND_COMPONENT messages.
        /// </summary>
        private static uint ReadComponentId(ReadOnlySpan<byte> messageBody) =>
            messageBody.Slice(4).ReadConst<uint>();

        private static bool IsNoSyncComponent(uint componentId) =>
            componentId is VIDEO_CONTROL_STATE or PHYSICS_COMBINED_IMPULSE or PHYSICS_COMBINED_FORCE;

        /// <summary>
        /// Filters CRDT messages from a span, removing messages with no-sync component IDs.
        /// Handles PUT_COMPONENT_NETWORK, DELETE_COMPONENT_NETWORK, and APPEND_COMPONENT.
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

                bool shouldDrop = messageType is CRDTMessageType.PUT_COMPONENT_NETWORK or CRDTMessageType.DELETE_COMPONENT_NETWORK
                                  && IsNoSyncComponent(ReadComponentId(crdtMessages));

                if (!shouldDrop)
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

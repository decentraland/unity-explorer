namespace CRDT.Protocol
{
    /// <summary>
    ///     First byte of a scene-room payload, written by the SDK runtime rather than by the Explorer.
    ///     Must be aligned with the SDK runtime's values at:
    ///     https://github.com/decentraland/js-sdk-toolchain/blob/c8695cd9b94e87ad567520089969583d9d36637f/packages/@dcl/sdk/src/network/binary-message-bus.ts#L3-L7
    /// </summary>
    public enum SdkCommsMessageType
    {
        CRDT = 1,

        /// <summary>Special signal to receive CRDT State from a peer.</summary>
        ReqCRDTState = 2,

        /// <summary>Special signal to send CRDT State to a peer.</summary>
        ResCRDTState = 3,
    }
}

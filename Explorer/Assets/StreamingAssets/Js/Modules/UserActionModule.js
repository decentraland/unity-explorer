//only for compatability with sdk 6 
//@deprecated, only available for SDK6 compatibility. Use RestrictedActions/TeleportTo

// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/user_action_module.proto

module.exports.requestTeleport = async function(message) {
    UnityUserActions.RequestTeleport(message.destination)
    return {};
}
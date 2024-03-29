//only for compatability with sdk 6 
//@deprecated, only available for SDK6 compatibility. Use RestrictedActions/TeleportTo


module.exports.requestTeleport = async function(message) {
    UnityUserActions.RequestTeleport(message.destination)
    return {};
}
module.exports.movePlayerTo = async function(message) {
    if (message.cameraTarget != undefined) {
        UnityRestrictedActionsApi.MovePlayerTo(
            message.newRelativePosition.x,
            message.newRelativePosition.y,
            message.newRelativePosition.z,
            message.cameraTarget.x,
            message.cameraTarget.y,
            message.cameraTarget.z)
    } else {
        UnityRestrictedActionsApi.MovePlayerTo(
            message.newRelativePosition.x,
            message.newRelativePosition.y,
            message.newRelativePosition.z)
    }
    
    return {};
}

module.exports.teleportTo = async function(message) {
    console.log('JSMODULE: teleportTo')
    return {};
}

module.exports.triggerEmote = async function(message) {
    console.log('JSMODULE: triggerEmote')
    return {};
}

module.exports.changeRealm = async function(message) {
    console.log('JSMODULE: changeRealm')
    return {};
}

module.exports.openExternalUrl = async function(message) {
    const isSuccess = UnityRestrictedActionsApi.OpenExternalUrl(message.url)
    return {
        success: isSuccess
    };
}

module.exports.openNftDialog = async function(message) {
    console.log('JSMODULE: openNftDialog')
    return {};
}

module.exports.setCommunicationsAdapter = async function(message) {
    console.log('JSMODULE: setCommunicationsAdapter')
    return {};
}

module.exports.triggerSceneEmote = async function(message) {
    console.log('JSMODULE: triggerSceneEmote')
    return {};
}
module.exports.movePlayerTo = async function(message) {
    const cameraTarget = message.cameraTarget != undefined
    const avatarTarget = message.avatarTarget != undefined
    
    UnityRestrictedActionsApi.MovePlayerTo(
        message.newRelativePosition.x,
        message.newRelativePosition.y,
        message.newRelativePosition.z,
        cameraTarget ? message.cameraTarget.x : null,
        cameraTarget ? message.cameraTarget.y : null,
        cameraTarget ? message.cameraTarget.z : null,
        avatarTarget ? message.avatarTarget.x : null,
        avatarTarget ? message.avatarTarget.y : null,
        avatarTarget ? message.avatarTarget.z : null)
    
    return {};
}

module.exports.teleportTo = async function(message) {
    const x = Number(message.worldCoordinates.x);
    const y = Number(message.worldCoordinates.y);
    UnityRestrictedActionsApi.TeleportTo(x, y);
    return {};
}

module.exports.triggerEmote = async function(message) {
    UnityRestrictedActionsApi.TriggerEmote(message.predefinedEmote)
    return {};
}

module.exports.changeRealm = async function(message) {
    if (message.message == undefined) {
        message.message = ''
    }
    const isSuccess = UnityRestrictedActionsApi.ChangeRealm(message.message, message.realm)
    return {
        success: isSuccess
    };
}

module.exports.openExternalUrl = async function(message) {
    const isSuccess = UnityRestrictedActionsApi.OpenExternalUrl(message.url)
    return {
        success: isSuccess
    };
}

module.exports.openNftDialog = async function(message) {
    const isSuccess = UnityRestrictedActionsApi.OpenNftDialog(message.urn)
    return {
        success: isSuccess
    };
}

module.exports.setCommunicationsAdapter = async function(message) {
    console.log('JSMODULE: setCommunicationsAdapter')
    return {
        success: false
    };
}

module.exports.triggerSceneEmote = async function(message) {
    if (message.loop == undefined) {
        message.loop = false
    }
    const isSuccess = await UnityRestrictedActionsApi.TriggerSceneEmote(message.src, message.loop)
    return {
        success: isSuccess
    };
}
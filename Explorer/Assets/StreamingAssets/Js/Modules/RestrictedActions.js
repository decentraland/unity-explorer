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
    UnityRestrictedActionsApi.TeleportTo(
        message.worldCoordinates.x,
        message.worldCoordinates.y
    )
    
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
    const isSuccess = UnityRestrictedActionsApi.TriggerSceneEmote(message.src, message.loop)
    return {
        success: isSuccess
    };
}
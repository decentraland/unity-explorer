// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/restricted_actions.proto

// Protocol enums exposed to scenes as runtime values (mirroring the generated
// ts-proto enums: both forward name->value and reverse value->name mappings).
// Scenes import these from `~system/RestrictedActions` and use them at runtime,
// e.g. `openExplorerUi({ ui: ExplorerUi.EU_MAP })` and `OpenExplorerUiResult[openResult]`.
function makeEnum(entries) {
    const e = {};
    for (const [name, value] of entries) { e[name] = value; e[value] = name; }
    return e;
}

module.exports.ExplorerUi = makeEnum([
    ['EU_SETTINGS', 0],
    ['EU_MAP', 1],
    ['EU_BACKPACK', 2],
    ['EU_CAMERA_REEL', 3],
    ['EU_COMMUNITIES', 4],
    ['EU_PLACES', 5],
    ['EU_EVENTS', 6],
]);

module.exports.OpenExplorerUiResult = makeEnum([
    ['UNSPECIFIED', 0],
    ['OPENED', 1],
    ['WAS_ALREADY_OPEN', 2],
    ['REJECTED_NOT_CURRENT_SCENE', 3],
    ['REJECTED_FEATURE_DISABLED', 4],
    ['REJECTED_NO_USER_GESTURE', 5],
]);

module.exports.movePlayerTo = async function(message) {
    const cameraTarget = message.cameraTarget != undefined
    const avatarTarget = message.avatarTarget != undefined
    const duration = message.duration != undefined

    // A present target must carry all three components: the host method takes plain doubles, so a
    // null or undefined component cannot be bound and is rejected here instead of failing inside interop.
    const isCompleteVector3 = v => v.x != undefined && v.y != undefined && v.z != undefined
    if ((cameraTarget && !isCompleteVector3(message.cameraTarget)) || (avatarTarget && !isCompleteVector3(message.avatarTarget)))
        return { success: false }

    // Each optional target is sent as a presence flag followed by its coordinates (0 when absent).
    const isSuccess = await UnityRestrictedActionsApi.MovePlayerTo(
        message.newRelativePosition.x,
        message.newRelativePosition.y,
        message.newRelativePosition.z,
        cameraTarget,
        cameraTarget ? message.cameraTarget.x : 0,
        cameraTarget ? message.cameraTarget.y : 0,
        cameraTarget ? message.cameraTarget.z : 0,
        avatarTarget,
        avatarTarget ? message.avatarTarget.x : 0,
        avatarTarget ? message.avatarTarget.y : 0,
        avatarTarget ? message.avatarTarget.z : 0,
        duration ? message.duration : null)
    
    return {
        success: isSuccess
    };
}

module.exports.teleportTo = async function(message) {
    // Coordinates are sent as a presence flag followed by x and y (0 when absent); realm is nullable.
    const coords = message.worldCoordinates
    const hasCoords = coords != undefined
    UnityRestrictedActionsApi.TeleportTo(
        hasCoords,
        hasCoords ? Number(coords.x) : 0,
        hasCoords ? Number(coords.y) : 0,
        message.realm != undefined ? message.realm : null);
    return {};
}

module.exports.triggerEmote = async function(message) {
    // mask is an optional AvatarMask enum: absent means full-body
    await UnityRestrictedActionsApi.TriggerEmote(
        message.predefinedEmote,
        message.mask != undefined ? message.mask : null);
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

module.exports.openExplorerUi = async function(message) {
    // requestId is optional, and 0 is the protocol's value for "no correlation"
    const openResult = await UnityRestrictedActionsApi.OpenExplorerUi(
        message.ui,
        message.requestId != undefined ? message.requestId : 0)
    return { openResult };
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
    // mask is an optional AvatarMask enum: absent means full-body
    const isSuccess = await UnityRestrictedActionsApi.TriggerSceneEmote(
        message.src,
        message.loop,
        message.mask != undefined ? message.mask : null);
    return {
        success: isSuccess
    };
}

module.exports.stopEmote = async function(message) {
    const isSuccess = await UnityRestrictedActionsApi.StopEmote()
    return {
        success: isSuccess
    };
}

module.exports.copyToClipboard = async function(message) {
    UnityRestrictedActionsApi.CopyToClipboard(message.text)
    return {};
}
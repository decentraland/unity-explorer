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

// Names carry the OIP_ prefix because the proto does: proto3 enum values are siblings of their enum,
// so bare names would collide with OpenExplorerUiResult. They MUST match the generated SDK type, or a
// scene reading OpenItemPurchaseResult.OIP_PURCHASED would typecheck and get undefined at runtime.
module.exports.OpenItemPurchaseResult = makeEnum([
    ['OIP_UNSPECIFIED', 0],
    ['OIP_PURCHASED', 1],
    ['OIP_DISMISSED', 2],
    ['OIP_REJECTED_NOT_CURRENT_SCENE', 3],
    ['OIP_REJECTED_NO_USER_GESTURE', 4],
    ['OIP_REJECTED_FEATURE_DISABLED', 5],
    ['OIP_REJECTED_NOT_PURCHASABLE', 6],
    ['OIP_FAILED', 7],
]);

module.exports.movePlayerTo = async function(message) {
    const cameraTarget = message.cameraTarget != undefined
    const avatarTarget = message.avatarTarget != undefined
    const duration = message.duration != undefined
    
    const isSuccess = await UnityRestrictedActionsApi.MovePlayerTo(
        message.newRelativePosition.x,
        message.newRelativePosition.y,
        message.newRelativePosition.z,
        cameraTarget ? message.cameraTarget.x : null,
        cameraTarget ? message.cameraTarget.y : null,
        cameraTarget ? message.cameraTarget.z : null,
        avatarTarget ? message.avatarTarget.x : null,
        avatarTarget ? message.avatarTarget.y : null,
        avatarTarget ? message.avatarTarget.z : null,
        duration ? message.duration : null)
    
    return {
        success: isSuccess
    };
}

module.exports.teleportTo = async function(message) {
    const x = Number(message.worldCoordinates.x);
    const y = Number(message.worldCoordinates.y);
    UnityRestrictedActionsApi.TeleportTo(x, y);
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
    const openResult = UnityRestrictedActionsApi.OpenExplorerUi(message.ui)
    return { openResult };
}

// The scene supplies ONLY the item URN: the client resolves the price from the catalog, signs and
// confirms. Deliberately coarse verdict -- "insufficient credits" is folded into FAILED so scene
// code cannot probe a wallet by attempting purchases.
module.exports.openItemPurchase = async function(message) {
    const result = await UnityRestrictedActionsApi.OpenItemPurchase(message.urn)
    return { result };
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
    const isSuccess = UnityRestrictedActionsApi.StopEmote()
    return {
        success: isSuccess
    };
}

module.exports.copyToClipboard = async function(message) {
    UnityRestrictedActionsApi.CopyToClipboard(message.text)
    return {};
}
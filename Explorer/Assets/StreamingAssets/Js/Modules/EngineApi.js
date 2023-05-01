// engine module
module.exports.crdtSendToRenderer = async function(messages) {
    const data = new Uint8Array(await UnityEngineApi.CrdtSendToRenderer(messages.data))
    return {
        data
    };
}

module.exports.sendBatch = async function() {
    return { events: [] }
}

module.exports.crdtGetState = async function() {
    const data = new Uint8Array(await UnityEngineApi.CrdtGetState())
    return {
        data
    };
}
// engine module
module.exports.crdtSendToRenderer = async function(messages) {
    const data = new Uint8Array(UnityEngineApi.CrdtSendToRenderer(messages.data))
    return {
        data: [data]
    };
}

module.exports.sendBatch = async function() {
    return { events: [] }
}

module.exports.crdtGetState = async function() {
    const data = new Uint8Array(UnityEngineApi.CrdtGetState())
    return {
        data: [data]
    };
}
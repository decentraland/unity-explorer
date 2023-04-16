// engine module
module.exports.crdtSendToRenderer = async function(messages) {
    await UnityEngineApi.CrdtSendToRenderer(messages.data);
    return {
        data: new Uint8Array()
    };
}

module.exports.sendBatch = async function() {
    return { events: [] }
}

module.exports.crdtGetState = async function() {
    await UnityEngineApi.CrdtGetState();
    return {
        data: new Uint8Array()
    };
}
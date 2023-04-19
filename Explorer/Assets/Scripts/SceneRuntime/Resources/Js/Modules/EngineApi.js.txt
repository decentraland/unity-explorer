// engine module
module.exports.crdtSendToRenderer = async function(messages) {
    return {
        data: await UnityEngineApi.CrdtSendToRenderer(messages.data)
    };
}

module.exports.sendBatch = async function() {
    return { events: [] }
}

module.exports.crdtGetState = async function() {
    return {
        data: await UnityEngineApi.CrdtGetState()
    };
}
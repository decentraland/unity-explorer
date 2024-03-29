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
        hasEntities: true //TODO replace with actual value
    };
}

module.exports.subscribe = async function(message) {
    console.log(`JSMODULE: EngineApi.subscribe(${message.eventId}): deprecated`)
}

module.exports.unsubscribe = async function(message) {
    console.log(`JSMODULE: EngineApi.unsubscribe(${message.eventId}): deprecated`)
}

module.exports.isServer = async function () {
    return {
        isServer: false
    }
}
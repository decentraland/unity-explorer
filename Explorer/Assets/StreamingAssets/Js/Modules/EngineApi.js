// engine module
module.exports.crdtSendToRenderer = async function(messages) {
    const data = new Uint8Array(UnityEngineApi.CrdtSendToRenderer(messages.data))
    return {
        data: [data]
    };
}

module.exports.sendBatch = async function() {
    const data = UnityEngineApi.SendBatch()
    // console.pravslog(`PRAVS - sendBatch() - ${data}`, data)
    return { 
        events: data
    };
}

module.exports.crdtGetState = async function() {
    const data = new Uint8Array(UnityEngineApi.CrdtGetState())
    return {
        data: [data]
    };
}

module.exports.subscribe = async function(data) {
    UnityEngineApi.SubscribeToSDKObservableEvent(data.eventId)
    return {};
}
module.exports.unsubscribe = async function(data) {
    UnityEngineApi.UnsubscribeFromSDKObservableEvent(data.eventId)
    return {};
}
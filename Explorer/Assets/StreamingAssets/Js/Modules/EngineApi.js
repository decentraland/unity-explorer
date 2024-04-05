// engine module
module.exports.crdtSendToRenderer = async function(messages) {
    const data = new Uint8Array(UnityEngineApi.CrdtSendToRenderer(messages.data))
    return {
        data: [data]
    };
}

module.exports.sendBatch = async function() {    
    return { 
        events: UnityEngineApi.SendBatch() 
    };
}

module.exports.crdtGetState = async function() {
    const data = new Uint8Array(UnityEngineApi.CrdtGetState())
    return {
        data: [data]
    };
}

module.exports.subscribe = async function(data) {
    UnityEngineApi.SubscribeToObservableEvent(data.eventId)
    return {};
}
module.exports.unsubscribe = async function(data) {
    UnityEngineApi.UnsubscribeFromObservableEvent(data.eventId)
    return {};
}
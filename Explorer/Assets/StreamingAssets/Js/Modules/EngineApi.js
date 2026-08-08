// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/engine_api.proto

module.exports.crdtSendToRenderer = async function(messages) {
    // The returned value is already a Uint8Array created and bulk-filled on the C# side:
    // wrapping it in `new Uint8Array(...)` would copy it byte by byte through the interop boundary
    const data = UnityEngineApi.CrdtSendToRenderer(messages.data)
    return {
        data: data ? [data] : []
    };
}

module.exports.sendBatch = async function() {
    const data = UnityEngineApi.SendBatch()
    if(!data) {
        return {
            events: []
        };
    } else {
        return {
            events: data
        };
    }
}

module.exports.crdtGetState = async function() {
    const data = UnityEngineApi.CrdtGetState()
    return {
        data: data ? [data] : [],
        hasEntities: true //TODO replace with actual value
    };
}

module.exports.subscribe = async function(message) {
    console.log(`JSMODULE: EngineApi.subscribe(${message.eventId}): deprecated`)
    UnityEngineApi.SubscribeToSDKObservableEvent(message.eventId)
    return {}
}

module.exports.unsubscribe = async function(message) {
    console.log(`JSMODULE: EngineApi.unsubscribe(${message.eventId}): deprecated`)
    UnityEngineApi.UnsubscribeFromSDKObservableEvent(message.eventId)
    return {}
}

module.exports.isServer = async function () {
    return {
        isServer: false
    }
}
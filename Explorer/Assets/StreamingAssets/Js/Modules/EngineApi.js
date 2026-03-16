// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/engine_api.proto

module.exports.crdtSendToRenderer = async function(messages) {
    let inputData = messages.data;
    // P2P transport may accumulate messages into an array of Uint8Arrays — merge into one
    if (Array.isArray(inputData)) {
        let totalLen = 0;
        for (let i = 0; i < inputData.length; i++) {
            if (inputData[i] && inputData[i].byteLength) totalLen += inputData[i].byteLength;
        }
        const merged = new Uint8Array(totalLen);
        let offset = 0;
        for (let i = 0; i < inputData.length; i++) {
            if (inputData[i] && inputData[i].byteLength) {
                merged.set(inputData[i], offset);
                offset += inputData[i].byteLength;
            }
        }
        inputData = merged;
    }
    const result = UnityEngineApi.CrdtSendToRenderer(inputData);
    const data = result != null ? new Uint8Array(result) : new Uint8Array(0);
    return {
        data: [data]
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
    const result = UnityEngineApi.CrdtGetState();
    const data = result != null ? new Uint8Array(result) : new Uint8Array(0);
    return {
        data: [data],
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
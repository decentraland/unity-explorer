// engine module
module.exports.crdtSendToRenderer = async function(messages) {
    const data = new Uint8Array(UnityEngineApi.CrdtSendToRenderer(messages.data))
    return {
        data: [data]
    };
}

const unityEvents = []
const sceneStart = {
    generic: {
        eventId: 'sceneStart',
        eventData: "{}"
    }
}
unityEvents.push(sceneStart)

module.exports.sendBatch = async function() {
    // console.log('PRAVS - sendBatch() - unityEvents...', unityEvents)
    
    // clear events
    const eventsCopy = unityEvents.map((x) => x)
    if (eventsCopy.length) {
        unityEvents.length = 0
    }
    
    return { events: eventsCopy }
}

module.exports.crdtGetState = async function() {
    const data = new Uint8Array(UnityEngineApi.CrdtGetState())
    return {
        data: [data]
    };
}

module.exports.subscribe = async function() {
    return {}
}
module.exports.unsubscribe = async function() {
    return {}
}
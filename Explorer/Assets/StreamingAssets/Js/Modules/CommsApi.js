// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/comms_api.proto

// Returns: { streams: Array<{ identity, trackSid, sourceType, name, speaking, trackName, width, height }> }
module.exports.getActiveVideoStreams = async function () {
    const json = CommsApi.GetActiveVideoStreams();
    const result = JSON.parse(json);
    return result;
}

// message: { topic: string, data: Uint8Array }
module.exports.publishData = async function (message) {
    CommsApi.PublishData(message.topic, message.data);
}

// message: { metadata: string }
module.exports.updateMetadata = async function (message) {
    CommsApi.UpdateMetadata(message.metadata);
}

// message: { topic: string }
module.exports.subscribeToTopic = async function (message) {
    CommsApi.SubscribeToTopic(message.topic);
}

// message: { topic: string }
// Returns: Array<{ sender: string, data: string }>
module.exports.consumeMessages = async function (message) {
    const json = CommsApi.ConsumeMessages(message.topic);
    return JSON.parse(json);
}
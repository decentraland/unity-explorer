// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/comms_api.proto

// Returns: { streams: Array<{ identity, trackSid, sourceType, name, speaking, trackName, width, height }> }
module.exports.getActiveVideoStreams = async function () {
    const json = CommsApi.GetActiveVideoStreams();
    const result = JSON.parse(json);
    return result;
}

// message: { topic: string, data: string }
// Publish is rate-limited per topic (MAX_MESSAGES_PER_SECOND).
// If the rate limit is exceeded or payload constraints fail, the message is silently dropped.
module.exports.publishData = async function (message) {
    CommsApi.PublishData(message.topic, message.data);
}

// message: { topic: string }
module.exports.subscribeToTopic = async function (message) {
    CommsApi.SubscribeToTopic(message.topic);
}

// message: { topic: string }
module.exports.unsubscribeFromTopic = async function (message) {
    CommsApi.UnsubscribeFromTopic(message.topic);
}

// message: { topic: string }
// Returns: Array<{ sender: string, data: string }>
// Implements a drop-old policy: when the buffer is full, oldest messages are discarded.
// Expected to be polled regularly; otherwise messages may be lost.
// Stores up to TOPIC_BUFFER_MAX_MESSAGE_COUNT messages per topic.
module.exports.consumeMessages = async function (message) {
    const json = CommsApi.ConsumeMessages(message.topic);
    return JSON.parse(json);
}

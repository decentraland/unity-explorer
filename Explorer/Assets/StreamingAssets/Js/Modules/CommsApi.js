// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/comms_api.proto

module.exports.getActiveVideoStreams = async function () {
    const json = CommsApi.GetActiveVideoStreams();
    const result = JSON.parse(json);
    return result;
}

module.exports.publishData = async function (message) {
    CommsApi.PublishData(message.topic, message.data);
}

module.exports.updateMetadata = async function (message) {
    CommsApi.UpdateMetadata(message.metadata);
}

module.exports.subscribeToTopic = async function (message) {
    CommsApi.SubscribeToTopic(message.topic);
}

module.exports.consumeMessages = async function (message) {
    const json = CommsApi.ConsumeMessages(message.topic);
    return JSON.parse(json);
}
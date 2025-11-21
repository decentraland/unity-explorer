// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/comms_api.proto

module.exports.getActiveVideoStreams = async function () {
    const json = CommsApi.GetActiveVideoStreams();
    const result = JSON.parse(json);
    return result;
}
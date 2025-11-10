// Responses should always correspond to the protocol definitions at
// https://github.com/decentraland/protocol/blob/main/proto/decentraland/kernel/apis/communications_controller.proto

module.exports.sendBinary = async function(message) {
    var peerData = message.peerData
    if (peerData === undefined)
        peerData = null
    
    const resultData = UnityCommunicationsControllerApi.SendBinary(message.data, peerData)
    return {
        data: resultData
    };
}

// Needed for scenes own MessageBus through 'comms' observable
module.exports.send = async function(message) {
    UnitySDKMessageBusCommsControllerApi.Send(message.message)
    return {};
}
module.exports.sendBinary = async function(message) {
    const resultData = UnityCommunicationsControllerApi.SendBinary(message.data)
    return {
        data: resultData
    };
}

// Needed for scenes own MessageBus through 'comms' observable
module.exports.send = async function(message) { 
    UnitySDKMessageBusCommsControllerApi.Send(message.message)
    return {};
}
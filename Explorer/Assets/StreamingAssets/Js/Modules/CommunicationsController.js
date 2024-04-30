module.exports.sendBinary = async function(message) {
    const resultData = UnityCommunicationsControllerApi.SendBinary(message.data)
    return {
        data: resultData
    };
}

// Needed for COMMS Messagebus support
module.exports.send = async function(message) {
    // message.message
    // message.payload
    // TODO: message variable should be converted to Uint8Array/ByteArray as Comms only parses that
    
    UnityCommunicationsControllerApi.Send(message.message)
    
    return {};
}
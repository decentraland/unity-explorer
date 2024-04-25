module.exports.sendBinary = async function(message) {
    const resultData = UnityCommunicationsControllerApi.SendBinary(message.data)
    return {
        data: resultData
    };
}

// Needed for COMMS Messagebus support
module.exports.send = async function(message) {
    console.warning("CommunicationsController.send is not implemented")

    // message.message
    // message.payload    
    // TODO: message variable should be converted to Uint8Array/ByteArray as Comms only parses that
    
    return {};
}
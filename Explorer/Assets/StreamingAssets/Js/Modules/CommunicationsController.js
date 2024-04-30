module.exports.sendBinary = async function(message) {
    const resultData = UnityCommunicationsControllerApi.SendBinary(message.data)
    return {
        data: resultData
    };
}

// Needed for COMMS Messagebus support
module.exports.send = async function(message) {    
    UnityCommunicationsControllerApi.Send(message.message)    
    return {};
}
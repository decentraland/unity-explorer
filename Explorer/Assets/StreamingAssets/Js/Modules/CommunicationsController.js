module.exports.sendBinary = async function(message) {
    const resultData = UnityCommunicationsControllerApi.SendBinary(message.data)
    return {
        data: resultData
    };
}

module.exports.send = async function(message) {
    console.warning("CommunicationsController.send is not implemented")
    return {};
}
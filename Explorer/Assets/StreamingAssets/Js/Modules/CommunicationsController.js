module.exports.sendBinary = async function(message) {
    const resultData = UnityCommunicationsControllerApi.SendBinary(message.data)
    return {
        data: resultData
    };
}
module.exports.sendAsync = async function(message) {
    const result = await UnityEthereumApi.SendAsync(message.id, message.method, message.jsonParams)
    return result;
}
module.exports.sendAsync = async function(message) {
    const result = await UnityEngineApi.SendEthereumMessageAsync(message.id, message.method, JSON.stringify(message.params))
    return result;
}